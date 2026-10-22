#Requires -Version 7.0
# Use pwsh 7; Windows PowerShell 5.1 does not support ProcessStartInfo.ArgumentList.
param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [ValidateRange(10, 600)][int]$TimeoutSeconds = 120
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Assert-ProbeApprovals($Approval, [string]$ExpectedRunId) {
    if ($Approval.runId -ne $ExpectedRunId) { throw 'Approval package targets another run.' }
    $contract = if ($Approval.PSObject.Properties['contract']) { $Approval.contract } else { $null }
    if ($null -ne $contract -and $contract -ne 'v4') { throw 'Unsupported approval contract.' }
    if ($contract -eq 'v4' -and -not $ExpectedRunId.StartsWith('gcam-', [StringComparison]::Ordinal)) {
        throw 'New v4 run ID must begin with gcam-.'
    }
    $acts = @('charter', 'real-world-effect', 'final-review', 'physical', 'desktop-go')
    if ($contract -eq 'v4' -and @($Approval.approvals | Where-Object { $_.humanActId -notin $acts }).Count) {
        throw 'New approval records require named acts.'
    }
    foreach ($act in @('desktop-go', 'charter')) {
        $legacy = if ($act -eq 'desktop-go') { 'HUM-01' } else { 'HUM-04' }
        $exactLegacy = $null -eq $contract -and @($Approval.approvals | Where-Object { $_.humanActId -eq $legacy }).Count -gt 0
        $records = @($Approval.approvals | Where-Object {
            if ($exactLegacy) { $_.humanActId -eq $legacy } else { $_.humanActId -eq $act }
        })
        if ($records.Count -ne 1 -or [string]::IsNullOrWhiteSpace($records[0].id) -or
            [string]::IsNullOrWhiteSpace($records[0].quote) -or $records[0].targetCommit -notmatch '^[0-9a-f]{40}$' -or
            $records[0].targetCommit -ne $Approval.commit -or
            [DateTimeOffset]::Parse($records[0].quotedAt) -lt [DateTimeOffset]::Parse($Approval.committedAt)) {
            throw "Approval act/change/date is incomplete: $act"
        }
    }
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'An administrator must run only the tracing wrapper; the harness stays non-elevated.'
}
$root = [IO.Path]::GetFullPath((Join-Path $env:TEMP 'gcam-todo43'))
$destination = [IO.Path]::GetFullPath($RunDirectory)
if (-not $destination.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Trace destination is outside the declared scratch root.'
}
$runId = [IO.Path]::GetFileName($destination)
if ($runId -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]{0,70}$') { throw 'Invalid run name.' }
if (-not (Test-Path -LiteralPath (Join-Path $destination 'approvals.json'))) { throw 'Prepare the reviewed approval package first.' }
$cursor = [IO.DirectoryInfo]::new($destination)
while ($null -ne $cursor -and $cursor.FullName.Length -ge $root.Length) {
    if ($cursor.Exists -and ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse point in trace destination.' }
    $cursor = $cursor.Parent
}
if ((Get-Item -LiteralPath (Join-Path $destination 'approvals.json')).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse approval source.' }
$approval = Get-Content -LiteralPath (Join-Path $destination 'approvals.json') -Raw | ConvertFrom-Json
Assert-ProbeApprovals $approval $runId
$toolNumber = 0
function Invoke-OwnedWpr([string[]]$WprArguments, [int]$DeadlineSeconds = 60) {
    $script:toolNumber++
    $start = [Diagnostics.ProcessStartInfo]::new('wpr.exe')
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $WprArguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($DeadlineSeconds * 1000)) { throw 'Owned WPR command timed out; no process is force-killed and session state needs author resolution.' }
        [IO.File]::WriteAllText((Join-Path $destination ('wpr-tool-' + $script:toolNumber + '.log')), $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult())
        if ($process.ExitCode -ne 0) { throw 'Owned WPR command failed; no other session is stopped.' }
    } finally { $process.Dispose() }
}
$complete = Join-Path $destination 'trace.complete.json'
if (Test-Path -LiteralPath $complete) {
    $prior = Get-Content -LiteralPath $complete -Raw | ConvertFrom-Json
    if ($prior.runId -ne $runId) { throw 'Foreign completion signal.' }
    Write-Output 'This trace is already complete; no session was touched.'
    return
}
$lock = [IO.FileStream]::new((Join-Path $destination 'trace.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
$started = $false
$instance = 'gcam-' + $runId
$etl = Join-Path $destination 'probe.etl'
try {
    if (Test-Path -LiteralPath $etl) { throw 'Existing ETL is retained; choose a new run name.' }
    if (Test-Path -LiteralPath (Join-Path $destination 'trace.ready.json')) { throw 'Prior trace state is unresolved; do not adopt or cancel it automatically.' }
    $profile = Join-Path $PSScriptRoot 'Probe.wprp'
    # All commands use this instance; never cancel an existing global WPR session.
    Invoke-OwnedWpr @('-start', ($profile + '!GcamProbe.Verbose'), '-filemode', '-recordtempto', $destination, '-instancename', $instance)
    $started = $true
    [IO.File]::WriteAllText((Join-Path $destination 'trace.ready.json'), (@{runId=$runId; instance=$instance; startedAt=[DateTimeOffset]::UtcNow.ToString('O')} | ConvertTo-Json) + "`n")
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $signal = Join-Path $destination 'harness.complete.json'
    while (-not (Test-Path -LiteralPath $signal) -and $watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) { Start-Sleep -Milliseconds 200 }
    if (-not (Test-Path -LiteralPath $signal)) { throw 'Harness completion timed out; the owned trace will still be saved.' }
    if ((Get-Content -LiteralPath $signal -Raw | ConvertFrom-Json).runId -ne $runId) { throw 'Completion signal run ID mismatch.' }
} finally {
    try {
        if ($started) {
            Invoke-OwnedWpr @('-stop', $etl, 'GCAM launch idle exit probe', '-skipPdbGen', '-instancename', $instance)
            [IO.File]::WriteAllText($complete, (@{runId=$runId; instance=$instance; etlSha256=(Get-FileHash -LiteralPath $etl -Algorithm SHA256).Hash.ToLowerInvariant(); finishedAt=[DateTimeOffset]::UtcNow.ToString('O')} | ConvertTo-Json) + "`n")
        }
    } finally { $lock.Dispose() }
}

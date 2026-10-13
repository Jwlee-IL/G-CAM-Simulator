param([Parameter(Mandatory)][string]$RunDirectory, [ValidateSet('Before','After','Watch')][string]$Phase,
    [ValidateRange(1,600)][int]$TimeoutSeconds = 120)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $env:TEMP 'gcam-todo43')) + [IO.Path]::DirectorySeparatorChar
$destination = [IO.Path]::GetFullPath($RunDirectory)
if (-not $destination.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unowned fallback output root.' }
if ($Phase -eq 'Watch') {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $outputPath = Join-Path $destination 'fallback-watch.jsonl'
    if (Test-Path -LiteralPath $outputPath) { throw 'Existing fallback watch is retained; use a new run.' }
    while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds -and -not (Test-Path -LiteralPath (Join-Path $destination 'harness.complete.json'))) {
        $owned = @(Get-Process Gcam.Studio -ErrorAction SilentlyContinue | Select-Object Id,StartTime)
        $ids = @($owned | ForEach-Object { $_.Id })
        $tcp = @(Get-NetTCPConnection -ErrorAction SilentlyContinue | Where-Object { $_.OwningProcess -in $ids } | Select-Object OwningProcess,LocalAddress,LocalPort,RemoteAddress,RemotePort,State)
        $sample = @{at=[DateTimeOffset]::UtcNow.ToString('O'); processes=$owned; tcp=$tcp; g1Eligible=$false;
            blindSpots=@('200 ms polling and cmdlet cost miss short-lived events', 'TCP only; UDP and shell-mediated effects not attributed')}
        [IO.File]::AppendAllText($outputPath, ($sample | ConvertTo-Json -Compress -Depth 6) + "`n")
        Start-Sleep -Milliseconds 200
    }
    return
}
# This script is prepared only. Registry reads are never part of the headless tests.
$keys = @('Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs', 'Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU')
$registry = @{}
foreach ($name in $keys) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($name, $false)
    if ($null -eq $key) { $registry[$name] = @{}; continue }
    try {
        $values = @{}
        foreach ($valueName in $key.GetValueNames()) { $values[$valueName] = $key.GetValue($valueName) }
        $registry[$name] = $values
    } finally { $key.Dispose() }
}
$owned = Get-ChildItem -LiteralPath $destination -File -Recurse | ForEach-Object {
    @{path=$_.FullName.Substring($destination.Length + 1); sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$processes = @(Get-Process Gcam.Studio -ErrorAction SilentlyContinue | Select-Object Id,StartTime)
$connections = @(Get-NetTCPConnection -ErrorAction SilentlyContinue | Where-Object { $_.OwningProcess -in $processes.Id } | Select-Object OwningProcess,LocalAddress,LocalPort,RemoteAddress,RemotePort,State)
$record = @{schemaVersion=1; phase=$Phase; at=[DateTimeOffset]::UtcNow.ToString('O'); registry=$registry; ownedFiles=@($owned); processes=$processes; connections=$connections;
    g1Eligible=$false; blindSpots=@('Selected HKCU keys only, no positive registry control', 'Polling misses short-lived connections and child processes', 'No whole-machine write attribution', 'Owned-folder snapshots miss transient writes')}
[IO.File]::WriteAllText((Join-Path $destination ('fallback-' + $Phase.ToLowerInvariant() + '.json')), ($record | ConvertTo-Json -Depth 12) + "`n")

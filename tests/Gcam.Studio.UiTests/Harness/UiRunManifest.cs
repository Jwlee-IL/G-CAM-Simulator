using System.Text.Json;
using Gcam.Studio.UiTests.Harness.Isolation;
using Gcam.Studio.UiTests.Harness.Observation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Execution safety, bundle qualification and product outcomes are separate axes.</summary>
public sealed class UiRunManifest(string runId, string scenario)
{
    public sealed record Gate(string Id, string Status, string Reason);
    public sealed record Issue(string Type, string Reason);
    public sealed record ReviewPlan(bool Required, string[] Scope, string SelectionRule, string[] HumanActIds);
    public sealed record Bundle(string Id, string ResultUse, string Status, string Reason, Gate[] Gates, ReviewPlan ReviewPlan, string[] ReviewIds);
    public sealed record ScenarioResult(string Id, string Status, string Reason, string? SkipConditionId);
    public sealed record Conditional(string Id, string Activation, string EvaluationLevel, string BundleId, string Status, string Basis);
    /// <summary>3 (TODO-52): an observed run's <c>writeAudit</c> adds <c>observationScope</c>, <c>unattributed</c> and
    /// <c>controlWindow</c> (UI-automation procedure v3.3). Records written earlier keep their version.</summary>
    public const int SchemaVersion = 3;
    public string RunId { get; } = runId;
    public string ManifestPhase { get; private set; } = "in_progress";
    public string RunStatus { get; private set; } = "blocked";
    public List<Issue> RunIssues { get; } = [];
    public List<Issue> ProductIssues { get; } = [];
    public static Issue[] LivenessIssues(LivenessResult? result)
    {
        if (result is null) return [];
        var issues = new List<Issue>();
        if (result.LaunchFailed) issues.Add(new("launch_window_timeout", (result.WindowSeen ? "main window not ready" : "main window absent")
            + "; startup seconds=" + result.StartupSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)));
        if (result.Breached) issues.Add(new("not_responding", "continuous non-response limit breached; phase=" + result.Phase));
        if (result.EarlyExit) issues.Add(new("app_exited", "exit before intentional close; code=" + result.ExitCode));
        if (result.AbnormalExit) issues.Add(new("abnormal_exit", "exit code=" + result.ExitCode));
        return issues.ToArray();
    }
    public void RecordLiveness(LivenessResult? result) => ProductIssues.AddRange(LivenessIssues(result));
    public Dictionary<string, object?> Evidence { get; } = [];
    public object? WriteAudit { get; set; }
    private ProbePackage? _package;
    private ProbePackage? _probeCharter;
    private OwnedStudioProcess.PreflightResult? _preflight;
    private bool _cleanupComplete;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    /// <summary>Only the launch-idle-exit probe runs under the approved probe charter; its package is that charter.</summary>
    public void RecordApprovedProbe(ProbePackage package) => _package = _probeCharter = package;
    /// <summary>The launch package identifies the revision and grants; it makes no charter or gate claim for this scenario
    /// (the package that applies to other scenarios is wired with the v4 charter).</summary>
    public void RecordPreflight(OwnedStudioProcess.PreflightResult preflight)
    {
        var package = preflight.ApprovalPackage;
        _preflight = preflight with { ApprovalPackage = package is null ? null : package with { Contract = "v4", Approvals = package.LiveApprovals } };
        _package ??= _preflight.ApprovalPackage;
    }

    public void CleanupCompleted(IEnumerable<Issue> issues)
    {
        if (ManifestPhase == "final") throw new InvalidOperationException("Final manifest is immutable");
        RunIssues.AddRange(issues); _cleanupComplete = true;
    }

    public object Finalise(string productOutcome, string resultUse = "exploratory", bool observed = false, string? skipConditionId = null)
    {
        if (!_cleanupComplete || ManifestPhase == "final") throw new InvalidOperationException("Cleanup must precede one finalisation");
        if (productOutcome is not ("passed" or "failed" or "blocked" or "skipped")) throw new ArgumentException("Unknown scenario outcome");
        if (productOutcome == "skipped" && string.IsNullOrWhiteSpace(skipConditionId)) throw new InvalidDataException("Skipped scenario requires a condition ID");
        if (resultUse is not ("exploratory" or "regression" or "evidence" or "manual")) throw new ArgumentException("Unknown result use");
        if (RunIssues.Any(i => i.Type is not ("unsafe_effect" or "cleanup_failed" or "blocked"))) throw new InvalidDataException("Unknown run issue type");
        RunStatus = new[] { "unsafe_effect", "cleanup_failed", "blocked" }.FirstOrDefault(s => RunIssues.Any(i => i.Type == s)) ?? "completed";
        if (ProductIssues.Count != 0) productOutcome = "failed";
        if (RunStatus != "completed" && productOutcome == "passed") productOutcome = "blocked";
        ManifestPhase = "final";
        string basis = observed ? "g1-observed-run" : "qualification-not-established";
        var charterPackage = _probeCharter;
        var qualification = _package?.Charter?.ScenarioIds.Contains(scenario) == true ? _package.RequireScenario(scenario) : null;
        var conditionals = new[] { "SOURCE-AVAILABLE", "BINARY-TRACEABLE", "SOURCELESS-BINARY", "SEL-LOCALE", "SEL-A11Y",
            "INPUT-COORDINATE", "RUN-RESTART", "SESSION-REUSE", "DEVICE-SIMULATED", "SHARED-RESOURCE", "VERDICT-PIXEL",
            "VERDICT-ARTIFACT", "VERDICT-SUMMARY", "GATE-SKIP", "CAPTURE-OUTPUT", "IMAGE-GATE", "BASELINE", "CI-NO-RUNTIME",
            "REVIEW-REQUIRED", "ADOPTION-EXISTING" }.Select(id =>
        {
            bool run = id is "SOURCE-AVAILABLE" or "BINARY-TRACEABLE" or "SOURCELESS-BINARY" or "RUN-RESTART" or "SESSION-REUSE" or "SHARED-RESOURCE" or "CI-NO-RUNTIME" or "ADOPTION-EXISTING";
            bool derived = id is "SOURCELESS-BINARY" or "CAPTURE-OUTPUT" or "REVIEW-REQUIRED" or "ADOPTION-EXISTING";
            bool policy = id is "SEL-A11Y" or "DEVICE-SIMULATED";
            string status = id switch
            {
                "SOURCE-AVAILABLE" or "SHARED-RESOURCE" or "ADOPTION-EXISTING" => "applies",
                "BINARY-TRACEABLE" => _package is null ? "undetermined" : "applies",
                "SOURCELESS-BINARY" => _package is null ? "applies" : "not_applicable",
                "CAPTURE-OUTPUT" or "REVIEW-REQUIRED" => resultUse == "manual" ? "applies" : "not_applicable",
                "SESSION-REUSE" or "DEVICE-SIMULATED" or "BASELINE" => "not_applicable",
                _ => "undetermined",
            };
            return new Conditional(id, policy ? "policy" : derived ? "derived" : "observed", run ? "run" : "bundle", run ? "not_applicable" : "runtime", status,
                status == "undetermined" ? "unused: current evidence does not establish this condition" : "source/result-use/probe facts; see charter and runtime evidence");
        }).ToArray();
        return new
        {
            schema = "gcam.ui-run", schemaVersion = SchemaVersion, contract = "v4", RunId, ManifestPhase, RunStatus, runIssues = RunIssues.ToArray(),
            startedAt = _startedAt, finishedAt = DateTimeOffset.UtcNow, productIssues = ProductIssues.ToArray(),
            productStatus = ProductIssues.Count == 0 ? productOutcome : "failed",
            purpose = resultUse,
            preflight = _preflight,
            realWorldEffects = _preflight?.RealWorldEffects ?? [],
            product = new { name = "GCAM Studio", revision = _package?.Commit ?? "unknown", workingTree = _package is null ? "unknown" : "clean",
                deploymentSha256 = _package?.DeploymentSha256 ?? _preflight?.TestData.DeploymentSha256,
                fingerprintCoverage = new[] { ".dll", ".exe", ".json" } },
            automation = new { revision = _package?.Commit ?? "unknown", workingTree = _package is null ? "unknown" : "clean",
                assemblySha256 = IsolationPaths.Hash(File.ReadAllBytes(typeof(UiRunManifest).Assembly.Location)), toolVersions = new { runtime = Environment.Version.ToString() } },
            environment = new { os = Environment.OSVersion.VersionString, locale = "en", formattingCulture = System.Globalization.CultureInfo.CurrentCulture.Name,
                display = new { measured = false, reason = "initial qualification does not inspect presentation" } },
            inputs = Array.Empty<object>(),
            executionGates = qualification?.Gates.Select(g => new Gate(g.Id, g.Status, g.Reason)).ToArray()
                ?? new[] { charterPackage is null ? new Gate("G0", "blocked", "no-applicable-charter-package")
                : new Gate("G0", "passed-lite", "approved-probe-charter"), new Gate("G1", "blocked", basis) },
            recommendedPractices = new[] { new Gate("G2", "recommended", "measurement"), new Gate("G6", "recommended", "operation") },
            resultBundles = new[] { new Bundle("runtime", resultUse, "blocked", basis,
                new[] { "G3", "G4", "G5" }.Select(id => new Gate(id, "blocked", basis)).ToArray(),
                new(false, resultUse == "manual" ? new[] { "final-render", "new-or-changed-capture-template" } : [],
                    "final review follows production and does not block it", []), [] ) },
            charter = new { status = qualification?.Status ?? (charterPackage is not null ? "approved" : "draft"),
                scope = qualification?.Scope ?? (charterPackage is null ? null : "launch-idle-exit-probe-only"),
                manualStatus = "draft-not-approved", approvals = qualification is null ? charterPackage?.LiveApprovals ?? [] : _package!.LiveApprovals },
            adoption = new { mode = "existing", qualificationScopes = new[] { "isolation", "process-monitor", "probe-launch" }.Select(id => new
            {
                id, status = "blocked", allowedUses = Array.Empty<string>(),
                dependsOnScopeIds = id == "probe-launch" ? new[] { "isolation", "process-monitor" } : [],
                gateBasis = new[] { new { gate = "G1", basis = "gap", evidenceRef = "pending observed-run classification" } },
                gaps = new[] { new { id = id + "-qualification", owner = "planner", expiresAt = "2026-11-13", residualRisk = "runtime not qualified",
                    qualificationCondition = "approved observed basis and applicable pilot gates", allowedUses = Array.Empty<string>() } },
            }).ToArray() },
            conditionalRequirements = conditionals,
            conditionalRequirementsUsage = "unused diagnostic machinery; these entries do not establish execution gates",
            writeAudit = WriteAudit, runWatch = new { applied = false, reason = "initial qualification not established" },
            ownedProcesses = Evidence.GetValueOrDefault("ownedProcesses", Array.Empty<object>()),
            processIssues = Evidence.GetValueOrDefault("processIssues", Array.Empty<string>()),
            processMonitor = Evidence.GetValueOrDefault("processMonitor", new { methods = Array.Empty<string>(), reason = "not performed" }),
            liveChannels = Evidence.GetValueOrDefault("liveChannels"),
            sharedResources = Evidence.GetValueOrDefault("sharedResources", Array.Empty<object>()),
            presentation = Evidence.GetValueOrDefault("presentation", new { fixedValues = new { }, unfixedValues = new[] { "not measured in headless qualification" } }),
            captureOmissions = Array.Empty<object>(),
            reviews = Array.Empty<object>(),
            scenarios = new[] { new ScenarioResult(scenario, productOutcome, productOutcome == "skipped" ? "not executed: " + skipConditionId : "", skipConditionId) }, evidence = JsonSerializer.SerializeToElement(Evidence),
        };
    }

    public void Save(string directory, object final)
    {
        string path = IsolationPaths.Inside(directory, "ui-manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(final, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) + "\n");
    }
}

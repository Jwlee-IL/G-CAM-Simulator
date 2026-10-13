using System.Text.Json;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Execution safety, bundle qualification and product outcomes are separate axes.</summary>
public sealed class UiRunManifest(string runId, string scenario)
{
    public sealed record Gate(string Id, string Status, string Reason);
    public sealed record Issue(string Type, string Reason);
    public sealed record ReviewPlan(bool Required, string[] Scope, string SelectionRule, string[] HumanActIds);
    public sealed record Bundle(string Id, string Profile, string Status, string Reason, Gate[] Gates, ReviewPlan ReviewPlan, string[] ReviewIds);
    public sealed record ScenarioResult(string Id, string Status, string Reason, string? SkipConditionId);
    public sealed record Conditional(string Id, string Activation, string EvaluationLevel, string BundleId, string Status, string Basis);
    public string RunId { get; } = runId;
    public string ManifestPhase { get; private set; } = "in_progress";
    public string RunStatus { get; private set; } = "blocked";
    public List<Issue> RunIssues { get; } = [];
    public Dictionary<string, object?> Evidence { get; } = [];
    public object? WriteAudit { get; set; }
    private ProbePackage? _package;
    private bool _cleanupComplete;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    public void RecordApprovedProbe(ProbePackage package) => _package = package;

    public void CleanupCompleted(IEnumerable<Issue> issues)
    {
        if (ManifestPhase == "final") throw new InvalidOperationException("Final manifest is immutable");
        RunIssues.AddRange(issues); _cleanupComplete = true;
    }

    public object Finalise(string productOutcome, string profile = "P1", bool observed = false, string? skipConditionId = null)
    {
        if (!_cleanupComplete || ManifestPhase == "final") throw new InvalidOperationException("Cleanup must precede one finalisation");
        if (productOutcome is not ("passed" or "failed" or "blocked" or "skipped")) throw new ArgumentException("Unknown scenario outcome");
        if (productOutcome == "skipped" && string.IsNullOrWhiteSpace(skipConditionId)) throw new InvalidDataException("Skipped scenario requires a condition ID");
        if (profile is not ("P1" or "P2" or "P3" or "P4")) throw new ArgumentException("Unknown bundle profile");
        if (RunIssues.Any(i => i.Type is not ("unsafe_effect" or "cleanup_failed" or "blocked"))) throw new InvalidDataException("Unknown run issue type");
        RunStatus = new[] { "unsafe_effect", "cleanup_failed", "blocked" }.FirstOrDefault(s => RunIssues.Any(i => i.Type == s)) ?? "completed";
        ManifestPhase = "final";
        string basis = observed ? "g1-observed-run" : "qualification-not-established";
        var conditionals = new[] { "SOURCE-AVAILABLE", "BINARY-TRACEABLE", "SOURCELESS-BINARY", "SEL-LOCALE", "SEL-A11Y",
            "INPUT-COORDINATE", "RUN-RESTART", "SESSION-REUSE", "DEVICE-SIMULATED", "SHARED-RESOURCE", "VERDICT-PIXEL",
            "VERDICT-ARTIFACT", "VERDICT-SUMMARY", "GATE-SKIP", "CAPTURE-OUTPUT", "IMAGE-GATE", "BASELINE", "CI-NO-RUNTIME",
            "PROFILE-P2-P4", "PROFILE-P3", "PROFILE-P4", "REVIEW-REQUIRED", "ADOPTION-EXISTING" }.Select(id =>
        {
            bool run = id is "SOURCE-AVAILABLE" or "BINARY-TRACEABLE" or "SOURCELESS-BINARY" or "RUN-RESTART" or "SESSION-REUSE" or "SHARED-RESOURCE" or "CI-NO-RUNTIME" or "ADOPTION-EXISTING";
            bool derived = id.StartsWith("PROFILE-", StringComparison.Ordinal) || id is "SOURCELESS-BINARY" or "CAPTURE-OUTPUT" or "REVIEW-REQUIRED" or "ADOPTION-EXISTING";
            bool policy = id is "SEL-A11Y" or "DEVICE-SIMULATED";
            string status = id switch
            {
                "SOURCE-AVAILABLE" or "SHARED-RESOURCE" or "ADOPTION-EXISTING" => "applies",
                "BINARY-TRACEABLE" => _package is null ? "undetermined" : "applies",
                "SOURCELESS-BINARY" => _package is null ? "applies" : "not_applicable",
                "PROFILE-P2-P4" => profile == "P1" ? "not_applicable" : "applies",
                "PROFILE-P3" => profile == "P3" ? "applies" : "not_applicable",
                "PROFILE-P4" or "CAPTURE-OUTPUT" or "REVIEW-REQUIRED" => profile == "P4" ? "applies" : "not_applicable",
                "SESSION-REUSE" or "DEVICE-SIMULATED" or "BASELINE" => "not_applicable",
                _ => "undetermined",
            };
            return new Conditional(id, policy ? "policy" : derived ? "derived" : "observed", run ? "run" : "bundle", run ? "not_applicable" : "runtime", status,
                status == "undetermined" ? "not qualified; current evidence does not establish this condition" : "source/profile/probe facts; see charter and runtime evidence");
        }).ToArray();
        return new
        {
            schema = "gcam.ui-run", schemaVersion = 2, RunId, ManifestPhase, RunStatus, runIssues = RunIssues.ToArray(),
            startedAt = _startedAt, finishedAt = DateTimeOffset.UtcNow,
            purpose = profile switch { "P4" => "manual", "P3" => "evidence", "P2" => "regression", _ => "exploratory" },
            product = new { name = "GCAM Studio", revision = _package?.Commit ?? "unknown", workingTree = _package is null ? "unknown" : "clean",
                deploymentSha256 = _package?.DeploymentSha256 },
            automation = new { revision = _package?.Commit ?? "unknown", workingTree = _package is null ? "unknown" : "clean",
                assemblySha256 = IsolationPaths.Hash(File.ReadAllBytes(typeof(UiRunManifest).Assembly.Location)), toolVersions = new { runtime = Environment.Version.ToString() } },
            environment = new { os = Environment.OSVersion.VersionString, locale = "en", formattingCulture = System.Globalization.CultureInfo.CurrentCulture.Name,
                display = new { measured = false, reason = "initial qualification does not inspect presentation" } },
            inputs = Array.Empty<object>(),
            executionGates = new[] { new Gate("G0", _package is null || profile != "P1" ? "blocked" : "passed-lite", _package is null ? "charter-not-approved" : "approved-probe-charter"), new Gate("G1", "blocked", basis) },
            resultBundles = new[] { new Bundle("runtime", profile, "blocked", basis,
                Enumerable.Range(2, 5).Select(n => new Gate("G" + n, n is 4 or 6 && profile == "P1" ? "not_applicable" : "blocked", basis)).ToArray(),
                new(profile is "P3" or "P4", profile == "P4" ? new[] { "final-render", "new-or-changed-capture-template" } : [],
                    profile == "P4" ? "every edition figure and flagged template" : "no initial-probe figure review", profile == "P4" ? new[] { "HUM-10" } : []), [] ) },
            charter = new { status = _package is null ? "draft" : "approved", scope = _package is null ? "unapproved" : "launch-idle-exit-probe-only",
                p4Status = "draft-not-approved", approvals = _package?.Approvals ?? [] },
            adoption = new { mode = "existing", qualificationScopes = new[] { "isolation", "process-monitor", "probe-launch" }.Select(id => new
            {
                id, status = "blocked", allowedUses = Array.Empty<string>(),
                dependsOnScopeIds = id == "probe-launch" ? new[] { "isolation", "process-monitor" } : [],
                gateBasis = new[] { new { gate = "G1", basis = "gap", evidenceRef = "pending observed-run classification" } },
                gaps = new[] { new { id = id + "-qualification", owner = "planner", expiresAt = "2026-11-13", residualRisk = "runtime not qualified",
                    qualificationCondition = "approved observed basis and applicable pilot gates", allowedUses = Array.Empty<string>() } },
            }).ToArray() },
            conditionalRequirements = conditionals,
            writeAudit = WriteAudit, runWatch = new { applied = false, reason = "initial qualification not established" },
            ownedProcesses = Evidence.GetValueOrDefault("ownedProcesses", Array.Empty<object>()),
            processMonitor = Evidence.GetValueOrDefault("processMonitor", new { methods = Array.Empty<string>(), reason = "not performed" }),
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

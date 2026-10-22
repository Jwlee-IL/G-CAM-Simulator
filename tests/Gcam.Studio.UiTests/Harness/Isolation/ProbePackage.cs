using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>An external approval package is prepared by the author after reviewing the concrete commit.</summary>
public sealed record ProbePackage(string RunId, string Commit, DateTimeOffset CommittedAt, string IsolationSha256,
    string DeploymentSha256, ApprovalRecord[] Approvals)
{
    public string? Contract { get; init; }
    public RealWorldEffect[] RealWorldEffects { get; init; } = [];

    /// <summary>Author scope before launch; only these fixed templates can be bound to a fresh scenario run ID.</summary>
    public sealed record RealWorldEffect(string EffectId, string ApprovalId, string RunId, string TargetCommit,
        string DeploymentSha256, string[] ScenarioIds, string[] TypedNameTemplates, string[] RecordFamilies);
    public sealed record EffectGrant(string EffectId, string RunId, string TargetCommit, string DeploymentSha256,
        DateTimeOffset CommittedAt, string ScenarioId, string ScenarioRunId, string[] TypedNames,
        string[] RecordFamilies, ApprovalRecord Approval);
    public static readonly string[] ShellFamilies = ["save-dialog-history", "recent-document-links", "recent-documents-registry"];
    public static readonly string[] ExportTemplates = ["report-{runId}.html", "report-{runId}.pdf", "report-{runId}.docx", "report-{runId}.csv"];

    public ApprovalRecord RequireApproval(string act)
    {
        string? legacy = act switch { "charter" => "HUM-04", "desktop-go" => "HUM-01", _ => null };
        // The old package selected these exact IDs. Additional historical charter acts must not invalidate it.
        var matches = Contract is null && legacy is not null && Approvals.Any(a => a.HumanActId == legacy)
            ? Approvals.Where(a => a.HumanActId == legacy).ToArray() : Approvals.Where(a => a.Covers(act)).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("Exactly one approval required: " + act);
        matches[0].Validate(act, Commit, CommittedAt);
        return matches[0];
    }

    [JsonIgnore]
    public ApprovalRecord[] LiveApprovals => Approvals.Where(a => ApprovalRecord.ActName(a.HumanActId) is not null)
        .Where(a => ApprovalRecord.ActName(a.HumanActId) is not ("charter" or "desktop-go") || a == RequireApproval(ApprovalRecord.ActName(a.HumanActId)!))
        .Select(a => a.ForLiveRecord()).ToArray();

    public void Validate(string runId, string isolationHash, string deploymentHash)
    {
        if (RunId != runId || IsolationSha256 != isolationHash || DeploymentSha256 != deploymentHash)
            throw new InvalidDataException("Probe package does not identify this run/isolation/deployment");
        if (Contract is not (null or "v4")) throw new InvalidDataException("Unknown approval contract");
        if (Contract == "v4" && Approvals.Any(a => !ApprovalRecord.Acts.Contains(a.HumanActId)))
            throw new InvalidDataException("New approval records require named acts");
        foreach (string act in new[] { "charter", "desktop-go" }) RequireApproval(act);
        foreach (var effect in RealWorldEffects)
        {
            var approval = Approvals.SingleOrDefault(a => a.Id == effect.ApprovalId && a.Covers("real-world-effect"))
                ?? throw new InvalidDataException("Missing real-world-effect approval");
            approval.Validate("real-world-effect", Commit, CommittedAt);
            if (effect.EffectId != "E6" || effect.RunId != RunId || effect.TargetCommit != Commit
                || effect.DeploymentSha256 != DeploymentSha256 || effect.ScenarioIds.Length == 0
                || effect.ScenarioIds.Any(string.IsNullOrWhiteSpace) || effect.TypedNameTemplates.Length == 0
                || effect.TypedNameTemplates.Any(t => !ExportTemplates.Contains(t)) || effect.RecordFamilies.Length == 0
                || effect.RecordFamilies.Any(f => !ShellFamilies.Contains(f)))
                throw new InvalidDataException("Real-world-effect scope differs from this run/export");
        }
    }

    public EffectGrant[] BindEffects(string scenarioRunId, string scenarioId, IReadOnlyList<string> declaredWrites)
    {
        Validate(RunId, IsolationSha256, DeploymentSha256);
        _ = IsolationPaths.RunRoot("scratch", scenarioRunId);
        string[] names = declaredWrites.Select(Path.GetFileName).OfType<string>()
            .Where(n => new[] { ".html", ".pdf", ".docx", ".csv" }.Contains(Path.GetExtension(n))).Distinct().Order().ToArray();
        if (names.Length == 0) return [];
        var grants = RealWorldEffects.Where(e => e.ScenarioIds.Contains(scenarioId)).Select(e =>
            new EffectGrant(e.EffectId, RunId, Commit, DeploymentSha256, CommittedAt, scenarioId, scenarioRunId,
                names.Where(n => e.TypedNameTemplates.Any(t => t.Replace("{runId}", scenarioRunId) == n)).ToArray(),
                e.RecordFamilies, Approvals.Single(a => a.Id == e.ApprovalId && a.Covers("real-world-effect")).ForLiveRecord())).Where(g => g.TypedNames.Length > 0).ToArray();
        if (names.Except(grants.SelectMany(g => g.TypedNames)).Any())
            throw new InvalidDataException("Export shell records lack this run's named E6 permission");
        return grants;
    }

    public void ValidateSource(string actualCommit, DateTimeOffset actualCommittedAt)
    {
        if (Commit != actualCommit || CommittedAt != actualCommittedAt)
            throw new InvalidDataException("Approval commit/date does not match the source query");
    }

    public static ProbePackage Load(string path) => JsonSerializer.Deserialize<ProbePackage>(File.ReadAllText(path),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Invalid probe package");

    public static string DeploymentFingerprint(string exe)
    {
        string root = Path.GetDirectoryName(exe)!;
        return ContentFingerprint.Digest(ContentFingerprint.Files(root, deployment: true));
    }
}

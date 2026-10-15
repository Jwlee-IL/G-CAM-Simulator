using System.Text.Json;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>An external approval package is prepared by the author after reviewing the concrete commit.</summary>
public sealed record ProbePackage(string RunId, string Commit, DateTimeOffset CommittedAt, string IsolationSha256,
    string DeploymentSha256, ApprovalRecord[] Approvals)
{
    public void Validate(string runId, string isolationHash, string deploymentHash)
    {
        if (RunId != runId || IsolationSha256 != isolationHash || DeploymentSha256 != deploymentHash)
            throw new InvalidDataException("Probe package does not identify this run/isolation/deployment");
        foreach (string act in new[] { "HUM-01", "HUM-04", "HUM-07" })
        {
            var approval = Approvals.SingleOrDefault(a => a.HumanActId == act)
                ?? throw new InvalidDataException("Missing probe approval: " + act);
            approval.Validate(act, Commit, CommittedAt);
        }
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

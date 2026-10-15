using System.Text.RegularExpressions;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>An author statement targets an exact change; a prior approval cannot cover a later commit.</summary>
public sealed record ApprovalRecord(string Id, string HumanActId, string TargetCommit, string Quote, DateTimeOffset QuotedAt)
{
    public void Validate(string requiredAct, string targetCommit, DateTimeOffset committedAt)
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Quote) || HumanActId != requiredAct
            || !Regex.IsMatch(TargetCommit, "^[0-9a-f]{40}$") || TargetCommit != targetCommit || QuotedAt < committedAt)
            throw new InvalidDataException("Approval does not cover this act/change/date");
    }
}

using System.Text.RegularExpressions;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>An author statement targets an exact change; a prior approval cannot cover a later commit.</summary>
public sealed record ApprovalRecord(string Id, string HumanActId, string TargetCommit, string Quote, DateTimeOffset QuotedAt)
{
    public static readonly string[] Acts = ["charter", "real-world-effect", "final-review", "physical", "desktop-go"];

    /// <summary>Historical identifiers are read aliases, never requirements for a new run.</summary>
    public static string? ActName(string value) => value switch
    {
        "HUM-01" => "desktop-go", "HUM-04" => "charter", "HUM-10" => "final-review",
        _ => Acts.Contains(value) ? value : null,
    };

    public bool Covers(string act) => HumanActId == act || ActName(HumanActId) == act;

    public ApprovalRecord ForLiveRecord() => this with
    {
        HumanActId = ActName(HumanActId) ?? throw new InvalidDataException("Retired approval act cannot be emitted"),
    };

    public void Validate(string requiredAct, string targetCommit, DateTimeOffset committedAt)
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Quote) || !Covers(requiredAct)
            || !Regex.IsMatch(TargetCommit, "^[0-9a-f]{40}$") || TargetCommit != targetCommit || QuotedAt < committedAt)
            throw new InvalidDataException("Approval does not cover this act/change/date");
    }
}

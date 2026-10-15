using System.Text.Json;
using System.Text.RegularExpressions;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Targets have declared semantics; an absent target is always denied, including in diagnostic runs.</summary>
public sealed class ActionPolicy
{
    public sealed record Entry(string Target, string SemanticAction, string Effect, string Policy, string[] Operations, string[] ControlTypes);
    public sealed record Document(int SchemaVersion, string ApprovalStatus, Entry[] Targets);
    private readonly Document _document;
    public ActionPolicy(Document document)
    {
        if (document.SchemaVersion != 1 || document.Targets.Select(t => t.Target).Distinct().Count() != document.Targets.Length)
            throw new InvalidDataException("Unknown or duplicate action policy");
        _document = document;
    }
    public static ActionPolicy Load(string path) => new(JsonSerializer.Deserialize<Document>(File.ReadAllText(path),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Missing policy"));

    public Entry Require(string target, string operation, string? controlType = null, bool qualified = false)
    {
        var entry = _document.Targets.SingleOrDefault(t => t.Target == target)
            ?? _document.Targets.SingleOrDefault(t => t.Target == "measurement-row" && Regex.IsMatch(target, "^M[1-9][0-9]*(\\.Delete)?$"))
            ?? _document.Targets.SingleOrDefault(t => t.Target == "readout-line-help" && Regex.IsMatch(target, "^Readout\\.LineHelp\\.(122\\.06|661\\.7|1173\\.2|1332\\.5)$"));
        if (entry is null || entry.Policy != "allow" || !entry.Operations.Contains(operation)
            || (controlType is not null && !entry.ControlTypes.Contains(controlType))
            || (qualified && _document.ApprovalStatus != "approved"))
            throw new InvalidOperationException($"action-policy-denied:{target}:{operation}");
        return entry;
    }

    public (string[] New, string[] Removed) Compare(IEnumerable<string> declared)
    {
        var actual = declared.ToHashSet(StringComparer.Ordinal);
        var policy = _document.Targets.Select(t => t.Target).ToHashSet(StringComparer.Ordinal);
        return (actual.Except(policy).Order().ToArray(), policy.Except(actual).Order().ToArray());
    }

    public void VerifyControlType(string target, string automationType)
    {
        var entry = Require(target, "read");
        var map = new Dictionary<string, string>
        {
            ["Button"] = "Button", ["RadioButton"] = "RadioButton", ["IconToggle"] = "RadioButton", ["CheckBox"] = "CheckBox",
            ["ComboBox"] = "ComboBox", ["TextBox"] = "Edit", ["TextBlock"] = "Text", ["TabItem"] = "TabItem",
            ["ListBox"] = "List", ["ListBoxItem"] = "ListItem",
        };
        var expected = entry.ControlTypes.Where(map.ContainsKey).Select(t => map[t]).ToArray();
        if (expected.Length != 0 && !expected.Contains(automationType)) throw new InvalidOperationException("action-policy-control-type-mismatch:" + target);
    }
}

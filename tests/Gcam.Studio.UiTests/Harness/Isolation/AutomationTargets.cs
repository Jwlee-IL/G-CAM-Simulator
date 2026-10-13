using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Static target inventory, including the four supported bound IDs. Unknown bindings fail closed.</summary>
public static class AutomationTargets
{
    public static IReadOnlyDictionary<string, string[]> Read(string repository)
    {
        var types = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        void Add(string id, string type)
        {
            if (!types.TryGetValue(id, out var set)) types[id] = set = [];
            set.Add(type);
        }
        foreach (string file in Directory.EnumerateFiles(Path.Combine(repository, "src/Gcam.Studio"), "*.xaml", SearchOption.AllDirectories)
            .Where(p => !p.Split(Path.DirectorySeparatorChar).Any(s => s is "bin" or "obj")))
        {
            foreach (var element in XDocument.Load(file).Descendants())
            foreach (var attr in element.Attributes().Where(a => a.Name.LocalName == "AutomationProperties.AutomationId"))
            {
                if (!attr.Value.StartsWith('{')) { Add(attr.Value, element.Name.LocalName); continue; }
                if (attr.Value == "{Binding DeleteAutomationId}") Add("measurement-row", "Button");
                else if (attr.Value == "{Binding HelpId}") Add("readout-line-help", "HelpMark");
                else if (attr.Value == "{Binding AutomationId}" && element.Name.LocalName == "RadioButton")
                {
                    foreach (var f in Directory.EnumerateFiles(Path.Combine(repository, "src/Gcam.Studio.Core/ViewModels"), "*WorkspaceViewModel.cs"))
                        foreach (Match m in Regex.Matches(File.ReadAllText(f), "\\\"(Workspace\\.[A-Za-z]+)\\\"")) Add(m.Groups[1].Value, "RadioButton");
                }
                else if (attr.Value == "{Binding AutomationId}" && element.Name.LocalName == "Button")
                {
                    foreach (Match m in Regex.Matches(File.ReadAllText(Path.Combine(repository, "src/Gcam.Studio.Core/ViewModels/MainViewModel.Readout.cs")),
                        "\\\"(Readout\\.(?:Window[0-9]+|BenchmarkWindow))\\\"")) Add(m.Groups[1].Value, "Button");
                }
                else throw new InvalidDataException("Unclassified bound AutomationId: " + attr.Value);
            }
        }
        foreach (Match m in Regex.Matches(File.ReadAllText(Path.Combine(repository, "src/Gcam.Studio/Controls/CaptionButtons.cs")), "\\\"(Window\\.[A-Za-z]+)\\\"")) Add(m.Groups[1].Value, "Button");
        string measurement = File.ReadAllText(Path.Combine(repository, "src/Gcam.Studio.Core/ViewModels/MeasurementViewModel.cs"));
        if (!measurement.Contains("DeleteAutomationId => Name + \".Delete\"", StringComparison.Ordinal)) throw new InvalidDataException("Measurement target binding changed");
        Add("measurement-row", "ListBoxItem");
        return types.ToDictionary(k => k.Key, v => v.Value.Order().ToArray());
    }
}

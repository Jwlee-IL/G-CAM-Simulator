using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Where a mark's visible label is read at capture time: the control's own text (buttons, tabs, list items,
/// text blocks), the field label text immediately before it, or its first child text (a check box with a text block).
/// Icon-only controls have no visible word and are not marked.</summary>
public enum LabelSource { Name, PrecedingText, ChildText }

/// <summary>One figure mark: a control by AutomationId and where its visible label comes from.</summary>
public sealed record Mark(string Target, LabelSource Label = LabelSource.Name);

/// <summary>
/// The record of one manual scenario execution (<c>gcam.manual-run</c> version 1): reader steps with their resolved
/// screen labels, notice references, automatic checks and accepted or rejected captures. Written once, after the app
/// is closed and the UI manifest is final; the generator prints a chapter only from a passed, completed record.
/// </summary>
public sealed partial class ManualRun
{
    public const string BreakVerdictVariable = "GCAM_UI_BREAK_VERDICT";

    public sealed record CheckLine(string Id, string Result, string Detail);
    public sealed record Figure(string Id, string Capture, string Marks, string Caption);
    public sealed record MissingFigure(string Id, string[] Reasons);
    public sealed record Evidence(string Path, string Sha256, long Bytes, string Role);
    public sealed record Subject(string DeploymentSha256, IReadOnlyDictionary<string, string> Files);

    public sealed class Step(int n, string template, string[] notices)
    {
        public int N { get; } = n;
        public string Template { get; } = template;
        public string? Text { get; set; }
        public Dictionary<string, string> Labels { get; } = new(StringComparer.Ordinal);
        public string[] Notices { get; } = notices;
        public List<CheckLine> Checks { get; } = [];
        public List<Figure> Figures { get; } = [];
        public List<MissingFigure> MissingFigures { get; } = [];
        public string Verdict { get; set; } = "passed";
        public string? Error { get; set; }
    }

    private readonly ManualRegistry _registry;
    private readonly Func<string, LabelSource, string> _label;
    private readonly List<Step> _steps = [];
    private readonly List<Evidence> _evidence = [];
    private readonly Dictionary<string, object?> _environment = [];
    private int _figures;

    public ManualRun(string scenario, ManualRegistry registry, string directory, string runId, DateTimeOffset startedAt,
        Func<string, LabelSource, string> label)
    {
        Scenario = scenario; _registry = registry; Chapter = registry.ForScenario(scenario);
        Directory = directory; RunId = runId; StartedAt = startedAt; _label = label;
    }

    public string Scenario { get; }
    public ManualRegistry.Chapter Chapter { get; }
    public string Directory { get; }
    public string RunId { get; }
    public DateTimeOffset StartedAt { get; }
    public IReadOnlyList<Step> Steps => _steps;
    public StudioWindow? Ui { get; private set; }
    public ManualCapture.Baseline? Baseline { get; private set; }

    /// <summary>With GCAM_UI_BREAK_VERDICT=1 each manual scenario corrupts one expectation and its chapter must drop out.</summary>
    public static bool Broken => Environment.GetEnvironmentVariable(BreakVerdictVariable) == "1";

    /// <summary>Binds the running window: the capture baseline (frame size, DPI, theme) is measured here, after Normalise.</summary>
    public void Attach(StudioWindow ui)
    {
        Ui = ui;
        Baseline = ManualCapture.MeasureBaseline(ui);
        _environment["frameWidthPx"] = Baseline.WidthPx;
        _environment["frameHeightPx"] = Baseline.HeightPx;
        _environment["dpi"] = Baseline.Dpi;
        _environment["perMonitorAware"] = Pointer.IsPerMonitorAware;
        _environment["themeAtStart"] = Baseline.Theme;
    }

    [GeneratedRegex(@"\{(?<id>[A-Za-z][A-Za-z0-9.]*)(?::(?<source>label|child))?\}")]
    private static partial Regex LabelToken();

    /// <summary>Opens the next reader step. <c>{Id}</c> in the text becomes the control's label as the screen shows it
    /// (<c>{Id:label}</c> its field label, <c>{Id:child}</c> its first child text). Labels are read at the step's first
    /// check or capture, after its action, or earlier with <see cref="ResolveLabels"/> when the action removes them
    /// (a dialog that closes); the previous step is resolved now if it was not.</summary>
    public void BeginStep(string text, params string[] notices)
    {
        foreach (string notice in notices) _registry.RequireNotice(notice, Chapter.Number);
        if (_steps.Count > 0) ResolveLabels();
        _steps.Add(new Step(_steps.Count + 1, text, notices));
    }

    public void ResolveLabels()
    {
        var step = Current;
        if (step.Text is not null) return;
        step.Text = LabelToken().Replace(step.Template, m =>
        {
            var source = m.Groups["source"].Value switch { "label" => LabelSource.PrecedingText, "child" => LabelSource.ChildText, _ => LabelSource.Name };
            string label = _label(m.Groups["id"].Value, source);
            if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("No visible label for " + m.Groups["id"].Value);
            step.Labels[m.Groups["id"].Value] = label;
            return "**" + EscapeMarkdown(label) + "**";
        });
    }

    private Step Current => _steps.Count > 0 ? _steps[^1] : throw new InvalidOperationException("A check or capture needs an open step");

    /// <summary>Records an automatic check under its stable ID; a failed check fails the step and stops the scenario.</summary>
    public void Check(string id, bool ok, string detail)
    {
        var step = Current;
        if (step.Text is null && ok) ResolveLabels();
        step.Checks.Add(new(id, ok ? "pass" : "fail", detail));
        if (ok) return;
        step.Verdict = "failed";
        Assert.Fail($"{id}: {detail}");
    }

    public void CheckEqual<T>(string id, T expected, T actual) =>
        Check(id, EqualityComparer<T>.Default.Equals(expected, actual), $"expected {expected}, actual {actual}");

    /// <summary>A failure that is not a recorded check (an action, wait or selector error) fails the open step.</summary>
    public void Fail(Exception error)
    {
        if (_steps.Count == 0) return;
        Current.Verdict = "failed";
        Current.Error ??= Sanitise(error.GetType().Name + ": " + error.Message);
    }

    /// <summary>Captures the owned window after the quality checks and measures the marks in the same transaction. A
    /// rejected capture keeps no pixels and is listed as a missing figure; the step's functional verdict is unchanged.</summary>
    public void Capture(string caption, IReadOnlyList<Mark> marks, string? region = null, bool full = false)
    {
        var step = Current;
        ResolveLabels();
        string id = $"{Chapter.Number}-{++_figures}";
        var outcome = ManualCapture.Take(Ui ?? throw new InvalidOperationException("No window attached"), Baseline!, Directory,
            id, Scenario, marks, region, full);
        if (outcome.Reasons.Length != 0)
        {
            step.MissingFigures.Add(new(id, outcome.Reasons));
            return;
        }
        _evidence.Add(new(outcome.PngPath!, IsolationPaths.Hash(outcome.Png!), outcome.Png!.LongLength, "capture"));
        _evidence.Add(new(outcome.MarksPath!, IsolationPaths.Hash(outcome.Marks!), outcome.Marks!.LongLength, "marks"));
        step.Figures.Add(new(id, outcome.PngPath!, outcome.MarksPath!, caption));
    }

    public string Status => _steps.Count == 0 ? "not_executed" : _steps.Any(s => s.Verdict == "failed") ? "failed" : "passed";

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, NewLine = "\n", PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The record bytes (pure: no desktop, no file access).</summary>
    public byte[] BuildRecord(DateTimeOffset finishedAt, string uiRunStatus, string? uiManifestSha256, Subject before, Subject after,
        object tool, object environment)
    {
        bool completed = uiRunStatus == "completed";
        var record = new
        {
            schema = "gcam.manual-run", schemaVersion = 1, runId = RunId, scenario = Scenario,
            chapter = new { number = Chapter.Number, title = Chapter.Title },
            registrySha256 = _registry.RegistrySha256, noticesSha256 = _registry.NoticesSha256,
            startedAt = StartedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
            finishedAt = finishedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
            status = Status,
            execution = new { status = completed ? "completed" : "aborted", uiRunStatus, abortReason = completed ? null : "ui-run-status: " + uiRunStatus },
            subject = new
            {
                deploymentSha256 = before.DeploymentSha256, files = before.Files,
                unchanged = before.DeploymentSha256 == after.DeploymentSha256,
            },
            tool, environment,
            links = new { uiManifest = new { sha256 = uiManifestSha256, retained = false, reason = "the UI manifest names local paths; its hash is the lineage" } },
            steps = _steps.Select(s => new
            {
                n = s.N, text = s.Text ?? s.Template, labels = s.Labels, notices = s.Notices, checks = s.Checks, figures = s.Figures,
                missingFigures = s.MissingFigures, verdict = s.Verdict, error = s.Error,
            }),
            evidence = _evidence,
        };
        return JsonSerializer.SerializeToUtf8Bytes(record, Json).Concat("\n"u8.ToArray()).ToArray();
    }

    /// <summary>Writes <c>manual-run.json</c> after cleanup; returns its path.</summary>
    public string Finalise(string? uiManifestPath, Subject before, Subject after)
    {
        string status = "missing", manifestSha = null!;
        if (uiManifestPath is not null && File.Exists(uiManifestPath))
        {
            byte[] bytes = File.ReadAllBytes(uiManifestPath);
            manifestSha = IsolationPaths.Hash(bytes);
            using var doc = JsonDocument.Parse(bytes);
            status = doc.RootElement.TryGetProperty("runStatus", out var s) ? s.GetString() ?? "missing" : "missing";
        }
        var environment = new Dictionary<string, object?>(_environment)
        {
            ["os"] = Environment.OSVersion.VersionString, ["uiLocale"] = "en",
            ["formattingCulture"] = CultureInfo.CurrentCulture.Name, ["runtime"] = Environment.Version.ToString(),
        };
        var tool = new
        {
            commit = GitHead(), dirtyFiles = RepoPaths.DirtyFileCount(),
            assemblySha256 = IsolationPaths.Hash(File.ReadAllBytes(typeof(ManualRun).Assembly.Location)),
        };
        string path = IsolationPaths.Inside(Directory, "manual-run.json");
        File.WriteAllBytes(path, BuildRecord(DateTimeOffset.Now, status, manifestSha, before, after, tool, environment));
        return path;
    }

    /// <summary>The Studio deployment the scenario launches (the fingerprint specification shared with stage0.py).</summary>
    public static Subject ReadSubject()
    {
        var files = ContentFingerprint.Files(Path.GetDirectoryName(RepoPaths.StudioExe())!, deployment: true);
        return new(ContentFingerprint.Digest(files), files);
    }

    private static string GitHead()
    {
        try
        {
            var start = new ProcessStartInfo("git", "rev-parse HEAD")
            {
                WorkingDirectory = RepoPaths.Root(), RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
            };
            start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
            using var git = Process.Start(start)!;
            string head = git.StandardOutput.ReadToEnd().Trim();
            return git.WaitForExit(10_000) && git.ExitCode == 0 && Regex.IsMatch(head, "^[0-9a-f]{40}$") ? head : "unknown";
        }
        catch (Exception)
        {
            return "unknown";
        }
    }

    [GeneratedRegex(@"[A-Za-z]:\\[^\s'""]*|\\\\[^\s'""]+")]
    private static partial Regex LocalPath();

    /// <summary>First line of an error, without local paths or the account name (records are public evidence).</summary>
    public static string Sanitise(string text)
    {
        string line = text.Split('\n')[0].Trim();
        line = LocalPath().Replace(line, "<path>");
        foreach (string name in new[] { Environment.UserName, Environment.MachineName }.Where(n => n.Length >= 3))
            line = Regex.Replace(line, Regex.Escape(name), "<identity>", RegexOptions.IgnoreCase);
        return line.Length <= 300 ? line : line[..300];
    }

    public static string EscapeMarkdown(string text) => Regex.Replace(text, @"([\\`*_\[\]<>|])", @"\$1");

    /// <summary>The label of a control by its source, read without navigation (explicit window scope).</summary>
    public static string ReadLabel(StudioWindow ui, string id, LabelSource source)
    {
        var element = ui.ById(id, ui.Element);
        switch (source)
        {
            case LabelSource.Name:
                return element.Current.Name;
            case LabelSource.ChildText:
                return element.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))?.Current.Name ?? "";
            default:
                var previous = TreeWalker.ControlViewWalker.GetPreviousSibling(element);
                return previous is not null && previous.Current.ControlType == ControlType.Text ? previous.Current.Name : "";
        }
    }
}

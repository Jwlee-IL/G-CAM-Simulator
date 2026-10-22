namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Artifact destinations stay under an owned run folder; no replacement of a prior execution.</summary>
public static class OwnedFolder
{
    public static string Create(string runId)
    {
        IsolationPaths.ValidateRunId(runId);
        // The record- wrapper is a folder name, not a run identifier.
        string root = IsolationPaths.RunRoot(Path.GetTempPath(), "record-" + runId, v4: false);
        if (Directory.Exists(root)) throw new IOException("Record folder already exists");
        Directory.CreateDirectory(root);
        File.WriteAllText(IsolationPaths.Inside(root, ".gcam-record-owner"), runId + "\n");
        return root;
    }
}

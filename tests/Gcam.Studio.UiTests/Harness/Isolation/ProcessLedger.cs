namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>PID plus birth time identifies an instance. Descendants are observed, never automatically adopted.</summary>
public sealed class ProcessLedger(string runId, string registeredImage)
{
    public sealed record Instance(int Pid, int ParentPid, DateTimeOffset StartedAt, string ImagePath, string Marker);
    private readonly List<Instance> _owned = [];
    public IReadOnlyList<Instance> Owned => _owned;
    public List<string> Issues { get; } = [];
    public string RunId { get; } = runId;

    public void RegisterDirect(Instance instance)
    {
        if (instance.Marker != RunId || !string.Equals(instance.ImagePath, registeredImage, StringComparison.OrdinalIgnoreCase)
            || _owned.Any(p => p.Pid == instance.Pid && p.StartedAt == instance.StartedAt))
            throw new InvalidDataException("Process instance is not a unique direct launch of the registered image");
        _owned.Add(instance);
    }

    public bool Owns(int pid, DateTimeOffset started) => _owned.Any(p => p.Pid == pid && p.StartedAt == started);

    public void Observe(IEnumerable<Instance> snapshot)
    {
        var instances = snapshot.ToArray();
        var descendants = _owned.Select(p => p.Pid).ToHashSet();
        bool grew;
        do
        {
            int prior = descendants.Count;
            foreach (var p in instances)
                if (descendants.Contains(p.ParentPid) && p.StartedAt >= _owned.Min(o => o.StartedAt)) descendants.Add(p.Pid);
            grew = descendants.Count != prior;
        } while (grew);
        foreach (var p in instances)
        {
            bool successor = descendants.Contains(p.Pid);
            if (!Owns(p.Pid, p.StartedAt) && (successor || string.Equals(p.ImagePath, registeredImage, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(p.ImagePath), Path.GetFileName(registeredImage), StringComparison.OrdinalIgnoreCase)))
            {
                string issue = $"unowned-process:{p.Pid}:{p.StartedAt:O}";
                if (!Issues.Contains(issue)) Issues.Add(issue);
            }
        }
    }
}

using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

public sealed class FocusPreparationTests
{
    [Fact]
    public void Focus_TransientRefusalRetries_AndPersistentRefusalFailsWithCause()
    {
        int attempts = 0;
        TimeSpan clock = TimeSpan.Zero;
        FocusPreparation.Wait(() => true, () => ++attempts < 3 ? throw new InvalidOperationException("cannot receive focus") : true,
            TimeSpan.FromSeconds(1), elapsed: () => clock, pause: () => clock += TimeSpan.FromMilliseconds(50));
        Assert.Equal(3, attempts);
        clock = TimeSpan.Zero;
        var failure = Assert.Throws<TimeoutException>(() => FocusPreparation.Wait(() => true,
            () => throw new InvalidOperationException("cannot receive focus"), TimeSpan.FromSeconds(1),
            elapsed: () => clock, pause: () => clock += TimeSpan.FromMilliseconds(50)));
        Assert.Equal("cannot receive focus", failure.InnerException!.Message);
        Assert.Equal(TimeSpan.FromSeconds(1), clock);
        Assert.Throws<InvalidOperationException>(() => FocusPreparation.Wait(
            () => throw new InvalidOperationException("wrong owned window or selector"), () => true,
            TimeSpan.FromSeconds(1), elapsed: () => TimeSpan.Zero));
    }

    /// <summary>A fake held root: the states it reports in turn, the last one repeating; each re-acquisition moves on.</summary>
    private sealed class FakeRoot(params FocusPreparation.RootState[] states) : FocusPreparation.IRoot
    {
        public int Reacquisitions { get; private set; }
        public int Inspections { get; private set; }
        public FocusPreparation.RootState Inspect()
        {
            Inspections++;
            return states[Math.Min(Reacquisitions, states.Length - 1)];
        }
        public void Reacquire() => Reacquisitions++;
    }

    private static readonly FocusPreparation.RootState Fallback = new("", "MSAA proxy (fake)", false);
    private static readonly FocusPreparation.RootState Wpf = new("WPF", "WPF provider (fake)", true);

    [Fact]
    public void IdLessRoot_IsReacquiredFromTheHandle_AndEachReacquisitionIsRecorded()
    {
        // The 2026-10-22 failure: names but no AutomationIds. The first fresh root is the WPF provider.
        var root = new FakeRoot(Fallback, Wpf);
        var log = new List<string>(); int ready = 0; TimeSpan clock = TimeSpan.Zero;
        FocusPreparation.Wait(() => { ready++; return true; }, () => true, TimeSpan.FromSeconds(5), log.Add,
            elapsed: () => clock, pause: () => clock += TimeSpan.FromMilliseconds(50), root: root);
        Assert.Equal(1, root.Reacquisitions);
        Assert.Equal(1, ready); // focus is never attempted on the fallback root
        Assert.Contains(log, l => l.StartsWith("UI Automation root re-acquisition 1: held root FrameworkId ''", StringComparison.Ordinal)
            && l.Contains("AutomationIds none", StringComparison.Ordinal) && l.Contains("MSAA proxy (fake)", StringComparison.Ordinal));
        Assert.Contains("UI Automation root usable after 1 re-acquisition(s): FrameworkId 'WPF'", log);
        // A root of another framework is re-acquired too, even with AutomationIds.
        var foreign = new FakeRoot(new("Win32", "Win32 proxy (fake)", true), Wpf);
        FocusPreparation.Wait(() => true, () => true, TimeSpan.FromSeconds(5), elapsed: () => TimeSpan.Zero, pause: () => { }, root: foreign);
        Assert.Equal(1, foreign.Reacquisitions);
    }

    [Fact]
    public void UsableRoot_IsInspectedOnce_AndKeepsTheFocusTimeout()
    {
        var root = new FakeRoot(Wpf); var log = new List<string>(); TimeSpan clock = TimeSpan.Zero;
        var failure = Assert.Throws<TimeoutException>(() => FocusPreparation.Wait(() => true,
            () => throw new InvalidOperationException("cannot receive focus"), TimeSpan.FromSeconds(1), log.Add,
            elapsed: () => clock, pause: () => clock += TimeSpan.FromMilliseconds(50), root: root));
        Assert.Equal((1, 0), (root.Inspections, root.Reacquisitions));
        Assert.Equal(TimeSpan.FromSeconds(1), clock); // no launch budget without a re-acquisition
        Assert.Equal("timed out after 1 s waiting for: owned window foreground and enabled child keyboard focus; transient UIA failures: 0; "
            + "UI Automation root re-acquisitions: 0", failure.Message);
        Assert.DoesNotContain(log, l => l.StartsWith("UI Automation root", StringComparison.Ordinal));
    }

    [Fact]
    public void RootIdLessUntilTheBudget_FailsWithTheFocusMessagePlusTheReacquisitionCount()
    {
        var root = new FakeRoot(Fallback); TimeSpan clock = TimeSpan.Zero; int ready = 0;
        var failure = Assert.Throws<TimeoutException>(() => FocusPreparation.Wait(() => { ready++; return true; }, () => true,
            TimeSpan.FromSeconds(5), elapsed: () => clock, pause: () => clock += TimeSpan.FromMilliseconds(50), root: root));
        Assert.Equal(0, ready);
        Assert.Equal(FocusPreparation.RootBudget, clock); // the wait ran to the launch budget, not to the 5 s focus timeout
        Assert.Equal(200, root.Reacquisitions);          // one per 50 ms poll over 10 s
        Assert.Equal("timed out after 10 s waiting for: owned window foreground and enabled child keyboard focus; transient UIA failures: 0; "
            + "UI Automation root re-acquisitions: 200", failure.Message);
        // A budget shorter than the focus timeout never shortens the wait.
        clock = TimeSpan.Zero;
        Assert.Throws<TimeoutException>(() => FocusPreparation.Wait(() => true, () => true, TimeSpan.FromSeconds(5),
            elapsed: () => clock, pause: () => clock += TimeSpan.FromMilliseconds(50), root: new FakeRoot(Fallback), rootBudget: TimeSpan.FromSeconds(1)));
        Assert.Equal(TimeSpan.FromSeconds(5), clock);
    }
}

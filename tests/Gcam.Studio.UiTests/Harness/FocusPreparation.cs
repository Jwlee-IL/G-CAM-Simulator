using System.Globalization;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Retries only focus acquisition; a persistent refusal remains a recorded failure.</summary>
/// <remarks>With a root guard the wait first requires a usable UI Automation root. On 2026-10-22 four launches (of 85 that
/// day, three in the M4 collection) held a root that was a fallback tree: names, structure and enabled states, but no
/// AutomationId anywhere and lossy control types (DataItem read as ListItem, ItemStatus empty), so every 'ThemeToggle' lookup
/// matched nothing for the whole 5 s. The likely cause: a root taken while WPF did not yet answer UI Automation for its
/// window is a fallback (MSAA) root, and everything found under it stays fallback; a fresh root from the same handle is
/// expected to be the WPF provider. Each unusable inspection re-acquires the root and is logged;
/// once a re-acquisition happened, the wait may run to <see cref="RootBudget"/>.</remarks>
public static class FocusPreparation
{
    /// <summary>The launch budget for focus preparation once the root had to be re-acquired: the 5 s focus wait plus 5 s.
    /// From the run records (2026-10-17 and 2026-10-22, 129 launches that reached the ready step): process start to ready
    /// step 1.6–3.2 s, median 1.9 s; the four affected launches each spent the whole 5 s on an ID-less root (8 lookups of
    /// about 0.6 s each). Their dumps show the window fully built, so WPF was answering by then and a fresh root is expected at
    /// the first re-acquisition; the extra 5 s leaves room for several slow inspections, and 10 s stays inside the 15 s
    /// main-window launch wait.</summary>
    public static readonly TimeSpan RootBudget = TimeSpan.FromSeconds(10);

    /// <summary>What a root offers: its framework, the provider description of the window's root as UI Automation answers
    /// for its handle now, and whether its tree exposes any AutomationId.</summary>
    public sealed record RootState(string FrameworkId, string ProviderDescription, bool ExposesAutomationIds)
    {
        public bool Usable => FrameworkId == "WPF" && ExposesAutomationIds;
    }

    /// <summary>The held root (a headless seam: tests use a fake). <see cref="Reacquire"/> replaces it with a fresh root from
    /// the same window handle.</summary>
    public interface IRoot
    {
        RootState Inspect();
        void Reacquire();
    }

    public static void Wait(Func<bool> ready, Func<bool> focus, TimeSpan timeout, Action<string>? log = null,
        Func<TimeSpan>? elapsed = null, Action? pause = null, IRoot? root = null, TimeSpan? rootBudget = null)
    {
        InvalidOperationException? refusal = null;
        int reacquisitions = 0;
        bool rootUsable = root is null;
        var budget = rootBudget ?? RootBudget;
        if (budget < timeout) budget = timeout;
        try
        {
            PollingWait.WaitUntil(() =>
            {
                if (!rootUsable)
                {
                    var state = root!.Inspect();
                    if (!state.Usable)
                    {
                        reacquisitions++;
                        log?.Invoke(string.Create(CultureInfo.InvariantCulture,
                            $"UI Automation root re-acquisition {reacquisitions}: held root FrameworkId '{state.FrameworkId}', AutomationIds {(state.ExposesAutomationIds ? "exposed" : "none")}, window provider '{state.ProviderDescription}'"));
                        root.Reacquire();
                        return false;
                    }
                    rootUsable = true;
                    if (reacquisitions > 0)
                        log?.Invoke(string.Create(CultureInfo.InvariantCulture,
                            $"UI Automation root usable after {reacquisitions} re-acquisition(s): FrameworkId '{state.FrameworkId}'"));
                }
                if (!ready()) return false;
                try { return focus(); }
                catch (InvalidOperationException ex) { refusal = ex; return false; }
            }, () => reacquisitions > 0 ? budget : timeout, "owned window foreground and enabled child keyboard focus", elapsed, pause);
        }
        catch (TimeoutException ex)
        {
            string message = root is null ? ex.Message
                : ex.Message + string.Create(CultureInfo.InvariantCulture, $"; UI Automation root re-acquisitions: {reacquisitions}");
            throw new TimeoutException(message, refusal ?? ex.InnerException);
        }
        finally { log?.Invoke("focus preparation last refusal: " + (refusal?.Message ?? "none")); }
    }
}

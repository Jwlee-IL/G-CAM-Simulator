namespace Gcam.Studio.UiTests.Harness;

/// <summary>Retries only focus acquisition; a persistent refusal remains a recorded failure.</summary>
public static class FocusPreparation
{
    public static void Wait(Func<bool> ready, Func<bool> focus, TimeSpan timeout, Action<string>? log = null,
        Func<TimeSpan>? elapsed = null, Action? pause = null)
    {
        InvalidOperationException? refusal = null;
        try
        {
            PollingWait.WaitUntil(() =>
            {
                if (!ready()) return false;
                try { return focus(); }
                catch (InvalidOperationException ex) { refusal = ex; return false; }
            }, timeout, "owned window foreground and enabled child keyboard focus", elapsed, pause);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException(ex.Message, refusal ?? ex.InnerException);
        }
        finally { log?.Invoke("focus preparation last refusal: " + (refusal?.Message ?? "none")); }
    }
}

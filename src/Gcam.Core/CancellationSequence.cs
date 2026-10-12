namespace Gcam.Core;

/// <summary>Checks cancellation at bounded intervals without changing enumeration or numerical accumulation order.</summary>
public static class CancellationSequence
{
    public static IEnumerable<T> Checked<T>(IEnumerable<T> source, CancellationToken token)
    {
        int count = 0;
        token.ThrowIfCancellationRequested();
        foreach (var item in source)
        {
            if ((count++ & 4095) == 0) token.ThrowIfCancellationRequested();
            yield return item;
        }
        token.ThrowIfCancellationRequested();
    }
}

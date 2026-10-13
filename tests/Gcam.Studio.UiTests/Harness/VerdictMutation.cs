namespace Gcam.Studio.UiTests.Harness;

/// <summary>Negative control proves the changed expectation rejects the original observation.</summary>
public static class VerdictMutation
{
    public static double Apply(double expected, double tolerance, bool broken)
    {
        if (!broken) return expected;
        double changed = expected + 1.0;
        if (changed == expected || Math.Abs(changed - expected) <= tolerance)
            throw new InvalidOperationException("verdict-mutation-not-applied");
        return changed;
    }
    public static void Verify(double original, double changed, double actual, double tolerance)
    {
        if (original == changed || !Verdict.Within(actual, original, tolerance) || Verdict.Within(actual, changed, tolerance))
            throw new InvalidOperationException("verdict-mutation-did-not-cause-expected-failure");
    }
}

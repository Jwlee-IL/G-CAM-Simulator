using System.Runtime.InteropServices;
using System.Windows.Automation;
using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

/// <summary>Pure polling oracles; a fake clock advances by 50 ms per failed poll. No app, UIA query or desktop input.</summary>
public sealed class PollingWaitTests
{
    [Fact]
    public void TransientThenSuccess_Returns_AndRecordsAllThreeKinds()
    {
        var errors = new Exception[] { new COMException("fixture: busy", unchecked((int)0x8000FFFF)),
            new ElementNotAvailableException("fixture: unavailable"), new SelectorMatchException("Readout.State", 0) };
        int calls = 0; var elapsed = TimeSpan.Zero; var steps = new List<string>();
        using var log = PollingWait.Record(steps.Add);
        PollingWait.WaitUntil(() => { if (calls < errors.Length) throw errors[calls++]; calls++; return true; },
            TimeSpan.FromMilliseconds(200), "fixture progress", () => elapsed, () => elapsed += TimeSpan.FromMilliseconds(50));
        Assert.Equal(4, calls); Assert.Equal(TimeSpan.FromMilliseconds(150), elapsed);
        string step = Assert.Single(steps);
        Assert.Contains("transient UIA failures: 3", step);
        Assert.Contains("SelectorMatchException: selector 'Readout.State' matched 0 elements", step);
    }

    [Fact]
    public void PersistentTransient_TimesOut_WithLastExceptionAndExactCount()
    {
        var errors = new Exception[] { new COMException("fixture: busy"),
            new SelectorMatchException("Readout.State", 0), new ElementNotAvailableException("fixture: last") };
        int calls = 0; var elapsed = TimeSpan.Zero; var steps = new List<string>();
        using var log = PollingWait.Record(steps.Add);
        var failure = Assert.Throws<TimeoutException>(() => PollingWait.WaitUntil(() => throw errors[calls++],
            TimeSpan.FromMilliseconds(150), "fixture timeout", () => elapsed, () => elapsed += TimeSpan.FromMilliseconds(50)));
        Assert.Equal(3, calls); Assert.Same(errors[2], failure.InnerException);
        Assert.Contains("fixture timeout; transient UIA failures: 3", failure.Message);
        Assert.Contains("transient UIA failures: 3; last: ElementNotAvailableException: fixture: last", Assert.Single(steps));
    }

    [Fact]
    public void NonTransient_PropagatesImmediately_IncludingAssertionsAndDuplicateSelectors()
    {
        foreach (var error in new Exception[] { new ArgumentException("fixture: invalid"),
            new Xunit.Sdk.XunitException("fixture: assertion"), new SelectorMatchException("Readout.State", 2),
            new InvalidOperationException("selector 'Readout.State' matched 0 elements (expected exactly 1)") })
        {
            int calls = 0, pauses = 0; var elapsed = TimeSpan.Zero; var steps = new List<string>();
            using var log = PollingWait.Record(steps.Add);
            var failure = Assert.Throws(error.GetType(), () => PollingWait.WaitUntil(() => { calls++; throw error; },
                TimeSpan.FromSeconds(1), "fixture strict", () => elapsed, () => { pauses++; elapsed += TimeSpan.FromMilliseconds(50); }));
            Assert.Same(error, failure); Assert.Equal(1, calls); Assert.Equal(0, pauses);
            Assert.Contains("transient UIA failures: 0; last: none", Assert.Single(steps));
        }
    }

    [Fact]
    public void FalseCondition_TimesOut_WithZeroTransientsAndNoInnerException()
    {
        int calls = 0; var elapsed = TimeSpan.Zero;
        var failure = Assert.Throws<TimeoutException>(() => PollingWait.WaitUntil(() => { calls++; return false; },
            TimeSpan.FromMilliseconds(100), "fixture false", () => elapsed, () => elapsed += TimeSpan.FromMilliseconds(50)));
        Assert.Equal(2, calls); Assert.Null(failure.InnerException);
        Assert.Contains("transient UIA failures: 0", failure.Message);
    }

    [Fact]
    public void RunRecordScope_RestoresPreviousLogger_AndDoesNotLeakBetweenRuns()
    {
        var outer = new List<string>(); var inner = new List<string>();
        void Ready() => PollingWait.WaitUntil(() => true, TimeSpan.FromSeconds(1), "fixture scope");
        using (PollingWait.Record(outer.Add))
        {
            using (PollingWait.Record(inner.Add)) Ready();
            Ready();
        }
        Ready();
        Assert.Single(outer); Assert.Single(inner);
        Assert.Contains("transient UIA failures: 0", outer[0]);
    }
}

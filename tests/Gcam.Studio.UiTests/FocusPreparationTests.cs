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
}

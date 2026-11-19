using OmpPricing.App.Providers;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Providers;

/// <summary>Per-provider rate limits from A5, proven with a manual clock.</summary>
public sealed class ThrottleTests
{
    [Fact]
    public void Daily_allowance_exhausts_the_provider()
    {
        var time = new ManualTimeProvider();
        var throttle = new Throttle(0, 1000, 1, time);

        Assert.Equal(ThrottleDecision.Granted, throttle.Acquire().Decision);
        Assert.Equal(ThrottleDecision.Exhausted, throttle.Acquire().Decision);
    }

    [Fact]
    public void Per_second_limit_reports_how_long_to_wait()
    {
        var time = new ManualTimeProvider();
        var throttle = new Throttle(1, 1000, 1000, time);

        Assert.Equal(ThrottleDecision.Granted, throttle.Acquire().Decision);

        var permit = throttle.Acquire();

        Assert.Equal(ThrottleDecision.Wait, permit.Decision);
        Assert.True(permit.Wait > TimeSpan.Zero);
        Assert.True(permit.Wait <= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Waiting_window_frees_the_next_permit()
    {
        var time = new ManualTimeProvider();
        var throttle = new Throttle(1, 1000, 1000, time);

        throttle.Acquire();
        Assert.Equal(ThrottleDecision.Wait, throttle.Acquire().Decision);

        time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(ThrottleDecision.Granted, throttle.Acquire().Decision);
    }

    [Fact]
    public void Per_minute_limit_is_a_sliding_window()
    {
        var time = new ManualTimeProvider();
        var throttle = new Throttle(0, 2, 0, time);

        Assert.Equal(ThrottleDecision.Granted, throttle.Acquire().Decision);
        Assert.Equal(ThrottleDecision.Granted, throttle.Acquire().Decision);

        var blocked = throttle.Acquire();
        Assert.Equal(ThrottleDecision.Wait, blocked.Decision);
        Assert.True(blocked.Wait > TimeSpan.FromSeconds(59));

        time.Advance(TimeSpan.FromSeconds(61));
        Assert.Equal(ThrottleDecision.Granted, throttle.Acquire().Decision);
    }

    [Fact]
    public void Zero_limits_mean_unlimited()
    {
        var throttle = new Throttle(0, 0, 0);

        for (var attempt = 0; attempt < 50; attempt++)
        {
            Assert.Equal(ThrottleDecision.Granted, throttle.Acquire().Decision);
        }
    }

    [Fact]
    public async Task Acquire_async_gives_up_instead_of_waiting_too_long()
    {
        var time = new ManualTimeProvider();
        var throttle = new Throttle(1, 1000, 1000, time);
        throttle.Acquire();

        // A manual clock cannot advance inside Task.Delay, so the long wait is returned, not awaited.
        var permit = await throttle.AcquireAsync(TimeSpan.FromSeconds(-1), CancellationToken.None);

        Assert.Equal(ThrottleDecision.Wait, permit.Decision);
    }
}

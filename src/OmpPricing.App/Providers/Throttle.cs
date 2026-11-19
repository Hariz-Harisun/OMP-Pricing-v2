namespace OmpPricing.App.Providers;

public enum ThrottleDecision
{
    Granted,
    Wait,
    Exhausted,
}

public sealed record ThrottlePermit(ThrottleDecision Decision, TimeSpan Wait);

/// <summary>
/// Shared rate limiter, configured per provider from A5. Sliding windows over the last second,
/// minute and 24 hours. A caller that gets <see cref="ThrottleDecision.Wait"/> waits and retries;
/// <see cref="ThrottleDecision.Exhausted"/> means the daily allowance is gone, so the row goes to
/// WaitingForQuota instead of burning calls (A6 default "Quota exhausted").
/// </summary>
public sealed class Throttle
{
    private readonly int _perSecond;
    private readonly int _perMinute;
    private readonly int _perDay;
    private readonly TimeProvider _time;
    private readonly List<DateTimeOffset> _stamps = [];
    private readonly object _gate = new();

    public Throttle(ProviderRateLimits limits, TimeProvider? timeProvider = null)
        : this(limits.PerSecond, limits.PerMinute, limits.PerDay, timeProvider)
    {
    }

    public Throttle(int perSecond, int perMinute, int perDay, TimeProvider? timeProvider = null)
    {
        _perSecond = perSecond;
        _perMinute = perMinute;
        _perDay = perDay;
        _time = timeProvider ?? TimeProvider.System;
    }

    public ThrottlePermit Acquire()
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            var dayStart = now.AddDays(-1);
            _stamps.RemoveAll(stamp => stamp <= dayStart);

            if (_perDay > 0 && _stamps.Count >= _perDay)
            {
                return new ThrottlePermit(ThrottleDecision.Exhausted, TimeSpan.Zero);
            }

            var wait = TimeSpan.Zero;
            if (_perSecond > 0)
            {
                wait = Longer(wait, WaitFor(now, TimeSpan.FromSeconds(1), _perSecond));
            }

            if (_perMinute > 0)
            {
                wait = Longer(wait, WaitFor(now, TimeSpan.FromMinutes(1), _perMinute));
            }

            if (wait > TimeSpan.Zero)
            {
                return new ThrottlePermit(ThrottleDecision.Wait, wait);
            }

            _stamps.Add(now);
            return new ThrottlePermit(ThrottleDecision.Granted, TimeSpan.Zero);
        }
    }

    /// <summary>Waits for a permit, but never longer than <paramref name="maxWait"/> per attempt.</summary>
    public async Task<ThrottlePermit> AcquireAsync(TimeSpan maxWait, CancellationToken cancellationToken)
    {
        while (true)
        {
            var permit = Acquire();
            if (permit.Decision != ThrottleDecision.Wait)
            {
                return permit;
            }

            if (permit.Wait > maxWait)
            {
                return new ThrottlePermit(ThrottleDecision.Wait, permit.Wait);
            }

            await Task.Delay(permit.Wait, _time, cancellationToken);
        }
    }

    private TimeSpan WaitFor(DateTimeOffset now, TimeSpan window, int limit)
    {
        var windowStart = now - window;
        var inWindow = _stamps.Count(stamp => stamp > windowStart);
        if (inWindow < limit)
        {
            return TimeSpan.Zero;
        }

        var oldest = _stamps.Where(stamp => stamp > windowStart).OrderBy(stamp => stamp).First();
        var wait = oldest + window - now;
        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    private static TimeSpan Longer(TimeSpan left, TimeSpan right) => right > left ? right : left;
}

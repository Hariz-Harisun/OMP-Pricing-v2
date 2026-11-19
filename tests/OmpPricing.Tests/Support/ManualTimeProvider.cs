namespace OmpPricing.Tests.Support;

/// <summary>TimeProvider with a fixed, advanceable UTC clock, so throttling and naming are testable.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public ManualTimeProvider(DateTimeOffset? start = null)
        => _now = start ?? new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}

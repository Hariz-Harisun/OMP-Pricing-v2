namespace OmpPricing.App.Providers;

/// <summary>
/// A lookup request. Provider-neutral: it carries the requested MPN, the manufacturer, the wildcard
/// prefix when the client wrote one, and the quantity that drives the price.
/// </summary>
public sealed record PartQuery
{
    public required string Mpn { get; init; }

    public string Mfg { get; init; } = "";

    /// <summary>Fixed part of a trailing wildcard MPN, for example BLM15GG221SZ1 of BLM15GG221SZ1x.</summary>
    public string? WildcardPrefix { get; init; }

    public int Qty { get; init; } = 1;

    /// <summary>
    /// A provider part number learned on an earlier run (state/match-cache.json). A provider may use
    /// it instead of the MPN, which is what resolves "Duplicate Products found".
    /// </summary>
    public string? ProviderPartNumber { get; init; }
}

public enum ProviderStatus
{
    Found,
    NotFound,
    Ambiguous,
    Throttled,
    Error,
}

/// <summary>The rate limits the provider reported on the last call, when the API sends them as headers.</summary>
public sealed record RateLimitInfo(int? Limit, int? Remaining);

/// <summary>What a provider answered for one <see cref="PartQuery"/>.</summary>
public sealed record ProviderResult(
    ProviderStatus Status,
    IReadOnlyList<Offer> Offers,
    string? Message = null,
    RateLimitInfo? RateLimit = null)
{
    public static ProviderResult Found(IReadOnlyList<Offer> offers, RateLimitInfo? rateLimit = null)
        => new(ProviderStatus.Found, offers, null, rateLimit);

    public static ProviderResult NotFound(string? message = null, RateLimitInfo? rateLimit = null)
        => new(ProviderStatus.NotFound, [], message, rateLimit);

    public static ProviderResult Ambiguous(string? message = null, RateLimitInfo? rateLimit = null)
        => new(ProviderStatus.Ambiguous, [], message, rateLimit);

    public static ProviderResult Throttled(string? message = null, RateLimitInfo? rateLimit = null)
        => new(ProviderStatus.Throttled, [], message, rateLimit);

    public static ProviderResult Error(string? message = null)
        => new(ProviderStatus.Error, [], message);
}

/// <summary>The one contract every distributor implements (AGENTS.md "Providers").</summary>
public interface IPriceProvider
{
    string Name { get; }

    Task<ProviderResult> FindAsync(PartQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// Built by each provider folder so <see cref="ProviderRegistry"/> can add a provider without
/// referencing any of its types. Returns null when the provider is disabled in configuration.
/// </summary>
public interface IPriceProviderFactory
{
    string Name { get; }

    IPriceProvider? Create(Microsoft.Extensions.Configuration.IConfigurationSection section);
}

/// <summary>Rate limits for one provider, read from configuration (A5). Zero or less means unlimited.</summary>
public sealed class ProviderRateLimits
{
    public int PerSecond { get; set; }

    public int PerMinute { get; set; }

    public int PerDay { get; set; }
}

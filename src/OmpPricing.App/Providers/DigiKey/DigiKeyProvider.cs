using OmpPricing.App.Providers;
using Serilog;

namespace OmpPricing.App.Providers.DigiKey;

/// <summary>DigiKey as a price provider: throttle, one product details call, map to offers.</summary>
public sealed class DigiKeyProvider : IPriceProvider
{
    public const string ProviderName = "DigiKey";

    private static readonly TimeSpan MaxThrottleWait = TimeSpan.FromSeconds(30);

    private readonly DigiKeyClient _client;
    private readonly Throttle _throttle;
    private readonly bool _isAuthorised;
    private readonly ILogger? _logger;

    public DigiKeyProvider(
        DigiKeyClient client,
        Throttle throttle,
        bool isAuthorised = true,
        ILogger? logger = null)
    {
        _client = client;
        _throttle = throttle;
        _isAuthorised = isAuthorised;
        _logger = logger;
    }

    public string Name => ProviderName;

    /// <summary>The rate-limit headers of the last call, kept for logging and diagnostics.</summary>
    public RateLimitInfo? LastRateLimit { get; private set; }

    public async Task<ProviderResult> FindAsync(PartQuery query, CancellationToken cancellationToken)
    {
        var permit = await _throttle.AcquireAsync(MaxThrottleWait, cancellationToken);

        if (permit.Decision == ThrottleDecision.Exhausted)
        {
            return ProviderResult.Throttled("DigiKey daily quota exhausted");
        }

        if (permit.Decision == ThrottleDecision.Wait)
        {
            return ProviderResult.Throttled(
                "DigiKey rate limit needs a wait of " + permit.Wait.TotalSeconds.ToString("0") + "s");
        }

        DigiKeyLookup lookup;
        try
        {
            lookup = await _client.GetProductDetailsAsync(query, cancellationToken);
        }
        catch (DigiKeyException exception)
        {
            _logger?.Warning("DigiKey token problem: {Message}", exception.Message);
            return ProviderResult.Error(exception.Message);
        }
        catch (HttpRequestException exception)
        {
            _logger?.Warning("DigiKey HTTP problem: {Message}", exception.Message);
            return ProviderResult.Error(exception.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProviderResult.Error("DigiKey request timed out");
        }

        LastRateLimit = lookup.RateLimit;

        if (lookup.RateLimit is not null)
        {
            _logger?.Debug(
                "DigiKey rate limit limit {Limit} remaining {Remaining}",
                lookup.RateLimit.Limit,
                lookup.RateLimit.Remaining);
        }

        if (lookup.Status != DigiKeyLookupStatus.Found || lookup.Product is null)
        {
            return lookup.Status switch
            {
                DigiKeyLookupStatus.Throttled => ProviderResult.Throttled(lookup.Message, lookup.RateLimit),
                DigiKeyLookupStatus.DuplicateProducts => ProviderResult.Ambiguous(lookup.Message, lookup.RateLimit),
                DigiKeyLookupStatus.NotFound => ProviderResult.NotFound(lookup.Message, lookup.RateLimit),
                _ => new ProviderResult(ProviderStatus.Error, [], lookup.Message, lookup.RateLimit),
            };
        }

        var offers = DigiKeyMapper.ToOffers(lookup.Product, _client.Options, _isAuthorised);
        return offers.Count == 0
            ? ProviderResult.NotFound("DigiKey returned no list-price variation", lookup.RateLimit)
            : ProviderResult.Found(offers, lookup.RateLimit);
    }
}

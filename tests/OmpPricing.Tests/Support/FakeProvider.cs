using OmpPricing.App.Providers;

namespace OmpPricing.Tests.Support;

/// <summary>
/// Test-only stand-in provider. Scripted answers, recorded calls, no network. The real app has no
/// fake pricer any more, so it can never write a made-up price.
/// </summary>
public sealed class FakeProvider : IPriceProvider
{
    private readonly Dictionary<string, ProviderResult> _byMpn = new(StringComparer.OrdinalIgnoreCase);
    private Func<PartQuery, ProviderResult>? _fallback;

    public FakeProvider(string name = "FakeProvider") => Name = name;

    public string Name { get; }

    public List<PartQuery> Calls { get; } = [];

    public FakeProvider Respond(string mpn, params Offer[] offers)
    {
        _byMpn[mpn] = ProviderResult.Found(offers);
        return this;
    }

    public FakeProvider RespondWith(string mpn, ProviderResult result)
    {
        _byMpn[mpn] = result;
        return this;
    }

    public FakeProvider RespondAll(Func<PartQuery, ProviderResult> fallback)
    {
        _fallback = fallback;
        return this;
    }

    public Task<ProviderResult> FindAsync(PartQuery query, CancellationToken cancellationToken)
    {
        Calls.Add(query);

        if (_byMpn.TryGetValue(query.Mpn, out var scripted))
        {
            return Task.FromResult(scripted);
        }

        if (_fallback is not null)
        {
            return Task.FromResult(_fallback(query));
        }

        return Task.FromResult(ProviderResult.NotFound("No scripted answer"));
    }

    /// <summary>One plausible list-price offer echoing the query, used for pipeline tests.</summary>
    public static FakeProvider Echoing(string name = "FakeProvider")
        => new FakeProvider(name).RespondAll(query => ProviderResult.Found([EchoOffer(query)]));

    public static Offer EchoOffer(
        PartQuery query,
        string? mfg = null,
        string? mpn = null,
        decimal price = 0.0125m,
        bool isMarketplace = false,
        bool isContractPrice = false)
        => new()
        {
            Provider = "FakeProvider",
            ProviderPartNumber = "FAKE-" + (mpn ?? query.Mpn),
            Mfg = mfg ?? query.Mfg,
            Mpn = mpn ?? query.Mpn,
            Currency = "USD",
            PriceBreaks = [new PriceBreak(Math.Max(query.Qty, 1), price)],
            Moq = 1,
            StandardPackage = 1,
            Packaging = "Tape & Reel (TR)",
            Stock = 100000,
            LeadWeeks = 12,
            RohsStatus = "ROHS3 Compliant",
            ReachStatus = "REACH Unaffected",
            IsAuthorisedProvider = true,
            IsMarketplace = isMarketplace,
            IsContractPrice = isContractPrice,
        };
}

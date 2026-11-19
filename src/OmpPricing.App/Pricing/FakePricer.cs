using OmpPricing.App.Providers;

namespace OmpPricing.App.Pricing;

/// <summary>
/// Temporary stand-in for the provider framework (Step 2). It returns one synthetic offer per
/// looked-up part so the Excel writing path can be exercised end to end in Step 1.
/// It is not a distributor, the prices are fake, and it makes no network call.
/// </summary>
public sealed class FakePricer
{
    private readonly List<string> _lookedUpMpns = [];

    /// <summary>MPNs the pipeline asked about, in request order. Generic rows must never appear here.</summary>
    public IReadOnlyList<string> LookedUpMpns => _lookedUpMpns;

    public int LookupCount => _lookedUpMpns.Count;

    public Task<IReadOnlyList<Offer>> GetOffersAsync(QuoteLine line, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _lookedUpMpns.Add(line.Mpn);

        var unitPrice = 0.01m + (line.RowNumber % 7) * 0.001m;

        Offer offer = new()
        {
            Provider = "FakeDistributor",
            Mfg = line.Mfg,
            Mpn = line.Mpn,
            Currency = "USD",
            PriceBreaks = [new PriceBreak(line.Qty ?? 1, unitPrice)],
            Moq = 1,
            StandardPackage = 1,
            Packaging = "Tape & Reel (TR)",
            Stock = 100000,
            LeadWeeks = 12,
            RohsStatus = "ROHS3 Compliant",
            ReachStatus = "REACH Unaffected",
            IsAuthorisedProvider = true,
        };

        IReadOnlyList<Offer> offers = [offer];
        return Task.FromResult(offers);
    }
}

using OmpPricing.App.Providers;

namespace OmpPricing.Tests.Providers;

/// <summary>
/// The contract every provider must satisfy (AGENTS.md "Adding a provider, checklist"). Each
/// provider test class calls these with its own fixtures, so the same rules are proven twice.
/// </summary>
internal static class ProviderContract
{
    public static void UnknownPart(ProviderResult result)
    {
        Assert.Equal(ProviderStatus.NotFound, result.Status);
        Assert.Empty(result.Offers);
    }

    public static void RateLimited(ProviderResult result)
    {
        Assert.Equal(ProviderStatus.Throttled, result.Status);
        Assert.Empty(result.Offers);
    }

    public static void MarketplaceFlagged(Offer offer)
    {
        Assert.True(offer.IsMarketplace);
    }

    public static void ListPricesOnly(ProviderResult result)
    {
        Assert.NotEmpty(result.Offers);
        Assert.All(result.Offers, offer => Assert.False(offer.IsContractPrice));
        Assert.All(result.Offers, offer => Assert.NotEmpty(offer.PriceBreaks));
        Assert.All(result.Offers, offer => Assert.All(offer.PriceBreaks, priceBreak => Assert.True(priceBreak.UnitPrice > 0m)));
    }
}

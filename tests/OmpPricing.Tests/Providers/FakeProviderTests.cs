using OmpPricing.App.Providers;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Providers;

/// <summary>The test-only FakeProvider passes the same contract as a real provider.</summary>
public sealed class FakeProviderTests
{
    [Fact]
    public async Task Unknown_part_returns_not_found()
    {
        var provider = new FakeProvider();

        var result = await provider.FindAsync(DigiKeyTestSetup.Query("NO-SUCH-PART"), CancellationToken.None);

        ProviderContract.UnknownPart(result);
    }

    [Fact]
    public async Task Rate_limited_answer_returns_throttled()
    {
        var provider = new FakeProvider().RespondWith("BLM18HE601SN1", ProviderResult.Throttled("quota"));

        var result = await provider.FindAsync(DigiKeyTestSetup.Query("BLM18HE601SN1"), CancellationToken.None);

        ProviderContract.RateLimited(result);
    }

    [Fact]
    public async Task Marketplace_offer_is_flagged()
    {
        var offer = FakeProvider.EchoOffer(DigiKeyTestSetup.Query("BAT54CW,115"), isMarketplace: true);
        var provider = new FakeProvider().Respond("BAT54CW,115", offer);

        var result = await provider.FindAsync(DigiKeyTestSetup.Query("BAT54CW,115"), CancellationToken.None);

        ProviderContract.MarketplaceFlagged(Assert.Single(result.Offers));
    }

    [Fact]
    public async Task Only_list_prices_come_back()
    {
        var provider = FakeProvider.Echoing();

        var result = await provider.FindAsync(DigiKeyTestSetup.Query("BLM18HE601SN1", "Murata"), CancellationToken.None);

        ProviderContract.ListPricesOnly(result);
    }

    [Fact]
    public async Task Calls_are_recorded_for_assertions()
    {
        var provider = FakeProvider.Echoing();

        await provider.FindAsync(DigiKeyTestSetup.Query("BLM18HE601SN1", "Murata"), CancellationToken.None);

        Assert.Single(provider.Calls);
        Assert.Equal("BLM18HE601SN1", provider.Calls[0].Mpn);
    }
}

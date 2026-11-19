using System.Net;
using OmpPricing.App.Providers;
using OmpPricing.App.Providers.DigiKey;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Providers.DigiKey;

/// <summary>Mapping DigiKey responses to the normalized offer, and the provider contract.</summary>
public sealed class DigiKeyProviderTests
{
    private static CancellationToken Token => CancellationToken.None;

    [Fact]
    public async Task Fixture_gives_one_offer_per_packaging_variation_with_the_documented_numbers()
    {
        var (provider, handler) = DigiKeyTestSetup.CreateProvider();
        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondJson(DigiKeyTestSetup.Fixture("blm15gg221sz1d-productdetails.json"));

        var result = await provider.FindAsync(DigiKeyTestSetup.Query("BLM15GG221SZ1D", "Murata", 1), Token);

        Assert.Equal(ProviderStatus.Found, result.Status);
        Assert.Equal(3, result.Offers.Count);

        var reel = Assert.Single(result.Offers, offer => offer.Packaging == "Tape & Reel (TR)");
        Assert.Equal(10000, reel.Moq);
        Assert.Equal(10000, reel.StandardPackage);
        Assert.Equal(0.213m, Assert.Single(reel.PriceBreaks).UnitPrice);
        Assert.Equal(0m, reel.FixedFee);
        Assert.Equal(0, reel.Stock);
        Assert.Equal(24, reel.LeadWeeks);
        Assert.Equal("Murata Electronics", reel.Mfg);
        Assert.Equal("BLM15GG221SZ1D", reel.Mpn);
        Assert.Equal("490-BLM15GG221SZ1DTR-ND", reel.ProviderPartNumber);
        Assert.Equal("USD", reel.Currency);
        Assert.False(reel.IsMarketplace);
        Assert.True(reel.TariffActive);

        var digiReel = Assert.Single(result.Offers, offer => offer.Packaging == "Digi-Reel®");
        Assert.Equal(7.00m, digiReel.FixedFee);
        Assert.Equal(1, digiReel.Moq);
    }

    [Fact]
    public async Task Contract_pricing_is_never_used_as_a_list_price()
    {
        var (provider, handler) = DigiKeyTestSetup.CreateProvider();
        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondJson(DigiKeyTestSetup.Fixture("bat54cw115-nexperia-productdetails.json"));

        var result = await provider.FindAsync(DigiKeyTestSetup.Query("BAT54CW,115", "NXP"), Token);

        Assert.Equal(ProviderStatus.Found, result.Status);
        Assert.Equal(3, result.Offers.Count);
        Assert.All(result.Offers, offer => Assert.False(offer.IsContractPrice));
    }

    [Fact]
    public async Task Marketplace_offer_is_flagged()
    {
        var (provider, handler) = DigiKeyTestSetup.CreateProvider();
        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondJson(DigiKeyTestSetup.Fixture("marketplace-productdetails.json"));

        var result = await provider.FindAsync(DigiKeyTestSetup.Query("BAT54CW,115", "NXP"), Token);

        var offer = Assert.Single(result.Offers);
        Assert.True(offer.IsMarketplace);
    }

    [Fact]
    public async Task Unknown_part_returns_not_found()
    {
        var (provider, handler) = DigiKeyTestSetup.CreateProvider();
        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondStatus(HttpStatusCode.NotFound, "{\"detail\":\"Product not found\"}");

        var result = await provider.FindAsync(DigiKeyTestSetup.Query("NO-SUCH-PART"), Token);

        ProviderContract.UnknownPart(result);
    }

    [Fact]
    public async Task Http_429_returns_throttled()
    {
        var (provider, handler) = DigiKeyTestSetup.CreateProvider();
        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondStatus(HttpStatusCode.TooManyRequests);

        var result = await provider.FindAsync(DigiKeyTestSetup.Query("BLM18HE601SN1", "Murata"), Token);

        ProviderContract.RateLimited(result);
    }

    [Fact]
    public async Task Rate_limit_headers_of_the_last_call_are_kept_on_the_provider()
    {
        var (provider, handler) = DigiKeyTestSetup.CreateProvider();
        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondJson(
                DigiKeyTestSetup.Fixture("blm15gg221sz1d-productdetails.json"),
                configure: response =>
                {
                    response.Headers.Add("X-RateLimit-Limit", "120");
                    response.Headers.Add("X-RateLimit-Remaining", "117");
                });

        await provider.FindAsync(DigiKeyTestSetup.Query("BLM18HE601SN1", "Murata"), Token);

        Assert.NotNull(provider.LastRateLimit);
        Assert.Equal(120, provider.LastRateLimit!.Limit);
        Assert.Equal(117, provider.LastRateLimit.Remaining);
    }

    [Fact]
    public async Task Token_failure_is_an_error_result_not_an_exception()
    {
        var (provider, handler) = DigiKeyTestSetup.CreateProvider();
        handler.RespondStatus(HttpStatusCode.Unauthorized, "{\"detail\":\"invalid_client\"}");

        var result = await provider.FindAsync(DigiKeyTestSetup.Query("BLM18HE601SN1", "Murata"), Token);

        Assert.Equal(ProviderStatus.Error, result.Status);
        Assert.Contains("token request failed", result.Message);
    }

    [Fact]
    public void Lead_weeks_are_parsed_from_the_api_string()
    {
        Assert.Equal(24, DigiKeyMapper.ParseLeadWeeks("24"));
        Assert.Equal(24, DigiKeyMapper.ParseLeadWeeks("24 Weeks"));
        Assert.Equal(8, DigiKeyMapper.ParseLeadWeeks("8.0"));
        Assert.Null(DigiKeyMapper.ParseLeadWeeks(""));
        Assert.Null(DigiKeyMapper.ParseLeadWeeks(null));
    }
}

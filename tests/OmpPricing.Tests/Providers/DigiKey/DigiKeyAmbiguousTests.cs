using System.Net;
using OmpPricing.App.Providers;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Providers.DigiKey;

/// <summary>
/// Decision D8: an unresolved duplicate stays Ambiguous (NeedsReview) and the candidate
/// manufacturers the keyword search reported are named, so purchasing can pick one.
/// </summary>
public sealed class DigiKeyAmbiguousTests
{
    private static readonly CancellationToken Token = CancellationToken.None;

    private static void QueueDuplicateThenKeywordThenDuplicate(StubHttpMessageHandler handler)
    {
        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondStatus(HttpStatusCode.NotFound, "{\"detail\":\"Duplicate Products found\"}")
            .RespondJson(DigiKeyTestSetup.Fixture("bat54cw115-keyword.json"))
            .RespondStatus(HttpStatusCode.NotFound, "{\"detail\":\"Duplicate Products found\"}");
    }

    [Fact]
    public async Task Unresolved_duplicate_names_every_candidate_manufacturer()
    {
        var (provider, handler) = DigiKeyTestSetup.CreateProvider();
        QueueDuplicateThenKeywordThenDuplicate(handler);

        var result = await provider.FindAsync(DigiKeyTestSetup.Query("BAT54CW,115", "UnKnownMfg"), Token);

        Assert.Equal(ProviderStatus.Ambiguous, result.Status);
        Assert.Empty(result.Offers);
        Assert.Contains("Duplicate Products found", result.Message);
        Assert.Contains("NXP", result.Message);
        Assert.Contains("Diodes", result.Message);
    }

    [Fact]
    public async Task A_known_manufacturer_still_resolves_through_the_retry()
    {
        var (provider, handler) = DigiKeyTestSetup.CreateProvider();
        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondStatus(HttpStatusCode.NotFound, "{\"detail\":\"Duplicate Products found\"}")
            .RespondJson(DigiKeyTestSetup.Fixture("bat54cw115-keyword.json"))
            .RespondJson(DigiKeyTestSetup.Fixture("bat54cw115-productdetails.json"));

        var result = await provider.FindAsync(DigiKeyTestSetup.Query("BAT54CW,115", "NXP"), Token);

        Assert.Equal(ProviderStatus.Found, result.Status);
        Assert.NotEmpty(result.Offers);
        Assert.Contains("manufacturerId=281", handler.Requests[3].Uri);
    }
}

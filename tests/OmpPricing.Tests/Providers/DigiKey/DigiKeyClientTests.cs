using System.Net;
using OmpPricing.App.Providers;
using OmpPricing.App.Providers.DigiKey;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Providers.DigiKey;

/// <summary>DigiKey v4 HTTP behaviour, proven against saved fixtures and a stub handler.</summary>
public sealed class DigiKeyClientTests
{
    private static CancellationToken Token => CancellationToken.None;

    [Fact]
    public async Task Token_request_is_form_urlencoded_with_client_credentials()
    {
        var (client, handler) = DigiKeyTestSetup.CreateClient();
        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondJson(DigiKeyTestSetup.Fixture("blm18he601sn1-productdetails.json"));

        var result = await client.GetProductDetailsAsync(
            DigiKeyTestSetup.Query("BLM18HE601SN1", "Murata"),
            Token);

        Assert.Equal(DigiKeyLookupStatus.Found, result.Status);

        var tokenRequest = handler.Requests[0];
        Assert.Equal("POST", tokenRequest.Method);
        Assert.Contains("/v1/oauth2/token", tokenRequest.Uri);
        Assert.Contains("application/x-www-form-urlencoded", tokenRequest.Header("Content-Type"));
        Assert.Contains("grant_type=client_credentials", tokenRequest.Body);
        Assert.Contains("client_id=unit-test-client", tokenRequest.Body);
    }

    [Fact]
    public async Task Token_is_reused_until_it_expires_and_then_renewed()
    {
        var time = new ManualTimeProvider();
        var (client, handler) = DigiKeyTestSetup.CreateClient(time);
        var fixture = DigiKeyTestSetup.Fixture("blm18he601sn1-productdetails.json");

        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondJson(fixture)
            .RespondJson(fixture)
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondJson(fixture);

        await client.GetProductDetailsAsync(DigiKeyTestSetup.Query("BLM18HE601SN1", "Murata"), Token);
        await client.GetProductDetailsAsync(DigiKeyTestSetup.Query("BLM18HE601SN1", "Murata"), Token);

        Assert.Equal(3, handler.Requests.Count);
        Assert.Single(handler.Requests, request => request.Uri.Contains("/v1/oauth2/token", StringComparison.Ordinal));

        time.Advance(TimeSpan.FromSeconds(700));
        await client.GetProductDetailsAsync(DigiKeyTestSetup.Query("BLM18HE601SN1", "Murata"), Token);

        Assert.Equal(5, handler.Requests.Count);
        Assert.Equal(2, handler.Requests.Count(request => request.Uri.Contains("/v1/oauth2/token", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Details_call_sends_the_required_headers()
    {
        var (client, handler) = DigiKeyTestSetup.CreateClient();
        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondJson(DigiKeyTestSetup.Fixture("blm18he601sn1-productdetails.json"));

        await client.GetProductDetailsAsync(DigiKeyTestSetup.Query("BLM18HE601SN1", "Murata"), Token);

        var details = handler.Requests[1];
        Assert.Equal("GET", details.Method);
        Assert.Equal("Bearer test-token", details.Header("Authorization"));
        Assert.Equal("unit-test-client", details.Header("X-DIGIKEY-Client-Id"));
        Assert.Equal("unit-test-account", details.Header("X-DIGIKEY-Account-Id"));
        Assert.Equal("US", details.Header("X-DIGIKEY-Locale-Site"));
        Assert.Equal("en", details.Header("X-DIGIKEY-Locale-Language"));
        Assert.Equal("USD", details.Header("X-DIGIKEY-Locale-Currency"));
        Assert.Contains("/products/v4/search/BLM18HE601SN1/productdetails", details.Uri);
    }

    [Fact]
    public async Task Comma_in_the_mpn_is_url_encoded()
    {
        var (client, handler) = DigiKeyTestSetup.CreateClient();
        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondJson(DigiKeyTestSetup.Fixture("bat54cw115-productdetails.json"));

        await client.GetProductDetailsAsync(DigiKeyTestSetup.Query("BAT54CW,115", "NXP"), Token);

        Assert.Contains("BAT54CW%2C115", handler.Requests[1].Uri);
    }

    [Fact]
    public async Task Duplicate_products_retries_with_the_manufacturer_id()
    {
        var (client, handler) = DigiKeyTestSetup.CreateClient();
        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondStatus(HttpStatusCode.NotFound, "{\"detail\":\"Duplicate Products found\"}")
            .RespondJson(DigiKeyTestSetup.Fixture("bat54cw115-keyword.json"))
            .RespondJson(DigiKeyTestSetup.Fixture("bat54cw115-productdetails.json"));

        var result = await client.GetProductDetailsAsync(
            DigiKeyTestSetup.Query("BAT54CW,115", "NXP"),
            Token);

        Assert.Equal(DigiKeyLookupStatus.Found, result.Status);
        Assert.True(result.UsedManufacturerRetry);
        Assert.Equal("BAT54CW,115", result.Product?.ManufacturerProductNumber);

        var keyword = handler.Requests[2];
        Assert.Equal("POST", keyword.Method);
        Assert.Contains("/products/v4/search/keyword", keyword.Uri);
        Assert.Contains("\"Keywords\":\"BAT54CW,115\"", keyword.Body);

        var retried = handler.Requests[3];
        Assert.Contains("manufacturerId=281", retried.Uri);
    }

    [Fact]
    public async Task Duplicate_products_without_a_resolvable_manufacturer_is_ambiguous()
    {
        var (client, handler) = DigiKeyTestSetup.CreateClient();
        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondStatus(HttpStatusCode.NotFound, "{\"detail\":\"Duplicate Products found\"}")
            .RespondStatus(HttpStatusCode.NotFound, "{\"detail\":\"nothing\"}");

        var result = await client.GetProductDetailsAsync(
            DigiKeyTestSetup.Query("BAT54CW,115", "NXP"),
            Token);

        // Decision D8: an unresolved duplicate stays Ambiguous and the candidates are reported.
        Assert.Equal(DigiKeyLookupStatus.DuplicateProducts, result.Status);
        Assert.Contains("Duplicate Products found", result.Message);
        Assert.Contains("Candidates:", result.Message);
    }

    [Fact]
    public async Task Http_429_is_throttled_and_the_rate_limit_headers_are_kept()
    {
        var (client, handler) = DigiKeyTestSetup.CreateClient();
        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondStatus(HttpStatusCode.TooManyRequests, "", response =>
            {
                response.Headers.Add("X-RateLimit-Limit", "120");
                response.Headers.Add("X-RateLimit-Remaining", "0");
            });

        var result = await client.GetProductDetailsAsync(DigiKeyTestSetup.Query("BLM18HE601SN1", "Murata"), Token);

        Assert.Equal(DigiKeyLookupStatus.Throttled, result.Status);
        Assert.NotNull(result.RateLimit);
        Assert.Equal(120, result.RateLimit!.Limit);
        Assert.Equal(0, result.RateLimit.Remaining);
    }

    [Fact]
    public async Task Unknown_part_is_not_found()
    {
        var (client, handler) = DigiKeyTestSetup.CreateClient();
        handler
            .RespondJson(DigiKeyTestSetup.TokenJson)
            .RespondStatus(HttpStatusCode.NotFound, "{\"detail\":\"Product not found\"}");

        var result = await client.GetProductDetailsAsync(DigiKeyTestSetup.Query("NO-SUCH-PART"), Token);

        Assert.Equal(DigiKeyLookupStatus.NotFound, result.Status);
        Assert.Equal("Product not found", result.Message);
    }
}

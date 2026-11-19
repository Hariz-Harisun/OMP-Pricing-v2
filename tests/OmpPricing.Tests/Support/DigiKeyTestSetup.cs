using System.Text.Json;
using OmpPricing.App.Providers;
using OmpPricing.App.Providers.DigiKey;

namespace OmpPricing.Tests.Support;

/// <summary>Builds a DigiKey client over a stub handler. Test credentials are fake and are not secrets.</summary>
internal static class DigiKeyTestSetup
{
    public const string TestHost = "https://api.digikey.test";

    public const string TokenJson =
        "{\"access_token\":\"test-token\",\"expires_in\":599,\"token_type\":\"Bearer\"}";

    public static DigiKeyOptions Options(TimeProvider? time = null) => new()
    {
        Enabled = true,
        ClientId = "unit-test-client",
        ClientSecret = "unit-test-secret",
        AccountId = "unit-test-account",
        Site = "US",
        Language = "en",
        Currency = "USD",
        RateLimits = new ProviderRateLimits { PerSecond = 0, PerMinute = 1000, PerDay = 1000 },
    };

    public static string Fixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Providers", "DigiKey", "fixtures", name);
        return File.ReadAllText(path);
    }

    public static (DigiKeyClient Client, StubHttpMessageHandler Handler) CreateClient(
        TimeProvider? time = null)
    {
        var handler = new StubHttpMessageHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri(TestHost) };
        var client = new DigiKeyClient(http, Options(), time);
        return (client, handler);
    }

    public static (DigiKeyProvider Provider, StubHttpMessageHandler Handler) CreateProvider(bool isAuthorised = true)
    {
        var (client, handler) = CreateClient();
        var provider = new DigiKeyProvider(client, new Throttle(0, 1000, 1000), isAuthorised);
        return (provider, handler);
    }

    public static PartQuery Query(string mpn, string mfg = "", int qty = 1, string? wildcardPrefix = null)
        => new() { Mpn = mpn, Mfg = mfg, Qty = qty, WildcardPrefix = wildcardPrefix };

    public static string Serialize(object value) => JsonSerializer.Serialize(value);
}

using System.Net;
using System.Text.Json;
using OmpPricing.App.Pipeline;
using OmpPricing.App.Providers;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Pipeline;

/// <summary>
/// `--probe` writes a credential-free record of a real call, so the UNVERIFIED parts of A5 can be
/// checked against the live API (decision D7).
/// </summary>
public sealed class ProbeTests : IDisposable
{
    private readonly string _state = Sample.NewTempFolder();
    private readonly StringWriter _out = new();

    public void Dispose()
    {
        try
        {
            Directory.Delete(_state, true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Probe_saves_the_raw_response_and_prints_a_summary()
    {
        var (provider, handler) = DigiKeyTestSetup.CreateProvider();
        handler.RespondJson(DigiKeyTestSetup.TokenJson);
        handler.RespondJson(
            DigiKeyTestSetup.Fixture("blm18he601sn1-productdetails.json"),
            configure: response =>
            {
                response.Headers.TryAddWithoutValidation("X-RateLimit-Limit", "120");
                response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "118");
            });

        var report = await new ProbeRunner(_state).RunAsync(provider, "BLM18HE601SN1", 400, _out, CancellationToken.None);

        Assert.Equal(ProviderStatus.Found, report.Result.Status);
        Assert.Equal(3, report.Result.Offers.Count);
        Assert.True(File.Exists(report.FilePath));
        Assert.Matches(@"DigiKey_BLM18HE601SN1_\d{8}-\d{6}\.json$", Path.GetFileName(report.FilePath));

        var saved = File.ReadAllText(report.FilePath);
        using var document = JsonDocument.Parse(saved);
        var root = document.RootElement;

        Assert.Equal("DigiKey", root.GetProperty("probe").GetProperty("provider").GetString());
        Assert.Equal(400, root.GetProperty("probe").GetProperty("qty").GetInt32());
        Assert.Equal("Found", root.GetProperty("result").GetProperty("status").GetString());
        Assert.Equal(120, root.GetProperty("result").GetProperty("rateLimit").GetProperty("limit").GetInt32());

        var exchange = root.GetProperty("exchanges")[0];
        Assert.Equal("GET", exchange.GetProperty("request").GetProperty("method").GetString());
        Assert.True(exchange.GetProperty("response").GetProperty("status").GetInt32() == 200);
        Assert.Equal(
            "120",
            exchange.GetProperty("response").GetProperty("headers").GetProperty("X-RateLimit-Limit").GetString());
        Assert.Equal(
            "BLM18HE601SN1D",
            exchange.GetProperty("response").GetProperty("body").GetProperty("Products")[0]
                .GetProperty("ManufacturerProductNumber").GetString());

        // One request per lookup: the token call is not captured.
        Assert.Single(root.GetProperty("exchanges").EnumerateArray());

        var summary = _out.ToString();
        Assert.Contains("DigiKey probe: BLM18HE601SN1 qty 400", summary);
        Assert.Contains("490-5214-2-ND", summary);
        Assert.Contains("MOQ 4000", summary);
        Assert.Contains("Tape & Reel (TR)", summary);
        Assert.Contains("0.08825", summary);
        Assert.Contains("Rate limit: 120 limit, 118 remaining", summary);
    }

    [Fact]
    public async Task Probe_file_holds_no_token_client_id_or_account_id()
    {
        var (provider, handler) = DigiKeyTestSetup.CreateProvider();
        handler.RespondJson(DigiKeyTestSetup.TokenJson);
        handler.RespondJson(DigiKeyTestSetup.Fixture("blm18he601sn1-productdetails.json"));

        var report = await new ProbeRunner(_state).RunAsync(provider, "BLM18HE601SN1", 1, _out, CancellationToken.None);
        var saved = File.ReadAllText(report.FilePath);

        Assert.DoesNotContain("test-token", saved);
        Assert.DoesNotContain("Bearer", saved);
        Assert.DoesNotContain("Authorization", saved);
        Assert.DoesNotContain("unit-test-client", saved);
        Assert.DoesNotContain("unit-test-account", saved);
        Assert.DoesNotContain("unit-test-secret", saved);
        Assert.DoesNotContain("client_secret", saved);

        // The locale headers are kept: they show what DigiKey actually applied.
        Assert.Contains("X-DIGIKEY-Locale-Site", saved);
    }

    [Fact]
    public async Task Probe_of_an_unknown_part_still_saves_the_error_response()
    {
        var (provider, handler) = DigiKeyTestSetup.CreateProvider();
        handler.RespondJson(DigiKeyTestSetup.TokenJson);
        handler.RespondStatus(HttpStatusCode.NotFound, "{\"detail\":\"Part not found\"}");

        var report = await new ProbeRunner(_state).RunAsync(provider, "NO-SUCH-PART", 1, _out, CancellationToken.None);

        Assert.Equal(ProviderStatus.NotFound, report.Result.Status);

        using var document = JsonDocument.Parse(File.ReadAllText(report.FilePath));
        var exchange = document.RootElement.GetProperty("exchanges")[0];

        Assert.Equal(404, exchange.GetProperty("response").GetProperty("status").GetInt32());
        Assert.Equal(
            "Part not found",
            exchange.GetProperty("response").GetProperty("body").GetProperty("detail").GetString());
        Assert.Contains("Status: NotFound", _out.ToString());
    }

    [Fact]
    public async Task A_429_probe_is_reported_as_throttled_with_its_headers()
    {
        var (provider, handler) = DigiKeyTestSetup.CreateProvider();
        handler.RespondJson(DigiKeyTestSetup.TokenJson);
        handler.RespondStatus(
            HttpStatusCode.TooManyRequests,
            "{\"detail\":\"too many\"}",
            response => response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "0"));

        var report = await new ProbeRunner(_state).RunAsync(provider, "BLM18HE601SN1", 1, _out, CancellationToken.None);

        Assert.Equal(ProviderStatus.Throttled, report.Result.Status);
        Assert.Contains("Rate limit: ? limit, 0 remaining", _out.ToString());
    }

    [Fact]
    public void Probe_target_is_found_by_name_and_a_typo_reports_the_enabled_providers()
    {
        var (provider, _) = DigiKeyTestSetup.CreateProvider();

        Assert.Same(provider, ProbeTarget.Resolve([provider], "digikey"));
        Assert.Null(ProbeTarget.Resolve([provider], "Mouser"));
        Assert.Equal("DigiKey", ProbeTarget.EnabledNames([provider]));
        Assert.Equal("(none)", ProbeTarget.EnabledNames([]));
    }
}

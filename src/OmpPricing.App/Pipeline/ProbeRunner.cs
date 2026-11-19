using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using OmpPricing.App.Providers;

namespace OmpPricing.App.Pipeline;

/// <summary>Finds the provider named on the command line, so a typo is reported instead of guessed.</summary>
public static class ProbeTarget
{
    public static IPriceProvider? Resolve(IReadOnlyList<IPriceProvider> providers, string name)
        => providers.FirstOrDefault(provider => string.Equals(provider.Name, name, StringComparison.OrdinalIgnoreCase));

    public static string EnabledNames(IReadOnlyList<IPriceProvider> providers)
        => providers.Count == 0 ? "(none)" : string.Join(", ", providers.Select(provider => provider.Name));
}

public sealed record ProbeReport(
    string Provider,
    string Mpn,
    int Qty,
    ProviderResult Result,
    string FilePath);

/// <summary>
/// `--probe &lt;provider&gt; &lt;MPN&gt;`: calls one real provider for one part, prints a readable summary of
/// the normalized offers and saves the raw exchanges to state/probe/ so the assumptions in A5 can be
/// checked against the live API. Credentials never reach the file (see <see cref="ProbeSanitizer"/>).
/// </summary>
public sealed class ProbeRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _probeFolder;
    private readonly TimeProvider _time;

    public ProbeRunner(string stateFolder, TimeProvider? timeProvider = null)
    {
        _probeFolder = Path.Combine(stateFolder, "probe");
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<ProbeReport> RunAsync(
        IPriceProvider provider,
        string mpn,
        int qty,
        TextWriter output,
        CancellationToken cancellationToken,
        string? mfg = null)
    {
        if (provider is IProbeSource source)
        {
            source.ResetExchanges();
        }

        var query = new PartQuery { Mpn = mpn, Mfg = mfg ?? "", Qty = qty };
        ProviderResult result;
        try
        {
            result = await provider.FindAsync(query, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            result = ProviderResult.Error(exception.Message);
        }

        var exchanges = provider is IProbeSource captured ? captured.Exchanges : [];
        var capturedAt = _time.GetUtcNow();
        var path = Save(provider.Name, mpn, qty, mfg, result, exchanges, capturedAt);

        WriteSummary(output, provider.Name, mpn, qty, mfg, result, exchanges, path);
        return new ProbeReport(provider.Name, mpn, qty, result, path);
    }

    private string Save(
        string provider,
        string mpn,
        int qty,
        string? mfg,
        ProviderResult result,
        IReadOnlyList<ProbeExchange> exchanges,
        DateTimeOffset capturedAt)
    {
        Directory.CreateDirectory(_probeFolder);

        var document = new JsonObject
        {
            ["probe"] = new JsonObject
            {
                ["provider"] = provider,
                ["mpn"] = mpn,
                ["mfg"] = mfg ?? "",
                ["qty"] = qty,
                ["capturedAtUtc"] = capturedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                ["note"] = "Credentials are removed: bearer tokens, cookies, API keys and "
                    + "client/account ids. The token request is never recorded.",
            },
            ["result"] = new JsonObject
            {
                ["status"] = result.Status.ToString(),
                ["message"] = result.Message,
                ["rateLimit"] = result.RateLimit is null
                    ? null
                    : new JsonObject
                    {
                        ["limit"] = result.RateLimit.Limit,
                        ["remaining"] = result.RateLimit.Remaining,
                    },
            },
            ["offers"] = new JsonArray(result.Offers.Select(ToJson).ToArray()),
            ["exchanges"] = new JsonArray(exchanges.Select(ToJson).ToArray()),
        };

        var name = provider + "_" + SafeFileName(mpn) + "_"
            + capturedAt.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json";
        var path = Path.Combine(_probeFolder, name);

        File.WriteAllText(path, document.ToJsonString(JsonOptions));
        return path;
    }

    private static JsonNode ToJson(Offer offer) => new JsonObject
    {
        ["provider"] = offer.Provider,
        ["providerPartNumber"] = offer.ProviderPartNumber,
        ["mfg"] = offer.Mfg,
        ["mpn"] = offer.Mpn,
        ["currency"] = offer.Currency,
        ["packaging"] = offer.Packaging,
        ["moq"] = offer.Moq,
        ["standardPackage"] = offer.StandardPackage,
        ["stock"] = offer.Stock,
        ["leadWeeks"] = offer.LeadWeeks,
        ["rohs"] = offer.RohsStatus,
        ["reach"] = offer.ReachStatus,
        ["countryOfOrigin"] = offer.CountryOfOrigin,
        ["marketplace"] = offer.IsMarketplace,
        ["tariffActive"] = offer.TariffActive,
        ["ncnr"] = offer.Ncnr,
        ["discontinued"] = offer.Discontinued,
        ["endOfLife"] = offer.EndOfLife,
        ["fixedFee"] = offer.FixedFee,
        ["contractPrice"] = offer.IsContractPrice,
        ["priceBreaks"] = new JsonArray(offer.PriceBreaks
            .Select(priceBreak => (JsonNode)new JsonObject
            {
                ["quantity"] = priceBreak.Quantity,
                ["unitPrice"] = priceBreak.UnitPrice,
            })
            .ToArray()),
    };

    private static JsonNode ToJson(ProbeExchange exchange)
    {
        var headers = new JsonObject();
        foreach (var (name, value) in exchange.Request.Headers)
        {
            headers[name] = value;
        }

        var responseHeaders = new JsonObject();
        foreach (var (name, value) in exchange.Response.Headers)
        {
            responseHeaders[name] = value;
        }

        return new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["method"] = exchange.Request.Method,
                ["url"] = exchange.Request.Url,
                ["headers"] = headers,
                ["body"] = exchange.Request.Body,
            },
            ["response"] = new JsonObject
            {
                ["status"] = exchange.Response.Status,
                ["headers"] = responseHeaders,
                ["body"] = AsJsonOrString(exchange.Response.Body),
            },
        };
    }

    /// <summary>Embeds the raw body as JSON when it parses, so the file stays readable.</summary>
    private static JsonNode? AsJsonOrString(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return JsonValue.Create(body);
        }
    }

    private static void WriteSummary(
        TextWriter output,
        string provider,
        string mpn,
        int qty,
        string? mfg,
        ProviderResult result,
        IReadOnlyList<ProbeExchange> exchanges,
        string path)
    {
        output.WriteLine(
            $"{provider} probe: {mpn} qty {qty}"
            + (string.IsNullOrWhiteSpace(mfg) ? "" : $" mfg {mfg}"));
        output.WriteLine($"Status: {result.Status}");

        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            output.WriteLine($"Message: {result.Message}");
        }

        if (result.RateLimit is not null)
        {
            output.WriteLine(
                "Rate limit: "
                + (result.RateLimit.Limit?.ToString(CultureInfo.InvariantCulture) ?? "?")
                + " limit, "
                + (result.RateLimit.Remaining?.ToString(CultureInfo.InvariantCulture) ?? "?")
                + " remaining");
        }

        output.WriteLine($"Offers: {result.Offers.Count}");

        var index = 1;
        foreach (var offer in result.Offers)
        {
            output.WriteLine(
                $"  {index++}. {offer.ProviderPartNumber} | {offer.Mpn} | {offer.Mfg} | {offer.Packaging ?? "-"}");
            output.WriteLine(
                "     MOQ " + Show(offer.Moq)
                + "  SPQ " + Show(offer.StandardPackage)
                + "  stock " + Show(offer.Stock)
                + "  lead " + (offer.LeadWeeks is null ? "-" : offer.LeadWeeks + "w")
                + "  " + offer.Currency);
            output.WriteLine(
                "     breaks: " + (offer.PriceBreaks.Count == 0
                    ? "none"
                    : string.Join(", ", offer.PriceBreaks.Select(priceBreak =>
                        priceBreak.Quantity.ToString(CultureInfo.InvariantCulture)
                        + "@"
                        + priceBreak.UnitPrice.ToString("0.#####", CultureInfo.InvariantCulture)))));
            output.WriteLine(
                "     RoHS " + (offer.RohsStatus ?? "-")
                + " / REACH " + (offer.ReachStatus ?? "-")
                + "  COO " + (offer.CountryOfOrigin ?? "-")
                + "  marketplace " + YesNo(offer.IsMarketplace)
                + "  tariff " + YesNo(offer.TariffActive)
                + "  NCNR " + YesNo(offer.Ncnr)
                + "  EOL " + YesNo(offer.EndOfLife)
                + "  fee " + offer.FixedFee.ToString("0.####", CultureInfo.InvariantCulture));
        }

        output.WriteLine($"HTTP exchanges: {exchanges.Count}");
        foreach (var exchange in exchanges)
        {
            output.WriteLine($"  {exchange.Request.Method} {exchange.Request.Url} -> {exchange.Response.Status}");
        }

        output.WriteLine($"Saved: {path}");
    }

    private static string Show(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "-";

    private static string YesNo(bool value) => value ? "yes" : "no";

    /// <summary>Only characters Windows forbids are replaced, so the MPN stays recognizable.</summary>
    private static string SafeFileName(string mpn)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = mpn.Trim().Select(character => invalid.Contains(character) ? '-' : character).ToArray();
        return new string(chars);
    }
}

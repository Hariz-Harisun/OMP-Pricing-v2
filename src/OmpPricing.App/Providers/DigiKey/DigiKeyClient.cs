using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OmpPricing.App.Providers;

namespace OmpPricing.App.Providers.DigiKey;

public enum DigiKeyLookupStatus
{
    Found,
    NotFound,
    DuplicateProducts,
    Throttled,
    Error,
}

public sealed record DigiKeyLookup(
    DigiKeyLookupStatus Status,
    DigiKeyProduct? Product,
    string? Message,
    RateLimitInfo? RateLimit,
    bool UsedManufacturerRetry);

/// <summary>
/// HTTP calls for DigiKey v4: the 2-legged OAuth token with renewal, product details by MPN or
/// DigiKey part number, the duplicate-manufacturer retry with manufacturerId, comma encoding, the
/// required headers and the rate-limit headers (A5).
/// </summary>
public sealed class DigiKeyClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _tokenGate = new(1, 1);

    private string? _token;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public DigiKeyClient(HttpClient http, DigiKeyOptions options, TimeProvider? timeProvider = null)
    {
        _http = http;
        Options = options;
        _time = timeProvider ?? TimeProvider.System;
    }

    public DigiKeyOptions Options { get; }

    public DateTimeOffset TokenExpiresAt => _tokenExpiresAt;

    public async Task<DigiKeyLookup> GetProductDetailsAsync(PartQuery query, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        var number = string.IsNullOrWhiteSpace(query.ProviderPartNumber) ? query.Mpn : query.ProviderPartNumber;

        var first = await SendDetailsAsync(token, number, null, cancellationToken);

        if (first.Status != DigiKeyLookupStatus.DuplicateProducts)
        {
            return first with { UsedManufacturerRetry = false };
        }

        // A5: an MPN that matches several manufacturers returns 404 Duplicate Products found; retry
        // with the manufacturerId the keyword search reports for this MPN and manufacturer.
        var manufacturerId = await FindManufacturerIdAsync(token, number, query.Mfg, cancellationToken);
        if (manufacturerId is null)
        {
            return first with
            {
                Status = DigiKeyLookupStatus.NotFound,
                Message = "Duplicate Products found and no manufacturerId could be resolved",
            };
        }

        var retry = await SendDetailsAsync(token, number, manufacturerId, cancellationToken);
        return retry with { UsedManufacturerRetry = true };
    }

    /// <summary>Resolves the DigiKey manufacturer id used by the duplicate-product retry.</summary>
    public async Task<string?> FindManufacturerIdAsync(
        string token,
        string mpn,
        string? mfg,
        CancellationToken cancellationToken)
    {
        var request = new DigiKeyKeywordRequest { Keywords = mpn, Limit = 10, Offset = 0 };
        var body = JsonSerializer.Serialize(request, JsonOptions);

        using var message = new HttpRequestMessage(HttpMethod.Post, Options.KeywordPath)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        AddHeaders(message, token);

        using var response = await _http.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var search = JsonSerializer.Deserialize<DigiKeySearchResponse>(payload, JsonOptions);
        var products = search?.Products ?? [];
        if (products.Count == 0)
        {
            return null;
        }

        var chosen = string.IsNullOrWhiteSpace(mfg)
            ? products[0]
            : products.FirstOrDefault(product =>
                product.Manufacturer?.Name is not null
                && product.Manufacturer.Name.Contains(mfg.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? products[0];

        return chosen.Manufacturer?.Id?.ToString(CultureInfo.InvariantCulture);
    }

    private async Task<DigiKeyLookup> SendDetailsAsync(
        string token,
        string number,
        string? manufacturerId,
        CancellationToken cancellationToken)
    {
        var path = Options.DetailsPath.Replace("{productNumber}", Uri.EscapeDataString(number), StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(manufacturerId))
        {
            path += "?manufacturerId=" + Uri.EscapeDataString(manufacturerId);
        }

        using var message = new HttpRequestMessage(HttpMethod.Get, path);
        AddHeaders(message, token);

        using var response = await _http.SendAsync(message, cancellationToken);
        var rateLimit = ReadRateLimit(response);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return new DigiKeyLookup(DigiKeyLookupStatus.Throttled, null, "HTTP 429 from DigiKey", rateLimit, false);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var detail = ReadDetail(body);
            if (detail.Contains("Duplicate Products found", StringComparison.OrdinalIgnoreCase))
            {
                return new DigiKeyLookup(DigiKeyLookupStatus.DuplicateProducts, null, detail, rateLimit, false);
            }

            return new DigiKeyLookup(DigiKeyLookupStatus.NotFound, null, detail, rateLimit, false);
        }

        if (!response.IsSuccessStatusCode)
        {
            return new DigiKeyLookup(
                DigiKeyLookupStatus.Error,
                null,
                "HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + " from DigiKey",
                rateLimit,
                false);
        }

        var search = JsonSerializer.Deserialize<DigiKeySearchResponse>(body, JsonOptions);
        var product = search?.Products?.FirstOrDefault();
        return product is null
            ? new DigiKeyLookup(DigiKeyLookupStatus.NotFound, null, "DigiKey returned no products", rateLimit, false)
            : new DigiKeyLookup(DigiKeyLookupStatus.Found, product, null, rateLimit, false);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && _time.GetUtcNow() < _tokenExpiresAt)
        {
            return _token;
        }

        await _tokenGate.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && _time.GetUtcNow() < _tokenExpiresAt)
            {
                return _token;
            }

            // A5: the token endpoint requires form-urlencoded fields; JSON or form-data bodies fail.
            using var message = new HttpRequestMessage(HttpMethod.Post, Options.TokenPath)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = Options.ClientId ?? "",
                    ["client_secret"] = Options.ClientSecret ?? "",
                    ["grant_type"] = "client_credentials",
                }),
            };

            using var response = await _http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new DigiKeyException(
                    "DigiKey token request failed with HTTP "
                    + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
            }

            var token = JsonSerializer.Deserialize<DigiKeyTokenResponse>(body, JsonOptions);
            if (string.IsNullOrWhiteSpace(token?.AccessToken))
            {
                throw new DigiKeyException("DigiKey token response contained no access_token");
            }

            _token = token.AccessToken;

            // A5: the token lives about 600 seconds; renew a minute early.
            var lifetime = token.ExpiresIn > 0 ? token.ExpiresIn : 600;
            _tokenExpiresAt = _time.GetUtcNow().AddSeconds(Math.Max(30, lifetime - 60));
            return _token;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private void AddHeaders(HttpRequestMessage message, string token)
    {
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        message.Headers.TryAddWithoutValidation("X-DIGIKEY-Client-Id", Options.ClientId);
        message.Headers.TryAddWithoutValidation("X-DIGIKEY-Account-Id", Options.AccountId);
        message.Headers.TryAddWithoutValidation("X-DIGIKEY-Locale-Site", Options.Site);
        message.Headers.TryAddWithoutValidation("X-DIGIKEY-Locale-Language", Options.Language);
        message.Headers.TryAddWithoutValidation("X-DIGIKEY-Locale-Currency", Options.Currency);
    }

    private static RateLimitInfo ReadRateLimit(HttpResponseMessage response)
    {
        return new RateLimitInfo(ReadInt(response, "X-RateLimit-Limit"), ReadInt(response, "X-RateLimit-Remaining"));
    }

    private static int? ReadInt(HttpResponseMessage response, string header)
    {
        return response.Headers.TryGetValues(header, out var values)
            && int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static string ReadDetail(string body)
    {
        try
        {
            var error = JsonSerializer.Deserialize<DigiKeyErrorResponse>(body, JsonOptions);
            return error?.Detail ?? error?.Title ?? "";
        }
        catch (JsonException)
        {
            return "";
        }
    }
}

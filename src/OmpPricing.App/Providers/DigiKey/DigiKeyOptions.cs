using OmpPricing.App.Providers;

namespace OmpPricing.App.Providers.DigiKey;

/// <summary>
/// Settings for the DigiKey v4 API, bound from Providers:DigiKey. Credentials are never in
/// appsettings.json: they come from user-secrets or OMP_Providers__DigiKey__* environment variables.
/// </summary>
public sealed class DigiKeyOptions
{
    public bool Enabled { get; set; }

    /// <summary>DigiKey is an authorised distributor; marketplace offers are still flagged separately.</summary>
    public bool IsAuthorised { get; set; } = true;

    public string BaseUrl { get; set; } = "https://api.digikey.com";

    public string TokenPath { get; set; } = "/v1/oauth2/token";

    /// <summary>{productNumber} is replaced with the URL-encoded MPN or DigiKey part number.</summary>
    public string DetailsPath { get; set; } = "/products/v4/search/{productNumber}/productdetails";

    public string KeywordPath { get; set; } = "/products/v4/search/keyword";

    public string Site { get; set; } = "US";

    public string Language { get; set; } = "en";

    public string Currency { get; set; } = "USD";

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    /// <summary>Required for 2-legged OAuth (A5).</summary>
    public string? AccountId { get; set; }

    public ProviderRateLimits RateLimits { get; set; } = new() { PerMinute = 120, PerDay = 1000 };

    /// <summary>Fails fast at startup, naming the missing settings but never printing their values.</summary>
    public void Validate()
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            missing.Add("ClientId");
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            missing.Add("ClientSecret");
        }

        if (string.IsNullOrWhiteSpace(AccountId))
        {
            missing.Add("AccountId");
        }

        if (missing.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "DigiKey is enabled but these settings are missing: " + string.Join(", ", missing)
            + ". Put provider credentials in user-secrets or in OMP_Providers__DigiKey__* environment variables; "
            + "never in appsettings.json.");
    }
}

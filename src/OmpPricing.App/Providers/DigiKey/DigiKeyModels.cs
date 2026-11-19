using System.Text.Json.Serialization;

namespace OmpPricing.App.Providers.DigiKey;

// Response types for DigiKey v4. Field names follow the response shape recorded in A5; anything not
// confirmed there is marked UNVERIFIED and is only mapped when the shape is known.

public sealed class DigiKeyTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }
}

public sealed class DigiKeySearchResponse
{
    [JsonPropertyName("Products")]
    public List<DigiKeyProduct>? Products { get; set; }

    [JsonPropertyName("ExactMatches")]
    public List<DigiKeyProduct>? ExactMatches { get; set; }

    [JsonPropertyName("SearchLocaleUsed")]
    public DigiKeyLocale? SearchLocaleUsed { get; set; }
}

public sealed class DigiKeyLocale
{
    [JsonPropertyName("Site")]
    public string? Site { get; set; }

    [JsonPropertyName("Language")]
    public string? Language { get; set; }

    [JsonPropertyName("Currency")]
    public string? Currency { get; set; }
}

public sealed class DigiKeyProduct
{
    [JsonPropertyName("ManufacturerProductNumber")]
    public string? ManufacturerProductNumber { get; set; }

    [JsonPropertyName("Manufacturer")]
    public DigiKeyManufacturer? Manufacturer { get; set; }

    [JsonPropertyName("ProductVariations")]
    public List<DigiKeyVariation>? ProductVariations { get; set; }

    [JsonPropertyName("Classifications")]
    public DigiKeyClassifications? Classifications { get; set; }

    /// <summary>A string in the API, for example "24" or "24 Weeks".</summary>
    [JsonPropertyName("ManufacturerLeadWeeks")]
    public string? ManufacturerLeadWeeks { get; set; }

    [JsonPropertyName("Ncnr")]
    public bool Ncnr { get; set; }

    [JsonPropertyName("Discontinued")]
    public bool Discontinued { get; set; }

    [JsonPropertyName("EndOfLife")]
    public bool EndOfLife { get; set; }

    [JsonPropertyName("ProductStatus")]
    public DigiKeyProductStatus? ProductStatus { get; set; }

    [JsonPropertyName("QuantityAvailable")]
    public int? QuantityAvailable { get; set; }
}

public sealed class DigiKeyManufacturer
{
    [JsonPropertyName("Id")]
    public int? Id { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }
}

public sealed class DigiKeyProductStatus
{
    [JsonPropertyName("Status")]
    public string? Status { get; set; }
}

public sealed class DigiKeyClassifications
{
    [JsonPropertyName("RohsStatus")]
    public string? RohsStatus { get; set; }

    [JsonPropertyName("ReachStatus")]
    public string? ReachStatus { get; set; }
}

public sealed class DigiKeyVariation
{
    [JsonPropertyName("DigiKeyProductNumber")]
    public string? DigiKeyProductNumber { get; set; }

    [JsonPropertyName("PackageType")]
    public DigiKeyPackageType? PackageType { get; set; }

    [JsonPropertyName("StandardPricing")]
    public List<DigiKeyPriceBreak>? StandardPricing { get; set; }

    /// <summary>Customer/contract pricing. Never used: OMP quotes the public list price (A5).</summary>
    [JsonPropertyName("MyPricing")]
    public List<DigiKeyPriceBreak>? MyPricing { get; set; }

    [JsonPropertyName("MarketPlace")]
    public bool MarketPlace { get; set; }

    [JsonPropertyName("TariffActive")]
    public bool TariffActive { get; set; }

    [JsonPropertyName("QuantityAvailableforPackageType")]
    public int? QuantityAvailableforPackageType { get; set; }

    [JsonPropertyName("MaxQuantityForDistribution")]
    public int? MaxQuantityForDistribution { get; set; }

    [JsonPropertyName("MinimumOrderQuantity")]
    public int? MinimumOrderQuantity { get; set; }

    [JsonPropertyName("StandardPackage")]
    public int? StandardPackage { get; set; }

    [JsonPropertyName("DigiReelFee")]
    public decimal? DigiReelFee { get; set; }
}

public sealed class DigiKeyPackageType
{
    [JsonPropertyName("Id")]
    public int? Id { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }
}

public sealed class DigiKeyPriceBreak
{
    [JsonPropertyName("BreakQuantity")]
    public int? BreakQuantity { get; set; }

    [JsonPropertyName("UnitPrice")]
    public decimal? UnitPrice { get; set; }

    [JsonPropertyName("TotalPrice")]
    public decimal? TotalPrice { get; set; }
}

/// <summary>Error bodies carry a "detail" text, for example "Duplicate Products found".</summary>
public sealed class DigiKeyErrorResponse
{
    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

public sealed class DigiKeyKeywordRequest
{
    [JsonPropertyName("Keywords")]
    public string Keywords { get; set; } = "";

    [JsonPropertyName("Limit")]
    public int Limit { get; set; } = 10;

    [JsonPropertyName("Offset")]
    public int Offset { get; set; }
}

/// <summary>Raised when the token endpoint refuses us; the provider turns it into an Error result.</summary>
public sealed class DigiKeyException : Exception
{
    public DigiKeyException(string message) : base(message)
    {
    }
}

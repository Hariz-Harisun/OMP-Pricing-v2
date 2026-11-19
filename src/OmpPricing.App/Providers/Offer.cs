namespace OmpPricing.App.Providers;

/// <summary>A quantity break: unit price for orders of at least <paramref name="Quantity"/>.</summary>
public sealed record PriceBreak(int Quantity, decimal UnitPrice);

/// <summary>
/// The normalized offer every provider maps into (AGENTS.md "Providers"). The pricing, matcher,
/// selector and Excel code only ever see this type, never a distributor-specific response type.
/// </summary>
public sealed class Offer
{
    public required string Provider { get; init; }

    public string? ProviderPartNumber { get; init; }

    public string Mfg { get; init; } = "";

    public string Mpn { get; init; } = "";

    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; init; } = "USD";

    public IReadOnlyList<PriceBreak> PriceBreaks { get; init; } = [];

    public int? Moq { get; init; }

    public int? StandardPackage { get; init; }

    public string? Packaging { get; init; }

    public int? Stock { get; init; }

    public int? LeadWeeks { get; init; }

    public bool IsMarketplace { get; init; }

    public decimal FixedFee { get; init; }

    public string? RohsStatus { get; init; }

    public string? ReachStatus { get; init; }

    /// <summary>Country of origin as the provider reports it (ISO code or English name).</summary>
    public string? CountryOfOrigin { get; init; }

    public bool TariffActive { get; init; }

    public bool Ncnr { get; init; }

    public bool Discontinued { get; init; }

    public bool EndOfLife { get; init; }

    /// <summary>True when the provider returned contract/customer pricing, which is never used (OMP = list price only).</summary>
    public bool IsContractPrice { get; init; }

    /// <summary>True when configuration flags this provider as an authorised distributor.</summary>
    public bool IsAuthorisedProvider { get; init; }
}

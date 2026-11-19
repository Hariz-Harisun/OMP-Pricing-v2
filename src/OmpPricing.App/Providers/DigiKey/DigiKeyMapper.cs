using System.Globalization;
using OmpPricing.App.Providers;

namespace OmpPricing.App.Providers.DigiKey;

/// <summary>
/// Maps a DigiKey product to the normalized <see cref="Offer"/>: one offer per packaging variation.
/// Only StandardPricing is mapped; MyPricing (contract pricing) is never used as a list price.
/// </summary>
public static class DigiKeyMapper
{
    public static IReadOnlyList<Offer> ToOffers(
        DigiKeyProduct product,
        DigiKeyOptions options,
        bool isAuthorisedProvider)
    {
        var offers = new List<Offer>();

        foreach (var variation in product.ProductVariations ?? [])
        {
            var breaks = (variation.StandardPricing ?? [])
                .Where(priceBreak => priceBreak.BreakQuantity is > 0 && priceBreak.UnitPrice is > 0m)
                .Select(priceBreak => new PriceBreak(priceBreak.BreakQuantity!.Value, priceBreak.UnitPrice!.Value))
                .OrderBy(priceBreak => priceBreak.Quantity)
                .ToList();

            if (breaks.Count == 0)
            {
                continue;
            }

            offers.Add(new Offer
            {
                Provider = DigiKeyProvider.ProviderName,
                ProviderPartNumber = variation.DigiKeyProductNumber,
                Mfg = product.Manufacturer?.Name ?? "",
                Mpn = product.ManufacturerProductNumber ?? "",
                Currency = options.Currency,
                PriceBreaks = breaks,
                Moq = variation.MinimumOrderQuantity,
                StandardPackage = variation.StandardPackage,
                Packaging = variation.PackageType?.Name,
                Stock = variation.QuantityAvailableforPackageType ?? product.QuantityAvailable,
                LeadWeeks = ParseLeadWeeks(product.ManufacturerLeadWeeks),
                IsMarketplace = variation.MarketPlace,
                FixedFee = variation.DigiReelFee ?? 0m,
                RohsStatus = product.Classifications?.RohsStatus,
                ReachStatus = product.Classifications?.ReachStatus,
                // UNVERIFIED: A5's response shape lists no country-of-origin field, so it stays empty
                // and the rule table decides column 21 (Step 3).
                CountryOfOrigin = null,
                TariffActive = variation.TariffActive,
                Ncnr = product.Ncnr,
                Discontinued = product.Discontinued,
                EndOfLife = product.EndOfLife,
                IsContractPrice = false,
                IsAuthorisedProvider = isAuthorisedProvider,
            });
        }

        return offers;
    }

    /// <summary>"24", "24 Weeks" and "24.0" all mean 24 whole weeks; anything unparseable stays empty.</summary>
    public static int? ParseLeadWeeks(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var start = raw.IndexOfAny("0123456789".ToCharArray());
        if (start < 0)
        {
            return null;
        }

        var end = start;
        while (end < raw.Length && char.IsDigit(raw[end]))
        {
            end++;
        }

        return int.TryParse(raw[start..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out var weeks)
            ? weeks
            : null;
    }
}

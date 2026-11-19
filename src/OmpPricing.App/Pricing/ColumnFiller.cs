using OmpPricing.App.Excel;
using OmpPricing.App.Providers;
using OmpPricing.App.Rules;

namespace OmpPricing.App.Pricing;

/// <summary>
/// Fills the compulsory columns 7 to 21 for the chosen offer (A6 rule 6) and records which
/// compulsory values are still missing.
/// </summary>
public static class ColumnFiller
{
    public static void Fill(
        QuoteLine line,
        ExcelOptions excel,
        PackagingMap packaging,
        CountryList countries)
    {
        var offer = line.SelectedOffer;
        if (offer is null)
        {
            return;
        }

        var columns = excel.Columns;
        var unitPrice = offer.PriceBreaks.Count > 0 ? offer.PriceBreaks[0].UnitPrice : (decimal?)null;

        line.Filled[columns.Supplier] = offer.Provider;
        line.Filled[columns.SupplierMfg] = offer.Mfg;
        line.Filled[columns.SupplierMpn] = offer.Mpn;
        line.Filled[columns.Currency] = offer.Currency;
        line.Filled[columns.UnitPrice] = unitPrice;
        line.Filled[columns.Spq] = offer.StandardPackage;
        line.Filled[columns.Moq] = offer.Moq;
        line.Filled[columns.LeadWeeks] = offer.LeadWeeks;

        // Columns 15, 16 and 17 come from rules/supplier-terms.json, which is empty until purchasing
        // fills it. Step 3 loads that table; in Step 1 they stay missing and the line is Incomplete.
        line.Filled[columns.CancellationWeeks] = null;
        line.Filled[columns.RescheduleWeeks] = null;
        line.Filled[columns.CxlResBasis] = null;

        line.Filled[columns.Packaging] = packaging.Map(offer.Packaging);
        line.Filled[columns.Authorised] = offer.IsAuthorisedProvider && !offer.IsMarketplace ? "Yes" : "No";
        line.Filled[columns.Compliance] = Compliance(offer.RohsStatus, offer.ReachStatus);

        // COO must be spelled exactly as on Sheet1 (A9.6); unknown values stay missing.
        line.Filled[columns.Coo] = countries.Canonical(offer.CountryOfOrigin);

        line.MissingColumns.Clear();
        foreach (var position in excel.CompulsoryColumns())
        {
            line.Filled.TryGetValue(position, out var value);
            if (!CompulsoryColumns.IsValid(position, value, columns, countries))
            {
                line.MissingColumns.Add(position);
            }
        }

        line.Remarks = BuildRemarks(line, excel, offer);
        line.Filled[columns.Remarks] = line.Remarks;
    }

    /// <summary>
    /// "Yes" only when both statuses are compliant, "No" when the provider states non-compliance,
    /// missing (null) when unknown (A6 rule 6). The compliance map becomes configuration in Step 3.
    /// </summary>
    private static string? Compliance(string? rohs, string? reach)
    {
        if (string.IsNullOrWhiteSpace(rohs) && string.IsNullOrWhiteSpace(reach))
        {
            return null;
        }

        var rohsText = (rohs ?? "").Trim();
        var reachText = (reach ?? "").Trim();

        if (rohsText.Contains("non-compliant", StringComparison.OrdinalIgnoreCase)
            || reachText.Contains("non-compliant", StringComparison.OrdinalIgnoreCase))
        {
            return "No";
        }

        var rohsCompliant = rohsText.Equals("ROHS3 Compliant", StringComparison.OrdinalIgnoreCase)
            || rohsText.Equals("RoHS Compliant", StringComparison.OrdinalIgnoreCase);

        var reachCompliant = reachText.Equals("REACH Unaffected", StringComparison.OrdinalIgnoreCase)
            || reachText.Equals("REACH Compliant", StringComparison.OrdinalIgnoreCase);

        return rohsCompliant && reachCompliant ? "Yes" : null;
    }

    /// <summary>Remarks holds exceptions only, never routine data (A6 rule 7).</summary>
    private static string BuildRemarks(QuoteLine line, ExcelOptions excel, Offer offer)
    {
        var notes = new List<string>();

        if (line.IsWildcard)
        {
            notes.Add("Wildcard MPN " + line.RawMpn.Trim() + " matched on prefix " + line.WildcardPrefix);
        }

        if (line.Alternates.Count > 0)
        {
            notes.Add("Alternates: " + string.Join("; ", line.Alternates));
        }

        if (offer.Moq is int moq && line.Qty is int qty && moq > qty)
        {
            notes.Add("MOQ " + moq.ToString() + " is above the requested Qty " + qty.ToString());
        }

        if (offer.TariffActive)
        {
            notes.Add("Tariff active");
        }

        if (offer.Ncnr)
        {
            notes.Add("NCNR");
        }

        if (offer.Discontinued)
        {
            notes.Add("Discontinued");
        }

        if (offer.EndOfLife)
        {
            notes.Add("End of life");
        }

        if (line.MissingColumns.Count > 0)
        {
            notes.Add("Missing: " + string.Join(", ", line.MissingColumns.Select(excel.Columns.NameOf)));
        }

        return notes.Count == 0 ? "" : string.Join("; ", notes);
    }
}

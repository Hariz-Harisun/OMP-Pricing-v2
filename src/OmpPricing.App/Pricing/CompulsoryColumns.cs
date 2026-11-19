using OmpPricing.App.Excel;
using OmpPricing.App.Rules;

namespace OmpPricing.App.Pricing;

/// <summary>
/// Validates the compulsory columns 7 to 21 in the A4 formats (A6 rules 6 and 9). A missing,
/// placeholder or wrongly formatted value counts as missing, which makes the line Incomplete.
/// </summary>
public static class CompulsoryColumns
{
    public static bool IsValid(int position, object? value, ColumnPositions columns, CountryList countries)
    {
        if (position == columns.Currency)
        {
            return value is string currency && currency.Length == 3 && currency.All(char.IsAsciiLetter);
        }

        if (position == columns.UnitPrice)
        {
            return value is decimal price && price > 0m;
        }

        if (position == columns.Spq
            || position == columns.Moq
            || position == columns.LeadWeeks
            || position == columns.CancellationWeeks
            || position == columns.RescheduleWeeks)
        {
            return value is int number && number >= 0;
        }

        if (position == columns.CxlResBasis)
        {
            return value is string basis && (basis == "DB" || basis == "PB");
        }

        if (position == columns.Authorised || position == columns.Compliance)
        {
            return value is string answer && (answer == "Yes" || answer == "No");
        }

        if (position == columns.Coo)
        {
            return value is string country && countries.Contains(country);
        }

        return value is string text && text.Trim().Length > 0;
    }
}

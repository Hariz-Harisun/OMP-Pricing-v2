using OmpPricing.App.Excel;
using OmpPricing.App.Pricing;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Pricing;

/// <summary>Columns 7 to 21 are compulsory and must hold a valid value in the A4 format (A6 rule 9).</summary>
public sealed class CompulsoryColumnsTests
{
    private static readonly ColumnPositions Columns = new();

    [Fact]
    public void Yes_no_columns_accept_only_the_exact_words()
    {
        var countries = Sample.Countries;

        Assert.True(CompulsoryColumns.IsValid(Columns.Authorised, "Yes", Columns, countries));
        Assert.True(CompulsoryColumns.IsValid(Columns.Authorised, "No", Columns, countries));
        Assert.False(CompulsoryColumns.IsValid(Columns.Authorised, "YES", Columns, countries));
        Assert.False(CompulsoryColumns.IsValid(Columns.Compliance, "yes", Columns, countries));
        Assert.False(CompulsoryColumns.IsValid(Columns.Compliance, "Yes ", Columns, countries));
    }

    [Fact]
    public void Cxl_res_basis_accepts_only_db_or_pb()
    {
        var countries = Sample.Countries;

        Assert.True(CompulsoryColumns.IsValid(Columns.CxlResBasis, "DB", Columns, countries));
        Assert.True(CompulsoryColumns.IsValid(Columns.CxlResBasis, "PB", Columns, countries));
        Assert.False(CompulsoryColumns.IsValid(Columns.CxlResBasis, "PB ", Columns, countries));
        Assert.False(CompulsoryColumns.IsValid(Columns.CxlResBasis, "PO", Columns, countries));
    }

    [Fact]
    public void Country_must_be_spelled_as_on_sheet1()
    {
        var countries = Sample.Countries;

        Assert.True(CompulsoryColumns.IsValid(Columns.Coo, "Taiwan", Columns, countries));
        Assert.True(CompulsoryColumns.IsValid(Columns.Coo, "Korea, Republic of", Columns, countries));
        Assert.False(CompulsoryColumns.IsValid(Columns.Coo, "Formosa", Columns, countries));
        Assert.False(CompulsoryColumns.IsValid(Columns.Coo, "", Columns, countries));
    }

    [Fact]
    public void Money_and_counts_must_be_numbers()
    {
        var countries = Sample.Countries;

        Assert.True(CompulsoryColumns.IsValid(Columns.UnitPrice, 0.00052m, Columns, countries));
        Assert.False(CompulsoryColumns.IsValid(Columns.UnitPrice, 0m, Columns, countries));
        Assert.False(CompulsoryColumns.IsValid(Columns.UnitPrice, "0.00052", Columns, countries));

        Assert.True(CompulsoryColumns.IsValid(Columns.Moq, 10000, Columns, countries));
        Assert.False(CompulsoryColumns.IsValid(Columns.Moq, null, Columns, countries));
        Assert.False(CompulsoryColumns.IsValid(Columns.LeadWeeks, "22", Columns, countries));
    }

    [Fact]
    public void Currency_must_be_a_three_letter_code()
    {
        var countries = Sample.Countries;

        Assert.True(CompulsoryColumns.IsValid(Columns.Currency, "USD", Columns, countries));
        Assert.False(CompulsoryColumns.IsValid(Columns.Currency, "US", Columns, countries));
        Assert.False(CompulsoryColumns.IsValid(Columns.Currency, "US1", Columns, countries));
    }

    [Fact]
    public void Supplier_mfg_and_mpn_must_not_be_blank()
    {
        var countries = Sample.Countries;

        Assert.False(CompulsoryColumns.IsValid(Columns.SupplierMfg, "  ", Columns, countries));
        Assert.True(CompulsoryColumns.IsValid(Columns.SupplierMpn, "BAT54CW,115", Columns, countries));
        Assert.Equal(15, new ExcelOptions().CompulsoryColumns().Count());
    }
}

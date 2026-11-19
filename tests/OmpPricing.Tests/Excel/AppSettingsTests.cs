using Microsoft.Extensions.Configuration;
using OmpPricing.App.Excel;

namespace OmpPricing.Tests.Excel;

/// <summary>The column positions the app uses come from appsettings.json (A4).</summary>
public sealed class AppSettingsTests
{
    [Fact]
    public void Appsettings_defines_the_sheet_columns_and_folders()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var excel = configuration.GetSection("Omp:Excel").Get<ExcelOptions>();

        Assert.NotNull(excel);
        Assert.Equal("LT Quote", excel!.SheetName);
        Assert.Equal("Offers", excel.OffersSheetName);
        Assert.Equal(2, excel.FirstDataRow);
        Assert.Equal("Sheet1", excel.CountrySheetName);
        Assert.Equal(2, excel.CountryFirstRow);

        var defaults = new ExcelOptions().Columns;
        var columns = excel.Columns;
        Assert.Equal(defaults.Pn, columns.Pn);
        Assert.Equal(1, columns.Pn);
        Assert.Equal(2, columns.Description);
        Assert.Equal(3, columns.Mfg);
        Assert.Equal(4, columns.Mpn);
        Assert.Equal(5, columns.Qty);
        Assert.Equal(6, columns.Eau);
        Assert.Equal(7, columns.Supplier);
        Assert.Equal(11, columns.UnitPrice);
        Assert.Equal(12, columns.Spq);
        Assert.Equal(13, columns.Moq);
        Assert.Equal(14, columns.LeadWeeks);
        Assert.Equal(15, columns.CancellationWeeks);
        Assert.Equal(16, columns.RescheduleWeeks);
        Assert.Equal(17, columns.CxlResBasis);
        Assert.Equal(18, columns.Packaging);
        Assert.Equal(19, columns.Authorised);
        Assert.Equal(20, columns.Compliance);
        Assert.Equal(21, columns.Coo);
        Assert.Equal(22, columns.Remarks);

        Assert.Equal(defaults.SupplierMfg, columns.SupplierMfg);
        Assert.Equal(defaults.SupplierMpn, columns.SupplierMpn);
        Assert.Equal(defaults.Currency, columns.Currency);

        Assert.Equal("output", configuration["Omp:OutputFolder"]);
        Assert.Equal("rules", configuration["Omp:RulesFolder"]);
    }

    [Fact]
    public void Packaging_map_turns_provider_names_into_the_short_codes_of_column_18()
    {
        var map = new OmpPricing.App.Pricing.PackagingMap(new Dictionary<string, string>
        {
            ["Tape & Reel (TR)"] = "T&R",
            ["Cut Tape (CT)"] = "CT",
        });

        Assert.Equal("T&R", map.Map("Tape & Reel (TR)"));
        Assert.Equal("CT", map.Map("Cut Tape (CT)"));
        Assert.Equal("Tray", map.Map("Tray"));
        Assert.Null(map.Map(null));
    }
}

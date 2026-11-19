namespace OmpPricing.App.Excel;

/// <summary>
/// Excel layout settings. Columns are addressed by 1-based position, never by header text (A4, A9.8).
/// Bound from the "Omp:Excel" configuration section.
/// </summary>
public sealed class ExcelOptions
{
    public string SheetName { get; set; } = "LT Quote";

    public string OffersSheetName { get; set; } = "Offers";

    public int FirstDataRow { get; set; } = 2;

    public string CountrySheetName { get; set; } = "Sheet1";

    public int CountryFirstRow { get; set; } = 2;

    public ColumnPositions Columns { get; set; } = new();

    public IEnumerable<int> CompulsoryColumns()
    {
        for (var position = Columns.Supplier; position <= Columns.Coo; position++)
        {
            yield return position;
        }
    }
}

/// <summary>The 22 columns of the client template, 1-based (A4).</summary>
public sealed class ColumnPositions
{
    public int Pn { get; set; } = 1;
    public int Description { get; set; } = 2;
    public int Mfg { get; set; } = 3;
    public int Mpn { get; set; } = 4;
    public int Qty { get; set; } = 5;
    public int Eau { get; set; } = 6;
    public int Supplier { get; set; } = 7;
    public int SupplierMfg { get; set; } = 8;
    public int SupplierMpn { get; set; } = 9;
    public int Currency { get; set; } = 10;
    public int UnitPrice { get; set; } = 11;
    public int Spq { get; set; } = 12;
    public int Moq { get; set; } = 13;
    public int LeadWeeks { get; set; } = 14;
    public int CancellationWeeks { get; set; } = 15;
    public int RescheduleWeeks { get; set; } = 16;
    public int CxlResBasis { get; set; } = 17;
    public int Packaging { get; set; } = 18;
    public int Authorised { get; set; } = 19;
    public int Compliance { get; set; } = 20;
    public int Coo { get; set; } = 21;
    public int Remarks { get; set; } = 22;

    /// <summary>Header text used in Remarks and reports for a position (A4).</summary>
    public string NameOf(int position) => position switch
    {
        7 => "Supplier",
        8 => "MFG",
        9 => "MPN",
        10 => "Currency",
        11 => "Unit Price",
        12 => "SPQ",
        13 => "MOQ",
        14 => "Lead time (WKS)",
        15 => "Cancellation window (WKS)",
        16 => "Reschedule window (WKS)",
        17 => "CXL & RES window",
        18 => "Packaging",
        19 => "Authorised Distributor",
        20 => "RoHS/REACH",
        21 => "COO",
        22 => "Remarks",
        _ => "Column " + position.ToString(),
    };
}

using OmpPricing.App.Providers;

namespace OmpPricing.App.Pricing;

/// <summary>
/// One data row of the client workbook. Identity is the Excel row number, never PN# (A9.1).
/// </summary>
public sealed class QuoteLine
{
    public required int RowNumber { get; init; }

    public string Pn { get; init; } = "";

    public string Description { get; init; } = "";

    /// <summary>Manufacturer exactly as the sender wrote it.</summary>
    public string RawMfg { get; init; } = "";

    /// <summary>The whole MPN cell exactly as the sender wrote it (may hold several MPNs).</summary>
    public string RawMpn { get; init; } = "";

    /// <summary>Normalized manufacturer (trimmed, whitespace collapsed).</summary>
    public string Mfg { get; set; } = "";

    /// <summary>Normalized MPN: first MPN of the cell, trimmed, uppercased, wildcard suffix removed.</summary>
    public string Mpn { get; set; } = "";

    public bool IsGeneric { get; set; }

    public bool IsWildcard { get; set; }

    /// <summary>Fixed part of a trailing wildcard MPN, for example BLM15GG221SZ1 of BLM15GG221SZ1x.</summary>
    public string? WildcardPrefix { get; set; }

    /// <summary>Further MPNs found in the same cell, listed in Remarks (A9.4).</summary>
    public IReadOnlyList<string> Alternates { get; set; } = [];

    public int? Qty { get; set; }

    public int? Eau { get; set; }

    public LineStatus Status { get; set; } = LineStatus.Queued;

    public string? Remarks { get; set; }

    public List<Offer> Offers { get; } = [];

    public Offer? SelectedOffer { get; set; }

    /// <summary>Values written into columns 7 to 22, keyed by 1-based position.</summary>
    public Dictionary<int, object?> Filled { get; } = new();

    /// <summary>Compulsory positions left without a valid value.</summary>
    public List<int> MissingColumns { get; } = new();
}

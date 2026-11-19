using System.Globalization;
using System.Text;

namespace OmpPricing.App.Pricing;

/// <summary>Result of applying the input rules (A6 rule 1, A9.2 to A9.4) to one MPN cell.</summary>
public sealed record NormalizedInput(
    string Mfg,
    string Mpn,
    bool IsGeneric,
    bool IsWildcard,
    string? WildcardPrefix,
    IReadOnlyList<string> Alternates,
    int? Qty,
    int? Eau,
    LineStatus Status,
    string? Remarks);

/// <summary>
/// Input normalization: trim and collapse whitespace, split multi-MPN cells, detect the lowercase
/// x wildcard BEFORE uppercasing, and flag generic parametric rows. Makes no provider call.
/// </summary>
public static class InputNormalizer
{
    private const string GenericPartRemark = "Generic part: no real MPN";

    private static readonly char[] MultiMpnSeparators = ['/'];

    private static readonly string[] Notes = ["(at)", "(alt)", "(alternative)"];

    private static readonly string[] GenericManufacturers = ["common", "undefined", "undefned"];

    public static NormalizedInput Normalize(string? mfgRaw, string? mpnRaw, string? qtyRaw, string? eauRaw)
    {
        var mfg = Collapse(mfgRaw);
        var cell = Collapse((mpnRaw ?? "").Replace((char)13, '/').Replace((char)10, '/'));

        var segments = cell
            .Split(MultiMpnSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => StripNote(segment))
            .Where(segment => segment.Length > 0)
            .ToList();

        var primary = segments.Count > 0 ? segments[0] : "";
        var alternates = segments.Skip(1).ToList();

        var isGeneric = IsGenericPart(mfg, primary);

        var isWildcard = false;
        var wildcardInside = false;
        string? prefix = null;

        if (primary.Length > 0)
        {
            var lastCharacter = primary[^1];
            if (lastCharacter == 'x')
            {
                var start = primary.Length;
                while (start > 0 && primary[start - 1] == 'x')
                {
                    start--;
                }

                prefix = primary[..start];
                isWildcard = prefix.Length > 0;
            }
            else if (primary.Contains('x'))
            {
                wildcardInside = true;
            }
        }

        var qty = ParsePositiveInteger(qtyRaw);
        var eau = ParsePositiveInteger(eauRaw);

        var mpn = (isWildcard && prefix is not null ? prefix : primary).ToUpperInvariant();

        LineStatus status;
        string? remarks;

        if (isGeneric)
        {
            status = LineStatus.NeedsReview;
            remarks = GenericPartRemark;
        }
        else if (qty is null)
        {
            status = LineStatus.InvalidInput;
            remarks = "Invalid Qty: '" + (qtyRaw ?? "") + "' is not a positive whole number";
        }
        else if (wildcardInside)
        {
            status = LineStatus.NeedsReview;
            remarks = "Wildcard inside MPN: " + primary;
        }
        else if (primary.Length == 0)
        {
            status = LineStatus.InvalidInput;
            remarks = "Missing MPN";
        }
        else
        {
            status = LineStatus.Queued;
            remarks = null;
        }

        return new NormalizedInput(mfg, mpn, isGeneric, isWildcard, prefix, alternates, qty, eau, status, remarks);
    }

    /// <summary>A generic parametric row: spec string or a placeholder manufacturer (A9.2).</summary>
    public static bool IsGenericPart(string? mfg, string? mpn)
    {
        var mpnText = (mpn ?? "").Trim();
        if (mpnText.StartsWith("SMT;", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var mfgText = (mfg ?? "").Trim();
        return GenericManufacturers.Contains(mfgText, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Trim and collapse runs of whitespace to a single space.</summary>
    public static string Collapse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;

        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasSpace)
                {
                    builder.Append(' ');
                }

                previousWasSpace = true;
                continue;
            }

            builder.Append(character);
            previousWasSpace = false;
        }

        return builder.ToString().Trim();
    }

    private static string StripNote(string value)
    {
        var trimmed = value.Trim();
        foreach (var note in Notes)
        {
            if (trimmed.EndsWith(note, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[..^note.Length].TrimEnd();
            }
        }

        return trimmed;
    }

    private static int? ParsePositiveInteger(string? raw)
    {
        var text = Collapse(raw);
        if (text.Length == 0)
        {
            return null;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        if (number <= 0 || number != decimal.Truncate(number) || number > int.MaxValue)
        {
            return null;
        }

        return (int)number;
    }
}

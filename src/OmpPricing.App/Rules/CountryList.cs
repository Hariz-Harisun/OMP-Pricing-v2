using System.Text;

namespace OmpPricing.App.Rules;

/// <summary>
/// The allowed country-of-origin names, exactly as spelled on Sheet1 of the client workbook (A9.6).
/// A COO that is not in this list counts as missing.
/// </summary>
public sealed class CountryList
{
    private readonly List<string> _names;
    private readonly Dictionary<string, string> _byUpperName;

    private CountryList(List<string> names)
    {
        _names = names;
        _byUpperName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            _byUpperName.TryAdd(name.ToUpperInvariant(), name);
        }
    }

    public IReadOnlyList<string> Names => _names;

    public int Count => _names.Count;

    public static CountryList Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Country list not found: " + path + ". Run --export-countries --file <path.xlsx> to create it.",
                path);
        }

        return FromNames(File.ReadAllLines(path, Encoding.UTF8));
    }

    public static CountryList FromNames(IEnumerable<string> names)
    {
        var cleaned = names
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .ToList();

        return new CountryList(cleaned);
    }

    public bool Contains(string? country) => Canonical(country) is not null;

    /// <summary>Returns the Sheet1 spelling of the country, or null when it is not in the list.</summary>
    public string? Canonical(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return null;
        }

        return _byUpperName.TryGetValue(country.Trim().ToUpperInvariant(), out var canonical) ? canonical : null;
    }

    public static void Save(IEnumerable<string> names, string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllLines(path, names, new UTF8Encoding(false));
    }
}

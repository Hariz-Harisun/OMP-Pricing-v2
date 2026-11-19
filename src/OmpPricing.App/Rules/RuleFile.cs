using System.Text.Json;

namespace OmpPricing.App.Rules;

/// <summary>Shared JSON loading for the small rule tables in rules/ (clear errors, no silent defaults).</summary>
internal static class RuleFile
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static List<T> Load<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Rule file not found: " + path, path);
        }

        var json = File.ReadAllText(path);

        try
        {
            var items = JsonSerializer.Deserialize<List<T>>(json, JsonOptions);
            return items ?? throw new InvalidOperationException("Rule file must be a JSON array: " + path);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Rule file " + path + " is not valid JSON: " + exception.Message, exception);
        }
    }
}

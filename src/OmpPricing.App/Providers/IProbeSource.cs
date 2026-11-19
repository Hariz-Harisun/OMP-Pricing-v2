using System.Text.RegularExpressions;

namespace OmpPricing.App.Providers;

/// <summary>One HTTP request as it was sent, with every credential already removed.</summary>
public sealed record ProbeRequest(
    string Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);

/// <summary>One HTTP response as it arrived. Bodies and headers such as X-RateLimit-* are kept.</summary>
public sealed record ProbeResponse(
    int Status,
    IReadOnlyDictionary<string, string> Headers,
    string Body);

public sealed record ProbeExchange(ProbeRequest Request, ProbeResponse Response);

/// <summary>
/// A provider that can hand back the raw exchanges of its last lookups, so `--probe` can save them
/// for verification against the real API. A provider without this capability still works.
/// </summary>
public interface IProbeSource
{
    IReadOnlyList<ProbeExchange> Exchanges { get; }

    void ResetExchanges();
}

/// <summary>
/// Removes credentials from probe captures. The token request is never captured, and both request
/// and response headers lose anything that looks like a credential (Authorization, cookies, API
/// keys, client/account ids) on both sides.
/// </summary>
public static class ProbeSanitizer
{
    public const string Removed = "<removed>";

    private static readonly Regex SecretHeader = new(
        "(authorization|cookie|api[-_]?key|secret|token|client[-_]?id|account[-_]?id)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsSecretHeader(string name) => SecretHeader.IsMatch(name);

    public static IReadOnlyDictionary<string, string> Headers(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, values) in headers)
        {
            var joined = string.Join(", ", values);
            // A header named like a credential is dropped; nothing of its value is kept at all.
            if (IsSecretHeader(name))
            {
                continue;
            }

            result[name] = joined;
        }

        return result;
    }

    public static IReadOnlyDictionary<string, string> Headers(params (string Name, string? Value)[] headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in headers)
        {
            if (value is null || IsSecretHeader(name))
            {
                continue;
            }

            result[name] = value;
        }

        return result;
    }
}

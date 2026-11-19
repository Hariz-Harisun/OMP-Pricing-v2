using Microsoft.Extensions.Configuration;
using OmpPricing.App.Providers;

namespace OmpPricing.App.Providers.DigiKey;

/// <summary>
/// Registers DigiKey with <see cref="ProviderRegistry"/>. This is the only registration point: the
/// registry itself never names a DigiKey type.
/// </summary>
public sealed class DigiKeyProviderFactory : IPriceProviderFactory
{
    public string Name => DigiKeyProvider.ProviderName;

    public IPriceProvider? Create(IConfigurationSection section)
    {
        var options = new DigiKeyOptions();
        section.Bind(options);

        if (!options.Enabled)
        {
            return null;
        }

        options.Validate();

        var http = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl),
            Timeout = TimeSpan.FromSeconds(60),
        };

        var client = new DigiKeyClient(http, options);
        return new DigiKeyProvider(client, new Throttle(options.RateLimits), options.IsAuthorised);
    }
}

using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace OmpPricing.App.Providers;

/// <summary>
/// Builds the list of enabled providers from configuration. Each provider folder ships a factory
/// (<see cref="IPriceProviderFactory"/>), so adding a distributor is a folder, one config section and
/// no change here or in the Pricing/Excel code.
/// </summary>
public sealed class ProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IPriceProviderFactory> _factories;

    public ProviderRegistry(IEnumerable<IPriceProviderFactory> factories)
    {
        _factories = factories.ToDictionary(factory => factory.Name, StringComparer.OrdinalIgnoreCase);
    }

    public static ProviderRegistry FromAssembly(Assembly? assembly = null)
    {
        var types = (assembly ?? typeof(ProviderRegistry).Assembly).GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface
                && typeof(IPriceProviderFactory).IsAssignableFrom(type)
                && type.GetConstructor(Type.EmptyTypes) is not null);

        var factories = types
            .Select(type => (IPriceProviderFactory)Activator.CreateInstance(type)!)
            .ToList();

        return new ProviderRegistry(factories);
    }

    public IReadOnlyCollection<string> KnownProviders => _factories.Keys.ToList();

    public IReadOnlyList<IPriceProvider> Build(IConfiguration configuration)
    {
        var providers = new List<IPriceProvider>();

        foreach (var section in configuration.GetSection("Providers").GetChildren())
        {
            if (!_factories.TryGetValue(section.Key, out var factory))
            {
                continue;
            }

            var provider = factory.Create(section);
            if (provider is not null)
            {
                providers.Add(provider);
            }
        }

        return providers;
    }
}

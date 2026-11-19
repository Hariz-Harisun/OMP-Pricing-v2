using Microsoft.Extensions.Configuration;
using OmpPricing.App.Providers;

namespace OmpPricing.Tests.Providers;

/// <summary>Configuration decides what runs: Providers:&lt;Name&gt;:Enabled and the credentials.</summary>
public sealed class ProviderRegistryTests
{
    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value))
            .Build();

    [Fact]
    public void Disabled_provider_is_not_built()
    {
        var registry = ProviderRegistry.FromAssembly();

        var providers = registry.Build(Configuration(("Providers:DigiKey:Enabled", "false")));

        Assert.Empty(providers);
    }

    [Fact]
    public void Enabled_provider_without_credentials_stops_with_a_clear_error()
    {
        var registry = ProviderRegistry.FromAssembly();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.Build(Configuration(
                ("Providers:DigiKey:Enabled", "true"),
                ("Providers:DigiKey:BaseUrl", "https://api.digikey.test"))));

        Assert.Contains("DigiKey is enabled", exception.Message);
        Assert.Contains("ClientId", exception.Message);
        Assert.Contains("user-secrets", exception.Message);
    }

    [Fact]
    public void Enabled_provider_with_credentials_is_built_from_configuration()
    {
        var registry = ProviderRegistry.FromAssembly();

        var providers = registry.Build(Configuration(
            ("Providers:DigiKey:Enabled", "true"),
            ("Providers:DigiKey:ClientId", "unit-test-client"),
            ("Providers:DigiKey:ClientSecret", "unit-test-secret"),
            ("Providers:DigiKey:AccountId", "unit-test-account"),
            ("Providers:DigiKey:RateLimits:PerMinute", "120"),
            ("Providers:DigiKey:RateLimits:PerDay", "1000")));

        var provider = Assert.Single(providers);
        Assert.Equal("DigiKey", provider.Name);
    }

    [Fact]
    public void Unknown_provider_sections_are_ignored()
    {
        var registry = ProviderRegistry.FromAssembly();

        var providers = registry.Build(Configuration(("Providers:SomeoneElse:Enabled", "true")));

        Assert.Empty(providers);
    }

    [Fact]
    public void Registry_knows_the_providers_shipped_in_this_build()
    {
        var registry = ProviderRegistry.FromAssembly();

        Assert.Contains("DigiKey", registry.KnownProviders);
    }
}

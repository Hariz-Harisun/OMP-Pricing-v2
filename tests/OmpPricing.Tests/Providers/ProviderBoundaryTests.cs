using System.Reflection;
using OmpPricing.App.Pipeline;

namespace OmpPricing.Tests.Providers;

/// <summary>
/// Nothing outside Providers/DigiKey/ may reference a DigiKey type (AGENTS.md "Providers"). A new
/// distributor plugs in through IPriceProviderFactory, so the registry stays free of provider types.
/// </summary>
public sealed class ProviderBoundaryTests
{
    private const string DigiKeyNamespace = "OmpPricing.App.Providers.DigiKey";

    [Fact]
    public void No_type_outside_the_digikey_folder_references_a_digikey_type()
    {
        var assembly = typeof(PricingPipeline).Assembly;

        var digiKeyTypes = assembly.GetTypes()
            .Where(type => type.Namespace is not null && type.Namespace.StartsWith(DigiKeyNamespace, StringComparison.Ordinal))
            .ToHashSet();

        var offenders = new List<string>();

        foreach (var type in assembly.GetTypes())
        {
            if (digiKeyTypes.Contains(type) || (type.Namespace?.StartsWith(DigiKeyNamespace, StringComparison.Ordinal) ?? false))
            {
                continue;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var field in type.GetFields(flags))
            {
                if (Touches(field.FieldType, digiKeyTypes))
                {
                    offenders.Add(type.FullName + "." + field.Name);
                }
            }

            foreach (var property in type.GetProperties(flags))
            {
                if (Touches(property.PropertyType, digiKeyTypes))
                {
                    offenders.Add(type.FullName + "." + property.Name);
                }
            }

            foreach (var method in type.GetMethods(flags))
            {
                if (Touches(method.ReturnType, digiKeyTypes)
                    || method.GetParameters().Any(parameter => Touches(parameter.ParameterType, digiKeyTypes)))
                {
                    offenders.Add(type.FullName + "." + method.Name);
                }
            }
        }

        Assert.Empty(offenders);
    }

    private static bool Touches(Type type, HashSet<Type> digiKeyTypes)
    {
        if (digiKeyTypes.Contains(type))
        {
            return true;
        }

        if (type.IsArray)
        {
            return Touches(type.GetElementType()!, digiKeyTypes);
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        return type.GetGenericArguments().Any(argument => Touches(argument, digiKeyTypes));
    }
}

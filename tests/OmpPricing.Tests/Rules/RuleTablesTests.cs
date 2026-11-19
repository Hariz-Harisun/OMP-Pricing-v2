using OmpPricing.App.Rules;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Rules;

/// <summary>Rule files: seed content, schema validation and the startup check on COO countries.</summary>
public sealed class RuleTablesTests
{
    private static string WriteRuleFile(string json, string name = "rules.json")
    {
        var path = Path.Combine(Sample.NewTempFolder(), name);
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Seed_suffix_rules_accept_the_murata_d_suffix()
    {
        var rules = SuffixRules.Load(Sample.RulesFile("suffix-rules.json"));

        Assert.Equal(1, rules.Count);
        Assert.True(rules.IsKnownSuffix("Murata", "BLM15GG221SZ1", "BLM15GG221SZ1D"));
        Assert.False(rules.IsKnownSuffix("Murata", "BLM15GG221SZ1", "BLM15GG221SZ1E"));
        Assert.False(rules.IsKnownSuffix("TDK", "BLM15GG221SZ1", "BLM15GG221SZ1D"));
    }

    [Fact]
    public void Seed_manufacturer_aliases_resolve_to_one_canonical_name()
    {
        var aliases = MfgAliases.Load(Sample.RulesFile("mfg-aliases.json"));

        Assert.Equal("Murata", aliases.Canonical("Murata Electronics"));
        Assert.Equal("Murata", aliases.Canonical("murata manufacturing"));
        Assert.Equal("TDK", aliases.Canonical("TDK"));
        Assert.Equal("", aliases.Canonical(null));
    }

    [Fact]
    public void Seed_supplier_terms_and_coo_rules_are_empty()
    {
        var countries = Sample.Countries;

        Assert.Equal(0, SupplierTermsTable.Load(Sample.RulesFile("supplier-terms.json")).Count);
        Assert.Equal(0, CooRulesTable.Load(Sample.RulesFile("coo-rules.json"), countries).Count);
    }

    [Fact]
    public void Supplier_term_with_a_bad_basis_is_rejected()
    {
        var path = WriteRuleFile(
            "[{\"provider\":\"DigiKey\",\"manufacturer\":null,\"cancellationWeeks\":2,\"rescheduleWeeks\":8,\"basis\":\"PO\"}]",
            "supplier-terms.json");

        var exception = Assert.Throws<InvalidOperationException>(() => SupplierTermsTable.Load(path));

        Assert.Contains("DB or PB", exception.Message);
    }

    [Fact]
    public void Supplier_term_without_a_provider_is_rejected()
    {
        var path = WriteRuleFile(
            "[{\"provider\":\"\",\"manufacturer\":null,\"cancellationWeeks\":2,\"rescheduleWeeks\":8,\"basis\":\"PB\"}]",
            "supplier-terms.json");

        var exception = Assert.Throws<InvalidOperationException>(() => SupplierTermsTable.Load(path));

        Assert.Contains("needs a provider", exception.Message);
    }

    [Fact]
    public void Valid_supplier_term_is_normalized()
    {
        var path = WriteRuleFile(
            "[{\"provider\":\"DigiKey\",\"manufacturer\":\"\",\"cancellationWeeks\":2,\"rescheduleWeeks\":8,\"basis\":\"pb\"}]",
            "supplier-terms.json");

        var table = SupplierTermsTable.Load(path);
        var entry = Assert.Single(table.Entries);

        Assert.Equal("PB", entry.Basis);
        Assert.Null(entry.Manufacturer);
    }

    [Fact]
    public void Coo_rule_country_must_be_in_countries_txt()
    {
        var path = WriteRuleFile(
            "[{\"manufacturer\":\"Murata\",\"series\":null,\"country\":\"Atlantis\"}]",
            "coo-rules.json");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CooRulesTable.Load(path, Sample.Countries));

        Assert.Contains("Atlantis", exception.Message);
        Assert.Contains("rules/countries.txt", exception.Message);
    }

    [Fact]
    public void Coo_rule_country_is_stored_with_the_sheet1_spelling()
    {
        var path = WriteRuleFile(
            "[{\"manufacturer\":\"Murata\",\"series\":null,\"country\":\"korea, republic of\"}]",
            "coo-rules.json");

        var table = CooRulesTable.Load(path, Sample.Countries);
        var entry = Assert.Single(table.Entries);

        Assert.Equal("Korea, Republic of", entry.Country);
        Assert.Null(entry.Series);
    }

    [Fact]
    public void Missing_rule_file_is_a_clear_error()
    {
        var path = Path.Combine(Sample.NewTempFolder(), "nope.json");

        var exception = Assert.Throws<FileNotFoundException>(() => SuffixRules.Load(path));

        Assert.Contains("Rule file not found", exception.Message);
    }

    [Fact]
    public void Invalid_json_names_the_file()
    {
        var path = WriteRuleFile("{ not json", "broken.json");

        var exception = Assert.Throws<InvalidOperationException>(() => MfgAliases.Load(path));

        Assert.Contains(path, exception.Message);
    }
}

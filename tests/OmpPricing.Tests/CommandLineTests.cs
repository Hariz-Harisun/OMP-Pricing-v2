namespace OmpPricing.Tests;

/// <summary>The command line: the probe pair, its quantity, and --no-cache (decisions D6, D7).</summary>
public sealed class CommandLineTests
{
    [Fact]
    public void Probe_takes_the_provider_and_the_mpn_positionally()
    {
        var options = App.CommandLine.Parse(["--probe", "DigiKey", "BAT54CW,115"]);

        Assert.Equal("DigiKey", options.ProbeProvider);
        Assert.Equal("BAT54CW,115", options.ProbeMpn);
        Assert.Null(options.File);
        Assert.Equal(1, options.Qty);
        Assert.False(options.ExportCountries);
    }

    [Fact]
    public void Probe_accepts_a_quantity_and_no_cache()
    {
        var options = App.CommandLine.Parse(["--probe", "DigiKey", "BLM18HE601SN1", "--qty", "400", "--no-cache"]);

        Assert.Equal(400, options.Qty);
        Assert.True(options.NoCache);
    }

    [Fact]
    public void A_missing_mpn_after_the_provider_is_an_error()
    {
        var exception = Assert.Throws<ArgumentException>(() => App.CommandLine.Parse(["--probe", "DigiKey"]));

        Assert.Contains("Missing value for --probe", exception.Message);
    }

    [Fact]
    public void A_non_positive_quantity_is_an_error()
    {
        Assert.Throws<ArgumentException>(
            () => App.CommandLine.Parse(["--probe", "DigiKey", "X1", "--qty", "0"]));
        Assert.Throws<ArgumentException>(
            () => App.CommandLine.Parse(["--probe", "DigiKey", "X1", "--qty", "lots"]));
    }

    [Fact]
    public void The_normal_run_still_parses_file_and_packaging()
    {
        var options = App.CommandLine.Parse(["--file", "in.xlsx", "--packaging", "CT"]);

        Assert.Equal("in.xlsx", options.File);
        Assert.Equal("CT", options.Packaging);
        Assert.Null(options.ProbeProvider);
        Assert.False(options.NoCache);
    }

    [Fact]
    public void Usage_documents_the_probe_and_no_cache()
    {
        Assert.Contains("--probe <provider> <MPN>", App.CommandLine.Usage);
        Assert.Contains("--no-cache", App.CommandLine.Usage);
        Assert.Contains("--qty <n>", App.CommandLine.Usage);
    }
}

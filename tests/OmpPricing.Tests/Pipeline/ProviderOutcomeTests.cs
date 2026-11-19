using ClosedXML.Excel;
using OmpPricing.App.Pricing;
using OmpPricing.App.Providers;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Pipeline;

/// <summary>
/// Provider outcomes decide the line status, and an empty provider list must never produce a price
/// (owner decision D5 in docs/decisions.md).
/// </summary>
public sealed class ProviderOutcomeTests
{
    [Fact]
    public void No_provider_configured_means_not_found_and_no_price_written()
    {
        using var run = PipelineRun.Execute();

        Assert.Equal(74, run.Result.StatusCounts[LineStatus.NotFound]);
        Assert.Equal(71, run.Result.StatusCounts[LineStatus.NeedsReview]);
        Assert.False(run.Result.StatusCounts.ContainsKey(LineStatus.Incomplete));
        Assert.Empty(run.Result.ProviderCalls);

        using var output = run.OpenOutput();
        var sheet = output.Worksheet("LT Quote");

        Assert.Equal("No price provider configured", sheet.Cell(3, 22).GetString());
        Assert.True(sheet.Cell(3, 7).IsEmpty());
        Assert.True(sheet.Cell(3, 11).IsEmpty());
        Assert.True(sheet.Cell(3, 19).IsEmpty());
    }

    [Fact]
    public void Throttled_provider_puts_the_rows_in_waiting_for_quota()
    {
        var provider = new FakeProvider().RespondAll(_ => ProviderResult.Throttled("daily quota"));

        using var run = PipelineRun.Execute(provider);

        Assert.Equal(74, run.Result.StatusCounts[LineStatus.WaitingForQuota]);
        Assert.Equal(72, run.Result.ProviderCalls["FakeProvider Throttled"]);

        using var output = run.OpenOutput();
        Assert.Contains("Waiting for provider quota", output.Worksheet("LT Quote").Cell(3, 22).GetString());
    }

    [Fact]
    public void Contract_priced_offers_are_ignored()
    {
        var provider = new FakeProvider().RespondAll(query =>
            ProviderResult.Found([FakeProvider.EchoOffer(query, isContractPrice: true)]));

        using var run = PipelineRun.Execute(provider);

        Assert.Equal(74, run.Result.StatusCounts[LineStatus.NotFound]);

        using var output = run.OpenOutput();
        Assert.True(output.Worksheet("LT Quote").Cell(3, 11).IsEmpty());
    }

    [Fact]
    public void Ambiguous_answer_needs_review_with_the_provider_message()
    {
        var provider = new FakeProvider().RespondAll(_ =>
            ProviderResult.Ambiguous("Duplicate Products found"));

        using var run = PipelineRun.Execute(provider);

        // 71 NeedsReview from the input rules plus the 74 ambiguous lookups.
        Assert.Equal(145, run.Result.StatusCounts[LineStatus.NeedsReview]);

        using var output = run.OpenOutput();
        Assert.Contains("Duplicate Products found", output.Worksheet("LT Quote").Cell(3, 22).GetString());
    }

    [Fact]
    public void Prefix_only_candidate_needs_review_and_is_not_priced()
    {
        var provider = new FakeProvider().RespondAll(query =>
            ProviderResult.Found([FakeProvider.EchoOffer(query, mpn: query.Mpn + "ZZZZ")]));

        using var run = PipelineRun.Execute(provider);

        using var output = run.OpenOutput();
        var sheet = output.Worksheet("LT Quote");

        Assert.True(sheet.Cell(40, 11).IsEmpty());
        Assert.Contains("Needs review", sheet.Cell(40, 22).GetString());
        Assert.Equal(145, run.Result.StatusCounts[LineStatus.NeedsReview]);
    }

    [Fact]
    public void Packaging_variant_candidate_is_accepted_and_written()
    {
        var provider = new FakeProvider().RespondAll(query =>
            ProviderResult.Found([FakeProvider.EchoOffer(query, mpn: query.Mpn + "D")]));

        using var run = PipelineRun.Execute(provider);

        using var output = run.OpenOutput();
        var sheet = output.Worksheet("LT Quote");

        // Row 3 is the Murata wildcard row: BLM15GG221SZ1 with prefix BLM15GG221SZ1.
        Assert.Equal("BLM15GG221SZ1D", sheet.Cell(3, 9).GetString());
        Assert.Equal("T&R", sheet.Cell(3, 18).GetString());
        Assert.Equal(LineStatus.Incomplete, RunStatus(run, 3));
    }

    private static LineStatus RunStatus(PipelineRun run, int row)
    {
        using var output = run.OpenOutput();
        var sheet = output.Worksheet("Offers");
        var rows = Enumerable.Range(2, sheet.LastRowUsed()!.RowNumber() - 1)
            .Where(candidate => sheet.Cell(candidate, 1).GetString() == row.ToString());

        var status = sheet.Cell(rows.First(), 2).GetString();
        return Enum.Parse<LineStatus>(status);
    }
}

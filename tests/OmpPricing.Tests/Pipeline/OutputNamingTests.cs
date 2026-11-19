using OmpPricing.App.Pipeline;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Pipeline;

/// <summary>The output is named &lt;name&gt;_&lt;yyyyMMdd-HHmmss&gt;.xlsx and never overwrites.</summary>
public sealed class OutputNamingTests
{
    [Fact]
    public async Task Output_name_carries_the_utc_timestamp()
    {
        var folder = Sample.NewTempFolder();
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));
        var sink = new FolderResultSink(folder, time);

        var path = await sink.SaveAsync(new InputWorkbook("LT2026100009.xlsx", [1, 2, 3]), CancellationToken.None);

        Assert.Equal(
            Path.Combine(folder, "LT2026100009_20260304-050607.xlsx"),
            path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Second_run_in_the_same_second_gets_a_suffix_and_the_first_file_survives()
    {
        var folder = Sample.NewTempFolder();
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));
        var sink = new FolderResultSink(folder, time);

        var first = await sink.SaveAsync(new InputWorkbook("LT2026100009.xlsx", [1]), CancellationToken.None);
        var second = await sink.SaveAsync(new InputWorkbook("LT2026100009.xlsx", [2]), CancellationToken.None);

        Assert.EndsWith("LT2026100009_20260304-050607.xlsx", first);
        Assert.EndsWith("LT2026100009_20260304-050607-1.xlsx", second);
        Assert.NotEqual(first, second);
        Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(first));
        Assert.Equal(new byte[] { 2 }, await File.ReadAllBytesAsync(second));
    }

    [Fact]
    public async Task Different_timestamps_give_different_names()
    {
        var folder = Sample.NewTempFolder();
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));
        var sink = new FolderResultSink(folder, time);

        var first = await sink.SaveAsync(new InputWorkbook("quote.xlsx", [1]), CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(1));
        var second = await sink.SaveAsync(new InputWorkbook("quote.xlsx", [2]), CancellationToken.None);

        Assert.EndsWith("quote_20260304-050607.xlsx", first);
        Assert.EndsWith("quote_20260304-050608.xlsx", second);
    }
}

using System.Net;
using System.Text;
using ClosedXML.Excel;
using OmpPricing.App.Providers;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Pipeline;

/// <summary>
/// Decision D9: every packaging variation an accepted match returns is written to the Offers sheet,
/// with exactly one of them marked as the selected offer.
/// </summary>
public sealed class OffersSheetTests : IClassFixture<OffersSheetTests.Fixture>
{
    private readonly Fixture _fixture;

    public OffersSheetTests(Fixture fixture) => _fixture = fixture;

    public sealed class Fixture : IDisposable
    {
        public Fixture() => Run = PipelineRun.Execute(CreateProvider());

        public PipelineRun Run { get; }

        public void Dispose() => Run.Dispose();

        /// <summary>The DigiKey provider over a stub that answers with the three-variation fixture.</summary>
        private static IPriceProvider CreateProvider()
        {
            var (provider, handler) = DigiKeyTestSetup.CreateProvider();
            var fixture = DigiKeyTestSetup.Fixture("blm15gg221sz1d-productdetails.json");

            handler.RespondAny(request =>
            {
                var uri = request.RequestUri!.AbsoluteUri;

                if (uri.Contains("/v1/oauth2/token", StringComparison.Ordinal))
                {
                    return Json(DigiKeyTestSetup.TokenJson);
                }

                // Echo the requested MPN into the fixture so the MPN matches the spreadsheet line.
                var number = ExtractProductNumber(uri);
                var body = fixture.Replace("BLM15GG221SZ1D", number, StringComparison.Ordinal);

                var response = Json(body);
                response.Headers.TryAddWithoutValidation("X-RateLimit-Limit", "120");
                response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "99");
                return response;
            });

            return provider;
        }

        private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        private static string ExtractProductNumber(string uri)
        {
            const string marker = "/productdetails";
            var path = new Uri(uri).AbsolutePath;
            var index = path.IndexOf("/search/", StringComparison.Ordinal) + "/search/".Length;
            var end = path.IndexOf(marker, StringComparison.Ordinal);
            var segment = end > index ? path[index..end] : path[index..];
            return Uri.UnescapeDataString(segment);
        }
    }

    [Fact]
    public void Every_variation_of_an_accepted_match_is_listed()
    {
        using var output = _fixture.Run.OpenOutput();
        var sheet = output.Worksheet("Offers");

        // Line 10 is a Murata part, so the fixture's manufacturer matches.
        var rows = OfferRows(sheet, 10).ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal(
            ["Tape & Reel (TR)", "Cut Tape (CT)", "Digi-Reel®"],
            rows.Select(row => sheet.Cell(row, 12).GetString()));
        Assert.Equal(3, rows.Select(row => sheet.Cell(row, 15).GetString()).Distinct().Count());
        Assert.Single(rows, row => sheet.Cell(row, 3).GetString() == "Yes");
        Assert.Equal(2, rows.Count(row => sheet.Cell(row, 3).GetString() == "No"));
    }

    [Fact]
    public void The_cheapest_variation_is_the_one_selected()
    {
        using var output = _fixture.Run.OpenOutput();
        var sheet = output.Worksheet("Offers");

        var selected = OfferRows(sheet, 10).Single(row => sheet.Cell(row, 3).GetString() == "Yes");
        var client = output.Worksheet("LT Quote");

        // Tape and Reel is 0.213 at MOQ 10000; cut tape and Digi-Reel start at 0.76.
        Assert.Equal("Tape & Reel (TR)", sheet.Cell(selected, 12).GetString());
        Assert.Equal(10000, sheet.Cell(selected, 9).Value.GetNumber());
        Assert.Equal("490-GCJ32EC71E226KE02TR-ND", sheet.Cell(selected, 15).GetString());
        Assert.Equal("T&R", client.Cell(10, 18).GetString());
        Assert.Equal(0.213m, (decimal)client.Cell(10, 11).Value.GetNumber());
    }

    [Fact]
    public void Offers_sheet_lists_all_three_variations_for_every_murata_line()
    {
        using var output = _fixture.Run.OpenOutput();
        var sheet = output.Worksheet("Offers");

        Assert.Equal("Line", sheet.Cell(1, 1).GetString());
        Assert.Equal("Provider part", sheet.Cell(1, 15).GetString());

        var last = sheet.LastRowUsed()!.RowNumber();
        var lines = Enumerable.Range(2, last - 1)
            .Where(row => sheet.Cell(row, 4).GetString() == "DigiKey")
            .Select(row => sheet.Cell(row, 1).GetString())
            .Distinct()
            .Count();

        // The fixture is a Murata part, so the 12 Murata lines plus 1 empty-MFG line match it.
        Assert.Equal(13, lines);
        Assert.Equal(13 * 3, Enumerable.Range(2, last - 1).Count(row => sheet.Cell(row, 4).GetString() == "DigiKey"));
    }

    private static IEnumerable<int> OfferRows(IXLWorksheet sheet, int line)
    {
        var last = sheet.LastRowUsed()!.RowNumber();
        return Enumerable.Range(2, last - 1).Where(row => sheet.Cell(row, 1).GetString() == line.ToString());
    }
}

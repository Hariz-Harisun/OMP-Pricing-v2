using OmpPricing.App.Excel;
using OmpPricing.App.Rules;

namespace OmpPricing.Tests.Support;

internal static class Sample
{
    public const string FileName = "LT2026100009.xlsx";

    public static string SamplePath => Path.Combine(AppContext.BaseDirectory, "samples", FileName);

    public static byte[] Bytes => File.ReadAllBytes(SamplePath);

    public static string RulesFile(string name) => Path.Combine(AppContext.BaseDirectory, "rules", name);

    public static CountryList Countries => CountryList.Load(RulesFile("countries.txt"));

    public static LoadedWorkbook Load(ExcelOptions? options = null)
        => WorkbookReader.Load(Bytes, options ?? new ExcelOptions());

    public static string NewTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "omp-pricing-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}

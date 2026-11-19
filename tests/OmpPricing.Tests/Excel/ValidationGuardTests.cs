using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using OmpPricing.App.Excel;
using OmpPricing.Tests.Support;

namespace OmpPricing.Tests.Excel;

/// <summary>
/// The client workbook keeps three dropdowns (A9.5). The country one is stored in an extension that
/// ClosedXML can drop; ValidationGuard must put it back.
/// </summary>
public sealed class ValidationGuardTests
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    public void Workbook_that_already_has_all_three_validations_is_returned_untouched()
    {
        var original = Sample.Bytes;

        var guarded = ValidationGuard.Ensure(original, new ExcelOptions(), 257);

        Assert.Equal(original, guarded);
        Assert.True(HasStandardValidation(guarded, "S2:S1048576"));
        Assert.True(HasStandardValidation(guarded, "T2:T1048576"));
        Assert.True(HasCountryValidation(guarded));
    }

    [Fact]
    public void Country_validation_is_restored_when_the_library_dropped_the_extension()
    {
        var stripped = StripExtensions(Sample.Bytes);

        Assert.False(HasCountryValidation(stripped));

        var restored = ValidationGuard.Ensure(stripped, new ExcelOptions(), 257);

        Assert.True(HasCountryValidation(restored));
        Assert.Contains("Sheet1!$A$2:$A$258", ReadSheetXml(restored));
        Assert.True(HasStandardValidation(restored, "S2:S1048576"));
        Assert.True(HasStandardValidation(restored, "T2:T1048576"));
    }

    [Fact]
    public void Yes_no_validations_are_restored_when_missing()
    {
        var stripped = StripStandardValidations(Sample.Bytes);

        Assert.False(HasStandardValidation(stripped, "S2:S1048576"));

        var restored = ValidationGuard.Ensure(stripped, new ExcelOptions(), 257);

        Assert.True(HasStandardValidation(restored, "S2:S1048576"));
        Assert.True(HasStandardValidation(restored, "T2:T1048576"));
        Assert.Contains("Yes, No", ReadSheetXml(restored));
    }

    private static bool HasStandardValidation(byte[] workbook, string sqref)
        => ReadSheetXml(workbook).Contains("sqref=\"" + sqref + "\"", StringComparison.Ordinal);

    private static bool HasCountryValidation(byte[] workbook)
    {
        var xml = ReadSheetXml(workbook);
        return xml.Contains("U1:U1048576", StringComparison.Ordinal)
            || xml.Contains("<xm:sqref>U:U</xm:sqref>", StringComparison.Ordinal);
    }

    private static string ReadSheetXml(byte[] workbook)
    {
        using var stream = new MemoryStream(workbook);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry("xl/worksheets/sheet1.xml")!;
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static byte[] StripExtensions(byte[] workbook) => Rewrite(workbook, document =>
    {
        var extensionList = document.Root?.Element(Main + "extLst");
        extensionList?.Remove();
    });

    private static byte[] StripStandardValidations(byte[] workbook) => Rewrite(workbook, document =>
    {
        var validations = document.Root?.Element(Main + "dataValidations");
        validations?.Remove();
    });

    private static byte[] Rewrite(byte[] workbook, Action<XDocument> change)
    {
        using var buffer = new MemoryStream();
        buffer.Write(workbook, 0, workbook.Length);
        buffer.Position = 0;

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("xl/worksheets/sheet1.xml")!;
            string xml;
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
            {
                xml = reader.ReadToEnd();
            }

            var document = XDocument.Parse(xml);
            change(document);

            entry.Delete();
            var replacement = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
            using var writer = new StreamWriter(replacement.Open(), new UTF8Encoding(false));
            writer.Write(document.ToString());
        }

        return buffer.ToArray();
    }
}

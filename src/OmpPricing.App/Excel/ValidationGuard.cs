using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace OmpPricing.App.Excel;

/// <summary>
/// Keeps the client's dropdowns intact (A9.5). After the workbook is written we check the sheet XML
/// for the Yes/No validations on the authorised and compliance columns and for the country list on
/// the COO column, which Excel stores in an extension (x14:dataValidations). ClosedXML can drop that
/// extension, so it is restored here.
/// </summary>
public static class ValidationGuard
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string X14Namespace = "http://schemas.microsoft.com/office/spreadsheetml/2009/9/main";
    private const string XmNamespace = "http://schemas.microsoft.com/office/excel/2006/main";
    private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string CountryValidationExtensionUri = "{CCE6A557-97BC-4b89-ADB6-D9C93CAAB3DF}";

    private static readonly XNamespace X14 = X14Namespace;
    private static readonly XNamespace Xm = XmNamespace;

    /// <summary>Returns the workbook with all three validations present, or the input when they already are.</summary>
    public static byte[] Ensure(byte[] workbook, ExcelOptions options, int countryCount)
    {
        using var buffer = new MemoryStream();
        buffer.Write(workbook, 0, workbook.Length);
        buffer.Position = 0;

        var changed = false;

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Update, leaveOpen: true))
        {
            var sheetPath = FindWorksheetPath(archive, options.SheetName);
            var entry = sheetPath is null ? null : archive.GetEntry(sheetPath);

            if (entry is not null)
            {
                string original;
                using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                {
                    original = reader.ReadToEnd();
                }

                var updated = EnsureValidations(original, options, countryCount, out changed);

                if (changed)
                {
                    entry.Delete();
                    var replacement = archive.CreateEntry(sheetPath!, CompressionLevel.Optimal);
                    using var writer = new StreamWriter(replacement.Open(), new UTF8Encoding(false));
                    writer.Write(updated);
                }
            }
        }

        return changed ? buffer.ToArray() : workbook;
    }

    private static string EnsureValidations(string xml, ExcelOptions options, int countryCount, out bool changed)
    {
        changed = false;

        var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        var root = document.Root;
        if (root is null)
        {
            return xml;
        }

        var columns = options.Columns;
        var authorisationLetter = ColumnLetter(columns.Authorised);
        var complianceLetter = ColumnLetter(columns.Compliance);
        var countryLetter = ColumnLetter(columns.Coo);

        var dataValidations = root.Element(Main + "dataValidations");
        var existing = dataValidations?.Elements(Main + "dataValidation").ToList() ?? [];

        var hasAuthorisation = existing.Any(validation => SqrefStartsWith(validation, authorisationLetter));
        var hasCompliance = existing.Any(validation => SqrefStartsWith(validation, complianceLetter));

        if (!hasAuthorisation || !hasCompliance)
        {
            if (dataValidations is null)
            {
                dataValidations = new XElement(Main + "dataValidations", new XAttribute("disablePrompts", "1"));
                var extensionList = root.Element(Main + "extLst");
                if (extensionList is not null)
                {
                    extensionList.AddBeforeSelf(dataValidations);
                }
                else
                {
                    root.Add(dataValidations);
                }
            }

            if (!hasAuthorisation)
            {
                dataValidations.Add(BuildYesNoValidation(authorisationLetter, "Authorised Distributor"));
            }

            if (!hasCompliance)
            {
                dataValidations.Add(BuildYesNoValidation(complianceLetter, "RoHS/REACH Compliance"));
            }

            dataValidations.SetAttributeValue("count", dataValidations.Elements(Main + "dataValidation").Count().ToString());
            changed = true;
        }

        var extensionListElement = root.Element(Main + "extLst");
        var hasCountry = extensionListElement is not null
            && extensionListElement.Descendants()
                .Any(element => element.Name.LocalName == "sqref"
                    && element.Value.TrimStart().StartsWith(countryLetter, StringComparison.OrdinalIgnoreCase));

        if (!hasCountry)
        {
            if (extensionListElement is null)
            {
                extensionListElement = new XElement(Main + "extLst");
                root.Add(extensionListElement);
            }

            extensionListElement.Add(BuildCountryValidation(options, countryCount, countryLetter));
            changed = true;
        }

        return changed ? document.Declaration is null
            ? document.ToString()
            : document.Declaration + Environment.NewLine + document.ToString() : xml;
    }

    private static XElement BuildYesNoValidation(string columnLetter, string promptTitle)
    {
        return new XElement(Main + "dataValidation",
            new XAttribute("type", "list"),
            new XAttribute("allowBlank", "1"),
            new XAttribute("showInputMessage", "1"),
            new XAttribute("showErrorMessage", "1"),
            new XAttribute("errorTitle", "Invalid"),
            new XAttribute("error", "Reply Yes or No only"),
            new XAttribute("promptTitle", promptTitle),
            new XAttribute("prompt", "Reply Yes or No only"),
            new XAttribute("sqref", columnLetter + "2:" + columnLetter + "1048576"),
            new XElement(Main + "formula1", "\"Yes, No\""));
    }

    private static XElement BuildCountryValidation(ExcelOptions options, int countryCount, string columnLetter)
    {
        var lastRow = options.CountryFirstRow + Math.Max(countryCount, 1) - 1;
        var source = options.CountrySheetName + "!$A$" + options.CountryFirstRow.ToString()
            + ":$A$" + lastRow.ToString();

        return new XElement(Main + "ext",
            new XAttribute("uri", CountryValidationExtensionUri),
            new XElement(X14 + "dataValidations",
                new XAttribute("disablePrompts", "1"),
                new XAttribute("count", "1"),
                new XAttribute(XNamespace.Xmlns + "xm", XmNamespace),
                new XElement(X14 + "dataValidation",
                    new XAttribute("type", "list"),
                    new XAttribute("errorStyle", "stop"),
                    new XAttribute("operator", "between"),
                    new XAttribute("allowBlank", "1"),
                    new XAttribute("showDropDown", "0"),
                    new XAttribute("showInputMessage", "1"),
                    new XAttribute("showErrorMessage", "1"),
                    new XAttribute("errorTitle", "Invalid Country of Origin"),
                    new XAttribute("error", "Please select a country from the list"),
                    new XAttribute("promptTitle", "Country of Origin"),
                    new XAttribute("prompt", "Please select a country from the list"),
                    new XElement(X14 + "formula1", new XElement(Xm + "f", source)),
                    new XElement(Xm + "sqref", columnLetter + "1:" + columnLetter + "1048576"))));
    }

    private static bool SqrefStartsWith(XElement validation, string columnLetter)
    {
        var sqref = (string?)validation.Attribute("sqref") ?? "";
        return sqref.StartsWith(columnLetter, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindWorksheetPath(ZipArchive archive, string sheetName)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbookEntry is null || relationshipsEntry is null)
        {
            return null;
        }

        XDocument workbook;
        XDocument relationships;

        using (var stream = workbookEntry.Open())
        {
            workbook = XDocument.Load(stream);
        }

        using (var stream = relationshipsEntry.Open())
        {
            relationships = XDocument.Load(stream);
        }

        var sheet = workbook.Descendants(Main + "sheet")
            .FirstOrDefault(element => (string?)element.Attribute("name") == sheetName);
        var relationshipId = (string?)sheet?.Attribute(XName.Get("id", RelationshipsNamespace));
        if (relationshipId is null)
        {
            return null;
        }

        var packageRelationships = XNamespace.Get(PackageRelationshipsNamespace);
        var target = relationships.Descendants(packageRelationships + "Relationship")
            .FirstOrDefault(element => (string?)element.Attribute("Id") == relationshipId)
            ?.Attribute("Target")?.Value;

        if (string.IsNullOrEmpty(target))
        {
            return null;
        }

        target = target.Replace((char)92, '/');
        if (target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) || target.StartsWith("/xl/", StringComparison.OrdinalIgnoreCase))
        {
            return target.TrimStart('/');
        }

        return target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
    }

    private static string ColumnLetter(int position)
    {
        var letter = "";
        var value = position;
        while (value > 0)
        {
            var remainder = (value - 1) % 26;
            letter = (char)('A' + remainder) + letter;
            value = (value - 1) / 26;
        }

        return letter;
    }
}

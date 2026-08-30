using System.IO.Compression;
using System.Xml.Linq;

namespace CapitalTracker.Application.Transfer;

/// <summary>
/// Reads the first worksheet of an .xlsx into the same grid a CSV produces, using nothing
/// but the BCL — an xlsx is a zip of XML, and the part of it that holds a table is small.
/// A dependency here would buy little: what actually bites are shared strings, sparse rows
/// and dates-as-numbers, and each of those has to be handled deliberately either way.
///
/// This exists because the real statements are .xlsx. Ukrainian banks hand out Excel, not
/// CSV, and asking the owner to re-save every file by hand is friction we can just remove.
/// </summary>
public static class XlsxReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static bool LooksLikeXlsx(byte[] content) =>
        content.Length > 4 && content[0] == 'P' && content[1] == 'K' && content[2] == 3 && content[3] == 4;

    public static List<string[]> Read(byte[] content)
    {
        using var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);

        var shared = ReadSharedStrings(archive);
        var sheet = Load(archive, FindFirstSheetPath(archive))
            ?? throw new InvalidOperationException("У файлі немає жодного аркуша.");

        var rows = new List<string[]>();

        foreach (var row in sheet.Descendants(Main + "row"))
        {
            var cells = new Dictionary<int, string>();

            foreach (var cell in row.Elements(Main + "c"))
            {
                // Placed by the cell's own reference, never by position: Excel omits empty
                // cells entirely, so appending in order silently shifts every column after
                // the first gap.
                var column = ColumnIndex(cell.Attribute("r")?.Value);
                var value = CellValue(cell, shared);
                if (value.Length > 0)
                    cells[column] = value;
            }

            if (cells.Count == 0)
            {
                rows.Add([]);
                continue;
            }

            var width = cells.Keys.Max() + 1;
            var line = new string[width];
            for (var i = 0; i < width; i++)
            {
                line[i] = cells.TryGetValue(i, out var value) ? value : "";
            }

            rows.Add(line);
        }

        // Ragged by nature — a header of eight columns over rows of six. Padded so every
        // consumer can index by column without bounds-checking each time.
        var widest = rows.Count == 0 ? 0 : rows.Max(r => r.Length);
        return rows
            .Select(r => r.Length == widest ? r : [.. r, .. Enumerable.Repeat("", widest - r.Length)])
            .ToList();
    }

    private static string CellValue(XElement cell, IReadOnlyList<string> shared)
    {
        var type = cell.Attribute("t")?.Value;

        if (type == "inlineStr")
            return Text(cell.Element(Main + "is"));

        var raw = cell.Element(Main + "v")?.Value ?? "";

        if (type == "s" && int.TryParse(raw, out var index) && index >= 0 && index < shared.Count)
            return shared[index];

        // Dates arrive as serial numbers and are left as such here: turning them back
        // requires the style table, and the date parser downstream already knows how to
        // recognise one. Keeping this reader dumb keeps that guesswork in one place.
        return raw;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var document = Load(archive, "xl/sharedStrings.xml");
        return document is null
            ? []
            : document.Root!.Elements(Main + "si").Select(Text).ToList();
    }

    /// <summary>Concatenates the runs a single string can be split across when it carries formatting.</summary>
    private static string Text(XElement? element) =>
        element is null ? "" : string.Concat(element.Descendants(Main + "t").Select(t => t.Value));

    /// <summary>
    /// The first sheet in the workbook's own order, resolved through the relationship file
    /// rather than assumed to be sheet1.xml — the names do not always line up.
    /// </summary>
    private static string FindFirstSheetPath(ZipArchive archive)
    {
        var workbook = Load(archive, "xl/workbook.xml");
        var relationships = Load(archive, "xl/_rels/workbook.xml.rels");

        var id = workbook?.Root?.Element(Main + "sheets")?.Elements(Main + "sheet").FirstOrDefault()
            ?.Attribute(Rel + "id")?.Value;

        var target = relationships?.Root?.Elements(PackageRel + "Relationship")
            .FirstOrDefault(r => r.Attribute("Id")?.Value == id)
            ?.Attribute("Target")?.Value;

        if (string.IsNullOrEmpty(target))
            return "xl/worksheets/sheet1.xml";

        return target.StartsWith('/') ? target[1..] : $"xl/{target}";
    }

    private static XDocument? Load(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        if (entry is null)
            return null;

        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    /// <summary>"BC12" is column 54. Letters only, and Excel counts them base-26 from 1.</summary>
    private static int ColumnIndex(string? reference)
    {
        var index = 0;
        foreach (var c in reference ?? "")
        {
            if (c is < 'A' or > 'Z')
                break;

            index = index * 26 + (c - 'A' + 1);
        }

        return Math.Max(index - 1, 0);
    }
}

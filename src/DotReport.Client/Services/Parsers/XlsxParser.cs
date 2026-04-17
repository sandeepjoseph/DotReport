using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace DotReport.Client.Services.Parsers;

/// <summary>
/// Extracts data from .xlsx via SpreadsheetML — no ClosedXML/OpenXml dependency.
/// Uses System.IO.Compression + LINQ to XML (BCL only).
/// </summary>
public sealed class XlsxParser : IDocumentParser
{
    public bool CanHandle(string extension) =>
        extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase);

    public async Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;

        using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
        var sharedStrings = LoadSharedStrings(zip);
        var sb = new StringBuilder();
        int sheetIndex = 0;

        foreach (var entry in zip.Entries.Where(e =>
            e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
            e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
        {
            ct.ThrowIfCancellationRequested();
            sheetIndex++;
            sb.AppendLine($"--- Sheet {sheetIndex} ---");

            using var sheetStream = entry.Open();
            var rows = await ParseRowsAsync(sheetStream, sharedStrings, ct);
            if (rows.Count == 0) continue;

            var headers = rows[0];
            sb.AppendLine($"Columns: {string.Join(", ", headers.Where(h => !string.IsNullOrWhiteSpace(h)))}");
            sb.AppendLine($"Rows: {rows.Count - 1}");
            sb.AppendLine();

            int maxRows = Math.Min(rows.Count - 1, 50);
            for (int i = 1; i <= maxRows; i++)
            {
                var cells = rows[i];
                sb.AppendLine($"--- Row {i} ---");
                for (int j = 0; j < Math.Min(headers.Length, cells.Length); j++)
                {
                    if (!string.IsNullOrWhiteSpace(headers[j]) && !string.IsNullOrWhiteSpace(cells[j]))
                        sb.AppendLine($"{headers[j]}: {cells[j]}");
                }
            }

            if (rows.Count - 1 > maxRows)
                sb.AppendLine($"[{rows.Count - 1 - maxRows} additional rows not shown]");
        }

        return sb.ToString();
    }

    private static List<string> LoadSharedStrings(ZipArchive zip)
    {
        var result = new List<string>();
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return result;
        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        result.AddRange(doc.Descendants(ns + "si")
            .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value))));
        return result;
    }

    private static async Task<List<string[]>> ParseRowsAsync(
        Stream stream, List<string> sharedStrings, CancellationToken ct)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
        var result = new List<string[]>();

        foreach (var row in doc.Descendants(ns + "row"))
        {
            var cells = row.Elements(ns + "c").Select(cell =>
            {
                var type  = cell.Attribute("t")?.Value;
                var value = cell.Element(ns + "v")?.Value ?? string.Empty;
                if (type == "s" && int.TryParse(value, out int idx) && idx < sharedStrings.Count)
                    return sharedStrings[idx];
                if (type == "inlineStr")
                    return string.Concat(cell.Descendants(ns + "t").Select(t => t.Value));
                return value;
            }).ToArray();
            result.Add(cells);
        }

        return result;
    }
}

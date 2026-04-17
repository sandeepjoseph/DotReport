using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace DotReport.Client.Services.Parsers;

/// <summary>
/// Extracts text from .docx files via Open XML package — no DocumentFormat.OpenXml dependency.
/// Uses System.IO.Compression + LINQ to XML (BCL only).
/// </summary>
public sealed class DocxParser : IDocumentParser
{
    public bool CanHandle(string extension) =>
        extension.Equals(".docx", StringComparison.OrdinalIgnoreCase);

    public async Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;

        using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
        var docEntry = zip.GetEntry("word/document.xml");
        if (docEntry is null) return string.Empty;

        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var sb = new StringBuilder();

        using var xmlStream = docEntry.Open();
        var doc = await XDocument.LoadAsync(xmlStream, LoadOptions.None, ct);

        // Paragraphs
        foreach (var para in doc.Descendants(w + "p"))
        {
            ct.ThrowIfCancellationRequested();
            var text = string.Concat(para.Descendants(w + "t").Select(t => t.Value));
            if (!string.IsNullOrWhiteSpace(text))
                sb.AppendLine(text.Trim());
        }

        // Tables — emit two-cell rows as Key: Value
        int tblIdx = 0;
        foreach (var tbl in doc.Descendants(w + "tbl"))
        {
            tblIdx++;
            sb.AppendLine($"--- Table {tblIdx} ---");
            foreach (var row in tbl.Elements(w + "tr"))
            {
                var cells = row.Elements(w + "tc")
                    .Select(c => string.Concat(c.Descendants(w + "t").Select(t => t.Value)).Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToArray();

                if (cells.Length == 2)       sb.AppendLine($"{cells[0]}: {cells[1]}");
                else if (cells.Length > 0)   sb.AppendLine(string.Join(" | ", cells));
            }
        }

        return sb.ToString();
    }
}

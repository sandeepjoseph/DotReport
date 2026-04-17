using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace DotReport.Client.Services.Parsers;

/// <summary>
/// Extracts text from PDF files using PdfPig (pure .NET, WASM-compatible).
/// Filters binary garbage that appears when PDFs use custom font encodings.
/// </summary>
public sealed class PdfParser : IDocumentParser
{
    // Register code-page encodings once — PdfPig needs Windows-1252 and similar
    // for older PDFs. Must be called before PdfDocument.Open in WASM environments.
    static PdfParser()
    {
        try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }
        catch { /* already registered */ }
    }

    public bool CanHandle(string extension) =>
        extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<string> ExtractTextAsync(
        Stream stream, string fileName, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        PdfDocument pdf;
        try
        {
            pdf = PdfDocument.Open(bytes);
        }
        catch (Exception ex) when (
            ex is TypeInitializationException ||
            ex is PlatformNotSupportedException ||
            ex is NotSupportedException)
        {
            return $"[PDF parsing unavailable: {ex.InnerException?.Message ?? ex.Message}. " +
                   "Export the PDF as plain text (.txt) and upload that file instead.]";
        }

        using (pdf)
        {
            var sb      = new StringBuilder();
            int pageNum = 0;

            foreach (Page page in pdf.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                pageNum++;
                var raw = page.Text;
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var cleaned = CleanPageText(raw);
                if (string.IsNullOrWhiteSpace(cleaned)) continue;

                sb.AppendLine($"--- Page {pageNum} ---");
                sb.AppendLine(cleaned);
                sb.AppendLine();
            }

            var result = sb.ToString();

            // If the entire document is still mostly unreadable (custom encoding failure),
            // return a clear message rather than binary noise.
            if (!string.IsNullOrWhiteSpace(result) && !HasReadableContent(result))
                return $"[PDF text extraction failed: this PDF uses a custom font encoding " +
                       $"that could not be decoded. Page count: {pageNum}. " +
                       "Try exporting the PDF as a plain-text file and uploading that instead.]";

            return result;
        }
    }

    // ── Cleaning helpers ─────────────────────────────────────────────────────

    private static string CleanPageText(string text)
    {
        var lines = text.Split('\n');
        var sb    = new StringBuilder(text.Length);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            // Skip lines dominated by non-printable / non-ASCII characters.
            int printable = line.Count(c => (c >= 32 && c < 127) || (c >= 160 && c <= 255));
            if ((double)printable / line.Length < 0.80) continue;

            if (IsPdfStreamLine(line)) continue;

            sb.AppendLine(line);
        }

        return sb.ToString().Trim();
    }

    private static bool IsPdfStreamLine(string line)
    {
        if (line.Length > 120) return false;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;

        int numericCount = parts.Count(p =>
            double.TryParse(p.TrimStart('-', '+'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out _));

        bool allNumericish = (double)numericCount / parts.Length >= 0.80;
        bool hasRealWords  = parts.Any(p => p.Length > 3 &&
                                            p.All(c => char.IsLetter(c) || c == '-' || c == '\''));
        return allNumericish && !hasRealWords;
    }

    private static bool HasReadableContent(string text)
    {
        var wordMatches = Regex.Matches(text, @"[A-Za-z]{3,}");
        return wordMatches.Count >= Math.Max(5, text.Length / 50);
    }
}

namespace DotReport.Client.Services.Parsers;

/// <summary>
/// Routes uploaded files to the appropriate parser by extension.
/// Supported: .txt .md .csv .tsv .pdf .docx .xlsx
/// </summary>
public sealed class DocumentParserFactory
{
    private readonly IReadOnlyList<IDocumentParser> _parsers = new IDocumentParser[]
    {
        new CsvParser(),
        new DocxParser(),
        new XlsxParser(),
        new PdfParser(),
        new PlainTextParser(),   // broadest match — always last
    };

    public const string SupportedExtensions = ".txt, .md, .csv, .tsv, .pdf, .docx, .xlsx";

    public bool CanParse(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return _parsers.Any(p => p.CanHandle(ext));
    }

    public async Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var parser = _parsers.FirstOrDefault(p => p.CanHandle(ext))
            ?? throw new NotSupportedException(
                $"Unsupported file type '{ext}'. Supported: {SupportedExtensions}");

        return await parser.ExtractTextAsync(stream, fileName, ct);
    }
}

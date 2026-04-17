namespace DotReport.Client.Services.Parsers;

public sealed class PlainTextParser : IDocumentParser
{
    private static readonly string[] Supported = { ".txt", ".md", ".log", ".json", ".xml" };

    public bool CanHandle(string extension) =>
        Array.Exists(Supported, e => e.Equals(extension, StringComparison.OrdinalIgnoreCase));

    public async Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }
}

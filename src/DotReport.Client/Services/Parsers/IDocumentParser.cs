namespace DotReport.Client.Services.Parsers;

public interface IDocumentParser
{
    bool CanHandle(string extension);
    Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default);
}

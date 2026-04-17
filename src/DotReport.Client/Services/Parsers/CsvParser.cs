using System.Text;

namespace DotReport.Client.Services.Parsers;

/// <summary>
/// Parses CSV/TSV files into Key: Value text blocks suitable for field extraction.
/// First row is treated as column headers.
/// </summary>
public sealed class CsvParser : IDocumentParser
{
    public bool CanHandle(string extension) =>
        extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase);

    public async Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var raw = await reader.ReadToEndAsync(ct);

        char delimiter = fileName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ',';
        var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return string.Empty;

        var headers = SplitLine(lines[0], delimiter);
        var sb = new StringBuilder();
        sb.AppendLine($"Columns: {string.Join(", ", headers)}");
        sb.AppendLine($"Total rows: {lines.Length - 1}");
        sb.AppendLine();

        int maxRows = Math.Min(lines.Length - 1, 50);
        for (int i = 1; i <= maxRows; i++)
        {
            ct.ThrowIfCancellationRequested();
            var values = SplitLine(lines[i], delimiter);
            sb.AppendLine($"--- Row {i} ---");
            for (int j = 0; j < Math.Min(headers.Length, values.Length); j++)
            {
                var val = values[j].Trim('"', ' ', '\r');
                if (!string.IsNullOrWhiteSpace(val))
                    sb.AppendLine($"{headers[j]}: {val}");
            }
        }

        if (lines.Length - 1 > maxRows)
            sb.AppendLine($"[{lines.Length - 1 - maxRows} additional rows not shown]");

        return sb.ToString();
    }

    private static string[] SplitLine(string line, char delimiter)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        foreach (char c in line)
        {
            if (c == '"')       { inQuotes = !inQuotes; }
            else if (c == delimiter && !inQuotes) { result.Add(current.ToString().Trim()); current.Clear(); }
            else                { current.Append(c); }
        }
        result.Add(current.ToString().Trim());
        return result.ToArray();
    }
}

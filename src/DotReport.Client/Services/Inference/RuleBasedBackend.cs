using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace DotReport.Client.Services.Inference;

/// <summary>
/// Tier-3 fallback: built-in regex / pattern extraction.
/// Always available — produces structured output from any document without any model.
/// </summary>
public sealed class RuleBasedBackend : IInferenceBackend
{
    public BackendTier Tier       => BackendTier.Tier3_Rules;
    public string      Name       => "Built-in Pattern Engine";
    public bool        IsReadyNow => true;   // always available — no dependencies

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => Task.FromResult(true);

    public async IAsyncEnumerable<string> StreamAsync(
        BackendRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var token in ExtractFieldsAsync(request.Prompt, ct))
            yield return token;
    }

    // ── Rule-based extraction ─────────────────────────────────────────────────

    private static async IAsyncEnumerable<string> ExtractFieldsAsync(
        string document,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var lines  = document.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var fields = new List<(string label, string value, float confidence)>();

        // Pattern 1 — explicit "Key: Value" lines
        var kvRx = new Regex(@"^([A-Za-z][A-Za-z0-9 _\-]{1,40})\s*[:=]\s*(.+)$");
        foreach (var line in lines)
        {
            var m = kvRx.Match(line.Trim());
            if (!m.Success) continue;
            var label = m.Groups[1].Value.Trim();
            var value = m.Groups[2].Value.Trim();
            if (value.Length is < 1 or > 200) continue;
            int printable = value.Count(c => c >= 32 && c < 127);
            if ((double)printable / value.Length < 0.90) continue;
            fields.Add((label, value, 0.88f));
        }

        // Pattern 2 — email addresses
        var emailRx = new Regex(@"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b");
        foreach (Match m in emailRx.Matches(document))
            fields.Add(("Email", m.Value, 0.95f));

        // Pattern 3 — dates
        var dateRx = new Regex(
            @"\b(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}|\d{4}[\/\-]\d{2}[\/\-]\d{2}|" +
            @"(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?\s+\d{1,2},?\s+\d{4})\b",
            RegexOptions.IgnoreCase);
        foreach (Match m in dateRx.Matches(document))
            fields.Add(("Date", m.Value, 0.91f));

        // Pattern 4 — phone numbers (strict — rejects PDF coordinate streams)
        var phoneRx = new Regex(
            @"(?<!\d)(\+?(?:\d{1,3}[\s\-.])?(\(?\d{3}\)?[\s\-.]){1,2}\d{3,4}[\s\-\.]\d{3,4})(?!\d)");
        foreach (Match m in phoneRx.Matches(document))
        {
            var raw    = m.Value.Trim();
            var digits = Regex.Replace(raw, @"\D", "");
            if (digits.Length is < 7 or > 15) continue;
            if (Regex.IsMatch(raw, @"\d\.\d")) continue;
            if (raw.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 4) continue;
            fields.Add(("Phone", raw, 0.85f));
        }

        // Pattern 5 — monetary amounts
        var amountRx = new Regex(
            @"(?:[$£€¥])\s*([\d,]+(?:\.\d{1,2})?)|(\d{1,3}(?:,\d{3})+(?:\.\d{1,2})?)\b",
            RegexOptions.IgnoreCase);
        foreach (Match m in amountRx.Matches(document))
            fields.Add(("Amount", m.Value.Trim(), 0.90f));

        // De-duplicate by label|value
        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = fields.Where(f => seen.Add($"{f.label}|{f.value}")).Take(30).ToList();

        var sb = new StringBuilder();
        foreach (var (label, value, conf) in unique)
            sb.AppendLine($"{label} | {value} | {conf:F2}");

        int wordCount = document.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        sb.AppendLine(
            $"ANALYSIS: Document contains {lines.Length} lines and {wordCount} words. " +
            $"{unique.Count} field{(unique.Count != 1 ? "s" : "")} extracted using built-in " +
            "pattern recognition. Connect a provisioned inference engine for deep semantic analysis.");

        // Stream word-by-word for a smooth UI animation
        foreach (var word in sb.ToString().Split(' '))
        {
            ct.ThrowIfCancellationRequested();
            yield return word + " ";
            await Task.Delay(18, ct);
        }
    }
}

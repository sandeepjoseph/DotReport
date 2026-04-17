using System.Globalization;
using System.Text.RegularExpressions;
using DotReport.Client.Models;

namespace DotReport.Client.Services;

/// <summary>
/// Scans all KnowledgeBase chunks for cross-document signals:
///   • Matching monetary amounts (reconciliation opportunities)
///   • Conflicting amounts sharing contextual labels (potential discrepancies)
///   • Date clusters linking multiple documents to the same event window
/// </summary>
public sealed class CrossDocIntelligenceService
{
    private static readonly Regex AmountRx = new(
        @"(?:[$£€¥])\s*([\d,]+(?:\.\d{1,2})?)|(\d{1,3}(?:,\d{3})+(?:\.\d{1,2})?|\d+\.\d{2})\b",
        RegexOptions.Compiled);

    private static readonly Regex DateRx = new(
        @"\b(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}|\d{4}[\/\-]\d{2}[\/\-]\d{2}|" +
        @"(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?\s+\d{1,2},?\s+\d{4})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] FinancialKeywords =
    {
        "total", "balance", "due", "amount", "subtotal",
        "net", "gross", "payment", "invoice", "sum",
    };

    public CrossDocReport Analyze(KnowledgeBase kb)
    {
        if (kb.Documents.Count < 2)
            return CrossDocReport.Empty;

        // ── Collect amounts per document ──────────────────────────────────────
        var docAmounts = new Dictionary<string, List<(decimal Amount, string Context)>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in kb.AllChunks)
        {
            if (!docAmounts.ContainsKey(chunk.DocumentName))
                docAmounts[chunk.DocumentName] = new();

            foreach (Match m in AmountRx.Matches(chunk.Text))
            {
                var raw = (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
                          .Replace(",", "");
                if (!decimal.TryParse(raw, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out decimal val) || val < 1m)
                    continue;

                docAmounts[chunk.DocumentName].Add(
                    (val, ExtractContext(chunk.Text, m.Index, 60)));
            }
        }

        // ── Cross-document amount comparison ──────────────────────────────────
        var matching      = new List<AmountMatch>();
        var discrepancies = new List<AmountMatch>();
        var docNames      = docAmounts.Keys.ToList();

        for (int i = 0; i < docNames.Count; i++)
        for (int j = i + 1; j < docNames.Count; j++)
        {
            var nameA = docNames[i];
            var nameB = docNames[j];

            foreach (var (amtA, ctxA) in docAmounts[nameA])
            foreach (var (amtB, ctxB) in docAmounts[nameB])
            {
                if (amtA == amtB)
                {
                    matching.Add(new AmountMatch(FormatAmount(amtA), amtA, nameA, nameB, ctxA, ctxB));
                }
                else if (AmountsAreRelated(ctxA, ctxB))
                {
                    discrepancies.Add(new AmountMatch(
                        $"{FormatAmount(amtA)} vs {FormatAmount(amtB)}", amtA,
                        nameA, nameB, ctxA, ctxB));
                }
            }
        }

        // ── Date clusters ─────────────────────────────────────────────────────
        var dateDocs = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in kb.AllChunks)
        {
            foreach (Match m in DateRx.Matches(chunk.Text))
            {
                var norm = NormalizeDate(m.Value);
                if (norm is null) continue;
                if (!dateDocs.ContainsKey(norm)) dateDocs[norm] = new();
                dateDocs[norm].Add(chunk.DocumentName);
            }
        }

        var clusters = dateDocs
            .Where(kv => kv.Value.Count >= 2)
            .Select(kv => new DateCluster(kv.Key, kv.Value.Order().ToList()))
            .OrderBy(c => c.NormalizedDate)
            .ToList();

        // ── Deduplicate results ───────────────────────────────────────────────
        var dedupMatches = matching
            .GroupBy(m => (m.Value, m.DocA, m.DocB))
            .Select(g => g.First())
            .Take(20)
            .ToList();

        var dedupDisc = discrepancies
            .GroupBy(d => (d.DocA, d.DocB, d.ContextA[..Math.Min(20, d.ContextA.Length)]))
            .Select(g => g.First())
            .Take(10)
            .ToList();

        return new CrossDocReport
        {
            MatchingAmounts = dedupMatches,
            Discrepancies   = dedupDisc,
            DateClusters    = clusters,
            Summary         = BuildSummary(dedupMatches, dedupDisc, clusters),
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ExtractContext(string text, int index, int radius)
    {
        int start = Math.Max(0, index - radius);
        int end   = Math.Min(text.Length, index + radius);
        return text[start..end].Replace('\n', ' ').Trim();
    }

    private static bool AmountsAreRelated(string ctxA, string ctxB)
    {
        foreach (var kw in FinancialKeywords)
            if (ctxA.Contains(kw, StringComparison.OrdinalIgnoreCase) &&
                ctxB.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static string FormatAmount(decimal value)
        => value >= 1_000_000 ? $"${value:N0}" : $"${value:N2}";

    private static string? NormalizeDate(string raw)
    {
        if (DateTime.TryParseExact(raw, new[]
            {
                "M/d/yyyy", "MM/dd/yyyy", "d/M/yyyy", "dd/MM/yyyy",
                "yyyy-MM-dd", "yyyy/MM/dd",
                "MMMM d, yyyy", "MMM d, yyyy", "MMM. d, yyyy",
                "MMMM d yyyy",  "MMM d yyyy",
            },
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt.ToString("yyyy-MM-dd");
        return null;
    }

    private static string BuildSummary(
        List<AmountMatch> matches,
        List<AmountMatch> discrepancies,
        List<DateCluster> clusters)
    {
        if (matches.Count == 0 && discrepancies.Count == 0 && clusters.Count == 0)
            return "No cross-document correlations detected.";

        var parts = new List<string>(3);
        if (matches.Count > 0)
            parts.Add($"{matches.Count} matching amount{(matches.Count != 1 ? "s" : "")}");
        if (discrepancies.Count > 0)
            parts.Add($"{discrepancies.Count} potential discrepanc{(discrepancies.Count != 1 ? "ies" : "y")}");
        if (clusters.Count > 0)
            parts.Add($"{clusters.Count} shared date cluster{(clusters.Count != 1 ? "s" : "")}");

        return string.Join(", ", parts) + " detected across documents.";
    }
}

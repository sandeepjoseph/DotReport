using System.Text.RegularExpressions;
using DotReport.Client.Models;

namespace DotReport.Client.Services;

/// <summary>
/// Session-scoped knowledge base.
/// Stores parsed documents as overlapping 600-char chunks.
/// BM25 full-text search surfaces the most relevant chunks for any query.
/// </summary>
public sealed class KnowledgeBase
{
    private const int  ChunkSize    = 600;
    private const int  ChunkOverlap = 120;
    private const double K1 = 1.5;
    private const double B  = 0.75;

    private readonly List<KnowledgeDocument>             _docs    = new();
    private readonly List<DocumentChunk>                 _chunks  = new();
    private readonly Dictionary<string, HashSet<string>> _index   =
        new(StringComparer.OrdinalIgnoreCase);

    public event Action? OnChanged;

    public IReadOnlyList<KnowledgeDocument> Documents => _docs;
    public IReadOnlyList<DocumentChunk>    AllChunks  => _chunks;
    public int TotalChunks => _chunks.Count;
    public bool HasDocuments => _docs.Count > 0;

    // ── Ingestion ────────────────────────────────────────────────────────────

    public KnowledgeDocument AddDocument(string fileName, string content)
    {
        var id     = Guid.NewGuid().ToString("N");
        var chunks = BuildChunks(id, fileName, content);

        var doc = new KnowledgeDocument
        {
            Id         = id,
            FileName   = fileName,
            FileType   = InferType(content),
            CharCount  = content.Length,
            ChunkCount = chunks.Count,
        };

        _docs.Add(doc);
        foreach (var c in chunks) { _chunks.Add(c); IndexChunk(c); }

        OnChanged?.Invoke();
        return doc;
    }

    public void RemoveDocument(string docId)
    {
        var gone = _chunks.Where(c => c.DocumentId == docId).ToList();
        foreach (var c in gone)
            foreach (var term in c.TermFrequencies.Keys)
                if (_index.TryGetValue(term, out var set))
                    set.Remove(c.Id);

        _chunks.RemoveAll(c => c.DocumentId == docId);
        _docs.RemoveAll(d => d.Id == docId);
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        _docs.Clear(); _chunks.Clear(); _index.Clear();
        OnChanged?.Invoke();
    }

    // ── Retrieval ────────────────────────────────────────────────────────────

    public List<SearchResult> Search(string query, int topN = 4)
    {
        if (_chunks.Count == 0) return new();
        var terms = Tokenize(query);
        return _chunks
            .Select(c => new SearchResult { Chunk = c, Score = Bm25(terms, c) })
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .Take(topN)
            .ToList();
    }

    /// <summary>
    /// Returns context text ready to inject into a system prompt.
    /// </summary>
    public string BuildContext(string query, int topN = 4)
    {
        var results = Search(query, topN);
        if (results.Count == 0)
            return "No relevant content found in the uploaded documents.";

        return string.Join("\n\n---\n\n", results.Select(r =>
            $"[Source: {r.Chunk.DocumentName}]\n{r.Chunk.Text}"));
    }

    /// <summary>
    /// Returns the unique document names referenced in a set of search results.
    /// </summary>
    public static List<string> SourcesFrom(IEnumerable<SearchResult> results)
        => results.Select(r => r.Chunk.DocumentName).Distinct().ToList();

    // ── BM25 internals ───────────────────────────────────────────────────────

    private void IndexChunk(DocumentChunk chunk)
    {
        foreach (var term in chunk.TermFrequencies.Keys)
        {
            if (!_index.TryGetValue(term, out var set))
                _index[term] = set = new HashSet<string>();
            set.Add(chunk.Id);
        }
    }

    private double Bm25(string[] terms, DocumentChunk chunk)
    {
        if (terms.Length == 0) return 0;
        double avgLen = _chunks.Average(c => (double)c.TermCount);
        double score  = 0;

        foreach (var term in terms.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!chunk.TermFrequencies.TryGetValue(term, out int tf)) continue;
            int df = _index.TryGetValue(term, out var docs) ? docs.Count : 0;
            double idf = Math.Log((_chunks.Count - df + 0.5) / (df + 0.5) + 1.0);
            double tfn = tf * (K1 + 1) / (tf + K1 * (1 - B + B * chunk.TermCount / avgLen));
            score += idf * tfn;
        }
        return score;
    }

    // ── Chunking ─────────────────────────────────────────────────────────────

    private static List<DocumentChunk> BuildChunks(string docId, string docName, string text)
    {
        var result = new List<DocumentChunk>();
        int start = 0, idx = 0;

        while (start < text.Length)
        {
            int end = Math.Min(start + ChunkSize, text.Length);

            // Prefer to break at a paragraph, sentence, or word boundary
            if (end < text.Length)
            {
                int br = text.LastIndexOfAny(new[] { '\n', '.', ' ' }, end,
                             Math.Min(100, end - start));
                if (br > start + ChunkSize / 3) end = br + 1;
            }

            var slice = text[start..end].Trim();
            if (slice.Length > 30)
            {
                var tokens = Tokenize(slice);
                var tf = tokens
                    .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

                result.Add(new DocumentChunk
                {
                    Id              = Guid.NewGuid().ToString("N"),
                    DocumentId      = docId,
                    DocumentName    = docName,
                    Text            = slice,
                    ChunkIndex      = idx++,
                    TermFrequencies = tf,
                    TermCount       = tokens.Length,
                });
            }
            start = Math.Max(end - ChunkOverlap, start + 1);
        }
        return result;
    }

    private static string[] Tokenize(string text)
        => Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9]+")
                .Where(t => t.Length > 2)
                .ToArray();

    // ── Document type inference ──────────────────────────────────────────────

    private static string InferType(string content)
    {
        var t = content.ToLowerInvariant();
        if (t.Contains("invoice") || t.Contains("bill to") || t.Contains("amount due"))
            return "Invoice";
        if ((t.Contains("debit") && t.Contains("credit")) || t.Contains("bank statement") || t.Contains("account balance"))
            return "Bank Statement";
        if (t.Contains("receipt") && (t.Contains("subtotal") || t.Contains("tax")))
            return "Receipt";
        if (t.Contains("ledger") || t.Contains("journal entry"))
            return "Ledger";
        if (t.Contains("contract") && t.Contains("parties"))
            return "Contract";
        if (t.Contains("columns:") || t.Contains("--- row "))
            return "Spreadsheet";
        return "Document";
    }
}

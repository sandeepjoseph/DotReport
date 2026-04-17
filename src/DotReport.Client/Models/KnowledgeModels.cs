namespace DotReport.Client.Models;

// ── Knowledge base documents ────────────────────────────────────────────────

public sealed class KnowledgeDocument
{
    public required string Id        { get; init; }
    public required string FileName  { get; init; }
    public string FileType  { get; init; } = "Document";
    public int    CharCount { get; init; }
    public int    ChunkCount { get; set; }
    public DateTimeOffset AddedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class DocumentChunk
{
    public required string Id           { get; init; }
    public required string DocumentId   { get; init; }
    public required string DocumentName { get; init; }
    public required string Text         { get; init; }
    public int ChunkIndex { get; init; }
    public Dictionary<string, int> TermFrequencies { get; init; } = new();
    public int TermCount { get; init; }
}

public sealed class SearchResult
{
    public required DocumentChunk Chunk { get; init; }
    public double Score { get; init; }
}

// ── Chat thread ─────────────────────────────────────────────────────────────

public enum ChatRole { User, Assistant }

public sealed class ChatMessage
{
    public required string   Id        { get; init; }
    public required ChatRole Role      { get; init; }
    public string Content   { get; set; } = string.Empty;
    public bool   IsStreaming { get; set; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public List<string> SourceDocuments  { get; init; } = new();
    public int          RetrievedFragments { get; set; } = -1; // -1 = no retrieval (user msg / system msg)
}

namespace DotReport.Client.Models;

public enum ReportSection { Summary, DataExtraction, Analysis, Recommendations }

public sealed class ReportDocument
{
    public required string DocumentId { get; init; }
    public required string SourceFileName { get; init; }
    public required string SourceContent { get; init; }
    public DateTimeOffset ProcessedAt { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<ReportSection, string> Sections { get; set; } = new();
    public List<ExtractedField> ExtractedFields { get; set; } = new();
    public ModelRole ProcessedBy { get; init; }
    public bool WasMergedOutput { get; init; }
    public string OutputPdfPath { get; set; } = string.Empty;
}

public sealed record ExtractedField
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public float Confidence { get; init; }
    public string FieldType { get; init; } = "text";
}

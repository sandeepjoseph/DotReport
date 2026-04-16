namespace DotReport.Shared.DTOs;

public sealed record InferenceResponseDto
{
    public required string RequestId { get; init; }
    public string Text { get; init; } = string.Empty;
    public string Status { get; init; } = "pending";
    public long TokenLatencyMs { get; init; }
    public int TokensGenerated { get; init; }
    public bool WasMerged { get; init; }
    public string? ErrorMessage { get; init; }
}

namespace DotReport.Client.Models;

public enum InferenceStatus { Pending, Streaming, Merging, Complete, Failed }

public sealed record InferenceResponse
{
    public required string RequestId { get; init; }
    public required ModelRole SourceModel { get; init; }
    public string Text { get; set; } = string.Empty;
    public InferenceStatus Status { get; set; } = InferenceStatus.Pending;
    public long TokenLatencyMs { get; set; }
    public int TokensGenerated { get; set; }
    public bool WasMerged { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public double TokensPerSecond =>
        TokensGenerated > 0 && TokenLatencyMs > 0
            ? TokensGenerated / (TokenLatencyMs / 1000.0)
            : 0;
}

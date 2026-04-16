namespace DotReport.Shared.DTOs;

/// <summary>Serializable inference request for cross-project sharing.</summary>
public sealed record InferenceRequestDto
{
    public required string RequestId { get; init; }
    public required string Prompt { get; init; }
    public required string SystemPrompt { get; init; }
    public int MaxTokens { get; init; } = 512;
    public float Temperature { get; init; } = 0.2f;
    public string ModelId { get; init; } = "phi4-mini-q4";
}

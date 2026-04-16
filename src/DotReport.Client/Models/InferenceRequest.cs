namespace DotReport.Client.Models;

public sealed record InferenceRequest
{
    public required string RequestId { get; init; }
    public required string Prompt { get; init; }
    public required string SystemPrompt { get; init; }
    public int MaxTokens { get; init; } = 512;
    public float Temperature { get; init; } = 0.2f;
    public ModelRole TargetModel { get; init; } = ModelRole.Primary;
    public bool StreamTokens { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

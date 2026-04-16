namespace DotReport.Client.Models;

public enum ModelRole { Primary, Backup }
public enum ModelStatus { Unloaded, Downloading, Cached, Loading, Ready, Error }
public enum BuildProfile { Standard, Lightweight }

public sealed record ModelConfig
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required ModelRole Role { get; init; }
    public required string OnnxManifestUrl { get; init; }
    public required long EstimatedVramMb { get; init; }
    public required int MaxTokens { get; init; }
    public required int ContextWindow { get; init; }
    public string Description { get; init; } = string.Empty;

    // Phi-4 Mini 4-bit ONNX — primary, complex logic
    public static readonly ModelConfig Phi4Mini = new()
    {
        Id = "phi4-mini-q4",
        DisplayName = "Phi-4 Mini",
        Role = ModelRole.Primary,
        OnnxManifestUrl = "models/phi4-mini-q4/manifest.json",
        EstimatedVramMb = 3800,
        MaxTokens = 512,
        ContextWindow = 4096,
        Description = "Primary inference engine — complex logic & report mapping"
    };

    // Qwen 2.5 1.5B 4-bit ONNX — backup, speed-optimized
    public static readonly ModelConfig Qwen25 = new()
    {
        Id = "qwen25-1b5-q4",
        DisplayName = "Qwen 2.5",
        Role = ModelRole.Backup,
        OnnxManifestUrl = "models/qwen25-1b5-q4/manifest.json",
        EstimatedVramMb = 1400,
        MaxTokens = 512,
        ContextWindow = 2048,
        Description = "Safety net — immediate UI feedback & low-VRAM execution"
    };
}

namespace DotReport.Client.Models;

public sealed record DeviceCapabilities
{
    public bool WebGpuSupported { get; init; }
    public bool WebAssemblySupported { get; init; }
    public long EstimatedVramMb { get; init; }
    public string GpuDescription { get; init; } = "Unknown";
    public string AdapterName { get; init; } = "Unknown";
    public bool HasSufficientVram => EstimatedVramMb >= 4096;
    public BuildProfile RecommendedBuild =>
        HasSufficientVram ? BuildProfile.Standard : BuildProfile.Lightweight;
    public string FallbackReason { get; init; } = string.Empty;
}

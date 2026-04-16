namespace DotReport.Shared.DTOs;

/// <summary>
/// Deserialized from manifest.json produced by the Python factory.
/// One manifest per model — lists all segments with URLs and checksums.
/// </summary>
public sealed class ModelManifestDto
{
    public string ModelId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<ModelSegmentDto> Segments { get; set; } = new();
}

public sealed class ModelSegmentDto
{
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

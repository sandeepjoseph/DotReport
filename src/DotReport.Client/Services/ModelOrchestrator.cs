using DotReport.Client.Interop;
using DotReport.Client.Models;

namespace DotReport.Client.Services;

/// <summary>
/// Manages the full model lifecycle:
/// pre-flight VRAM check → segment download → IndexedDB caching → ONNX load → stream inference.
/// Reports download progress per segment so the 3D Dodecahedron can assemble face by face. UAC 7.3.
/// </summary>
public sealed class ModelOrchestrator
{
    private readonly VRAMDetector _vram;
    private readonly IndexedDbService _db;
    private readonly OnnxInterop _onnx;

    public event Func<ModelConfig, int, Task>? OnSegmentCached;   // (model, faceIndex)
    public event Func<ModelConfig, ModelStatus, Task>? OnStatusChanged;

    private readonly Dictionary<string, ModelStatus> _statuses = new();

    public ModelOrchestrator(VRAMDetector vram, IndexedDbService db, OnnxInterop onnx)
    {
        _vram = vram;
        _db   = db;
        _onnx = onnx;
    }

    /// <summary>
    /// Pre-flight: detect VRAM, pick build profile, return capabilities. UAC 7.1.
    /// </summary>
    public async Task<DeviceCapabilities> RunPreFlightAsync()
        => await _vram.DetectAsync();

    /// <summary>
    /// Downloads model segments into IndexedDB.
    /// Fires OnSegmentCached for each face (12 total) so the UI can animate assembly.
    /// </summary>
    public async Task ProvisionModelAsync(ModelConfig config, HttpClient http, CancellationToken ct = default)
    {
        SetStatus(config, ModelStatus.Downloading);

        var manifest = await http.GetFromJsonAsync<ModelManifest>(config.OnnxManifestUrl, ct)
            ?? throw new InvalidOperationException($"Manifest not found: {config.OnnxManifestUrl}");

        int segmentCount = manifest.Segments.Count;
        int faceStep = ProxyState.TotalDodecahedronFaces / Math.Max(segmentCount, 1);

        for (int i = 0; i < manifest.Segments.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var seg = manifest.Segments[i];

            // Skip if already cached
            bool exists = await _db.ExistsAsync($"{config.Id}/{seg.FileName}");
            if (!exists)
            {
                var bytes = await http.GetByteArrayAsync(seg.Url, ct);
                await _db.StoreAsync($"{config.Id}/{seg.FileName}", bytes);
            }

            int faceIndex = (i + 1) * faceStep;
            if (OnSegmentCached != null)
                await OnSegmentCached.Invoke(config, Math.Min(faceIndex, ProxyState.TotalDodecahedronFaces));
        }

        SetStatus(config, ModelStatus.Cached);
    }

    /// <summary>
    /// Loads a cached ONNX model into the ONNX Runtime Web session.
    /// </summary>
    public async Task LoadModelAsync(ModelConfig config)
    {
        SetStatus(config, ModelStatus.Loading);
        await _onnx.LoadModelAsync(config.Id);
        SetStatus(config, ModelStatus.Ready);
    }

    /// <summary>
    /// Streams inference tokens from the specified model.
    /// </summary>
    public async IAsyncEnumerable<string> StreamAsync(
        InferenceRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var modelId = request.TargetModel == ModelRole.Primary
            ? ModelConfig.Phi4Mini.Id
            : ModelConfig.Qwen25.Id;

        await foreach (var token in _onnx.StreamTokensAsync(modelId, request.Prompt, request.SystemPrompt,
                           request.MaxTokens, request.Temperature, ct))
        {
            yield return token;
        }
    }

    public ModelStatus GetStatus(ModelConfig config)
        => _statuses.TryGetValue(config.Id, out var s) ? s : ModelStatus.Unloaded;

    private void SetStatus(ModelConfig config, ModelStatus status)
    {
        _statuses[config.Id] = status;
        OnStatusChanged?.Invoke(config, status);
    }
}

internal sealed class ModelManifest
{
    public string ModelId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<ModelSegment> Segments { get; set; } = new();
}

internal sealed class ModelSegment
{
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

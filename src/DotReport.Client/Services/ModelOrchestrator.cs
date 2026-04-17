using System.Net.Http.Json;
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
    /// Skipped gracefully when no model file exists in IndexedDB (demo / stub mode).
    /// </summary>
    public async Task LoadModelAsync(ModelConfig config)
    {
        SetStatus(config, ModelStatus.Loading);
        bool exists = await _db.ExistsAsync($"{config.Id}/model.onnx");
        if (exists)
            await _onnx.LoadModelAsync(config.Id);
        SetStatus(config, ModelStatus.Ready);
    }

    /// <summary>
    /// Streams inference tokens from the specified model.
    /// Falls back to built-in rule-based extraction when no ONNX session is loaded.
    /// </summary>
    public async IAsyncEnumerable<string> StreamAsync(
        InferenceRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var modelId = request.TargetModel == ModelRole.Primary
            ? ModelConfig.Phi4Mini.Id
            : ModelConfig.Qwen25.Id;

        // Use ONNX only if the model file was actually loaded into a JS session
        bool modelReady = _onnx.IsModelLoaded(modelId);

        if (modelReady)
        {
            await foreach (var token in _onnx.StreamTokensAsync(modelId, request.Prompt, request.SystemPrompt,
                               request.MaxTokens, request.Temperature, ct))
                yield return token;
        }
        else
        {
            // Built-in rule-based extraction — runs entirely in .NET, no model needed
            await foreach (var token in ExtractFieldsAsync(request.Prompt, ct))
                yield return token;
        }
    }

    /// <summary>
    /// Rule-based document field extractor used as fallback when no ONNX model is loaded.
    /// Scans the document for common key:value patterns and named entities.
    /// </summary>
    private static async IAsyncEnumerable<string> ExtractFieldsAsync(
        string document,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var lines = document.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var fields = new List<(string label, string value, float confidence)>();

        // Pattern 1 — explicit "Key: Value" lines
        // Value must be printable text (≥ 90 % ASCII-printable chars) to skip binary garbage.
        var kvPattern = new System.Text.RegularExpressions.Regex(
            @"^([A-Za-z][A-Za-z0-9 _\-]{1,40})\s*[:=]\s*(.+)$");

        foreach (var line in lines)
        {
            var m = kvPattern.Match(line.Trim());
            if (!m.Success) continue;
            var label = m.Groups[1].Value.Trim();
            var value = m.Groups[2].Value.Trim();
            if (value.Length is < 1 or > 200) continue;
            // Reject values that contain non-printable / binary characters
            int printable = value.Count(c => c >= 32 && c < 127);
            if ((double)printable / value.Length < 0.90) continue;
            fields.Add((label, value, 0.88f));
        }

        // Pattern 2 — email addresses
        var emailPattern = new System.Text.RegularExpressions.Regex(
            @"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b");
        foreach (System.Text.RegularExpressions.Match m in emailPattern.Matches(document))
            fields.Add(("Email", m.Value, 0.95f));

        // Pattern 3 — dates
        var datePattern = new System.Text.RegularExpressions.Regex(
            @"\b(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}|\d{4}[\/\-]\d{2}[\/\-]\d{2}|(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?\s+\d{1,2},?\s+\d{4})\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match m in datePattern.Matches(document))
            fields.Add(("Date", m.Value, 0.91f));

        // Pattern 4 — phone numbers (strict format to avoid matching PDF coordinates).
        // Accepts: +1-800-555-1234  (555) 123-4567  555.123.4567  +44 7700 900000
        // Rejects: coordinate sequences like "393.75 0 0 -393.75", long timestamps, etc.
        var phonePattern = new System.Text.RegularExpressions.Regex(
            @"(?<!\d)(\+?(?:\d{1,3}[\s\-.])?(\(?\d{3}\)?[\s\-.]){1,2}\d{3,4}[\s\-\.]\d{3,4})(?!\d)");
        foreach (System.Text.RegularExpressions.Match m in phonePattern.Matches(document))
        {
            var raw    = m.Value.Trim();
            var digits = System.Text.RegularExpressions.Regex.Replace(raw, @"\D", "");
            if (digits.Length is < 7 or > 15) continue;
            // Reject if the match contains decimal numbers (e.g. "393.75") — those are coordinates
            if (System.Text.RegularExpressions.Regex.IsMatch(raw, @"\d\.\d")) continue;
            // Reject if too many whitespace-separated tokens (coordinate streams have 4–8 parts)
            if (raw.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 4) continue;
            fields.Add(("Phone", raw, 0.85f));
        }

        // Remove duplicates by label|value pair to preserve tabular data with same value under different columns
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = fields.Where(f => seen.Add($"{f.label}|{f.value}")).Take(30).ToList();

        // Emit in the format ParseInferenceOutput expects: "LABEL | value | confidence\n"
        var sb = new System.Text.StringBuilder();
        foreach (var (label, value, conf) in unique)
            sb.AppendLine($"{label} | {value} | {conf:F2}");

        // Summary line
        int wordCount = document.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        int lineCount = lines.Length;
        sb.AppendLine($"ANALYSIS: Document contains {lineCount} lines and {wordCount} words. " +
                      $"{unique.Count} field{(unique.Count != 1 ? "s" : "")} extracted using built-in pattern recognition. " +
                      "Connect a provisioned inference engine for deep semantic analysis.");

        // Stream tokens word-by-word so the UI animates
        foreach (var word in sb.ToString().Split(' '))
        {
            ct.ThrowIfCancellationRequested();
            yield return word + " ";
            await Task.Delay(18, ct); // ~55 tokens/sec — smooth streaming feel
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

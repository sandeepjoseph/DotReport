using DotReport.Client.Models;

namespace DotReport.Client.Services;

/// <summary>
/// Pre-Warm Service — loads models into VRAM while the user is still
/// selecting their letterhead / configuring the report template.
/// From Test Report Recommendation #1: "Pre-Warm setting in the 3D UI."
/// Eliminates first-inference cold-start latency.
/// </summary>
public sealed class ModelWarmupService
{
    private readonly ModelOrchestrator _orchestrator;
    private readonly ProxyState _proxyState;

    // Minimal warm-up prompt — forces the tokenizer + first layer to load
    private const string WarmupPrompt =
        "<|system|>\nYou are ready.\n<|user|>\nReady?\n<|assistant|>\nReady.";

    public bool IsPrimaryWarmed  { get; private set; }
    public bool IsBackupWarmed   { get; private set; }
    public event Action? OnWarmupComplete;

    public ModelWarmupService(ModelOrchestrator orchestrator, ConsolidatorProxy proxy)
    {
        _orchestrator = orchestrator;
        _proxyState   = proxy.State;
    }

    /// <summary>
    /// Fires a silent single-token pass on both models to populate VRAM caches.
    /// Called automatically once provisioning completes — runs in background.
    /// </summary>
    public async Task WarmupAsync(CancellationToken ct = default)
    {
        var tasks = new List<Task>();

        if (_proxyState.PrimaryStatus == ModelStatus.Ready)
            tasks.Add(WarmModelAsync(ModelConfig.Phi4Mini, ct)
                .ContinueWith(_ => IsPrimaryWarmed = true, ct));

        if (_proxyState.BackupStatus == ModelStatus.Ready)
            tasks.Add(WarmModelAsync(ModelConfig.Qwen25, ct)
                .ContinueWith(_ => IsBackupWarmed = true, ct));

        await Task.WhenAll(tasks);
        OnWarmupComplete?.Invoke();
    }

    private async Task WarmModelAsync(ModelConfig config, CancellationToken ct)
    {
        var warmupRequest = new InferenceRequest
        {
            RequestId    = $"warmup-{config.Id}",
            Prompt       = WarmupPrompt,
            SystemPrompt = string.Empty,
            MaxTokens    = 1,           // single token — just pre-populates KV cache
            Temperature  = 0f,
            TargetModel  = config.Role,
            StreamTokens = false
        };

        // Consume the single token and discard — side effect is the warm cache
        await foreach (var _ in _orchestrator.StreamAsync(warmupRequest, ct)) { break; }
    }
}

using DotReport.Client.Models;

namespace DotReport.Client.Services;

/// <summary>
/// The Consolidator Proxy — manages the dual-model lifecycle.
/// Runs Primary (Phi-4 Mini) and Backup (Qwen 2.5) concurrently.
/// If Primary exceeds 500ms token-latency, Backup output is merged
/// so the user never encounters a "Thinking" hang. UAC 7.2.
/// </summary>
public sealed class ConsolidatorProxy
{
    private const int LatencyThresholdMs = 500;

    private readonly ModelOrchestrator _orchestrator;
    private readonly ProxyState _state;

    public ProxyState State => _state;
    public event Action? OnStateChanged;

    public ConsolidatorProxy(ModelOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        _state = new ProxyState();
    }

    /// <summary>
    /// Fires both models concurrently. Returns a merged stream of tokens.
    /// Backup provides immediate feedback; Primary refines the final output.
    /// </summary>
    public async IAsyncEnumerable<string> InferAsync(
        string userContent,
        string systemPrompt,
        CancellationToken ct = default)
    {
        _state.IsProcessing = true;
        NotifyStateChanged();

        var requestId = Guid.NewGuid().ToString("N");
        var primaryRequest = BuildRequest(requestId + "-p", userContent, systemPrompt, ModelRole.Primary);
        var backupRequest  = BuildRequest(requestId + "-b", userContent, systemPrompt, ModelRole.Backup);

        using var primaryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var backupCts  = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Channel-based merge: whichever produces tokens first wins immediate display.
        var channel = System.Threading.Channels.Channel.CreateUnbounded<string>();
        var primaryResponse = new InferenceResponse { RequestId = primaryRequest.RequestId, SourceModel = ModelRole.Primary };
        var backupResponse  = new InferenceResponse { RequestId = backupRequest.RequestId,  SourceModel = ModelRole.Backup  };

        var primaryTask = RunModelAsync(primaryRequest, primaryResponse, channel.Writer, primaryCts.Token);
        var backupTask  = RunModelAsync(backupRequest,  backupResponse,  channel.Writer, backupCts.Token);

        // Latency watchdog: if primary stalls, merge backup immediately.
        var watchdogTask = Task.Run(async () =>
        {
            await Task.Delay(LatencyThresholdMs, ct);
            if (primaryResponse.Status == InferenceStatus.Pending ||
                primaryResponse.Status == InferenceStatus.Streaming &&
                primaryResponse.TokenLatencyMs > LatencyThresholdMs)
            {
                _state.LastPrimaryLatencyMs = primaryResponse.TokenLatencyMs;
                primaryResponse.WasMerged = true;
                NotifyStateChanged();
            }
        }, ct);

        _ = Task.WhenAll(primaryTask, backupTask, watchdogTask)
               .ContinueWith(_ => channel.Writer.TryComplete(), ct);

        await foreach (var token in channel.Reader.ReadAllAsync(ct))
            yield return token;

        _state.IsProcessing = false;
        NotifyStateChanged();
    }

    private async Task RunModelAsync(
        InferenceRequest request,
        InferenceResponse response,
        System.Threading.Channels.ChannelWriter<string> writer,
        CancellationToken ct)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            response.Status = InferenceStatus.Streaming;

            await foreach (var token in _orchestrator.StreamAsync(request, ct))
            {
                response.TokenLatencyMs = sw.ElapsedMilliseconds;
                response.TokensGenerated++;
                response.Text += token;

                // Only write to channel if this is the active source
                if (!response.WasMerged)
                    await writer.WriteAsync(token, ct);
            }

            sw.Stop();
            response.Status = InferenceStatus.Complete;
            response.CompletedAt = DateTimeOffset.UtcNow;
        }
        catch (OperationCanceledException) { /* graceful cancel */ }
        catch (Exception ex)
        {
            response.Status = InferenceStatus.Failed;
            response.ErrorMessage = ex.Message;
        }
    }

    private static InferenceRequest BuildRequest(
        string id, string content, string system, ModelRole role) => new()
    {
        RequestId = id,
        Prompt = content,
        SystemPrompt = system,
        TargetModel = role,
        StreamTokens = true,
        MaxTokens = 512,
        Temperature = role == ModelRole.Primary ? 0.1f : 0.3f
    };

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}

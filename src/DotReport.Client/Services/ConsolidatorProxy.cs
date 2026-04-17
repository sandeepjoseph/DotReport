using System.Runtime.CompilerServices;
using DotReport.Client.Models;
using DotReport.Client.Services.Inference;

namespace DotReport.Client.Services;

/// <summary>
/// Inference entry point for the application.
/// Delegates to InferenceCircuitBreaker which tries backends in tier order:
///   Tier1 → Cloud LLM via server proxy (highest quality)
///   Tier2 → Local ONNX model (requires provisioning)
///   Tier3 → Built-in rule engine (always available — guaranteed)
/// ProxyState is preserved for backward compatibility with Provision and Report pages.
/// </summary>
public sealed class ConsolidatorProxy
{
    private readonly InferenceCircuitBreaker _breaker;
    private readonly ProxyState _state;

    public ProxyState State => _state;
    public event Action? OnStateChanged;

    public ConsolidatorProxy(InferenceCircuitBreaker breaker)
    {
        _breaker = breaker;
        _state   = new ProxyState();
    }

    public async IAsyncEnumerable<string> InferAsync(
        string userContent,
        string systemPrompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _state.IsProcessing = true;
        NotifyStateChanged();

        var request = new BackendRequest(userContent, systemPrompt);

        try
        {
            await foreach (var token in _breaker.StreamAsync(request, ct))
                yield return token;
        }
        finally
        {
            _state.IsProcessing     = false;
            _state.LastPrimaryLatencyMs = 0;
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}

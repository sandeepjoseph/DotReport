using Microsoft.JSInterop;

namespace DotReport.Client.Interop;

/// <summary>
/// C# bridge to ONNX Runtime Web.
/// Handles WebGPU-accelerated inference with WASM fallback. UAC 7.1.
/// All execution is client-side — no server round-trips. UAC 7.5.
/// </summary>
public sealed class OnnxInterop(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private readonly HashSet<string> _loadedModels = new();

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>(
            "import", "./js/onnx-runner.js");
        return _module;
    }

    /// <summary>
    /// Loads an ONNX model from IndexedDB into a runtime session.
    /// Prefers WebGPU backend; falls back to WASM. UAC 7.1.
    /// </summary>
    public async Task LoadModelAsync(string modelId)
    {
        if (_loadedModels.Contains(modelId)) return;
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("loadModel", modelId);
        _loadedModels.Add(modelId);
    }

    /// <summary>
    /// Streams inference tokens from the loaded ONNX session.
    /// Uses a JS-backed async iterator via a callback channel.
    /// </summary>
    public async IAsyncEnumerable<string> StreamTokensAsync(
        string modelId,
        string userPrompt,
        string systemPrompt,
        int maxTokens,
        float temperature,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var m = await GetModuleAsync();
        var channel = System.Threading.Channels.Channel.CreateUnbounded<string>();

        // DotNet reference so JS can push tokens back
        var dotNetRef = DotNetObjectReference.Create(
            new TokenReceiver(channel.Writer));

        await m.InvokeVoidAsync("startInference",
            modelId, systemPrompt, userPrompt, maxTokens, temperature, dotNetRef);

        await foreach (var token in channel.Reader.ReadAllAsync(ct))
        {
            if (token == "[EOS]") break;
            yield return token;
        }

        dotNetRef.Dispose();
    }

    public bool IsModelLoaded(string modelId) => _loadedModels.Contains(modelId);

    public async Task UnloadModelAsync(string modelId)
    {
        if (!_loadedModels.Contains(modelId)) return;
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("unloadModel", modelId);
        _loadedModels.Remove(modelId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            foreach (var id in _loadedModels)
                await _module.InvokeVoidAsync("unloadModel", id);
            await _module.DisposeAsync();
        }
    }

    // Receives tokens from JS and pushes them into the channel
    private sealed class TokenReceiver(System.Threading.Channels.ChannelWriter<string> writer)
    {
        [JSInvokable]
        public void ReceiveToken(string token) => writer.TryWrite(token);

        [JSInvokable]
        public void ReceiveComplete() => writer.TryWrite("[EOS]");

        [JSInvokable]
        public void ReceiveError(string error)
        {
            writer.TryComplete(new Exception(error));
        }
    }
}

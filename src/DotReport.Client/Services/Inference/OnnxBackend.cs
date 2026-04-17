using System.Runtime.CompilerServices;
using DotReport.Client.Interop;
using DotReport.Client.Models;

namespace DotReport.Client.Services.Inference;

/// <summary>
/// Tier-2: ONNX Runtime Web backend.
/// Available only when at least one model has been provisioned and loaded via
/// the Provision page. Prefers Phi-4 Mini; falls back to Qwen 2.5.
/// </summary>
public sealed class OnnxBackend : IInferenceBackend
{
    private readonly OnnxInterop _onnx;

    public BackendTier Tier       => BackendTier.Tier2_Onnx;
    public string      Name       => "ONNX Runtime (Local)";
    public bool        IsReadyNow =>
        _onnx.IsModelLoaded(ModelConfig.Phi4Mini.Id) ||
        _onnx.IsModelLoaded(ModelConfig.Qwen25.Id);

    public OnnxBackend(OnnxInterop onnx) => _onnx = onnx;

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => Task.FromResult(IsReadyNow);

    public async IAsyncEnumerable<string> StreamAsync(
        BackendRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var modelId = _onnx.IsModelLoaded(ModelConfig.Phi4Mini.Id)
            ? ModelConfig.Phi4Mini.Id
            : ModelConfig.Qwen25.Id;

        await foreach (var token in _onnx.StreamTokensAsync(
            modelId, request.Prompt, request.SystemPrompt,
            request.MaxTokens, request.Temperature, ct))
            yield return token;
    }
}

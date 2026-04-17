namespace DotReport.Client.Services.Inference;

public enum BackendTier
{
    Tier1_Server = 1,   // Cloud LLM via DotReport.Server proxy — highest quality
    Tier2_Onnx   = 2,   // ONNX Runtime Web — local model, requires provisioning
    Tier3_Rules  = 3,   // Built-in pattern engine — always available, guaranteed
}

public sealed record BackendRequest(
    string Prompt,
    string SystemPrompt,
    int    MaxTokens   = 512,
    float  Temperature = 0.1f);

/// <summary>Snapshot of an agent's status for UI display.</summary>
public sealed record AgentInfo(
    string      AgentLabel,   // "AI Agent 1" / "AI Agent 2" / "AI Agent 3"
    string      BackendName,  // human-readable backend description
    BackendTier Tier,
    bool        IsReady,      // synchronous readiness — no network call
    bool        IsActive);    // currently handling inference

public interface IInferenceBackend
{
    BackendTier Tier { get; }
    string      Name { get; }

    /// <summary>Synchronous readiness check for UI display — no network call.</summary>
    bool IsReadyNow { get; }

    /// <summary>Returns true if this backend can accept requests right now.</summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>Streams inference tokens for the given request.</summary>
    IAsyncEnumerable<string> StreamAsync(
        BackendRequest request,
        CancellationToken ct = default);
}

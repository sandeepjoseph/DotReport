using System.Runtime.CompilerServices;

namespace DotReport.Client.Services.Inference;

/// <summary>
/// Tries inference backends in ascending tier order (Tier1 → Tier2 → Tier3).
/// Each backend has an independent circuit breaker with decorrelated jitter backoff.
/// Tier3 (rule engine) is the guaranteed last resort — requests never fail completely.
/// </summary>
public sealed class InferenceCircuitBreaker
{
    private enum CircuitState { Closed, Open, HalfOpen }

    private sealed class BreakerEntry
    {
        public CircuitState State        = CircuitState.Closed;
        public int          Failures;
        public long         RetryAfterMs;              // Environment.TickCount64 threshold
        public long         LastSleepMs  = BaseMs;
    }

    private const int FailureThreshold = 3;
    private const int BaseMs           = 2_000;
    private const int CapMs            = 60_000;

    private readonly IReadOnlyList<IInferenceBackend>      _backends;
    private readonly Dictionary<BackendTier, BreakerEntry> _breakers = new();

    /// <summary>The tier that produced the last (or current) response.</summary>
    public BackendTier ActiveTier { get; private set; } = BackendTier.Tier3_Rules;
    public string      ActiveName { get; private set; } = string.Empty;

    /// <summary>
    /// Returns display info for all registered agents — used by the UI status bar.
    /// </summary>
    public IReadOnlyList<AgentInfo> GetAgentInfos() =>
        _backends.Select((b, i) => new AgentInfo(
            AgentLabel : $"AI Agent {i + 1}",
            BackendName: b.Name,
            Tier       : b.Tier,
            IsReady    : b.IsReadyNow,
            IsActive   : b.Tier == ActiveTier
        )).ToList();

    public InferenceCircuitBreaker(IEnumerable<IInferenceBackend> backends)
    {
        _backends = backends.OrderBy(b => (int)b.Tier).ToList();
        foreach (var b in _backends)
            _breakers[b.Tier] = new BreakerEntry();
    }

    public async IAsyncEnumerable<string> StreamAsync(
        BackendRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var backend = await SelectBackendAsync(ct);
        var entry   = _breakers[backend.Tier];
        ActiveTier  = backend.Tier;
        ActiveName  = backend.Name;

        // Use a channel so we can catch exceptions after yield has started
        var channel = System.Threading.Channels.Channel.CreateUnbounded<string>();

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var token in backend.StreamAsync(request, ct))
                    await channel.Writer.WriteAsync(token, ct);

                OnSuccess(entry);
                channel.Writer.TryComplete();
            }
            catch (OperationCanceledException)
            {
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                OnFailure(entry);
                channel.Writer.TryComplete(ex);
            }
        }, ct);

        await foreach (var token in channel.Reader.ReadAllAsync(ct))
            yield return token;
    }

    // ── Backend selection ─────────────────────────────────────────────────────

    private async Task<IInferenceBackend> SelectBackendAsync(CancellationToken ct)
    {
        foreach (var b in _backends)
        {
            var entry = _breakers[b.Tier];

            if (entry.State == CircuitState.Open)
            {
                if (Environment.TickCount64 < entry.RetryAfterMs) continue;
                entry.State = CircuitState.HalfOpen;   // attempt recovery probe
            }

            if (await b.IsAvailableAsync(ct))
                return b;

            // IsAvailableAsync returned false while in HalfOpen → re-open
            if (entry.State == CircuitState.HalfOpen)
                OpenCircuit(entry);
        }

        // Tier3 guaranteed — always return the last backend
        return _backends[^1];
    }

    // ── State transitions ─────────────────────────────────────────────────────

    private static void OnSuccess(BreakerEntry entry)
    {
        entry.Failures    = 0;
        entry.LastSleepMs = BaseMs;
        entry.State       = CircuitState.Closed;
    }

    private static void OnFailure(BreakerEntry entry)
    {
        entry.Failures++;
        if (entry.Failures >= FailureThreshold)
            OpenCircuit(entry);
    }

    private static void OpenCircuit(BreakerEntry entry)
    {
        // Decorrelated jitter: sleep = min(cap, rand(base, prev * 3))
        long next          = Math.Min(CapMs, Random.Shared.NextInt64(BaseMs, entry.LastSleepMs * 3 + 1));
        entry.LastSleepMs  = next;
        entry.RetryAfterMs = Environment.TickCount64 + next;
        entry.State        = CircuitState.Open;
    }
}

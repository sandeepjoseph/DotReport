using System.Net.Http.Json;
using System.Runtime.CompilerServices;

namespace DotReport.Client.Services.Inference;

/// <summary>
/// Tier-1: Cloud LLM accessed via the DotReport.Server reverse proxy.
/// Streams tokens over Server-Sent Events (SSE).
/// Falls back transparently via the circuit breaker when the server is not deployed.
/// </summary>
public sealed class ServerProxyBackend : IInferenceBackend
{
    private readonly HttpClient _http;
    private const string HealthPath = "api/inference/health";
    private const string StreamPath = "api/inference/stream";

    public BackendTier Tier       => BackendTier.Tier1_Server;
    public string      Name       => "Cloud LLM (Server Proxy)";
    public bool        IsReadyNow => _lastAvailable;

    private bool _lastAvailable;

    public ServerProxyBackend(HttpClient http) => _http = http;

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync(
                HealthPath, HttpCompletionOption.ResponseHeadersRead, ct);
            // Must be JSON — a 200 returning index.html (SPA fallback) is not a real proxy
            var ct2 = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;
            _lastAvailable = resp.IsSuccessStatusCode &&
                             ct2.Contains("json", StringComparison.OrdinalIgnoreCase);
        }
        catch { _lastAvailable = false; }
        return _lastAvailable;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        BackendRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new
        {
            request.Prompt,
            request.SystemPrompt,
            request.MaxTokens,
            request.Temperature,
        };

        using var resp = await _http.PostAsJsonAsync(StreamPath, payload, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line[5..].Trim();
            if (data == "[DONE]") break;
            if (!string.IsNullOrEmpty(data))
                yield return data;
        }
    }
}

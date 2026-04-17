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
            // Cache-bust so stale browser/proxy caches don't hide the real content-type
            var url = $"{HealthPath}?_={Environment.TickCount64}";
            using var req  = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            // Must return application/json — any other type (e.g. text/html SPA fallback) means
            // there is no real server proxy, and we must not attempt POST /api/inference/stream.
            var mediaType = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;
            _lastAvailable = resp.IsSuccessStatusCode &&
                             mediaType.Contains("json", StringComparison.OrdinalIgnoreCase);
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

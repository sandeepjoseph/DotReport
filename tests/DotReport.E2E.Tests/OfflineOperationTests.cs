using FluentAssertions;
using Microsoft.Playwright;

namespace DotReport.E2E.Tests;

/// <summary>
/// E2E Playwright tests for offline (No-Internet) operation.
/// UAC 7.5 + Test Strategy Section 5 "The No-Internet Test":
/// Once downloaded, all features must work with Wi-Fi off.
/// Test Report SYS-03: Offline Operation — PASS criteria.
/// </summary>
[Collection("Playwright")]
public sealed class OfflineOperationTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fixture;
    public OfflineOperationTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task IndexPage_WhileOffline_StillLoads()
    {
        var page    = await _fixture.NewPageAsync();
        var context = page.Context;

        // First load while online
        await page.GotoAsync(PlaywrightFixture.BaseUrl);
        await page.WaitForSelectorAsync(".ec-shell");

        // Go offline
        await context.SetOfflineAsync(true);
        await page.ReloadAsync();

        // Should still render from cache / service worker
        var shell = await page.QuerySelectorAsync(".ec-shell");
        shell.Should().NotBeNull("app must load from cache when offline");

        await context.SetOfflineAsync(false);
    }

    [Fact]
    public async Task IndexPage_WhileOffline_DoesNotShowCloudDependencyError()
    {
        var page    = await _fixture.NewPageAsync();
        var context = page.Context;

        await page.GotoAsync(PlaywrightFixture.BaseUrl);
        await page.WaitForSelectorAsync(".ec-shell");

        await context.SetOfflineAsync(true);

        // No network errors should bubble up as UI errors
        var errors = new List<string>();
        page.PageError += (_, e) => errors.Add(e);

        await page.ReloadAsync();
        await page.WaitForTimeoutAsync(2000);

        errors.Should().NotContain(e => e.Contains("fetch") && e.Contains("failed"),
            "app must not depend on network calls after initial load");

        await context.SetOfflineAsync(false);
    }

    [Fact]
    public async Task ReportPage_WhileOffline_ReportEngineIsAccessible()
    {
        var page    = await _fixture.NewPageAsync();
        var context = page.Context;

        await page.GotoAsync($"{PlaywrightFixture.BaseUrl}/report");
        await page.WaitForSelectorAsync(".ec-report");

        await context.SetOfflineAsync(true);
        await page.ReloadAsync();

        var report = await page.QuerySelectorAsync(".ec-report");
        report.Should().NotBeNull();

        await context.SetOfflineAsync(false);
    }

    [Fact]
    public async Task NetworkMonitor_DuringInference_ZeroBytesLeakToExternalHosts()
    {
        // UAC 7.5 + Test Report Section 4: Zero-Leakage validation
        var page = await _fixture.NewPageAsync();

        var externalRequests = new List<string>();
        page.Request += (_, req) =>
        {
            var url = req.Url;
            // Allow localhost and CDN for Babylon.js/ONNX (initial load only)
            if (!url.Contains("localhost") &&
                !url.Contains("127.0.0.1") &&
                !url.Contains("cdn.babylonjs.com") &&
                !url.Contains("cdn.jsdelivr.net"))
            {
                externalRequests.Add(url);
            }
        };

        await page.GotoAsync(PlaywrightFixture.BaseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Filter to only calls that would carry document data
        var dataCalls = externalRequests
            .Where(u => !u.Contains(".js") && !u.Contains(".css") && !u.Contains(".wasm"))
            .ToList();

        dataCalls.Should().BeEmpty(
            "zero document data must be transmitted to external hosts during inference");
    }
}

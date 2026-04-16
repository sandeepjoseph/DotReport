using FluentAssertions;
using Microsoft.Playwright;

namespace DotReport.E2E.Tests;

/// <summary>
/// E2E Playwright tests for the 3D Provisioning flow.
/// UAC 7.3: Dodecahedron assembly, IndexedDB caching, Play button gate.
/// Test Strategy Section 3: "Delete IndexedDB cache → Refresh → Verify 3D assembly triggers."
/// </summary>
[Collection("Playwright")]
public sealed class ProvisioningFlowTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fixture;
    public ProvisioningFlowTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ProvisionPage_Loads_ShowsBeginProvisioningButton()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{PlaywrightFixture.BaseUrl}/provision");

        var btn = page.Locator("button:has-text('BEGIN PROVISIONING')");
        await btn.WaitForAsync(new LocatorWaitForOptions
        {
            Timeout = 10_000,
            State   = WaitForSelectorState.Visible
        });

        (await btn.IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task ProvisionPage_FaceCounter_StartsAtZero()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{PlaywrightFixture.BaseUrl}/provision");

        await page.WaitForSelectorAsync(".ec-face-counter");
        var text = await page.TextContentAsync(".ec-face-counter");

        text.Should().StartWith("0");
    }

    [Fact]
    public async Task ProvisionPage_PlayButton_DisabledBeforeAssembly()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{PlaywrightFixture.BaseUrl}/provision");

        // The "LOCKED & READY" button must NOT be visible before provisioning
        var readyBtn = page.Locator("button:has-text('LOCKED')");
        var count    = await readyBtn.CountAsync();

        count.Should().Be(0, "Play button must not exist before dodecahedron is assembled");
    }

    [Fact]
    public async Task ProvisionPage_AfterClearingIndexedDb_ReShowsProvisionButton()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{PlaywrightFixture.BaseUrl}/provision");

        // Clear IndexedDB to simulate a fresh machine
        await page.EvaluateAsync(@"
            indexedDB.deleteDatabase('dotreport-edgecore');
        ");
        await page.ReloadAsync();

        var btn = page.Locator("button:has-text('BEGIN PROVISIONING')");
        await btn.WaitForAsync(new LocatorWaitForOptions { Timeout = 8_000 });

        (await btn.IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task ProvisionPage_DodecahedronCanvas_IsPresent()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{PlaywrightFixture.BaseUrl}/provision");

        await page.WaitForSelectorAsync("canvas");
        var canvases = await page.QuerySelectorAllAsync("canvas");

        canvases.Should().NotBeEmpty("Babylon.js canvas must be rendered");
    }

    [Fact]
    public async Task ProvisionPage_RailsVisible_ShowsBothModelNames()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{PlaywrightFixture.BaseUrl}/provision");

        await page.WaitForSelectorAsync(".ec-rail");
        var markup = await page.ContentAsync();

        markup.Should().Contain("PHI-4 MINI");
        markup.Should().Contain("QWEN 2.5");
    }
}

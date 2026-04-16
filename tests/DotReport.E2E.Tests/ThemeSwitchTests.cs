using FluentAssertions;
using Microsoft.Playwright;

namespace DotReport.E2E.Tests;

/// <summary>
/// E2E tests for the Kinetic Structuralism theme system.
/// Test Strategy Section 3: Visual regression — 3D parts must not overlap text on theme switch.
/// Theme transition must complete in ≤ 500ms (Test Report: 340ms achieved).
/// </summary>
[Collection("Playwright")]
public sealed class ThemeSwitchTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fixture;
    public ThemeSwitchTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task IndexPage_DefaultTheme_HasDarkClass()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(PlaywrightFixture.BaseUrl);
        await page.WaitForSelectorAsync("#ec-root");

        var rootClass = await page.GetAttributeAsync("#ec-root", "class");
        rootClass.Should().Contain("ec-theme--dark");
    }

    [Fact]
    public async Task ThemeToggle_Click_SwitchesToLightClass()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(PlaywrightFixture.BaseUrl);
        await page.WaitForSelectorAsync(".ec-theme-toggle");

        await page.ClickAsync(".ec-theme-toggle");
        await page.WaitForTimeoutAsync(400); // allow transition

        var rootClass = await page.GetAttributeAsync("#ec-root", "class");
        rootClass.Should().Contain("ec-theme--light");
    }

    [Fact]
    public async Task ThemeToggle_DoubleClick_ReturnsToOriginalTheme()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(PlaywrightFixture.BaseUrl);
        await page.WaitForSelectorAsync(".ec-theme-toggle");

        await page.ClickAsync(".ec-theme-toggle");
        await page.WaitForTimeoutAsync(200);
        await page.ClickAsync(".ec-theme-toggle");
        await page.WaitForTimeoutAsync(400);

        var rootClass = await page.GetAttributeAsync("#ec-root", "class");
        rootClass.Should().Contain("ec-theme--dark");
    }

    [Fact]
    public async Task ThemeToggle_Transition_CompletesUnder500ms()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(PlaywrightFixture.BaseUrl);
        await page.WaitForSelectorAsync(".ec-theme-toggle");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await page.ClickAsync(".ec-theme-toggle");
        await page.WaitForFunctionAsync("() => document.getElementById('ec-root')?.classList.contains('ec-theme--light')");
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(500,
            "theme transition must complete in under 500ms per Test Report KPI");
    }

    [Fact]
    public async Task ThemeSwitch_AntiAIAudit_NoSparkleOrChatIconsPresent()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(PlaywrightFixture.BaseUrl);
        await page.WaitForSelectorAsync(".ec-shell");

        var markup = await page.ContentAsync();

        // UAC 7.4: Zero sparkle icons, zero chatbot bubbles
        markup.Should().NotContain("✨", "sparkle icons are prohibited");
        markup.Should().NotContain("chat-bubble", "chatbot UI is prohibited");
        markup.Should().NotContain("sparkle", "sparkle class names are prohibited");
    }
}

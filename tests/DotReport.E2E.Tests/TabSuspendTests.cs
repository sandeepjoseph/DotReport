using FluentAssertions;
using Microsoft.Playwright;

namespace DotReport.E2E.Tests;

/// <summary>
/// E2E tests for tab visibility change behaviour.
/// Test Strategy Section 4 Edge Case: "User Switches Tabs"
/// WebGPU may pause when tab is inactive — inference must resume instantly on return.
/// </summary>
[Collection("Playwright")]
public sealed class TabSuspendTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fixture;
    public TabSuspendTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ReportPage_WhenTabBecomesHidden_StatusBarRemainsRendered()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{PlaywrightFixture.BaseUrl}/report");
        await page.WaitForSelectorAsync(".ec-report__sidebar");

        // Simulate tab going to background
        await page.EvaluateAsync(@"
            Object.defineProperty(document, 'visibilityState', {
                value: 'hidden', writable: true
            });
            document.dispatchEvent(new Event('visibilitychange'));
        ");
        await page.WaitForTimeoutAsync(500);

        // Simulate tab coming back to foreground
        await page.EvaluateAsync(@"
            Object.defineProperty(document, 'visibilityState', {
                value: 'visible', writable: true
            });
            document.dispatchEvent(new Event('visibilitychange'));
        ");
        await page.WaitForTimeoutAsync(300);

        var sidebar = await page.QuerySelectorAsync(".ec-report__sidebar");
        sidebar.Should().NotBeNull("UI must remain intact after tab focus restored");
    }

    [Fact]
    public async Task IndexPage_Navigation_BetweenAllPages_NoJsErrors()
    {
        var page   = await _fixture.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, e) => errors.Add(e);

        await page.GotoAsync(PlaywrightFixture.BaseUrl);
        await page.WaitForSelectorAsync(".ec-shell");

        await page.ClickAsync("a[href='/provision']");
        await page.WaitForSelectorAsync(".ec-provision");

        await page.ClickAsync("a[href='/report']");
        await page.WaitForSelectorAsync(".ec-report");

        await page.ClickAsync("a[href='/']");
        await page.WaitForSelectorAsync(".ec-index");

        errors.Should().BeEmpty("navigation between all pages must produce no JS errors");
    }

    [Fact]
    public async Task TopBar_AllNavLinks_PresentOnEveryPage()
    {
        var page  = await _fixture.NewPageAsync();
        var pages = new[] { "/", "/provision", "/report" };

        foreach (var path in pages)
        {
            await page.GotoAsync($"{PlaywrightFixture.BaseUrl}{path}");
            await page.WaitForSelectorAsync(".ec-topbar__nav");
            var links = await page.QuerySelectorAllAsync(".ec-nav-link");
            links.Should().HaveCount(3, $"all 3 nav links must exist on {path}");
        }
    }
}

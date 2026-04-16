using Microsoft.Playwright;

namespace DotReport.E2E.Tests;

/// <summary>
/// Shared Playwright browser fixture for all E2E tests.
/// Uses Chromium with WebGPU flags enabled.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser    Browser    { get; private set; } = null!;

    // Set via environment variable in CI, defaults to local dev server
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("DOTREPORT_BASE_URL")
        ?? "http://localhost:5000";

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[]
            {
                "--enable-unsafe-webgpu",
                "--enable-features=Vulkan",
                "--use-gl=swiftshader",       // software WebGPU in CI
                "--disable-gpu-sandbox"
            }
        });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        Playwright.Dispose();
    }

    public async Task<IPage> NewPageAsync()
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
        });
        return await context.NewPageAsync();
    }
}

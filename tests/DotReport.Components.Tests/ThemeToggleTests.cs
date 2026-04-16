using Bunit;
using DotReport.Client.Components;
using DotReport.Client.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DotReport.Components.Tests;

/// <summary>
/// bUnit tests for the ThemeToggle component.
/// Verifies dark/light theme switch in the Kinetic Structuralism design (UAC 7.4).
/// </summary>
public sealed class ThemeToggleTests : TestContext
{
    private readonly ThemeService _themeService = new();

    public ThemeToggleTests()
    {
        Services.AddSingleton(_themeService);
    }

    [Fact]
    public void ThemeToggle_DefaultDark_TrackHasDarkClass()
    {
        _themeService.Set(EdgeTheme.Dark);

        var cut = RenderComponent<ThemeToggle>();

        cut.Find(".ec-toggle-track")
           .ClassList.Should().Contain("ec-toggle-track--dark");
    }

    [Fact]
    public void ThemeToggle_SwitchedToLight_TrackHasLightClass()
    {
        _themeService.Set(EdgeTheme.Light);

        var cut = RenderComponent<ThemeToggle>();

        cut.Find(".ec-toggle-track")
           .ClassList.Should().Contain("ec-toggle-track--light");
    }

    [Fact]
    public void ThemeToggle_Clicked_CallsThemeServiceToggle()
    {
        _themeService.Set(EdgeTheme.Dark);
        var cut = RenderComponent<ThemeToggle>();

        cut.Find(".ec-theme-toggle").Click();

        _themeService.Current.Should().Be(EdgeTheme.Light);
    }

    [Fact]
    public void ThemeToggle_DoubleClick_TogglesBack()
    {
        _themeService.Set(EdgeTheme.Dark);
        var cut = RenderComponent<ThemeToggle>();

        cut.Find(".ec-theme-toggle").Click();
        cut.Find(".ec-theme-toggle").Click();

        _themeService.Current.Should().Be(EdgeTheme.Dark);
    }

    [Fact]
    public void ThemeToggle_RendersArchiveAndMonolithLabels()
    {
        var cut = RenderComponent<ThemeToggle>();

        cut.Markup.Should().Contain("ARCHIVE");
        cut.Markup.Should().Contain("MONOLITH");
    }
}

using DotReport.Client.Services;
using FluentAssertions;

namespace DotReport.Components.Tests;

/// <summary>
/// Unit tests for ThemeService (no Blazor rendering needed).
/// Tests Theme A / Theme B switch logic and CSS class generation.
/// </summary>
public sealed class ThemeServiceTests
{
    [Fact]
    public void ThemeService_DefaultTheme_IsDark()
    {
        var svc = new ThemeService();
        svc.Current.Should().Be(EdgeTheme.Dark);
    }

    [Fact]
    public void ThemeService_Toggle_SwitchesFromDarkToLight()
    {
        var svc = new ThemeService();
        svc.Toggle();
        svc.Current.Should().Be(EdgeTheme.Light);
    }

    [Fact]
    public void ThemeService_ToggleTwice_ReturnsToOriginal()
    {
        var svc = new ThemeService();
        svc.Toggle();
        svc.Toggle();
        svc.Current.Should().Be(EdgeTheme.Dark);
    }

    [Fact]
    public void ThemeService_DarkTheme_CurrentThemeClassIsDark()
    {
        var svc = new ThemeService();
        svc.CurrentThemeClass.Should().Be("ec-theme--dark");
    }

    [Fact]
    public void ThemeService_LightTheme_CurrentThemeClassIsLight()
    {
        var svc = new ThemeService();
        svc.Set(EdgeTheme.Light);
        svc.CurrentThemeClass.Should().Be("ec-theme--light");
    }

    [Fact]
    public void ThemeService_SetSameTheme_DoesNotFireEvent()
    {
        var svc = new ThemeService();
        int fired = 0;
        svc.OnThemeChanged += () => fired++;

        svc.Set(EdgeTheme.Dark); // same as default — should not fire

        fired.Should().Be(0);
    }

    [Fact]
    public void ThemeService_SetDifferentTheme_FiresOnThemeChanged()
    {
        var svc = new ThemeService();
        int fired = 0;
        svc.OnThemeChanged += () => fired++;

        svc.Set(EdgeTheme.Light);

        fired.Should().Be(1);
    }

    [Theory]
    [InlineData(EdgeTheme.Dark,  true)]
    [InlineData(EdgeTheme.Light, false)]
    public void ThemeService_IsDark_MatchesCurrentTheme(EdgeTheme theme, bool expectedIsDark)
    {
        var svc = new ThemeService();
        svc.Set(theme);
        svc.IsDark.Should().Be(expectedIsDark);
    }

    [Theory]
    [InlineData(EdgeTheme.Dark,  "STEALTH MONOLITH")]
    [InlineData(EdgeTheme.Light, "ARCHITECTURAL ARCHIVE")]
    public void ThemeService_CurrentThemeLabel_MatchesTheme(EdgeTheme theme, string expectedLabel)
    {
        var svc = new ThemeService();
        svc.Set(theme);
        svc.CurrentThemeLabel.Should().Be(expectedLabel);
    }
}

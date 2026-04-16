namespace DotReport.Client.Services;

public enum EdgeTheme { Light, Dark }

/// <summary>
/// Manages the active Kinetic Structuralism theme.
/// Theme A: Retro Light (Architectural Archive)
/// Theme B: Modern Dark (Stealth Monolith)
/// </summary>
public sealed class ThemeService
{
    private EdgeTheme _current = EdgeTheme.Dark;

    public EdgeTheme Current => _current;
    public string CurrentThemeClass => _current == EdgeTheme.Dark ? "ec-theme--dark" : "ec-theme--light";
    public string CurrentThemeLabel => _current == EdgeTheme.Dark ? "STEALTH MONOLITH" : "ARCHITECTURAL ARCHIVE";
    public bool IsDark => _current == EdgeTheme.Dark;

    public event Action? OnThemeChanged;

    public void Toggle()
    {
        _current = _current == EdgeTheme.Dark ? EdgeTheme.Light : EdgeTheme.Dark;
        OnThemeChanged?.Invoke();
    }

    public void Set(EdgeTheme theme)
    {
        if (_current == theme) return;
        _current = theme;
        OnThemeChanged?.Invoke();
    }
}

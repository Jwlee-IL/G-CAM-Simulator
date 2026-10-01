namespace Gcam.Studio.Core.Services;

public enum AppTheme
{
    Dark,
    Light,
}

/// <summary>Switches the colour tokens at runtime. Implemented by the WPF shell.</summary>
public interface IThemeService
{
    AppTheme Current { get; }

    void Apply(AppTheme theme);
}

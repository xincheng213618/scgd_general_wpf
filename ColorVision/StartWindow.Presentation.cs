using ColorVision.Themes;
using ColorVision.UI;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision;

public partial class StartWindow
{
    private bool _presentationClosed;
    private StartupTheme _startupTheme;

    private void InitializePresentation()
    {
        // Capture the saved selection for this splash; changes in Settings apply on the next startup.
        _startupTheme = ConfigService.Instance?.GetRequiredService<ThemeConfig>().StartupTheme ?? StartupTheme.Dark;
        _subscribedThemeManager!.CurrentUIThemeChanged += StartupUiThemeChanged;
        SystemParameters.StaticPropertyChanged += StartupSystemSettingsChanged;
        ApplyStartupPalette(_subscribedThemeManager.CurrentUITheme);
    }

    private void StartupUiThemeChanged(Theme theme)
    {
        if (_startupTheme != StartupTheme.FollowApplication) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(() => StartupUiThemeChanged(theme)); return; }
        if (!_presentationClosed) ApplyStartupPalette(theme);
    }

    private void StartupSystemSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SystemParameters.HighContrast)) return;
        Dispatcher.InvokeAsync(() =>
        {
            if (!_presentationClosed && _subscribedThemeManager != null)
                ApplyStartupPalette(_subscribedThemeManager.CurrentUITheme);
        });
    }

    private void ApplyStartupPalette(Theme theme)
    {
        bool dark = _startupTheme switch
        {
            StartupTheme.Light => false,
            StartupTheme.FollowApplication => theme == Theme.Dark,
            _ => true
        };
        startupScene.IsDark = dark;
        if (SystemParameters.HighContrast)
        {
            Resources["Startup.Surface"] = SystemColors.WindowBrush;
            Resources["Startup.Ink"] = Resources["Startup.Muted"] = Resources["Startup.Faint"] = SystemColors.WindowTextBrush;
            Resources["Startup.Border"] = Resources["Startup.Progress"] = SystemColors.HighlightBrush;
            Resources["Startup.Track"] = SystemColors.ControlBrush;
            return;
        }

        void Set(string key, string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            Resources[key] = brush;
        }
        Set("Startup.Surface", dark ? "#080E19" : "#F4F5F7");
        Set("Startup.Ink", dark ? "#E1EBF6" : "#202C45");
        Set("Startup.Muted", dark ? "#93A6BD" : "#50657E");
        Set("Startup.Faint", dark ? "#71849D" : "#64778B");
        Set("Startup.Border", dark ? "#283445" : "#CDD8E3");
        Set("Startup.Track", dark ? "#243041" : "#D8E0E8");
        var progress = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        progress.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(dark ? "#68E6E8" : "#168E9E"), 0));
        progress.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(dark ? "#7CA9FF" : "#3E75CB"), 0.55));
        progress.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(dark ? "#B6A0FF" : "#8164B6"), 1));
        progress.Freeze();
        Resources["Startup.Progress"] = progress;
    }

    private void ReleasePresentation()
    {
        _presentationClosed = true;
        if (_subscribedThemeManager != null) _subscribedThemeManager.CurrentUIThemeChanged -= StartupUiThemeChanged;
        SystemParameters.StaticPropertyChanged -= StartupSystemSettingsChanged;
    }
}

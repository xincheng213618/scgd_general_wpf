using ColorVision.Themes;
using ColorVision.Themes.Controls;
using Spectrum.Menus;
using ColorVision.UI.Menus;
using Spectrum.Help.Art;
using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Spectrum.Help;

public class MenuSpectrumAbout : SpectrumMenuIBase
{
    public override string OwnerGuid => MenuItemConstants.Help;
    public override string GuidId => "Spectrum.About";
    public override int Order => 100000;
    public override string Header => AboutText.SpectrumTitle;

    public override void Execute()
    {
        new SpectrumAboutWindow { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
    }
}

public partial class SpectrumAboutWindow : BaseWindow
{
    private ThemeManager? _themePublisher;
    private bool _dark;
    private bool _closed;

    public SpectrumAboutWindow()
    {
        InitializeComponent();
        Icon = null;
        AboutWindowChrome.FitToWorkArea(this, 16);

        Assembly assembly = typeof(SpectrumAboutWindow).Assembly;
        VersionLabel.Text = assembly.GetName().Version?.ToString() ?? "—";
        BuildLabel.Text = $"BUILD {File.GetLastWriteTime(assembly.Location):yyyy.MM.dd} / X{IntPtr.Size * 8}";
        RuntimeLabel.Text = $".NET {Environment.Version} / {IntPtr.Size * 8}-BIT";
        CopyrightLabel.Text = $"© 2023–{DateTime.Now.Year} ColorVision · Spectrum";
        ApplyPalette(ThemeManager.Current.CurrentUITheme == Theme.Dark);
        Loaded += Window_Loaded;
        Activated += Window_Activated;
        Deactivated += Window_Deactivated;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= Window_Loaded;
        _themePublisher = ThemeManager.Current;
        _themePublisher.CurrentUIThemeChanged += ThemeChanged;
        SystemParameters.StaticPropertyChanged += SystemSettingsChanged;
        ApplyPalette(_themePublisher.CurrentUITheme == Theme.Dark);
        SpectralScene.MotionEnabled = IsActive;
    }

    private void ThemeChanged(Theme theme)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(() => ThemeChanged(theme)); return; }
        if (!_closed) ApplyPalette(theme == Theme.Dark);
    }

    private void SystemSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast))
            Dispatcher.InvokeAsync(() => { if (!_closed) ApplyPalette(_dark); });
    }

    private void ApplyPalette(bool dark)
    {
        _dark = dark;
        bool highContrast = SystemParameters.HighContrast;
        void SetBrush(string key, string value) => Resources[key] = FreezeBrush((Color)ColorConverter.ConvertFromString(value));
        SetBrush("About.Ink", dark ? "#EDF3FF" : "#202C45");
        SetBrush("About.Muted", dark ? "#99A9C0" : "#5B6C87");
        SetBrush("About.Faint", dark ? "#788AA5" : "#6B7C92");
        SetBrush("About.Line", dark ? "#424D6687" : "#35516B8E");
        SetBrush("About.Surface", dark ? "#FF080D19" : "#FFF1F4FA");
        SetBrush("About.Hover", dark ? "#246794CD" : "#1853678F");
        Background = (Brush)Resources["About.Surface"];
        if (highContrast)
        {
            Resources["About.Surface"] = Background = SystemColors.WindowBrush;
            Resources["About.Ink"] = Resources["About.Muted"] = Resources["About.Faint"] = SystemColors.WindowTextBrush;
            Resources["About.Line"] = Resources["About.Hover"] = SystemColors.HighlightBrush;
        }
        SpectralScene.IsDark = dark;
        PaletteLabel.Text = dark ? "N O C T U R N E" : "O P A L I N E";
    }

    private static SolidColorBrush FreezeBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void PaletteButton_Click(object sender, RoutedEventArgs e) => ApplyPalette(!_dark);
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_Activated(object? sender, EventArgs e) => SpectralScene.MotionEnabled = true;
    private void Window_Deactivated(object? sender, EventArgs e) => SpectralScene.MotionEnabled = false;
    private void Exhibition_MouseMove(object sender, MouseEventArgs e) => SpectralScene.TrackPointer(e.GetPosition(Exhibition));
    private void Exhibition_MouseLeave(object sender, MouseEventArgs e) => SpectralScene.TrackPointer(null);

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key != Key.Escape) { base.OnPreviewKeyDown(e); return; }
        Close();
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        if (_themePublisher != null) _themePublisher.CurrentUIThemeChanged -= ThemeChanged;
        _themePublisher = null;
        SystemParameters.StaticPropertyChanged -= SystemSettingsChanged;
        SpectralScene.MotionEnabled = false;
        base.OnClosed(e);
    }
}

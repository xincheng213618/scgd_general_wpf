using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Themes;

public partial class ThemeConfig
{
    [Browsable(false)]
    public ThemeAppearance LightAppearance { get; set; } = new();
    [Browsable(false)]
    public ThemeAppearance DarkAppearance { get; set; } = new();

    [ConfigSetting(Order = -39, Group = "Appearance", Section = "ThemeColors", Name = "AppearancePreset")]
    [PropertyEditorType(typeof(EnumPropertiesEditor)), JsonIgnore]
    public ThemePreset Preset
    {
        get => ActiveAppearance.Preset;
        set
        {
            ActiveAppearance.Preset = Enum.IsDefined(value) ? value : ThemePreset.Default;
            ActiveAppearance.Accent = ActiveAppearance.Background = ActiveAppearance.Foreground = null;
            Apply();
            RefreshAppearanceProperties();
        }
    }

    [ConfigSetting(Order = -38, Group = "Appearance", Section = "ThemeColors", Name = "AppearanceAccent")]
    [PropertyEditorType(typeof(ThemeColorPropertiesEditor)), JsonIgnore]
    public string AccentColor
    {
        get => GetColor(ActiveAppearance.Accent, "CV.Color.Accent.Primary", "#0169CC");
        set { ActiveAppearance.Accent = ThemeAppearance.NormalizeColor(value); Apply(); RefreshAppearanceProperties(); }
    }

    [ConfigSetting(Order = -37, Group = "Appearance", Section = "ThemeColors", Name = "AppearanceBackground")]
    [PropertyEditorType(typeof(ThemeColorPropertiesEditor)), JsonIgnore]
    public string BackgroundColor
    {
        get => GetColor(ActiveAppearance.Background, "CV.Color.Surface.Window", "#FFFFFF");
        set { ActiveAppearance.Background = ThemeAppearance.NormalizeColor(value); Apply(); RefreshAppearanceProperties(); }
    }

    [ConfigSetting(Order = -36, Group = "Appearance", Section = "ThemeColors", Name = "AppearanceForeground")]
    [PropertyEditorType(typeof(ThemeColorPropertiesEditor)), JsonIgnore]
    public string ForegroundColor
    {
        get => GetColor(ActiveAppearance.Foreground, "CV.Color.Text.Primary", "#0D0D0D");
        set { ActiveAppearance.Foreground = ThemeAppearance.NormalizeColor(value); Apply(); RefreshAppearanceProperties(); }
    }

    [ConfigSetting(Order = -35, Group = "Appearance", Section = "ThemeColors", Name = "AppearanceFont")]
    [PropertyEditorType(typeof(ThemeFontPropertiesEditor))]
    public string UIFontFamily { get; set; } = string.Empty;

    [ConfigSetting(Order = -20, Group = "Appearance", Section = "AppearanceStartup", Name = "AppearanceReset")]
    [PropertyEditorType(typeof(ThemeResetPropertiesEditor)), JsonIgnore]
    public RelayCommand ResetAppearanceCommand => new(_ =>
    {
        LightAppearance = new();
        DarkAppearance = new();
        UIFontFamily = string.Empty;
        Theme = Theme.UseSystem;
        Apply();
        RefreshAppearanceProperties();
    });

    [Browsable(false), JsonIgnore]
    private ThemeAppearance ActiveAppearance => (Theme == Theme.Dark || Theme == Theme.UseSystem && ThemeManager.Current.AppsTheme == Theme.Dark)
        ? DarkAppearance ??= new() : LightAppearance ??= new();

    public void Apply(Application? application = null)
    {
        application ??= Application.Current;
        if (application != null)
            ThemeManager.Current.ApplyAppearance(application, Theme, LightAppearance ??= new(), DarkAppearance ??= new(), UIFontFamily);
    }

    internal void RefreshAppearanceProperties()
    {
        foreach (string name in new[] { nameof(Preset), nameof(AccentColor), nameof(BackgroundColor), nameof(ForegroundColor), nameof(UIFontFamily) })
            OnPropertyChanged(name);
    }

    private static string GetColor(string? configured, string key, string fallback)
    {
        if (ThemeAppearance.NormalizeColor(configured) is string normalized) return normalized;
        return Application.Current?.TryFindResource(key) is Color color ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : fallback;
    }
}

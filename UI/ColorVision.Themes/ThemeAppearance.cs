using System;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Themes;

public enum ThemePreset
{
    [System.ComponentModel.Description("AppearanceDefault")]
    Default,
    Codex,
    Warm
}

/// <summary>Optional appearance overrides for one resolved light/dark mode.</summary>
public sealed class ThemeAppearance
{
    public ThemePreset Preset { get; set; }
    public string? Accent { get; set; }
    public string? Background { get; set; }
    public string? Foreground { get; set; }

    public ThemeAppearance Copy() => new() { Preset = Preset, Accent = Accent, Background = Background, Foreground = Foreground };

    public static string? NormalizeColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string hex = value.Trim().TrimStart('#');
        if (hex.Length != 6 || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _))
            return null;
        return "#" + hex.ToUpperInvariant();
    }

    public ResourceDictionary CreateResources(Theme theme, ResourceDictionary defaults, string? fontFamily = null)
    {
        bool dark = theme == Theme.Dark;
        var resources = new ResourceDictionary();
        Color background = defaults["CV.Color.Surface.Window"] is Color windowColor ? windowColor : Parse(dark ? "#262626" : "#FFFFFF");
        Color foreground = defaults["CV.Color.Text.Primary"] is Color textColor ? textColor : (dark ? Colors.White : Colors.Black);
        Color accent = defaults["CV.Color.Accent.Primary"] is Color accentColor ? accentColor : Parse(dark ? "#4D8DFF" : "#007ACC");
        if (Preset == ThemePreset.Codex)
        {
            background = Parse(dark ? "#212121" : "#FFFFFF");
            foreground = Parse(dark ? "#ECECEC" : "#0D0D0D");
            accent = Parse(dark ? "#5DA8FF" : "#0169CC");
        }
        else if (Preset == ThemePreset.Warm)
        {
            background = Parse(dark ? "#25221F" : "#FFFCF7");
            foreground = Parse(dark ? "#F2EBE2" : "#2C2925");
            accent = Parse(dark ? "#E7A56A" : "#A65D34");
        }
        background = ReadColor(Background, background);
        foreground = ReadColor(Foreground, foreground);
        accent = ReadColor(Accent, accent);
        Color contrast = background.R * .2126 + background.G * .7152 + background.B * .0722 < 128 ? Colors.White : Colors.Black;
        Color sidebar = Mix(background, contrast, .025);
        Color hover = Mix(background, contrast, .065);
        Color border = Mix(background, contrast, .105);
        Color secondary = Mix(background, foreground, .62);

        Brush(sidebar, "CV.Surface.Sidebar");
        Brush(background, "CV.Surface.Page");
        Brush(hover, "CV.Surface.NavigationSelected");
        resources["CV.Font.UI"] = new FontFamily(string.IsNullOrWhiteSpace(fontFamily) ? "Microsoft YaHei UI, Segoe UI" : fontFamily);

        // The default profile retains the shipped palette and vendor-specific defaults.
        if (Preset == ThemePreset.Default && NormalizeColor(Accent) == null && NormalizeColor(Background) == null && NormalizeColor(Foreground) == null)
            return resources;

        ColorResource(background, "CV.Color.Surface.Window", "GlobalBackgroundColor", "UpdateDialogBackgroundColor");
        ColorResource(background, "CV.Color.Surface.Control", "UpdateDialogSurfaceColor");
        ColorResource(hover, "CV.Color.Surface.ControlHover", "GlobalBroderBrushColor", "UpdateDialogSurfaceHoverColor");
        ColorResource(border, "CV.Color.Border.Control", "UpdateDialogBorderColor");
        ColorResource(foreground, "CV.Color.Text.Primary", "GlobalTextColor", "UpdateDialogTextPrimaryColor");
        ColorResource(secondary, "UpdateDialogTextSecondaryColor");
        ColorResource(accent, "CV.Color.Accent.Primary", "UpdateDialogAccentColor");
        ColorResource(Mix(accent, contrast, .12), "CV.Color.Accent.Hover", "UpdateDialogAccentHoverColor");
        ColorResource(Mix(background, accent, .16), "SelectedBackgroundColor");
        ColorResource(hover, "SelectedUnfocusedColor");
        ColorResource(sidebar, "CV.Color.Docking.Chrome");
        ColorResource(background, "CV.Color.Docking.Surface");
        ColorResource(border, "CV.Color.Docking.Border");
        ColorResource(accent, "CV.Color.Docking.Accent");
        ColorResource(foreground, "CV.Color.Docking.Text");
        ColorResource(secondary, "CV.Color.Docking.MutedText");
        ColorResource(hover, "CV.Color.Docking.Hover");
        ColorResource(Mix(background, contrast, .1), "CV.Color.Docking.Pressed");

        Brush(background, "GlobalBackground", "ButtonBackground", "MeasureBackground", "RegionBrush", "TextBoxBackgroundBrush", "SubMenuBrushKey", "ContextMenuBackground", "ContextMenuRectangleFill", "IconSolidColorBackgroundBrush");
        Brush(sidebar, "GlobalBorderBrush1", "BorderBackGroud1", "BorderBackGroud2", "environmentcommandbar", "environmentcommandbaricon");
        Brush(hover, "GlobalBorderBrush", "MenuHighBrushKey", "toplevelmenuheaderhover", "EnvironmentCommandBarMenuItemMouseOver", "EnvironmentCommandBarHoverOverSelectedIcon", "CaptionButtonChoiceBackgrouundBrush");
        Brush(border, "ButtonBorderBrush", "BorderBrush", "BordrBrush", "SubMenuBorderBrushKey", "ContextMenuBorderBrush", "MenuItemSeparatorFillTop", "MeasureBarBackgroundBrush");
        Brush(foreground, "GlobalTextBrush", "PrimaryTextBrush", "DefaultBrush", "CaptionButtonBrush", "CaptionButtonForeground", "TitleBarActionForeground", "SystemColorsMenuText", "IconSolidColorBrush");
        Brush(secondary, "SecondaryTextBrush", "MenuItemSecondaryForeground", "TitleBarActionInactiveForeground");
        Brush(accent, "PrimaryBrush", "StatusBarBackgroundBrush");
        Brush(Mix(background, accent, .14), "LightPrimaryBrush");
        Brush(Mix(accent, Colors.Black, .15), "DarkPrimaryBrush");
        return resources;

        void ColorResource(Color color, params string[] keys)
        {
            foreach (string key in keys) resources[key] = color;
        }
        void Brush(Color color, params string[] keys)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            foreach (string key in keys) resources[key] = brush;
        }
    }

    private static Color ReadColor(string? value, Color fallback) => NormalizeColor(value) is string hex ? Parse(hex) : fallback;
    private static Color Parse(string value) => (Color)ColorConverter.ConvertFromString(value);
    private static Color Mix(Color first, Color second, double amount) => Color.FromRgb(
        (byte)Math.Round(first.R + (second.R - first.R) * amount),
        (byte)Math.Round(first.G + (second.G - first.G) * amount),
        (byte)Math.Round(first.B + (second.B - first.B) * amount));
}

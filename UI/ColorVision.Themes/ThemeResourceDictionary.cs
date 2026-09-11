#pragma warning disable CA1010 // WPF ResourceDictionary defines the collection contract.
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Themes;

/// <summary>A complete theme resource group. Hosts can merge Themes/Theme.xaml for the default light theme.</summary>
public sealed class ThemeResourceDictionary : ResourceDictionary
{
    public ThemeResourceDictionary() : this(Theme.Light) { }

    internal ThemeResourceDictionary(Theme theme)
    {
        AppliedTheme = theme;
        // Prepare the entire group before it replaces the active application resources.
        var sources = new List<string>(theme == Theme.Dark ? ThemeManager.ResourceDictionaryDark : ThemeManager.ResourceDictionaryWhite);
        sources.AddRange(ThemeManager.ResourceDictionaryBase);
        foreach (string source in sources)
            MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.RelativeOrAbsolute) });
    }

    internal Theme AppliedTheme { get; }

    internal static bool IsThemeResource(ResourceDictionary dictionary)
    {
        if (dictionary is ThemeResourceDictionary)
            return true;

        string source = dictionary.Source?.OriginalString ?? string.Empty;
        if (source.StartsWith("pack://application:,,,", StringComparison.OrdinalIgnoreCase))
            source = source.Substring("pack://application:,,,".Length);
        return LegacySources.Contains(source.TrimStart('/'));
    }

    // Only adopt the known legacy startup dictionaries; unrelated host/plugin dictionaries retain their position.
    private static readonly HashSet<string> LegacySources = new(StringComparer.OrdinalIgnoreCase)
    {
        "ColorVision.Themes;component/Themes/Theme.xaml",
        "ColorVision.Themes;component/Themes/White.xaml",
        "ColorVision.Themes;component/Themes/Dark.xaml",
        "ColorVision.Themes;component/Themes/Base.xaml",
        "ColorVision.Themes;component/Themes/Menu.xaml",
        "ColorVision.Themes;component/Themes/GroupBox.xaml",
        "ColorVision.Themes;component/Themes/Icons.xaml",
        "ColorVision.Themes;component/Themes/Window/BaseWindow.xaml",
        "HandyControl;component/Themes/basic/colors/colors.xaml",
        "HandyControl;component/Themes/basic/colors/colorsdark.xaml",
        "HandyControl;component/Themes/Theme.xaml"
    };
}

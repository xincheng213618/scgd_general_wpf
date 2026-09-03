using AvalonDock.Themes;
using System;
using System.Windows;

namespace ColorVision;

internal sealed class AvalonDockTheme : DictionaryTheme
{
    internal AvalonDockTheme(bool isDark) : base(CreateResources(isDark))
    {
    }

    private static ResourceDictionary CreateResources(bool isDark)
    {
        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = (isDark ? (Theme)new Vs2013DarkTheme() : new Vs2013LightTheme()).GetResourceUri()
        });
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"/ColorVision;component/Themes/AvalonDockModern{(isDark ? "Dark" : "Light")}.xaml", UriKind.Relative)
        });
        // Floating windows load the theme independently. Keep all chrome in the theme,
        // not in MainWindow.Resources or a Loaded-time visual-tree patch.
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/ColorVision;component/Themes/AvalonDockModernTemplates.xaml", UriKind.Relative)
        });
        return resources;
    }
}

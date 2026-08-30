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
        var resources = new ResourceDictionary
        {
            Source = (isDark ? (Theme)new Vs2013DarkTheme() : new Vs2013LightTheme()).GetResourceUri()
        };
        // Floating windows load the theme independently, so keep the correction in the theme.
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/ColorVision;component/Themes/AvalonDockGripTemplates.xaml", UriKind.Relative)
        });
        return resources;
    }
}

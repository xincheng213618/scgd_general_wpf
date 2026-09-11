using AvalonDock.Themes;
using System.Windows;

namespace ColorVision.Solution.Themes;

/// <summary>Shared modern docking appearance for the main workspace and standalone tools.</summary>
public class AvalonDockTheme : DictionaryTheme
{
    public AvalonDockTheme(bool isDark) : base(CreateResources(isDark))
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
            Source = new Uri($"/ColorVision.Solution;component/Themes/AvalonDockModern{(isDark ? "Dark" : "Light")}.xaml", UriKind.Relative)
        });
        // Floating windows load the theme independently, so its templates and
        // command resources travel with the theme instead of a particular window.
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/ColorVision.Solution;component/Themes/AvalonDockModernTemplates.xaml", UriKind.Relative)
        });
        return resources;
    }
}

using AvalonDock.Themes;
using System;
using System.IO;
using System.Windows;

namespace Conoscope.Presentation.Docking;

/// <summary>Uses the shared docking chrome without requiring a newer host assembly type.</summary>
internal sealed class ConoscopeDockTheme(ResourceDictionary resources) : DictionaryTheme(resources)
{
    public static AvalonDock.Themes.Theme Create(ColorVision.Themes.Theme theme)
    {
        bool isDark = theme == ColorVision.Themes.Theme.Dark;
        AvalonDock.Themes.Theme baseTheme = isDark ? new Vs2013DarkTheme() : new Vs2013LightTheme();
        foreach (string assemblyName in new[] { "ColorVision.Solution", "ColorVision" })
        {
            try
            {
                var resources = new ResourceDictionary();
                resources.MergedDictionaries.Add(new ResourceDictionary { Source = baseTheme.GetResourceUri() });
                resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri($"/{assemblyName};component/Themes/AvalonDockModern{(isDark ? "Dark" : "Light")}.xaml", UriKind.Relative)
                });
                resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri($"/{assemblyName};component/Themes/AvalonDockModernTemplates.xaml", UriKind.Relative)
                });
                return new ConoscopeDockTheme(resources);
            }
            catch (IOException)
            {
                // Older supported hosts predate the shared resources; retain their base theme.
            }
        }
        return baseTheme;
    }
}

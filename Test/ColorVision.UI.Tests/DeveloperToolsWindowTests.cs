using ColorVision.Engine.Services.DeveloperTools;
using ColorVision.ToolPlugins.DeveloperTools;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class DeveloperToolsWindowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpeningTheTwoPagesDoesNotSelectOrAuthorizeAnInstallation(bool dark)
    {
        WpfTestHost.Invoke(() =>
        {
            var dictionaries = new List<ResourceDictionary>();
            DeveloperToolsWindow? window = null;
            try
            {
                foreach (string source in new[]
                {
                    $"/HandyControl;component/Themes/basic/colors/{(dark ? "colorsdark" : "colors")}.xaml",
                    "/HandyControl;component/Themes/Theme.xaml",
                    $"/ColorVision.Themes;component/Themes/{(dark ? "Dark" : "White")}.xaml",
                    "/ColorVision.Themes;component/Themes/Base.xaml",
                })
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                    dictionaries.Add(dictionary);
                }
                window = new DeveloperToolsWindow();
                var tabs = Assert.IsType<TabControl>(window.FindName("ToolTabs"));
                var root = Assert.IsType<Grid>(window.Content);
                Assert.Equal(2, tabs.Items.Count);
                foreach (int pageIndex in new[] { 0, 1 })
                {
                    tabs.SelectedIndex = pageIndex;
                    root.Measure(new Size(1016, 746));
                    root.Arrange(new Rect(0, 0, 1016, 746));
                    root.UpdateLayout();
                    DeveloperToolPageModel page = pageIndex == 0 ? window.Python : window.NodeJs;
                    Assert.Empty(page.Releases);
                    Assert.False(page.CanInstall);
                    Assert.Null(page.SelectedRelease);
                }

                string? previewDirectory = Environment.GetEnvironmentVariable("COLORVISION_DEVTOOLS_PREVIEW_DIR");
                if (!string.IsNullOrEmpty(previewDirectory))
                {
                    Directory.CreateDirectory(previewDirectory);
                    var discovery = new DeveloperToolDiscoveryService();
                    window.Python.Apply(discovery.Inspect(DeveloperToolKind.Python));
                    window.NodeJs.Apply(discovery.Inspect(DeveloperToolKind.NodeJs));
                    foreach (int pageIndex in new[] { 0, 1 })
                    {
                        tabs.SelectedIndex = pageIndex;
                        root.UpdateLayout();
                        var bitmap = new RenderTargetBitmap(1016, 746, 96, 96, PixelFormats.Pbgra32);
                        var background = new DrawingVisual();
                        using (DrawingContext drawing = background.RenderOpen())
                            drawing.DrawRectangle((Brush)window.FindResource("GlobalBackground"), null, new Rect(0, 0, 1016, 746));
                        bitmap.Render(background);
                        bitmap.Render(root);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using FileStream output = File.Create(Path.Combine(previewDirectory, $"developer-tools-{(dark ? "dark" : "light")}-{pageIndex}.png"));
                        encoder.Save(output);
                    }
                }
            }
            finally
            {
                window?.Close();
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            }
        });
    }
}

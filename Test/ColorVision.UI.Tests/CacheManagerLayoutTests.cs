using ColorVision.Engine.Services.Caches;
using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Themes;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CacheManagerLayoutTests
{
    [Theory]
    [InlineData(false, "zh-CN", 1024, 610)]
    [InlineData(true, "en-US", 820, 520)]
    public void OverviewAndCalibrationKeepTheirActionsAndListVisible(bool dark, string culture, int width, int height)
    {
        WpfTestHost.Invoke(() =>
        {
            var originalCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            LocalCalibrationCacheManagerWindow? window = null;
            try
            {
                window = new LocalCalibrationCacheManagerWindow();
                foreach (string source in new[]
                {
                    $"/HandyControl;component/Themes/basic/colors/{(dark ? "colorsdark" : "colors")}.xaml",
                    "/HandyControl;component/Themes/Theme.xaml",
                    $"/ColorVision.Themes;component/Themes/{(dark ? "Dark" : "White")}.xaml",
                    "/ColorVision.Themes;component/Themes/Base.xaml"
                }) window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
                window.Resources.MergedDictionaries.Add(new ThemeAppearance().CreateResources(dark ? Theme.Dark : Theme.Light, window.Resources));
                var overlay = (UIElement)window.FindName("LoadingOverlay");
                Assert.True(SpinWait.SpinUntil(() =>
                {
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                    return overlay.Visibility == Visibility.Collapsed;
                }, TimeSpan.FromSeconds(10)));
                const ulong used = 256UL << 20;
                const ulong budget = 1024UL << 20;
                CacheModuleSnapshot[] snapshots = CacheManagerService.Modules.Select(module =>
                    module.Id == "Calibration"
                        ? new CacheModuleSnapshot(module.Id, module.Name, module.Description, used, 1, 7, 1, 1, true, false, "",
                            [new CacheEntrySnapshot("DSNU", @"C:\Calibration\DSNU.dat", used, used, 7, 1, EngineLocalization.Get("正在使用"))]) { BudgetBytes = budget }
                        : new CacheModuleSnapshot(module.Id, module.Name, module.Description, 0, 0, 0, 0, 0, true, true, "", [])).ToArray();
                typeof(LocalCalibrationCacheManagerWindow).GetMethod("ApplySnapshots", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [snapshots]);
                var nav = (ListBox)window.FindName("CacheNavigation");
                var root = (FrameworkElement)window.Content;
                foreach (string id in new[] { "Overview", "Calibration" })
                {
                    nav.SelectedItem = nav.Items.Cast<CacheNavigationItem>().Single(page => page.Id == id);
                    root.Measure(new Size(width, height));
                    root.Arrange(new Rect(0, 0, width, height));
                    root.UpdateLayout();
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    root.UpdateLayout();
                    var grid = (DataGrid)window.FindName(id == "Overview" ? "CacheModulesGrid" : "CacheDetailsGrid");
                    var button = (Button)window.FindName(id == "Overview" ? "ReleaseAllButton" : "ApplyCalibrationBudgetButton");
                    Assert.True(grid.ActualHeight >= 70);
                    Assert.True(button.ActualHeight > 0 && button.TranslatePoint(new Point(button.ActualWidth, 0), root).X <= width + 1);
                    if (id == "Calibration") Assert.Equal(25d, ((ProgressBar)window.FindName("CalibrationBudgetUsageBar")).Value);
                    if (Environment.GetEnvironmentVariable("CV_CACHE_PREVIEW_DIR") is { Length: > 0 } directory)
                    {
                        Directory.CreateDirectory(directory);
                        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(root);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream = File.Create(Path.Combine(directory, $"{id}-{(dark ? "dark" : "light")}-{culture}.png"));
                        encoder.Save(stream);
                    }
                }
            }
            finally
            {
                window?.Close();
                CultureInfo.CurrentUICulture = originalCulture;
            }
        });
    }
}

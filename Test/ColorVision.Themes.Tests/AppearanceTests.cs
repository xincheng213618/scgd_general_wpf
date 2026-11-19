using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Themes.Tests;

public partial class ThemeResourceTests
{
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageW(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [Fact]
    public void DefaultCaptionUsesTheMatchingIcoFrameForSmallAndLargeWindowIcons()
    {
        Run((app, manager) =>
        {
            var window = new Window { ShowInTaskbar = false };
            try
            {
                window.ApplyCaption();
                IntPtr hwnd = new WindowInteropHelper(window).EnsureHandle();
                var frame = Assert.IsAssignableFrom<BitmapFrame>(window.Icon);
                var decoder = Assert.IsType<IconBitmapDecoder>(frame.Decoder);
                foreach (int iconType in new[] { 0, 1 })
                {
                    const uint WmGetIcon = 0x007F;
                    IntPtr icon = SendMessageW(hwnd, WmGetIcon, iconType, IntPtr.Zero);
                    Assert.NotEqual(IntPtr.Zero, icon);
                    var native = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    var closest = decoder.Frames.MinBy(f =>
                        Math.Abs(f.PixelWidth - native.PixelWidth) + Math.Abs(f.PixelHeight - native.PixelHeight))!;
                    var expectedAlpha = AlphaPixels(closest);
                    var nativeAlpha = AlphaPixels(native);
                    if (closest.PixelWidth == native.PixelWidth && closest.PixelHeight == native.PixelHeight)
                    {
                        Assert.Equal(expectedAlpha, nativeAlpha);
                    }
                    else
                    {
                        // At intermediate DPI sizes Windows can resample the closest frame.
                        // The opaque area should still occupy the same fraction of the icon.
                        double expectedCoverage = (double)expectedAlpha.Count(a => a >= 128) / expectedAlpha.Length;
                        double nativeCoverage = (double)nativeAlpha.Count(a => a >= 128) / nativeAlpha.Length;
                        Assert.InRange(nativeCoverage, expectedCoverage * 0.8, expectedCoverage * 1.2);
                    }
                }
            }
            finally { window.Close(); }
        });
    }

    private static byte[] AlphaPixels(BitmapSource source)
    {
        var bitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        return pixels.Where((_, i) => i % 4 == 3).ToArray();
    }

    // The override layer must cover both semantic colors and retained brush names,
    // preserve host precedence, and survive repeated mode changes without growing resources.
    [Fact]
    public void AppearanceOverridesPreserveModeProfilesAndHostResources()
    {
        Run((app, manager) =>
        {
            var light = new ThemeAppearance { Preset = ThemePreset.Codex, Accent = "#8364C8" };
            var dark = new ThemeAppearance { Preset = ThemePreset.Warm };
            var host = new ResourceDictionary { ["HostOnly"] = "preserved" };
            app.Resources.MergedDictionaries.Add(host);
            manager.ApplyAppearance(app, Theme.Light, light, dark, "Segoe UI");
            Assert.Equal(Color.FromRgb(131, 100, 200), app.Resources["CV.Color.Accent.Primary"]);
            Assert.Equal((Color)app.Resources["CV.Color.Accent.Primary"], ((SolidColorBrush)app.Resources["PrimaryBrush"]).Color);
            Assert.Equal((Color)app.Resources["CV.Color.Accent.Primary"], ((SolidColorBrush)app.Resources["CV.Accent.Primary"]).Color);
            Assert.Equal((Color)app.Resources["CV.Color.Text.Primary"], ((SolidColorBrush)app.Resources["CV.Text.Primary"]).Color);
            Assert.Equal(Colors.White, ((SolidColorBrush)app.Resources["GlobalBackground"]).Color);
            Assert.Equal("Segoe UI", ((FontFamily)app.Resources["CV.Font.UI"]).Source);
            Assert.Equal("preserved", app.Resources["HostOnly"]);
            int count = app.Resources.MergedDictionaries.Count;
            manager.ApplyTheme(app, Theme.Dark);
            Assert.Equal(Color.FromRgb(37, 34, 31), app.Resources["CV.Color.Surface.Window"]);
            manager.ApplyTheme(app, Theme.Light);
            Assert.Equal(Color.FromRgb(131, 100, 200), app.Resources["CV.Color.Accent.Primary"]);
            Assert.Equal(count, app.Resources.MergedDictionaries.Count);
            host["PrimaryBrush"] = Brushes.Red;
            manager.ApplyAppearance(app, Theme.Light, light, dark, null);
            Assert.Same(Brushes.Red, app.Resources["PrimaryBrush"]);
        });
    }

    [Fact]
    public void SystemFollowingUsesItsOwnProfileAndFailedLoadPreservesLastAppearance()
    {
        Run((app, manager) =>
        {
            manager.AppsTheme = Theme.Light;
            manager.ApplyAppearance(app, Theme.UseSystem, new() { Preset = ThemePreset.Codex }, new() { Preset = ThemePreset.Warm }, null);
            manager.AppsTheme = Theme.Dark;
            Assert.Equal(Theme.UseSystem, manager.CurrentTheme);
            Assert.Equal(Color.FromRgb(37, 34, 31), app.Resources["CV.Color.Surface.Window"]);
            List<string> original = ThemeManager.ResourceDictionaryDark;
            try
            {
                ThemeManager.ResourceDictionaryDark = ["/ColorVision.Themes;component/DoesNotExist.xaml"];
                Assert.ThrowsAny<Exception>(() => manager.ApplyAppearance(app, Theme.Dark, new(), new() { Accent = "#FF0000" }, null));
                Assert.Equal(Color.FromRgb(37, 34, 31), app.Resources["CV.Color.Surface.Window"]);
                Assert.Equal(Theme.UseSystem, manager.CurrentTheme);
            }
            finally { ThemeManager.ResourceDictionaryDark = original; }
            manager.ApplyThemeChanged(app, Theme.Dark);
            Assert.Equal(Color.FromRgb(37, 34, 31), app.Resources["CV.Color.Surface.Window"]);
        });
    }

    // Searching is a visibility operation: filtering a selected choice must not
    // write null to the bound setting or change the available language count.
    [Fact]
    public void SearchDoesNotRemoveSelectedChoiceAndResetsWhenClosed()
    {
        Run((app, manager) =>
        {
            manager.ApplyTheme(app, Theme.Light);
            var combo = new ComboBox { ItemsSource = new[] { "System", "English", "繁體中文", "简体中文" }, SelectedIndex = 0 };
            combo.Style = (Style)app.Resources["SettingsComboBoxStyle"];
            ComboBoxSearch.SetIsEnabled(combo, true);
            var window = new Window { Content = combo, Width = 400, Height = 200, ShowInTaskbar = false };
            try
            {
                window.Show();
                combo.IsDropDownOpen = true;
                app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var search = Assert.IsType<TextBox>(combo.Template.FindName("PART_SearchBox", combo));
                search.Text = "English";
                Assert.Equal("System", combo.SelectedItem);
                Assert.Equal(4, combo.Items.Count);
                Assert.Equal(Visibility.Collapsed, ((ComboBoxItem)combo.ItemContainerGenerator.ContainerFromIndex(0)).Visibility);
                Assert.Equal(Visibility.Visible, ((ComboBoxItem)combo.ItemContainerGenerator.ContainerFromIndex(1)).Visibility);
                var closedFrame = new DispatcherFrame();
                combo.DropDownClosed += (_, _) => closedFrame.Continue = false;
                combo.IsDropDownOpen = false;
                if (closedFrame.Continue) System.Windows.Threading.Dispatcher.PushFrame(closedFrame);
                Assert.Equal(string.Empty, search.Text);
                Assert.Equal(Visibility.Visible, ((ComboBoxItem)combo.ItemContainerGenerator.ContainerFromIndex(0)).Visibility);
                Assert.Equal("System", combo.SelectedItem);
            }
            finally { window.Close(); }
        });
    }
}

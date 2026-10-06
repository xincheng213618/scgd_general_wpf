using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class CvRawFileCacheSettingsTests
{
    [Fact]
    public void GlobalOptionsAndCacheManagerSharePersistedSettingsAndStartupRestoresThem()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cvraw-cache-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "config.json");
        IConfigService previous = ConfigService.Instance;
        int previousMaximumEntries = CVFileReadCache.MaximumEntries;
        ConfigService.SetInstance(new ConfigHandler { ConfigFilePath = path });
        CVFileReadCache.IsEnabled = true;
        CVFileReadCache.MaximumEntries = 1;
        LocalCalibrationCacheManagerWindow? window = null;
        ToggleButton? option = null;
        TextBox? countOption = null;
        try
        {
            Assert.Equal(1, CvRawFileCacheConfig.Current.MaximumEntries);
            window = WpfTestHost.Invoke(() =>
            {
                EnsurePropertyEditorResources();
                return new LocalCalibrationCacheManagerWindow();
            });
            WaitUntilIdle(window);
            WpfTestHost.Invoke(() =>
            {
                var modules = (DataGrid)window.FindName("CacheModulesGrid");
                Assert.Equal(2, modules.Items.Count);
                modules.SelectedItem = modules.Items.Cast<object>().Single(item =>
                    (string)item.GetType().GetProperty("Id")!.GetValue(item)! == "ImageFile");
                Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("ImageCacheOptionsPanel")).Visibility);
                var checkbox = (CheckBox)window.FindName("ImageCacheEnabledCheckBox");
                var property = typeof(CvRawFileCacheConfig).GetProperty(nameof(CvRawFileCacheConfig.IsEnabled))!;
                var registration = property.GetCustomAttribute<ConfigSettingAttribute>();
                Assert.NotNull(registration);
                Assert.Equal(ConfigSettingConstants.Universal, registration.Group);
                Assert.Equal(ConfigSettingConstants.SectionFileArchive, registration.Section);
                // Global options use the shared property-editor path and the persisted singleton.
                DockPanel editor = PropertyEditorHelper.GenProperties(property, CvRawFileCacheConfig.Current);
                option = Assert.Single(editor.Children.OfType<ToggleButton>());
                var countProperty = typeof(CvRawFileCacheConfig).GetProperty(nameof(CvRawFileCacheConfig.MaximumEntries))!;
                var countRegistration = countProperty.GetCustomAttribute<ConfigSettingAttribute>();
                Assert.NotNull(countRegistration);
                Assert.Equal(ConfigSettingConstants.Universal, countRegistration.Group);
                Assert.Equal(ConfigSettingConstants.SectionFileArchive, countRegistration.Section);
                countOption = Assert.Single(PropertyEditorHelper.GenProperties(countProperty, CvRawFileCacheConfig.Current).Children.OfType<TextBox>());
                countOption.SetCurrentValue(TextBox.TextProperty, "2");
                countOption.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.Equal(2, CVFileReadCache.MaximumEntries);
                Assert.Equal("2", ((TextBox)window.FindName("ImageCacheCountTextBox")).Text);
                Assert.True(checkbox.IsChecked);
                Assert.True(option.IsChecked);
                option.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
                ConfigService.Instance.SaveConfigs(); // The options dialog saves on close.
                Assert.False(checkbox.IsChecked);
                Assert.False(option.IsChecked);
                Assert.False(CVFileReadCache.IsEnabled);
            });
            WaitUntilIdle(window);
            Assert.False(ReadSavedSetting(path));
            Assert.Equal(2, ReadSavedCount(path));
            WpfTestHost.Invoke(() =>
            {
                var checkbox = (CheckBox)window.FindName("ImageCacheEnabledCheckBox");
                checkbox.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                checkbox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            });
            WaitUntilIdle(window);
            WpfTestHost.Invoke(() =>
            {
                Assert.True(option!.IsChecked);
                Assert.True(CVFileReadCache.IsEnabled);
            });
            Assert.True(ReadSavedSetting(path));
            WpfTestHost.Invoke(() =>
            {
                ((TextBox)window.FindName("ImageCacheCountTextBox")).SetCurrentValue(TextBox.TextProperty, "3");
                ((Button)window.FindName("ApplyImageCacheCountButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            });
            WaitUntilIdle(window);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(3, CVFileReadCache.MaximumEntries);
                Assert.Equal("3", countOption!.Text);
                ((TextBox)window.FindName("ImageCacheCountTextBox")).SetCurrentValue(TextBox.TextProperty, "0");
                ((Button)window.FindName("ApplyImageCacheCountButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Equal(3, CVFileReadCache.MaximumEntries);
                Assert.Equal(3, CvRawFileCacheConfig.Current.MaximumEntries);
            });
            Assert.Equal(3, ReadSavedCount(path));
            WpfTestHost.Invoke(() => { option!.SetCurrentValue(ToggleButton.IsCheckedProperty, false); ConfigService.Instance.SaveConfigs(); });
            WaitUntilIdle(window);
            WpfTestHost.Invoke(() => window.Close());
            window = null;

            // Simulate a new process whose native cache starts enabled before loading saved settings.
            CVFileReadCache.IsEnabled = true;
            CVFileReadCache.MaximumEntries = 1;
            ConfigHandler restarted = new() { ConfigFilePath = path };
            restarted.LoadConfigs();
            ConfigService.SetInstance(restarted);
            new CvRawFileCacheInitializer().InitializeAsync().GetAwaiter().GetResult();
            Assert.False(CvRawFileCacheConfig.Current.IsEnabled);
            Assert.False(CVFileReadCache.IsEnabled);
            Assert.Equal(3, CvRawFileCacheConfig.Current.MaximumEntries);
            Assert.Equal(3, CVFileReadCache.MaximumEntries);
        }
        finally
        {
            if (window != null) { WaitUntilIdle(window); WpfTestHost.Invoke(() => window.Close()); }
            ConfigService.SetInstance(previous);
            CVFileReadCache.IsEnabled = true;
            CVFileReadCache.Release();
            CVFileReadCache.MaximumEntries = previousMaximumEntries;
            foreach (string file in Directory.EnumerateFiles(root)) File.Delete(file);
            Directory.Delete(root);
        }
    }

    private static bool ReadSavedSetting(string path)
        => Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path))[typeof(CvRawFileCacheConfig).FullName!]![nameof(CvRawFileCacheConfig.IsEnabled)]!.ToObject<bool>();

    private static int ReadSavedCount(string path)
        => Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path))[typeof(CvRawFileCacheConfig).FullName!]![nameof(CvRawFileCacheConfig.MaximumEntries)]!.ToObject<int>();

    private static void EnsurePropertyEditorResources()
    {
        Application application = Application.Current;
        Dictionary<string, object> defaults = new()
        {
            ["GlobalTextBrush"] = Brushes.Black,
            ["GlobalBorderBrush"] = Brushes.Transparent,
            ["BorderBrush"] = Brushes.Gray,
            ["ButtonCommand"] = new Style(typeof(Button)),
            ["TextBox.Small"] = new Style(typeof(TextBox)),
            ["ComboBox.Small"] = new Style(typeof(ComboBox)),
            ["bool2VisibilityConverter"] = new BooleanToVisibilityConverter(),
        };
        foreach ((string key, object value) in defaults)
            if (application.TryFindResource(key) == null) application.Resources[key] = value;
    }

    private static void WaitUntilIdle(LocalCalibrationCacheManagerWindow window)
    {
        FieldInfo busy = typeof(LocalCalibrationCacheManagerWindow).GetField("isBusy", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.True(SpinWait.SpinUntil(() => WpfTestHost.Invoke(() => !(bool)busy.GetValue(window)!), TimeSpan.FromSeconds(10)));
    }

}

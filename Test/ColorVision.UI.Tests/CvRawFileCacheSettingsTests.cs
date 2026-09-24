using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.UI.Tests;

public sealed class CvRawFileCacheSettingsTests
{
    [Fact]
    public void GlobalOptionsAndCacheTabSharePersistedSettingAndStartupRestoresIt()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cvraw-cache-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "config.json");
        IConfigService previous = ConfigService.Instance;
        ConfigService.SetInstance(new ConfigHandler { ConfigFilePath = path });
        CVFileReadCache.IsEnabled = true;
        LocalCalibrationCacheManagerWindow? window = null;
        ToggleButton? option = null;
        try
        {
            window = WpfTestHost.Invoke(() => new LocalCalibrationCacheManagerWindow());
            WaitUntilIdle(window);
            WpfTestHost.Invoke(() =>
            {
                var checkbox = (CheckBox)window.FindName("ImageCacheEnabledCheckBox");
                var property = typeof(CvRawFileCacheConfig).GetProperty(nameof(CvRawFileCacheConfig.IsEnabled))!;
                var registration = property.GetCustomAttribute<ConfigSettingAttribute>();
                Assert.NotNull(registration);
                Assert.Equal(ConfigSettingConstants.Universal, registration.Group);
                Assert.Equal(ConfigSettingConstants.SectionFileArchive, registration.Section);
                // Global options use the shared property-editor path and the persisted singleton.
                DockPanel editor = PropertyEditorHelper.GenProperties(property, CvRawFileCacheConfig.Current);
                option = Assert.Single(editor.Children.OfType<ToggleButton>());
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
            WpfTestHost.Invoke(() =>
            {
                var checkbox = (CheckBox)window.FindName("ImageCacheEnabledCheckBox");
                checkbox.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                checkbox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.True(option!.IsChecked);
                Assert.True(CVFileReadCache.IsEnabled);
            });
            WaitUntilIdle(window);
            Assert.True(ReadSavedSetting(path));
            WpfTestHost.Invoke(() => { option!.SetCurrentValue(ToggleButton.IsCheckedProperty, false); ConfigService.Instance.SaveConfigs(); });
            WaitUntilIdle(window);
            WpfTestHost.Invoke(() => window.Close());
            window = null;

            // Simulate a new process whose native cache starts enabled before loading saved settings.
            CVFileReadCache.IsEnabled = true;
            ConfigHandler restarted = new() { ConfigFilePath = path };
            restarted.LoadConfigs();
            ConfigService.SetInstance(restarted);
            new CvRawFileCacheInitializer().InitializeAsync().GetAwaiter().GetResult();
            Assert.False(CvRawFileCacheConfig.Current.IsEnabled);
            Assert.False(CVFileReadCache.IsEnabled);
        }
        finally
        {
            if (window != null) { WaitUntilIdle(window); WpfTestHost.Invoke(() => window.Close()); }
            ConfigService.SetInstance(previous);
            CVFileReadCache.IsEnabled = true;
            CVFileReadCache.Release();
            foreach (string file in Directory.EnumerateFiles(root)) File.Delete(file);
            Directory.Delete(root);
        }
    }

    private static bool ReadSavedSetting(string path)
        => Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path))[typeof(CvRawFileCacheConfig).FullName!]![nameof(CvRawFileCacheConfig.IsEnabled)]!.ToObject<bool>();

    private static void WaitUntilIdle(LocalCalibrationCacheManagerWindow window)
    {
        FieldInfo busy = typeof(LocalCalibrationCacheManagerWindow).GetField("isBusy", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.True(SpinWait.SpinUntil(() => WpfTestHost.Invoke(() => !(bool)busy.GetValue(window)!), TimeSpan.FromSeconds(10)));
    }

}

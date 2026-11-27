using ColorVision.Engine.Media;
using ColorVision.Core;
using ColorVision.Engine.Services.Caches;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class CvRawFileCacheSettingsTests
{
    [Fact]
    public void CalibrationBudgetEditsRefreshOnlyTheSelectedModuleAndAcceptZero()
    {
        ulong original = OpenCVCalibration.GetCalibrationSharedCacheStatistics().BudgetBytes;
        IConfigService previous = ConfigService.Instance;
        DirectoryInfo directory = Directory.CreateTempSubdirectory("CalibrationBudgetUI-");
        string path = Path.Combine(directory.FullName, "config.json");
        TestEnvironment.SetConfig(new ConfigHandler { ConfigFilePath = path });
        var window = WpfTestHost.Invoke(() => new LocalCalibrationCacheManagerWindow());
        try
        {
            WaitUntilIdle(window);
            var nav = WpfTestHost.Invoke(() => (ListBox)window.FindName("CacheNavigation"));
            var page = WpfTestHost.Invoke(() => nav.Items.Cast<CacheNavigationItem>().Single(item => item.Id == "Calibration"));
            WpfTestHost.Invoke(() =>
            {
                nav.SelectedItem = page;
                TextBox input = (TextBox)window.FindName("CalibrationBudgetTextBox");
                Button apply = (Button)window.FindName("ApplyCalibrationBudgetButton");
                Assert.Equal((original >> 20).ToString(), input.Text);
                input.Text = "-1";
                apply.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Equal(original, OpenCVCalibration.GetCalibrationSharedCacheStatistics().BudgetBytes);
                Assert.Contains("MiB", ((TextBlock)window.FindName("StatusText")).Text);
                input.Text = "0";
                apply.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("LoadingOverlay")).Visibility);
                Assert.True(nav.IsEnabled);
            });
            WaitUntilIdle(window);
            Assert.Equal(0UL, OpenCVCalibration.GetCalibrationSharedCacheStatistics().BudgetBytes);
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(page, nav.SelectedItem);
                Assert.Equal("0", ((TextBox)window.FindName("CalibrationBudgetTextBox")).Text);
                Assert.Equal(Visibility.Collapsed, ((ProgressBar)window.FindName("CalibrationBudgetUsageBar")).Visibility);
            });
            Assert.Equal(0, Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path))[typeof(LocalCalibrationWarmupConfig).FullName!]!["MemoryBudgetMiB"]!.ToObject<int>());
        }
        finally
        {
            WaitUntilIdle(window);
            WpfTestHost.Invoke(() => window.Close());
            OpenCVCalibration.SetCalibrationSharedCacheBudget(original);
            TestEnvironment.SetConfig(previous);
            foreach (string file in Directory.EnumerateFiles(directory.FullName)) File.Delete(file);
            directory.Delete();
        }
    }

    [Fact]
    public void BulkReleaseKeepsExplicitModuleScopeAcrossSearchAndRefresh()
    {
        LocalCalibrationCacheManagerWindow window = WpfTestHost.Invoke(() => new LocalCalibrationCacheManagerWindow());
        WaitUntilIdle(window);
        try
        {
            WpfTestHost.Invoke(() =>
            {
                MethodInfo apply = typeof(LocalCalibrationCacheManagerWindow).GetMethod("ApplySnapshots", BindingFlags.NonPublic | BindingFlags.Instance)!;
                MethodInfo readTargets = typeof(LocalCalibrationCacheManagerWindow).GetMethod("GetMarkedModules", BindingFlags.NonPublic | BindingFlags.Instance)!;
                CacheModuleSnapshot[] snapshots = CacheManagerService.Modules.Select(module => new CacheModuleSnapshot(
                    module.Id, module.Name, module.Description, 64, 1, 0, 0, 0, true, false, "", [])).ToArray();
                apply.Invoke(window, [snapshots]);
                DataGrid grid = (DataGrid)window.FindName("CacheModulesGrid");
                TextBox search = (TextBox)window.FindName("CacheSearchBox");
                Button release = (Button)window.FindName("ReleaseMarkedButton");
                Assert.False(release.IsEnabled);
                grid.Items.Cast<CacheModuleViewItem>().Single(item => item.Id == "Snapshot").IsMarkedForRelease = true;
                Assert.True(release.IsEnabled);

                // Filtering must never replace the user's chosen target with the visible row.
                search.Text = snapshots.Single(item => item.Id == "ImageFile").Name;
                Assert.Single(grid.Items.Cast<CacheModuleViewItem>());
                Assert.Equal("Snapshot", Assert.Single((ICacheModule[])readTargets.Invoke(window, null)!).Id);
                apply.Invoke(window, [snapshots]);
                Assert.Equal("Snapshot", Assert.Single((ICacheModule[])readTargets.Invoke(window, null)!).Id);
                Assert.NotEmpty(((TextBlock)window.FindName("ReleaseSelectionText")).Text);

                grid.Items.Cast<CacheModuleViewItem>().Single().IsMarkedForRelease = true;
                Assert.Equal(new[] { "Snapshot", "ImageFile" }, ((ICacheModule[])readTargets.Invoke(window, null)!).Select(module => module.Id));
                search.Clear();
                Assert.False(grid.Items.Cast<CacheModuleViewItem>().Single(item => item.Id == "Calibration").IsMarkedForRelease);
                foreach (CacheModuleViewItem item in grid.Items) item.IsMarkedForRelease = false;
                Assert.False(release.IsEnabled);
            });
        }
        finally { WpfTestHost.Invoke(() => window.Close()); }
    }

    [Fact]
    public void GlobalOptionsAndCacheManagerSharePersistedSettingsAndStartupRestoresThem()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cvraw-cache-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "config.json");
        IConfigService previous = ConfigService.Instance;
        bool previousEnabled = CVFileReadCache.IsEnabled;
        int previousMaximumEntries = CVFileReadCache.MaximumEntries;
        TestEnvironment.SetConfig(new ConfigHandler { ConfigFilePath = path });
        CVFileReadCache.IsEnabled = false;
        CVFileReadCache.MaximumEntries = 1;
        LocalCalibrationCacheManagerWindow? window = null;
        ToggleButton? option = null;
        TextBox? countOption = null;
        try
        {
            Assert.Equal(1, CvRawFileCacheConfig.Current.MaximumEntries);
            Assert.True(CvRawFileCacheConfig.Current.IsEnabled);
            new CvRawFileCacheInitializer().InitializeAsync().GetAwaiter().GetResult();
            Assert.True(CVFileReadCache.IsEnabled);
            window = WpfTestHost.Invoke(() =>
            {
                EnsurePropertyEditorResources();
                return new LocalCalibrationCacheManagerWindow();
            });
            WaitUntilIdle(window);
            WpfTestHost.Invoke(() =>
            {
                var modules = (ListBox)window.FindName("CacheNavigation");
                Assert.Equal(5, modules.Items.Count); // Overview and the four cache modules.
                Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("OverviewPanel")).Visibility);
                Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("ImageCacheOptionsPanel")).Visibility);
                Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("CameraCacheOptionsPanel")).Visibility);
                Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("CalibrationWarmupOptionsPanel")).Visibility);
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
            var moduleGrid = WpfTestHost.Invoke(() => (DataGrid)window.FindName("CacheModulesGrid"));
            var oldModules = WpfTestHost.Invoke(() => moduleGrid.Items.Cast<CacheModuleViewItem>().ToDictionary(item => item.Id));
            var originalPage = WpfTestHost.Invoke(() => ((ListBox)window.FindName("CacheNavigation")).SelectedItem);
            WpfTestHost.Invoke(() =>
            {
                var checkbox = (CheckBox)window.FindName("ImageCacheEnabledCheckBox");
                checkbox.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                checkbox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("LoadingOverlay")).Visibility);
                Assert.True(((FrameworkElement)window.FindName("CacheContentPanel")).IsEnabled);
            });
            WaitUntilIdle(window);
            WpfTestHost.Invoke(() =>
            {
                Assert.True(option!.IsChecked);
                Assert.True(CVFileReadCache.IsEnabled);
                Assert.Same(originalPage, ((ListBox)window.FindName("CacheNavigation")).SelectedItem);
                foreach (var row in moduleGrid.Items.Cast<CacheModuleViewItem>()) Assert.Same(oldModules[row.Id], row);
                Assert.True(oldModules["ImageFile"].Snapshot.IsEnabled);
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
                var navigation = (ListBox)window.FindName("CacheNavigation");
                navigation.SelectedItem = navigation.Items.Cast<object>().Single(item =>
                    (string)item.GetType().GetProperty("Id")!.GetValue(item)! == "ImageFile");
                Assert.True(((CheckBox)window.FindName("ImageCacheEnabledCheckBox")).IsChecked);
                Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("ImageCacheOptionsPanel")).Visibility);
                Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("CameraCacheOptionsPanel")).Visibility);
                navigation.SelectedItem = navigation.Items.Cast<CacheNavigationItem>().Single(item => item.Id == "Calibration");
                Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("CalibrationWarmupOptionsPanel")).Visibility);
                var warmup = (CheckBox)window.FindName("CalibrationWarmupCheckBox");
                Assert.True(warmup.IsChecked);
                warmup.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
                warmup.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.False(LocalCalibrationWarmupHistory.Enabled);
            });
            Assert.Equal(3, ReadSavedCount(path));
            object selectedPage = WpfTestHost.Invoke(() => ((ListBox)window.FindName("CacheNavigation")).SelectedItem);
            object details = WpfTestHost.Invoke(() => ((DataGrid)window.FindName("CacheDetailsGrid")).ItemsSource);
            WpfTestHost.Invoke(() => { option!.SetCurrentValue(ToggleButton.IsCheckedProperty, false); ConfigService.Instance.SaveConfigs(); });
            WaitUntilIdle(window);
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(selectedPage, ((ListBox)window.FindName("CacheNavigation")).SelectedItem);
                Assert.Same(details, ((DataGrid)window.FindName("CacheDetailsGrid")).ItemsSource);
                window.Close();
            });
            window = null;

            // Simulate a new process whose native cache starts enabled before loading saved settings.
            CVFileReadCache.IsEnabled = true;
            CVFileReadCache.MaximumEntries = 1;
            ConfigHandler restarted = new() { ConfigFilePath = path };
            restarted.LoadConfigs();
            TestEnvironment.SetConfig(restarted);
            new CvRawFileCacheInitializer().InitializeAsync().GetAwaiter().GetResult();
            Assert.False(CvRawFileCacheConfig.Current.IsEnabled);
            Assert.False(CVFileReadCache.IsEnabled);
            Assert.Equal(3, CvRawFileCacheConfig.Current.MaximumEntries);
            Assert.Equal(3, CVFileReadCache.MaximumEntries);
        }
        finally
        {
            if (window != null) { WaitUntilIdle(window); WpfTestHost.Invoke(() => window.Close()); }
            TestEnvironment.SetConfig(previous);
            CVFileReadCache.IsEnabled = previousEnabled;
            CVFileReadCache.Release();
            CVFileReadCache.MaximumEntries = previousMaximumEntries;
            foreach (string file in Directory.EnumerateFiles(root)) File.Delete(file);
            Directory.Delete(root);
        }
    }

    [Fact]
    public void CameraCacheDefaultsOnAndDisablingRetiresIdleAndReturnedBuffersAcrossExistingAndNewPools()
    {
        string root = Path.Combine(Path.GetTempPath(), $"camera-cache-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "config.json");
        IConfigService previous = ConfigService.Instance;
        bool previousEnabled = LocalCameraRawBufferPool.IsCacheEnabled;
        TestEnvironment.SetConfig(new ConfigHandler { ConfigFilePath = path });
        LocalCalibrationCacheManagerWindow? window = null;
        try
        {
            Assert.True(CameraRawBufferCacheConfig.Current.IsEnabled);
            LocalCameraRawBufferPool.IsCacheEnabled = false;
            new CameraRawBufferCacheInitializer().InitializeAsync().GetAwaiter().GetResult();
            Assert.True(LocalCameraRawBufferPool.IsCacheEnabled);
            using var existingPool = new LocalCameraRawBufferPool();
            using (var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 64, 0, existingPool)) { }
            Assert.Equal(64, existingPool.IdleBytes);

            window = WpfTestHost.Invoke(() => new LocalCalibrationCacheManagerWindow());
            WaitUntilIdle(window);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("CameraCacheOptionsPanel")).Visibility);
                Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("ImageCacheOptionsPanel")).Visibility);
                var checkbox = (CheckBox)window.FindName("CameraCacheEnabledCheckBox");
                Assert.True(checkbox.IsChecked);
            });
            WaitUntilIdle(window);
            Assert.True(LocalCameraRawBufferPool.IsCacheEnabled);
            using var newPool = new LocalCameraRawBufferPool();
            using var borrowed = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 64, 0, existingPool);
            using var lease = borrowed.Acquire();
            Marshal.WriteByte(lease.RawPointer, 63, 37);
            borrowed.Dispose();
            using (var idle = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 64, 0, existingPool)) { }
            using (var idle = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 128, 0, newPool)) { }
            Assert.Equal(64, existingPool.IdleBytes);
            Assert.Equal(128, newPool.IdleBytes);

            WpfTestHost.Invoke(() =>
            {
                var navigation = (ListBox)window.FindName("CacheNavigation");
                navigation.SelectedItem = navigation.Items.Cast<object>().Single(item =>
                    (string)item.GetType().GetProperty("Id")!.GetValue(item)! == "CameraRawBuffer");
                Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("ImageCacheOptionsPanel")).Visibility);
                var checkbox = (CheckBox)window.FindName("CameraCacheEnabledCheckBox");
                checkbox.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
                checkbox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("LoadingOverlay")).Visibility);
                Assert.True(((FrameworkElement)window.FindName("CacheContentPanel")).IsEnabled);
            });
            WaitUntilIdle(window);
            Assert.False(ReadSavedCameraSetting(path));
            Assert.False(LocalCameraRawBufferPool.IsCacheEnabled);
            Assert.Equal(0, existingPool.IdleBytes);
            Assert.Equal(0, newPool.IdleBytes);
            Assert.Equal(37, Marshal.ReadByte(lease.RawPointer, 63));
            lease.Dispose();
            Assert.Equal(0, existingPool.IdleBytes);
            using (var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 128, 0, newPool)) { }
            Assert.Equal(0, newPool.IdleBytes);
            WpfTestHost.Invoke(() => window.Close());
            window = null;

            foreach (bool enabled in new[] { false, true })
            {
                CameraRawBufferCacheConfig.Current.IsEnabled = enabled;
                CameraRawBufferCacheConfig.SaveCurrent();
                LocalCameraRawBufferPool.IsCacheEnabled = !enabled;
                ConfigHandler restarted = new() { ConfigFilePath = path };
                restarted.LoadConfigs();
                TestEnvironment.SetConfig(restarted);
                new CameraRawBufferCacheInitializer().InitializeAsync().GetAwaiter().GetResult();
                Assert.Equal(enabled, CameraRawBufferCacheConfig.Current.IsEnabled);
                Assert.Equal(enabled, LocalCameraRawBufferPool.IsCacheEnabled);
            }
        }
        finally
        {
            if (window != null) { WaitUntilIdle(window); WpfTestHost.Invoke(() => window.Close()); }
            TestEnvironment.SetConfig(previous);
            LocalCameraRawBufferPool.IsCacheEnabled = previousEnabled;
            foreach (string file in Directory.EnumerateFiles(root)) File.Delete(file);
            Directory.Delete(root);
        }
    }

    private static bool ReadSavedCameraSetting(string path)
        => Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path))[typeof(CameraRawBufferCacheConfig).FullName!]![nameof(CameraRawBufferCacheConfig.IsEnabled)]!.ToObject<bool>();

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
        Assert.True(SpinWait.SpinUntil(() =>
        {
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            return WpfTestHost.Invoke(() => !(bool)busy.GetValue(window)!
                && ((FrameworkElement)window.FindName("ImageCacheOptionsPanel")).IsEnabled
                && ((FrameworkElement)window.FindName("CameraCacheOptionsPanel")).IsEnabled
                && ((FrameworkElement)window.FindName("CalibrationBudgetPanel")).IsEnabled);
        }, TimeSpan.FromSeconds(10)));
    }

}

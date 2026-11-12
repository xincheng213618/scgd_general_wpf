using ColorVision.Settings.Maintenance;
using ColorVision.Themes;
using ColorVision.UI.Desktop.Diagnostics;
using ColorVision.UI.Desktop.Settings;
using ColorVision.Common.MVVM;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class ConfigurationMaintenanceTests
{
    [Theory]
    [InlineData("zh-Hans", "配置")]
    [InlineData("en-US", "Configuration")]
    [InlineData("zh-Hant", "設定")]
    public void ConfigurationAndDumpShareOneStableNavigationGroupWithoutConstructingPages(string culture, string expectedTitle)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            ConfigSettingMetadata configuration = Assert.Single(new ConfigurationMaintenanceSettingsProvider().GetConfigSettings());
            ConfigSettingMetadata dump = Assert.Single(new CrashDumpSettingsProvider().GetConfigSettings());
            var entries = SettingEntryCatalog.Create([configuration, dump]);
            Assert.Equal(2, entries.Count);
            Assert.Single(entries.Select(entry => entry.Group).Distinct());
            Assert.All(entries, entry =>
            {
                Assert.Equal(ConfigSettingConstants.Configuration, entry.Group);
                Assert.Equal(expectedTitle, entry.GroupDisplayName);
                Assert.Null(entry.CustomContent);
            });
            string configurationId = entries[0].Id;
            configuration.Name = "Changed display label";
            Assert.Equal(configurationId, Assert.Single(SettingEntryCatalog.Create([configuration])).Id);

            ConfigSettingMetadata legacy = new() { Name = "Independent legacy tools", Type = ConfigSettingType.TabItem, ViewType = typeof(UserControl) };
            Assert.Equal(legacy.Name, Assert.Single(SettingEntryCatalog.Create([legacy])).Group);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public void IsolatedConfigurationPageKeepsResetWhitelistDefaultsAndBusyControlsWithoutFileScanning()
    {
        WpfTestHost.Invoke(() =>
        {
            var dictionaries = new List<ResourceDictionary>();
            ConfigurationMaintenanceControl? page = null;
            try
            {
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }
                page = new ConfigurationMaintenanceControl(configHandler: null);
                var host = new Grid();
                host.Children.Add(page);
                Layout(host, 800, 900);
                page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                var options = ((WrapPanel)page.FindName("ResetOptions")).Children.OfType<CheckBox>().ToArray();
                Assert.Equal(ConfigurationMaintenanceControl.ResetSectionNames.Order(),
                    options.SelectMany(option => ((string)option.Tag).Split(',')).Order());
                Assert.Equal(new[] { true, true, false, false, false }, options.Select(option => option.IsChecked == true));
                Assert.False(((Button)page.FindName("CancelResetButton")).IsEnabled);
                Assert.Null(page.FindName("FileManagementButton"));
                Assert.Null(page.FindName("MemoryManagementButton"));
                Assert.True(((Button)page.FindName("BackupButton")).IsEnabled);
                page.ViewModel.IsBusy = true;
                foreach (Border border in Descendants(page).OfType<Border>())
                    BindingOperations.GetBindingExpression(border, UIElement.IsEnabledProperty)?.UpdateTarget();
                Layout(host, 800, 900);
                Assert.False(((Button)page.FindName("BackupButton")).IsEnabled);
                Assert.False(((Button)page.FindName("ScheduleResetButton")).IsEnabled);
                Assert.Equal(string.Empty, page.ViewModel.Status);
            }
            finally
            {
                page?.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                foreach (ResourceDictionary dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            }
        });
    }

    [Fact]
    public void ConfigurationPageComposesResetAndDumpInsideOneNativeSettingsHost()
    {
        WpfTestHost.Invoke(() =>
        {
            CultureInfo previousCulture = CultureInfo.CurrentUICulture;
            ResourceDictionary resources = Application.Current.Resources;
            var dictionaries = new List<ResourceDictionary>();
            var previousLocalResources = new Dictionary<object, object>();
            SettingWindow? window = null;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    resources.MergedDictionaries.Add(dictionary);
                }
                foreach (object key in resources.Keys.Cast<object>().ToArray())
                {
                    if (!dictionaries.Any(dictionary => dictionary.Contains(key))) continue;
                    previousLocalResources[key] = resources[key];
                    resources.Remove(key);
                }
                ResourceDictionary appearance = new ThemeAppearance().CreateResources(Theme.Light, resources);
                foreach (object key in appearance.Keys.Cast<object>().ToArray())
                {
                    if (!resources.Keys.Cast<object>().Contains(key)) continue;
                    previousLocalResources[key] = resources[key];
                    resources.Remove(key);
                }
                resources.MergedDictionaries.Add(appearance);
                dictionaries.Add(appearance);
                ConfigSettingMetadata configuration = Assert.Single(new ConfigurationMaintenanceSettingsProvider().GetConfigSettings());
                configuration.ViewType = typeof(PreviewConfigurationPage);
                // This is a detached model and inert copy of the actions markup: no WER registry is read or changed.
                ConfigSettingMetadata dump = new()
                {
                    Name = "崩溃转储", Description = "配置 Windows Error Reporting 转储，并保存或收集当前进程的诊断文件。",
                    Group = ConfigSettingConstants.Configuration, Order = 870, Type = ConfigSettingType.Class,
                    Source = new PreviewDumpConfiguration(), ViewType = typeof(PreviewDumpActions)
                };
                window = new SettingWindow([configuration, dump]);
                var host = Assert.IsType<Grid>(window.Content);
                var navigation = (ListBox)window.FindName("NavigationListBox");
                Assert.Single(navigation.Items);
                NavigationEntry configurationNavigation = navigation.Items.Cast<NavigationEntry>().Single(entry => entry.Group == ConfigSettingConstants.Configuration);
                navigation.SelectedIndex = -1;
                navigation.SelectedItem = configurationNavigation;
                Layout(host, 1180, 1160);
                host.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Layout(host, 1180, 1160);
                Assert.Equal("配置", ((TextBlock)window.FindName("CurrentGroupTitle")).Text);
                var content = (StackPanel)window.FindName("SettingsContentPanel");
                Assert.Single(Descendants(content).OfType<PreviewConfigurationPage>());
                Assert.Single(Descendants(content).OfType<PreviewDumpActions>());
                Assert.Contains(Descendants(content).OfType<TextBlock>(), text => text.Text == "转储类型");
                Capture(host, "configuration-light-1180.png");
            }
            finally
            {
                window?.Close();
                CultureInfo.CurrentUICulture = previousCulture;
                for (int index = dictionaries.Count - 1; index >= 0; index--) resources.MergedDictionaries.Remove(dictionaries[index]);
                foreach ((object key, object value) in previousLocalResources) resources[key] = value;
            }
        });
    }

    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Capture(FrameworkElement content, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("STORAGE_RENDER_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (DrawingContext drawing = visual.RenderOpen())
        {
            Rect bounds = new(0, 0, content.ActualWidth, content.ActualHeight);
            Brush background = content.TryFindResource("CV.Surface.Sidebar") as Brush
                ?? content.TryFindResource("GlobalBackground") as Brush ?? Brushes.White;
            drawing.DrawRectangle(background, null, bounds);
            drawing.DrawRectangle(new VisualBrush(content), null, bounds);
        }
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name));
        encoder.Save(stream);
    }

    public sealed class PreviewConfigurationPage : UserControl
    {
        public PreviewConfigurationPage() => Content = new ConfigurationMaintenanceControl(configHandler: null);
    }

    public sealed class PreviewDumpConfiguration : ViewModelBase
    {
        [ConfigSetting(Order = 85, Name = "转储类型", Description = "选择 Windows Error Reporting 在程序崩溃时保存的转储内容。")]
        [PropertyEditorType(typeof(CrashDumpTypePropertiesEditor))]
        public CrashDumpType DumpType { get; set; } = CrashDumpType.Mini;
        [ConfigSetting(Order = 86, Name = "最多保留", Description = "最多保留的转储文件数量，允许 1 到 999。")]
        public int DumpCount { get; set; } = 10;
        [ConfigSetting(Order = 87, Name = "保存目录", Description = "转储文件的保存位置，必须使用绝对路径。", Layout = ConfigSettingLayout.Wide)]
        [PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
        public string DumpFolder { get; set; } = @"C:\ColorVision\Diagnostics\CrashDumps";
    }

    public sealed class PreviewDumpActions : UserControl
    {
        public PreviewDumpActions()
        {
            Content = XamlReader.Parse("""
                <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                  <Border CornerRadius="8" BorderThickness="1" Padding="18" Background="{DynamicResource GlobalBackground}" BorderBrush="{DynamicResource ButtonBorderBrush}">
                    <StackPanel>
                      <TextBlock Text="系统操作" FontSize="16" FontWeight="SemiBold" Foreground="{DynamicResource GlobalTextBrush}"/>
                      <TextBlock Text="通过 ColorVision Service Host 应用或清除系统设置。" Margin="0,5,0,14" FontSize="12.5" Opacity="0.68" TextWrapping="Wrap" Foreground="{DynamicResource GlobalTextBrush}"/>
                      <WrapPanel><Button Padding="14,6" Content="应用系统设置"/><Button Margin="8,0,0,0" Padding="14,6" Content="清除系统设置"/><Button Margin="8,0,0,0" Padding="14,6" Content="保存当前进程 Dump"/></WrapPanel>
                    </StackPanel>
                  </Border>
                  <Border Margin="0,12,0,0" CornerRadius="8" BorderThickness="1" Padding="14,10" Background="{DynamicResource GlobalBackground}" BorderBrush="{DynamicResource ButtonBorderBrush}">
                    <TextBlock Text="当前目标：ColorVision.exe。" FontSize="12.5" TextWrapping="Wrap" Foreground="{DynamicResource GlobalTextBrush}"/>
                  </Border>
                </StackPanel>
                """);
        }
    }
}

using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.UI.Desktop.Diagnostics;
using ColorVision.UI.Desktop.LanRemote;
using ColorVision.UI.Desktop.Marketplace;
using ColorVision.UI.Desktop.Settings;
using ColorVision.UI.Plugins;
using MQTTMessageLib.Algorithm;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class ScreenshotLocalizationTests
{
    [Theory]
    [InlineData("zh-Hans", "POI模板", "存储类型", "数据库", "文件")]
    [InlineData("en-US", "POI template", "Storage Type", "Database", "File")]
    [InlineData("zh-Hant", "POI範本", "儲存類型", "資料庫", "檔案")]
    public void PoiLabelsAndStorageOptionsPreserveEnumBinding(string culture, string template, string storage, string database, string file)
    {
        WithCulture(culture, () =>
        {
            var config = new PoiDisplayAlgorithmConfig();
            Assert.Equal(template, config.Template.DisplayName);
            var panel = Assert.IsType<StackPanel>(new DisplayAlgorithmConfigurationBuilder().Build(config));
            var row = Assert.Single(panel.Children.OfType<DockPanel>(), row => row.Children.OfType<TextBlock>().Any(label => label.Text == storage));
            var combo = Assert.Single(row.Children.OfType<ComboBox>());
            var options = combo.Items.Cast<KeyValuePair<Enum, string>>().ToArray();
            Assert.Equal(database, Assert.Single(options, option => Equals(option.Key, POIStorageModel.Db)).Value);
            Assert.Equal(file, Assert.Single(options, option => Equals(option.Key, POIStorageModel.File)).Value);
            Assert.Equal(POIStorageModel.Db, combo.SelectedValue);
            combo.SelectedValue = POIStorageModel.File;
            combo.GetBindingExpression(Selector.SelectedValueProperty)!.UpdateSource();
            Assert.Equal(POIStorageModel.File, config.StorageModel);
            Assert.Equal("1", Newtonsoft.Json.JsonConvert.SerializeObject(config.StorageModel));
            config.StorageModel = POIStorageModel.Db;
            combo.GetBindingExpression(Selector.SelectedValueProperty)!.UpdateTarget();
            Assert.Equal(POIStorageModel.Db, combo.SelectedValue);
        });
    }

    [Theory]
    [InlineData("zh-Hans", "崩溃转储", "转储类型", "启用 CVRAW 文件缓存", "文件归档", "启动时检查更新", "局域网控制")]
    [InlineData("en-US", "Crash dumps", "Dump type", "Enable CVRAW file cache", "File storage", "Check for updates on startup", "LAN control")]
    [InlineData("zh-Hant", "當機傾印", "傾印類型", "啟用 CVRAW 檔案快取", "檔案儲存", "啟動時檢查更新", "區域網路控制")]
    public void SettingsResolveUserFacingTitlesInsteadOfClrFallbacks(string culture, string crash, string dump, string cache, string section, string update, string lan)
    {
        WithCulture(culture, () =>
        {
            var metadata = Assert.Single(new CrashDumpSettingsProvider().GetConfigSettings());
            Assert.Equal(crash, SettingMetadataResolver.CreateEntry(metadata, null).Title);
            var config = new CrashDumpConfiguration("LocalizationTest.exe");
            var property = typeof(CrashDumpConfiguration).GetProperty(nameof(CrashDumpConfiguration.DumpType))!;
            var attribute = property.GetCustomAttribute<ConfigSettingAttribute>()!;
            var entry = SettingMetadataResolver.CreateEntry(new ConfigSettingMetadata
            {
                Name = attribute.Name, Description = attribute.Description, BindingName = property.Name, Source = config
            }, property);
            Assert.Equal(dump, entry.Title);
            Assert.False(string.IsNullOrWhiteSpace(entry.Description));
            var cacheEntry = SettingMetadataResolver.CreateEntry(new ConfigSettingMetadata
            {
                Source = new CvRawFileCacheConfig(), BindingName = nameof(CvRawFileCacheConfig.IsEnabled), Section = ConfigSettingConstants.SectionFileArchive
            }, typeof(CvRawFileCacheConfig).GetProperty(nameof(CvRawFileCacheConfig.IsEnabled)));
            Assert.Equal(cache, cacheEntry.Title);
            Assert.Equal(section, cacheEntry.SectionDisplayName);
            Assert.Equal(update, SettingResources.StartupCheckUpdates);
            var startup = Assert.Single(SettingEntryCatalog.Create(new[]
            {
                new ConfigSettingMetadata { Source = new ColorVision.Update.AutoUpdateConfig(), BindingName = "IsAutoUpdate" },
                new ConfigSettingMetadata { Source = new MarketplaceWindowConfig(), BindingName = "IsAutoUpdate" }
            }));
            Assert.Equal(update, startup.Title);
            Assert.Equal("setting:startup-check-updates", startup.Id);
            var lanEntry = SettingMetadataResolver.CreateEntry(new ConfigSettingMetadata
            {
                Name = "局域网控制", Type = ConfigSettingType.TabItem, Source = new LanRemoteControlConfig()
            }, null);
            Assert.Equal(lan, lanEntry.Title);
            Assert.NotEqual("SettingsSectionOther", SettingResources.SectionOther);
        });
    }

    [Fact]
    public void InstalledPluginTranslationPreservesManifestDataAndUnknownNames()
    {
        WithCulture("en-US", () =>
        {
            var manifest = new PluginManifest { Id = "WindowsServicePlugin", Name = "视彩服务插件", Description = "原始说明" };
            var info = new PluginInfo { Manifest = manifest, Name = manifest.Name, Description = manifest.Description };
            var view = new PluginInfoVM(info, skipIndividualCheck: true);
            Assert.Equal("ColorVision Service Plugin", view.Name);
            Assert.Equal("视彩服务插件", info.Name);
            Assert.Equal("原始说明", manifest.Description);
            var unknown = new PluginInfo { Manifest = new PluginManifest { Id = "CustomPlugin" }, Name = "用户插件名", Description = "用户说明" };
            var unknownView = new PluginInfoVM(unknown, skipIndividualCheck: true);
            Assert.Equal(unknown.Name, unknownView.Name);
            Assert.Equal(unknown.Description, unknownView.Description);
        });
    }

    private static void WithCulture(string culture, Action action)
    {
        WpfTestHost.Invoke(() =>
        {
            CultureInfo previous = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                Application.Current.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
                Application.Current.Resources["GlobalTextBrush"] = Brushes.Black;
                Application.Current.Resources["GlobalBorderBrush"] = Brushes.Gray;
                Application.Current.Resources["BorderBrush"] = Brushes.Gray;
                Application.Current.Resources["ButtonCommand"] = new Style(typeof(Button));
                Application.Current.Resources["TextBox.Small"] = new Style(typeof(TextBox));
                Application.Current.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
                action();
            }
            finally { CultureInfo.CurrentUICulture = previous; }
        });
    }
}

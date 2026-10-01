using ColorVision.Engine.Media;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForBlackScreen;
using ColorVision.UI.Desktop.Diagnostics;
using ColorVision.UI.Desktop.LanRemote;
using ColorVision.UI.Desktop.Marketplace;
using ColorVision.UI.Desktop.Settings;
using ColorVision.UI.Plugins;
using MQTTMessageLib.Algorithm;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class ScreenshotLocalizationTests
{
    [Theory]
    [InlineData("zh-Hans", "黑画面检测", "黑画面检测模板", "关注点模板", "就绪")]
    [InlineData("en-US", "Black Screen Detection", "Black Screen Template", "POI Template", "Ready")]
    [InlineData("zh-Hant", "黑畫面檢測", "黑畫面檢測範本", "關注點範本", "就緒")]
    public void AlgorithmPanelLocalizesLabelsAndKeepsTemplateIdentity(string culture, string algorithm, string template, string poi, string ready)
    {
        WithCulture(culture, () =>
        {
            var metadata = typeof(AlgorithmFPForBlackScreen).GetCustomAttribute<DisplayAlgorithmAttribute>()!;
            Assert.Equal(algorithm, metadata.DisplayName);
            Assert.Equal("黑画面检测", metadata.Name);
            Assert.Equal(57, metadata.Order);
            var sample = new PoiParam { Id = 42, Name = "客户黑画面模板" };
            var items = new[] { new KeyValuePair<string, PoiParam>(sample.Name, sample) };
            var primary = new DisplayAlgorithmTemplateSelection("黑画面检测模板", new TemplatePoi(), "请先选择黑画面检测模板", itemsSource: () => items);
            var secondary = new DisplayAlgorithmTemplateSelection("关注点模板", new TemplatePoi(), "请先选择关注点模板", itemsSource: () => items);
            var config = new DualTemplateDisplayAlgorithmConfig(primary, secondary);
            var panel = Assert.IsType<StackPanel>(new DisplayAlgorithmConfigurationBuilder().Build(config));
            var rows = panel.Children.OfType<Grid>().ToArray();
            Assert.Equal(new[] { template, poi }, rows.SelectMany(row => row.Children.OfType<TextBlock>()).Select(label => label.Text));
            Assert.All(rows.SelectMany(row => row.Children.OfType<TextBlock>()), label => Assert.Equal(TextWrapping.Wrap, label.TextWrapping));
            var combo = Assert.Single(rows[0].Children.OfType<ComboBox>());
            Assert.Equal(0, combo.SelectedIndex);
            Assert.Same(sample, primary.SelectedValue);
            Assert.Equal("客户黑画面模板", primary.SelectedName);
            Assert.Equal(42, sample.Id);
            Assert.Equal(ready, new ColorVision.Solution.SolutionManager(restoreLastWorkspace: false).WorkspaceOpenStatus);
        });
    }

    [Fact]
    public void EnglishAlgorithmCatalogAndTemplatePromptsDoNotFallBackToChinese()
    {
        WithCulture("en-US", () =>
        {
            var algorithms = typeof(DisplayAlgorithmAttribute).Assembly.GetTypes()
                .Select(type => type.GetCustomAttribute<DisplayAlgorithmAttribute>())
                .Where(attribute => attribute != null).Cast<DisplayAlgorithmAttribute>().ToArray();
            Assert.NotEmpty(algorithms);
            Assert.All(algorithms, attribute => Assert.DoesNotMatch("[\\u3400-\\u9fff]", attribute.DisplayName));
            Assert.Equal("Emitting Area Location", Assert.Single(algorithms, attribute => attribute.Name == "EmittingAreaLocation,").DisplayName);
            var selection = new DisplayAlgorithmTemplateSelection("黑画面检测模板", new TemplatePoi(), "请先选择黑画面检测模板", itemsSource: () => Array.Empty<object>());
            Assert.False(selection.IsSelectionValid());
            Assert.Equal("Select a black screen template first", selection.ValidationMessage);
        });
    }

    [Theory]
    [InlineData("zh-Hans", "加载图片")]
    [InlineData("en-US", "Load Image")]
    [InlineData("zh-Hant", "載入圖片")]
    public void SavedDefaultImageTitleUsesCurrentLanguageWithoutRewritingSavedOrCustomTitles(string culture, string displayTitle)
    {
        WithCulture(culture, () =>
        {
            var node = new LocalImageNode();
            node.Create();
            foreach (string savedTitle in new[] { "加载图片", "Load Image", "載入圖片" })
            {
                node.OnLoadNode(new Dictionary<string, byte[]> { ["Title"] = Encoding.UTF8.GetBytes(savedTitle) });
                byte[] before = node.GetSaveData();
                Assert.Equal(displayTitle, node.OnGetDrawTitle());
                Assert.Equal(savedTitle, node.Title);
                Assert.Equal(before, node.GetSaveData());
            }
            node.Title = "客户自定义图片节点";
            Assert.Equal("客户自定义图片节点", node.OnGetDrawTitle());
        });
    }

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

using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Desktop.Settings;
using ColorVision.UI.Languages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class StartupThemeSettingsTests
{
    [Fact]
    public void ExistingConfigurationDefaultsToDarkAndEachPolicyRoundTripsWithoutChangingApplicationTheme()
    {
        var legacy = Assert.IsType<ThemeConfig>(JsonConvert.DeserializeObject<ThemeConfig>("{\"Theme\":1}"));
        Assert.Equal(Theme.Light, legacy.Theme);
        Assert.Equal(StartupTheme.Dark, legacy.StartupTheme);
        Assert.Equal(StartupTheme.Dark, new ThemeConfig().StartupTheme);
        foreach (StartupTheme policy in Enum.GetValues<StartupTheme>())
        {
            legacy.StartupTheme = policy;
            string json = JsonConvert.SerializeObject(legacy);
            Assert.Equal((int)policy, (int)JObject.Parse(json)[nameof(ThemeConfig.StartupTheme)]!);
            var restored = Assert.IsType<ThemeConfig>(JsonConvert.DeserializeObject<ThemeConfig>(json));
            Assert.Equal(policy, restored.StartupTheme);
            Assert.Equal(Theme.Light, restored.Theme);
        }
        foreach (int invalid in new[] { -1, 99 })
        {
            legacy.StartupTheme = (StartupTheme)invalid;
            Assert.Equal(StartupTheme.Dark, legacy.StartupTheme);
            var restored = Assert.IsType<ThemeConfig>(JsonConvert.DeserializeObject<ThemeConfig>($"{{\"StartupTheme\":{invalid}}}"));
            Assert.Equal(StartupTheme.Dark, restored.StartupTheme);
        }
    }

    [Theory]
    [InlineData("zh-CN", "启动页主题", "深色", "浅色", "跟随软件主题")]
    [InlineData("en-US", "Startup screen theme", "Dark", "Light", "Follow application theme")]
    [InlineData("zh-TW", "啟動頁主題", "深色", "淺色", "跟隨軟體主題")]
    public void RealSettingsRowUsesLocalizedStandardComboAndIndexesBetweenThemeAndLanguage(
        string cultureName, string expectedTitle, string dark, string light, string follow)
    {
        WpfTestHost.Invoke(() =>
        {
            Application application = Application.Current;
            ResourceDictionary previousResources = application.Resources;
            Window? previousMainWindow = application.MainWindow;
            ThemeManager previousManager = ThemeManager.Current;
            IConfigService? previousConfig = ConfigService.Instance;
            CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
            CultureInfo previousFormatCulture = CultureInfo.CurrentCulture;
            var manager = new ThemeManager();
            SettingWindow? window = null;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                application.Resources = new ResourceDictionary();
                ThemeManager.Current = manager;
                var config = new StartupThemeTestConfigService();
                config.Theme.Theme = Theme.Dark;
                ConfigService.SetInstance(config);
                manager.ApplyTheme(application, Theme.Dark);
                ResourceDictionary[] originalPalette = application.Resources.MergedDictionaries.ToArray();

                ConfigSettingMetadata startup = Metadata(config.Theme, nameof(ThemeConfig.StartupTheme));
                ConfigSettingMetadata[] appearance =
                [
                    Metadata(new LanguageConfig(), nameof(LanguageConfig.UICulture)),
                    startup,
                    Metadata(config.Theme, nameof(ThemeConfig.Theme))
                ];
                Assert.All(appearance, setting => Assert.Equal(ConfigSettingConstants.SectionAppearance, setting.Section));
                Assert.Equal(new[] { nameof(ThemeConfig.Theme), nameof(ThemeConfig.StartupTheme), nameof(LanguageConfig.UICulture) },
                    appearance.OrderBy(setting => setting.Order).Select(setting => setting.BindingName));
                var property = typeof(ThemeConfig).GetProperty(nameof(ThemeConfig.StartupTheme))!;
                Assert.Equal(typeof(EnumPropertiesEditor), property.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType);

                MethodInfo createItems = typeof(SettingSearchProvider).GetMethod("CreateItems", BindingFlags.Static | BindingFlags.NonPublic)!;
                var searchItems = (IReadOnlyList<ISearch>)createItems.Invoke(null,
                    [appearance, new Action<string>(_ => throw new InvalidOperationException("Indexing must not navigate or save settings."))])!;
                SearchMeta searchItem = Assert.IsType<SearchMeta>(Assert.Single(searchItems
                    .Where(item => item.GuidId!.EndsWith(":" + nameof(ThemeConfig.StartupTheme), StringComparison.Ordinal))));
                Assert.Equal(expectedTitle, searchItem.Header);
                Assert.Contains(searchItem.Aliases, alias => alias.Contains("startuptheme", StringComparison.OrdinalIgnoreCase));

                // Use the real settings shell and row factory with only this row. Other option editors need no activation.
                ConstructorInfo constructor = typeof(SettingWindow).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                    null, [typeof(IEnumerable<ConfigSettingMetadata>)], null)!;
                window = (SettingWindow)constructor.Invoke([new[] { startup }]);
                window.ShowActivated = false;
                window.ShowInTaskbar = false;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -10000;
                window.Top = -10000;
                window.Show();
                window.UpdateLayout();
                PumpDispatcher();
                FrameworkElement row = Assert.Single(Descendants(window).OfType<FrameworkElement>()
                    .Where(element => Equals(element.Tag, searchItem.GuidId)));
                ComboBox combo = Assert.IsType<ComboBox>(Assert.Single(Descendants(row).OfType<ComboBox>()));
                Assert.Empty(Descendants(row).OfType<RadioButton>());
                Assert.Contains(Descendants(row).OfType<TextBlock>(), text => text.Text == expectedTitle);
                Assert.Equal(new[] { dark, light, follow }, combo.Items.Cast<KeyValuePair<object?, string>>().Select(item => item.Value));
                Assert.Equal(StartupTheme.Dark, combo.SelectedValue);
                if (cultureName == "en-US")
                {
                    foreach (TextBlock text in Descendants(row).OfType<TextBlock>())
                        Assert.DoesNotMatch(@"[\u3400-\u9FFF]", text.Text);
                    Assert.DoesNotMatch(@"[\u3400-\u9FFF]", searchItem.Description!);
                }

                foreach (StartupTheme choice in new[] { StartupTheme.Light, StartupTheme.FollowApplication, StartupTheme.Dark })
                {
                    combo.SelectedValue = choice;
                    combo.GetBindingExpression(Selector.SelectedValueProperty)!.UpdateSource();
                    PumpDispatcher();
                    Assert.Equal(choice, config.Theme.StartupTheme);
                    Assert.Equal(Theme.Dark, config.Theme.Theme);
                    Assert.Equal(Theme.Dark, manager.CurrentTheme);
                    Assert.Equal(Theme.Dark, manager.CurrentUITheme);
                    Assert.Equal(originalPalette, application.Resources.MergedDictionaries);
                }
                Assert.Equal(0, config.SaveCalls);
            }
            finally
            {
                try { window?.Close(); PumpDispatcher(); }
                finally
                {
                    manager.Dispose();
                    ThemeManager.Current = previousManager;
                    ConfigService.SetInstance(previousConfig!);
                    application.Resources = previousResources;
                    application.MainWindow = previousMainWindow;
                    CultureInfo.CurrentUICulture = previousUiCulture;
                    CultureInfo.CurrentCulture = previousFormatCulture;
                }
            }
        });
    }

    private static ConfigSettingMetadata Metadata(object source, string name)
    {
        var attribute = source.GetType().GetProperty(name)!.GetCustomAttribute<ConfigSettingAttribute>()!;
        return new ConfigSettingMetadata
        {
            Source = source, BindingName = name, Order = attribute.Order, Group = attribute.Group,
            Name = attribute.Name, Description = attribute.Description, Section = attribute.Section, Layout = attribute.Layout
        };
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}

internal sealed class StartupThemeTestConfigService : IConfigService
{
    public ThemeConfig Theme { get; } = new();
    public int SaveCalls { get; private set; }
    public IConfig GetRequiredService(Type type) => type == typeof(ThemeConfig) ? Theme
        : throw new InvalidOperationException($"The startup presentation fixture must not initialize {type.Name}.");
    public T GetRequiredService<T>() where T : IConfig => (T)GetRequiredService(typeof(T));
    public void SaveConfigs() => SaveCalls++;
    public void Save<T>() where T : IConfig => SaveCalls++;
    public void LoadConfigs() => throw new InvalidOperationException("Presentation tests must not load persisted configuration.");
}

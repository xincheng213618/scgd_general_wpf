using ColorVision.UI.Desktop.Settings;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class SettingSearchProviderTests
{
    [Fact]
    public void IndexingUsesMetadataWithoutReadingValuesOrConstructingCustomPages()
    {
        var values = new UnreadSettings();
        UnconstructedPage.ConstructorCalls = 0;
        ConfigSettingMetadata[] metadata =
        [
            new() { Name = "Logging", Description = "Record diagnostic details", BindingName = nameof(UnreadSettings.Enabled), Source = values },
            new() { Name = "Advanced tools", Type = ConfigSettingType.TabItem, ViewType = typeof(UnconstructedPage) }
        ];
        IReadOnlyList<ISearch> results = Build(metadata, _ => throw new InvalidOperationException("Indexing must not navigate."));

        Assert.Equal(2, results.Count);
        Assert.Equal(0, values.Reads);
        Assert.Equal(0, values.Writes);
        Assert.Equal(0, UnconstructedPage.ConstructorCalls);
        SearchMeta logging = Assert.IsType<SearchMeta>(results[0]);
        Assert.Equal("Settings", logging.CategoryKey);
        Assert.Contains("diagnostic", logging.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(logging.Aliases, alias => alias.Contains("Enabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PropertyIdentitySurvivesTitleAndCultureChangesAndDuplicateMetadataIsDeduplicated()
    {
        var metadata = new ConfigSettingMetadata { Name = "Log level", BindingName = nameof(UnreadSettings.Enabled), Source = new UnreadSettings() };
        string first = Assert.Single(Build([metadata, metadata], _ => { })).GuidId!;
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
            metadata.Name = "日志级别";
            string translated = Assert.Single(Build([metadata], _ => { })).GuidId!;
            Assert.Equal(first, translated);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public void StartupUpdateSearchMatchesTheSingleAggregatedWindowEntryWithoutReadingFlags()
    {
        var application = new AutoUpdateConfig();
        var plugin = new MarketplaceWindowConfig();
        IReadOnlyList<ISearch> results = Build(
        [
            new() { Source = application, BindingName = nameof(AutoUpdateConfig.IsAutoUpdate), Name = "App updates" },
            new() { Source = plugin, BindingName = nameof(MarketplaceWindowConfig.IsAutoUpdate), Name = "Plugin updates" }
        ], _ => { });
        ISearch result = Assert.Single(results);
        Assert.Equal("setting:startup-check-updates", result.GuidId);
        Assert.Equal(0, application.Reads + plugin.Reads);
    }

    [Fact]
    public void SelectingAResultOnlyRequestsNavigationByItsStableIdentity()
    {
        string? destination = null;
        var values = new UnreadSettings();
        ISearch result = Assert.Single(Build(
            [new() { Name = "Logging", BindingName = nameof(UnreadSettings.Enabled), Source = values }], id => destination = id));

        Assert.Null(destination);
        Assert.True(result.Command!.CanExecute(null));
        result.Command.Execute(null);
        Assert.Equal(result.GuidId, destination);
        Assert.Equal(0, values.Reads + values.Writes);
    }

    [Fact]
    public void NavigationClearsOldFilterSelectsTheCorrectGroupAndTargetsTheActualRow()
    {
        WpfTestHost.Invoke(() =>
        {
            ResourceDictionary resources = Application.Current.Resources;
            Dictionary<object, object> locals = resources.Keys.Cast<object>().ToDictionary(key => key, key => resources[key]);
            List<ResourceDictionary> dictionaries = resources.MergedDictionaries.ToList();
            SettingWindow? window = null;
            try
            {
                resources.Clear();
                resources.MergedDictionaries.Clear();
                foreach (string source in new[]
                {
                    "/HandyControl;component/Themes/basic/colors/colorsdark.xaml",
                    "/HandyControl;component/Themes/Theme.xaml",
                    "/ColorVision.Themes;component/Themes/Dark.xaml",
                    "/ColorVision.Themes;component/Themes/Base.xaml"
                }) resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
                var values = new SafeSettings();
                ConfigSettingMetadata[] metadata =
                [
                    new() { Name = "Enabled", Source = values, BindingName = nameof(SafeSettings.Enabled), Group = "Appearance" },
                    new() { Name = "Log level", Source = values, BindingName = nameof(SafeSettings.LogLevel), Group = "Diagnostics" }
                ];
                IReadOnlyList<ISearch> results = Build(metadata, _ => { });
                string targetId = results.Single(result => result.Header == "Log level").GuidId!;
                ConstructorInfo constructor = typeof(SettingWindow).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
                    [typeof(IEnumerable<ConfigSettingMetadata>)], null)!;
                window = (SettingWindow)constructor.Invoke([metadata]);
                var search = (TextBox)window.FindName("SearchTextBox");
                search.Text = "Enabled";

                Assert.True(window.NavigateToSetting(targetId));
                Assert.Equal(string.Empty, search.Text);
                Assert.Equal("Diagnostics", ((TextBlock)window.FindName("CurrentGroupTitle")).Text);
                FrameworkElement host = (FrameworkElement)window.Content;
                host.Measure(new Size(1000, 700));
                host.Arrange(new Rect(0, 0, 1000, 700));
                host.UpdateLayout();
                Assert.Single(Descendants(host).Where(element => Equals(element.Tag, targetId)));
                Assert.False(window.NavigateToSetting("missing-setting"));
                Assert.Equal("INFO", values.LogLevel);
                Assert.False(values.Enabled);
            }
            finally
            {
                window?.Close();
                resources.Clear();
                resources.MergedDictionaries.Clear();
                foreach (ResourceDictionary dictionary in dictionaries) resources.MergedDictionaries.Add(dictionary);
                foreach ((object key, object value) in locals) resources[key] = value;
            }
        });
    }

    private static IReadOnlyList<ISearch> Build(IEnumerable<ConfigSettingMetadata> metadata, Action<string> navigate)
    {
        MethodInfo method = typeof(SettingSearchProvider).GetMethod("CreateItems", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (IReadOnlyList<ISearch>)method.Invoke(null, [metadata, navigate])!;
    }

    private static IEnumerable<FrameworkElement> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is FrameworkElement element) yield return element;
            foreach (FrameworkElement descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class UnreadSettings
    {
        public int Reads;
        public int Writes;
        public bool Enabled { get { Reads++; throw new InvalidOperationException("Do not read a value to index it."); } set { Writes++; } }
    }

    public sealed class UnconstructedPage : UserControl
    {
        public static int ConstructorCalls;
        public UnconstructedPage() { ConstructorCalls++; throw new InvalidOperationException("Do not construct UI to index it."); }
    }

    private sealed class AutoUpdateConfig
    {
        public int Reads;
        public bool IsAutoUpdate { get { Reads++; throw new InvalidOperationException(); } set { } }
    }

    private sealed class MarketplaceWindowConfig
    {
        public int Reads;
        public bool IsAutoUpdate { get { Reads++; throw new InvalidOperationException(); } set { } }
    }

    private sealed class SafeSettings
    {
        public bool Enabled { get; set; }
        public string LogLevel { get; set; } = "INFO";
    }
}

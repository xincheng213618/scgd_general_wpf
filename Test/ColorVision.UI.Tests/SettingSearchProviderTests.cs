using ColorVision.UI.Desktop.Settings;
using System.Globalization;
using System.Reflection;
using System.Windows.Controls;

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

    private static IReadOnlyList<ISearch> Build(IEnumerable<ConfigSettingMetadata> metadata, Action<string> navigate)
    {
        MethodInfo method = typeof(SettingSearchProvider).GetMethod("CreateItems", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (IReadOnlyList<ISearch>)method.Invoke(null, [metadata, navigate])!;
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

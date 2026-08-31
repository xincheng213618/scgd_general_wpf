using ColorVision.Recovery;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using ColorVision.UI.Serach;
using System.Globalization;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class StartupMaintenanceSearchTests
{
    private const string WizardId = "maintenance:setup-wizard";
    private const string RecoveryId = "maintenance:startup-recovery";

    [Theory]
    [InlineData("zh-CN", "初始化向导", "故障恢复", "高级维护", "打开", "管理员")]
    [InlineData("en-US", "Setup wizard", "Startup recovery", "Advanced maintenance", "Open", "Administrator")]
    public void DiscoveryReadsLocalizedStableSearchOnlyMetadataWithoutRequestingMaintenance(
        string culture, string wizardTitle, string recoveryTitle, string category, string openingMarker, string adminMarker)
    {
        using var language = new CultureScope(culture);
        var fixture = new Fixture();
        Type type = typeof(StartupMaintenanceSearchProvider);
        Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
        Assert.True(typeof(ISearchProvider).IsAssignableFrom(type));
        Assert.False(typeof(IMenuItem).IsAssignableFrom(type));
        Assert.False(typeof(IMenuItemProvider).IsAssignableFrom(type));
        Assert.False(typeof(IHotKey).IsAssignableFrom(type));
        Assert.False(typeof(IHotkeyProvider).IsAssignableFrom(type));

        IReadOnlyList<SearchResultItem> catalog = fixture.Manager.GetStaticResults();
        Assert.Equal(2, catalog.Count);
        Assert.Equal(wizardTitle, catalog.Single(item => item.Source.GuidId == WizardId).Title);
        Assert.Equal(recoveryTitle, catalog.Single(item => item.Source.GuidId == RecoveryId).Title);
        foreach (SearchResultItem item in catalog)
        {
            SearchMeta metadata = Assert.IsType<SearchMeta>(item.Source);
            Assert.Equal(SearchType.Menu, metadata.Type);
            Assert.Equal("Commands", item.CategoryKey);
            Assert.Equal(category, item.Category);
            Assert.Equal("Menu:" + metadata.GuidId, item.StableId);
            Assert.Equal(type.FullName, item.ProviderId);
            Assert.Contains(openingMarker, item.Description, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(adminMarker, item.Description, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("确认后重启", item.Description, StringComparison.Ordinal);
            Assert.DoesNotContain("Restart into", item.Description, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(item.Aliases);
            Assert.Null(metadata.ActionId);
            Assert.Empty(item.ActionId);
            Assert.Empty(item.ShortcutText);
            Assert.True(metadata.Command!.CanExecute(null));
            Assert.True(SearchCommandExecutor.CanExecute(metadata.Command, null, null));
        }

        Assert.Equal(catalog.Select(item => item.StableId), fixture.Manager.GetStaticResults(refresh: true).Select(item => item.StableId));
        Assert.Equal(1, fixture.ProviderCreations);
        Assert.Empty(fixture.Requests);
    }

    [Theory]
    [InlineData("zh-CN", "初始化向导", WizardId)]
    [InlineData("zh-CN", "故障恢复", RecoveryId)]
    [InlineData("en-US", "setup wizard", WizardId)]
    [InlineData("en-US", "startup recovery", RecoveryId)]
    public async Task LocalizedKeywordsFindTheExpectedCommandWithoutExecutingIt(string culture, string query, string id)
    {
        using var language = new CultureScope(culture);
        var fixture = new Fixture();

        SearchQueryResult response = await fixture.Manager.QueryAsync(query, CancellationToken.None, category: "Commands");

        Assert.Equal(id, Assert.Single(response.Items).Source.GuidId);
        Assert.Empty(response.FailedSources);
        Assert.False(response.IsTruncated);
        Assert.Empty(fixture.Requests);
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    public async Task EveryChineseAndEnglishAliasIsSearchableInEitherUiLanguage(string culture)
    {
        using var language = new CultureScope(culture);
        var fixture = new Fixture();
        IReadOnlyList<SearchResultItem> catalog = fixture.Manager.GetStaticResults();

        foreach (SearchResultItem item in catalog)
        {
            Assert.Contains(item.Aliases, alias => alias.Any(character => character > 127));
            Assert.Contains(item.Aliases, alias => alias.All(character => character <= 127));
            foreach (string alias in item.Aliases)
            {
                SearchQueryResult response = await fixture.Manager.QueryAsync(alias, CancellationToken.None);
                Assert.Contains(response.Items, candidate => candidate.StableId == item.StableId);
                Assert.Empty(response.FailedSources);
            }
        }

        Assert.Equal(1, fixture.ProviderCreations);
        Assert.Empty(fixture.Requests);
    }

    [Theory]
    [InlineData(WizardId, true)]
    [InlineData(RecoveryId, false)]
    public async Task ExplicitExecutionRequestsOnlyTheSelectedMaintenanceMode(string id, bool setupWizard)
    {
        var fixture = new Fixture();
        SearchQueryResult response = await fixture.Manager.QueryAsync(id, CancellationToken.None);
        SearchResultItem item = Assert.Single(response.Items);
        Assert.Empty(fixture.Requests);
        Assert.True(item.Source.Command!.CanExecute(null));
        Assert.Empty(fixture.Requests);

        // The injected delegate only records a mode: no controller, window, process or production config is used.
        Assert.True(SearchCommandExecutor.TryExecute(item.Source.Command, null, null));

        Assert.Equal(setupWizard ? StartupMaintenanceMode.SetupWizard : StartupMaintenanceMode.Recovery, Assert.Single(fixture.Requests));
    }

    [Theory]
    [InlineData("Commands", 2)]
    [InlineData("commands", 2)]
    [InlineData("Settings", 0)]
    [InlineData("Templates", 0)]
    [InlineData("External", 0)]
    public async Task CategoryFilteringKeepsMaintenanceInCommandsOnly(string category, int count)
    {
        var fixture = new Fixture();

        SearchQueryResult response = await fixture.Manager.QueryAsync(string.Empty, CancellationToken.None, category: category);

        Assert.Equal(count, response.Items.Count);
        Assert.All(response.Items, item => Assert.Equal(SearchType.Menu, item.Source.Type));
        Assert.Empty(response.FailedSources);
        Assert.Empty(fixture.Requests);
    }

    [Fact]
    public async Task MenuIndexSwitchControlsBothEntriesWithoutChangingOtherTypeSwitchesOrExecutingActions()
    {
        var fixture = new Fixture();
        fixture.Config.EnableTemplateIndex = false;
        fixture.Config.EnableThirdPartyAppIndex = false;
        Assert.Equal(2, (await fixture.Manager.QueryAsync(string.Empty, CancellationToken.None)).Items.Count);

        fixture.Config.EnableMenuIndex = false;
        Assert.Empty((await fixture.Manager.QueryAsync(string.Empty, CancellationToken.None)).Items);
        Assert.Empty((await fixture.Manager.QueryAsync("recovery", CancellationToken.None)).Items);
        fixture.Config.EnableMenuIndex = true;
        Assert.Equal(2, (await fixture.Manager.QueryAsync(string.Empty, CancellationToken.None)).Items.Count);

        Assert.Equal(1, fixture.ProviderCreations);
        Assert.Empty(fixture.Requests);
    }

    [Theory]
    [InlineData("zh-CN", "初始化向导", WizardId)]
    [InlineData("zh-CN", "故障恢复", RecoveryId)]
    [InlineData("en-US", "setup wizard", WizardId)]
    [InlineData("en-US", "startup recovery", RecoveryId)]
    public async Task MatchingMaintenanceCommandRanksBeforeBrowserFallbackIncludingWhenLimited(string culture, string query, string id)
    {
        using var language = new CultureScope(culture);
        var fixture = new Fixture();
        fixture.Config.EnableBrowserSearch = true;

        SearchQueryResult response = await fixture.Manager.QueryAsync(query, CancellationToken.None);

        Assert.Equal(2, response.Items.Count);
        Assert.Equal(id, response.Items[0].Source.GuidId);
        Assert.Equal("External", response.Items[1].CategoryKey);
        Assert.Equal("external:browser", response.Items[1].Source.GuidId);
        SearchQueryResult limited = await fixture.Manager.QueryAsync(query, CancellationToken.None, limit: 1);
        Assert.Equal(id, Assert.Single(limited.Items).Source.GuidId);
        Assert.True(limited.IsTruncated);
        Assert.Empty(response.FailedSources);
        Assert.Empty(limited.FailedSources);
        Assert.Empty(fixture.Requests);
        // No external Command is executed; generating the fallback does not launch a browser.
    }

    [Fact]
    public void SearchOnlyEntriesDoNotAcquireShortcutsFromSimilarlyNamedRuntimeActions()
    {
        var fixture = new Fixture();
        fixture.Hotkeys.Add(new HotKeys("初始化向导", new Hotkey(Key.F11, ModifierKeys.Control), () => throw new InvalidOperationException("No shortcut may execute."))
            { Id = WizardId });
        fixture.Hotkeys.Add(new HotKeys("故障恢复", new Hotkey(Key.F12, ModifierKeys.Control), () => throw new InvalidOperationException("No shortcut may execute."))
            { Id = typeof(StartupMaintenanceSearchProvider).FullName! });

        IReadOnlyList<SearchResultItem> catalog = fixture.Manager.GetStaticResults();

        Assert.Equal(2, catalog.Count);
        Assert.All(catalog, item => { Assert.Empty(item.ActionId); Assert.Empty(item.ShortcutText); });
        Assert.Empty(fixture.Requests);
    }

    private sealed class Fixture
    {
        public SearchConfig Config { get; } = new() { EnableEverythingSearch = false, EnableBrowserSearch = false };
        public List<HotKeys> Hotkeys { get; } = [];
        public List<StartupMaintenanceMode> Requests { get; } = [];
        public SearchManager Manager { get; }
        public int ProviderCreations { get; private set; }

        public Fixture()
        {
            var provider = new StartupMaintenanceSearchProvider(Requests.Add);
            Manager = new SearchManager(() => [typeof(StartupMaintenanceSearchProvider).Assembly],
                _ => [typeof(StartupMaintenanceSearchProvider)], () => Config, () => Hotkeys, type =>
                {
                    Assert.Equal(typeof(StartupMaintenanceSearchProvider), type);
                    ProviderCreations++;
                    return provider;
                });
        }
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _original = CultureInfo.CurrentUICulture;
        public CultureScope(string culture) => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        public void Dispose() => CultureInfo.CurrentUICulture = _original;
    }
}

using ColorVision.Common.MVVM;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using ColorVision.UI.Serach;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

public sealed class SearchManagerTests
{
    [Fact]
    public async Task StaticCatalogIsCachedUntilRefreshButShortcutsAreReadFromCurrentRuntime()
    {
        var provider = new StaticProvider { Items = [Entry("日志", "action.log")] };
        var runtime = new HotKeys { Id = "action.log", Hotkey = new Hotkey(Key.L, ModifierKeys.Control) };
        SearchManager manager = Create([provider], hotkeys: [runtime]);
        Assert.Equal("Ctrl+L", Assert.Single((await manager.QueryAsync("日志", CancellationToken.None)).Items).ShortcutText);
        runtime.SetBindings([]);
        Assert.Empty(Assert.Single((await manager.QueryAsync("日志", CancellationToken.None)).Items).ShortcutText);
        Assert.Equal(1, provider.Reads);
        provider.Items.Add(Entry("设置"));
        Assert.Single(manager.GetStaticResults());
        Assert.Equal(2, manager.GetStaticResults(refresh: true).Count);
        Assert.Equal(2, provider.Reads);
    }

    [Fact]
    public void SameCountAssemblyReplacementInvalidatesDiscovery()
    {
        Assembly first = typeof(SearchManagerTests).Assembly;
        Assembly second = typeof(SearchManager).Assembly;
        Assembly[] current = [first];
        var firstProvider = new StaticProvider { Items = [Entry("first")] };
        var secondProvider = new SecondStaticProvider { Items = [Entry("second")] };
        SearchManager manager = new(() => current,
            assembly => assembly == first ? [typeof(StaticProvider)] : [typeof(SecondStaticProvider)],
            Config, () => [], type => type == typeof(StaticProvider) ? firstProvider : secondProvider);
        Assert.Equal("first", Assert.Single(manager.GetStaticResults()).Title);
        current = [second];
        Assert.Equal("second", Assert.Single(manager.GetStaticResults()).Title);
        Assert.Equal(1, firstProvider.Reads);
        Assert.Equal(1, secondProvider.Reads);
    }

    [Fact]
    public async Task DiscoveryAndEnumerationFailuresAreIsolatedAndPartialProviderResultsAreDiscarded()
    {
        var valid = new StaticProvider { Items = [Entry("日志")] };
        SearchManager manager = new(() => [typeof(SearchManagerTests).Assembly],
            _ => [typeof(ConstructorFailure), typeof(EnumerationFailure), typeof(StaticProvider)], Config, () => [],
            type => type == typeof(StaticProvider) ? valid : Activator.CreateInstance(type));
        SearchQueryResult result = await manager.QueryAsync("", CancellationToken.None);
        Assert.Equal("日志", Assert.Single(result.Items).Title);
        Assert.Contains(typeof(ConstructorFailure).FullName!, result.FailedSources);
        Assert.Contains(typeof(EnumerationFailure).FullName!, result.FailedSources);
    }

    [Fact]
    public async Task PartiallyLoadableAssemblyStillContributesSafeTypesAndReportsFailure()
    {
        var provider = new StaticProvider { Items = [Entry("日志")] };
        SearchManager manager = new(() => [typeof(SearchManagerTests).Assembly],
            _ => throw new ReflectionTypeLoadException([typeof(StaticProvider), null], [new TypeLoadException("fixture")]),
            Config, () => [], _ => provider);
        SearchQueryResult result = await manager.QueryAsync("日志", CancellationToken.None);
        Assert.Single(result.Items);
        Assert.Single(result.FailedSources);
    }

    [Fact]
    public async Task EmptyQueryDoesNotCallDynamicProviderOrCreateExternalLaunchers()
    {
        var provider = new DynamicProvider();
        var config = Config();
        config.EnableBrowserSearch = true;
        SearchManager manager = Create([provider], config);
        Assert.Empty((await manager.QueryAsync(" \t ", CancellationToken.None)).Items);
        Assert.Equal(0, provider.Queries);
    }

    [Fact]
    public async Task AsyncProviderTakesPrecedenceAndCancellationPropagates()
    {
        var provider = new AsyncProvider();
        SearchManager manager = Create([provider]);
        using var cancellation = new CancellationTokenSource();
        Task<SearchQueryResult> query = manager.QueryAsync("日志", cancellation.Token);
        Assert.Equal(1, provider.AsyncQueries);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query);
        Assert.Equal(0, provider.SyncQueries);
    }

    [Fact]
    public async Task LegacyProviderRunsOnCallingDispatcherWithoutBackgroundThreadHandoff()
    {
        var provider = new DynamicProvider();
        int expectedThread = 0;
        Task<SearchQueryResult> query = WpfTestHost.Invoke(() =>
        {
            expectedThread = Environment.CurrentManagedThreadId;
            return Create([provider]).QueryAsync("日志", CancellationToken.None);
        });
        Assert.Single((await query).Items);
        Assert.Equal(expectedThread, provider.ThreadId);
    }

    [Fact]
    public async Task FailedDynamicSourceDoesNotHideResultsFromOtherSources()
    {
        SearchQueryResult result = await Create([new DynamicFailure(), new DynamicProvider()]).QueryAsync("日志", CancellationToken.None);
        Assert.Single(result.Items);
        Assert.Contains(typeof(DynamicFailure).FullName!, result.FailedSources);
    }

    [Fact]
    public async Task PerSourceAndTotalLimitsReportTruncation()
    {
        var provider = new StaticProvider { Items = Enumerable.Range(0, 100).Select(i => Entry("日志" + i)).ToList() };
        SearchQueryResult result = await Create([provider]).QueryAsync("日志", CancellationToken.None);
        Assert.Equal(20, result.Items.Count);
        Assert.True(result.IsTruncated);
        result = await Create([provider]).QueryAsync("日志", CancellationToken.None, limit: 5);
        Assert.Equal(5, result.Items.Count);
        Assert.True(result.IsTruncated);
    }

    [Fact]
    public async Task DisabledTypesAndMissingCommandsAreExcludedWithoutExecutingCommands()
    {
        SearchConfig config = Config();
        config.EnableTemplateIndex = false;
        var provider = new StaticProvider { Items = [Entry("menu"), new SearchMeta { Header = "disabled", Type = SearchType.File,
            Command = new RelayCommand(_ => throw new InvalidOperationException("Must not execute")) }, new SearchMeta { Header = "no-command" }] };
        Assert.Equal("menu", Assert.Single((await Create([provider], config).QueryAsync("", CancellationToken.None)).Items).Title);
    }

    [Fact]
    public async Task SessionHistoryOnlyPromotesExistingStaticEntries()
    {
        var provider = new StaticProvider { Items = [Entry("first"), Entry("second")] };
        SearchManager manager = Create([provider]);
        var initial = await manager.QueryAsync("", CancellationToken.None);
        manager.RecordUsed(initial.Items[1].StableId);
        manager.RecordUsed("not-present");
        Assert.Equal("second", (await manager.QueryAsync("", CancellationToken.None)).Items[0].Title);
        Assert.Empty((await manager.QueryAsync("missing", CancellationToken.None)).Items);
    }

    [Fact]
    public async Task BrowserLauncherCapturesItsQueryAndRechecksCurrentConfigurationWithoutLaunching()
    {
        SearchConfig config = Config();
        config.EnableBrowserSearch = true;
        SearchManager manager = Create([], config);
        SearchResultItem result = Assert.Single((await manager.QueryAsync("a & b", CancellationToken.None)).Items);
        Assert.Equal("External", result.CategoryKey);
        Assert.True(result.Source.Command!.CanExecute(null));
        config.SearchEngine = SearchEngine.Bing;
        Assert.False(result.Source.Command.CanExecute(null));
        config.SearchEngine = SearchEngine.Google;
        config.EnableBrowserSearch = false;
        Assert.False(result.Source.Command.CanExecute(null));
        Assert.Equal("https://www.google.com/search?q=a%20%26%20b", SearchManager.GetBrowserSearchUrl("a & b", SearchEngine.Google));
    }

    [Fact]
    public void EverythingQueryIsOneStructuredArgumentEvenWithQuotesAndSwitchLikeText()
    {
        string query = "name \"quoted\" -exit";
        var startInfo = SearchManager.CreateEverythingStartInfo(@"C:\Tools\Everything.exe", query);
        Assert.Equal(["-s", query], startInfo.ArgumentList);
        Assert.Empty(startInfo.Arguments);
    }

    [Fact]
    public void MenuMetadataSharesHotkeyIdentityAndKeepsRoutedCommandIntact()
    {
        WpfTestHost.Invoke(() =>
        {
            var menu = new SafeMenu();
            SearchMeta result = Assert.IsType<SearchMeta>(MenuSearchProvider.CreateSearchItem(menu));
            Assert.Equal("sample.action", result.ActionId);
            Assert.Equal("Save current document", result.Description);
            Assert.Equal("Commands", result.CategoryKey);
            Assert.Same(ApplicationCommands.Save, result.Command);
            Assert.Equal(0, menu.Executions);
        });
    }

    [Fact]
    public void PaletteOmitsOtherWindowMenusAndHiddenMenus()
    {
        WpfTestHost.Invoke(() =>
        {
            Assert.Null(MenuSearchProvider.CreateSearchItem(new SafeMenu { Target = "OtherWindow" }));
            Assert.Null(MenuSearchProvider.CreateSearchItem(new SafeMenu { Visible = Visibility.Collapsed }));
        });
    }

    [Fact]
    public void CloseSearchResultUsesTheDocumentRouteInsteadOfTheActiveWindowAdapter()
    {
        WpfTestHost.Invoke(() =>
        {
            var menu = new ColorVision.UI.Menus.Base.File.MenuClose();
            SearchMeta result = Assert.IsType<SearchMeta>(MenuSearchProvider.CreateSearchItem(menu));
            Assert.Same(ColorVision.UI.Menus.Base.File.MenuClose.CloseDocumentCommand, result.Command);
            Assert.NotSame(menu.Command, result.Command);
            var owner = new Window();
            int calls = 0;
            owner.CommandBindings.Add(new(ColorVision.UI.Menus.Base.File.MenuClose.CloseDocumentCommand,
                (_, _) => calls++, (_, e) => { e.CanExecute = true; e.Handled = true; }));
            Assert.False(owner.IsActive);
            Assert.True(SearchCommandExecutor.TryExecute(result.Command, null, owner));
            Assert.Equal(1, calls);
        });
    }

    private static SearchManager Create(object[] providers, SearchConfig? config = null, HotKeys[]? hotkeys = null)
        => new(() => [typeof(SearchManagerTests).Assembly], _ => providers.Select(provider => provider.GetType()),
            () => config ?? Config(), () => hotkeys ?? [], type => providers.First(provider => provider.GetType() == type));

    private static SearchConfig Config() => new() { EnableBrowserSearch = false, EnableEverythingSearch = false };
    private static ISearch Entry(string title, string? actionId = null) => new SearchMeta { Header = title, GuidId = title,
        ActionId = actionId, Command = new RelayCommand(_ => throw new InvalidOperationException("Must not execute")) };

    public class StaticProvider : ISearchProvider
    {
        public int Reads { get; private set; }
        public List<ISearch> Items { get; set; } = [];
        public IEnumerable<ISearch> GetSearchItems() { Reads++; return Items.ToArray(); }
    }
    public sealed class SecondStaticProvider : StaticProvider { }
    public sealed class ConstructorFailure : ISearchProvider
    {
        public ConstructorFailure() => throw new InvalidOperationException("fixture");
        public IEnumerable<ISearch> GetSearchItems() => [];
    }
    public sealed class EnumerationFailure : ISearchProvider
    {
        public IEnumerable<ISearch> GetSearchItems() { yield return Entry("partial"); throw new InvalidOperationException("fixture"); }
    }
    public sealed class DynamicProvider : IDynamicSearchProvider
    {
        public int Queries { get; private set; }
        public int ThreadId { get; private set; }
        public IEnumerable<ISearch> Search(string query, int limit) { Queries++; ThreadId = Environment.CurrentManagedThreadId; return [Entry("日志")]; }
    }
    public sealed class DynamicFailure : IDynamicSearchProvider
    {
        public IEnumerable<ISearch> Search(string query, int limit) => throw new InvalidOperationException("fixture");
    }
    public sealed class AsyncProvider : IDynamicSearchProvider, IAsyncSearchProvider
    {
        public int SyncQueries { get; private set; }
        public int AsyncQueries { get; private set; }
        public IEnumerable<ISearch> Search(string query, int limit) { SyncQueries++; return []; }
        public async Task<IReadOnlyList<ISearch>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
        {
            AsyncQueries++;
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return [];
        }
    }
    private sealed class SafeMenu : MenuItemBase, IHotKey
    {
        public string Target { get; set; } = MenuItemConstants.MainWindowTarget;
        public Visibility Visible { get; set; } = Visibility.Visible;
        public int Executions { get; private set; }
        public override string Header => "Save(_S)";
        public override string OwnerGuid => MenuItemConstants.File;
        public override string TargetName => Target;
        public override Visibility Visibility => Visible;
        public override ICommand Command => ApplicationCommands.Save;
        public HotKeys HotKeys => new("Save", new Hotkey(Key.S, ModifierKeys.Control), Execute) { Id = "sample.action", Description = "Save current document" };
        public override void Execute() => Executions++;
    }
}

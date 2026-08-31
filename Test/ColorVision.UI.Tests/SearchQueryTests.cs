using ColorVision.Common.MVVM;
using ColorVision.UI.Serach;

namespace ColorVision.UI.Tests;

public sealed class SearchQueryTests
{
    [Fact]
    public void ExactTitlePrecedesPrefixSubstringAliasAndDescription()
    {
        SearchResultItem exact = Item("日志");
        SearchResultItem prefix = Item("日志窗口");
        SearchResultItem contains = Item("打开日志");
        SearchResultItem alias = Item("诊断", aliases: ["日志"]);
        SearchResultItem description = Item("选项", description: "设置日志级别");
        var result = SearchQuery.MatchAndRank([description, alias, contains, prefix, exact], [], "日志");
        Assert.Equal([exact, prefix, contains, alias, description], result);
    }

    [Fact]
    public void AllWhitespaceSeparatesTermsAndEachTermCanMatchDifferentMetadata()
    {
        SearchResultItem expected = Item("日志级别", description: "Configure logging", aliases: ["设置"]);
        Assert.Same(expected, Assert.Single(SearchQuery.MatchAndRank([expected, Item("日志")], [], "设置\tLOGGING　级别")));
        Assert.Empty(SearchQuery.MatchAndRank([expected], [], "日志 不存在"));
    }

    [Fact]
    public void ActionIdentityDeduplicatesAcrossProvidersWithoutDiscardingDistinctActions()
    {
        SearchResultItem first = Item("保存", provider: "Menu", actionId: "app.save");
        SearchResultItem duplicate = Item("保存", provider: "Other", actionId: "APP.SAVE");
        SearchResultItem distinct = Item("保存副本", provider: "Other", actionId: "app.saveCopy");
        Assert.Equal([first, distinct], SearchQuery.MatchAndRank([first, duplicate, distinct], [], "保存"));
    }

    [Fact]
    public void DynamicProviderMayMatchIndexedTextThatIsNotPartOfTheVisibleTitle()
    {
        SearchResultItem flowNode = Item("曝光节点", category: "FlowNodes");
        Assert.Same(flowNode, Assert.Single(SearchQuery.MatchAndRank([], [flowNode], "camera-index-property")));
        Assert.Empty(SearchQuery.MatchAndRank([flowNode], [], "camera-index-property"));
    }

    [Fact]
    public void ExternalLaunchersRemainAfterLocalResultsEvenWhenTheirTitleMatchesExactly()
    {
        SearchResultItem external = Item("日志", category: "External");
        SearchResultItem local = Item("打开日志窗口");
        Assert.Equal([local, external], SearchQuery.MatchAndRank([local], [external], "日志"));
    }

    [Fact]
    public void QuotasPreventOneProviderFromConsumingAllResultsAndCategoryUsesStableKeys()
    {
        SearchResultItem[] menus = Enumerable.Range(0, 10).Select(i => Item($"日志{i}", provider: "Menus")).ToArray();
        SearchResultItem setting = Item("日志级别", provider: "Settings", category: "Settings");
        var result = SearchQuery.MatchAndRank(menus.Append(setting), [], "日志", limit: 4, perSourceLimit: 2);
        Assert.Equal(3, result.Count);
        Assert.Contains(setting, result);
        Assert.Same(setting, Assert.Single(SearchQuery.MatchAndRank(menus.Append(setting), [], "日志", category: "settings")));
    }

    [Fact]
    public void RecentUsageBoostsEmptyQueryWithoutOverridingRelevanceOrCreatingFalseMatches()
    {
        SearchResultItem recent = Item("日志窗口");
        SearchResultItem exact = Item("日志");
        Assert.Same(recent, SearchQuery.MatchAndRank([exact, recent], [], "", recentIds: [recent.StableId])[0]);
        Assert.Same(exact, SearchQuery.MatchAndRank([recent, exact], [], "日志", recentIds: [recent.StableId])[0]);
        Assert.Empty(SearchQuery.MatchAndRank([recent], [], "不匹配", recentIds: [recent.StableId]));
    }

    [Fact]
    public void MissingIdGetsDeterministicProviderScopedFallbackAndTemplateUnderscoresSurvive()
    {
        var source = new SearchMeta { Header = "_Open(_O)", Command = new RelayCommand(_ => { }) };
        SearchResultItem a = new(source, "Menus");
        SearchResultItem b = new(new SearchMeta { Header = source.Header, Command = source.Command }, "Menus");
        Assert.Equal("Open", a.Title);
        Assert.Equal(a.StableId, b.StableId);
        Assert.NotEqual(a.StableId, new SearchResultItem(source, "Other").StableId);
        Assert.Equal("camera_01", new SearchResultItem(new SearchMeta { Header = "camera_01", Type = SearchType.File }, "Templates").Title);
    }

    private static SearchResultItem Item(string title, string description = "", string[]? aliases = null,
        string provider = "Test", string category = "Commands", string? actionId = null)
        => new(new SearchMeta { Header = title, GuidId = title, Description = description, Aliases = aliases ?? [],
            CategoryKey = category, ActionId = actionId, Command = new RelayCommand(_ => { }) }, provider);
}

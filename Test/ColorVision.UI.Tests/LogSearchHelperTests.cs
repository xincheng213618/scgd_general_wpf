using ColorVision.UI.LogImp;

namespace ColorVision.UI.Tests;

public class LogSearchHelperTests
{
    private static readonly string[] EmptySearchLines = { "info one" };
    private static readonly string[] FilterTextLines =
    {
        "INFO Camera online",
        "WARN Camera offline",
        "INFO Algorithm ready"
    };

    [Fact]
    public void FilterLines_ReturnsEmptyResultForEmptySearch()
    {
        var success = LogSearchHelper.FilterLines(string.Empty, EmptySearchLines, out var filteredLines);

        Assert.True(success);
        Assert.Empty(filteredLines);
    }

    [Fact]
    public void FilterLines_MatchesPlainKeywordsCaseInsensitively()
    {
        var lines = new[]
        {
            "INFO Camera online",
            "WARN Camera offline",
            "INFO Algorithm ready"
        };

        var success = LogSearchHelper.FilterLines("camera ONLINE", lines, out var filteredLines);

        Assert.True(success);
        Assert.Equal(new[] { "INFO Camera online" }, filteredLines);
    }

    [Fact]
    public void FilterLines_UsesRegexWhenPatternContainsRegexCharacters()
    {
        var lines = new[]
        {
            "2026-06-03 INFO frame=12",
            "2026-06-03 INFO frame=abc",
            "2026-06-03 WARN frame=99"
        };

        var success = LogSearchHelper.FilterLines(@"INFO frame=\d+", lines, out var filteredLines);

        Assert.True(success);
        Assert.Equal(new[] { "2026-06-03 INFO frame=12" }, filteredLines);
    }

    [Fact]
    public void FilterLines_ReturnsFalseForInvalidRegex()
    {
        var success = LogSearchHelper.FilterLines("[", new[] { "anything" }, out var filteredLines);

        Assert.False(success);
        Assert.Empty(filteredLines);
    }

    [Fact]
    public void FilterText_ReturnsJoinedMatchingLines()
    {
        var logText = string.Join(Environment.NewLine, FilterTextLines);

        var success = LogSearchHelper.FilterText("info", logText, out var filteredText);

        Assert.True(success);
        Assert.Equal("INFO Camera online" + Environment.NewLine + "INFO Algorithm ready", filteredText);
    }

    [Fact]
    public void FilterItems_ReturnsOriginalItemsForMatchingText()
    {
        var items = new[]
        {
            new SearchItem(1, "INFO Camera online"),
            new SearchItem(2, "WARN Camera offline"),
            new SearchItem(3, "INFO Algorithm ready")
        };

        var success = LogSearchHelper.FilterItems("camera offline", items, item => item.Text, out var filteredItems);

        Assert.True(success);
        Assert.Equal(2, filteredItems.Single().Id);
    }

    private sealed record SearchItem(int Id, string Text);
}

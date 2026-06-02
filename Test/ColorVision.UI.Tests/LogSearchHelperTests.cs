using ColorVision.UI.LogImp;

namespace ColorVision.UI.Tests;

public class LogSearchHelperTests
{
    private static readonly string[] EmptySearchLines = { "info one" };

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
}

using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotMarkdownTableBoundaryTests
{
    [Fact]
    public void EscapedTrailingPipeWithoutOuterDelimiterRemainsCellContent()
    {
        var table = Parse(
            "Name | Value | Note",
            "--- | --- | ---",
            @"alpha | beta | keep\|");

        Assert.Equal(["alpha", "beta", "keep|"], Assert.Single(table.Rows));
    }

    [Fact]
    public void EscapedTrailingPipeBeforeOuterDelimiterRemainsCellContent()
    {
        var table = Parse(
            "| Name | Value | Note |",
            "| --- | --- | --- |",
            @"| alpha | beta | keep\| |");

        Assert.Equal(["alpha", "beta", "keep|"], Assert.Single(table.Rows));
    }

    [Fact]
    public void ActualOuterDelimitersAreExcludedFromCells()
    {
        var table = Parse(
            "| Name | Value |",
            "| --- | --- |",
            "| alpha | beta |");

        Assert.Equal(["Name", "Value"], table.Headers);
        Assert.Equal(["alpha", "beta"], Assert.Single(table.Rows));
    }

    [Fact]
    public void ExplicitEmptyTrailingCellIsPreservedBeforeOuterDelimiter()
    {
        var table = Parse(
            "Name | Value | Note",
            "--- | --- | ---",
            "alpha | beta | |");

        Assert.Equal(["alpha", "beta", ""], Assert.Single(table.Rows));
    }

    private static CopilotMarkdownTableModel Parse(params string[] lines)
    {
        Assert.True(CopilotMarkdownTableParser.TryParse(lines, 0, out var table, out var consumedLineCount));
        Assert.Equal(lines.Length, consumedLineCount);
        return table;
    }
}

using System.Text;
using ColorVision.Solution.Terminal;

namespace ColorVision.UI.Tests;

public class TerminalHistoryLayoutTests
{
    [Fact]
    public void NarrowHistoryRetainsContentAndMapsPhysicalCursorColumns()
    {
        var buffer = new TerminalScreenBuffer(16, 1);
        buffer.Write("first line\r\n\x1b[9G");
        var (rows, _, cursorColumn) = buffer.RenderLines();
        var layout = new TerminalHistoryLayout();

        layout.Update(rows, buffer.ScrollbackCount, 4, buffer.ScrollbackDiscarded);

        Assert.Equal(new[] { "firs", "t li", "ne", "" }, layout.Select(line => line.Text));
        Assert.True(layout[0].IsWrapped);
        Assert.True(layout[1].IsWrapped);
        Assert.False(layout[2].IsWrapped);
        Assert.Same(rows[1], layout[3]);
        Assert.Equal((0, 8), layout.GetSourcePosition(2));
        Assert.Equal((2, 2), layout.GetDisplayPosition(0, rows[0].Length));

        // The live buffer normally has the same width and may place the cursor
        // in blank columns beyond the last painted cell.
        layout.Update(rows, buffer.ScrollbackCount, 16, buffer.ScrollbackDiscarded);
        Assert.Equal((1, 8), layout.GetDisplayPosition(1, cursorColumn));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(1)]
    public void NarrowHistoryDoesNotSplitWideGlyphsOrDiscardTheirStyle(int columns)
    {
        var buffer = new TerminalScreenBuffer(20, 1);
        buffer.Write("ab\x1b[31m中😀e\u0301\x1b[0m\r\nactive");
        var (source, _, _) = buffer.RenderLines();
        var layout = new TerminalHistoryLayout();
        layout.Update(source, buffer.ScrollbackCount, columns, buffer.ScrollbackDiscarded);

        var text = new StringBuilder();
        for (int i = 0; i < layout.Count - 1; i++)
        {
            Assert.InRange(layout[i].Length, 1, columns);
            Assert.False(layout[i].Cells[0].IsContinuation);
            layout[i].AppendText(text, 0, layout[i].Length);
        }
        Assert.Equal("ab中😀e\u0301", text.ToString());

        var (middleLine, middleColumn) = layout.GetDisplayPosition(0, 2);
        Assert.Equal((0, 2), layout.GetSourcePosition(middleLine, middleColumn));
        Assert.Equal('中', layout[middleLine].Cells[middleColumn].Char);
        Assert.Equal(2, layout[middleLine].Cells[middleColumn].Fg);
        Assert.Equal(columns == 1 ? 1 : 2, layout[middleLine].Cells[middleColumn].DisplayWidth);
        Assert.Equal("active", layout[^1].Text);
    }

    [Fact]
    public void HistoryLayoutKeepsSoftBoundariesAndReusesSlicesAfterEviction()
    {
        var buffer = new TerminalScreenBuffer(5, 1);
        buffer.Write("abcdefghi\r\nnext");
        var (source, _, _) = buffer.RenderLines();
        var layout = new TerminalHistoryLayout();
        layout.Update(source, buffer.ScrollbackCount, 2, buffer.ScrollbackDiscarded);
        Assert.Equal(new[] { "ab", "cd", "e", "fg", "hi", "next" }, layout.Select(line => line.Text));
        Assert.All(layout.Take(4), line => Assert.True(line.IsWrapped));
        Assert.False(layout[4].IsWrapped);
        var retained = layout[3];

        // Simulate eviction of the first source row as output adds a new row.
        layout.Update(new[] { source[1], source[2], source[2] }, 2, 2, 1);
        Assert.Equal(new[] { "fg", "hi", "ne", "xt", "next" }, layout.Select(line => line.Text));
        Assert.Same(retained, layout[0]);
        Assert.Equal((0, 0), layout.GetSourcePosition(-10, -4));
        Assert.Equal((4, 4), layout.GetDisplayPosition(9000, 1000));
    }
}

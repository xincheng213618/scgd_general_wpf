#pragma warning disable CA1707
using ColorVision.Solution.Terminal;

namespace ColorVision.UI.Tests;

public class TerminalScreenBufferTests
{
    [Fact]
    public void Write_HandlesCrLf()
    {
        var buffer = new TerminalScreenBuffer(10, 3);

        buffer.Write("abc\r\nxyz");

        Assert.Equal("abc\r\nxyz", buffer.Render());
        Assert.Equal(1, buffer.CursorRow);
        Assert.Equal(3, buffer.CursorCol);
    }

    [Fact]
    public void Write_HandlesBackspace()
    {
        var buffer = new TerminalScreenBuffer(10, 3);

        buffer.Write("abc\bX");

        Assert.Equal("abX", buffer.Render());
        Assert.Equal(3, buffer.CursorCol);
    }

    [Fact]
    public void Write_HandlesCursorMovement()
    {
        var buffer = new TerminalScreenBuffer(10, 3);

        buffer.Write("abcdef\x1b[2DZZ");

        Assert.Equal("abcdZZ", buffer.Render());
        Assert.Equal(6, buffer.CursorCol);
    }

    [Fact]
    public void Write_HandlesClearScreen()
    {
        var buffer = new TerminalScreenBuffer(10, 3);

        buffer.Write("line1\r\nline2\x1b[2J\x1b[Hhome");

        Assert.Equal("home", buffer.Render());
        Assert.Equal(0, buffer.CursorRow);
        Assert.Equal(4, buffer.CursorCol);
    }

    [Fact]
    public void Write_HandlesSgrColors()
    {
        var buffer = new TerminalScreenBuffer(10, 3);

        buffer.Write("\x1b[31;1mR\x1b[0mN");
        var (lines, _, _) = buffer.RenderLines();

        Assert.Equal('R', lines[0].Cells[0].Char);
        Assert.Equal(2, lines[0].Cells[0].Fg);
        Assert.True(lines[0].Cells[0].IsBold);
        Assert.Equal('N', lines[0].Cells[1].Char);
        Assert.Equal(0, lines[0].Cells[1].Fg);
        Assert.False(lines[0].Cells[1].IsBold);
    }

    [Fact]
    public void TerminalLine_DeepCopiesCellsForScrollbackSnapshots()
    {
        var cells = new[]
        {
            new TerminalCell { Char = 'a' },
            new TerminalCell { Char = 'b' },
            new TerminalCell { Char = 'c' }
        };

        var line = new TerminalLine(cells);
        cells[0] = new TerminalCell { Char = 'z' };

        Assert.Equal("abc", line.Text);
    }

    [Fact]
    public void RenderLines_ReturnsSnapshotThatIsNotMutatedByLaterWrites()
    {
        var buffer = new TerminalScreenBuffer(6, 2);
        buffer.Write("alpha\r\nbeta");

        var (lines, _, _) = buffer.RenderLines();
        buffer.Write("\x1b[Hzzzzz");

        Assert.Equal("alpha", lines[0].Text);
    }

    [Fact]
    public void Write_WrapsLongLines()
    {
        var buffer = new TerminalScreenBuffer(5, 3);

        buffer.Write("abcdef");

        Assert.Equal("abcde\r\nf", buffer.Render());
        Assert.Equal(1, buffer.CursorRow);
        Assert.Equal(1, buffer.CursorCol);
    }

    [Fact]
    public void Resize_ShrinksRowsIntoScrollbackAndClampsCursor()
    {
        var buffer = new TerminalScreenBuffer(5, 3);
        buffer.Write("one\r\ntwo\r\nthree");

        buffer.Resize(5, 2);
        var (lines, cursorLine, cursorCol) = buffer.RenderLines();

        Assert.Equal("one", lines[0].Text);
        Assert.Equal("two", lines[1].Text);
        Assert.Equal("three", lines[2].Text);
        Assert.Equal(2, cursorLine);
        Assert.Equal(5, cursorCol);
    }

    [Fact]
    public void Resize_DoesNotAddBlankRowsToScrollback()
    {
        var buffer = new TerminalScreenBuffer(5, 3);

        buffer.Resize(5, 2);
        var (lines, cursorLine, cursorCol) = buffer.RenderLines();

        Assert.Single(lines);
        Assert.Equal(string.Empty, lines[0].Text);
        Assert.Equal(0, cursorLine);
        Assert.Equal(0, cursorCol);
    }
}
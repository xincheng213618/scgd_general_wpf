#pragma warning disable CA1707
using System.Text;
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
        Assert.Equal(4, cursorCol);
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

    [Fact]
    public void Resize_ReflowsTheActivePrimaryScreenAndKeepsAbsoluteCursorUpdatesOnThePrompt()
    {
        var buffer = new TerminalScreenBuffer(40, 6);
        buffer.Write("first\r\nabcdefghijklmnopqrstuvwxyz0123456789\r\nCURSOR>");

        buffer.Resize(18, 6);
        Assert.Equal("first\r\nabcdefghijklmnopqr\r\nstuvwxyz0123456789\r\nCURSOR>", buffer.Render());
        Assert.Equal(3, buffer.CursorRow);

        // The next input from ConPTY uses the new physical position of the prompt.
        buffer.Write("\x1b[4;8Hsize\r\nSIZE18x6\r\nCURSOR>");
        buffer.Resize(40, 6);
        Assert.Equal("first\r\nabcdefghijklmnopqrstuvwxyz0123456789\r\nCURSOR>size\r\nSIZE18x6\r\nCURSOR>", buffer.Render());
        Assert.Equal(4, buffer.CursorRow);
        Assert.Equal(7, buffer.CursorCol);
    }

    [Fact]
    public void Resize_PreservesWideGlyphsAndSpacesAcrossSoftWraps()
    {
        var buffer = new TerminalScreenBuffer(8, 3);
        buffer.Write("ab  \x1b[31m中\x1b[0mcdx");

        buffer.Resize(5, 3);
        Assert.Equal("ab  \r\n中cdx\r\n", buffer.Render());
        var (lines, _, _) = buffer.RenderLines();
        Assert.True(lines[0].IsWrapped);
        Assert.Equal(2, lines[1].Cells[0].Fg);
        Assert.True(lines[1].Cells[1].IsContinuation);

        buffer.Write("!");
        buffer.Resize(8, 3);
        Assert.Equal("ab  中cd\r\nx!", buffer.Render());
    }

    [Fact]
    public void Resize_MovesAnInsertionPointPastTheNewMarginAndRejoinsSubsequentInputOnGrowth()
    {
        var buffer = new TerminalScreenBuffer(40, 6);
        buffer.Write("CURSOR>123456789ab");

        buffer.Resize(18, 6);
        Assert.Equal(1, buffer.CursorRow);
        Assert.Equal(0, buffer.CursorCol);
        buffer.Write("\x1b[2;1Hsize\r\nSIZE18x6\r\nCURSOR>");

        buffer.Resize(40, 6);
        Assert.Equal("CURSOR>123456789absize\r\nSIZE18x6\r\nCURSOR>", buffer.Render());
        Assert.Equal(2, buffer.CursorRow);
    }

    [Fact]
    public void Resize_RespectsScrollbackLimitWhenAWidthChangeProducesManyRows()
    {
        var buffer = new TerminalScreenBuffer(128, 30);
        buffer.Write(new string('x', 128 * 30));

        buffer.Resize(1, 2);
        var (lines, cursorLine, cursorCol) = buffer.RenderLines();

        Assert.Equal(3002, lines.Count);
        Assert.Equal(838, buffer.ScrollbackDiscarded);
        Assert.Equal(3001, cursorLine);
        Assert.Equal(0, cursorCol);
        Assert.All(lines, line => Assert.Equal("x", line.Text));
    }

    [Fact]
    public void Resize_LeavesPrimaryRowsIntactWhileTheAlternateScreenChangesSize()
    {
        var buffer = new TerminalScreenBuffer(20, 4);
        const string primaryText = "abcdefghijklmnop\r\nqrstuvwxyz012345\r\nprimary";
        buffer.Write(primaryText + "\x1b[?1049halt output");

        buffer.Resize(5, 2);
        Assert.Equal("alt o", buffer.Render());
        buffer.Resize(20, 4);
        buffer.Write("\x1b[?1049l!");

        Assert.Equal(primaryText + "!", buffer.Render());
        Assert.Equal(2, buffer.CursorRow);
        Assert.Equal(8, buffer.CursorCol);
    }

    [Fact]
    public void Write_RetainsParserStateAcrossEveryChunkBoundary()
    {
        const string output = "start\x1b]0;hidden\x1b\\\x1bPqhidden\x1b\\\x1b[38;5;255;48:2::10:20:30m色\x1b[0m\x1b[?25l!";

        for (int split = 0; split <= output.Length; split++)
        {
            var buffer = new TerminalScreenBuffer(10, 2);
            buffer.Write(output[..split]);
            buffer.Write(output[split..]);
            var (lines, _, _) = buffer.RenderLines();

            Assert.Equal("start色!", lines[0].Text);
            Assert.Equal(256, lines[0].Cells[5].Fg);
            Assert.Equal(0x10A141E, lines[0].Cells[5].Bg);
            Assert.False(buffer.CursorVisible);
        }
    }

    [Fact]
    public void Write_PreservesThePrimaryScreenWhileAnApplicationUsesTheAlternateScreen()
    {
        var buffer = new TerminalScreenBuffer(12, 2);
        buffer.Write("history\r\nabove\r\nmain\x1b[?1049h\x1b[?1;2004;9001h");
        buffer.Write("one\r\ntwo\r\nthree\x1b[3J");

        Assert.True(buffer.IsAlternateScreen);
        Assert.True(buffer.ApplicationCursorKeys);
        Assert.True(buffer.BracketedPaste);
        Assert.True(buffer.Win32InputMode);
        Assert.Equal("two\r\nthree", buffer.Render());

        buffer.Write("\x1b[?1049l!");
        Assert.False(buffer.IsAlternateScreen);
        Assert.Equal("history\r\nabove\r\nmain!", buffer.Render());

        buffer.Write("\x1b" + "c");
        Assert.False(buffer.ApplicationCursorKeys);
        Assert.False(buffer.BracketedPaste);
        Assert.False(buffer.Win32InputMode);
        Assert.Equal("", buffer.Render());
    }

    [Fact]
    public void Write_DecodesLineDrawingAndRestoresTheSavedCharacterSet()
    {
        var buffer = new TerminalScreenBuffer(20, 2);

        buffer.Write("\x1b(");
        buffer.Write("0lqqk\x1b(B\r\n");
        buffer.Write("\x1b)0\x0e" + "x\u001b7\x0f" + "a\u001b8" + "q\x0f" + "x");

        Assert.Equal("┌──┐\r\n│─x", buffer.Render());
    }

    [Fact]
    public void Write_HonorsProgramDefinedTabStopsInBothDirections()
    {
        var buffer = new TerminalScreenBuffer(14, 2);
        buffer.Write("\x1b[3g\x1b[4G\x1bH\x1b[8G\x1bH\rX\tA\x1b[1IB\x1b[2ZC");
        Assert.Equal("X  C   B", buffer.Render());

        buffer.Write("\r\x1b[4G\x1b[0g\r\tD");
        Assert.Equal("X  C   D", buffer.Render());
    }

    [Fact]
    public void Write_ScrollsWithinMarginsWithoutMovingHeadersOrFooters()
    {
        var buffer = new TerminalScreenBuffer(12, 4);
        buffer.Write("head\r\none\r\ntwo\r\nfoot\x1b[2;3r\x1b[3;1H\nnew");
        Assert.Equal("head\r\ntwo\r\nnew\r\nfoot", buffer.Render());

        buffer.Write("\x1b[2;1H\x1bM");
        Assert.Equal("head\r\n\r\ntwo\r\nfoot", buffer.Render());
    }

    [Fact]
    public void Write_AnswersDeviceQueriesWithoutAddingVisibleText()
    {
        var buffer = new TerminalScreenBuffer(12, 3);
        var responses = new List<string>();
        buffer.ResponseReady += responses.Add;

        buffer.Write("one\r\ntwo\x1b[c\x1b[5n\x1b[6n");

        Assert.Equal(new[] { "\x1b[?1;2c", "\x1b[0n", "\x1b[2;4R" }, responses);
        Assert.Equal("one\r\ntwo", buffer.Render());
    }

    [Fact]
    public void Write_AccountsForWideAndCombiningGlyphsAcrossChunks()
    {
        var buffer = new TerminalScreenBuffer(8, 2);
        buffer.Write("甲e\u0301\ud83d");
        buffer.Write("\ude00Z");
        var (lines, _, cursorCol) = buffer.RenderLines();
        var line = Assert.Single(lines);

        Assert.Equal("甲e\u0301😀Z", line.Text);
        Assert.Equal(6, cursorCol);
        Assert.Equal(2, line.Cells[0].DisplayWidth);
        Assert.True(line.Cells[1].IsContinuation);
        Assert.Equal(5, line.GetColumnForTextOffset(5));

        var selection = new StringBuilder();
        line.AppendText(selection, 4, 6);
        Assert.Equal("😀Z", selection.ToString());

        buffer.Write("\x1b[2GX");
        Assert.Equal(" Xe\u0301😀Z", buffer.Render());
    }

    [Theory]
    [InlineData("❤\ufe0f", 2)]
    [InlineData("1\ufe0f\u20e3", 2)]
    [InlineData("漢\u200d字", 4)]
    [InlineData("x\u200b\u200e", 1)]
    [InlineData("x\u00ad", 2)]
    [InlineData("⌚🀄𛄟", 6)]
    [InlineData("🌡", 1)]
    public void Write_AlignsFollowingTextWithConPtyUnicodeColumns(string text, int columns)
    {
        for (int split = 0; split <= text.Length; split++)
        {
            var buffer = new TerminalScreenBuffer(16, 2);
            buffer.Write(text[..split]);
            buffer.Write(text[split..] + "X");
            var (lines, _, cursorColumn) = buffer.RenderLines();
            var line = Assert.Single(lines);

            Assert.Equal(text + "X", line.Text);
            Assert.Equal(columns + 1, cursorColumn);
            Assert.Equal('X', line.Cells[columns].Char);
        }
    }

    [Fact]
    public void Write_WrapsAnEmojiThatBecomesWideAtTheLastColumn()
    {
        var buffer = new TerminalScreenBuffer(5, 1);
        buffer.Write("1234\x1b[31m❤");
        buffer.Write("\x1b[0m\ufe0f!");
        var (lines, cursorLine, cursorColumn) = buffer.RenderLines();

        Assert.Equal("1234", lines[0].Text);
        Assert.True(lines[0].IsWrapped);
        Assert.Equal("❤\ufe0f!", lines[1].Text);
        Assert.True(lines[1].Cells[1].IsContinuation);
        Assert.Equal(2, lines[1].Cells[0].Fg);
        Assert.Equal((1, 3), (cursorLine, cursorColumn));
    }

    [Fact]
    public void RenderLines_ReusesUnchangedSnapshotsAndKeepsSnapshotsStableThroughEviction()
    {
        var buffer = new TerminalScreenBuffer(12, 2);
        buffer.Write("first\r\nsecond");
        var (original, _, _) = buffer.RenderLines();

        buffer.Write("!");
        var (updated, _, _) = buffer.RenderLines();
        Assert.Same(original[0], updated[0]);

        buffer.Write(string.Concat(Enumerable.Repeat("\r\nline", 3600)));
        var (fullHistory, _, _) = buffer.RenderLines();
        Assert.Equal(3002, fullHistory.Count);
        Assert.Equal(600, buffer.ScrollbackDiscarded);

        buffer.Write("\x1b[3J");
        Assert.Equal("line\r\nline", buffer.Render());
        Assert.Equal("first", original[0].Text);
        Assert.Equal("second", original[1].Text);
        Assert.Equal("line", fullHistory[0].Text);
    }

    [Fact]
    public void ClearDisplayedOutput_PreservesTheWrappedPromptAndPendingTerminalData()
    {
        var buffer = new TerminalScreenBuffer(5, 3);
        buffer.Write("old\r\nout\r\nprompt\x1b[?2004h\x1b[31");

        buffer.ClearDisplayedOutput();
        buffer.Write("m!");
        var (lines, cursorRow, cursorColumn) = buffer.RenderLines();

        Assert.Equal(3, lines.Count);
        Assert.Equal("", lines[0].Text);
        Assert.Equal("promp", lines[1].Text);
        Assert.True(lines[1].IsWrapped);
        Assert.Equal("t!", lines[2].Text);
        Assert.Equal(2, lines[2].Cells[1].Fg);
        Assert.Equal(2, cursorRow);
        Assert.Equal(2, cursorColumn);
        Assert.True(buffer.BracketedPaste);
    }
}

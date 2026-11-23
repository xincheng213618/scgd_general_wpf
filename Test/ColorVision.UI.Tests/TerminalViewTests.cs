using ColorVision.Solution.Terminal;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public class TerminalViewTests
{
    [Fact]
    public void DoubleClickSelectionFromTheSecondCellOfAWideCharacterCopiesTheWholeWord()
    {
        StaTest.Run(() =>
        {
            var display = new TerminalView();
            var buffer = new TerminalScreenBuffer(40, 5);
            buffer.Write("PS 中文🙂.txt next");
            var (lines, cursorLine, cursorColumn) = buffer.RenderLines();
            display.UpdateContent(lines, cursorLine, cursorColumn, buffer.CursorVisible,
                buffer.IsAlternateScreen, buffer.ScrollbackDiscarded, buffer.ScrollbackCount, buffer.Cols);

            display.SelectWordAt(new Point(4.5 * display.CharWidth, 0));

            Assert.Equal("中文🙂.txt", display.GetSelectedText());
        });
    }

    [Fact]
    public void OutputFollowsTheTailUnlessTheUserScrollsIntoHistory()
    {
        StaTest.Run(() =>
        {
            var display = new TerminalView();
            var scroll = new ScrollViewer { Content = display, CanContentScroll = true };
            var viewport = new Size(400, display.LineHeight * 5);
            scroll.Measure(viewport);
            scroll.Arrange(new Rect(viewport));
            Assert.Same(scroll, display.ScrollOwner);

            var buffer = new TerminalScreenBuffer(40, 5);
            void Refresh()
            {
                var (lines, cursorLine, cursorColumn) = buffer.RenderLines();
                display.UpdateContent(lines, cursorLine, cursorColumn, buffer.CursorVisible,
                    buffer.IsAlternateScreen, buffer.ScrollbackDiscarded, buffer.ScrollbackCount, buffer.Cols);
            }

            // A wheel event on a clean screen must not turn off automatic following.
            display.MouseWheelUp();
            buffer.Write(string.Concat(Enumerable.Repeat("line\r\n", 20)));
            Refresh();
            Assert.Equal(display.ExtentHeight - display.ViewportHeight, display.VerticalOffset);

            display.PageUp();
            double historyOffset = display.VerticalOffset;
            buffer.Write("more\r\n");
            Refresh();
            Assert.Equal(historyOffset, display.VerticalOffset);

            display.FollowOutput();
            Assert.Equal(display.ExtentHeight - display.ViewportHeight, display.VerticalOffset);
        });
    }

    [Fact]
    public void NarrowerViewportPreservesTheHistoryAnchorAndSelectedText()
    {
        StaTest.Run(() =>
        {
            var display = new TerminalView();
            var size = new Size(400, display.LineHeight * 3);
            display.Measure(size);
            display.Arrange(new Rect(size));
            var buffer = new TerminalScreenBuffer(40, 3);
            buffer.Write("wide-line-before-selection\r\nselect-中文🙂.log\r\nline 3\r\nline 4\r\nline 5\r\nline 6");

            void Refresh()
            {
                var (lines, cursorLine, cursorColumn) = buffer.RenderLines();
                display.UpdateContent(lines, cursorLine, cursorColumn, buffer.CursorVisible,
                    buffer.IsAlternateScreen, buffer.ScrollbackDiscarded, buffer.ScrollbackCount, buffer.Cols);
            }

            Refresh();
            display.SetVerticalOffset(display.LineHeight);
            display.SelectWordAt(new Point(0, 0));
            Assert.Equal("select-中文🙂.log", display.GetSelectedText());

            buffer.Resize(8, 3);
            Refresh();

            Assert.Equal(display.LineHeight * 4, display.VerticalOffset);
            Assert.Equal("select-中文🙂.log", display.GetSelectedText());
        });
    }
}

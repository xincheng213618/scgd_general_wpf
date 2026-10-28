using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotMarkdownRenderSchedulingTests
{
    [Fact]
    public void UnloadingStopsPendingRenderingAndReloadingUsesTheLatestUnloadedValue()
    {
        Run(() =>
        {
            using var fixture = new LoadedMarkdownFixture("Already visible");
            fixture.WaitForText("Already visible");
            var originalDocument = fixture.Viewer.Document;

            fixture.View.Markdown = "Pending before unload";
            fixture.Unload();
            fixture.View.Markdown = "First unloaded revision";
            fixture.View.Markdown = "Latest unloaded revision";
            PumpFor(TimeSpan.FromMilliseconds(650));

            Assert.False(fixture.View.IsLoaded);
            Assert.Same(originalDocument, fixture.Viewer.Document);
            Assert.Equal("Already visible", fixture.DocumentText);

            fixture.Reload();
            fixture.WaitForText("Latest unloaded revision");
            Assert.NotSame(originalDocument, fixture.Viewer.Document);
        });
    }

    [Theory]
    [InlineData("Read selected words", "Read selected words while streaming", "selected words", "while streaming")]
    [InlineData("Read [reference](https://example.test)", "Read [reference](https://example.test) while streaming", "reference", "while streaming")]
    [InlineData("| Name | Value |\n| --- | --- |\n| selected | one |", "| Name | Value |\n| --- | --- |\n| selected | one |\n| next | two |", "selected", "next")]
    [InlineData("**selected words", "**selected words** while streaming", "selected words", "while streaming")]
    public void AppendingToAResponsePreservesSelectedTextAndStillRendersTheNewContent(
        string initialMarkdown, string appendedMarkdown, string selectedText, string addedText)
    {
        Run(() =>
        {
            using var fixture = new LoadedMarkdownFixture(initialMarkdown);
            PumpUntil(() => fixture.DocumentText.Contains(selectedText, StringComparison.Ordinal), "Initial content did not render.");
            fixture.SelectText(selectedText);
            var originalDocument = fixture.Viewer.Document;

            fixture.View.Markdown = appendedMarkdown;
            PumpUntil(() => !ReferenceEquals(originalDocument, fixture.Viewer.Document), "The appended content did not render.");

            Assert.Equal(selectedText, fixture.Viewer.Selection.Text);
            Assert.Contains(addedText, fixture.DocumentText, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecyclingAMessageClearsSelectionsAndCodeViewportEvenWhenItsMarkdownSharesTheOldPrefix(bool appendText)
    {
        Run(() =>
        {
            var code = string.Join("\n", Enumerable.Range(0, 40).Select(index => $"line {index:D2} " + new string('x', 100)));
            var markdown = "Shared selected paragraph\n\n```text\n" + code + "\n```";
            var originalMessage = CopilotResponseTimelineItem.FromMarkdown(markdown);
            var nextMessage = CopilotResponseTimelineItem.FromMarkdown(markdown + (appendText ? "\nAnother response" : string.Empty));
            using var fixture = new LoadedMarkdownFixture(markdown);
            fixture.BindMessage(originalMessage);
            fixture.WaitForCodeText(code.Replace("\n", "\r\n", StringComparison.Ordinal));
            fixture.SelectText("selected paragraph");
            var originalEditor = fixture.CodeEditors[0];
            originalEditor.Select(0, "line 00".Length);
            originalEditor.ScrollToHorizontalOffset(36);
            originalEditor.ScrollToVerticalOffset(72);
            fixture.Host.UpdateLayout();
            Assert.True(originalEditor.HorizontalOffset > 0);
            Assert.True(originalEditor.VerticalOffset > 0);
            var originalDocument = fixture.Viewer.Document;

            fixture.Unload();
            fixture.View.DataContext = BindingOperations.DisconnectedSource;
            fixture.View.DataContext = nextMessage;
            Assert.True(fixture.Viewer.Selection.IsEmpty);
            Assert.Equal(0, originalEditor.SelectionLength);
            fixture.Reload();
            PumpUntil(() => !ReferenceEquals(originalDocument, fixture.Viewer.Document) && fixture.CodeEditors.Length == 1
                && fixture.CodeEditors[0].IsLoaded, "The recycled message did not render.");

            Assert.Same(nextMessage, fixture.View.DataContext);
            Assert.True(fixture.Viewer.Selection.IsEmpty);
            Assert.Equal(0, fixture.CodeEditors[0].SelectionLength);
            Assert.Equal(0, fixture.CodeEditors[0].HorizontalOffset);
            Assert.Equal(0, fixture.CodeEditors[0].VerticalOffset);
        });
    }

    private static void Run(Action action) => StaTest.Run(action, TimeSpan.FromSeconds(25), "The loaded Markdown scheduling test did not finish.");

    private static void PumpUntil(Func<bool> condition, string failureMessage, Func<string>? describeFailure = null)
    {
        if (condition())
            return;

        var frame = new DispatcherFrame();
        var poll = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(10) };
        var timeout = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(6) };
        poll.Tick += (_, _) =>
        {
            if (condition())
                frame.Continue = false;
        };
        timeout.Tick += (_, _) => frame.Continue = false;
        try
        {
            poll.Start();
            timeout.Start();
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            poll.Stop();
            timeout.Stop();
        }

        Assert.True(condition(), describeFailure == null ? failureMessage : $"{failureMessage} {describeFailure()}");
    }

    private static void PumpFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var elapsed = new DispatcherTimer(DispatcherPriority.Background) { Interval = duration };
        elapsed.Tick += (_, _) => frame.Continue = false;
        try
        {
            elapsed.Start();
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            elapsed.Stop();
        }
    }

    private sealed class LoadedMarkdownFixture : IDisposable
    {
        public LoadedMarkdownFixture(string markdown, double width = 600)
        {
            View = new CopilotMarkdownView
            {
                Markdown = markdown,
                Width = width,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Viewer = Assert.IsType<RichTextBox>(View.FindName("DocumentViewer"));
            // Stretch the instance's real timer interval to separate producer cadence
            // from rendering; these assertions do not impose a production latency SLA.
            var timerField = typeof(CopilotMarkdownView).GetField("_renderTimer", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The Markdown render timer is unavailable.");
            Assert.IsType<DispatcherTimer>(timerField.GetValue(View)).Interval = TimeSpan.FromMilliseconds(250);
            Host = new Window
            {
                Content = View,
                Width = 720,
                Height = 340,
                Left = -10000,
                Top = -10000,
                WindowStartupLocation = WindowStartupLocation.Manual,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowActivated = false,
                ShowInTaskbar = false,
                Opacity = 0
            };
            try
            {
                Host.Show();
                Host.UpdateLayout();
                PumpUntil(() => View.IsLoaded, "The Markdown control did not complete its real Loaded lifecycle.");
                Assert.IsType<HwndSource>(PresentationSource.FromVisual(View));
                Assert.False(Host.IsActive);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public CopilotMarkdownView View { get; }
        public RichTextBox Viewer { get; }
        public Window Host { get; }
        public string DocumentText => new TextRange(Viewer.Document.ContentStart, Viewer.Document.ContentEnd).Text.TrimEnd('\r', '\n');
        public TextBox[] CodeEditors => Viewer.Document.Blocks.OfType<BlockUIContainer>()
            .SelectMany(block => FindCodeEditors(block.Child)).ToArray();

        public void BindMessage(CopilotResponseTimelineItem message)
        {
            var originalDocument = Viewer.Document;
            var ownerChanged = !ReferenceEquals(View.DataContext, message);
            View.DataContext = message;
            View.SetBinding(CopilotMarkdownView.MarkdownProperty, new Binding(nameof(CopilotResponseTimelineItem.Markdown)));
            if (ownerChanged)
                PumpUntil(() => !ReferenceEquals(originalDocument, Viewer.Document), "The bound message did not render its own document.");
        }

        public void WaitForText(string text) => PumpUntil(() => DocumentText == text, $"The Markdown document did not display the expected text: {text}");

        public void WaitForCodeText(string text, int index = 0) => PumpUntil(
            () => CodeEditors.Length > index && CodeEditors[index].Text == text,
            "The code editor did not display the expected text.");

        private static IEnumerable<TextBox> FindCodeEditors(DependencyObject root)
        {
            if (root is TextBox editor)
            {
                yield return editor;
                yield break;
            }
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                foreach (var child in FindCodeEditors(VisualTreeHelper.GetChild(root, index)))
                    yield return child;
            }
        }

        public void SelectText(string text, bool reversed = false)
        {
            var start = FindText(text);
            var end = start.GetPositionAtOffset(text.Length)!;
            Viewer.Selection.Select(reversed ? end : start, reversed ? start : end);
            Assert.Equal(text, Viewer.Selection.Text);
        }

        public TextPointer FindText(string text)
        {
            for (var position = Viewer.Document.ContentStart; position != null;
                 position = position.GetNextContextPosition(LogicalDirection.Forward))
            {
                if (position.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text)
                    continue;
                var index = position.GetTextInRun(LogicalDirection.Forward).IndexOf(text, StringComparison.Ordinal);
                if (index < 0)
                    continue;

                return position.GetPositionAtOffset(index)!;
            }
            throw new InvalidOperationException($"The rendered response does not contain the expected text: {text}");
        }

        public void Unload()
        {
            Host.Content = null;
            PumpUntil(() => !View.IsLoaded, "The Markdown control did not unload after removal from its native host.");
        }

        public void Reload()
        {
            Host.Content = View;
            Host.UpdateLayout();
            PumpUntil(() => View.IsLoaded, "The Markdown control did not reload after returning to its native host.");
            Assert.IsType<HwndSource>(PresentationSource.FromVisual(View));
        }

        public void Dispose()
        {
            Host.Content = null;
            Host.Close();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }
    }
}

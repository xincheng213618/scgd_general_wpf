using ColorVision.Copilot;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotMarkdownRenderSchedulingTests
{
    [Fact]
    public void AContinuousStreamRendersBeforeItStopsAndEventuallyDisplaysTheCompleteText()
    {
        Run(() =>
        {
            using var fixture = new LoadedMarkdownFixture("Initial content");
            fixture.WaitForText("Initial content");
            var intermediateTexts = new List<string>();
            var producing = true;
            var appendedChunks = 0;
            TextChangedEventHandler observeDocument = (_, _) =>
            {
                var text = fixture.DocumentText;
                if (producing && text.Contains("chunk-", StringComparison.Ordinal))
                    intermediateTexts.Add(text);
            };
            fixture.Viewer.TextChanged += observeDocument;

            // A producer above the render timer's Background priority keeps supplying
            // updates even when a slow dispatcher makes both timers due together.
            var producer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(20)
            };
            producer.Tick += (_, _) =>
            {
                fixture.View.Markdown += $" chunk-{++appendedChunks:D2}";
                if (appendedChunks == 60)
                {
                    producing = false;
                    producer.Stop();
                }
            };
            try
            {
                producer.Start();
                PumpUntil(() => !producing, "The stream producer did not finish.");
                fixture.WaitForText(fixture.View.Markdown);

                Assert.NotEmpty(intermediateTexts);
                Assert.Contains(intermediateTexts, text => !text.Contains("chunk-60", StringComparison.Ordinal));
                Assert.EndsWith("chunk-60", fixture.DocumentText, StringComparison.Ordinal);
            }
            finally
            {
                producer.Stop();
                fixture.Viewer.TextChanged -= observeDocument;
            }
        });
    }

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

    [Fact]
    public void ReloadingUnchangedContentPreservesTheDocumentAndItsTextSelection()
    {
        Run(() =>
        {
            using var fixture = new LoadedMarkdownFixture("Keep this selected text");
            fixture.WaitForText("Keep this selected text");
            var originalDocument = fixture.Viewer.Document;
            fixture.Viewer.Selection.Select(originalDocument.ContentStart, originalDocument.ContentEnd);
            var selectedText = fixture.Viewer.Selection.Text;
            Assert.Contains("selected text", selectedText, StringComparison.Ordinal);

            fixture.Unload();
            // Establish that WPF's own unload kept the selection before exercising
            // the control's reload scheduling and its delayed callback.
            Assert.Same(originalDocument, fixture.Viewer.Document);
            Assert.Equal(selectedText, fixture.Viewer.Selection.Text);
            fixture.Reload();
            PumpFor(TimeSpan.FromMilliseconds(650));

            Assert.Same(originalDocument, fixture.Viewer.Document);
            Assert.Equal(selectedText, fixture.Viewer.Selection.Text);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(640)]
    public void ALoadedTableReflowsWhenItsWidthBecomesNarrow(double initialWidth)
    {
        Run(() =>
        {
            const string longValue = "This is a deliberately long value that needs a readable narrow layout.";
            const string markdown = "| Field | Value |\n| --- | --- |\n| Details | " + longValue + " |";
            using var fixture = new LoadedMarkdownFixture(markdown, initialWidth);
            var renderedWidthField = typeof(CopilotMarkdownView).GetField("_lastRenderedWidth", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The last rendered Markdown width is unavailable.");
            double LastRenderedWidth() => Assert.IsType<double>(renderedWidthField.GetValue(fixture.View));
            string DescribeDocument() => $"FirstBlock={fixture.Viewer.Document.Blocks.FirstBlock?.GetType().Name ?? "null"}; "
                + $"ActualWidth={fixture.View.ActualWidth}; ViewerWidth={fixture.Viewer.ActualWidth}; LastRenderedWidth={LastRenderedWidth()}; "
                + $"Text={fixture.DocumentText.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal)}";
            if (initialWidth == 0)
            {
                // Rendering at zero width can use the control's plain-text fallback.
                // Require real rendered content and its recorded zero width, rather
                // than assuming that attaching a Table succeeded at that geometry.
                PumpUntil(() => fixture.DocumentText.Contains(longValue, StringComparison.Ordinal),
                    "The loaded zero-width document did not render its actual content.", DescribeDocument);
                Assert.True(LastRenderedWidth() == 0, DescribeDocument());
                Assert.Equal(0, fixture.View.ActualWidth, precision: 1);
                var zeroWidthDocumentState = DescribeDocument();
                fixture.View.Width = 640;
                fixture.Host.UpdateLayout();
                PumpUntil(() => fixture.Viewer.Document.Blocks.FirstBlock is Table,
                    "The zero-width document did not reflow to a table when a real wide viewport became available.",
                    () => $"Initial: {zeroWidthDocumentState}; Current: {DescribeDocument()}");
            }
            else
            {
                PumpUntil(() => fixture.Viewer.Document.Blocks.FirstBlock is Table, "The initial wide or fallback-width table did not render.");
            }
            Assert.Equal(640, fixture.View.ActualWidth, precision: 1);
            var originalDocument = fixture.Viewer.Document;
            var initialDocumentState = initialWidth == 0 ? DescribeDocument() : null;

            fixture.View.Width = 280;
            fixture.Host.UpdateLayout();
            PumpUntil(() => fixture.Viewer.Document.Blocks.FirstBlock is Section,
                "The loaded table did not reflow to the narrow key/value layout.",
                initialWidth == 0 ? () => $"Initial: {initialDocumentState}; Current: {DescribeDocument()}" : null);

            Assert.Equal(280, fixture.View.ActualWidth, precision: 1);
            Assert.NotSame(originalDocument, fixture.Viewer.Document);
            Assert.Contains(longValue, fixture.DocumentText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void AnUpdateRaisedWhileReplacingTheDocumentIsRenderedEvenWhenItRestoresThePreviousText()
    {
        Run(() =>
        {
            using var fixture = new LoadedMarkdownFixture("Original A");
            fixture.WaitForText("Original A");
            var restoredDuringRender = false;
            TextChangedEventHandler restorePreviousText = (_, _) =>
            {
                if (restoredDuringRender || fixture.DocumentText != "Replacement B")
                    return;

                restoredDuringRender = true;
                fixture.View.Markdown = "Original A";
            };
            fixture.Viewer.TextChanged += restorePreviousText;
            try
            {
                fixture.View.Markdown = "Replacement B";
                PumpUntil(() => restoredDuringRender, "Replacing the real document did not raise the reentrant update.");
                fixture.WaitForText("Original A");
                Assert.Equal("Original A", fixture.View.Markdown);
            }
            finally
            {
                fixture.Viewer.TextChanged -= restorePreviousText;
            }
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

        public void WaitForText(string text) => PumpUntil(() => DocumentText == text, $"The Markdown document did not display the expected text: {text}");

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

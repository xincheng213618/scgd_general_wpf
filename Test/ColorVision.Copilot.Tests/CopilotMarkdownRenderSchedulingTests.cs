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

    [Fact]
    public void ASelectionAcrossFormattingAndParagraphsSurvivesAnAppendedResponse()
    {
        Run(() =>
        {
            using var fixture = new LoadedMarkdownFixture("Start **selected** and [linked](https://example.test)\n\nSecond paragraph end");
            fixture.WaitForText("Start selected and linked\r\nSecond paragraph end");
            fixture.Viewer.Selection.Select(
                fixture.FindText("selected"),
                fixture.FindText("paragraph").GetPositionAtOffset("paragraph".Length)!);
            const string selectedText = "selected and linked\r\nSecond paragraph";
            Assert.Equal(selectedText, fixture.Viewer.Selection.Text);

            fixture.View.Markdown += " while streaming";
            fixture.WaitForText("Start selected and linked\r\nSecond paragraph end while streaming");

            Assert.Equal(selectedText, fixture.Viewer.Selection.Text);
        });
    }

    [Fact]
    public void SelectingTheWholeResponseDoesNotSelectNewlyAppendedText()
    {
        Run(() =>
        {
            using var fixture = new LoadedMarkdownFixture("Already visible");
            fixture.WaitForText("Already visible");
            fixture.Viewer.SelectAll();

            fixture.View.Markdown = "Already visible and newly appended";
            fixture.WaitForText(fixture.View.Markdown);

            Assert.Equal("Already visible", fixture.Viewer.Selection.Text);
        });
    }

    [Fact]
    public void WidthReflowPreservesAReversedSelectionButReplacingTheMessageClearsIt()
    {
        Run(() =>
        {
            using var fixture = new LoadedMarkdownFixture("Read [reference](https://example.test) here");
            fixture.WaitForText("Read reference here");
            fixture.SelectText("reference", reversed: true);
            var originalDocument = fixture.Viewer.Document;

            fixture.View.Width = 480;
            fixture.Host.UpdateLayout();
            PumpUntil(() => !ReferenceEquals(originalDocument, fixture.Viewer.Document), "The changed width did not render.");

            Assert.Equal("reference", fixture.Viewer.Selection.Text);
            Assert.Equal(0, fixture.Viewer.CaretPosition.CompareTo(fixture.Viewer.Selection.Start));

            fixture.View.Markdown = "Another message has a reference too";
            fixture.WaitForText(fixture.View.Markdown);
            Assert.True(fixture.Viewer.Selection.IsEmpty);
        });
    }

    [Fact]
    public void CodeSelectionsSurviveContinuedStreamingAndClosingTheFence()
    {
        Run(() =>
        {
            using var fixture = new LoadedMarkdownFixture("```text\nfirst block\n```\n\n```csharp\nvar selected = 1;");
            fixture.WaitForCodeText("var selected = 1;", 1);
            fixture.CodeEditors[0].Select(0, "first".Length);
            fixture.CodeEditors[1].Select("var ".Length, "selected".Length);

            foreach (var suffix in new[] { " // streaming", "\nConsole.WriteLine(selected);", "\n```\nAfter the code" })
            {
                var previous = fixture.CodeEditors[1];
                fixture.View.Markdown += suffix;
                PumpUntil(() => fixture.CodeEditors.Length == 2 && !ReferenceEquals(previous, fixture.CodeEditors[1]),
                    "The next streamed code revision did not render.");
                Assert.Equal("first", fixture.CodeEditors[0].SelectedText);
                Assert.Equal("selected", fixture.CodeEditors[1].SelectedText);
            }

            Assert.Equal("var selected = 1; // streaming\r\nConsole.WriteLine(selected);", fixture.CodeEditors[1].Text);
            Assert.Contains("After the code", fixture.DocumentText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void CodeSelectionAndScrollSurviveWidthReflowButDoNotTransferToAnotherMessage()
    {
        Run(() =>
        {
            var code = string.Join("\n", Enumerable.Range(0, 40).Select(index => $"line {index:D2} " + new string('x', 100)));
            using var fixture = new LoadedMarkdownFixture("```text\n" + code + "\n```");
            fixture.WaitForCodeText(code.Replace("\n", "\r\n", StringComparison.Ordinal));
            fixture.Host.UpdateLayout();
            var original = fixture.CodeEditors[0];
            original.Select(0, "line 00".Length);
            original.ScrollToHorizontalOffset(36);
            original.ScrollToVerticalOffset(72);
            fixture.Host.UpdateLayout();
            var horizontalOffset = original.HorizontalOffset;
            var verticalOffset = original.VerticalOffset;
            Assert.True(horizontalOffset > 0);
            Assert.True(verticalOffset > 0);

            fixture.View.Width = 480;
            fixture.Host.UpdateLayout();
            PumpUntil(() => !ReferenceEquals(original, fixture.CodeEditors[0]) && fixture.CodeEditors[0].IsLoaded,
                "The resized code block did not finish loading.");
            fixture.Host.UpdateLayout();

            Assert.Equal("line 00", fixture.CodeEditors[0].SelectedText);
            Assert.Equal(horizontalOffset, fixture.CodeEditors[0].HorizontalOffset);
            Assert.Equal(verticalOffset, fixture.CodeEditors[0].VerticalOffset);

            fixture.View.Markdown = "Another message\n\n```text\n" + code + "\n```";
            PumpUntil(() => fixture.DocumentText.Contains("Another message", StringComparison.Ordinal), "The replacement message did not render.");
            Assert.Equal(0, fixture.CodeEditors[0].SelectionLength);
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

    [Fact]
    public void TheSameTimelineMessageKeepsSelectionsWhileAppendingAndReconnecting()
    {
        Run(() =>
        {
            var message = CopilotResponseTimelineItem.FromMarkdown("Shared selected paragraph\n\n```text\nselected code");
            using var fixture = new LoadedMarkdownFixture(message.Markdown);
            fixture.BindMessage(message);
            fixture.WaitForCodeText("selected code");
            fixture.SelectText("selected paragraph");
            fixture.CodeEditors[0].Select(0, "selected".Length);
            var originalEditor = fixture.CodeEditors[0];

            message.AppendMarkdown(" continues");
            PumpUntil(() => !ReferenceEquals(originalEditor, fixture.CodeEditors[0]), "The same timeline item did not render its append.");
            Assert.Same(message, fixture.View.DataContext);
            Assert.Equal("selected paragraph", fixture.Viewer.Selection.Text);
            Assert.Equal("selected", fixture.CodeEditors[0].SelectedText);

            fixture.Unload();
            fixture.View.DataContext = BindingOperations.DisconnectedSource;
            fixture.View.DataContext = null;
            fixture.View.DataContext = message;
            fixture.Reload();
            Assert.Equal("selected paragraph", fixture.Viewer.Selection.Text);
            Assert.Equal("selected", fixture.CodeEditors[0].SelectedText);
        });
    }

    [Fact]
    public void SwitchingMessageDuringDocumentReplacementDoesNotRestoreTheOldSelection()
    {
        Run(() =>
        {
            var originalMessage = CopilotResponseTimelineItem.FromMarkdown("Shared selected paragraph");
            var nextMessage = CopilotResponseTimelineItem.FromMarkdown("Shared selected paragraph continues");
            using var fixture = new LoadedMarkdownFixture(originalMessage.Markdown);
            fixture.BindMessage(originalMessage);
            fixture.WaitForText(originalMessage.Markdown);
            fixture.SelectText("selected");
            var originalDocument = fixture.Viewer.Document;
            var switched = false;
            TextChangedEventHandler switchMessage = (_, _) =>
            {
                if (switched || ReferenceEquals(originalDocument, fixture.Viewer.Document))
                    return;
                switched = true;
                fixture.View.DataContext = nextMessage;
            };
            fixture.Viewer.TextChanged += switchMessage;
            try
            {
                originalMessage.AppendMarkdown(" continues");
                PumpUntil(() => switched && fixture.DocumentText == nextMessage.Markdown, "The replacement did not switch the message.");
                Assert.True(fixture.Viewer.Selection.IsEmpty);
            }
            finally
            {
                fixture.Viewer.TextChanged -= switchMessage;
            }
        });
    }

    [Fact]
    public void ARecycledMessageCannotReceiveThePreviousEditorsPendingLoadedViewport()
    {
        Run(() =>
        {
            var code = string.Join("\n", Enumerable.Range(0, 40).Select(index => $"line {index:D2} " + new string('x', 100)));
            var message = CopilotResponseTimelineItem.FromMarkdown("```text\n" + code + "\n```");
            using var fixture = new LoadedMarkdownFixture(message.Markdown);
            fixture.BindMessage(message);
            fixture.WaitForCodeText(code.Replace("\n", "\r\n", StringComparison.Ordinal));
            fixture.CodeEditors[0].ScrollToHorizontalOffset(36);
            fixture.CodeEditors[0].ScrollToVerticalOffset(72);
            fixture.Host.UpdateLayout();
            Assert.True(fixture.CodeEditors[0].HorizontalOffset > 0);
            Assert.True(fixture.CodeEditors[0].VerticalOffset > 0);

            // Stop between document replacement and layout, while the old viewport
            // handover is still subscribed to the replacement editor's Loaded event.
            var render = typeof(CopilotMarkdownView).GetMethod("RenderMarkdown", BindingFlags.Instance | BindingFlags.NonPublic)!;
            render.Invoke(fixture.View, [message.Markdown]);
            var pendingEditor = fixture.CodeEditors[0];
            Assert.False(pendingEditor.IsLoaded);
            fixture.View.DataContext = CopilotResponseTimelineItem.FromMarkdown(message.Markdown);
            fixture.Host.UpdateLayout();
            PumpUntil(() => pendingEditor.IsLoaded, "The pending code editor did not complete layout.");

            Assert.Equal(0, pendingEditor.HorizontalOffset);
            Assert.Equal(0, pendingEditor.VerticalOffset);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosingAFencePreservesSelectAllWhenTheTerminalCodeNewlineDisappears(bool hasTerminalNewline)
    {
        Run(() =>
        {
            using var fixture = new LoadedMarkdownFixture("```text\nselected code" + (hasTerminalNewline ? "\n" : string.Empty));
            fixture.WaitForCodeText("selected code" + (hasTerminalNewline ? "\r\n" : string.Empty));
            var original = fixture.CodeEditors[0];
            original.SelectAll();

            fixture.View.Markdown += (hasTerminalNewline ? string.Empty : "\n") + "```";
            PumpUntil(() => !ReferenceEquals(original, fixture.CodeEditors[0]), "The closing code fence did not render.");

            Assert.Equal("selected code", fixture.CodeEditors[0].Text);
            Assert.Equal("selected code", fixture.CodeEditors[0].SelectedText);
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

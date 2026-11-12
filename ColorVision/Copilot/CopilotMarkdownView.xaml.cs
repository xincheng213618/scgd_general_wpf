using LocalizedText = global::ColorVision.DisplayText;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    public partial class CopilotMarkdownView : UserControl
    {
        public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
            nameof(Markdown),
            typeof(string),
            typeof(CopilotMarkdownView),
            new PropertyMetadata(string.Empty, OnMarkdownChanged));

        private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex UnorderedListRegex = new(@"^\s*[-+*]\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex OrderedListRegex = new(@"^\s*(\d+)[.)]\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex ThematicBreakRegex = new(@"^\s{0,3}((\*\s*){3,}|(-\s*){3,}|(_\s*){3,})$", RegexOptions.Compiled);

        private static readonly TimeSpan MinimumRenderInterval = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan MaximumRenderInterval = TimeSpan.FromSeconds(1);

        private readonly DispatcherTimer _renderTimer;
        private string _stableSource = string.Empty;
        private int _stableLength;
        private int _stableBlockCount;
        private string? _renderedMarkdown;
        private FlowDocument? _renderDocument;
        private double _lastRenderedWidth;
        private object? _selectionDataContext;
        private long _selectionRevision;

        private readonly record struct MarkdownCodeFence(char Marker, int Length, int Indentation, string Info);

        public CopilotMarkdownView()
        {
            InitializeComponent();
            _renderTimer = new DispatcherTimer
            {
                Interval = MinimumRenderInterval,
            };
            _renderTimer.Tick += RenderTimer_Tick;
            Loaded += CopilotMarkdownView_Loaded;
            SizeChanged += CopilotMarkdownView_SizeChanged;
            Unloaded += CopilotMarkdownView_Unloaded;
            DataContextChanged += CopilotMarkdownView_DataContextChanged;
        }

        public string Markdown
        {
            get => (string)GetValue(MarkdownProperty);
            set => SetValue(MarkdownProperty, value);
        }

        private static void OnMarkdownChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is CopilotMarkdownView view)
                view.ScheduleRender();
        }

        private void CopilotMarkdownView_Loaded(object sender, RoutedEventArgs e)
        {
            ScheduleRender();
        }

        private void CopilotMarkdownView_Unloaded(object sender, RoutedEventArgs e)
        {
            _renderTimer.Stop();
        }

        private void CopilotMarkdownView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Recycling can temporarily disconnect an item before attaching it again.
            // Preserve the same owner's selection, but never carry it to another message.
            if (e.NewValue == null || ReferenceEquals(e.NewValue, BindingOperations.DisconnectedSource)
                || ReferenceEquals(e.NewValue, _selectionDataContext))
            {
                return;
            }

            _selectionDataContext = e.NewValue;
            _selectionRevision++;
            _renderedMarkdown = null;
            DocumentViewer.Selection.Select(DocumentViewer.Document.ContentStart, DocumentViewer.Document.ContentStart);
            foreach (var editor in GetCodeEditors(DocumentViewer.Document))
            {
                editor.Select(0, 0);
                editor.ScrollToHorizontalOffset(0);
                editor.ScrollToVerticalOffset(0);
            }
            ScheduleRender();
        }

        private void CopilotMarkdownView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
                ScheduleRender();
        }

        private void RenderTimer_Tick(object? sender, EventArgs e)
        {
            _renderTimer.Stop();
            if (IsLoaded && NeedsRender())
            {
                RenderMarkdown(Markdown ?? string.Empty);
                ScheduleRender();
            }
        }

        private void ScheduleRender()
        {
            if (!IsLoaded || _renderTimer.IsEnabled || !NeedsRender())
                return;

            // Keep the first deadline so a continuous stream cannot postpone rendering indefinitely.
            _renderTimer.Start();
        }

        private bool NeedsRender() => !string.Equals(Markdown ?? string.Empty, _renderedMarkdown, StringComparison.Ordinal)
            || Math.Abs(ActualWidth - _lastRenderedWidth) >= 24;

        private void RenderMarkdown(string markdown)
        {
            var selectionRevision = _selectionRevision;
            var selection = CaptureTextSelection(markdown);
            var codeSelections = CaptureCodeSelections(markdown);
            // Tables size themselves when built, so a new width needs the whole document again.
            var widthChanged = Math.Abs(ActualWidth - _lastRenderedWidth) >= 24;
            _lastRenderedWidth = ActualWidth;
            var renderStart = Stopwatch.GetTimestamp();
            try
            {
                if (widthChanged || !TryAppendToRenderedDocument(markdown))
                    DocumentViewer.Document = BuildMarkdownDocument(markdown);
            }
            catch (Exception)
            {
                // Formula controls can fail while WPF attaches the new document,
                // after the LaTeX parser has already accepted the expression.
                // Keep the chat usable by replacing the whole document with text.
                _stableLength = 0;
                DocumentViewer.Document = CreatePlainTextDocument(markdown);
            }

            // Layout costs about as much again, so this keeps rendering near a quarter of the UI thread.
            var renderInterval = Stopwatch.GetElapsedTime(renderStart) * 2;
            _renderTimer.Interval = renderInterval < MinimumRenderInterval ? MinimumRenderInterval
                : renderInterval > MaximumRenderInterval ? MaximumRenderInterval
                : renderInterval;

            if (selectionRevision == _selectionRevision)
                RestoreTextSelection(selection);
            if (selectionRevision == _selectionRevision)
                RestoreCodeSelections(codeSelections, markdown);
            // Document replacement can synchronously switch the binding owner.
            // Leave that owner's render pending even if its text happens to match.
            _renderedMarkdown = selectionRevision == _selectionRevision ? markdown : null;
        }

        private FlowDocument BuildMarkdownDocument(string markdown)
        {
            var document = CreateDocument();
            _renderDocument = document;
            _stableLength = 0;
            try
            {
                if (!string.IsNullOrWhiteSpace(markdown))
                    PopulateMarkdownDocument(NormalizeLineEndings(markdown), 0);
                return document;
            }
            finally
            {
                _renderDocument = null;
            }
        }

        // A long answer would otherwise rebuild every block on each streamed update, which at 30,000
        // characters takes longer than the update interval and leaves the UI thread no time. Only the
        // text after the last stable boundary can still change while the response is appended to.
        private bool TryAppendToRenderedDocument(string markdown)
        {
            var document = DocumentViewer.Document;
            if (_renderedMarkdown == null || _stableLength == 0 || document.Blocks.Count < _stableBlockCount)
                return false;

            var normalized = NormalizeLineEndings(markdown);
            if (normalized.Length < _stableLength
                || !normalized.AsSpan(0, _stableLength).SequenceEqual(_stableSource.AsSpan(0, _stableLength)))
            {
                return false;
            }

            while (document.Blocks.Count > _stableBlockCount)
            {
                // A selection reaching a removed block would otherwise widen over the text added back
                // in its place. Clearing it leaves the caller to restore what was captured.
                var removed = document.Blocks.LastBlock;
                if (DocumentViewer.Selection.End.CompareTo(removed.ContentStart) > 0)
                    DocumentViewer.Selection.Select(document.ContentStart, document.ContentStart);
                document.Blocks.Remove(removed);
            }
            _renderDocument = document;
            try
            {
                PopulateMarkdownDocument(normalized, _stableLength);
            }
            finally
            {
                _renderDocument = null;
            }
            return true;
        }

        private static string NormalizeLineEndings(string markdown)
            => markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

        private void PopulateMarkdownDocument(string normalized, int startOffset)
        {
            var lines = normalized[startOffset..].Split('\n');
            var stableLength = startOffset;
            var stableBlockCount = CurrentDocument.Blocks.Count;
            var paragraphLines = new List<string>();
            var codeBuilder = new StringBuilder();
            var displayMathBuilder = new StringBuilder();
            var inCodeBlock = false;
            var codeLanguage = string.Empty;
            var codeFence = default(MarkdownCodeFence);
            var displayMathOpening = string.Empty;
            var displayMathClosing = string.Empty;

            void FlushParagraph()
            {
                if (paragraphLines.Count == 0)
                    return;

                AddTextBlock(string.Join(" ", paragraphLines.Select(line => line.Trim())), margin: new Thickness(0, 0, 0, 8));
                paragraphLines.Clear();
            }

            void FlushCodeBlock()
            {
                var code = codeBuilder.ToString();
                if (code.EndsWith(Environment.NewLine, StringComparison.Ordinal))
                    code = code[..^Environment.NewLine.Length];
                AddCodeBlock(code, codeLanguage);
                codeBuilder.Clear();
            }

            for (int lineIndex = 0, lineStart = startOffset; lineIndex < lines.Length; lineStart += lines[lineIndex].Length + 1, lineIndex++)
            {
                var sourceLine = lines[lineIndex];
                var line = sourceLine ?? string.Empty;
                if (!string.IsNullOrEmpty(displayMathClosing))
                {
                    var closingIndex = line.IndexOf(displayMathClosing, StringComparison.Ordinal);
                    if (closingIndex < 0)
                    {
                        displayMathBuilder.AppendLine(line);
                        continue;
                    }

                    displayMathBuilder.Append(line[..closingIndex]);
                    var latex = displayMathBuilder.ToString().Trim();
                    AddFormulaBlock(latex, displayMathOpening + latex + displayMathClosing);
                    displayMathBuilder.Clear();
                    displayMathOpening = string.Empty;
                    var consumedClosing = displayMathClosing;
                    displayMathClosing = string.Empty;
                    var remainder = line[(closingIndex + consumedClosing.Length)..].Trim();
                    if (remainder.Length > 0)
                        paragraphLines.Add(remainder);
                    continue;
                }

                if (inCodeBlock)
                {
                    if (IsClosingCodeFence(line, codeFence))
                    {
                        FlushCodeBlock();
                        inCodeBlock = false;
                        codeLanguage = string.Empty;
                        codeFence = default;
                    }
                    else
                    {
                        codeBuilder.AppendLine(RemoveCodeFenceIndentation(line, codeFence.Indentation));
                    }
                    continue;
                }

                if (TryParseOpeningCodeFence(line, out codeFence))
                {
                    FlushParagraph();
                    codeLanguage = NormalizeCodeLanguage(codeFence.Info);
                    inCodeBlock = true;
                    continue;
                }

                if (CopilotMarkdownMath.TryParseDisplayLine(line, out var formulas))
                {
                    FlushParagraph();
                    foreach (var formula in formulas)
                        AddFormulaBlock(formula.Content, formula.OriginalText);
                    continue;
                }

                if (CopilotMarkdownMath.TryStartDisplayBlock(
                    line,
                    out displayMathOpening,
                    out displayMathClosing,
                    out var initialMathContent))
                {
                    FlushParagraph();
                    if (initialMathContent.Length > 0)
                        displayMathBuilder.AppendLine(initialMathContent);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    FlushParagraph();
                    // Outside code and math, no parser state crosses a completed blank line, and
                    // earlier blocks never depend on text after it. The final line may still grow.
                    if (lineIndex < lines.Length - 1)
                    {
                        stableLength = lineStart + line.Length + 1;
                        stableBlockCount = CurrentDocument.Blocks.Count;
                    }
                    continue;
                }

                if (CopilotMarkdownTableParser.TryParse(lines, lineIndex, out var table, out var consumedLineCount))
                {
                    FlushParagraph();
                    AddTable(table);
                    for (var skipped = 0; skipped < consumedLineCount - 1; skipped++)
                        lineStart += lines[lineIndex++].Length + 1;
                    continue;
                }

                if (ThematicBreakRegex.IsMatch(line))
                {
                    FlushParagraph();
                    AddThematicBreak();
                    continue;
                }

                var headingMatch = HeadingRegex.Match(line.Trim());
                if (headingMatch.Success)
                {
                    FlushParagraph();
                    AddHeading(headingMatch.Groups[2].Value.Trim(), headingMatch.Groups[1].Value.Length);
                    continue;
                }

                var unorderedMatch = UnorderedListRegex.Match(line);
                if (unorderedMatch.Success)
                {
                    FlushParagraph();
                    AddListItem("•", unorderedMatch.Groups[1].Value.Trim());
                    continue;
                }

                var orderedMatch = OrderedListRegex.Match(line);
                if (orderedMatch.Success)
                {
                    FlushParagraph();
                    AddListItem(orderedMatch.Groups[1].Value + ".", orderedMatch.Groups[2].Value.Trim());
                    continue;
                }

                var trimmed = line.TrimStart();
                if (trimmed.StartsWith('>'))
                {
                    FlushParagraph();
                    AddQuote(trimmed[1..].TrimStart());
                    continue;
                }

                paragraphLines.Add(line);
            }

            FlushParagraph();
            if (inCodeBlock)
                FlushCodeBlock();
            if (!string.IsNullOrEmpty(displayMathClosing))
            {
                var rawFormula = displayMathOpening + displayMathBuilder.ToString().TrimEnd();
                AddTextBlock(rawFormula, new Thickness(0, 2, 0, 8));
            }

            _stableSource = normalized;
            _stableLength = stableLength;
            _stableBlockCount = stableBlockCount;
        }

        private void AddHeading(string text, int level)
        {
            var fontSize = level switch
            {
                1 => 18d,
                2 => 16d,
                3 => 14d,
                _ => 13d,
            };
            var block = CreateParagraph(fontSize, FontWeights.SemiBold, new Thickness(0, level <= 2 ? 8 : 5, 0, 6));
            AddInlines(block.Inlines, text);
            CurrentDocument.Blocks.Add(block);
        }

        private void AddTextBlock(string text, Thickness margin)
        {
            var block = CreateParagraph(13, FontWeights.Normal, margin);
            AddInlines(block.Inlines, text);
            CurrentDocument.Blocks.Add(block);
        }

        private void AddListItem(string marker, string text)
        {
            var block = CreateParagraph(13, FontWeights.Normal, new Thickness(14, 0, 0, 5));
            var markerRun = new Run(marker + " ") { FontWeight = FontWeights.SemiBold };
            markerRun.SetResourceReference(TextElement.ForegroundProperty, "SecondaryTextBrush");
            block.Inlines.Add(markerRun);
            AddInlines(block.Inlines, text);
            CurrentDocument.Blocks.Add(block);
        }

        private void AddQuote(string text)
        {
            var block = CreateParagraph(13, FontWeights.Normal, new Thickness(0, 2, 0, 8));
            block.BorderThickness = new Thickness(3, 0, 0, 0);
            block.Padding = new Thickness(10, 2, 0, 2);
            block.SetResourceReference(Block.BorderBrushProperty, "PrimaryBrush");
            AddInlines(block.Inlines, text);
            CurrentDocument.Blocks.Add(block);
        }

        private void AddCodeBlock(string code, string language)
        {
            var header = new DockPanel
            {
                LastChildFill = true,
                Margin = new Thickness(10, 5, 7, 4),
            };
            var copyButton = new Button
            {
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Content = LocalizedText.Get("复制"),
                Cursor = Cursors.Hand,
                FontSize = 11,
                Padding = new Thickness(6, 2, 6, 2),
                Tag = code,
                ToolTip = LocalizedText.Get("复制代码"),
            };
            copyButton.SetResourceReference(Control.ForegroundProperty, "GlobalTextBrush");
            AutomationProperties.SetName(copyButton, LocalizedText.Get("复制代码"));
            copyButton.Click += CopyCodeButton_Click;
            DockPanel.SetDock(copyButton, Dock.Right);
            header.Children.Add(copyButton);

            var languageLabel = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Opacity = 0.58,
                Text = string.IsNullOrWhiteSpace(language) ? LocalizedText.Get("代码") : language,
                VerticalAlignment = VerticalAlignment.Center,
            };
            languageLabel.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            header.Children.Add(languageLabel);

            var lineCount = Math.Max(1, code.Count(character => character == '\n') + 1);
            var codeTextBox = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Height = Math.Min(420, Math.Max(38, lineCount * 18 + 16)),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                IsReadOnly = true,
                Padding = new Thickness(10, 6, 10, 8),
                Text = code,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            codeTextBox.SetResourceReference(Control.ForegroundProperty, "GlobalTextBrush");
            AutomationProperties.SetName(codeTextBox, LocalizedText.Get("代码内容"));

            var content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.Children.Add(header);
            Grid.SetRow(codeTextBox, 1);
            content.Children.Add(codeTextBox);

            var border = new Border
            {
                BorderThickness = new Thickness(1),
                Child = content,
                CornerRadius = new CornerRadius(5),
            };
            border.SetResourceReference(Border.BackgroundProperty, "ButtonBackground");
            border.SetResourceReference(Border.BorderBrushProperty, "ButtonBorderBrush");

            var block = new BlockUIContainer(border)
            {
                Margin = new Thickness(0, 2, 0, 10),
                Tag = codeTextBox,
            };
            CurrentDocument.Blocks.Add(block);
        }

        private static bool TryParseOpeningCodeFence(string line, out MarkdownCodeFence fence)
        {
            fence = default;
            var indentation = 0;
            while (indentation < line.Length && line[indentation] == ' ')
                indentation++;
            if (indentation > 3 || indentation >= line.Length)
                return false;

            var marker = line[indentation];
            if (marker is not ('`' or '~'))
                return false;

            var markerEnd = indentation;
            while (markerEnd < line.Length && line[markerEnd] == marker)
                markerEnd++;
            var markerLength = markerEnd - indentation;
            if (markerLength < 3)
                return false;

            var info = line[markerEnd..].Trim();
            if (marker == '`' && info.Contains('`'))
                return false;

            fence = new MarkdownCodeFence(marker, markerLength, indentation, info);
            return true;
        }

        private static bool IsClosingCodeFence(string line, MarkdownCodeFence fence)
        {
            var indentation = 0;
            while (indentation < line.Length && line[indentation] == ' ')
                indentation++;
            if (indentation > 3 || indentation >= line.Length || line[indentation] != fence.Marker)
                return false;

            var markerEnd = indentation;
            while (markerEnd < line.Length && line[markerEnd] == fence.Marker)
                markerEnd++;
            if (markerEnd - indentation < fence.Length)
                return false;

            for (var index = markerEnd; index < line.Length; index++)
            {
                if (line[index] is not (' ' or '\t'))
                    return false;
            }
            return true;
        }

        private static string RemoveCodeFenceIndentation(string line, int indentation)
        {
            if (indentation <= 0 || line.Length == 0)
                return line;

            var index = 0;
            var column = 0;
            while (index < line.Length && column < indentation)
            {
                if (line[index] == ' ')
                {
                    index++;
                    column++;
                    continue;
                }

                if (line[index] != '\t')
                    break;

                var nextTabStop = column + 4 - column % 4;
                index++;
                if (nextTabStop > indentation)
                    return new string(' ', nextTabStop - indentation) + line[index..];
                column = nextTabStop;
            }
            return line[index..];
        }

        private static string NormalizeCodeLanguage(string? fenceInfo)
        {
            var language = (fenceInfo ?? string.Empty).Trim();
            if (language.Length == 0)
                return string.Empty;

            language = language.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0]
                .Trim('{', '}', '.');
            return language.Length <= 32 ? language : language[..32];
        }

        private static void CopyCodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string code } button || string.IsNullOrEmpty(code))
                return;

            try
            {
                Clipboard.SetText(code);
                button.Content = LocalizedText.Get("已复制");
                button.ToolTip = LocalizedText.Get("代码已复制到剪贴板");
            }
            catch (Exception ex)
            {
                button.Content = LocalizedText.Get("复制失败");
                button.ToolTip = CopilotUserFacingErrorFormatter.Sanitize(ex.Message);
            }
        }


    }
}

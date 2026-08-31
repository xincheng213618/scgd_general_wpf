using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using WpfMath.Controls;
using WpfMath.Parsers;
using XamlMath.Exceptions;

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
        private static readonly Regex InlineRegex = new(@"(\*\*[^*\r\n]+\*\*|`[^`\r\n]+`|\*[^*\r\n]+\*|\[[^\]\r\n]+\]\((?:[^()\r\n]|\([^()\r\n]*\))+\)|<https?://[^<>\s]+>)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ThematicBreakRegex = new(@"^\s{0,3}((\*\s*){3,}|(-\s*){3,}|(_\s*){3,})$", RegexOptions.Compiled);

        private readonly DispatcherTimer _renderTimer;
        private string _pendingMarkdown = string.Empty;
        private FlowDocument? _renderDocument;
        private double _lastRenderedWidth;

        public CopilotMarkdownView()
        {
            InitializeComponent();
            _renderTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100),
            };
            _renderTimer.Tick += RenderTimer_Tick;
            Loaded += CopilotMarkdownView_Loaded;
            SizeChanged += CopilotMarkdownView_SizeChanged;
            Unloaded += CopilotMarkdownView_Unloaded;
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

        private void CopilotMarkdownView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.WidthChanged || _lastRenderedWidth <= 0 || Math.Abs(e.NewSize.Width - _lastRenderedWidth) < 24)
                return;

            ScheduleRender();
        }

        private void RenderTimer_Tick(object? sender, EventArgs e)
        {
            _renderTimer.Stop();
            RenderMarkdown(_pendingMarkdown);
        }

        private void ScheduleRender()
        {
            _pendingMarkdown = Markdown ?? string.Empty;
            if (!IsLoaded)
                return;

            _renderTimer.Stop();
            _renderTimer.Start();
        }

        private void RenderMarkdown(string markdown)
        {
            _lastRenderedWidth = ActualWidth;
            try
            {
                DocumentViewer.Document = BuildMarkdownDocument(markdown);
            }
            catch (Exception)
            {
                // Formula controls can fail while WPF attaches the new document,
                // after the LaTeX parser has already accepted the expression.
                // Keep the chat usable by replacing the whole document with text.
                DocumentViewer.Document = CreatePlainTextDocument(markdown);
            }
        }

        private FlowDocument BuildMarkdownDocument(string markdown)
        {
            var document = CreateDocument();
            _renderDocument = document;
            try
            {
                if (!string.IsNullOrWhiteSpace(markdown))
                    PopulateMarkdownDocument(markdown);
                return document;
            }
            finally
            {
                _renderDocument = null;
            }
        }

        private void PopulateMarkdownDocument(string markdown)
        {
            var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var lines = normalized.Split('\n');
            var paragraphLines = new List<string>();
            var codeBuilder = new StringBuilder();
            var displayMathBuilder = new StringBuilder();
            var inCodeBlock = false;
            var codeLanguage = string.Empty;
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
                if (codeBuilder.Length == 0)
                    return;

                AddCodeBlock(codeBuilder.ToString().TrimEnd('\r', '\n'), codeLanguage);
                codeBuilder.Clear();
            }

            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
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

                var trimmedStart = line.TrimStart();
                if (trimmedStart.StartsWith("```", StringComparison.Ordinal))
                {
                    FlushParagraph();
                    if (inCodeBlock)
                    {
                        FlushCodeBlock();
                        codeLanguage = string.Empty;
                    }
                    else
                    {
                        codeLanguage = NormalizeCodeLanguage(trimmedStart[3..]);
                    }

                    inCodeBlock = !inCodeBlock;
                    continue;
                }

                if (inCodeBlock)
                {
                    codeBuilder.AppendLine(line);
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
                    continue;
                }

                if (CopilotMarkdownTableParser.TryParse(lines, lineIndex, out var table, out var consumedLineCount))
                {
                    FlushParagraph();
                    AddTable(table);
                    lineIndex += consumedLineCount - 1;
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
            FlushCodeBlock();
            if (!string.IsNullOrEmpty(displayMathClosing))
            {
                var rawFormula = displayMathOpening + displayMathBuilder.ToString().TrimEnd();
                AddTextBlock(rawFormula, new Thickness(0, 2, 0, 8));
            }
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
                Content = "复制",
                Cursor = Cursors.Hand,
                FontSize = 11,
                Padding = new Thickness(6, 2, 6, 2),
                Tag = code,
                ToolTip = "复制代码",
            };
            copyButton.SetResourceReference(Control.ForegroundProperty, "GlobalTextBrush");
            AutomationProperties.SetName(copyButton, "复制代码");
            copyButton.Click += CopyCodeButton_Click;
            DockPanel.SetDock(copyButton, Dock.Right);
            header.Children.Add(copyButton);

            var languageLabel = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Opacity = 0.58,
                Text = string.IsNullOrWhiteSpace(language) ? "代码" : language,
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
            AutomationProperties.SetName(codeTextBox, "代码内容");

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
            };
            CurrentDocument.Blocks.Add(block);
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
                button.Content = "已复制";
                button.ToolTip = "代码已复制到剪贴板";
            }
            catch (Exception ex)
            {
                button.Content = "复制失败";
                button.ToolTip = CopilotUserFacingErrorFormatter.Sanitize(ex.Message);
            }
        }


    }
}

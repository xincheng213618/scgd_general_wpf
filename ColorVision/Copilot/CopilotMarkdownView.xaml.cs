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

                if (inCodeBlock)
                {
                    codeBuilder.AppendLine(line);
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

        private void AddTable(CopilotMarkdownTableModel model)
        {
            var availableWidth = GetAvailableTableWidth();
            if (ShouldUseKeyValueLayout(model, availableWidth))
            {
                AddTableAsKeyValueRecords(model);
                return;
            }

            var table = new Table
            {
                CellSpacing = 0,
                Margin = new Thickness(0, 3, 0, 10),
            };
            table.SetResourceReference(TextElement.ForegroundProperty, "GlobalTextBrush");
            var columnWidths = CalculateTableColumnWidths(model, availableWidth);
            for (var columnIndex = 0; columnIndex < model.Headers.Count; columnIndex++)
            {
                table.Columns.Add(new TableColumn { Width = new GridLength(columnWidths[columnIndex]) });
            }

            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(CreateTableRow(model.Headers, model.Alignments, isHeader: true, isSection: false));
            foreach (var cells in model.Rows)
            {
                var isSection = !string.IsNullOrWhiteSpace(cells[0]) && cells.Skip(1).All(string.IsNullOrWhiteSpace);
                rowGroup.Rows.Add(CreateTableRow(cells, model.Alignments, isHeader: false, isSection));
            }
            if (model.WasTruncated)
            {
                var truncatedCells = Enumerable.Repeat(string.Empty, model.Headers.Count).ToArray();
                truncatedCells[0] = $"… table limited to {CopilotMarkdownTableParser.MaximumRows} rows";
                rowGroup.Rows.Add(CreateTableRow(truncatedCells, model.Alignments, isHeader: false, isSection: true));
            }

            table.RowGroups.Add(rowGroup);
            CurrentDocument.Blocks.Add(table);
        }

        private double GetAvailableTableWidth()
        {
            var width = DocumentViewer.ActualWidth;
            if (!double.IsFinite(width) || width < 1)
                width = ActualWidth;
            if (!double.IsFinite(width) || width < 1)
                width = 640;
            return Math.Max(160, width - 4);
        }

        private static bool ShouldUseKeyValueLayout(CopilotMarkdownTableModel model, double availableWidth)
        {
            if (model.Headers.Count < 2)
                return false;

            var hasLongValue = model.Rows.Take(64).SelectMany(row => row.Skip(1)).Any(value => EstimateTableCellWidth(value) >= 220);
            return model.Headers.Count == 2
                ? availableWidth < 320 && hasLongValue
                : availableWidth < model.Headers.Count * 140 && hasLongValue;
        }

        private static double[] CalculateTableColumnWidths(CopilotMarkdownTableModel model, double availableWidth)
        {
            var columnCount = model.Headers.Count;
            var minimumWidths = new double[columnCount];
            var preferredWidths = new double[columnCount];
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var isFirst = columnIndex == 0;
                var isCompactLast = columnCount > 2 && columnIndex == columnCount - 1;
                var minimumWidth = isFirst ? 96 : isCompactLast ? 72 : 88;
                var maximumWidth = isFirst ? 220 : isCompactLast ? 140 : 420;
                minimumWidths[columnIndex] = minimumWidth;
                preferredWidths[columnIndex] = MeasureBoundedTableColumn(model, columnIndex, minimumWidth, maximumWidth);
            }

            var minimumTotal = minimumWidths.Sum();
            if (minimumTotal >= availableWidth)
            {
                var scale = availableWidth / minimumTotal;
                return minimumWidths.Select(width => width * scale).ToArray();
            }

            var preferredTotal = preferredWidths.Sum();
            if (preferredTotal <= availableWidth)
            {
                var result = preferredWidths.ToArray();
                var flexibleColumns = Enumerable.Range(1, Math.Max(0, columnCount - 1))
                    .Where(index => columnCount <= 2 || index < columnCount - 1)
                    .ToArray();
                if (flexibleColumns.Length == 0)
                    flexibleColumns = [columnCount - 1];

                var extraPerColumn = (availableWidth - preferredTotal) / flexibleColumns.Length;
                foreach (var columnIndex in flexibleColumns)
                    result[columnIndex] += extraPerColumn;
                return result;
            }

            var remainingWidth = availableWidth - minimumTotal;
            var growthTotal = preferredWidths.Select((width, index) => width - minimumWidths[index]).Sum();
            if (growthTotal <= 0)
                return minimumWidths;

            return minimumWidths
                .Select((width, index) => width + remainingWidth * (preferredWidths[index] - width) / growthTotal)
                .ToArray();
        }

        private void AddTableAsKeyValueRecords(CopilotMarkdownTableModel model)
        {
            foreach (var row in model.Rows)
            {
                var section = new Section
                {
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Margin = new Thickness(0, 2, 0, 6),
                    Padding = new Thickness(0, 0, 0, 6),
                };
                section.SetResourceReference(Block.BorderBrushProperty, "ButtonBorderBrush");

                if (model.Headers.Count == 2 && !string.IsNullOrWhiteSpace(row[0]))
                {
                    AddKeyValuePair(section, row[0], row[1]);
                }
                else
                {
                    for (var columnIndex = 0; columnIndex < model.Headers.Count; columnIndex++)
                    {
                        if (!string.IsNullOrWhiteSpace(row[columnIndex]))
                            AddKeyValuePair(section, model.Headers[columnIndex], row[columnIndex]);
                    }
                }

                if (section.Blocks.Count > 0)
                    CurrentDocument.Blocks.Add(section);
            }

            if (model.WasTruncated)
                AddTextBlock($"… table limited to {CopilotMarkdownTableParser.MaximumRows} rows", new Thickness(0, 0, 0, 8));
        }

        private static void AddKeyValuePair(Section section, string key, string value)
        {
            var paragraph = CreateParagraph(12.5, FontWeights.Normal, new Thickness(0, 0, 0, 4));
            var keyRun = new Run(key) { FontWeight = FontWeights.SemiBold };
            paragraph.Inlines.Add(keyRun);
            paragraph.Inlines.Add(new LineBreak());
            AddInlines(paragraph.Inlines, value);
            section.Blocks.Add(paragraph);
        }

        private static double MeasureBoundedTableColumn(
            CopilotMarkdownTableModel model,
            int columnIndex,
            double minimumWidth,
            double maximumWidth)
        {
            var values = model.Rows
                .Take(64)
                .Select(row => row[columnIndex])
                .Prepend(model.Headers[columnIndex]);
            var contentWidth = values.Max(EstimateTableCellWidth) + 28;
            return Math.Clamp(contentWidth, minimumWidth, maximumWidth);
        }

        private static double EstimateTableCellWidth(string? value)
        {
            var width = 0d;
            foreach (var character in value ?? string.Empty)
            {
                width += character <= 0x7f ? 7 : 13;
                if (width >= 220)
                    return width;
            }
            return width;
        }

        private static TableRow CreateTableRow(
            IReadOnlyList<string> cells,
            IReadOnlyList<CopilotMarkdownTableAlignment> alignments,
            bool isHeader,
            bool isSection)
        {
            var row = new TableRow();
            for (var columnIndex = 0; columnIndex < cells.Count; columnIndex++)
            {
                var paragraph = CreateParagraph(12.5, isHeader || isSection ? FontWeights.SemiBold : FontWeights.Normal, new Thickness(0));
                paragraph.LineHeight = 18;
                paragraph.TextAlignment = alignments[columnIndex] switch
                {
                    CopilotMarkdownTableAlignment.Center => TextAlignment.Center,
                    CopilotMarkdownTableAlignment.Right => TextAlignment.Right,
                    _ => TextAlignment.Left,
                };
                AddInlines(paragraph.Inlines, cells[columnIndex]);

                var cell = new TableCell(paragraph)
                {
                    BorderThickness = new Thickness(columnIndex == 0 ? 1 : 0, isHeader ? 1 : 0, 1, 1),
                    Padding = new Thickness(7, isHeader || isSection ? 5 : 4, 7, isHeader || isSection ? 5 : 4),
                };
                cell.SetResourceReference(Block.BorderBrushProperty, "ButtonBorderBrush");
                if (isHeader || isSection)
                    cell.SetResourceReference(Block.BackgroundProperty, "GlobalBorderBrush1");
                row.Cells.Add(cell);
            }
            return row;
        }

        private void AddThematicBreak()
        {
            var block = new Paragraph
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(0, 6, 0, 10),
            };
            block.SetResourceReference(Block.BorderBrushProperty, "ButtonBorderBrush");
            CurrentDocument.Blocks.Add(block);
        }

        private void AddFormulaBlock(string latex, string originalText)
        {
            if (!TryCreateFormulaControl(latex, isDisplay: true, out var formula))
            {
                AddTextBlock(originalText, new Thickness(0, 2, 0, 8));
                return;
            }

            var viewbox = new Viewbox
            {
                Child = formula,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = Math.Max(320, DocumentViewer.ActualWidth - 24),
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
            };
            var block = new BlockUIContainer(viewbox)
            {
                Margin = new Thickness(0, 4, 0, 10),
            };
            CurrentDocument.Blocks.Add(block);
        }

        private FlowDocument CurrentDocument => _renderDocument
            ?? throw new InvalidOperationException("Markdown blocks can only be added while a document is being built.");

        private static FlowDocument CreateDocument()
        {
            return new FlowDocument
            {
                ColumnGap = 0,
                ColumnWidth = 100000,
                PagePadding = new Thickness(0),
            };
        }

        private static FlowDocument CreatePlainTextDocument(string markdown)
        {
            var document = CreateDocument();
            if (!string.IsNullOrEmpty(markdown))
                document.Blocks.Add(CreatePlainTextParagraph(markdown));
            return document;
        }

        private static Paragraph CreatePlainTextParagraph(string text)
        {
            var paragraph = CreateParagraph(13, FontWeights.Normal, new Thickness(0, 0, 0, 8));
            paragraph.Inlines.Add(new Run(text));
            return paragraph;
        }

        private static Paragraph CreateParagraph(double fontSize, FontWeight fontWeight, Thickness margin)
        {
            var block = new Paragraph
            {
                FontSize = fontSize,
                FontWeight = fontWeight,
                LineHeight = fontSize * 1.55,
                Margin = margin,
            };
            block.SetResourceReference(TextElement.ForegroundProperty, "GlobalTextBrush");
            return block;
        }

        private static void AddInlines(InlineCollection inlines, string text)
        {
            var currentIndex = 0;
            foreach (Match match in InlineRegex.Matches(text))
            {
                if (match.Index > currentIndex)
                    AddMathAwareText(inlines, text[currentIndex..match.Index]);

                var token = match.Value;
                if (token.StartsWith("**", StringComparison.Ordinal) && token.EndsWith("**", StringComparison.Ordinal))
                {
                    var span = new Span { FontWeight = FontWeights.SemiBold };
                    AddMathAwareText(span.Inlines, token[2..^2]);
                    inlines.Add(span);
                }
                else if (token.StartsWith('`') && token.EndsWith('`'))
                {
                    var codeRun = new Run(token[1..^1]) { FontFamily = new FontFamily("Consolas") };
                    codeRun.SetResourceReference(TextElement.BackgroundProperty, "GlobalBorderBrush1");
                    inlines.Add(codeRun);
                }
                else if (token.StartsWith('*') && token.EndsWith('*'))
                {
                    var span = new Span { FontStyle = FontStyles.Italic };
                    AddMathAwareText(span.Inlines, token[1..^1]);
                    inlines.Add(span);
                }
                else if (TryParseLinkToken(token, out var linkText, out var linkTarget))
                {
                    AddLinkInline(inlines, linkText, linkTarget);
                }
                else
                {
                    AddMathAwareText(inlines, token);
                }

                currentIndex = match.Index + match.Length;
            }

            if (currentIndex < text.Length)
                AddMathAwareText(inlines, text[currentIndex..]);
        }

        private static bool TryParseLinkToken(string token, out string linkText, out string linkTarget)
        {
            linkText = string.Empty;
            linkTarget = string.Empty;
            if (token.StartsWith('<') && token.EndsWith('>'))
            {
                linkTarget = token[1..^1].Trim();
                linkText = linkTarget;
                return linkTarget.Length > 0;
            }

            if (!token.StartsWith('[') || !token.EndsWith(')'))
                return false;

            var separatorIndex = token.IndexOf("](", StringComparison.Ordinal);
            if (separatorIndex <= 1)
                return false;

            linkText = token[1..separatorIndex];
            var targetAndTitle = token[(separatorIndex + 2)..^1].Trim();
            if (targetAndTitle.StartsWith('<'))
            {
                var closingAngleBracket = targetAndTitle.IndexOf('>');
                linkTarget = closingAngleBracket > 1 ? targetAndTitle[1..closingAngleBracket] : string.Empty;
            }
            else
            {
                linkTarget = targetAndTitle.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault() ?? string.Empty;
            }
            return linkTarget.Length > 0;
        }

        private static void AddLinkInline(InlineCollection inlines, string linkText, string linkTarget)
        {
            if (TryCreateSafeWebUri(linkTarget, out var uri))
            {
                var webHyperlink = new Hyperlink(new Run(linkText))
                {
                    Cursor = Cursors.Hand,
                    NavigateUri = uri,
                    ToolTip = uri.AbsoluteUri,
                };
                webHyperlink.SetResourceReference(TextElement.ForegroundProperty, "PrimaryBrush");
                AutomationProperties.SetName(webHyperlink, $"打开链接：{linkText}");
                webHyperlink.RequestNavigate += Hyperlink_RequestNavigate;
                inlines.Add(webHyperlink);
                return;
            }
            if (CopilotLocalFileLinkNavigator.TryResolve(linkTarget, out var fileTarget))
            {
                var fileHyperlink = new Hyperlink(new Run(linkText))
                {
                    Cursor = Cursors.Hand,
                    Tag = fileTarget,
                    ToolTip = CopilotLocalFileLinkNavigator.BuildToolTip(fileTarget),
                };
                fileHyperlink.SetResourceReference(TextElement.ForegroundProperty, "PrimaryBrush");
                AutomationProperties.SetName(fileHyperlink, $"打开工作区文件：{linkText}");
                fileHyperlink.Click += LocalFileHyperlink_Click;
                inlines.Add(fileHyperlink);
                return;
            }

            var fallback = new Run(linkText);
            ToolTipService.SetToolTip(fallback, "仅支持 HTTP/HTTPS 或当前工作区内的文件链接");
            inlines.Add(fallback);
        }

        private static bool TryCreateSafeWebUri(string? value, out Uri uri)
        {
            uri = null!;
            var candidate = (value ?? string.Empty).Trim();
            if (candidate.Length == 0
                || candidate.Length > 4096
                || !Uri.TryCreate(candidate, UriKind.Absolute, out var parsedUri)
                || (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
            {
                return false;
            }

            uri = parsedUri;
            return true;
        }

        private static void LocalFileHyperlink_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is not Hyperlink { Tag: CopilotLocalFileLinkTarget target } hyperlink)
                return;

            if (!CopilotLocalFileLinkNavigator.TryOpen(target, out var errorMessage))
                hyperlink.ToolTip = "无法打开文件：" + CopilotUserFacingErrorFormatter.Sanitize(errorMessage);
        }

        private static void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = true;
            if (!TryCreateSafeWebUri(e.Uri?.AbsoluteUri, out var uri))
                return;

            try
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                if (sender is Hyperlink hyperlink)
                    hyperlink.ToolTip = "无法打开链接：" + CopilotUserFacingErrorFormatter.Sanitize(ex.Message);
            }
        }


        private static void AddMathAwareText(InlineCollection inlines, string text)
        {
            foreach (var segment in CopilotMarkdownMath.ParseInline(text))
            {
                if (!segment.IsMath)
                {
                    inlines.Add(new Run(segment.Content));
                    continue;
                }

                if (!TryCreateFormulaControl(segment.Content, isDisplay: false, out var formula))
                {
                    inlines.Add(new Run(segment.OriginalText));
                    continue;
                }

                inlines.Add(new InlineUIContainer(formula)
                {
                    BaselineAlignment = BaselineAlignment.Center,
                });
            }
        }

        private static bool TryCreateFormulaControl(string latex, bool isDisplay, out FormulaControl formula)
        {
            formula = null!;
            if (string.IsNullOrWhiteSpace(latex))
                return false;

            try
            {
                _ = WpfTeXFormulaParser.Instance.Parse(latex);
                formula = new FormulaControl
                {
                    ErrorTemplate = null!,
                    Formula = latex,
                    Focusable = false,
                    IsHitTestVisible = true,
                    Margin = isDisplay ? new Thickness(0) : new Thickness(2, 0, 2, 0),
                    Padding = new Thickness(0),
                    Scale = isDisplay ? 18 : 14,
                    SnapsToDevicePixels = true,
                    ToolTip = latex,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                formula.SetResourceReference(Control.ForegroundProperty, "GlobalTextBrush");
                AutomationProperties.SetName(formula, "Math formula: " + latex);
                AutomationProperties.SetHelpText(formula, latex);
                return true;
            }
            catch (TexException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

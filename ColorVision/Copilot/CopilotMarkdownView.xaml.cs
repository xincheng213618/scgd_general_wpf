using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
        private static readonly Regex InlineRegex = new(@"(\*\*[^*\r\n]+\*\*|`[^`\r\n]+`|\*[^*\r\n]+\*|\[[^\]\r\n]+\]\([^)]+\))", RegexOptions.Compiled);

        private readonly DispatcherTimer _renderTimer;
        private string _pendingMarkdown = string.Empty;

        public CopilotMarkdownView()
        {
            InitializeComponent();
            _renderTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100),
            };
            _renderTimer.Tick += RenderTimer_Tick;
            Loaded += CopilotMarkdownView_Loaded;
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
            MarkdownDocument.Blocks.Clear();
            if (string.IsNullOrWhiteSpace(markdown))
                return;

            var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var lines = normalized.Split('\n');
            var paragraphLines = new List<string>();
            var codeBuilder = new StringBuilder();
            var inCodeBlock = false;

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

                AddCodeBlock(codeBuilder.ToString().TrimEnd());
                codeBuilder.Clear();
            }

            foreach (var sourceLine in lines)
            {
                var line = sourceLine ?? string.Empty;
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    FlushParagraph();
                    if (inCodeBlock)
                        FlushCodeBlock();

                    inCodeBlock = !inCodeBlock;
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
            MarkdownDocument.Blocks.Add(block);
        }

        private void AddTextBlock(string text, Thickness margin)
        {
            var block = CreateParagraph(13, FontWeights.Normal, margin);
            AddInlines(block.Inlines, text);
            MarkdownDocument.Blocks.Add(block);
        }

        private void AddListItem(string marker, string text)
        {
            var block = CreateParagraph(13, FontWeights.Normal, new Thickness(14, 0, 0, 5));
            var markerRun = new Run(marker + " ") { FontWeight = FontWeights.SemiBold };
            markerRun.SetResourceReference(TextElement.ForegroundProperty, "SecondaryTextBrush");
            block.Inlines.Add(markerRun);
            AddInlines(block.Inlines, text);
            MarkdownDocument.Blocks.Add(block);
        }

        private void AddQuote(string text)
        {
            var block = CreateParagraph(13, FontWeights.Normal, new Thickness(0, 2, 0, 8));
            block.BorderThickness = new Thickness(3, 0, 0, 0);
            block.Padding = new Thickness(10, 2, 0, 2);
            block.SetResourceReference(Block.BorderBrushProperty, "PrimaryBrush");
            AddInlines(block.Inlines, text);
            MarkdownDocument.Blocks.Add(block);
        }

        private void AddCodeBlock(string code)
        {
            var block = CreateParagraph(12, FontWeights.Normal, new Thickness(0, 2, 0, 10));
            block.FontFamily = new FontFamily("Consolas");
            block.BorderThickness = new Thickness(1);
            block.Padding = new Thickness(10, 8, 10, 8);
            block.SetResourceReference(Block.BackgroundProperty, "ButtonBackground");
            block.SetResourceReference(Block.BorderBrushProperty, "ButtonBorderBrush");
            block.Inlines.Add(new Run(code));
            MarkdownDocument.Blocks.Add(block);
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
                    inlines.Add(new Run(text[currentIndex..match.Index]));

                var token = match.Value;
                if (token.StartsWith("**", StringComparison.Ordinal) && token.EndsWith("**", StringComparison.Ordinal))
                {
                    inlines.Add(new Run(token[2..^2]) { FontWeight = FontWeights.SemiBold });
                }
                else if (token.StartsWith('`') && token.EndsWith('`'))
                {
                    var codeRun = new Run(token[1..^1]) { FontFamily = new FontFamily("Consolas") };
                    codeRun.SetResourceReference(TextElement.BackgroundProperty, "GlobalBorderBrush1");
                    inlines.Add(codeRun);
                }
                else if (token.StartsWith('*') && token.EndsWith('*'))
                {
                    inlines.Add(new Run(token[1..^1]) { FontStyle = FontStyles.Italic });
                }
                else
                {
                    var closingBracket = token.IndexOf(']');
                    var linkText = closingBracket > 1 ? token[1..closingBracket] : token;
                    var linkRun = new Run(linkText) { TextDecorations = TextDecorations.Underline };
                    linkRun.SetResourceReference(TextElement.ForegroundProperty, "PrimaryBrush");
                    inlines.Add(linkRun);
                }

                currentIndex = match.Index + match.Length;
            }

            if (currentIndex < text.Length)
                inlines.Add(new Run(text[currentIndex..]));
        }
    }
}

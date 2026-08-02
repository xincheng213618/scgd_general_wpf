#pragma warning disable CA1822
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using WpfMath.Controls;
using WpfMath.Parsers;
using XamlMath.Exceptions;

namespace ColorVision.Copilot
{
    public partial class CopilotMarkdownView
    {
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
                fileHyperlink.ContextMenu = CreateLocalFileContextMenu(fileTarget);
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

        private static ContextMenu CreateLocalFileContextMenu(CopilotLocalFileLinkTarget target)
        {
            var openFolderItem = new MenuItem
            {
                Header = ColorVision.UI.Properties.Resources.OpenFolder,
                Tag = target,
            };
            openFolderItem.Click += LocalFileOpenFolder_Click;
            return new ContextMenu
            {
                Items =
                {
                    openFolderItem,
                },
            };
        }

        private static void LocalFileOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is not MenuItem { Tag: CopilotLocalFileLinkTarget target } menuItem)
                return;

            if (!CopilotLocalFileLinkNavigator.TryOpenContainingFolder(target, out var errorMessage))
                menuItem.ToolTip = "无法打开文件夹：" + CopilotUserFacingErrorFormatter.Sanitize(errorMessage);
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

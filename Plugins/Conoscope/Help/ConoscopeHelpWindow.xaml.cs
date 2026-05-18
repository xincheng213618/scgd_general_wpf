using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using Conoscope.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Conoscope.Help
{
    public class MenuConoscopeHelp : ConoscopeMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 1;
        public override string Header => Properties.Resources.HelpCenterTitle;

        public override void Execute()
        {
            ConoscopeHelpWindow.ShowWindow(Application.Current.GetActiveWindow());
        }
    }

    public partial class ConoscopeHelpWindow : Window
    {
        private bool isInitializing;
        private readonly List<ConoscopeHelpEntry> allEntries;
        private List<ConoscopeHelpEntry> filteredEntries;
        private readonly string? initialEntryId;
        private readonly ConoscopeHelpCategory? initialCategory;

        public ConoscopeHelpWindow(string? entryId = null, ConoscopeHelpCategory? category = null)
        {
            isInitializing = true;
            InitializeComponent();
            this.ApplyCaption();
            DetailDocumentBox.Document = CreateEmptyDocument();
            allEntries = ConoscopeHelpContent.GetAllEntries().ToList();
            filteredEntries = new List<ConoscopeHelpEntry>(allEntries);
            initialEntryId = entryId;
            initialCategory = ResolveInitialCategory(entryId, category);
            Loaded += ConoscopeHelpWindow_Loaded;
            isInitializing = false;
        }

        public static void ShowWindow(Window? owner, string? entryId = null, ConoscopeHelpCategory? category = null)
        {
            Window? resolvedOwner = owner ?? Application.Current.GetActiveWindow();
            new ConoscopeHelpWindow(entryId, category)
            {
                Owner = resolvedOwner,
                WindowStartupLocation = resolvedOwner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
            }.Show();
        }

        private void ConoscopeHelpWindow_Loaded(object sender, RoutedEventArgs e)
        {
            tbTitle.Text = string.Format(Properties.Resources.HelpTitleWithVersion, GetVersionText());
            HelpList.ItemsSource = filteredEntries;

            isInitializing = true;
            ApplyInitialCategory();
            isInitializing = false;

            ApplyFilter();
            SelectInitialEntry();
        }

        private static string GetVersionText()
        {
            return typeof(ConoscopeHelpWindow).Assembly.GetName().Version?.ToString() ?? Properties.Resources.UnknownVersion;
        }

        private static ConoscopeHelpCategory? ResolveInitialCategory(string? entryId, ConoscopeHelpCategory? category)
        {
            if (category.HasValue)
            {
                return category;
            }

            if (string.IsNullOrWhiteSpace(entryId))
            {
                return null;
            }

            return ConoscopeHelpContent.GetAllEntries()
                .FirstOrDefault(item => string.Equals(item.Id, entryId, StringComparison.Ordinal))
                ?.Category;
        }

        private void ApplyInitialCategory()
        {
            switch (initialCategory)
            {
                case ConoscopeHelpCategory.QuickStart:
                    FilterQuickStart.IsChecked = true;
                    break;
                case ConoscopeHelpCategory.Workflow:
                    FilterWorkflow.IsChecked = true;
                    break;
                case ConoscopeHelpCategory.Principle:
                    FilterPrinciple.IsChecked = true;
                    break;
                case ConoscopeHelpCategory.Terminology:
                    FilterTerminology.IsChecked = true;
                    break;
                default:
                    FilterAll.IsChecked = true;
                    break;
            }
        }

        private void SelectInitialEntry()
        {
            if (!string.IsNullOrWhiteSpace(initialEntryId))
            {
                ConoscopeHelpEntry? matchedEntry = filteredEntries.FirstOrDefault(item => string.Equals(item.Id, initialEntryId, StringComparison.Ordinal));
                if (matchedEntry != null)
                {
                    HelpList.SelectedItem = matchedEntry;
                    return;
                }
            }

            if (filteredEntries.Count > 0)
            {
                HelpList.SelectedIndex = 0;
            }
            else
            {
                ShowPlaceholder();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing)
            {
                return;
            }

            ApplyFilter();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (isInitializing)
            {
                return;
            }

            ApplyFilter();
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            if (SearchBox == null)
            {
                return;
            }

            SearchBox.Text = string.Empty;
        }

        private void ApplyFilter()
        {
            if (isInitializing || SearchBox == null || HelpList == null || ItemCountText == null)
            {
                return;
            }

            string keyword = SearchBox.Text?.Trim() ?? string.Empty;
            ConoscopeHelpCategory? categoryFilter = ResolveSelectedCategory();

            filteredEntries = allEntries.Where(entry =>
            {
                if (categoryFilter.HasValue && entry.Category != categoryFilter.Value)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(keyword))
                {
                    return true;
                }

                return entry.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || entry.Summary.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || entry.Keywords.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || entry.SearchText.Contains(keyword, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            HelpList.ItemsSource = filteredEntries;
            UpdateItemCount();

            if (HelpList.SelectedItem is ConoscopeHelpEntry selected
                && filteredEntries.Any(item => string.Equals(item.Id, selected.Id, StringComparison.Ordinal)))
            {
                return;
            }

            if (filteredEntries.Count > 0)
            {
                HelpList.SelectedIndex = 0;
            }
            else
            {
                ShowPlaceholder();
            }
        }

        private ConoscopeHelpCategory? ResolveSelectedCategory()
        {
            if (FilterQuickStart?.IsChecked == true)
            {
                return ConoscopeHelpCategory.QuickStart;
            }

            if (FilterWorkflow?.IsChecked == true)
            {
                return ConoscopeHelpCategory.Workflow;
            }

            if (FilterPrinciple?.IsChecked == true)
            {
                return ConoscopeHelpCategory.Principle;
            }

            if (FilterTerminology?.IsChecked == true)
            {
                return ConoscopeHelpCategory.Terminology;
            }

            return null;
        }

        private void UpdateItemCount()
        {
            ItemCountText.Text = string.Format(Properties.Resources.ItemCountFormat, filteredEntries.Count, allEntries.Count);
        }

        private void HelpList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HelpList.SelectedItem is not ConoscopeHelpEntry entry)
            {
                ShowPlaceholder();
                return;
            }

            DetailCategory.Text = entry.CategoryDisplay;
            DetailTitle.Text = entry.Title;
            DetailSummary.Text = entry.Summary;
            PlaceholderText.Visibility = Visibility.Collapsed;
            DetailDocumentHost.Visibility = Visibility.Visible;
            DetailDocumentBox.Document = BuildDocument(entry);
        }

        private void ShowPlaceholder()
        {
            DetailCategory.Text = string.Empty;
            DetailTitle.Text = string.Empty;
            DetailSummary.Text = string.Empty;
            PlaceholderText.Visibility = Visibility.Visible;
            DetailDocumentHost.Visibility = Visibility.Collapsed;
            DetailDocumentBox.Document = CreateEmptyDocument();
        }

        private FlowDocument BuildDocument(ConoscopeHelpEntry entry)
        {
            Brush textBrush = ResolveBrush("GlobalTextBrush", Color.FromRgb(31, 35, 40));
            Brush mutedBrush = ResolveBrush("GlobalTextBrush", Color.FromRgb(75, 85, 99));
            Brush accentBrush = ResolveBrush("PrimaryBrush", Color.FromRgb(15, 60, 99));
            Brush codeBackgroundBrush = ResolveBrush("GlobalBorderBrush", Color.FromRgb(240, 242, 244));
            Brush codeBorderBrush = ResolveBrush("GlobalBorderBrush1", Color.FromRgb(208, 215, 222));

            FlowDocument document = CreateEmptyDocument();

            foreach (ConoscopeHelpBlock block in entry.DetailBlocks)
            {
                switch (block.Kind)
                {
                    case ConoscopeHelpBlockKind.Heading:
                        document.Blocks.Add(CreateHeadingParagraph(block.Text ?? string.Empty, block.Level, accentBrush));
                        break;
                    case ConoscopeHelpBlockKind.Paragraph:
                        document.Blocks.Add(CreateBodyParagraph(textBrush, block.Text ?? string.Empty));
                        break;
                    case ConoscopeHelpBlockKind.BulletedList:
                        AddListBlock(document, block.Items, false, block.IndentLevel, textBrush, accentBrush);
                        break;
                    case ConoscopeHelpBlockKind.NumberedList:
                        AddListBlock(document, block.Items, true, block.IndentLevel, textBrush, accentBrush);
                        break;
                    case ConoscopeHelpBlockKind.CodeBlock:
                        AddCodeBlock(document, block.Text ?? string.Empty, codeBackgroundBrush, codeBorderBrush, textBrush);
                        break;
                }
            }

            if (document.Blocks.Count == 0)
            {
                document.Blocks.Add(CreateBodyParagraph(mutedBrush, Properties.Resources.NoContentToDisplay));
            }

            return document;
        }

        private static FlowDocument CreateEmptyDocument()
        {
            return new FlowDocument
            {
                PagePadding = new Thickness(0),
                Background = Brushes.Transparent,
                TextAlignment = TextAlignment.Left,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                LineHeight = 24
            };
        }

        private static Paragraph CreateHeadingParagraph(string text, int level, Brush foreground)
        {
            return new Paragraph(new Run(text))
            {
                Margin = level switch
                {
                    3 => new Thickness(0, 8, 0, 6),
                    _ => new Thickness(0, 10, 0, 8)
                },
                FontSize = level switch
                {
                    3 => 14,
                    _ => 16
                },
                FontWeight = FontWeights.SemiBold,
                Foreground = foreground
            };
        }

        private static Paragraph CreateBodyParagraph(Brush foreground, string text)
        {
            return new Paragraph(new Run(text))
            {
                Margin = new Thickness(0, 0, 0, 12),
                Foreground = foreground
            };
        }

        private static void AddListBlock(
            FlowDocument document,
            IReadOnlyList<string>? items,
            bool ordered,
            int indentLevel,
            Brush textBrush,
            Brush accentBrush)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            for (int index = 0; index < items.Count; index++)
            {
                string marker = ordered ? $"{index + 1}." : "•";
                Paragraph paragraph = new()
                {
                    Margin = new Thickness(indentLevel * 20, 0, 0, 6),
                    Foreground = textBrush
                };
                paragraph.Inlines.Add(new Run(marker + " ")
                {
                    Foreground = accentBrush,
                    FontWeight = FontWeights.SemiBold
                });
                paragraph.Inlines.Add(new Run(items[index]));
                document.Blocks.Add(paragraph);
            }
        }

        private static void AddCodeBlock(FlowDocument document, string code, Brush backgroundBrush, Brush borderBrush, Brush textBrush)
        {
            string trimmedCode = code.Trim();
            if (string.IsNullOrWhiteSpace(trimmedCode))
            {
                return;
            }

            Border border = new()
            {
                Background = backgroundBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 2, 0, 12),
                Child = new TextBlock
                {
                    Text = trimmedCode,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = textBrush,
                    TextWrapping = TextWrapping.Wrap
                }
            };

            document.Blocks.Add(new BlockUIContainer(border));
        }

        private Brush ResolveBrush(string resourceKey, Color fallbackColor)
        {
            return TryFindResource(resourceKey) as Brush ?? new SolidColorBrush(fallbackColor);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

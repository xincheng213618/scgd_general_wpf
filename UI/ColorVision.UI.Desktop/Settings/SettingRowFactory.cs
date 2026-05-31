using ColorVision.Common.MVVM;
using ColorVision.UI.Properties;
using log4net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.UI.Desktop.Settings
{
    internal sealed class SettingRowFactory
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SettingRowFactory));

        public static Border CreateSectionCard(string sectionName, IReadOnlyList<SettingEntry> entries)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 14),
                Padding = new Thickness(0)
            };
            card.SetResourceReference(Border.BackgroundProperty, "GlobalBackground");
            card.SetResourceReference(Border.BorderBrushProperty, "GlobalBorderBrush");

            var stackPanel = new StackPanel();
            var header = new DockPanel
            {
                LastChildFill = true,
                Margin = new Thickness(16, 12, 16, 6)
            };

            var title = new TextBlock
            {
                Text = sectionName,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Opacity = 0.9,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            header.Children.Add(title);
            stackPanel.Children.Add(header);

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                bool isLast = i == entries.Count - 1;
                FrameworkElement row = entry.Metadata.Type == ConfigSettingType.Property
                    ? CreatePropertySettingRow(entry, isLast)
                    : CreateCustomSettingRow(entry, isLast);

                stackPanel.Children.Add(row);
            }

            card.Child = stackPanel;
            return card;
        }

        public static Border CreateEmptyState(string text)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18),
                Child = CreateInlineMessage(text)
            };
            border.SetResourceReference(Border.BackgroundProperty, "GlobalBackground");
            border.SetResourceReference(Border.BorderBrushProperty, "GlobalBorderBrush");
            return border;
        }

        private static Border CreatePropertySettingRow(SettingEntry entry, bool isLast)
        {
            var row = new Grid
            {
                MinHeight = 58
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 220 });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textPanel = CreateSettingTextPanel(entry);
            textPanel.Margin = new Thickness(0, 0, 20, 0);
            Grid.SetColumn(textPanel, 0);
            row.Children.Add(textPanel);

            var editorHost = new Border
            {
                Width = 340,
                MinWidth = 260,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Child = CreatePropertyEditor(entry)
            };
            Grid.SetColumn(editorHost, 1);
            row.Children.Add(editorHost);

            return CreateRowShell(row, isLast);
        }

        private static Border CreateCustomSettingRow(SettingEntry entry, bool isLast)
        {
            var stackPanel = new StackPanel();
            stackPanel.Children.Add(CreateSettingTextPanel(entry));

            var contentHost = new Border
            {
                Margin = new Thickness(0, 10, 0, 0),
                Child = GetOrCreateCustomContent(entry)
            };
            stackPanel.Children.Add(contentHost);

            return CreateRowShell(stackPanel, isLast);
        }

        private static StackPanel CreateSettingTextPanel(SettingEntry entry)
        {
            var stackPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            var title = new TextBlock
            {
                Text = entry.Title,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                TextWrapping = TextWrapping.Wrap
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            stackPanel.Children.Add(title);

            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                var description = new TextBlock
                {
                    Text = entry.Description,
                    Margin = new Thickness(0, 4, 0, 0),
                    FontSize = 12,
                    Opacity = 0.68,
                    TextWrapping = TextWrapping.Wrap
                };
                description.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                stackPanel.Children.Add(description);
            }

            return stackPanel;
        }

        private static Border CreateRowShell(UIElement child, bool isLast)
        {
            var border = new Border
            {
                Padding = new Thickness(16, 12, 16, 12),
                BorderThickness = new Thickness(0, 0, 0, isLast ? 0 : 1),
                Child = child
            };
            border.SetResourceReference(Border.BorderBrushProperty, "GlobalBorderBrush");
            return border;
        }

        private static FrameworkElement CreatePropertyEditor(SettingEntry entry)
        {
            try
            {
                if (entry.PropertyInfo == null || entry.Metadata.Source == null)
                {
                    return CreateInlineMessage(SettingResources.EditorUnavailable);
                }

                DockPanel dockPanel = PropertyEditorHelper.GenProperties(entry.PropertyInfo, entry.Metadata.Source);
                dockPanel.Margin = new Thickness(0);
                dockPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                dockPanel.VerticalAlignment = VerticalAlignment.Center;

                RemoveInlinePropertyLabel(dockPanel, entry);
                dockPanel.LastChildFill = ShouldFillLastChild(dockPanel);
                ApplyEditorSizing(dockPanel);
                return dockPanel;
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to create setting editor: {entry.Title}: {ex.Message}");
                return CreateInlineMessage(SettingResources.EditorLoadFailed);
            }
        }

        private static FrameworkElement GetOrCreateCustomContent(SettingEntry entry)
        {
            if (entry.CustomContent != null)
            {
                DetachFromParent(entry.CustomContent);
                return entry.CustomContent;
            }

            try
            {
                if (entry.Metadata.ViewType != null)
                {
                    object? instance = Activator.CreateInstance(entry.Metadata.ViewType);
                    if (instance is FrameworkElement element)
                    {
                        entry.CustomContent = element;
                        return element;
                    }

                    Log.Warn($"Lazy load failed for {entry.Metadata.ViewType.Name}: type is not a FrameworkElement");
                    entry.CustomContent = CreateInlineMessage(SettingResources.PageTypeInvalid);
                    return entry.CustomContent;
                }

                if (entry.Metadata.Type == ConfigSettingType.Class && entry.Metadata.Source is ViewModelBase viewModel)
                {
                    var panel = PropertyEditorHelper.GenPropertyEditorControl(viewModel);
                    panel.Margin = new Thickness(0);
                    entry.CustomContent = panel;
                    return panel;
                }

                entry.CustomContent = CreateInlineMessage(SettingResources.PageUnavailable);
                return entry.CustomContent;
            }
            catch (Exception ex)
            {
                Log.Warn($"Lazy load failed for {entry.Title}: {ex.Message}");
                entry.CustomContent = CreateInlineMessage(SettingResources.PageLoadFailed);
                return entry.CustomContent;
            }
        }

        private static TextBlock CreateInlineMessage(string text)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Opacity = 0.72,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            return textBlock;
        }

        private static void RemoveInlinePropertyLabel(DockPanel dockPanel, SettingEntry entry)
        {
            for (int index = dockPanel.Children.Count - 1; index >= 0; index--)
            {
                if (dockPanel.Children[index] is TextBlock textBlock && IsInlinePropertyLabel(textBlock, entry))
                {
                    dockPanel.Children.RemoveAt(index);
                }
            }
        }

        private static bool IsInlinePropertyLabel(TextBlock textBlock, SettingEntry entry)
        {
            string text = textBlock.Text ?? string.Empty;
            return textBlock.MinWidth >= 80
                || string.Equals(text, entry.Title, StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, entry.Metadata.BindingName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, entry.PropertyInfo?.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldFillLastChild(DockPanel dockPanel)
        {
            if (dockPanel.Children.Count != 1) return true;
            return dockPanel.Children[0] is TextBox or Panel;
        }

        private static void ApplyEditorSizing(DependencyObject element)
        {
            switch (element)
            {
                case TextBox textBox:
                    textBox.MinWidth = Math.Max(textBox.MinWidth, 220);
                    textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                    break;
                case ComboBox comboBox:
                    comboBox.MinWidth = Math.Max(comboBox.MinWidth, 220);
                    comboBox.HorizontalAlignment = HorizontalAlignment.Right;
                    break;
                case ToggleButton toggleButton:
                    toggleButton.HorizontalAlignment = HorizontalAlignment.Right;
                    toggleButton.VerticalAlignment = VerticalAlignment.Center;
                    break;
                case ButtonBase button:
                    button.VerticalAlignment = VerticalAlignment.Center;
                    break;
            }

            foreach (object child in LogicalTreeHelper.GetChildren(element))
            {
                if (child is DependencyObject dependencyObject)
                {
                    ApplyEditorSizing(dependencyObject);
                }
            }
        }

        private static void DetachFromParent(FrameworkElement element)
        {
            switch (element.Parent)
            {
                case Panel panel:
                    panel.Children.Remove(element);
                    break;
                case ContentControl contentControl when ReferenceEquals(contentControl.Content, element):
                    contentControl.Content = null;
                    break;
                case Decorator decorator when ReferenceEquals(decorator.Child, element):
                    decorator.Child = null;
                    break;
            }
        }
    }
}
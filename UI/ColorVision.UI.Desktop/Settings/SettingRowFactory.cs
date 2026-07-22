using ColorVision.Common.MVVM;
using ColorVision.UI.Properties;
using log4net;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
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
                Margin = new Thickness(0, 0, 0, 16),
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            card.SetResourceReference(Border.BackgroundProperty, "GlobalBackground");
            card.SetResourceReference(Border.BorderBrushProperty, "ButtonBorderBrush");

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                bool isLast = i == entries.Count - 1;
                FrameworkElement row = CreatePropertySettingRow(entry, isLast, i);

                stackPanel.Children.Add(row);
            }

            card.Child = stackPanel;
            return card;
        }

        public static FrameworkElement CreateCustomPage(SettingEntry entry, bool showTitle)
        {
            if (entry.Metadata.Type == ConfigSettingType.Class && entry.Metadata.Source is ViewModelBase viewModel)
            {
                return CreateClassSettingsPage(entry, viewModel, showTitle);
            }

            var content = GetOrCreateCustomContent(entry);
            PrepareCustomContent(content);
            var host = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Child = content
            };

            if (!showTitle)
            {
                return host;
            }

            return CreateTitledCustomPage(entry, host);
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

        private static Border CreatePropertySettingRow(SettingEntry entry, bool isLast, int rowIndex)
        {
            return entry.Metadata.Layout == ConfigSettingLayout.Wide
                ? CreateWidePropertySettingRow(entry, isLast, rowIndex)
                : CreateInlinePropertySettingRow(entry, isLast, rowIndex);
        }

        private static Border CreateInlinePropertySettingRow(SettingEntry entry, bool isLast, int rowIndex)
        {
            var row = new Grid
            {
                MinHeight = 50,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 220 });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textPanel = CreateSettingTextPanel(entry);
            textPanel.Margin = new Thickness(0, 0, 20, 0);
            Grid.SetColumn(textPanel, 0);
            row.Children.Add(textPanel);

            var editorHost = new Border
            {
                Width = 288,
                MinWidth = 228,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Child = CreatePropertyEditor(entry)
            };
            Grid.SetColumn(editorHost, 1);
            row.Children.Add(editorHost);

            return CreateRowShell(row, isLast, rowIndex);
        }

        private static Border CreateWidePropertySettingRow(SettingEntry entry, bool isLast, int rowIndex)
        {
            var row = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var textPanel = CreateSettingTextPanel(entry);
            textPanel.Margin = new Thickness(0, 0, 0, 12);
            row.Children.Add(textPanel);

            var editor = CreatePropertyEditor(entry, useWideLayout: true);
            editor.HorizontalAlignment = HorizontalAlignment.Stretch;
            row.Children.Add(editor);

            return CreateRowShell(row, isLast, rowIndex);
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
                FontSize = 14,
                FontWeight = FontWeights.Normal,
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
                    FontSize = 12.5,
                    Opacity = 0.68,
                    TextWrapping = TextWrapping.Wrap
                };
                description.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                stackPanel.Children.Add(description);
            }

            return stackPanel;
        }

        private static Border CreateRowShell(UIElement child, bool isLast, int rowIndex)
        {
            var border = new Border
            {
                Padding = new Thickness(24, 10, 24, 10),
                BorderThickness = new Thickness(0, 0, 0, isLast ? 0 : 1),
                Child = child,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            border.SetResourceReference(Border.BorderBrushProperty, "ButtonBorderBrush");
            SetRowBackground(border, rowIndex);
            return border;
        }

        private static void SetRowBackground(Border border, int rowIndex)
        {
            border.Background = null;
        }

        private static FrameworkElement CreateClassSettingsPage(SettingEntry entry, ViewModelBase viewModel, bool showTitle)
        {
            var propertyEntries = CreateClassPropertyEntries(entry, viewModel);
            FrameworkElement content = propertyEntries.Count == 0
                ? CreateEmptyState(SettingResources.EditorUnavailable)
                : CreateRowsCard(propertyEntries);

            return showTitle ? CreateTitledCustomPage(entry, content) : content;
        }

        private static List<SettingEntry> CreateClassPropertyEntries(SettingEntry pageEntry, object source)
        {
            var entries = new List<SettingEntry>();
            var properties = GetEditableProperties(source);

            for (int index = 0; index < properties.Count; index++)
            {
                var property = properties[index];
                var metadata = new ConfigSettingMetadata
                {
                    Group = pageEntry.Group,
                    Name = string.Empty,
                    Description = string.Empty,
                    Section = pageEntry.SectionKey,
                    Order = pageEntry.Metadata.Order + index,
                    Type = ConfigSettingType.Property,
                    BindingName = property.Name,
                    Source = source
                };

                entries.Add(SettingMetadataResolver.CreateEntry(metadata, property));
            }

            return entries;
        }

        private static List<PropertyInfo> GetEditableProperties(object source)
        {
            var type = source.GetType();
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanRead && property.CanWrite)
                .Where(property => property.GetIndexParameters().Length == 0)
                .Where(property => property.GetCustomAttribute<BrowsableAttribute>()?.Browsable ?? true)
                .Where(property => CanCreateEditor(property))
                .OrderBy(property => GetInheritanceDepth(property.DeclaringType ?? type))
                .ThenBy(property => property.GetCustomAttribute<DisplayAttribute>()?.GetOrder() ?? int.MaxValue)
                .ThenBy(property => property.MetadataToken)
                .ToList();
        }

        private static bool CanCreateEditor(PropertyInfo property)
        {
            var editorAttr = property.GetCustomAttribute<PropertyEditorTypeAttribute>();
            if (editorAttr?.EditorType != null) return true;
            if (PropertyEditorHelper.GetEditorTypeForPropertyType(property.PropertyType) != null) return true;

            return CanGenerateNestedEditor(property.PropertyType);
        }

        private static bool CanGenerateNestedEditor(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (!type.IsClass || type == typeof(string)) return false;
            if (typeof(Delegate).IsAssignableFrom(type) || typeof(Type).IsAssignableFrom(type) || typeof(System.Resources.ResourceManager).IsAssignableFrom(type)) return false;
            if (typeof(DependencyObject).IsAssignableFrom(type) || typeof(System.Collections.IEnumerable).IsAssignableFrom(type)) return false;
            if (IsFrameworkType(type) && !typeof(INotifyPropertyChanged).IsAssignableFrom(type)) return false;

            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(property => property.CanRead
                    && property.CanWrite
                    && property.GetIndexParameters().Length == 0
                    && (property.GetCustomAttribute<BrowsableAttribute>()?.Browsable ?? true));
        }

        private static bool IsFrameworkType(Type type)
        {
            string namespaceName = type.Namespace ?? string.Empty;
            return namespaceName == "System"
                || namespaceName.StartsWith("System.", StringComparison.Ordinal)
                || namespaceName.StartsWith("Microsoft.", StringComparison.Ordinal)
                || namespaceName.StartsWith("MS.", StringComparison.Ordinal);
        }

        private static int GetInheritanceDepth(Type type)
        {
            int depth = 0;
            Type? current = type;
            while (current != null)
            {
                current = current.BaseType;
                depth++;
            }

            return depth;
        }

        private static Border CreateRowsCard(List<SettingEntry> entries)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            card.SetResourceReference(Border.BackgroundProperty, "GlobalBackground");
            card.SetResourceReference(Border.BorderBrushProperty, "ButtonBorderBrush");

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            for (int index = 0; index < entries.Count; index++)
            {
                stackPanel.Children.Add(CreatePropertySettingRow(entries[index], index == entries.Count - 1, index));
            }

            card.Child = stackPanel;
            return card;
        }

        private static StackPanel CreateTitledCustomPage(SettingEntry entry, FrameworkElement content)
        {
            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var textPanel = CreateSettingTextPanel(entry);
            textPanel.Margin = new Thickness(0, 0, 0, 8);
            stackPanel.Children.Add(textPanel);

            content.HorizontalAlignment = HorizontalAlignment.Stretch;
            stackPanel.Children.Add(content);
            return stackPanel;
        }

        private static FrameworkElement CreatePropertyEditor(SettingEntry entry, bool useWideLayout = false)
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
                dockPanel.LastChildFill = useWideLayout || ShouldFillLastChild(dockPanel);
                if (useWideLayout)
                {
                    foreach (UIElement child in dockPanel.Children)
                    {
                        if (child is FrameworkElement element)
                            element.HorizontalAlignment = HorizontalAlignment.Stretch;
                    }
                }
                else
                {
                    ApplyEditorSizing(dockPanel);
                }
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
                    textBox.MinWidth = Math.Max(textBox.MinWidth, 200);
                    textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                    break;
                case ComboBox comboBox:
                    comboBox.SetResourceReference(FrameworkElement.StyleProperty, "SettingsEditorComboBoxStyle");
                    comboBox.MinHeight = Math.Max(comboBox.MinHeight, 32);
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

        private static void PrepareCustomContent(FrameworkElement content)
        {
            content.VerticalAlignment = VerticalAlignment.Top;
            content.HorizontalAlignment = double.IsNaN(content.Width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
            ApplyCustomContentSizing(content);
        }

        private static void ApplyCustomContentSizing(DependencyObject element)
        {
            switch (element)
            {
                case DockPanel dockPanel:
                    dockPanel.Width = double.NaN;
                    dockPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                    dockPanel.LastChildFill = true;
                    break;
                case Panel panel:
                    panel.HorizontalAlignment = HorizontalAlignment.Stretch;
                    break;
                case TextBox textBox:
                    textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                    break;
                case ComboBox comboBox:
                    comboBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                    break;
            }

            foreach (object child in LogicalTreeHelper.GetChildren(element))
            {
                if (child is DependencyObject dependencyObject)
                {
                    ApplyCustomContentSizing(dependencyObject);
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

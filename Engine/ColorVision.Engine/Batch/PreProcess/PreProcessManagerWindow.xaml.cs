#pragma warning disable CS8603,CS8622
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Properties = ColorVision.Engine.Properties;

namespace ColorVision.Engine.Batch
{
    public class PreTypeNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PreProcessAction action)
            {
                return parameter?.ToString() switch
                {
                    "Description" => action.Metadata.Description,
                    "Category" => action.Metadata.Category,
                    "Enabled" => action.EnabledText,
                    "IsEnabled" => action.IsEnabled,
                    "Templates" => action.TemplateSummary,
                    "TypeName" => action.Metadata.TypeName,
                    "MetadataName" => action.Metadata.DisplayName,
                    _ => action.DisplayName,
                };
            }
            if (value is ListBoxItem item && parameter?.ToString() == "Index")
            {
                var listBox = FindParent<ListBox>(item);
                if (listBox != null)
                {
                    int index = listBox.Items.IndexOf(item.Content);
                    return (index + 1).ToString();
                }
            }
            return value?.GetType().Name ?? string.Empty;
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class PreProcessTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PreProcessAction action)
            {
                string text = action.DisplayName;
                if (!string.Equals(text, action.Metadata.DisplayName, StringComparison.Ordinal))
                {
                    text += $"\n{string.Format(Properties.Resources.Flow_PreProcess_TypeFormat, action.Metadata.DisplayName)}";
                }
                if (!string.IsNullOrWhiteSpace(action.Metadata.Description))
                {
                    text += $"\n\n{action.Metadata.Description}";
                }
                text += $"\n\n{string.Format(Properties.Resources.Flow_PreProcess_AppliedTemplatesFormat, action.TemplateSummary)}";
                return text;
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class PreBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public partial class PreProcessManagerWindow : Window
    {
        public PreProcessManagerWindow()
        {
            InitializeComponent();
            Loaded += Window_Loaded;
            Closing += Window_Closing;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshPropertyPanel();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            PreProcessManager.GetInstance().SavePersisted();
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshPropertyPanel();
        }

        private void RefreshPropertyPanel()
        {
            PropertyPanel.Children.Clear();

            var manager = DataContext as PreProcessManager;
            var selectedAction = manager?.SelectedProcess;

            if (selectedAction == null)
            {
                var placeholder = new TextBlock
                {
                    Text = Properties.Resources.Flow_PreProcess_SelectAction,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 32, 0, 0),
                    Opacity = 0.58
                };
                placeholder.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                PropertyPanel.Children.Add(placeholder);
                return;
            }

            AddActionSettingsSection(selectedAction);

            var config = selectedAction.Process.GetConfig();
            if (config != null)
            {
                AddConfigSection(config);
            }
        }

        private void AddActionSettingsSection(PreProcessAction action)
        {
            var border = CreateSectionBorder();
            var stack = new StackPanel();
            border.Child = stack;

            AddSectionHeader(stack, Properties.Resources.Flow_PreProcess_ActionSettings);
            AddBoundTextBox(stack, Properties.Resources.Flow_PreProcess_ActionName, action, nameof(PreProcessAction.ActionName), null);
            AddBoundCheckBox(stack, Properties.Resources.IsEnable, action, nameof(PreProcessAction.IsEnabled));
            AddBoundTextBox(stack, Properties.Resources.Flow_PreProcess_AppliedTemplates, action, nameof(PreProcessAction.TemplateNames), Properties.Resources.Flow_PreProcess_AllTemplatesHint);

            PropertyPanel.Children.Add(border);
        }

        private void AddConfigSection(object config)
        {
            var border = CreateSectionBorder();
            var stack = new StackPanel();
            border.Child = stack;

            AddSectionHeader(stack, Properties.Resources.Parameter);

            var configPanel = PropertyEditorHelper.GenPropertyEditorControl(config);
            stack.Children.Add(configPanel);
            PropertyPanel.Children.Add(border);
        }

        private static Border CreateSectionBorder()
        {
            var border = new Border
            {
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            border.SetResourceReference(Border.CornerRadiusProperty, "ControlCornerRadius");
            return border;
        }

        private static void AddSectionHeader(StackPanel parent, string text)
        {
            var header = new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                FontSize = 15,
                Margin = new Thickness(0, 0, 0, 12)
            };
            header.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            parent.Children.Add(header);
        }

        private static void AddBoundTextBox(StackPanel parent, string label, object source, string path, string? tooltip)
        {
            var textBox = new TextBox
            {
                MinHeight = 28,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = tooltip
            };
            textBox.SetResourceReference(FrameworkElement.StyleProperty, "PreProcessTextBoxStyle");
            textBox.SetBinding(TextBox.TextProperty, new Binding(path)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            AddSettingRow(parent, label, textBox);
        }

        private static void AddBoundCheckBox(StackPanel parent, string label, object source, string path)
        {
            var checkBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center
            };
            checkBox.SetResourceReference(Control.ForegroundProperty, "GlobalTextBrush");
            checkBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding(path)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            AddSettingRow(parent, label, checkBox);
        }

        private static void AddSettingRow(StackPanel parent, string label, FrameworkElement editor)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelText = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.72
            };
            labelText.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            grid.Children.Add(labelText);

            Grid.SetColumn(editor, 1);
            grid.Children.Add(editor);
            parent.Children.Add(grid);
        }

        private void AddProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

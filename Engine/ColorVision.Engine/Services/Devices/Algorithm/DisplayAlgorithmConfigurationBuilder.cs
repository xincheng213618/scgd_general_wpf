using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ColorVision.UI;
using ColorVision.Common.Utilities;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public sealed class DisplayAlgorithmConfigurationBuilder
    {
        private const double LabelWidth = 110;

        public FrameworkElement Build(DisplayAlgorithmConfigBase configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            StackPanel panel = new();
            BuildObject(configuration, panel, 0);
            return panel;
        }

        private void BuildObject(
            object source,
            Panel panel,
            int depth)
        {
            IEnumerable<PropertyInfo> properties = source.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property =>
                    property.CanRead &&
                    (property.CanWrite || property.GetValue(source) is DisplayAlgorithmTemplateSelection) &&
                    property.GetIndexParameters().Length == 0 &&
                    (property.GetCustomAttribute<BrowsableAttribute>()?.Browsable ?? true))
                .OrderBy(property => property.GetCustomAttribute<DisplayAttribute>()?.GetOrder() ?? int.MaxValue)
                .ThenBy(property => property.MetadataToken);

            foreach (PropertyInfo property in properties)
            {
                FrameworkElement? editor = CreateEditor(source, property, depth);
                if (editor == null)
                {
                    continue;
                }

                ApplyVisibility(editor, source, property);
                panel.Children.Add(editor);
            }
        }

        private FrameworkElement? CreateEditor(
            object source,
            PropertyInfo property,
            int depth)
        {
            object? value = property.GetValue(source);

            if (value is DisplayAlgorithmTemplateSelection templateSelection)
            {
                bool isPrimaryTemplate = source is SingleTemplateDisplayAlgorithmConfig && property.Name == nameof(SingleTemplateDisplayAlgorithmConfig.Template);
                return CreateTemplateEditor(templateSelection, showLabel: !isPrimaryTemplate);
            }

            if (property.GetCustomAttribute<DisplayAlgorithmFileAttribute>() is DisplayAlgorithmFileAttribute fileAttribute)
            {
                return CreateFileEditor(source, property, fileAttribute);
            }

            Type propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (propertyType == typeof(bool))
            {
                return CreateBooleanEditor(source, property);
            }

            if (propertyType.IsEnum)
            {
                return CreateEnumEditor(source, property, propertyType);
            }

            if (propertyType == typeof(string) ||
                propertyType == typeof(byte) ||
                propertyType == typeof(short) ||
                propertyType == typeof(int) ||
                propertyType == typeof(long) ||
                propertyType == typeof(float) ||
                propertyType == typeof(double) ||
                propertyType == typeof(decimal))
            {
                return CreateTextEditor(source, property);
            }

            if (typeof(ICommand).IsAssignableFrom(propertyType) &&
                value is ICommand command &&
                property.GetCustomAttribute<CommandDisplayAttribute>() is CommandDisplayAttribute commandDisplay)
            {
                return new Button
                {
                    Content = PropertyEditorHelper.GetLocalizedString(PropertyEditorHelper.GetResourceManager(source), commandDisplay.DisplayName),
                    Command = command,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(LabelWidth, 2, 0, 2)
                };
            }

            if (value != null && propertyType.IsClass && propertyType != typeof(Type) && depth < 3)
            {
                StackPanel nestedPanel = new();
                BuildObject(value, nestedPanel, depth + 1);
                if (nestedPanel.Children.Count == 0)
                {
                    return null;
                }

                return new Expander
                {
                    Header = GetDisplayName(source, property),
                    IsExpanded = true,
                    Content = nestedPanel,
                    Margin = new Thickness(depth * 8, 2, 0, 2)
                };
            }

            return null;
        }

        private static FrameworkElement CreateTemplateEditor(
            DisplayAlgorithmTemplateSelection selection,
            bool showLabel)
        {
            Grid row = CreateSelectionRow(showLabel);
            if (showLabel)
            {
                row.Children.Add(CreateLabel(selection.DisplayName));
            }

            ComboBox comboBox = new()
            {
                MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = selection.ItemsSource,
                DisplayMemberPath = "Key",
                SelectedValuePath = "Value"
            };
            System.Windows.Automation.AutomationProperties.SetName(comboBox, selection.DisplayName);
            comboBox.SetResourceReference(FrameworkElement.StyleProperty, "ComboBox.Small");
            comboBox.SetBinding(
                System.Windows.Controls.Primitives.Selector.SelectedIndexProperty,
                CreateBinding(selection, nameof(selection.SelectedIndex)));

            Button editButton = new()
            {
                Width = 24,
                Height = 24,
                Padding = new Thickness(2),
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Command = selection.EditCommand,
                ToolTip = selection.DisplayName,
                Content = new TextBlock
                {
                    Text = "\uE713",
                    FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
            System.Windows.Automation.AutomationProperties.SetName(editButton, selection.DisplayName);

            int editorColumn = showLabel ? 1 : 0;
            Grid.SetColumn(comboBox, editorColumn);
            Grid.SetColumn(editButton, editorColumn + 1);
            row.Children.Add(comboBox);
            row.Children.Add(editButton);

            return row;
        }

        private static FrameworkElement CreateFileEditor(
            object source,
            PropertyInfo property,
            DisplayAlgorithmFileAttribute attribute)
        {
            bool showLabel = property.Name != nameof(DisplayAlgorithmConfigBase.ImageFilePath);
            Grid row = CreateSelectionRow(showLabel);
            if (showLabel)
            {
                row.Children.Add(CreateLabel(GetDisplayName(source, property)));
            }
            TextBox textBox = CreateTextBox(source, property, usePlaceholderStyle: true);
            HandyControl.Controls.InfoElement.SetPlaceholder(textBox, ColorVision.Themes.Properties.Resources.Upload_SelectFile);
            System.Windows.Automation.AutomationProperties.SetName(textBox,
                property.Name == nameof(DisplayAlgorithmConfigBase.ImageFilePath) ? Properties.Resources.Image : GetDisplayName(source, property));
            Button browseButton = new()
            {
                Content = "...",
                Width = 30,
                Margin = new Thickness(5, 0, 0, 0)
            };
            browseButton.Click += (_, _) =>
            {
                Microsoft.Win32.OpenFileDialog dialog = new()
                {
                    Filter = string.IsNullOrWhiteSpace(attribute.Filter)
                        ? ColorVision.Engine.Services.ServicesHelper.ImageFileDialogFilter
                        : attribute.Filter,
                    RestoreDirectory = true
                };

                string? currentPath = property.GetValue(source) as string;
                if (!string.IsNullOrWhiteSpace(currentPath))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(currentPath);
                    dialog.FileName = Path.GetFileName(currentPath);
                }

                if (dialog.ShowDialog() == true)
                {
                    property.SetValue(source, dialog.FileName);
                    textBox.SetCurrentValue(TextBox.TextProperty, dialog.FileName);
                }
            };

            int editorColumn = showLabel ? 1 : 0;
            Grid.SetColumn(browseButton, editorColumn + 1);
            Grid.SetColumn(textBox, editorColumn);
            row.Children.Add(browseButton);
            row.Children.Add(textBox);
            return row;
        }

        private static Grid CreateSelectionRow(bool showLabel)
        {
            Grid row = new() { Margin = new Thickness(0, 2, 0, 2) };
            if (showLabel)
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelWidth) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // Preserve the input width when moving unlabeled fields to the left.
            if (!showLabel)
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelWidth) });
            return row;
        }

        private static FrameworkElement CreateBooleanEditor(object source, PropertyInfo property)
        {
            DockPanel row = CreateRow();
            TextBlock label = CreateLabel(GetDisplayName(source, property));
            CheckBox checkBox = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            checkBox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, CreateBinding(source, property.Name));
            DockPanel.SetDock(label, Dock.Left);
            row.Children.Add(label);
            row.Children.Add(checkBox);
            return row;
        }

        private static FrameworkElement CreateEnumEditor(object source, PropertyInfo property, Type enumType)
        {
            DockPanel row = CreateRow();
            TextBlock label = CreateLabel(GetDisplayName(source, property));
            var resources = PropertyEditorHelper.GetResourceManager(source);
            var enumResources = PropertyEditorHelper.GetResourceManager(enumType);
            ComboBox comboBox = new()
            {
                MinWidth = 0,
                ItemsSource = Enum.GetValues(enumType).Cast<Enum>().Select(value =>
                    new KeyValuePair<Enum, string>(value,
                        resources?.GetString(value.ToString(), CultureInfo.CurrentUICulture)
                        ?? enumResources?.GetString(value.ToString(), CultureInfo.CurrentUICulture)
                        ?? PropertyEditorHelper.GetLocalizedString(enumResources, value.ToDescription()))).ToList(),
                DisplayMemberPath = "Value",
                SelectedValuePath = "Key"
            };
            comboBox.SetResourceReference(FrameworkElement.StyleProperty, "ComboBox.Small");
            comboBox.SetBinding(
                System.Windows.Controls.Primitives.Selector.SelectedValueProperty,
                CreateBinding(source, property.Name));
            DockPanel.SetDock(label, Dock.Left);
            row.Children.Add(label);
            row.Children.Add(comboBox);
            return row;
        }

        private static FrameworkElement CreateTextEditor(object source, PropertyInfo property)
        {
            DockPanel row = CreateRow();
            TextBlock label = CreateLabel(GetDisplayName(source, property));
            TextBox textBox = CreateTextBox(source, property);
            DockPanel.SetDock(label, Dock.Left);
            row.Children.Add(label);
            row.Children.Add(textBox);
            return row;
        }

        private static TextBox CreateTextBox(object source, PropertyInfo property, bool usePlaceholderStyle = false)
        {
            TextBox textBox = usePlaceholderStyle
                ? new HandyControl.Controls.TextBox()
                : new TextBox();
            textBox.MinWidth = 0;
            textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            textBox.SetResourceReference(
                FrameworkElement.StyleProperty,
                usePlaceholderStyle ? "TextBoxPlus.Small" : "TextBox.Small");
            textBox.SetBinding(TextBox.TextProperty, CreateBinding(source, property.Name));
            return textBox;
        }

        private static DockPanel CreateRow()
        {
            return new DockPanel
            {
                LastChildFill = true,
                Margin = new Thickness(0, 2, 0, 2)
            };
        }

        private static TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Width = LabelWidth,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = text
            };
        }

        private static Binding CreateBinding(object source, string propertyName)
        {
            return new Binding(propertyName)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                ValidatesOnExceptions = true,
                NotifyOnValidationError = true
            };
        }

        private static string GetDisplayName(object source, PropertyInfo property)
        {
            return PropertyEditorHelper.GetDisplayName(PropertyEditorHelper.GetResourceManager(source), property);
        }

        private static void ApplyVisibility(FrameworkElement editor, object source, PropertyInfo property)
        {
            PropertyVisibilityAttribute? attribute = property.GetCustomAttribute<PropertyVisibilityAttribute>();
            if (attribute == null)
            {
                return;
            }

            Binding binding = new(attribute.PropertyName)
            {
                Source = source,
                Mode = BindingMode.OneWay,
                Converter = DisplayAlgorithmVisibilityConverter.Instance,
                ConverterParameter = attribute
            };
            editor.SetBinding(UIElement.VisibilityProperty, binding);
        }

        private sealed class DisplayAlgorithmVisibilityConverter : IValueConverter
        {
            public static DisplayAlgorithmVisibilityConverter Instance { get; } = new();

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                PropertyVisibilityAttribute attribute = (PropertyVisibilityAttribute)parameter;
                bool visible = attribute.ExpectedValue == null
                    ? value is true
                    : Equals(value, attribute.ExpectedValue);
                if (attribute.IsInverted)
                {
                    visible = !visible;
                }
                return visible ? Visibility.Visible : Visibility.Collapsed;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotSupportedException();
            }
        }
    }
}

using ColorVision.UI;
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

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public sealed class DisplayAlgorithmConfigurationBuilder
    {
        private const double LabelWidth = 110;
        private const double EditorMinWidth = 150;

        public FrameworkElement Build(
            DisplayAlgorithmConfigBase configuration,
            FrameworkElement? primaryTemplateAction = null)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            StackPanel panel = new();
            BuildObject(configuration, panel, 0, ref primaryTemplateAction);
            return panel;
        }

        private void BuildObject(
            object source,
            Panel panel,
            int depth,
            ref FrameworkElement? primaryTemplateAction)
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
                FrameworkElement? editor = CreateEditor(
                    source,
                    property,
                    depth,
                    ref primaryTemplateAction);
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
            int depth,
            ref FrameworkElement? primaryTemplateAction)
        {
            object? value = property.GetValue(source);

            if (value is DisplayAlgorithmTemplateSelection templateSelection)
            {
                FrameworkElement? action = primaryTemplateAction;
                primaryTemplateAction = null;
                return CreateTemplateEditor(templateSelection, action);
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
                    Content = commandDisplay.DisplayName,
                    Command = command,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(LabelWidth, 2, 0, 2)
                };
            }

            if (value != null && propertyType.IsClass && propertyType != typeof(Type) && depth < 3)
            {
                StackPanel nestedPanel = new();
                BuildObject(value, nestedPanel, depth + 1, ref primaryTemplateAction);
                if (nestedPanel.Children.Count == 0)
                {
                    return null;
                }

                return new Expander
                {
                    Header = GetDisplayName(property),
                    IsExpanded = true,
                    Content = nestedPanel,
                    Margin = new Thickness(depth * 8, 2, 0, 2)
                };
            }

            return null;
        }

        private static FrameworkElement CreateTemplateEditor(
            DisplayAlgorithmTemplateSelection selection,
            FrameworkElement? action)
        {
            DockPanel row = CreateRow();
            TextBlock label = CreateLabel(selection.DisplayName);
            ComboBox comboBox = new()
            {
                MinWidth = EditorMinWidth,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = selection.ItemsSource,
                DisplayMemberPath = "Key",
                SelectedValuePath = "Value"
            };
            comboBox.SetResourceReference(FrameworkElement.StyleProperty, "ComboBox.Small");
            comboBox.SetBinding(
                System.Windows.Controls.Primitives.Selector.SelectedIndexProperty,
                CreateBinding(selection, nameof(selection.SelectedIndex)));

            Grid editButton = new()
            {
                Width = 26,
                Margin = new Thickness(5, 0, 0, 0)
            };
            editButton.Children.Add(new TextBlock
            {
                Text = "\uE713",
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 15,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            editButton.Children.Add(new Button
            {
                Command = selection.EditCommand,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0)
            });

            DockPanel.SetDock(label, Dock.Left);
            if (action != null)
            {
                action.Margin = new Thickness(8, 0, 0, 0);
                DockPanel.SetDock(action, Dock.Right);
            }
            DockPanel.SetDock(editButton, Dock.Right);
            row.Children.Add(label);
            if (action != null)
            {
                row.Children.Add(action);
            }
            row.Children.Add(editButton);
            row.Children.Add(comboBox);
            return row;
        }

        private static FrameworkElement CreateFileEditor(
            object source,
            PropertyInfo property,
            DisplayAlgorithmFileAttribute attribute)
        {
            DockPanel row = CreateRow();
            TextBlock label = CreateLabel(
                property.Name == nameof(DisplayAlgorithmConfigBase.ImageFilePath)
                    ? Properties.Resources.Image
                    : GetDisplayName(property));
            TextBox textBox = CreateTextBox(source, property);
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

            DockPanel.SetDock(label, Dock.Left);
            DockPanel.SetDock(browseButton, Dock.Right);
            row.Children.Add(label);
            row.Children.Add(browseButton);
            row.Children.Add(textBox);
            return row;
        }

        private static FrameworkElement CreateBooleanEditor(object source, PropertyInfo property)
        {
            DockPanel row = CreateRow();
            TextBlock label = CreateLabel(GetDisplayName(property));
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
            TextBlock label = CreateLabel(GetDisplayName(property));
            ComboBox comboBox = new()
            {
                MinWidth = EditorMinWidth,
                ItemsSource = Enum.GetValues(enumType)
            };
            comboBox.SetResourceReference(FrameworkElement.StyleProperty, "ComboBox.Small");
            comboBox.SetBinding(
                System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
                CreateBinding(source, property.Name));
            DockPanel.SetDock(label, Dock.Left);
            row.Children.Add(label);
            row.Children.Add(comboBox);
            return row;
        }

        private static FrameworkElement CreateTextEditor(object source, PropertyInfo property)
        {
            DockPanel row = CreateRow();
            TextBlock label = CreateLabel(GetDisplayName(property));
            TextBox textBox = CreateTextBox(source, property);
            DockPanel.SetDock(label, Dock.Left);
            row.Children.Add(label);
            row.Children.Add(textBox);
            return row;
        }

        private static TextBox CreateTextBox(object source, PropertyInfo property)
        {
            TextBox textBox = new()
            {
                MinWidth = EditorMinWidth,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            textBox.SetResourceReference(FrameworkElement.StyleProperty, "TextBox.Small");
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

        private static string GetDisplayName(PropertyInfo property)
        {
            return property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? property.Name;
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

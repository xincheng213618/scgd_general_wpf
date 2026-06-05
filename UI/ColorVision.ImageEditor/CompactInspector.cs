using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor
{
    public interface ICompactInspectorProvider
    {
        IEnumerable<CompactInspectorItem> GetCompactInspectorItems();
    }

    public enum CompactInspectorEditorKind
    {
        Auto,
        Toggle,
        Number,
        Text,
        Brush,
        Enum,
    }

    public abstract class CompactInspectorItem
    {
        public int Order { get; init; }
        public string? ToolTip { get; init; }
    }

    public sealed class CompactInspectorPropertyItem : CompactInspectorItem
    {
        public required object Source { get; init; }
        public required string PropertyName { get; init; }
        public string? Label { get; init; }
        public object? Icon { get; init; }
        public bool ShowLabel { get; init; }
        public double Width { get; init; } = 52;
        public CompactInspectorEditorKind EditorKind { get; init; } = CompactInspectorEditorKind.Auto;
    }

    public static class CompactInspectorIcons
    {
        public static TextBlock CreateText(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            };
        }

        public static TextBlock CreateGlyph(string glyph)
        {
            return new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            };
        }
    }

    public sealed class CompactInspectorButtonItem : CompactInspectorItem
    {
        public string? Label { get; init; }
        public object? Icon { get; init; }
        public required ICommand Command { get; init; }
        public double Width { get; init; } = 28;
    }

    public sealed class CompactInspectorPresenter : IDisposable
    {
        private readonly EditorContext _context;

        public CompactInspectorPresenter(EditorContext context)
        {
            _context = context;
            _context.DrawEditorManager.CurrentChanged += DrawEditorManager_CurrentChanged;
            _context.SelectionVisual.SelectionChanged += SelectionVisual_SelectionChanged;
            Refresh();
        }

        private void DrawEditorManager_CurrentChanged(object? sender, EventArgs e)
        {
            Refresh();
        }

        private void SelectionVisual_SelectionChanged(object? sender, EventArgs e)
        {
            Refresh();
        }

        public void Refresh()
        {
            object? source = ResolveSource();
            List<CompactInspectorItem> items = ResolveItems(source)
                .OrderBy(item => item.Order)
                .ToList();

            if (source is DrawingVisualBase drawingVisual)
            {
                items.Add(new CompactInspectorButtonItem
                {
                    Order = 10_000,
                    Icon = CompactInspectorIcons.CreateText("⋯"),
                    Width = 22,
                    ToolTip = "完整编辑",
                    Command = new RelayCommand(_ =>
                    {
                        new PropertyEditorWindow(drawingVisual.BaseAttribute)
                        {
                            Owner = _context.OwnerWindow,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        }.ShowDialog();
                    })
                });
            }

            _context.SetCompactInspectorItems(items.Select(CompactInspectorElementFactory.CreateElement));
        }

        private object? ResolveSource()
        {
            if (_context.DrawEditorManager.Current is ICompactInspectorProvider currentToolProvider)
            {
                return currentToolProvider;
            }

            if (_context.DrawEditorManager.Current != null)
            {
                return null;
            }

            if (_context.SelectionVisual.PrimarySelectedVisual is { } selectedVisual)
            {
                return selectedVisual;
            }

            return null;
        }

        private IEnumerable<CompactInspectorItem> ResolveItems(object? source)
        {
            if (source is ICompactInspectorProvider provider)
            {
                return provider.GetCompactInspectorItems();
            }

            if (source is DrawingVisualBase drawingVisual && drawingVisual.BaseAttribute is ICompactInspectorProvider attributeProvider)
            {
                return attributeProvider.GetCompactInspectorItems();
            }

            return Array.Empty<CompactInspectorItem>();
        }

        public void Dispose()
        {
            _context.DrawEditorManager.CurrentChanged -= DrawEditorManager_CurrentChanged;
            _context.SelectionVisual.SelectionChanged -= SelectionVisual_SelectionChanged;
        }
    }

    internal static class CompactInspectorElementFactory
    {
        public static FrameworkElement CreateElement(CompactInspectorItem item)
        {
            return item switch
            {
                CompactInspectorPropertyItem propertyItem => CreatePropertyElement(propertyItem),
                CompactInspectorButtonItem buttonItem => CreateButtonElement(buttonItem),
                _ => new TextBlock { Text = "?" }
            };
        }

        private static FrameworkElement CreatePropertyElement(CompactInspectorPropertyItem item)
        {
            PropertyInfo? property = item.Source.GetType().GetProperty(item.PropertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanRead || !property.CanWrite)
            {
                return CreateFallbackChip(item.Label ?? item.PropertyName, item.ToolTip);
            }

            CompactInspectorEditorKind editorKind = ResolveEditorKind(item.EditorKind, property.PropertyType);
            string toolTip = item.ToolTip ?? GetDisplayName(item.Source, property, item.Label);

            FrameworkElement element = editorKind switch
            {
                CompactInspectorEditorKind.Toggle => CreateToggleElement(item, toolTip),
                CompactInspectorEditorKind.Brush => CreateBrushElement(item, property, toolTip),
                CompactInspectorEditorKind.Number => CreateTextInputElement(item, property, toolTip, HorizontalAlignment.Center),
                CompactInspectorEditorKind.Text => CreateTextInputElement(item, property, toolTip, HorizontalAlignment.Left),
                CompactInspectorEditorKind.Enum => CreateEnumElement(item, property, toolTip),
                _ => CreateAutoElement(item, property, toolTip),
            };

            return WrapWithLabel(item, element, toolTip);
        }

        private static FrameworkElement CreateFallbackChip(string text, string? toolTip)
        {
            Border border = CreateChipBorder(toolTip);
            border.Child = new TextBlock
            {
                Text = text,
                Margin = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center,
            };
            return border;
        }

        private static FrameworkElement CreateButtonElement(CompactInspectorButtonItem item)
        {
            Button button = new Button
            {
                Width = item.Width,
                Height = 20,
                Padding = new Thickness(4, 0, 4, 0),
                Margin = new Thickness(0),
                MinWidth = item.Width,
                Command = item.Command,
                ToolTip = item.ToolTip,
                Content = item.Icon ?? item.Label ?? "...",
            };

            button.SetResourceReference(FrameworkElement.StyleProperty, "CompactInspectorButtonStyle");

            return WrapControlInChip(button, item.ToolTip);
        }

        private static FrameworkElement CreateAutoElement(CompactInspectorPropertyItem item, PropertyInfo property, string toolTip)
        {
            CompactInspectorEditorKind kind = ResolveEditorKind(CompactInspectorEditorKind.Auto, property.PropertyType);
            return kind switch
            {
                CompactInspectorEditorKind.Toggle => CreateToggleElement(item, toolTip),
                CompactInspectorEditorKind.Brush => CreateBrushElement(item, property, toolTip),
                CompactInspectorEditorKind.Enum => CreateEnumElement(item, property, toolTip),
                CompactInspectorEditorKind.Number => CreateTextInputElement(item, property, toolTip, HorizontalAlignment.Center),
                _ => CreateTextInputElement(item, property, toolTip, HorizontalAlignment.Left),
            };
        }

        private static CompactInspectorEditorKind ResolveEditorKind(CompactInspectorEditorKind preferredKind, Type propertyType)
        {
            if (preferredKind != CompactInspectorEditorKind.Auto)
            {
                return preferredKind;
            }

            if (propertyType == typeof(bool))
            {
                return CompactInspectorEditorKind.Toggle;
            }

            if (propertyType.IsEnum)
            {
                return CompactInspectorEditorKind.Enum;
            }

            if (propertyType == typeof(double) || propertyType == typeof(float) || propertyType == typeof(int) || propertyType == typeof(long) || propertyType == typeof(decimal))
            {
                return CompactInspectorEditorKind.Number;
            }

            if (propertyType == typeof(Color) || typeof(Brush).IsAssignableFrom(propertyType))
            {
                return CompactInspectorEditorKind.Brush;
            }

            return CompactInspectorEditorKind.Text;
        }

        private static FrameworkElement CreateToggleElement(CompactInspectorPropertyItem item, string toolTip)
        {
            ToggleButton toggleButton = new ToggleButton
            {
                MinWidth = 24,
                Height = 20,
                Padding = new Thickness(4, 0, 4, 0),
                Margin = new Thickness(0),
                ToolTip = toolTip,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = item.Icon ?? item.Label ?? toolTip,
            };

            toggleButton.SetResourceReference(FrameworkElement.StyleProperty, "CompactInspectorToggleButtonStyle");
            toggleButton.SetBinding(ToggleButton.IsCheckedProperty, CreateBinding(item.Source, item.PropertyName));
            return WrapControlInChip(toggleButton, toolTip);
        }

        private static FrameworkElement CreateBrushElement(CompactInspectorPropertyItem item, PropertyInfo property, string toolTip)
        {
            Button button = new Button
            {
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                ToolTip = toolTip,
            };

            button.SetResourceReference(FrameworkElement.StyleProperty, "CompactInspectorButtonStyle");

            object? currentValue = property.GetValue(item.Source);
            if (currentValue is Color color)
            {
                button.Background = new SolidColorBrush(color);
            }
            else
            {
                button.SetBinding(Control.BackgroundProperty, new Binding(item.PropertyName)
                {
                    Source = item.Source,
                    Mode = BindingMode.OneWay,
                });
            }

            button.Click += (_, __) => OpenColorPicker(item.Source, property, button);
            return WrapControlInChip(button, toolTip);
        }

        private static FrameworkElement CreateTextInputElement(CompactInspectorPropertyItem item, PropertyInfo property, string toolTip, HorizontalAlignment alignment)
        {
            TextBox textBox = new TextBox
            {
                Width = item.Width,
                Height = 20,
                MinWidth = item.Width,
                Margin = new Thickness(0),
                ToolTip = toolTip,
                HorizontalContentAlignment = alignment,
            };

            textBox.SetResourceReference(FrameworkElement.StyleProperty, "CompactInspectorValueTextBoxStyle");
            textBox.SetBinding(TextBox.TextProperty, CreateBinding(item.Source, item.PropertyName));
            return WrapControlInChip(textBox, toolTip);
        }

        private static FrameworkElement CreateEnumElement(CompactInspectorPropertyItem item, PropertyInfo property, string toolTip)
        {
            ComboBox comboBox = new ComboBox
            {
                Width = Math.Max(item.Width, 72),
                Height = 20,
                MinWidth = Math.Max(item.Width, 72),
                Margin = new Thickness(0),
                ToolTip = toolTip,
                ItemsSource = Enum.GetValues(property.PropertyType),
            };

            comboBox.SetResourceReference(FrameworkElement.StyleProperty, "CompactInspectorValueComboBoxStyle");
            comboBox.SetBinding(Selector.SelectedItemProperty, CreateBinding(item.Source, item.PropertyName));
            return WrapControlInChip(comboBox, toolTip);
        }

        private static FrameworkElement WrapWithLabel(CompactInspectorPropertyItem item, FrameworkElement element, string toolTip)
        {
            FrameworkElement? prefix = CreatePrefixElement(item);
            if (prefix == null && (!item.ShowLabel || string.IsNullOrWhiteSpace(item.Label)))
            {
                return element;
            }

            StackPanel stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(2, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = toolTip,
            };

            if (prefix != null)
            {
                stackPanel.Children.Add(prefix);
            }
            else
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = item.Label,
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 10.5,
                    Opacity = 0.72,
                });
            }

            stackPanel.Children.Add(element);
            return stackPanel;
        }

        private static FrameworkElement? CreatePrefixElement(CompactInspectorPropertyItem item)
        {
            if (item.EditorKind == CompactInspectorEditorKind.Toggle || item.Icon == null)
            {
                return null;
            }

            FrameworkElement prefix = item.Icon as FrameworkElement
                ?? new TextBlock
                {
                    Text = Convert.ToString(item.Icon, CultureInfo.CurrentCulture) ?? string.Empty,
                };

            prefix.Margin = new Thickness(0, 0, 4, 0);
            prefix.VerticalAlignment = VerticalAlignment.Center;
            prefix.Opacity = 0.78;
            return prefix;
        }

        private static Border WrapControlInChip(FrameworkElement control, string? toolTip)
        {
            Border border = CreateChipBorder(toolTip);
            border.Child = control;
            return border;
        }

        private static Border CreateChipBorder(string? toolTip)
        {
            Border border = new Border
            {
                Margin = new Thickness(1, 0, 0, 0),
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = toolTip,
            };
            border.Background = Brushes.Transparent;
            border.BorderBrush = Brushes.Transparent;
            return border;
        }

        private static Binding CreateBinding(object source, string propertyName)
        {
            return new Binding(propertyName)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            };
        }

        private static void OpenColorPicker(object source, PropertyInfo property, Button button)
        {
            HandyControl.Controls.ColorPicker colorPicker = new HandyControl.Controls.ColorPicker();
            object? value = property.GetValue(source);
            if (value is SolidColorBrush solidColorBrush)
            {
                colorPicker.SelectedBrush = solidColorBrush;
            }
            else if (value is Color color)
            {
                colorPicker.SelectedBrush = new SolidColorBrush(color);
            }

            Window window = new Window
            {
                Owner = Application.Current?.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = colorPicker,
                Width = 250,
                Height = 400,
            };

            colorPicker.Confirmed += (_, __) =>
            {
                if (property.PropertyType == typeof(Color))
                {
                    property.SetValue(source, colorPicker.SelectedBrush.Color);
                    button.Background = new SolidColorBrush(colorPicker.SelectedBrush.Color);
                }
                else
                {
                    property.SetValue(source, colorPicker.SelectedBrush);
                    button.Background = colorPicker.SelectedBrush;
                }

                window.Close();
            };
            window.Closed += (_, __) => colorPicker.Dispose();
            window.ShowDialog();
        }

        private static string GetDisplayName(object source, PropertyInfo property, string? fallbackLabel)
        {
            ResourceManager? resourceManager = PropertyEditorHelper.GetResourceManager(source);
            string raw = fallbackLabel
                ?? property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
                ?? property.Name;

            return resourceManager?.GetString(raw, CultureInfo.CurrentUICulture) ?? raw;
        }
    }
}

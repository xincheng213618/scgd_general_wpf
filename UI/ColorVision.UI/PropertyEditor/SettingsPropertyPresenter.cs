using System.Reflection;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.UI
{
    /// <summary>Settings layout around the existing property editors and their validation/visibility bindings.</summary>
    public static class SettingsPropertyPresenter
    {
        public static FrameworkElement Create(object source, IReadOnlyList<string>? propertyNames = null)
        {
            StackPanel panel = new();
            var resourceManager = PropertyEditorHelper.GetResourceManager(source);
            IEnumerable<PropertyInfo> properties = propertyNames == null
                ? PropertyEditorHelper.GetEditableProperties(source.GetType())
                : propertyNames.Select(name => source.GetType().GetProperty(name)).OfType<PropertyInfo>();

            foreach (PropertyInfo property in properties)
            {
                if (!PropertyEditorHelper.TryCreatePropertyDockPanel(property, source, out DockPanel editor)) continue;
                DisplayAttribute? display = property.GetCustomAttribute<DisplayAttribute>();
                string title = display?.GetName() ?? PropertyEditorHelper.GetDisplayName(resourceManager, property);
                string description = display?.GetDescription() ?? PropertyEditorHelper.GetDescription(resourceManager, property);
                TextBlock? originalLabel = editor.Children.OfType<TextBlock>().FirstOrDefault();
                if (originalLabel != null) editor.Children.Remove(originalLabel);
                editor.Margin = new Thickness(0);
                editor.HorizontalAlignment = HorizontalAlignment.Stretch;
                editor.VerticalAlignment = VerticalAlignment.Center;

                if (editor.Children.Count == 1 && editor.Children[0] is FrameworkElement control)
                {
                    control.Margin = new Thickness(0);
                    control.MinWidth = 0;
                    AutomationProperties.SetName(control, title);
                    if (control is TextBox or ComboBox)
                    {
                        control.Width = double.NaN;
                        control.MinHeight = 30;
                        control.HorizontalAlignment = HorizontalAlignment.Stretch;
                        control.SetResourceReference(Control.ForegroundProperty, "GlobalTextBrush");
                        control.SetResourceReference(Control.BackgroundProperty, "ButtonBackground");
                        control.SetResourceReference(Control.BorderBrushProperty, "ButtonBorderBrush");
                    }
                    editor.LastChildFill = control is TextBox or ComboBox or Panel;
                }

                Grid row = new() { Margin = new Thickness(14, 8, 14, 8), MinHeight = 30 };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
                StackPanel labels = new() { Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
                TextBlock name = new() { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
                name.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                labels.Children.Add(name);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    TextBlock hint = new() { Text = description, FontSize = 12, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
                    hint.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
                    labels.Children.Add(hint);
                }
                row.Children.Add(labels);
                Grid.SetColumn(editor, 1);
                row.Children.Add(editor);
                Border border = new() { Child = row, BorderThickness = new Thickness(0, 0, 0, 1) };
                border.SetResourceReference(Border.BorderBrushProperty, "ButtonBorderBrush");
                border.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(UIElement.Visibility)) { Source = editor });
                panel.Children.Add(border);
            }
            return panel;
        }
    }
}

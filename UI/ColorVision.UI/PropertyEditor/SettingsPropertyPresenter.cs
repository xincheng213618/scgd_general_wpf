using System.ComponentModel;
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
            IReadOnlyList<PropertyInfo> properties = (propertyNames == null
                ? PropertyEditorHelper.GetEditableProperties(source.GetType())
                : propertyNames.Select(name => source.GetType().GetProperty(name)).OfType<PropertyInfo>()).ToArray();

            var groups = properties.GroupBy(property => GetGroupName(property, resourceManager)).ToArray();
            bool showGroups = groups.Length > 1 || groups.Any(group => !string.IsNullOrWhiteSpace(group.Key));
            foreach (var group in groups)
            {
                if (showGroups && !string.IsNullOrWhiteSpace(group.Key))
                    panel.Children.Add(CreateGroupHeader(group.Key));

                foreach (PropertyInfo property in group)
                    AddPropertyRow(panel, source, property, resourceManager);
            }
            return panel;
        }

        private static string GetGroupName(PropertyInfo property, System.Resources.ResourceManager? resourceManager)
        {
            string? displayGroup = property.GetCustomAttribute<DisplayAttribute>()?.GetGroupName();
            if (!string.IsNullOrWhiteSpace(displayGroup)) return displayGroup;
            return PropertyEditorHelper.GetLocalizedString(resourceManager, property.GetCustomAttribute<CategoryAttribute>()?.Category);
        }

        private static Border CreateGroupHeader(string title)
        {
            TextBlock text = new() { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(14, 10, 14, 9) };
            text.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            Border header = new() { Child = text, BorderThickness = new Thickness(0, 1, 0, 1) };
            header.SetResourceReference(Border.BackgroundProperty, "GlobalBorderBrush");
            header.SetResourceReference(Border.BorderBrushProperty, "ButtonBorderBrush");
            return header;
        }

        private static void AddPropertyRow(Panel panel, object source, PropertyInfo property, System.Resources.ResourceManager? resourceManager)
        {
            if (!PropertyEditorHelper.TryCreatePropertyDockPanel(property, source, out DockPanel editor)) return;
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
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
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
    }
}

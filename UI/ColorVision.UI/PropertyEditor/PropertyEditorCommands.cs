using ColorVision.UI.Extension;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.UI
{
    public static partial class PropertyEditorHelper
    {
        // Keep the existing public signature: device pages and compiled extensions share this entry point.
        public static void GenCommand(object obj, UniformGrid uniformGrid, bool compact = false)
        {
            if (uniformGrid == null) return;
            ArgumentNullException.ThrowIfNull(obj);

            uniformGrid.SizeChanged -= CompactCommands_SizeChanged;
            uniformGrid.Children.Clear();
            ResourceManager? resources = GetResourceManager(obj);
            var commands = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                .Select(property => (Property: property, Display: property.GetCustomAttribute<CommandDisplayAttribute>(),
                    Category: property.GetCustomAttribute<CategoryAttribute>()?.Category ?? string.Empty))
                .Where(item => item.Display != null && (item.Property.GetCustomAttribute<BrowsableAttribute>()?.Browsable ?? true))
                .Where(item => item.Property.GetValue(obj) is ICommand)
                .OrderBy(item => item.Display!.Order)
                .ToList();

            if (compact)
            {
                uniformGrid.SizeChanged += CompactCommands_SizeChanged;
                foreach (var item in commands)
                    uniformGrid.Children.Add(CreateCommandButton(obj, item.Property, item.Display!, resources, true));
                uniformGrid.AutoUpdateLayout(100);
                return;
            }

            // A single vertical root avoids equal-height category rows imposed by the existing UniformGrid hosts.
            uniformGrid.Columns = 1;
            uniformGrid.Rows = 1;
            if (commands.Count == 0) return;

            var root = new StackPanel();
            root.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/ColorVision.UI;component/PropertyEditor/CommandPanelStyles.xaml", UriKind.Relative)
            });
            uniformGrid.Children.Add(root);
            var groups = commands.GroupBy(item => item.Category)
                .OrderBy(group => group.Min(item => item.Display!.CategoryOrder))
                .ThenBy(group => group.Min(item => item.Display!.Order))
                .ToList();

            foreach (var group in groups)
            {
                var content = new StackPanel();
                if (!string.IsNullOrWhiteSpace(group.Key) || groups.Count > 1)
                {
                    var heading = new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(group.Key) ? Properties.Resources.CommandOtherActions : GetLocalizedString(resources, group.Key)
                    };
                    heading.SetResourceReference(FrameworkElement.StyleProperty, "PropertyEditorCommandHeading");
                    content.Children.Add(heading);
                }

                var buttons = new UniformGrid { Columns = 3 };
                buttons.SizeChanged += CommandCategory_SizeChanged;
                foreach (var item in group)
                    buttons.Children.Add(CreateCommandButton(obj, item.Property, item.Display!, resources, false));
                content.Children.Add(buttons);

                var section = new Border { Child = content };
                section.SetResourceReference(FrameworkElement.StyleProperty, "PropertyEditorCommandSection");
                root.Children.Add(section);
            }
        }

        private static Button CreateCommandButton(object source, PropertyInfo property, CommandDisplayAttribute display, ResourceManager? resources, bool compact)
        {
            string name = GetDisplayName(resources, property, display.DisplayName);
            string description = GetDescription(resources, property);
            var button = new Button
            {
                Tag = property,
                ToolTip = string.IsNullOrWhiteSpace(description) ? name : $"{name}\n{description}",
                HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Stretch
            };
            button.SetBinding(Button.CommandProperty, new Binding(property.Name) { Source = source, Mode = BindingMode.OneWay });
            AutomationProperties.SetName(button, name);
            AutomationProperties.SetHelpText(button, description);
            ApplyVisibilityBinding(button, property, source);

            if (compact)
            {
                button.Margin = new Thickness(2, 0, 2, 0);
                button.Content = name;
                button.SetResourceReference(FrameworkElement.StyleProperty, "ButtonDefault.Small");
            }
            else
            {
                button.SetResourceReference(FrameworkElement.StyleProperty, "PropertyEditorCommandButton");
                var text = new StackPanel();
                var title = new TextBlock { Text = name, FontSize = 13, FontWeight = FontWeights.Medium, TextWrapping = TextWrapping.Wrap };
                title.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Button.Foreground))
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1)
                });
                text.Children.Add(title);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    var detail = new TextBlock { Text = description, FontSize = 10.5, Margin = new Thickness(0, 2, 0, 0), Opacity = 0.72, TextTrimming = TextTrimming.CharacterEllipsis };
                    detail.SetResourceReference(TextBlock.ForegroundProperty, "PropertyEditorCommandTextSecondary");
                    text.Children.Add(detail);
                }
                button.Content = text;
            }

            if (display.CommandType == CommandType.Highlighted)
                button.SetResourceReference(Control.ForegroundProperty, "DangerBrush");
            return button;
        }

        private static void CompactCommands_SizeChanged(object sender, SizeChangedEventArgs e) => ((UniformGrid)sender).AutoUpdateLayout(100);
        private static void CommandCategory_SizeChanged(object sender, SizeChangedEventArgs e) => ((UniformGrid)sender).AutoUpdateLayout(180);
    }
}

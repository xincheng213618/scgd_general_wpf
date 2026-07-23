#pragma warning disable CS8625
using ColorVision.Engine.Templates.Jsons;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    public class NodePanelBuilder
    {
        private const double DefaultSignLabelMinWidth = 90;

        private readonly NodeConfiguratorContext _context;

        public NodePanelBuilder(NodeConfiguratorContext context)
        {
            _context = context;
        }

        private static TextBlock CreateSignLabel(string text, double minWidth = DefaultSignLabelMinWidth)
        {
            return new TextBlock
            {
                Text = text,
                MinWidth = minWidth,
                Margin = new Thickness(0, 0, 6, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = text,
                Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"]
            };
        }

        private (DockPanel Panel, HandyControl.Controls.ComboBox ComboBox) CreateTemplateSelector<T>(string propertyName, string signName, IEnumerable<TemplateModel<T>> itemSource) where T : ParamBase
        {
            var property = _context.Node.GetType().GetProperty(propertyName);
            if (property?.PropertyType != typeof(string) || !property.CanWrite)
                throw new InvalidOperationException($"{_context.Node.GetType().Name}.{propertyName} must be a writable string property.");

            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(CreateSignLabel(signName));

            HandyControl.Controls.ComboBox comboBox = new HandyControl.Controls.ComboBox()
            {
                IsEditable = true,
                SelectedValuePath = "Key",
                DisplayMemberPath = "Key",
                Style = (Style)Application.Current.FindResource("ComboBoxPlus.Small")
            };
            HandyControl.Controls.InfoElement.SetShowClearButton(comboBox, true);
            comboBox.ItemsSource = itemSource;
            comboBox.SetBinding(Selector.SelectedValueProperty, new Binding(propertyName)
            {
                Source = _context.Node,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                Converter = EmptySelectionConverter.Instance
            });

            return (dockPanel, comboBox);
        }

        private static Button CreateOpenTemplateEditorButton(ITemplate template, ComboBox comboBox)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = "\uE713",
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 15,
                Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"]
            };

            Button button = new Button
            {
                Width = 20,
                Padding = new Thickness(2),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(5, 0, 0, 0),
                Content = textBlock
            };
            button.Click += (_, _) =>
            {
                new TemplateEditorWindow(template, comboBox.SelectedIndex)
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                }.ShowDialog();
            };
            return button;
        }

        public void AddTemplateCollectionPanel<T>(string propertyName, string signName, ObservableCollection<TemplateModel<T>> itemSource) where T : ParamModBase
        {
            var selector = CreateTemplateSelector(propertyName, signName, itemSource);
            selector.Panel.Children.Add(selector.ComboBox);
            _context.SignStackPanel.Children.Add(selector.Panel);
        }

        public void AddTemplateJsonPanel<T>(string propertyName, string signName, ITemplateJson<T> template) where T : TemplateJsonParam, new()
        {
            var selector = CreateTemplateSelector(propertyName, signName, template.TemplateParams);
            Button openTemplateEditorButton = CreateOpenTemplateEditorButton(template, selector.ComboBox);
            DockPanel.SetDock(openTemplateEditorButton, Dock.Right);
            selector.Panel.Children.Add(openTemplateEditorButton);
            selector.Panel.Children.Add(selector.ComboBox);
            _context.SignStackPanel.Children.Add(selector.Panel);
        }

        public void AddTemplatePanel<T>(string propertyName, string signName, ITemplate<T> template) where T : ParamModBase, new()
        {
            var selector = CreateTemplateSelector(propertyName, signName, template.TemplateParams);
            Button openTemplateEditorButton = CreateOpenTemplateEditorButton(template, selector.ComboBox);
            DockPanel.SetDock(openTemplateEditorButton, Dock.Right);
            selector.Panel.Children.Add(openTemplateEditorButton);
            selector.Panel.Children.Add(selector.ComboBox);
            _context.SignStackPanel.Children.Add(selector.Panel);
        }

        private sealed class EmptySelectionConverter : IValueConverter
        {
            public static EmptySelectionConverter Instance { get; } = new EmptySelectionConverter();

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value;

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value ?? string.Empty;
        }
    }
}

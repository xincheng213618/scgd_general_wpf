#pragma warning disable CS8625
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.FlowProcessing.Editor.NodeConfiguration
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

        private (DockPanel Panel, HandyControl.Controls.ComboBox ComboBox, Action Refresh) CreateTemplateSelector<T>(string propertyName, string signName, IEnumerable<TemplateModel<T>> itemSource) where T : ParamBase
        {
            var property = _context.Node.GetType().GetProperty(propertyName);
            if (property?.PropertyType != typeof(string) || !property.CanWrite)
                throw new InvalidOperationException($"{_context.Node.GetType().Name}.{propertyName} must be a writable string property.");

            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(CreateSignLabel(signName));

            HandyControl.Controls.ComboBox comboBox = new HandyControl.Controls.ComboBox()
            {
                IsEditable = true,
                DisplayMemberPath = "Key",
                Style = (Style)Application.Current.FindResource("ComboBoxPlus.Small")
            };
            HandyControl.Controls.InfoElement.SetShowClearButton(comboBox, true);
            bool isRefreshing = false;
            void RefreshItems()
            {
                isRefreshing = true;
                try { TemplateSelectionDisplay.Select(comboBox, itemSource, property.GetValue(_context.Node)?.ToString()); }
                finally { isRefreshing = false; }
            }
            RefreshItems();
            comboBox.SelectionChanged += (_, _) =>
            {
                if (isRefreshing) return;
                string selectedName = TemplateSelectionDisplay.GetName(comboBox.SelectedItem);
                if (property.GetValue(_context.Node)?.ToString() != selectedName)
                    property.SetValue(_context.Node, selectedName);
                comboBox.ToolTip = (comboBox.SelectedItem as TemplateSelectionDisplay.MissingTemplate)?.Key;
            };

            EventHandler<PropertyChangedEventArgs> propertyChanged = (_, _) => RefreshItems();
            bool isSubscribed = true;
            PropertyChangedEventManager.AddHandler(_context.Node, propertyChanged, propertyName);
            comboBox.Unloaded += (_, _) =>
            {
                PropertyChangedEventManager.RemoveHandler(_context.Node, propertyChanged, propertyName);
                isSubscribed = false;
            };
            comboBox.Loaded += (_, _) =>
            {
                if (!isSubscribed)
                {
                    PropertyChangedEventManager.AddHandler(_context.Node, propertyChanged, propertyName);
                    isSubscribed = true;
                }
                RefreshItems();
            };
            return (dockPanel, comboBox, RefreshItems);
        }

        private static Button CreateOpenTemplateEditorButton(ITemplate template, ComboBox comboBox, Action refresh)
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
                int selectedIndex = comboBox.SelectedItem is TemplateBase ? comboBox.SelectedIndex : -1;
                var window = template.CreateManagerWindow(selectedIndex);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
                refresh();
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
            Button openTemplateEditorButton = CreateOpenTemplateEditorButton(template, selector.ComboBox, selector.Refresh);
            DockPanel.SetDock(openTemplateEditorButton, Dock.Right);
            selector.Panel.Children.Add(openTemplateEditorButton);
            selector.Panel.Children.Add(selector.ComboBox);
            _context.SignStackPanel.Children.Add(selector.Panel);
        }

        public void AddTemplatePanel<T>(string propertyName, string signName, ITemplate<T> template) where T : ParamModBase, new()
        {
            var selector = CreateTemplateSelector(propertyName, signName, template.TemplateParams);
            Button openTemplateEditorButton = CreateOpenTemplateEditorButton(template, selector.ComboBox, selector.Refresh);
            DockPanel.SetDock(openTemplateEditorButton, Dock.Right);
            selector.Panel.Children.Add(openTemplateEditorButton);
            selector.Panel.Children.Add(selector.ComboBox);
            _context.SignStackPanel.Children.Add(selector.Panel);
        }
    }
}

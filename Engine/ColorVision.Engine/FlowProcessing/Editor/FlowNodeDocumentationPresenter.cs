using FlowEngineLib;
using ST.Library.UI;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.FlowProcessing.Editor
{
    internal sealed record FlowNodeDocumentation(
        string Title,
        string Summary,
        string Usage,
        string Processing,
        string Notes,
        IReadOnlyList<FlowNodePortDocumentation> Inputs,
        IReadOnlyList<FlowNodePortDocumentation> Outputs,
        IReadOnlyList<FlowNodePropertyDocumentation> Properties);

    internal sealed record FlowNodePortDocumentation(string Name, string DataType);

    internal sealed record FlowNodePropertyDocumentation(string Name, string Description);

    internal static class FlowNodeDocumentationPresenter
    {
        public static FlowNodeDocumentation GetDocumentation(STNode node)
        {
            ArgumentNullException.ThrowIfNull(node);

            Type nodeType = node.GetType();
            FlowNodeDocumentationAttribute? documentationAttribute = nodeType.GetCustomAttribute<FlowNodeDocumentationAttribute>(inherit: true);
            STNodeAttribute? nodeAttribute = nodeType.GetCustomAttribute<STNodeAttribute>(inherit: true);
            string summary = Localize(documentationAttribute?.Summary);
            if (string.IsNullOrWhiteSpace(summary))
                summary = nodeAttribute?.DisplayDescription ?? string.Empty;
            if (string.IsNullOrWhiteSpace(summary))
                summary = "该节点尚未提供用途说明。";

            return new FlowNodeDocumentation(
                string.IsNullOrWhiteSpace(node.Title) ? nodeType.Name : node.Title,
                summary,
                Localize(documentationAttribute?.Usage),
                Localize(documentationAttribute?.Processing),
                Localize(documentationAttribute?.Notes),
                GetPorts(node.GetAllInputOptions()),
                GetPorts(node.GetAllOutputOptions()),
                GetProperties(nodeType));
        }

        public static FrameworkElement Create(STNode node)
        {
            FlowNodeDocumentation documentation = GetDocumentation(node);
            var panel = new StackPanel { Margin = new Thickness(10, 8, 10, 12) };

            var title = new TextBlock
            {
                Text = documentation.Title,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            SetTextBrush(title);
            panel.Children.Add(title);

            AddSection(panel, "用途", documentation.Summary);
            AddSection(panel, "如何使用", documentation.Usage);
            AddSection(panel, "处理顺序", documentation.Processing);
            AddPortsSection(panel, documentation.Inputs, documentation.Outputs);
            AddPropertiesSection(panel, documentation.Properties);
            AddSection(panel, "注意事项", documentation.Notes);
            return panel;
        }

        private static IReadOnlyList<FlowNodePortDocumentation> GetPorts(IEnumerable<STNodeOption> options)
        {
            return options
                .Where(option => option != null && !ReferenceEquals(option, STNodeOption.Empty) && !string.IsNullOrWhiteSpace(option.Text))
                .Select(option => new FlowNodePortDocumentation(option.Text, GetFriendlyTypeName(option.DataType)))
                .ToArray();
        }

        private static IReadOnlyList<FlowNodePropertyDocumentation> GetProperties(Type nodeType)
        {
            return nodeType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => FlowNodePropertyMetadataProvider.Instance.IsPropertyManaged(property))
                .Where(property => FlowNodePropertyMetadataProvider.Instance.IsBrowsable(property))
                .Where(property => !FlowNodePropertyMetadataProvider.AdvancedOptions.IsAdvancedProperty(property))
                .Select(property => new FlowNodePropertyDocumentation(
                    FlowNodePropertyMetadataProvider.Instance.GetDisplayName(property) ?? property.Name,
                    FlowNodePropertyMetadataProvider.Instance.GetDescription(property) ?? string.Empty))
                .Where(property => !string.IsNullOrWhiteSpace(property.Description))
                .ToArray();
        }

        private static void AddSection(Panel panel, string heading, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return;

            panel.Children.Add(CreateHeading(heading));
            var body = new TextBlock
            {
                Text = content,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20,
                Margin = new Thickness(0, 0, 0, 12)
            };
            SetTextBrush(body);
            panel.Children.Add(body);
        }

        private static void AddPortsSection(
            Panel panel,
            IReadOnlyList<FlowNodePortDocumentation> inputs,
            IReadOnlyList<FlowNodePortDocumentation> outputs)
        {
            if (inputs.Count == 0 && outputs.Count == 0)
                return;

            panel.Children.Add(CreateHeading("端口"));
            AddPortRows(panel, "输入", inputs);
            AddPortRows(panel, "输出", outputs);
            panel.Children.Add(new Border { Height = 8 });
        }

        private static void AddPortRows(Panel panel, string direction, IReadOnlyList<FlowNodePortDocumentation> ports)
        {
            foreach (FlowNodePortDocumentation port in ports)
            {
                var text = new TextBlock
                {
                    Text = $"{direction} · {port.Name}    {port.DataType}",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 1, 0, 3)
                };
                SetTextBrush(text);
                panel.Children.Add(text);
            }
        }

        private static void AddPropertiesSection(Panel panel, IReadOnlyList<FlowNodePropertyDocumentation> properties)
        {
            if (properties.Count == 0)
                return;

            panel.Children.Add(CreateHeading("参数说明"));
            foreach (FlowNodePropertyDocumentation property in properties)
            {
                var row = new Grid { Margin = new Thickness(0, 2, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                row.ColumnDefinitions.Add(new ColumnDefinition());

                var name = new TextBlock
                {
                    Text = property.Name,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                SetTextBrush(name);
                row.Children.Add(name);

                var description = new TextBlock
                {
                    Text = property.Description,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 19
                };
                SetTextBrush(description);
                Grid.SetColumn(description, 1);
                row.Children.Add(description);
                panel.Children.Add(row);
            }
            panel.Children.Add(new Border { Height = 6 });
        }

        private static TextBlock CreateHeading(string text)
        {
            var heading = new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 2, 0, 5)
            };
            SetTextBrush(heading);
            return heading;
        }

        private static string GetFriendlyTypeName(Type? type)
        {
            if (type == null || type == typeof(object))
                return string.Empty;
            if (!type.IsGenericType)
                return type.Name;

            string genericName = type.Name.Split('`')[0];
            return $"{genericName}<{string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName))}>";
        }

        private static string Localize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : Lang.GetOrDefault(value);
        }

        private static void SetTextBrush(TextBlock textBlock)
        {
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
        }
    }
}

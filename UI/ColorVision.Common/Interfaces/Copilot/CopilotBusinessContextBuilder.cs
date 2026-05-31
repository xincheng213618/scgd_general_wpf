using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ColorVision.UI
{
    public sealed class CopilotContextProperty
    {
        public string Name { get; init; } = string.Empty;

        public string Value { get; init; } = string.Empty;
    }

    public sealed class CopilotImageContextSnapshot
    {
        public string SourceId { get; init; } = "image-editor";

        public string Title { get; init; } = "Current image editor image";

        public string ImagePath { get; init; } = string.Empty;

        public string FileName { get; init; } = string.Empty;

        public string FileSize { get; init; } = string.Empty;

        public string ImageSize { get; init; } = string.Empty;

        public string PixelFormat { get; init; } = string.Empty;

        public string Channel { get; init; } = string.Empty;

        public string Depth { get; init; } = string.Empty;

        public string Dpi { get; init; } = string.Empty;

        public IReadOnlyList<CopilotContextProperty> Metadata { get; init; } = Array.Empty<CopilotContextProperty>();

        public IReadOnlyList<string> SelectedRegions { get; init; } = Array.Empty<string>();

        public int AnnotationCount { get; init; }

        public IReadOnlyList<string> AnnotationSummaries { get; init; } = Array.Empty<string>();
    }

    public sealed class CopilotFlowNodeContextSnapshot
    {
        public string Title { get; init; } = string.Empty;

        public string NodeName { get; init; } = string.Empty;

        public string NodeType { get; init; } = string.Empty;

        public string DeviceCode { get; init; } = string.Empty;

        public string NodeId { get; init; } = string.Empty;

        public string Position { get; init; } = string.Empty;

        public string Mark { get; init; } = string.Empty;

        public bool IsActive { get; init; }

        public bool IsSelected { get; init; }

        public IReadOnlyList<string> Inputs { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Outputs { get; init; } = Array.Empty<string>();

        public IReadOnlyList<CopilotContextProperty> Parameters { get; init; } = Array.Empty<CopilotContextProperty>();
    }

    public sealed class CopilotFlowContextSnapshot
    {
        public string SourceId { get; init; } = "flow";

        public string FlowName { get; init; } = string.Empty;

        public string TemplateName { get; init; } = string.Empty;

        public string TemplateId { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public bool IsRunning { get; init; }

        public string BatchSerialNumber { get; init; } = string.Empty;

        public string BatchStatus { get; init; } = string.Empty;

        public string BatchResult { get; init; } = string.Empty;

        public string BatchProgress { get; init; } = string.Empty;

        public string LastNodeSummary { get; init; } = string.Empty;

        public string RecentRunMessage { get; init; } = string.Empty;

        public string RecentFailureSummary { get; init; } = string.Empty;

        public IReadOnlyList<CopilotFlowNodeContextSnapshot> Nodes { get; init; } = Array.Empty<CopilotFlowNodeContextSnapshot>();
    }

    public sealed class CopilotDeviceContextSnapshot
    {
        public string SourceId { get; init; } = "device";

        public string Title { get; init; } = "Device service";

        public string ServiceName { get; init; } = string.Empty;

        public string ServiceCode { get; init; } = string.Empty;

        public string ServiceType { get; init; } = string.Empty;

        public string DeviceStatus { get; init; } = string.Empty;

        public string IsAlive { get; init; } = string.Empty;

        public string LastAliveTime { get; init; } = string.Empty;

        public string HeartbeatTime { get; init; } = string.Empty;

        public string SendTopic { get; init; } = string.Empty;

        public string SubscribeTopic { get; init; } = string.Empty;

        public IReadOnlyList<CopilotContextProperty> RuntimeProperties { get; init; } = Array.Empty<CopilotContextProperty>();

        public IReadOnlyList<CopilotContextProperty> ConfigProperties { get; init; } = Array.Empty<CopilotContextProperty>();

        public string RecentLogSummary { get; init; } = string.Empty;

        public string RecentLogContent { get; init; } = string.Empty;
    }

    public static class CopilotBusinessContextBuilder
    {
        private const int MaxContextChars = 16000;
        private const int MaxProperties = 60;
        private const int MaxListItems = 30;

        private static readonly Regex SensitiveNameRegex = new(
            "(password|passwd|pwd|secret|token|api[_-]?key|access[_-]?key|private[_-]?key|appsecret|servicetoken|license|lincense|sn)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SensitiveInlineRegex = new(
            "(?<name>password|passwd|pwd|secret|token|api[_-]?key|access[_-]?key|private[_-]?key|appsecret|servicetoken|license|lincense|sn)\\s*[:=]\\s*(?<value>[^,;\\s]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static CopilotContextItem BuildImageContextItem(CopilotImageContextSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var summaryParts = new[]
            {
                EmptyToNull(snapshot.FileName),
                EmptyToNull(snapshot.ImageSize),
                snapshot.AnnotationCount > 0 ? $"annotations {snapshot.AnnotationCount}" : null,
                snapshot.SelectedRegions.Count > 0 ? $"selected regions {snapshot.SelectedRegions.Count}" : null,
            }.Where(part => !string.IsNullOrWhiteSpace(part));

            var builder = new StringBuilder();
            builder.AppendLine("Surface: Image editor");
            builder.AppendLine("Note: This context contains structured image metadata, selected regions, and annotation summaries only. It does not contain image pixels.");
            AppendKeyValue(builder, "Image path", snapshot.ImagePath);
            AppendKeyValue(builder, "File name", snapshot.FileName);
            AppendKeyValue(builder, "File size", snapshot.FileSize);
            AppendKeyValue(builder, "Image size", snapshot.ImageSize);
            AppendKeyValue(builder, "Pixel format", snapshot.PixelFormat);
            AppendKeyValue(builder, "Channel", snapshot.Channel);
            AppendKeyValue(builder, "Depth", snapshot.Depth);
            AppendKeyValue(builder, "DPI", snapshot.Dpi);

            AppendProperties(builder, "Metadata", snapshot.Metadata, maskSensitiveValues: false);
            AppendList(builder, "Selected regions / ROI", snapshot.SelectedRegions);
            AppendList(builder, "Annotations / result summaries", snapshot.AnnotationSummaries);

            return new CopilotContextItem
            {
                Id = BuildItemId(snapshot.SourceId, "image"),
                Title = string.IsNullOrWhiteSpace(snapshot.Title) ? "Current image editor image" : snapshot.Title,
                Summary = string.Join(" · ", summaryParts),
                Content = Truncate(builder.ToString().TrimEnd(), MaxContextChars),
            };
        }

        public static CopilotContextItem BuildFlowContextItem(CopilotFlowContextSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var flowName = FirstNonEmpty(snapshot.FlowName, snapshot.TemplateName, "Unnamed flow");
            var summaryParts = new[]
            {
                flowName,
                EmptyToNull(snapshot.Status),
                snapshot.IsRunning ? "running" : "not running",
                snapshot.Nodes.Count > 0 ? $"nodes {snapshot.Nodes.Count}" : null,
                EmptyToNull(snapshot.BatchStatus),
            }.Where(part => !string.IsNullOrWhiteSpace(part));

            var builder = new StringBuilder();
            builder.AppendLine("Surface: Flow editor / runner");
            builder.AppendLine("Note: This context reads the current flow structure and recent run state only. It never starts, stops, or mutates a flow.");
            AppendKeyValue(builder, "Flow name", flowName);
            AppendKeyValue(builder, "Template name", snapshot.TemplateName);
            AppendKeyValue(builder, "Template id", snapshot.TemplateId);
            AppendKeyValue(builder, "Run status", snapshot.Status);
            AppendKeyValue(builder, "Is running", snapshot.IsRunning ? "yes" : "no");
            AppendKeyValue(builder, "Batch serial number", snapshot.BatchSerialNumber);
            AppendKeyValue(builder, "Batch status", snapshot.BatchStatus);
            AppendKeyValue(builder, "Batch progress", snapshot.BatchProgress);
            AppendKeyValue(builder, "Batch result", snapshot.BatchResult);
            AppendKeyValue(builder, "Last node", snapshot.LastNodeSummary);
            AppendKeyValue(builder, "Recent failure summary", snapshot.RecentFailureSummary);
            AppendKeyValue(builder, "Recent run message", snapshot.RecentRunMessage);

            if (snapshot.Nodes.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Node summary:");
                foreach (var node in snapshot.Nodes.Take(MaxListItems))
                {
                    var label = FirstNonEmpty(node.Title, node.NodeName, node.NodeType, node.NodeId, "Unnamed node");
                    builder.Append("- ").Append(label);
                    if (!string.IsNullOrWhiteSpace(node.NodeType))
                        builder.Append(" / ").Append(node.NodeType);
                    if (!string.IsNullOrWhiteSpace(node.DeviceCode))
                        builder.Append(" / device ").Append(node.DeviceCode);
                    if (node.IsActive)
                        builder.Append(" / active");
                    if (node.IsSelected)
                        builder.Append(" / selected");
                    builder.AppendLine();

                    AppendIndentedKeyValue(builder, "NodeName", node.NodeName);
                    AppendIndentedKeyValue(builder, "NodeID", node.NodeId);
                    AppendIndentedKeyValue(builder, "Position", node.Position);
                    AppendIndentedKeyValue(builder, "Mark", node.Mark);
                    AppendIndentedList(builder, "Inputs", node.Inputs);
                    AppendIndentedList(builder, "Outputs", node.Outputs);
                    AppendIndentedProperties(builder, "Parameters", node.Parameters);
                }
            }

            return new CopilotContextItem
            {
                Id = BuildItemId(snapshot.SourceId, "flow"),
                Title = $"Flow context · {flowName}",
                Summary = string.Join(" · ", summaryParts),
                Content = Truncate(builder.ToString().TrimEnd(), MaxContextChars),
            };
        }

        public static CopilotContextItem BuildDeviceContextItem(CopilotDeviceContextSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var serviceName = FirstNonEmpty(snapshot.ServiceName, snapshot.ServiceCode, "Unnamed device");
            var summaryParts = new[]
            {
                serviceName,
                EmptyToNull(snapshot.ServiceType),
                EmptyToNull(snapshot.DeviceStatus),
                EmptyToNull(snapshot.IsAlive),
            }.Where(part => !string.IsNullOrWhiteSpace(part));

            var builder = new StringBuilder();
            builder.AppendLine("Surface: Device / service panel");
            builder.AppendLine("Note: This context contains device status, configuration summaries, and recent log clues only. Sensitive fields are redacted.");
            AppendKeyValue(builder, "Device name", serviceName);
            AppendKeyValue(builder, "Device code", snapshot.ServiceCode);
            AppendKeyValue(builder, "Service type", snapshot.ServiceType);
            AppendKeyValue(builder, "Device status", snapshot.DeviceStatus);
            AppendKeyValue(builder, "Heartbeat status", snapshot.IsAlive);
            AppendKeyValue(builder, "Last heartbeat", snapshot.LastAliveTime);
            AppendKeyValue(builder, "Heartbeat interval", snapshot.HeartbeatTime);
            AppendKeyValue(builder, "Send topic", MaskIfSensitive("SendTopic", snapshot.SendTopic));
            AppendKeyValue(builder, "Subscribe topic", MaskIfSensitive("SubscribeTopic", snapshot.SubscribeTopic));

            AppendProperties(builder, "Runtime state", snapshot.RuntimeProperties, maskSensitiveValues: true);
            AppendProperties(builder, "Configuration summary", snapshot.ConfigProperties, maskSensitiveValues: true);
            AppendKeyValue(builder, "Recent log summary", snapshot.RecentLogSummary);
            if (!string.IsNullOrWhiteSpace(snapshot.RecentLogContent))
            {
                builder.AppendLine();
                builder.AppendLine("Recent log excerpt:");
                builder.AppendLine(MaskSensitiveText(Truncate(snapshot.RecentLogContent, 6000)));
            }

            return new CopilotContextItem
            {
                Id = BuildItemId(snapshot.SourceId, "device"),
                Title = string.IsNullOrWhiteSpace(snapshot.Title) ? $"Device service · {serviceName}" : snapshot.Title,
                Summary = string.Join(" · ", summaryParts),
                Content = Truncate(builder.ToString().TrimEnd(), MaxContextChars),
            };
        }

        public static string MaskIfSensitive(string name, string? value)
        {
            var text = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return IsSensitiveName(name) ? "<redacted>" : MaskSensitiveText(text);
        }

        public static string MaskSensitiveText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return SensitiveInlineRegex.Replace(text, match => $"{match.Groups["name"].Value}=<redacted>");
        }

        private static void AppendProperties(StringBuilder builder, string title, IReadOnlyList<CopilotContextProperty> properties, bool maskSensitiveValues)
        {
            if (properties == null || properties.Count == 0)
                return;

            builder.AppendLine();
            builder.AppendLine(title + ":");
            foreach (var item in properties.Where(item => item != null).Take(MaxProperties))
            {
                var name = item.Name ?? string.Empty;
                var value = maskSensitiveValues ? MaskIfSensitive(name, item.Value) : item.Value;
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(value))
                    continue;

                builder.Append("- ").Append(name).Append(": ").AppendLine(value ?? string.Empty);
            }
        }

        private static void AppendIndentedProperties(StringBuilder builder, string title, IReadOnlyList<CopilotContextProperty> properties)
        {
            if (properties == null || properties.Count == 0)
                return;

            builder.Append("  ").Append(title).Append(": ");
            builder.AppendLine(string.Join("; ", properties.Take(12).Select(item => $"{item.Name}={MaskIfSensitive(item.Name, item.Value)}")));
        }

        private static void AppendList(StringBuilder builder, string title, IReadOnlyList<string> items)
        {
            if (items == null || items.Count == 0)
                return;

            builder.AppendLine();
            builder.AppendLine(title + ":");
            foreach (var item in items.Where(item => !string.IsNullOrWhiteSpace(item)).Take(MaxListItems))
                builder.Append("- ").AppendLine(item);
        }

        private static void AppendIndentedList(StringBuilder builder, string title, IReadOnlyList<string> items)
        {
            if (items == null || items.Count == 0)
                return;

            builder.Append("  ").Append(title).Append(": ").AppendLine(string.Join(", ", items.Take(12)));
        }

        private static void AppendKeyValue(StringBuilder builder, string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            builder.Append(name).Append(": ").AppendLine(value.Trim());
        }

        private static void AppendIndentedKeyValue(StringBuilder builder, string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            builder.Append("  ").Append(name).Append(": ").AppendLine(value.Trim());
        }

        private static bool IsSensitiveName(string? name)
        {
            return !string.IsNullOrWhiteSpace(name) && SensitiveNameRegex.IsMatch(name);
        }

        private static string BuildItemId(string sourceId, string suffix)
        {
            var source = string.IsNullOrWhiteSpace(sourceId) ? "copilot-context" : sourceId.Trim();
            return source.EndsWith(":" + suffix, StringComparison.OrdinalIgnoreCase)
                ? source
                : $"{source}:{suffix}";
        }

        private static string? EmptyToNull(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static string Truncate(string value, int maxChars)
        {
            var content = value ?? string.Empty;
            if (content.Length <= maxChars)
                return content;

            return content[..maxChars] + Environment.NewLine + $"...<content truncated; kept the first {maxChars} characters.>";
        }
    }
}
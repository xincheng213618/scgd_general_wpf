#pragma warning disable CA1822,CA1826,CA1859,CA1861
using ColorVision.Engine.FlowProcessing.Integration;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        private static string NormalizeActionName(string? action)
        {
            return (action ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        }

        private static bool IsForbiddenFlowExecutionAction(string action)
        {
            return action is "start" or "stop" or "run" or "rerun" or "execute" or "start_flow" or "stop_flow" or "run_flow" or "execute_flow";
        }

        private static CopilotFlowNodeContextSnapshot? FindFlowNode(CopilotFlowContextSnapshot snapshot, string nodeQuery)
        {
            if (string.IsNullOrWhiteSpace(nodeQuery))
                return snapshot.Nodes.FirstOrDefault(node => node.IsSelected) ?? snapshot.Nodes.FirstOrDefault();

            var query = nodeQuery.Trim();
            return snapshot.Nodes.FirstOrDefault(node =>
                    string.Equals(node.NodeId, query, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(node.NodeName, query, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(node.Title, query, StringComparison.OrdinalIgnoreCase))
                ?? snapshot.Nodes.FirstOrDefault(node =>
                    node.NodeId.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || node.NodeName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || node.Title.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        private static string FormatLiveContext(CopilotLiveContext liveContext)
        {
            var builder = new StringBuilder();
            builder.AppendLine("ColorVision live context");
            builder.AppendLine($"Source id: {EmptyLabel(liveContext.SourceId)}");
            builder.AppendLine($"Title: {EmptyLabel(liveContext.Title)}");
            builder.AppendLine($"Summary: {EmptyLabel(liveContext.Summary)}");
            builder.AppendLine($"Snapshot items: {liveContext.SnapshotItems.Count}");

            foreach (var item in liveContext.SnapshotItems)
            {
                builder.AppendLine();
                builder.AppendLine($"## {EmptyLabel(item.Title)}");
                if (!string.IsNullOrWhiteSpace(item.Summary))
                    builder.AppendLine($"Summary: {item.Summary}");
                if (!string.IsNullOrWhiteSpace(item.Content))
                    builder.AppendLine(RedactForDisplay(item.Content.Trim()));
            }

            return builder.ToString().TrimEnd();
        }

        private static string FormatTemplateLiveContext(CopilotLiveContext liveContext)
        {
            var builder = new StringBuilder();
            builder.AppendLine("ColorVision active template context");
            builder.AppendLine($"Source id: {EmptyLabel(liveContext.SourceId)}");
            builder.AppendLine($"Title: {EmptyLabel(liveContext.Title)}");
            builder.AppendLine($"Summary: {EmptyLabel(liveContext.Summary)}");
            builder.AppendLine($"Snapshot items: {liveContext.SnapshotItems.Count}");

            foreach (var item in liveContext.SnapshotItems)
            {
                builder.AppendLine();
                builder.AppendLine($"## {EmptyLabel(item.Title)}");
                if (!string.IsNullOrWhiteSpace(item.Summary))
                    builder.AppendLine($"Summary: {item.Summary}");

                AppendTemplateMetadata(builder, item.Content);

                if (!string.IsNullOrWhiteSpace(item.Content))
                {
                    builder.AppendLine();
                    builder.AppendLine("Snapshot content:");
                    builder.AppendLine(RedactForDisplay(TrimLong(item.Content.Trim(), 12000)));
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendTemplateMetadata(StringBuilder builder, string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return;

            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            AppendFirstLineWithPrefix(builder, lines, "Surface:");
            AppendFirstLineWithPrefix(builder, lines, "Template name:");
            AppendFirstLineWithPrefix(builder, lines, "Current selection:");
            AppendFirstLineWithPrefix(builder, lines, "Window title:");
            AppendFirstLineWithPrefix(builder, lines, "Editor mode:");
            AppendFirstLineWithPrefix(builder, lines, "Unsaved changes:");
            AppendFirstLineWithPrefix(builder, lines, "JSON validation:");
            AppendFirstLineWithPrefix(builder, lines, "JSON line count:");

            var json = ExtractFencedJson(content);
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    builder.AppendLine($"Template JSON root: {document.RootElement.ValueKind}");
                    return;
                }

                var properties = document.RootElement.EnumerateObject().Take(40).ToArray();
                builder.AppendLine($"Template JSON root: object");
                builder.AppendLine($"Template JSON top-level keys: {string.Join(", ", properties.Select(property => property.Name))}");

                var templateType = FirstJsonScalar(document.RootElement, "$type", "Type", "TemplateType", "ParamType", "ModelType");
                if (!string.IsNullOrWhiteSpace(templateType))
                    builder.AppendLine($"Template type: {TrimLong(templateType, 160)}");

                var templateName = FirstJsonScalar(document.RootElement, "Name", "TemplateName", "Key", "Code");
                if (!string.IsNullOrWhiteSpace(templateName))
                    builder.AppendLine($"Template name from JSON: {TrimLong(templateName, 160)}");

                var keyParameters = properties
                    .Where(property => property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                    .Where(property => !IsSensitiveDisplayKey(property.Name))
                    .Take(20)
                    .Select(property => $"{property.Name}={TrimLong(property.Value.ToString(), 120)}")
                    .ToArray();
                if (keyParameters.Length > 0)
                    builder.AppendLine($"Key parameter summary: {string.Join(", ", keyParameters)}");

                foreach (var key in new[] { "Id", "ID", "Name", "Key", "Type", "TemplateType", "Code" })
                {
                    if (document.RootElement.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                        builder.AppendLine($"Template JSON {key}: {TrimLong(value.ToString(), 160)}");
                }
            }
            catch (JsonException ex)
            {
                builder.AppendLine($"Template JSON parse: failed ({ex.Message})");
            }
        }

        private static void AppendFirstLineWithPrefix(StringBuilder builder, IReadOnlyList<string> lines, string prefix)
        {
            var line = lines.FirstOrDefault(item => item.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(line))
                builder.AppendLine(line.Trim());
        }

        private static string ExtractFencedJson(string content)
        {
            const string fence = "```";
            var jsonFenceStart = content.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (jsonFenceStart < 0)
                return string.Empty;

            var jsonStart = content.IndexOf('\n', jsonFenceStart);
            if (jsonStart < 0)
                return string.Empty;

            var jsonEnd = content.IndexOf(fence, jsonStart + 1, StringComparison.Ordinal);
            if (jsonEnd < 0)
                return string.Empty;

            return content[(jsonStart + 1)..jsonEnd].Trim();
        }

        private static string FirstJsonScalar(JsonElement element, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (element.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                    return value.ToString();
            }

            return string.Empty;
        }

        private static string RedactForDisplay(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var sensitiveTerms = SensitiveDisplayTerms;
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (!sensitiveTerms.Any(term => line.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var separatorIndex = line.IndexOf(':');
                if (separatorIndex < 0)
                    separatorIndex = line.IndexOf('=');

                lines[index] = separatorIndex >= 0
                    ? line[..(separatorIndex + 1)] + " <redacted>"
                    : "<redacted>";
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static readonly string[] SensitiveDisplayTerms =
        {
            "password",
            "passwd",
            "pwd",
            "secret",
            "token",
            "api_key",
            "apikey",
            "access_key",
            "private_key",
            "authorization",
            "bearer",
        };

        private static bool IsSensitiveDisplayKey(string? key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && SensitiveDisplayTerms.Any(term => key.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static CopilotMcpToolCallResult ToMcpResult(CopilotCapabilityResult result, string errorCode)
        {
            var text = string.Join(Environment.NewLine, new[]
            {
                result.Summary,
                result.Content,
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

            if (result.Success)
                return CopilotMcpToolCallResult.Ok(text);

            return CopilotMcpToolCallResult.Fail(
                errorCode,
                string.IsNullOrWhiteSpace(result.ErrorMessage) ? text : result.ErrorMessage,
                result.FailureKind);
        }

        private static string FormatFlowSnapshot(CopilotFlowContextSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("ColorVision flow summary");
            builder.AppendLine($"Flow name: {EmptyLabel(snapshot.FlowName)}");
            builder.AppendLine($"Template name: {EmptyLabel(snapshot.TemplateName)}");
            builder.AppendLine($"Template id: {EmptyLabel(snapshot.TemplateId)}");
            builder.AppendLine($"Status: {EmptyLabel(snapshot.Status)}");
            builder.AppendLine($"Is running: {snapshot.IsRunning}");
            builder.AppendLine($"Batch serial number: {EmptyLabel(snapshot.BatchSerialNumber)}");
            builder.AppendLine($"Batch status: {EmptyLabel(snapshot.BatchStatus)}");
            builder.AppendLine($"Batch result: {EmptyLabel(snapshot.BatchResult)}");
            builder.AppendLine($"Batch progress: {EmptyLabel(snapshot.BatchProgress)}");
            builder.AppendLine($"Last node: {EmptyLabel(snapshot.LastNodeSummary)}");
            builder.AppendLine($"Recent failure summary: {EmptyLabel(snapshot.RecentFailureSummary)}");
            builder.AppendLine($"Node count: {snapshot.Nodes.Count}");
            var selectedNodes = snapshot.Nodes.Where(node => node.IsSelected).ToArray();
            builder.AppendLine($"Selected node count: {selectedNodes.Length}");
            if (selectedNodes.Length > 0)
                builder.AppendLine($"Selected nodes: {string.Join(", ", selectedNodes.Select(node => EmptyLabel(FirstNonEmpty(node.Title, node.NodeName, node.NodeId))))}");

            if (!string.IsNullOrWhiteSpace(snapshot.RecentRunMessage))
            {
                builder.AppendLine();
                builder.AppendLine("Recent run message:");
                builder.AppendLine(TrimLong(snapshot.RecentRunMessage, 4000));
            }

            foreach (var node in snapshot.Nodes.Take(60))
            {
                builder.AppendLine();
                builder.AppendLine($"Node: {EmptyLabel(node.Title)}");
                builder.AppendLine($"- Type: {EmptyLabel(node.NodeType)}");
                builder.AppendLine($"- Name: {EmptyLabel(node.NodeName)}");
                builder.AppendLine($"- Device code: {EmptyLabel(node.DeviceCode)}");
                builder.AppendLine($"- Node id: {EmptyLabel(node.NodeId)}");
                builder.AppendLine($"- Position: {EmptyLabel(node.Position)}");
                builder.AppendLine($"- Active: {node.IsActive}");
                builder.AppendLine($"- Selected: {node.IsSelected}");
                AppendList(builder, "- Inputs", node.Inputs);
                AppendList(builder, "- Outputs", node.Outputs);
                if (node.Parameters.Count > 0)
                    builder.AppendLine($"- Parameters: {RedactForDisplay(string.Join(", ", node.Parameters.Select(item => $"{item.Name}={item.Value}")))}");
                if (!string.IsNullOrWhiteSpace(node.Mark))
                    builder.AppendLine($"- Mark: {node.Mark}");
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendList(StringBuilder builder, string label, IReadOnlyList<string> values)
        {
            if (values.Count == 0)
                return;

            builder.Append(label).Append(": ").AppendLine(string.Join("; ", values));
        }

        private static IReadOnlyList<string> BuildFlowPreviewSuggestions(string action, CopilotFlowNodeContextSnapshot? matchedNode, CopilotFlowContextSnapshot snapshot)
        {
            return action switch
            {
                "select_node" => matchedNode == null
                    ? new[] { "Choose one of the listed node_id values and preview select_node again." }
                    : new[] { "Use open_node_property to inspect the matched node in ColorVision.", "Review get_flow_summary before changing any template parameters." },
                "open_node_property" => matchedNode == null
                    ? new[] { "Provide node_id or node_name for the node whose properties should be inspected." }
                    : new[] { "Open the node property panel in ColorVision for manual review.", "Use explain_node for a read-only parameter summary before editing templates." },
                "inspect_node_errors" => new[] { "Review node mark and recent failure summary.", "Use trace_recent_failure to correlate the last node and recent run message." },
                "explain_node" => new[] { "Compare the node parameters with the active template JSON.", "Use preview_template_patch for any proposed template change before applying it." },
                "trace_recent_failure" => string.IsNullOrWhiteSpace(snapshot.RecentFailureSummary)
                    ? new[] { "No recent failure summary is available; capture get_recent_log with an error query if needed." }
                    : new[] { "Inspect the matched or last node before editing parameters.", "Use get_diagnostic_bundle for a compact shareable diagnostic snapshot." },
                _ => new[] { "Use get_flow_summary for read-only flow context." },
            };
        }
    }
}

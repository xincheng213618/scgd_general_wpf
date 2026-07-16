using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot
{
    internal static class CopilotToolApprovalArgumentFormatter
    {
        private const int MaximumSummaryLength = 900;
        private const int MaximumValueLength = 240;
        private const int MaximumDepth = 4;
        private const int MaximumObjectProperties = 16;
        private const int MaximumArrayItems = 8;

        internal static string Create(CopilotAgentToolInput input)
        {
            input ??= CopilotAgentToolInput.Empty;
            var arguments = new Dictionary<string, object?>(input.Arguments, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(input.Query) && !arguments.ContainsKey("query"))
                arguments["query"] = input.Query.Trim();
            if (!string.IsNullOrWhiteSpace(input.Path) && !arguments.ContainsKey("path"))
                arguments["path"] = input.Path.Trim();
            if (input.StartLine.HasValue && !arguments.ContainsKey("startLine"))
                arguments["startLine"] = input.StartLine.Value;
            if (input.EndLine.HasValue && !arguments.ContainsKey("endLine"))
                arguments["endLine"] = input.EndLine.Value;
            if (arguments.Count == 0)
                return "(none)";

            JsonElement serialized;
            try
            {
                serialized = JsonSerializer.SerializeToElement(arguments);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Tool arguments cannot be represented safely for approval.", nameof(input), ex);
            }

            var builder = new StringBuilder();
            AppendValue(builder, serialized, null, 0);
            var summary = builder.ToString();
            return summary.Length <= MaximumSummaryLength ? summary : summary[..(MaximumSummaryLength - 3)] + "...";
        }

        private static void AppendValue(StringBuilder builder, JsonElement value, string? propertyName, int depth)
        {
            if (builder.Length >= MaximumSummaryLength)
                return;
            if (CopilotMcpAuditLogger.IsSensitiveArgumentName(propertyName))
            {
                builder.Append("\"<redacted>\"");
                return;
            }
            if (depth >= MaximumDepth && value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                builder.Append("\"<max-depth>\"");
                return;
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    builder.Append('{');
                    var propertyCount = 0;
                    foreach (var property in value.EnumerateObject())
                    {
                        if (propertyCount >= MaximumObjectProperties || builder.Length >= MaximumSummaryLength)
                        {
                            if (propertyCount > 0)
                                builder.Append(',');
                            builder.Append("\"...\":\"<truncated>\"");
                            break;
                        }
                        if (propertyCount++ > 0)
                            builder.Append(',');
                        builder.Append(JsonSerializer.Serialize(property.Name)).Append(':');
                        AppendValue(builder, property.Value, property.Name, depth + 1);
                    }
                    builder.Append('}');
                    break;
                case JsonValueKind.Array:
                    builder.Append('[');
                    var itemCount = 0;
                    foreach (var item in value.EnumerateArray())
                    {
                        if (itemCount >= MaximumArrayItems || builder.Length >= MaximumSummaryLength)
                        {
                            if (itemCount > 0)
                                builder.Append(',');
                            builder.Append("\"<truncated>\"");
                            break;
                        }
                        if (itemCount++ > 0)
                            builder.Append(',');
                        AppendValue(builder, item, null, depth + 1);
                    }
                    builder.Append(']');
                    break;
                case JsonValueKind.String:
                    var text = value.GetString() ?? string.Empty;
                    var redacted = CopilotMcpAuditLogger.RedactText(text);
                    if (!string.Equals(text, redacted, StringComparison.Ordinal) && redacted.Contains("<redacted>", StringComparison.Ordinal))
                        redacted = "<redacted>";
                    if (redacted.Length > MaximumValueLength)
                        redacted = redacted[..(MaximumValueLength - 3)] + "...";
                    builder.Append(JsonSerializer.Serialize(redacted));
                    break;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    builder.Append(value.GetRawText());
                    break;
                default:
                    builder.Append("\"<unsupported>\"");
                    break;
            }
        }
    }
}

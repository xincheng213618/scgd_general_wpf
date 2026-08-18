#pragma warning disable CA1822,CA1826,CA1859,CA1861
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        private static CopilotMcpToolDescriptor Tool(
            string name,
            string description,
            object inputSchema,
            string category,
            string riskLevel,
            string usageExample,
            CopilotToolIdempotency? idempotency = null,
            bool? destructiveHint = null,
            bool? openWorldHint = null) => new()
        {
            Name = name,
            Description = description,
            InputSchema = FreezeInputSchema(inputSchema),
            Category = category,
            RiskLevel = riskLevel,
            UsageExample = usageExample,
            Annotations = BuildToolAnnotations(
                riskLevel,
                idempotency,
                destructiveHint,
                openWorldHint),
        };

        private static IReadOnlyDictionary<string, object> BuildToolAnnotations(
            string riskLevel,
            CopilotToolIdempotency? idempotency,
            bool? destructiveHint,
            bool? openWorldHint)
        {
            var isReadOnly = string.Equals(riskLevel, "read-only", StringComparison.OrdinalIgnoreCase);
            var isIdempotent = idempotency.HasValue
                ? idempotency == CopilotToolIdempotency.Idempotent
                : isReadOnly;
            return new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
            {
                ["readOnlyHint"] = isReadOnly,
                ["destructiveHint"] = destructiveHint ?? !isReadOnly,
                ["idempotentHint"] = isIdempotent,
                ["openWorldHint"] = openWorldHint ?? false,
                ["riskLevel"] = riskLevel,
            });
        }

        private static JsonElement FreezeInputSchema(object inputSchema)
        {
            ArgumentNullException.ThrowIfNull(inputSchema);
            return inputSchema is JsonElement element
                ? element.Clone()
                : JsonSerializer.SerializeToElement(inputSchema);
        }

        private static object EmptySchema() => Schema(new Dictionary<string, object>());

        private static object Schema(Dictionary<string, object> properties, params string[] required)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required,
                ["additionalProperties"] = false,
            };
        }

        private static object StringProperty(string description) => new Dictionary<string, object>
        {
            ["type"] = "string",
            ["description"] = description,
        };

        private static object IntegerProperty(string description, int minimum, int maximum) => new Dictionary<string, object>
        {
            ["type"] = "integer",
            ["description"] = description,
            ["minimum"] = minimum,
            ["maximum"] = maximum,
        };

        private static object BooleanProperty(string description) => new Dictionary<string, object>
        {
            ["type"] = "boolean",
            ["description"] = description,
        };

        private static string NormalizeToolName(string? toolName)
        {
            return (toolName ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string NormalizeResourceUri(string? uri)
        {
            return (uri ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string FormatAuditEntries(IReadOnlyList<CopilotMcpAuditEntry> entries, string title)
        {
            var builder = new StringBuilder();
            builder.AppendLine(title);
            builder.AppendLine($"Entries: {entries.Count}");

            foreach (var entry in entries)
            {
                builder.AppendLine();
                builder.AppendLine($"- Timestamp UTC: {entry.TimestampUtc:O}");
                builder.AppendLine($"  Tool: {EmptyLabel(entry.ToolName)}");
                builder.AppendLine($"  Approval event: {!string.IsNullOrWhiteSpace(entry.ActionId)}");
                builder.AppendLine($"  Arguments: {EmptyLabel(entry.ArgumentSummary)}");
                builder.AppendLine($"  Success: {entry.Success}");
                builder.AppendLine($"  Duration ms: {entry.DurationMs}");
                builder.AppendLine($"  Error: {EmptyLabel(entry.ErrorMessage)}");
                builder.AppendLine($"  Caller/source: {EmptyLabel(entry.CallerSource)}");
                builder.AppendLine($"  Scope id: {EmptyLabel(entry.ScopeId)}");
                builder.AppendLine($"  Trace id: {EmptyLabel(entry.TraceId)}");
                builder.AppendLine($"  Run id: {EmptyLabel(entry.RunId)}");
            }

            return builder.ToString().TrimEnd();
        }

        private static string FormatAuditEntryOneLine(CopilotMcpAuditEntry? entry)
        {
            if (entry == null)
                return "(none)";

            var result = entry.Success ? "success" : "failed";
            var error = entry.Success || string.IsNullOrWhiteSpace(entry.ErrorMessage)
                ? string.Empty
                : $"; error={entry.ErrorMessage}";
            var approvalEvent = string.IsNullOrWhiteSpace(entry.ActionId)
                ? string.Empty
                : "; approval_event=true";
            return $"{entry.TimestampUtc:O}; tool={EmptyLabel(entry.ToolName)}; result={result}; duration_ms={entry.DurationMs}; caller={EmptyLabel(entry.CallerSource)}; scope={EmptyLabel(entry.ScopeId)}{approvalEvent}{error}";
        }

        private static bool IsRealFailureAuditEntry(CopilotMcpAuditEntry entry)
        {
            return !entry.Success && !IsApprovalFlowAuditEntry(entry);
        }

        private static bool IsApprovalFlowAuditEntry(CopilotMcpAuditEntry entry)
        {
            if (entry.Success)
                return false;

            var toolName = entry.ToolName ?? string.Empty;
            if (string.Equals(toolName, "action_rejected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "action_expired", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var error = entry.ErrorMessage ?? string.Empty;
            return error.Contains("confirmation_required", StringComparison.OrdinalIgnoreCase)
                || error.Contains("pending_user_confirmation", StringComparison.OrdinalIgnoreCase)
                || error.Contains("risk_level: confirmation-required", StringComparison.OrdinalIgnoreCase)
                || error.Contains("risk_level=confirmation-required", StringComparison.OrdinalIgnoreCase)
                || error.Contains("action_pending", StringComparison.OrdinalIgnoreCase)
                || error.Contains("action_not_approved", StringComparison.OrdinalIgnoreCase)
                || error.Contains("action_rejected", StringComparison.OrdinalIgnoreCase)
                || error.Contains("action_expired", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildArgumentSummary(IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            if (arguments == null || arguments.Count == 0)
                return "{}";

            return string.Join(", ", arguments.Select(pair => $"{pair.Key}={TrimLong(CopilotMcpAuditLogger.RedactArgument(pair.Key, pair.Value.ToString()), 160)}"));
        }

        private static string BuildAuditArgumentSummary(IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            if (arguments == null || arguments.Count == 0)
                return "{}";

            const int maximumFieldNames = 32;
            var fieldNames = arguments.Keys
                .OrderBy(name => name, StringComparer.Ordinal)
                .Take(maximumFieldNames)
                .Select(name => TrimLong(CopilotMcpAuditLogger.RedactText(name), 80));
            var omittedSuffix = arguments.Count > maximumFieldNames
                ? $", ... (+{arguments.Count - maximumFieldNames} fields)"
                : string.Empty;
            return $"fields={string.Join(", ", fieldNames)}{omittedSuffix}";
        }

        private static string BuildExactArgumentBinding(IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                if (arguments != null)
                {
                    foreach (var pair in arguments.OrderBy(item => item.Key, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(pair.Key);
                        WriteCanonicalJsonElement(writer, pair.Value);
                    }
                }
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteCanonicalJsonElement(Utf8JsonWriter writer, JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(property.Name);
                        WriteCanonicalJsonElement(writer, property.Value);
                    }
                    writer.WriteEndObject();
                    return;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in value.EnumerateArray())
                        WriteCanonicalJsonElement(writer, item);
                    writer.WriteEndArray();
                    return;
                case JsonValueKind.Undefined:
                    writer.WriteNullValue();
                    return;
                default:
                    value.WriteTo(writer);
                    return;
            }
        }

        private static string GetString(IReadOnlyDictionary<string, JsonElement>? arguments, params string[] names)
        {
            if (arguments == null)
                return string.Empty;

            foreach (var name in names)
            {
                if (!arguments.TryGetValue(name, out var value))
                    continue;

                return value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString() ?? string.Empty,
                    JsonValueKind.Number => value.ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => value.ToString(),
                };
            }

            return string.Empty;
        }

        private static int? GetInt(IReadOnlyDictionary<string, JsonElement>? arguments, string name)
        {
            if (arguments == null || !arguments.TryGetValue(name, out var value))
                return null;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                return number;

            return null;
        }

        private static long? GetLong(IReadOnlyDictionary<string, JsonElement>? arguments, string name)
        {
            if (arguments == null || !arguments.TryGetValue(name, out var value))
                return null;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                return number;

            return null;
        }

        private static bool? GetBool(IReadOnlyDictionary<string, JsonElement>? arguments, string name)
        {
            if (arguments == null || !arguments.TryGetValue(name, out var value))
                return null;

            if (value.ValueKind == JsonValueKind.True)
                return true;

            if (value.ValueKind == JsonValueKind.False)
                return false;

            if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
                return parsed;

            return null;
        }

        private static string EmptyLabel(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim();
        }

        private static string TrimLong(string? value, int maxLength)
        {
            var text = value ?? string.Empty;
            return text.Length <= maxLength ? text : text[..maxLength] + "...";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static T? SafeInvoke<T>(Func<T> provider)
        {
            try
            {
                return provider();
            }
            catch
            {
                return default;
            }
        }
    }
}

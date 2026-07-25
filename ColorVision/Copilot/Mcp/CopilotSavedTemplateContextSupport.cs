using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.Copilot.Mcp
{
    internal static class CopilotSavedTemplateContextSupport
    {
        private const int MaximumRawSnapshotCharacters = 200_000;
        private const int MaximumSnapshotJsonCharacters = 12_000;
        private const int MaximumMetadataCharacters = 240;
        private static readonly SnapshotLimits[] SnapshotLimitLevels =
        {
            new(300, 100, 1_200, 12),
            new(160, 60, 400, 10),
            new(80, 30, 180, 8),
            new(50, 20, 100, 7),
        };

        public static CopilotMcpToolCallResult Read(string? templateCode, string? templateName)
        {
            var normalizedCode = (templateCode ?? string.Empty).Trim();
            var normalizedName = (templateName ?? string.Empty).Trim();
            if (normalizedCode.Length == 0)
                return CopilotMcpToolCallResult.Fail("missing_template_code", "get_saved_template_context requires template_code from the selected saved-template reference.");
            if (normalizedName.Length == 0)
                return CopilotMcpToolCallResult.Fail("missing_template_name", "get_saved_template_context requires template_name from the selected saved-template reference.");

            try
            {
                if (!TryResolveTemplate(normalizedCode, out var template, out var resolvedCode, out var resolveError))
                    return CopilotMcpToolCallResult.Fail("saved_template_type_not_found", resolveError);

                var names = template.GetTemplateNames()
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.Trim())
                    .ToArray();
                var storedName = names.FirstOrDefault(name =>
                    string.Equals(name, normalizedName, StringComparison.OrdinalIgnoreCase));
                if (storedName == null)
                {
                    return CopilotMcpToolCallResult.Fail(
                        "saved_template_not_found",
                        $"No loaded saved template named '{SanitizeMetadata(normalizedName)}' exists for template code '{SanitizeMetadata(resolvedCode)}'.");
                }

                var index = TryGetTemplateIndex(template, storedName, names);
                if (index < 0)
                {
                    return CopilotMcpToolCallResult.Fail(
                        "saved_template_not_found",
                        $"The loaded saved template '{SanitizeMetadata(storedName)}' could not be resolved to a template value.");
                }

                var value = template.GetParamValue(index);
                if (value == null)
                    return CopilotMcpToolCallResult.Fail("saved_template_value_unavailable", "The selected saved template has no loaded value.");

                return BuildSnapshot(
                    resolvedCode,
                    storedName,
                    template.Title,
                    template.TemplateDicId,
                    template.GetType(),
                    value);
            }
            catch (Exception ex)
            {
                return CopilotMcpToolCallResult.Fail(
                    "saved_template_read_failed",
                    "The saved template could not be read: " + CopilotMcpAuditLogger.RedactText(ex.Message));
            }
        }

        internal static CopilotMcpToolCallResult BuildSnapshot(
            string templateCode,
            string templateName,
            string? templateTitle,
            int templateDictionaryId,
            Type templateRuntimeType,
            object templateValue)
        {
            ArgumentNullException.ThrowIfNull(templateRuntimeType);
            ArgumentNullException.ThrowIfNull(templateValue);

            if (!TryGetRawSnapshot(templateValue, out var rawSnapshot, out var serializationWarning, out var snapshotError))
                return snapshotError;

            try
            {
                using var document = JsonDocument.Parse(
                    rawSnapshot,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip,
                        MaxDepth = 64,
                    });
                var snapshotJson = CreateBoundedRedactedJson(
                    document.RootElement,
                    out var snapshotTruncated,
                    out var redactedFieldCount);
                var builder = new StringBuilder();
                builder.AppendLine("ColorVision saved template context");
                builder.AppendLine("Mode: read-only in-memory snapshot");
                builder.AppendLine("Would modify: False");
                builder.AppendLine("Would save: False");
                builder.AppendLine($"Template code: {SanitizeMetadata(templateCode)}");
                builder.AppendLine($"Template name: {SanitizeMetadata(templateName)}");
                builder.AppendLine($"Template title: {SanitizeMetadata(templateTitle)}");
                builder.AppendLine($"Template dictionary id: {templateDictionaryId.ToString(CultureInfo.InvariantCulture)}");
                builder.AppendLine($"Template runtime type: {SanitizeMetadata(templateRuntimeType.FullName)}");
                builder.AppendLine($"Value runtime type: {SanitizeMetadata(templateValue.GetType().FullName)}");
                builder.AppendLine($"Snapshot truncated: {snapshotTruncated}");
                builder.AppendLine($"Sensitive fields redacted: {redactedFieldCount.ToString(CultureInfo.InvariantCulture)}");
                if (!string.IsNullOrWhiteSpace(serializationWarning))
                    builder.AppendLine($"Snapshot warning: {SanitizeMetadata(serializationWarning)}");
                builder.AppendLine();
                builder.AppendLine("Snapshot JSON:");
                builder.AppendLine("```json");
                builder.AppendLine(snapshotJson);
                builder.AppendLine("```");
                builder.AppendLine("This snapshot came from the already loaded template collection. No database query, mutation, or save was performed.");
                return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
            }
            catch (JsonException ex)
            {
                return CopilotMcpToolCallResult.Fail(
                    "invalid_saved_template_snapshot",
                    "The loaded saved template did not produce valid JSON: " + CopilotMcpAuditLogger.RedactText(ex.Message));
            }
        }

        private static bool TryResolveTemplate(
            string templateCode,
            out ITemplate template,
            out string resolvedCode,
            out string error)
        {
            var entries = TemplateControl.ITemplateNames
                .Where(pair => pair.Value != null)
                .ToArray();
            var matches = DistinctTemplates(entries.Where(pair =>
                string.Equals(pair.Key, templateCode, StringComparison.OrdinalIgnoreCase)));
            if (matches.Count == 0)
            {
                matches = DistinctTemplates(entries.Where(pair =>
                    string.Equals(pair.Value.Code, templateCode, StringComparison.OrdinalIgnoreCase)));
            }
            if (matches.Count == 0)
            {
                matches = DistinctTemplates(entries.Where(pair =>
                    string.Equals(pair.Value.Name, templateCode, StringComparison.OrdinalIgnoreCase)));
            }

            if (matches.Count == 1)
            {
                template = matches[0].Template;
                resolvedCode = FirstNonEmpty(template.Code, matches[0].Key, template.Name, templateCode);
                error = string.Empty;
                return true;
            }

            template = null!;
            resolvedCode = string.Empty;
            error = matches.Count == 0
                ? $"No loaded template type matches code '{SanitizeMetadata(templateCode)}'."
                : $"Template code '{SanitizeMetadata(templateCode)}' is ambiguous across {matches.Count.ToString(CultureInfo.InvariantCulture)} loaded template types.";
            return false;
        }

        private static List<(string Key, ITemplate Template)> DistinctTemplates(
            IEnumerable<KeyValuePair<string, ITemplate>> entries)
        {
            var seen = new HashSet<ITemplate>(ReferenceEqualityComparer.Instance);
            var results = new List<(string Key, ITemplate Template)>();
            foreach (var entry in entries)
            {
                if (seen.Add(entry.Value))
                    results.Add((entry.Key, entry.Value));
            }
            return results;
        }

        private static int TryGetTemplateIndex(ITemplate template, string storedName, IReadOnlyList<string> names)
        {
            try
            {
                var index = template.GetTemplateIndex(storedName);
                if (index >= 0)
                    return index;
            }
            catch (NotImplementedException)
            {
            }
            catch (NotSupportedException)
            {
            }

            for (var index = 0; index < names.Count; index++)
            {
                if (string.Equals(names[index], storedName, StringComparison.OrdinalIgnoreCase))
                    return index;
            }
            return -1;
        }

        private static bool TryGetRawSnapshot(
            object templateValue,
            out string rawSnapshot,
            out string warning,
            out CopilotMcpToolCallResult error)
        {
            warning = string.Empty;
            error = null!;
            try
            {
                if (templateValue is IEditTemplateJson jsonTemplate
                    && !string.IsNullOrWhiteSpace(jsonTemplate.JsonValue))
                {
                    rawSnapshot = jsonTemplate.JsonValue.Trim();
                }
                else
                {
                    var hadSerializationErrors = false;
                    var settings = new Newtonsoft.Json.JsonSerializerSettings
                    {
                        MaxDepth = 12,
                        ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                    };
                    settings.Error += (_, args) =>
                    {
                        if (ContainsSnapshotLimitException(args.ErrorContext.Error))
                            return;

                        hadSerializationErrors = true;
                        args.ErrorContext.Handled = true;
                    };

                    using var textWriter = new BoundedStringWriter(MaximumRawSnapshotCharacters);
                    using var jsonWriter = new Newtonsoft.Json.JsonTextWriter(textWriter)
                    {
                        CloseOutput = false,
                        Formatting = Newtonsoft.Json.Formatting.Indented,
                    };
                    Newtonsoft.Json.JsonSerializer.Create(settings).Serialize(jsonWriter, templateValue);
                    jsonWriter.Flush();
                    rawSnapshot = textWriter.ToString();
                    if (hadSerializationErrors)
                        warning = "Some unreadable properties were omitted.";
                }
            }
            catch (Exception ex) when (ContainsSnapshotLimitException(ex))
            {
                rawSnapshot = string.Empty;
                error = CopilotMcpToolCallResult.Fail(
                    "invalid_saved_template_snapshot_size",
                    $"The loaded saved template exceeds the {MaximumRawSnapshotCharacters.ToString("N0", CultureInfo.InvariantCulture)}-character safety limit.");
                return false;
            }
            catch (Exception ex)
            {
                rawSnapshot = string.Empty;
                error = CopilotMcpToolCallResult.Fail(
                    "saved_template_snapshot_failed",
                    "The saved template value could not be serialized: " + CopilotMcpAuditLogger.RedactText(ex.Message));
                return false;
            }

            if (rawSnapshot.Length > MaximumRawSnapshotCharacters)
            {
                error = CopilotMcpToolCallResult.Fail(
                    "invalid_saved_template_snapshot_size",
                    $"The loaded saved template exceeds the {MaximumRawSnapshotCharacters.ToString("N0", CultureInfo.InvariantCulture)}-character safety limit.");
                rawSnapshot = string.Empty;
                return false;
            }
            if (string.IsNullOrWhiteSpace(rawSnapshot))
            {
                error = CopilotMcpToolCallResult.Fail("saved_template_value_unavailable", "The selected saved template produced an empty snapshot.");
                return false;
            }
            return true;
        }

        private static string CreateBoundedRedactedJson(
            JsonElement root,
            out bool truncated,
            out int redactedFieldCount)
        {
            foreach (var limits in SnapshotLimitLevels)
            {
                var state = new SnapshotBuildState(limits.MaximumNodes);
                var node = CreateBoundedNode(root, limits, state, depth: 0);
                var json = node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "null";
                if (json.Length <= MaximumSnapshotJsonCharacters)
                {
                    truncated = state.Truncated;
                    redactedFieldCount = state.RedactedFieldCount;
                    return json;
                }
            }

            truncated = true;
            redactedFieldCount = 0;
            return """
                {
                  "$copilot_truncated": true,
                  "message": "The redacted template snapshot exceeded the bounded output limit."
                }
                """;
        }

        private static JsonNode? CreateBoundedNode(
            JsonElement element,
            SnapshotLimits limits,
            SnapshotBuildState state,
            int depth)
        {
            if (depth > limits.MaximumDepth || !state.TryConsumeNode())
            {
                state.Truncated = true;
                return JsonValue.Create("<copilot:truncated>");
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    var result = new JsonObject();
                    var count = 0;
                    foreach (var property in element.EnumerateObject())
                    {
                        if (count >= limits.MaximumCollectionItems || state.RemainingNodes <= 0)
                        {
                            AddTruncationMarker(result);
                            state.Truncated = true;
                            break;
                        }

                        count++;
                        if (CopilotMcpAuditLogger.IsSensitiveArgumentName(property.Name))
                        {
                            result[property.Name] = "<redacted>";
                            state.RedactedFieldCount++;
                            continue;
                        }
                        result[property.Name] = CreateBoundedNode(property.Value, limits, state, depth + 1);
                    }
                    return result;
                }
                case JsonValueKind.Array:
                {
                    var result = new JsonArray();
                    var count = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        if (count >= limits.MaximumCollectionItems || state.RemainingNodes <= 0)
                        {
                            result.Add("<copilot:truncated>");
                            state.Truncated = true;
                            break;
                        }
                        count++;
                        result.Add(CreateBoundedNode(item, limits, state, depth + 1));
                    }
                    return result;
                }
                case JsonValueKind.String:
                {
                    var value = CopilotMcpAuditLogger.RedactText(element.GetString());
                    if (value.Length > limits.MaximumStringCharacters)
                    {
                        value = value[..limits.MaximumStringCharacters] + "…";
                        state.Truncated = true;
                    }
                    return JsonValue.Create(value);
                }
                case JsonValueKind.Number:
                    return JsonNode.Parse(element.GetRawText());
                case JsonValueKind.True:
                    return JsonValue.Create(true);
                case JsonValueKind.False:
                    return JsonValue.Create(false);
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                default:
                    return null;
            }
        }

        private static void AddTruncationMarker(JsonObject target)
        {
            var name = "$copilot_truncated";
            while (target.ContainsKey(name))
                name += "_";
            target[name] = true;
        }

        private static bool ContainsSnapshotLimitException(Exception exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is SnapshotLimitException)
                    return true;
            }
            return false;
        }

        private static string SanitizeMetadata(string? value)
        {
            var text = CopilotMcpAuditLogger.RedactText(value ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            if (text.Length == 0)
                return "(none)";
            return text.Length <= MaximumMetadataCharacters ? text : text[..MaximumMetadataCharacters] + "…";
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        }

        private readonly record struct SnapshotLimits(
            int MaximumNodes,
            int MaximumCollectionItems,
            int MaximumStringCharacters,
            int MaximumDepth);

        private sealed class SnapshotBuildState(int maximumNodes)
        {
            public int RemainingNodes { get; private set; } = maximumNodes;

            public int RedactedFieldCount { get; set; }

            public bool Truncated { get; set; }

            public bool TryConsumeNode()
            {
                if (RemainingNodes <= 0)
                    return false;

                RemainingNodes--;
                return true;
            }
        }

        private sealed class BoundedStringWriter(int maximumCharacters)
            : StringWriter(new StringBuilder(Math.Min(maximumCharacters, 4_096)), CultureInfo.InvariantCulture)
        {
            public override void Write(char value)
            {
                EnsureCapacity(1);
                base.Write(value);
            }

            public override void Write(char[] buffer, int index, int count)
            {
                EnsureCapacity(count);
                base.Write(buffer, index, count);
            }

            public override void Write(string? value)
            {
                EnsureCapacity(value?.Length ?? 0);
                base.Write(value);
            }

            private void EnsureCapacity(int additionalCharacters)
            {
                if (additionalCharacters > maximumCharacters - GetStringBuilder().Length)
                    throw new SnapshotLimitException();
            }
        }

        private sealed class SnapshotLimitException : Exception
        {
        }
    }
}

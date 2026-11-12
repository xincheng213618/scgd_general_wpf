#pragma warning disable CA1822,CA1826,CA1859,CA1861
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        private static void AppendTemplateFieldHints(StringBuilder builder, string templateJson, string evidence)
        {
            try
            {
                using var document = JsonDocument.Parse(templateJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    builder.AppendLine($"- Template JSON root: {document.RootElement.ValueKind}");
                    return;
                }

                var topLevelKeys = document.RootElement.EnumerateObject()
                    .Where(property => !IsSensitiveDisplayKey(property.Name))
                    .Take(40)
                    .Select(property => property.Name)
                    .ToArray();
                builder.AppendLine($"- Top-level keys: {string.Join(", ", topLevelKeys)}");

                var candidates = BuildTemplatePatchCandidateFields(document.RootElement, evidence, null).Take(12).ToArray();
                if (candidates.Length == 0)
                    builder.AppendLine("- Related adjustable fields: none detected from current evidence.");
                else
                {
                    builder.AppendLine("- Related adjustable fields:");
                    foreach (var candidate in candidates)
                        builder.AppendLine("  - " + candidate);
                }
            }
            catch (JsonException ex)
            {
                builder.AppendLine($"- Template JSON parse failed: {ex.Message}");
            }
        }

        private static IReadOnlyList<string> BuildTemplatePatchCandidateFields(JsonElement currentRoot, string intent, CopilotFlowNodeContextSnapshot? matchedNode)
        {
            var terms = BuildPatchIntentTerms(intent, matchedNode);
            var lines = new List<string>();
            foreach (var property in currentRoot.EnumerateObject())
            {
                if (IsSensitiveDisplayKey(property.Name) || !IsScalarJsonKind(property.Value.ValueKind))
                    continue;

                var isCommonField = IsCommonTemplateAdjustmentField(property.Name);
                var matchesIntent = terms.Any(term => property.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
                if (!isCommonField && !matchesIntent)
                    continue;

                var reason = matchesIntent ? "matches diagnosis/node wording" : "common adjustable template field";
                lines.Add($"{property.Name} ({property.Value.ValueKind}, current={DescribeJsonValue(property.Value)}) - {reason}");
            }

            if (matchedNode?.Parameters.Count > 0)
            {
                foreach (var parameter in matchedNode.Parameters)
                {
                    if (IsSensitiveDisplayKey(parameter.Name) || IsSensitiveDisplayKey(parameter.PropertyName)
                        || CopilotMcpAuditLogger.IsSensitiveArgumentName(parameter.Name) || CopilotMcpAuditLogger.IsSensitiveArgumentName(parameter.PropertyName))
                        continue;

                    var matchingTemplateField = currentRoot.EnumerateObject()
                        .FirstOrDefault(property => string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
                    var relation = matchingTemplateField.Value.ValueKind == JsonValueKind.Undefined
                        ? "node parameter; no same-name top-level template field detected"
                        : "node parameter; same-name top-level template field exists";
                    lines.Add($"{parameter.Name} (node parameter, value={TrimLong(RedactForDisplay(CopilotFlowParameterRedaction.RedactValue(parameter)), 120)}) - {relation}");
                }
            }

            if (lines.Count == 0)
            {
                foreach (var property in currentRoot.EnumerateObject().Where(property => !IsSensitiveDisplayKey(property.Name) && IsScalarJsonKind(property.Value.ValueKind)).Take(12))
                    lines.Add($"{property.Name} ({property.Value.ValueKind}, current={DescribeJsonValue(property.Value)}) - available scalar template field");
            }

            return lines.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static IReadOnlyList<string> BuildPatchIntentTerms(string intent, CopilotFlowNodeContextSnapshot? matchedNode)
        {
            var text = string.Join(" ", new[]
            {
                intent,
                matchedNode?.Title,
                matchedNode?.NodeName,
                matchedNode?.NodeType,
                matchedNode?.Mark,
                matchedNode == null ? string.Empty : string.Join(" ", matchedNode.Parameters.Select(parameter => parameter.Name)),
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

            var terms = text
                .Split(new[] { ' ', ',', ';', ':', '.', '/', '\\', '-', '_', '[', ']', '(', ')', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(term => term.Length >= 3)
                .Select(term => term.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (text.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                terms.AddRange(new[] { "timeout", "delay", "retry", "exposure" });
            if (text.Contains("camera", StringComparison.OrdinalIgnoreCase))
                terms.AddRange(new[] { "camera", "exposure", "gain", "roi", "width", "height" });
            if (text.Contains("threshold", StringComparison.OrdinalIgnoreCase) || text.Contains("ng", StringComparison.OrdinalIgnoreCase))
                terms.AddRange(new[] { "threshold", "limit", "min", "max" });

            return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static bool IsCommonTemplateAdjustmentField(string name)
        {
            return new[]
            {
                "exposure",
                "gain",
                "timeout",
                "delay",
                "retry",
                "threshold",
                "limit",
                "min",
                "max",
                "roi",
                "width",
                "height",
                "offset",
                "scale",
            }.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsScalarJsonKind(JsonValueKind kind)
        {
            return kind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False;
        }

        private static bool JsonKindsCompatible(JsonValueKind currentKind, JsonValueKind proposedKind)
        {
            if (currentKind == proposedKind)
                return true;

            return IsBooleanKind(currentKind) && IsBooleanKind(proposedKind);
        }

        private static bool IsBooleanKind(JsonValueKind kind)
        {
            return kind is JsonValueKind.True or JsonValueKind.False;
        }

        private static string BuildPreviewTemplatePatchPayload(string templateIdentifier, JsonElement proposedChanges)
        {
            var payload = new JsonObject
            {
                ["template_identifier"] = templateIdentifier.Trim(),
                ["proposed_changes"] = JsonNode.Parse(proposedChanges.GetRawText()),
            };

            return payload.ToJsonString(StructuredJsonOptions);
        }

        private static string EscapeForInlineJson(string? value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void AppendDiagnosticSection(StringBuilder builder, string title, string content)
        {
            builder.AppendLine();
            builder.AppendLine("## " + title);
            builder.AppendLine(string.IsNullOrWhiteSpace(content) ? "(empty)" : content.Trim());
        }

        private static string RedactForDiagnostics(string text)
        {
            return CopilotMcpAuditLogger.RedactText(RedactForDisplay(text));
        }

        private static string TruncateWithLimit(string text, int maxChars)
        {
            if (text.Length <= maxChars)
                return text;

            var suffix = $"{Environment.NewLine}...<diagnostic bundle truncated to max_chars={maxChars}>";
            if (suffix.Length >= maxChars)
                return text[..maxChars];

            return text[..(maxChars - suffix.Length)] + suffix;
        }
    }
}

#pragma warning disable CA1822,CA1826,CA1859,CA1861
using ColorVision.Engine.FlowProcessing.Integration;
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
        private static string BuildFailureEvidenceText(
            CopilotFlowContextSnapshot? snapshot,
            CopilotFlowNodeContextSnapshot? matchedNode,
            CopilotCapabilityResult logResult,
            CopilotLiveContext? liveContext,
            string templateJson)
        {
            var builder = new StringBuilder();
            if (snapshot != null)
            {
                builder.AppendLine(snapshot.FlowName);
                builder.AppendLine(snapshot.Status);
                builder.AppendLine(snapshot.BatchStatus);
                builder.AppendLine(snapshot.BatchResult);
                builder.AppendLine(snapshot.LastNodeSummary);
                builder.AppendLine(snapshot.RecentFailureSummary);
                builder.AppendLine(snapshot.RecentRunMessage);
            }

            if (matchedNode != null)
            {
                builder.AppendLine(matchedNode.Title);
                builder.AppendLine(matchedNode.NodeName);
                builder.AppendLine(matchedNode.NodeType);
                builder.AppendLine(matchedNode.DeviceCode);
                builder.AppendLine(matchedNode.Mark);
                foreach (var parameter in matchedNode.Parameters)
                    builder.AppendLine(parameter.Name + "=" + parameter.Value);
            }

            builder.AppendLine(liveContext?.Title);
            builder.AppendLine(liveContext?.Summary);
            builder.AppendLine(logResult.Summary);
            builder.AppendLine(logResult.Content);
            builder.AppendLine(templateJson);
            return RedactForDiagnostics(builder.ToString());
        }

        private static IReadOnlyList<string> BuildLikelyFailureCauses(string evidence)
        {
            var lower = (evidence ?? string.Empty).ToLowerInvariant();
            var causes = new List<string>();

            if (lower.Contains("timeout"))
                causes.Add("Timeout evidence is present. Check acquisition latency, trigger timing, exposure duration, retry/delay settings, and device connectivity.");

            if (lower.Contains("camera") || lower.Contains("image") || lower.Contains("acquire"))
                causes.Add("Camera/acquisition evidence is present. Compare the related node parameters with template fields such as Exposure, Gain, Timeout, ROI, Width, and Height.");

            if (lower.Contains("exposure") || lower.Contains("gain") || lower.Contains("brightness"))
                causes.Add("Image brightness or acquisition-parameter evidence is present. Treat exposure/gain changes as a template patch candidate, then preview the JSON diff first.");

            if (lower.Contains("threshold") || lower.Contains("limit") || lower.Contains("min") || lower.Contains("max") || lower.Contains("ng"))
                causes.Add("Threshold/limit evidence is present. Review min/max/threshold fields before proposing any template patch.");

            if (lower.Contains("template") || lower.Contains("json") || lower.Contains("parameter"))
                causes.Add("Template/parameter evidence is present. Use suggest_template_patch to turn the diagnosis into explicit proposed_changes, then preview_template_patch.");

            if (lower.Contains("mqtt") || lower.Contains("connect") || lower.Contains("socket") || lower.Contains("network"))
                causes.Add("Communication evidence is present. Prefer log and device-panel inspection before changing template parameters.");

            if (causes.Count == 0)
                causes.Add("No strong keyword pattern was detected. Inspect the matched node, recent log, and active template fields before changing parameters.");

            return causes;
        }

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
                    if (IsSensitiveDisplayKey(parameter.Name))
                        continue;

                    var matchingTemplateField = currentRoot.EnumerateObject()
                        .FirstOrDefault(property => string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
                    var relation = matchingTemplateField.Value.ValueKind == JsonValueKind.Undefined
                        ? "node parameter; no same-name top-level template field detected"
                        : "node parameter; same-name top-level template field exists";
                    lines.Add($"{parameter.Name} (node parameter, value={TrimLong(RedactForDisplay(parameter.Value), 120)}) - {relation}");
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

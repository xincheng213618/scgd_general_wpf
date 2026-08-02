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
        private bool TryGetJsonArgument(IReadOnlyDictionary<string, JsonElement>? arguments, string name, out string json, out string error)
        {
            json = string.Empty;
            error = string.Empty;

            if (arguments == null || !arguments.TryGetValue(name, out var value))
            {
                error = $"The preview_template_patch tool requires {name}.";
                return false;
            }

            json = value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : value.GetRawText();

            if (!string.IsNullOrWhiteSpace(json))
                return true;

            error = $"The {name} argument must not be empty.";
            return false;
        }

        private bool TryBuildTemplatePatchComputation(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            out TemplatePatchComputation computation,
            out string errorCode,
            out string errorMessage)
        {
            computation = new TemplatePatchComputation();
            errorCode = string.Empty;
            errorMessage = string.Empty;

            var templateIdentifier = FirstNonEmpty(GetString(arguments, "template_identifier"), GetString(arguments, "template"), GetString(arguments, "identifier"));
            if (string.IsNullOrWhiteSpace(templateIdentifier))
            {
                errorCode = "missing_template_identifier";
                errorMessage = "The preview_template_patch tool requires template_identifier.";
                return false;
            }

            if (!TryGetJsonArgument(arguments, "proposed_changes", out var proposedChangesJson, out var proposedChangesError))
            {
                errorCode = "missing_proposed_changes";
                errorMessage = proposedChangesError;
                return false;
            }

            var currentJson = GetString(arguments, "current_json");
            var sourceId = string.Empty;
            if (string.IsNullOrWhiteSpace(currentJson))
            {
                if (!TryGetActiveTemplateSourceAndJson(out sourceId, out currentJson))
                {
                    errorCode = "template_context_unavailable";
                    errorMessage = "No current active template JSON editor context is available. Open a template JSON editor or provide current_json for preview-only use.";
                    return false;
                }
            }

            try
            {
                using var currentDocument = JsonDocument.Parse(currentJson);
                using var proposedDocument = JsonDocument.Parse(proposedChangesJson);

                if (currentDocument.RootElement.ValueKind != JsonValueKind.Object)
                {
                    errorCode = "invalid_template_json";
                    errorMessage = $"The current template JSON root must be an object, but was {currentDocument.RootElement.ValueKind}.";
                    return false;
                }

                if (proposedDocument.RootElement.ValueKind != JsonValueKind.Object)
                {
                    errorCode = "invalid_proposed_changes";
                    errorMessage = $"The proposed_changes root must be an object, but was {proposedDocument.RootElement.ValueKind}.";
                    return false;
                }

                if (TryFindSensitiveJsonProperty(proposedDocument.RootElement, out var sensitivePath))
                {
                    errorCode = "sensitive_template_field_not_allowed";
                    errorMessage = $"preview_template_patch refuses to modify sensitive fields: {sensitivePath}.";
                    return false;
                }

                var changes = BuildTemplatePatchChangeLines(currentDocument.RootElement, proposedDocument.RootElement);
                computation = new TemplatePatchComputation
                {
                    TemplateIdentifier = templateIdentifier.Trim(),
                    SourceId = sourceId,
                    CurrentJson = currentJson,
                    ProposedChangesJson = proposedChangesJson,
                    PatchedJson = CreatePatchedTemplateJson(currentJson, proposedChangesJson),
                    Changes = changes,
                };
                return true;
            }
            catch (JsonException ex)
            {
                errorCode = "invalid_template_patch_json";
                errorMessage = $"Template patch preview failed JSON validation: {ex.Message}";
                return false;
            }
        }

        private CopilotMcpToolCallResult ValidateTemplatePatchPreviewCanApply(CopilotMcpTemplatePatchPreview preview)
        {
            if (string.IsNullOrWhiteSpace(preview.SourceId))
                return CopilotMcpToolCallResult.Fail("template_patch_not_applyable", "The preview was not created from the active template editor. Re-run preview_template_patch without current_json while the template editor is active.");

            if (!TryGetActiveTemplateSourceAndJson(out var activeSourceId, out var activeJson))
                return CopilotMcpToolCallResult.Fail("template_context_unavailable", "No active template JSON editor context is available.");

            if (!string.Equals(activeSourceId, preview.SourceId, StringComparison.OrdinalIgnoreCase))
                return CopilotMcpToolCallResult.Fail("template_context_mismatch", "The active template editor is not the same editor that created the preview. Re-run preview_template_patch for the current editor.");

            if (!JsonSemanticallyEquals(activeJson, preview.CurrentJson, out var compareError))
                return CopilotMcpToolCallResult.Fail("template_patch_conflict", string.IsNullOrWhiteSpace(compareError)
                    ? "The active template JSON changed after preview_template_patch. Re-run preview_template_patch before applying."
                    : compareError);

            try
            {
                using var proposedDocument = JsonDocument.Parse(preview.ProposedChangesJson);
                if (TryFindSensitiveJsonProperty(proposedDocument.RootElement, out var sensitivePath))
                    return CopilotMcpToolCallResult.Fail("sensitive_template_field_not_allowed", $"apply_template_patch refuses to modify sensitive fields: {sensitivePath}.");

                using var patchedDocument = JsonDocument.Parse(preview.PatchedJson);
                if (patchedDocument.RootElement.ValueKind != JsonValueKind.Object)
                    return CopilotMcpToolCallResult.Fail("invalid_patched_template_json", "The patched template JSON root is not an object.");
            }
            catch (JsonException ex)
            {
                return CopilotMcpToolCallResult.Fail("invalid_patched_template_json", $"The patched template JSON is invalid: {ex.Message}");
            }

            return CopilotMcpToolCallResult.Ok("ok");
        }

        private bool TryGetActiveTemplateSourceAndJson(out string sourceId, out string currentJson)
        {
            sourceId = string.Empty;
            currentJson = string.Empty;
            var liveContext = _environment.LiveContextProvider();
            if (liveContext == null || !liveContext.SourceId.StartsWith("template-json-editor:", StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (var item in liveContext.SnapshotItems)
            {
                var json = ExtractFencedJson(item.Content);
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                sourceId = liveContext.SourceId;
                currentJson = json;
                return true;
            }

            return false;
        }

        private static IReadOnlyList<string> BuildTemplatePatchChangeLines(JsonElement currentRoot, JsonElement proposedRoot)
        {
            var changes = new List<string>();
            foreach (var proposedProperty in proposedRoot.EnumerateObject())
            {
                currentRoot.TryGetProperty(proposedProperty.Name, out var currentValue);
                var currentText = currentValue.ValueKind == JsonValueKind.Undefined ? "(missing)" : DescribeJsonValue(currentValue);
                var proposedText = DescribeJsonValue(proposedProperty.Value);
                if (string.Equals(currentText, proposedText, StringComparison.Ordinal))
                    continue;

                changes.Add($"- {proposedProperty.Name}: {currentText} -> {proposedText}");
            }

            return changes;
        }

        private static IReadOnlyList<string> BuildTemplatePatchWarningLines(JsonElement currentRoot, JsonElement proposedRoot)
        {
            var warnings = new List<string>();
            foreach (var proposedProperty in proposedRoot.EnumerateObject())
            {
                if (!currentRoot.TryGetProperty(proposedProperty.Name, out var currentValue))
                {
                    warnings.Add($"- Warning: {proposedProperty.Name} is a new top-level key. Confirm the template schema supports it.");
                    continue;
                }

                if (proposedProperty.Value.ValueKind == JsonValueKind.Null)
                    warnings.Add($"- Warning: {proposedProperty.Name} is set to null. Confirm this does not disable or remove required behavior.");

                if (!JsonKindsCompatible(currentValue.ValueKind, proposedProperty.Value.ValueKind))
                    warnings.Add($"- Warning: {proposedProperty.Name} changes type from {currentValue.ValueKind} to {proposedProperty.Value.ValueKind}.");
            }

            return warnings;
        }

        private static string CreatePatchedTemplateJson(string currentJson, string proposedChangesJson)
        {
            var currentObject = JsonNode.Parse(currentJson)?.AsObject()
                ?? throw new JsonException("Current template JSON root must be an object.");
            var proposedObject = JsonNode.Parse(proposedChangesJson)?.AsObject()
                ?? throw new JsonException("Proposed changes root must be an object.");

            foreach (var property in proposedObject)
                currentObject[property.Key] = property.Value?.DeepClone();

            return currentObject.ToJsonString(StructuredJsonOptions);
        }

        private static string BuildTemplatePatchPreviewText(TemplatePatchComputation computation, CopilotMcpTemplatePatchPreview? storedPreview)
        {
            var builder = new StringBuilder();
            builder.AppendLine("ColorVision template patch preview");
            builder.AppendLine($"Template identifier: {computation.TemplateIdentifier}");
            builder.AppendLine("Mode: preview only");
            builder.AppendLine("Would save: False");
            builder.AppendLine("Current JSON valid: True");
            builder.AppendLine("Proposed changes valid: True");
            builder.AppendLine($"Apply eligible: {computation.IsApplyEligible}");
            if (storedPreview != null)
            {
                builder.AppendLine($"preview_id: {storedPreview.PreviewId}");
                builder.AppendLine($"source_id: {storedPreview.SourceId}");
                builder.AppendLine($"current_json_hash: {storedPreview.CurrentJsonHash}");
                builder.AppendLine($"preview_expires_at: {storedPreview.ExpiresAt:O}");
                builder.AppendLine("Next step: call apply_template_patch with this preview_id to create a user-confirmed action.");
            }
            else
            {
                builder.AppendLine("Next step: open the target template JSON editor and call preview_template_patch without current_json before applying.");
            }
            builder.AppendLine($"Changed key fields: {computation.Changes.Count}");
            foreach (var change in computation.Changes.Take(80))
                builder.AppendLine(change);
            foreach (var warning in BuildTemplatePatchWarnings(computation).Take(80))
                builder.AppendLine(warning);
            builder.AppendLine("No template file was saved or mutated.");
            return builder.ToString().TrimEnd();
        }

        private static IReadOnlyList<string> BuildTemplatePatchWarnings(TemplatePatchComputation computation)
        {
            try
            {
                using var currentDocument = JsonDocument.Parse(computation.CurrentJson);
                using var proposedDocument = JsonDocument.Parse(computation.ProposedChangesJson);
                return BuildTemplatePatchWarningLines(currentDocument.RootElement, proposedDocument.RootElement);
            }
            catch (JsonException)
            {
                return Array.Empty<string>();
            }
        }

        private static string BuildTemplatePatchConfirmationPreview(CopilotMcpTemplatePatchPreview preview)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Template patch ready for user confirmation");
            builder.AppendLine($"preview_id: {preview.PreviewId}");
            builder.AppendLine($"template_identifier: {preview.TemplateIdentifier}");
            builder.AppendLine($"source_id: {preview.SourceId}");
            builder.AppendLine($"current_json_hash: {preview.CurrentJsonHash}");
            builder.AppendLine($"changed_key_fields: {preview.Changes.Count}");
            foreach (var change in preview.Changes.Take(80))
                builder.AppendLine(change);
            builder.AppendLine("The active template JSON will be revalidated and conflict-checked again when confirm_action executes.");
            return builder.ToString().TrimEnd();
        }

        private static bool JsonSemanticallyEquals(string leftJson, string rightJson, out string error)
        {
            error = string.Empty;
            try
            {
                using var leftDocument = JsonDocument.Parse(leftJson);
                using var rightDocument = JsonDocument.Parse(rightJson);
                if (leftDocument.RootElement.ValueKind != JsonValueKind.Object || rightDocument.RootElement.ValueKind != JsonValueKind.Object)
                {
                    error = "Template JSON root must be an object.";
                    return false;
                }

                return string.Equals(
                    JsonSerializer.Serialize(leftDocument.RootElement),
                    JsonSerializer.Serialize(rightDocument.RootElement),
                    StringComparison.Ordinal);
            }
            catch (JsonException ex)
            {
                error = $"Template JSON validation failed during conflict check: {ex.Message}";
                return false;
            }
        }

        private string ExtractCurrentTemplateJson()
        {
            var liveContext = _environment.LiveContextProvider();
            if (liveContext == null || !liveContext.SourceId.StartsWith("template-json-editor:", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            foreach (var item in liveContext.SnapshotItems)
            {
                var json = ExtractFencedJson(item.Content);
                if (!string.IsNullOrWhiteSpace(json))
                    return json;
            }

            return string.Empty;
        }

        private static bool TryFindSensitiveJsonProperty(JsonElement element, out string path)
        {
            return TryFindSensitiveJsonProperty(element, "$", out path);
        }

        private static bool TryFindSensitiveJsonProperty(JsonElement element, string pathPrefix, out string path)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPath = $"{pathPrefix}.{property.Name}";
                    if (IsSensitiveDisplayKey(property.Name))
                    {
                        path = propertyPath;
                        return true;
                    }

                    if (TryFindSensitiveJsonProperty(property.Value, propertyPath, out path))
                        return true;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (TryFindSensitiveJsonProperty(item, $"{pathPrefix}[{index}]", out path))
                        return true;
                    index++;
                }
            }

            path = string.Empty;
            return false;
        }

        private static string DescribeJsonValue(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Undefined)
                return "(missing)";

            return RedactForDisplay(TrimLong(value.GetRawText(), 240));
        }
    }
}

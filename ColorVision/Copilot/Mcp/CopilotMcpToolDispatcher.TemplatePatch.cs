using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        private CopilotMcpToolCallResult PreviewTemplatePatch(IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            if (!TryBuildTemplatePatchComputation(arguments, out var computation, out var errorCode, out var errorMessage))
                return CopilotMcpToolCallResult.Fail(errorCode, errorMessage);

            CopilotMcpTemplatePatchPreview? storedPreview = null;
            if (computation.IsApplyEligible)
            {
                storedPreview = CopilotMcpTemplatePatchPreviewStore.Instance.Create(
                    computation.TemplateIdentifier,
                    computation.SourceId,
                    computation.CurrentJson,
                    computation.ProposedChangesJson,
                    computation.PatchedJson,
                    computation.Changes);
            }

            return CopilotMcpToolCallResult.Ok(BuildTemplatePatchPreviewText(computation, storedPreview));
        }

        private async Task<CopilotMcpToolCallResult> SuggestTemplatePatchAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var intent = FirstNonEmpty(
                GetString(arguments, "intent"),
                GetString(arguments, "diagnosis"),
                GetString(arguments, "goal"),
                GetString(arguments, "target"));
            var nodeQuery = FirstNonEmpty(GetString(arguments, "node_id"), GetString(arguments, "node_name"), GetString(arguments, "node"));
            var templateIdentifier = FirstNonEmpty(GetString(arguments, "template_identifier"), GetString(arguments, "template"), GetString(arguments, "identifier"), "active-template");
            var currentJson = GetString(arguments, "current_json");
            var sourceId = string.Empty;
            if (string.IsNullOrWhiteSpace(currentJson))
                TryGetActiveTemplateSourceAndJson(out sourceId, out currentJson);

            if (string.IsNullOrWhiteSpace(currentJson))
                return CopilotMcpToolCallResult.Fail("template_context_unavailable", "No active template JSON is available. Open a template JSON editor or provide current_json before suggesting a template patch.");

            try
            {
                using var currentDocument = JsonDocument.Parse(currentJson);
                if (currentDocument.RootElement.ValueKind != JsonValueKind.Object)
                    return CopilotMcpToolCallResult.Fail("invalid_template_json", $"The current template JSON root must be an object, but was {currentDocument.RootElement.ValueKind}.");

                var hasProposedChanges = arguments != null && arguments.ContainsKey("proposed_changes");
                JsonDocument? proposedDocument = null;
                try
                {
                    if (hasProposedChanges)
                    {
                        if (!TryGetJsonArgument(arguments, "proposed_changes", out var proposedChangesJson, out var proposedChangesError))
                            return CopilotMcpToolCallResult.Fail("missing_proposed_changes", proposedChangesError);

                        proposedDocument = JsonDocument.Parse(proposedChangesJson);
                        if (proposedDocument.RootElement.ValueKind != JsonValueKind.Object)
                            return CopilotMcpToolCallResult.Fail("invalid_proposed_changes", $"The proposed_changes root must be an object, but was {proposedDocument.RootElement.ValueKind}.");

                        if (TryFindSensitiveJsonProperty(proposedDocument.RootElement, out var sensitivePath))
                            return CopilotMcpToolCallResult.Fail("sensitive_template_field_not_allowed", $"suggest_template_patch refuses to suggest sensitive fields: {sensitivePath}.");
                    }

                    var snapshot = await _environment.FlowSnapshotProvider(cancellationToken);
                    var matchedNode = snapshot == null ? null : FindFlowNode(snapshot, nodeQuery);
                    var builder = new StringBuilder();
                    builder.AppendLine("ColorVision template patch suggestion");
                    builder.AppendLine("Mode: read-only suggestion");
                    builder.AppendLine("Would apply: False");
                    builder.AppendLine("Would save: False");
                    builder.AppendLine($"Template identifier: {templateIdentifier.Trim()}");
                    builder.AppendLine($"Template source: {EmptyLabel(sourceId)}");
                    builder.AppendLine($"Intent: {EmptyLabel(intent)}");
                    builder.AppendLine($"Related node: {EmptyLabel(FirstNonEmpty(matchedNode?.Title ?? string.Empty, matchedNode?.NodeName ?? string.Empty, nodeQuery))}");

                    builder.AppendLine();
                    builder.AppendLine("## Candidate Fields");
                    foreach (var field in BuildTemplatePatchCandidateFields(currentDocument.RootElement, intent, matchedNode).Take(20))
                        builder.AppendLine("- " + field);

                    if (proposedDocument != null)
                    {
                        var changes = BuildTemplatePatchChangeLines(currentDocument.RootElement, proposedDocument.RootElement);
                        var warnings = BuildTemplatePatchWarningLines(currentDocument.RootElement, proposedDocument.RootElement);

                        builder.AppendLine();
                        builder.AppendLine("## Proposed Changes");
                        builder.AppendLine(proposedDocument.RootElement.ToString());
                        builder.AppendLine();
                        builder.AppendLine("## Change Summary");
                        if (changes.Count == 0)
                            builder.AppendLine("- No top-level changes detected.");
                        foreach (var change in changes.Take(80))
                            builder.AppendLine(change);

                        builder.AppendLine();
                        builder.AppendLine("## Safety Warnings");
                        if (warnings.Count == 0)
                            builder.AppendLine("- No type-change, null, or unknown-key warnings were detected.");
                        foreach (var warning in warnings.Take(80))
                            builder.AppendLine(warning);

                        builder.AppendLine();
                        builder.AppendLine("## Next MCP Call");
                        builder.AppendLine("Call preview_template_patch with this payload, then review the returned diff and preview_id:");
                        builder.AppendLine("```json");
                        builder.AppendLine(BuildPreviewTemplatePatchPayload(templateIdentifier, proposedDocument.RootElement));
                        builder.AppendLine("```");
                    }
                    else
                    {
                        builder.AppendLine();
                        builder.AppendLine("## Suggested Patch Shape");
                        builder.AppendLine("No proposed_changes were supplied. Choose explicit top-level fields from Candidate Fields, then call suggest_template_patch again with proposed_changes or call preview_template_patch directly.");
                        builder.AppendLine("```json");
                        builder.AppendLine("{");
                        builder.AppendLine("  \"template_identifier\": \"" + EscapeForInlineJson(templateIdentifier) + "\",");
                        builder.AppendLine("  \"proposed_changes\": {");
                        builder.AppendLine("    \"FieldName\": \"new value\"");
                        builder.AppendLine("  }");
                        builder.AppendLine("}");
                        builder.AppendLine("```");
                    }

                    builder.AppendLine();
                    builder.AppendLine("No template JSON was applied, saved, or mutated.");
                    return CopilotMcpToolCallResult.Ok(RedactForDiagnostics(builder.ToString().TrimEnd()));
                }
                finally
                {
                    proposedDocument?.Dispose();
                }
            }
            catch (JsonException ex)
            {
                return CopilotMcpToolCallResult.Fail("invalid_template_patch_json", $"Template patch suggestion failed JSON validation: {ex.Message}");
            }
        }

        private async Task<CopilotMcpToolCallResult> ApplyTemplatePatchAsync(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            var previewId = GetString(arguments, "preview_id");
            if (!CopilotMcpTemplatePatchPreviewStore.Instance.TryGet(previewId, out var preview, out var previewMessage))
                return CopilotMcpToolCallResult.Fail("template_patch_preview_required", previewMessage);

            var validationResult = ValidateTemplatePatchPreviewCanApply(preview);
            if (!validationResult.Success)
                return validationResult;

            if (IsInAppAgentFrameworkApproved(executionScope))
                return await ExecuteTemplatePatchPreviewAsync(preview.PreviewId, cancellationToken);

            return CreateConfirmableActionResult(
                "Confirm template patch",
                $"Apply previewed JSON changes to active template editor: {preview.TemplateIdentifier}",
                "apply_template_patch",
                arguments,
                BuildTemplatePatchConfirmationPreview(preview),
                token => ExecuteTemplatePatchPreviewAsync(preview.PreviewId, token),
                executeOnApproval: IsInAppAgent(executionScope),
                executionScope: executionScope);
        }

        private async Task<CopilotMcpToolCallResult> ExecuteTemplatePatchPreviewAsync(string previewId, CancellationToken cancellationToken)
        {
            if (!CopilotMcpTemplatePatchPreviewStore.Instance.TryGet(previewId, out var preview, out var previewMessage))
                return CopilotMcpToolCallResult.Fail("template_patch_preview_expired", previewMessage);

            var validationResult = ValidateTemplatePatchPreviewCanApply(preview);
            if (!validationResult.Success)
                return validationResult;

            using var proposedDocument = JsonDocument.Parse(preview.ProposedChangesJson);
            if (!TryFindSensitiveJsonProperty(proposedDocument.RootElement, out _))
            {
                var request = new CopilotTemplatePatchApplyRequest
                {
                    PreviewId = preview.PreviewId,
                    TemplateIdentifier = preview.TemplateIdentifier,
                    SourceId = preview.SourceId,
                    ExpectedCurrentJson = preview.CurrentJson,
                    PatchedJson = preview.PatchedJson,
                };

                return await _environment.ApplyTemplatePatchHandler(request, cancellationToken);
            }

            return CopilotMcpToolCallResult.Fail("sensitive_template_field_not_allowed", "apply_template_patch refuses to modify sensitive template fields.");
        }
    }
}

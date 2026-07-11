using ColorVision.Copilot.Mcp;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotTemplatePatchTool : ICopilotTool
    {
        private static readonly string[] ChangeIntentMarkers =
        {
            "change", "adjust", "modify", "set ", "preview", "apply", "patch", "parameter", "threshold", "exposure",
            "修改", "调整", "改成", "设置", "预览", "应用", "参数", "阈值", "曝光",
        };
        private static readonly string[] ApplyIntentMarkers = { "apply", "confirm", "use this preview", "应用", "确认", "使用这个预览" };

        private readonly CopilotMcpToolDispatcher _dispatcher;

        public CopilotTemplatePatchTool()
            : this(new CopilotMcpToolDispatcher())
        {
        }

        public CopilotTemplatePatchTool(CopilotMcpToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public string Name => "TemplatePatch";

        public string Description => "Preview guarded changes to the active template JSON, or stage an existing preview for explicit ColorVision approval. For a preview, input.query must be a JSON string like {\"proposed_changes\":{\"Exposure\":12}}. To stage an existing preview, use {\"preview_id\":\"id\",\"apply\":true}. This tool never saves the template.";

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat)
                return false;

            var context = CopilotLiveContextRegistry.Current;
            if (context == null || !context.SourceId.StartsWith("template-json-editor:", StringComparison.OrdinalIgnoreCase))
                return false;

            var text = request.UserText ?? string.Empty;
            return ChangeIntentMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            var payloadText = ExtractJsonObject(toolInput?.Query);
            if (string.IsNullOrWhiteSpace(payloadText))
                return Failure("Template patch input is missing.", "The planner must provide input.query as JSON with proposed_changes, or preview_id plus apply=true.");

            try
            {
                using var document = JsonDocument.Parse(payloadText);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return Failure("Template patch input is invalid.", "The template patch input root must be a JSON object.");

                if (TryGetString(document.RootElement, "preview_id", out var previewId))
                {
                    if (!TryGetBoolean(document.RootElement, "apply", out var apply) || !apply)
                        return Failure("Template patch application was not requested.", "Staging a preview requires apply=true.");
                    if (!ApplyIntentMarkers.Any(marker => (request.UserText ?? string.Empty).Contains(marker, StringComparison.OrdinalIgnoreCase)))
                        return Failure("Template patch application requires explicit user intent.", "Ask the user to explicitly apply or confirm the preview before staging it.");

                    return await StagePreviewForApprovalAsync(previewId, cancellationToken);
                }

                var proposedChanges = document.RootElement.TryGetProperty("proposed_changes", out var proposedElement)
                    ? proposedElement
                    : document.RootElement;
                if (proposedChanges.ValueKind != JsonValueKind.Object)
                    return Failure("Template patch input is invalid.", "proposed_changes must be a JSON object.");

                var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
                {
                    ["template_identifier"] = JsonSerializer.SerializeToElement("active-template"),
                    ["proposed_changes"] = proposedChanges.Clone(),
                };
                var result = await _dispatcher.CallAsync("preview_template_patch", arguments, cancellationToken, CopilotMcpToolDispatcher.InAppAgentCallerSource);
                return ToToolResult(result, "Template patch preview created.", "Template patch preview failed.");
            }
            catch (JsonException ex)
            {
                return Failure("Template patch input is invalid JSON.", ex.Message);
            }
        }

        private async Task<CopilotToolResult> StagePreviewForApprovalAsync(string previewId, CancellationToken cancellationToken)
        {
            var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["preview_id"] = JsonSerializer.SerializeToElement(previewId),
            };
            var result = await _dispatcher.CallAsync("apply_template_patch", arguments, cancellationToken, CopilotMcpToolDispatcher.InAppAgentCallerSource);
            var isWaitingForApproval = string.Equals(result.ErrorCode, "confirmation_required", StringComparison.OrdinalIgnoreCase);
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success || isWaitingForApproval,
                Summary = isWaitingForApproval ? "Template patch is waiting for explicit ColorVision approval." : result.Success ? "Template patch applied." : "Template patch staging failed.",
                Content = result.Text,
                ErrorMessage = result.Success || isWaitingForApproval ? string.Empty : result.Text,
            };
        }

        private CopilotToolResult ToToolResult(CopilotMcpToolCallResult result, string successSummary, string failureSummary)
        {
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success,
                Summary = result.Success ? successSummary : failureSummary,
                Content = result.Text,
                ErrorMessage = result.Success ? string.Empty : result.Text,
            };
        }

        private CopilotToolResult Failure(string summary, string error)
        {
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = summary,
                ErrorMessage = error,
            };
        }

        private static string ExtractJsonObject(string? text)
        {
            var value = (text ?? string.Empty).Trim();
            var start = value.IndexOf('{');
            var end = value.LastIndexOf('}');
            return start >= 0 && end > start ? value[start..(end + 1)] : string.Empty;
        }

        private static bool TryGetString(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
                return false;

            value = property.GetString()?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryGetBoolean(JsonElement element, string propertyName, out bool value)
        {
            value = false;
            if (!element.TryGetProperty(propertyName, out var property))
                return false;

            if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                value = property.GetBoolean();
                return true;
            }

            return property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out value);
        }
    }
}

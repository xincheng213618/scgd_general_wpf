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
        private readonly ICopilotApplicationCapabilityInvoker _capabilityInvoker;

        public CopilotTemplatePatchTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotTemplatePatchTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
        {
            _capabilityInvoker = capabilityInvoker ?? throw new ArgumentNullException(nameof(capabilityInvoker));
        }

        public string Name => "TemplatePatch";

        public string Description => "Preview guarded changes to the active template JSON. input.query must be a JSON string like {\"proposed_changes\":{\"Exposure\":12}}. This function never applies or saves the template; use ApplyTemplatePatch for an existing preview.";

        public CopilotToolAccess Access => CopilotToolAccess.ReadOnly;

        public CopilotToolRiskLevel RiskLevel => CopilotToolRiskLevel.Low;

        public CopilotToolApprovalMode ApprovalMode => CopilotToolApprovalMode.Never;

        public CopilotToolIdempotency Idempotency => CopilotToolIdempotency.Unknown;

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("JSON object containing proposed_changes for a preview.", required: true);

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat)
                return false;

            var context = CopilotLiveContextRegistry.Current;
            if (context == null || !context.SourceId.StartsWith("template-json-editor:", StringComparison.OrdinalIgnoreCase))
                return false;

            return CopilotTemplatePatchIntentSupport.HasChangeIntent(request.UserText)
                && !CopilotTemplatePatchIntentSupport.HasApplyIntent(request.UserText);
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
                    return Failure("Template patch preview input cannot apply an existing preview.", $"Use ApplyTemplatePatch with preview_id={previewId} after an explicit apply request.");

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
                var result = await _capabilityInvoker.InvokeAsync(
                    "preview_template_patch",
                    arguments,
                    CopilotApplicationCapabilityCaller.InAppAgent,
                    cancellationToken);
                return ToToolResult(result, "Template patch preview created.", "Template patch preview failed.");
            }
            catch (JsonException ex)
            {
                return Failure("Template patch input is invalid JSON.", ex.Message);
            }
        }

        private CopilotToolResult ToToolResult(CopilotApplicationCapabilityCallResult result, string successSummary, string failureSummary)
        {
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success,
                Summary = result.Success ? successSummary : failureSummary,
                Content = result.Content,
                ErrorMessage = result.Success ? string.Empty : result.Content,
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

    }

    internal static class CopilotTemplatePatchIntentSupport
    {
        private static readonly string[] ChangeIntentMarkers =
        {
            "change", "adjust", "modify", "set ", "preview", "apply", "patch", "parameter", "threshold", "exposure",
            "修改", "调整", "改成", "设置", "预览", "应用", "参数", "阈值", "曝光",
        };
        private static readonly string[] ApplyIntentMarkers = { "apply", "confirm", "use this preview", "应用", "确认", "使用这个预览" };

        public static bool HasChangeIntent(string? text) => ChangeIntentMarkers.Any(marker => (text ?? string.Empty).Contains(marker, StringComparison.OrdinalIgnoreCase));

        public static bool HasApplyIntent(string? text) => ApplyIntentMarkers.Any(marker => (text ?? string.Empty).Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}

using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotApplyTemplatePatchTool : ICopilotFrameworkApprovedTool
    {
        private readonly ICopilotApplicationCapabilityInvoker _capabilityInvoker;

        public CopilotApplyTemplatePatchTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotApplyTemplatePatchTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
        {
            _capabilityInvoker = capabilityInvoker ?? throw new ArgumentNullException(nameof(capabilityInvoker));
        }

        public string Name => "ApplyTemplatePatch";

        public string Description => "Apply an existing guarded template preview after explicit approval. input.query must be JSON like {\"preview_id\":\"id\"}. The change affects only the active editor and does not save the template.";

        public CopilotToolAccess Access => CopilotToolAccess.Write;

        public CopilotToolRiskLevel RiskLevel => CopilotToolRiskLevel.High;

        public CopilotToolApprovalMode ApprovalMode => CopilotToolApprovalMode.Always;

        public CopilotToolIdempotency Idempotency => CopilotToolIdempotency.NonIdempotent;

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("JSON object containing the preview_id to apply.", required: true);

        public bool CanHandle(CopilotAgentRequest request)
        {
            var context = CopilotLiveContextRegistry.Current;
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && context != null
                && context.SourceId.StartsWith("template-json-editor:", StringComparison.OrdinalIgnoreCase)
                && CopilotTemplatePatchIntentSupport.HasApplyIntent(request.UserText);
        }

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(request, toolInput, CopilotApplicationCapabilityCaller.InAppAgent, cancellationToken);
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(request, toolInput, CopilotApplicationCapabilityCaller.InAppAgentFrameworkApproved, cancellationToken);
        }

        private async Task<CopilotToolResult> ExecuteCoreAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CopilotApplicationCapabilityCaller caller,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!CopilotTemplatePatchIntentSupport.HasApplyIntent(request.UserText))
                return Failure("Template patch application requires explicit user intent.", "Ask the user to explicitly apply or confirm the preview.");

            if (!TryReadPreviewId(toolInput?.Query, out var previewId, out var error))
                return Failure("Template patch apply input is invalid.", error);

            var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["preview_id"] = JsonSerializer.SerializeToElement(previewId),
            };
            var result = await _capabilityInvoker.InvokeAsync("apply_template_patch", arguments, caller, cancellationToken);
            var isWaitingForApproval = result.IsApprovalRequired;
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success || isWaitingForApproval,
                Summary = isWaitingForApproval ? "Template patch is waiting for explicit ColorVision approval." : result.Success ? "Template patch applied to the active editor." : "Template patch application failed.",
                Content = result.Content,
                ErrorMessage = result.Success || isWaitingForApproval ? string.Empty : result.Content,
                FailureKind = result.FailureKind,
                Approval = result.Approval,
            };
        }

        private static bool TryReadPreviewId(string? input, out string previewId, out string error)
        {
            previewId = string.Empty;
            error = string.Empty;
            try
            {
                using var document = JsonDocument.Parse((input ?? string.Empty).Trim());
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("preview_id", out var property)
                    || property.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(property.GetString()))
                {
                    error = "input.query must be a JSON object with a non-empty preview_id.";
                    return false;
                }

                previewId = property.GetString()!.Trim();
                return true;
            }
            catch (JsonException ex)
            {
                error = $"input.query must be valid JSON: {ex.Message}";
                return false;
            }
        }

        private static CopilotToolResult Failure(string summary, string error)
        {
            return new CopilotToolResult
            {
                ToolName = "ApplyTemplatePatch",
                Success = false,
                Summary = summary,
                ErrorMessage = error,
            };
        }
    }
}

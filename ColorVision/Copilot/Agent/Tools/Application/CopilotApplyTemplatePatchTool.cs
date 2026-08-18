using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotApplyTemplatePatchTool : ICopilotFrameworkApprovedTool, ICopilotApplicationCapabilityClient
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

        public string Name => CopilotSharedCapabilityCatalog.ApplyTemplatePatch.AgentToolName;

        public ICopilotApplicationCapabilityInvoker ApplicationCapabilityInvoker => _capabilityInvoker;

        public string Description => CopilotSharedCapabilityCatalog.ApplyTemplatePatch.AgentDescription;

        public CopilotToolCapabilityDescriptor Capability =>
            CopilotSharedCapabilityCatalog.ApplyTemplatePatch.AgentCapability;

        public CopilotToolAccess Access => Capability.Access;

        public CopilotToolRiskLevel RiskLevel => Capability.RiskLevel;

        public CopilotToolApprovalMode ApprovalMode => Capability.ApprovalMode;

        public CopilotToolIdempotency Idempotency => Capability.Idempotency;

        public bool AllowsTemporaryFullAccess => Capability.AllowsTemporaryFullAccess;

        public CopilotToolInputSchema InputSchema => CopilotSharedCapabilityCatalog.ApplyTemplatePatch.AgentInputSchema;

        public bool CanHandle(CopilotAgentRequest request)
        {
            var context = CopilotLiveContextRegistry.Current;
            return request != null
                && request.Mode is not (CopilotAgentMode.Chat or CopilotAgentMode.Diagnose)
                && context != null
                && context.SourceId.StartsWith("template-json-editor:", StringComparison.OrdinalIgnoreCase)
                && CopilotTemplatePatchIntentSupport.HasApplyIntent(request.UserText);
        }

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(request, toolInput, frameworkApprovalGranted: false, cancellationToken);
        }

        Task<CopilotToolResult> ICopilotFrameworkApprovedTool.ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(request, toolInput, frameworkApprovalGranted: true, cancellationToken);
        }

        private async Task<CopilotToolResult> ExecuteCoreAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            bool frameworkApprovalGranted,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Mode == CopilotAgentMode.Diagnose)
                return Failure("Template patch application is unavailable in Diagnose mode.", "Start a separate explicit template-edit request before applying a preview.");
            if (!CopilotTemplatePatchIntentSupport.HasApplyIntent(request.UserText))
                return Failure("Template patch application requires explicit user intent.", "Ask the user to explicitly apply or confirm the preview.");

            var previewId = toolInput.GetStringArgument("preview_id");
            if (string.IsNullOrWhiteSpace(previewId)
                && !TryReadPreviewId(toolInput?.Query, out previewId, out var error))
            {
                return Failure("Template patch apply input is invalid.", error);
            }

            var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["preview_id"] = JsonSerializer.SerializeToElement(previewId),
            };
            var result = await CopilotApplicationCapabilityInvocation.InvokeAsync(
                _capabilityInvoker,
                CopilotSharedCapabilityCatalog.ApplyTemplatePatch.McpToolName,
                arguments,
                request,
                frameworkApprovalGranted,
                cancellationToken);
            return CopilotApplicationCapabilityInvocation.ToToolResult(
                result,
                Name,
                "Template patch applied to the active editor.",
                "Template patch application failed.",
                "Template patch is waiting for explicit ColorVision approval.");
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
                ToolName = CopilotSharedCapabilityCatalog.ApplyTemplatePatch.AgentToolName,
                Success = false,
                Summary = summary,
                ErrorMessage = error,
            };
        }
    }
}

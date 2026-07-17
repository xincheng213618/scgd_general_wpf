using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotSetLanguageTool : ICopilotFrameworkApprovedTool
    {
        private readonly ICopilotApplicationCapabilityInvoker _capabilityInvoker;

        public CopilotSetLanguageTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotSetLanguageTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
        {
            _capabilityInvoker = capabilityInvoker ?? throw new ArgumentNullException(nameof(capabilityInvoker));
        }

        public string Name => "SetLanguage";

        public string Description => "Switch the UI language requested by the user. input.query can contain a language or culture name such as English, Chinese, en-US, or zh-Hans. The change may ask for confirmation and restart the application.";

        public CopilotToolAccess Access => CopilotToolAccess.Write;

        public CopilotToolRiskLevel RiskLevel => CopilotToolRiskLevel.High;

        public CopilotToolApprovalMode ApprovalMode => CopilotToolApprovalMode.Always;

        public CopilotToolIdempotency Idempotency => CopilotToolIdempotency.Idempotent;

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("Requested language or culture name, such as English, Chinese, en-US, or zh-Hans.", required: true);

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && Application.Current != null
                && CopilotApplicationCapability.HasLanguageIntent(request.UserText);
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return await ExecuteCoreAsync(request, toolInput, CopilotApplicationCapabilityCaller.InAppAgent, cancellationToken);
        }

        public async Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return await ExecuteCoreAsync(request, toolInput, CopilotApplicationCapabilityCaller.InAppAgentFrameworkApproved, cancellationToken);
        }

        private async Task<CopilotToolResult> ExecuteCoreAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CopilotApplicationCapabilityCaller caller,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var sourceText = string.IsNullOrWhiteSpace(toolInput?.Query)
                ? request.UserText
                : toolInput.Query;

            var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["language"] = JsonSerializer.SerializeToElement(sourceText),
            };
            var result = await _capabilityInvoker.InvokeAsync("set_language", arguments, caller, cancellationToken);
            var isWaitingForApproval = result.IsApprovalRequired;
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success || isWaitingForApproval,
                Summary = isWaitingForApproval ? "Language change is waiting for explicit ColorVision approval." : result.Success ? "Language change completed." : "Language change failed.",
                Content = result.Content,
                ErrorMessage = result.Success || isWaitingForApproval ? string.Empty : result.Content,
                FailureKind = result.FailureKind,
                FailureCode = result.Success || isWaitingForApproval ? string.Empty : CopilotToolFailureCode.Normalize(result.ErrorCode),
                Approval = result.Approval,
            };
        }
    }
}

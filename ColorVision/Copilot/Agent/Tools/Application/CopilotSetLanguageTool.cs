using ColorVision.Copilot.Mcp;
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
        private readonly CopilotMcpToolDispatcher _dispatcher;

        public CopilotSetLanguageTool()
            : this(new CopilotMcpToolDispatcher())
        {
        }

        public CopilotSetLanguageTool(CopilotMcpToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
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
            return await ExecuteCoreAsync(request, toolInput, CopilotMcpToolDispatcher.InAppAgentCallerSource, cancellationToken);
        }

        public async Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return await ExecuteCoreAsync(request, toolInput, CopilotMcpToolDispatcher.InAppAgentFrameworkApprovedCallerSource, cancellationToken);
        }

        private async Task<CopilotToolResult> ExecuteCoreAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            string callerSource,
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
            var result = await _dispatcher.CallAsync("set_language", arguments, cancellationToken, callerSource);
            var isWaitingForApproval = string.Equals(result.ErrorCode, "confirmation_required", StringComparison.OrdinalIgnoreCase);
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success || isWaitingForApproval,
                Summary = isWaitingForApproval ? "Language change is waiting for explicit ColorVision approval." : result.Success ? "Language change completed." : "Language change failed.",
                Content = result.Text,
                ErrorMessage = result.Success || isWaitingForApproval ? string.Empty : result.Text,
                Approval = result.ToApprovalInfo(),
            };
        }
    }
}

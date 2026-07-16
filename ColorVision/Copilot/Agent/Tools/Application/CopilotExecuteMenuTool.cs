using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotExecuteMenuTool : ICopilotFrameworkApprovedTool
    {
        private readonly ICopilotApplicationCapabilityInvoker _capabilityInvoker;

        public CopilotExecuteMenuTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotExecuteMenuTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
        {
            _capabilityInvoker = capabilityInvoker ?? throw new ArgumentNullException(nameof(capabilityInvoker));
        }

        public string Name => "ExecuteMenu";

        public string Description => "Execute a generic main-menu command by exact menu name or path after explicit approval, such as Options, VAM, or Check for Updates. Put the target menu directly in input.query. Prefer dedicated lower-risk tools such as SetTheme when available.";

        public CopilotToolAccess Access => CopilotToolAccess.Write;

        public CopilotToolRiskLevel RiskLevel => CopilotToolRiskLevel.High;

        public CopilotToolApprovalMode ApprovalMode => CopilotToolApprovalMode.Always;

        public CopilotToolIdempotency Idempotency => CopilotToolIdempotency.Unknown;

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("Exact menu name or menu path requested by the user.", required: true);

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat || Application.Current == null)
                return false;

            if (CopilotFlowCreationSupport.HasCreateIntent(request.UserText))
                return false;

            if (!CopilotApplicationCapability.HasMenuIntent(request.UserText))
                return false;

            return CopilotApplicationCapability.HasMenuCandidates(request.UserText);
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
                ["query"] = JsonSerializer.SerializeToElement(sourceText),
                ["dry_run"] = JsonSerializer.SerializeToElement(false),
            };
            var result = await _capabilityInvoker.InvokeAsync("execute_menu", arguments, caller, cancellationToken);
            var isWaitingForApproval = result.IsApprovalRequired;
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success || isWaitingForApproval,
                Summary = isWaitingForApproval
                    ? "Menu command is waiting for explicit ColorVision approval."
                    : result.Success ? "Menu command executed." : "Menu command execution failed.",
                Content = result.Content,
                ErrorMessage = result.Success || isWaitingForApproval ? string.Empty : result.Content,
                Approval = result.Approval,
            };
        }
    }
}

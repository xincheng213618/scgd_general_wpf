using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotCreateFlowTool : ICopilotFrameworkApprovedTool
    {
        private readonly ICopilotApplicationCapabilityInvoker _capabilityInvoker;

        public CopilotCreateFlowTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotCreateFlowTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
        {
            _capabilityInvoker = capabilityInvoker ?? throw new ArgumentNullException(nameof(capabilityInvoker));
        }

        public string Name => "CreateFlow";

        public string Description => "Create a new empty ColorVision flow after explicit user approval. Put only the requested flow name in input.query, or leave it empty to generate a timestamped name. This tool stages the action and never opens the flow-template manager.";

        public CopilotToolAccess Access => CopilotToolAccess.Write;

        public CopilotToolRiskLevel RiskLevel => CopilotToolRiskLevel.High;

        public CopilotToolApprovalMode ApprovalMode => CopilotToolApprovalMode.Always;

        public CopilotToolIdempotency Idempotency => CopilotToolIdempotency.NonIdempotent;

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("Optional name for the new flow. Omit to generate a timestamped name.");

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && Application.Current != null
                && CopilotFlowCreationSupport.HasCreateIntent(request.UserText);
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

            var flowName = CopilotFlowCreationSupport.ResolveFlowName(request.UserText, toolInput?.Query);
            var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = JsonSerializer.SerializeToElement(flowName),
            };
            var result = await _capabilityInvoker.InvokeAsync("create_flow", arguments, caller, cancellationToken);
            var isWaitingForApproval = result.IsApprovalRequired;

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success || isWaitingForApproval,
                Summary = isWaitingForApproval
                    ? $"Flow {flowName} is waiting for explicit ColorVision approval."
                    : result.Success ? $"Created flow {flowName}." : "Flow creation failed.",
                Content = result.Content,
                ErrorMessage = result.Success || isWaitingForApproval ? string.Empty : result.Content,
                FailureKind = result.FailureKind,
                FailureCode = result.Success || isWaitingForApproval ? string.Empty : CopilotToolFailureCode.Normalize(result.ErrorCode),
                Approval = result.Approval,
            };
        }
    }
}

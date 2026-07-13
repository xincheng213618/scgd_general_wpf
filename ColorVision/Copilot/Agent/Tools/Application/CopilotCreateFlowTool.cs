using ColorVision.Copilot.Mcp;
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
        private readonly CopilotMcpToolDispatcher _dispatcher;

        public CopilotCreateFlowTool()
            : this(new CopilotMcpToolDispatcher())
        {
        }

        public CopilotCreateFlowTool(CopilotMcpToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
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

            var flowName = CopilotFlowCreationSupport.ResolveFlowName(request.UserText, toolInput?.Query);
            var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = JsonSerializer.SerializeToElement(flowName),
            };
            var result = await _dispatcher.CallAsync("create_flow", arguments, cancellationToken, callerSource);
            var isWaitingForApproval = string.Equals(result.ErrorCode, "confirmation_required", StringComparison.OrdinalIgnoreCase);

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success || isWaitingForApproval,
                Summary = isWaitingForApproval
                    ? $"Flow {flowName} is waiting for explicit ColorVision approval."
                    : result.Success ? $"Created flow {flowName}." : "Flow creation failed.",
                Content = result.Text,
                ErrorMessage = result.Success || isWaitingForApproval ? string.Empty : result.Text,
                Approval = result.ToApprovalInfo(),
            };
        }
    }
}

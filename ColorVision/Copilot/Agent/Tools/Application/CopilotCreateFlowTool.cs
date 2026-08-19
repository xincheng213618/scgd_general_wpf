using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotCreateFlowTool : ICopilotFrameworkApprovedTool, ICopilotApplicationCapabilityClient
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

        public string Name => CopilotSharedCapabilityCatalog.CreateFlow.AgentToolName;

        public ICopilotApplicationCapabilityInvoker ApplicationCapabilityInvoker => _capabilityInvoker;

        public string Description => CopilotSharedCapabilityCatalog.CreateFlow.AgentDescription;

        public CopilotToolCapabilityDescriptor Capability =>
            CopilotSharedCapabilityCatalog.CreateFlow.AgentCapability;

        public CopilotToolAccess Access => Capability.Access;

        public CopilotToolRiskLevel RiskLevel => Capability.RiskLevel;

        public CopilotToolApprovalMode ApprovalMode => Capability.ApprovalMode;

        public CopilotToolIdempotency Idempotency => Capability.Idempotency;

        public CopilotToolInputSchema InputSchema => CopilotSharedCapabilityCatalog.CreateFlow.AgentInputSchema;

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
            return await ExecuteCoreAsync(request, toolInput, frameworkApprovalGranted: false, cancellationToken);
        }

        async Task<CopilotToolResult> ICopilotFrameworkApprovedTool.ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return await ExecuteCoreAsync(request, toolInput, frameworkApprovalGranted: true, cancellationToken);
        }

        private async Task<CopilotToolResult> ExecuteCoreAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            bool frameworkApprovalGranted,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var requestedName = toolInput.GetStringArgument("name");
            if (string.IsNullOrWhiteSpace(requestedName))
                requestedName = toolInput?.Query;
            var flowName = CopilotFlowCreationSupport.ResolveFlowName(request.UserText, requestedName);
            var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = JsonSerializer.SerializeToElement(flowName),
            };
            var result = await CopilotApplicationCapabilityInvocation.InvokeAsync(
                _capabilityInvoker,
                CopilotSharedCapabilityCatalog.CreateFlow.McpToolName,
                arguments,
                request,
                frameworkApprovalGranted,
                cancellationToken);
            return CopilotApplicationCapabilityInvocation.ToToolResult(
                result,
                Name,
                $"Created flow {flowName}.",
                "Flow creation failed.",
                $"Flow {flowName} is waiting for explicit ColorVision approval.");
        }
    }
}

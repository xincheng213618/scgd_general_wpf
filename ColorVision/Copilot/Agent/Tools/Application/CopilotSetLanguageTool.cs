using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotSetLanguageTool : ICopilotFrameworkApprovedTool, ICopilotApplicationCapabilityClient
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

        public string Name => CopilotSharedCapabilityCatalog.SetLanguage.AgentToolName;

        public ICopilotApplicationCapabilityInvoker ApplicationCapabilityInvoker => _capabilityInvoker;

        public string Description => CopilotSharedCapabilityCatalog.SetLanguage.AgentDescription;

        public CopilotToolCapabilityDescriptor Capability =>
            CopilotSharedCapabilityCatalog.SetLanguage.AgentCapability;

        public CopilotToolAccess Access => Capability.Access;

        public CopilotToolRiskLevel RiskLevel => Capability.RiskLevel;

        public CopilotToolApprovalMode ApprovalMode => Capability.ApprovalMode;

        public CopilotToolIdempotency Idempotency => Capability.Idempotency;

        public CopilotToolInputSchema InputSchema => CopilotSharedCapabilityCatalog.SetLanguage.AgentInputSchema;

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

            var sourceText = toolInput.GetStringArgument("language");
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                sourceText = string.IsNullOrWhiteSpace(toolInput?.Query)
                    ? request.UserText
                    : toolInput.Query;
            }

            var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["language"] = JsonSerializer.SerializeToElement(sourceText),
            };
            var result = await CopilotApplicationCapabilityInvocation.InvokeAsync(
                _capabilityInvoker,
                CopilotSharedCapabilityCatalog.SetLanguage.McpToolName,
                arguments,
                request,
                frameworkApprovalGranted,
                cancellationToken);
            return CopilotApplicationCapabilityInvocation.ToToolResult(
                result,
                Name,
                "Language change completed.",
                "Language change failed.",
                "Language change is waiting for explicit ColorVision approval.");
        }
    }
}

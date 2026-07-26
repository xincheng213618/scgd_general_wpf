using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public enum CopilotApplicationCapabilityCaller
    {
        InAppAgent,
    }

    public sealed class CopilotApplicationCapabilityCallResult
    {
        public bool Success { get; init; }

        public string Content { get; init; } = string.Empty;

        public string ErrorCode { get; init; } = string.Empty;

        public CopilotToolFailureKind FailureKind { get; init; }

        public CopilotToolApprovalInfo? Approval { get; init; }

        public bool IsApprovalRequired => Approval != null;
    }

    public interface ICopilotApplicationCapabilityInvoker
    {
        Task<CopilotApplicationCapabilityCallResult> InvokeAsync(
            string capabilityName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotApplicationCapabilityCaller caller,
            CancellationToken cancellationToken);
    }

    internal interface ICopilotApprovedApplicationCapabilityInvoker
    {
        Task<CopilotApplicationCapabilityCallResult> InvokeApprovedAsync(
            string capabilityName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotAgentRequest request,
            CancellationToken cancellationToken);
    }

    internal static class CopilotApplicationCapabilityInvocation
    {
        public static Task<CopilotApplicationCapabilityCallResult> InvokeAsync(
            ICopilotApplicationCapabilityInvoker invoker,
            string capabilityName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotAgentRequest request,
            bool frameworkApprovalGranted,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(invoker);
            ArgumentNullException.ThrowIfNull(request);
            if (!frameworkApprovalGranted)
            {
                return invoker.InvokeAsync(
                    capabilityName,
                    arguments,
                    CopilotApplicationCapabilityCaller.InAppAgent,
                    cancellationToken);
            }

            if (invoker is ICopilotApprovedApplicationCapabilityInvoker approvedInvoker)
                return approvedInvoker.InvokeApprovedAsync(capabilityName, arguments, request, cancellationToken);

            return Task.FromResult(new CopilotApplicationCapabilityCallResult
            {
                Success = false,
                ErrorCode = "approved_capability_channel_unavailable",
                FailureKind = CopilotToolFailureKind.Authorization,
                Content = "The configured application capability invoker does not expose ColorVision's internal approved-execution channel.",
            });
        }
    }

}

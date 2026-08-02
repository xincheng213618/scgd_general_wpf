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
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken);
    }

    internal interface ICopilotScopedApplicationCapabilityInvoker
    {
        Task<CopilotApplicationCapabilityCallResult> InvokeScopedAsync(
            string capabilityName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotAgentRequest request,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken);
    }

    internal static class CopilotCapabilityRevisionAuthorization
    {
        public static bool TryValidate(
            CopilotExecutionScope executionScope,
            Func<long> currentRevisionProvider,
            out string rejectionReason)
        {
            ArgumentNullException.ThrowIfNull(executionScope);
            ArgumentNullException.ThrowIfNull(currentRevisionProvider);

            long currentRevision;
            try
            {
                currentRevision = Math.Max(0, currentRevisionProvider());
            }
            catch
            {
                rejectionReason = "The current Copilot capability revision could not be verified before execution.";
                return false;
            }

            if (currentRevision == executionScope.CapabilityRevision)
            {
                rejectionReason = string.Empty;
                return true;
            }

            rejectionReason =
                $"The Copilot capability catalog changed after approval (revision {executionScope.CapabilityRevision} -> {currentRevision}). Re-plan the tool call and request a fresh approval.";
            return false;
        }
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
            var activeInvocation = CopilotToolInvocationContext.Current;
            var executionScope = activeInvocation?.ExecutionScope
                ?? CopilotExecutionScope.ForAgentRequest(request);
            if (!frameworkApprovalGranted)
            {
                if (invoker is ICopilotScopedApplicationCapabilityInvoker scopedInvoker)
                {
                    return scopedInvoker.InvokeScopedAsync(
                        capabilityName,
                        arguments,
                        request,
                        executionScope,
                        cancellationToken);
                }

                return invoker.InvokeAsync(
                    capabilityName,
                    arguments,
                    CopilotApplicationCapabilityCaller.InAppAgent,
                    cancellationToken);
            }

            if (activeInvocation == null
                || !activeInvocation.FrameworkApprovalGranted
                || !ReferenceEquals(activeInvocation.AgentRequest, request)
                || !executionScope.HasToolCallBinding)
            {
                return Task.FromResult(new CopilotApplicationCapabilityCallResult
                {
                    Success = false,
                    ErrorCode = "approved_execution_context_missing",
                    FailureKind = CopilotToolFailureKind.Authorization,
                    Content = "The approved application capability call is not bound to the active ColorVision tool invocation.",
                });
            }

            if (invoker is ICopilotApprovedApplicationCapabilityInvoker approvedInvoker)
            {
                return approvedInvoker.InvokeApprovedAsync(
                    capabilityName,
                    arguments,
                    request,
                    executionScope,
                    cancellationToken);
            }

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

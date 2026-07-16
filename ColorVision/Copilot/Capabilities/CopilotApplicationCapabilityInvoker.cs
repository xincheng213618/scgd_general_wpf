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
        InAppAgentFrameworkApproved,
    }

    public sealed class CopilotApplicationCapabilityCallResult
    {
        public bool Success { get; init; }

        public string Content { get; init; } = string.Empty;

        public string ErrorCode { get; init; } = string.Empty;

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

}

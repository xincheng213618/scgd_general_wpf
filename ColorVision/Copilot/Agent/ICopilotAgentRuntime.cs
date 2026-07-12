using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public interface ICopilotAgentRuntime
    {
        Task<CopilotAgentRunResult> RunAsync(
            CopilotAgentRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken);
    }
}

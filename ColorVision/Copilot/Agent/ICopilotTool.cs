using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public interface ICopilotTool
    {
        string Name { get; }

        string Description { get; }

        bool CanHandle(CopilotAgentRequest request);

        Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken);
    }
}
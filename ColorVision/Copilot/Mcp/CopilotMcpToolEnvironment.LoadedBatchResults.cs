using ColorVision.Engine;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp;

internal sealed partial class CopilotMcpToolEnvironment
{
    private static Task<CopilotLoadedBatchResultsSnapshot> CreateDefaultLoadedBatchResultsSnapshotAsync(CancellationToken cancellationToken)
        => new CopilotLoadedBatchResultsReader().CaptureAsync(cancellationToken);
}

using ColorVision.Engine.Services;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp;

internal sealed partial class CopilotMcpToolEnvironment
{
    private static Task<CopilotDeviceServicesSnapshot?> CreateDefaultDeviceServicesSnapshotAsync(CancellationToken cancellationToken)
        => new CopilotDeviceServicesReader().CaptureCurrentAsync(cancellationToken);
}

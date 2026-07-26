using ColorVision.Copilot;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotContextDiagnosticsTests
{
    [Fact]
    public void TrustedProjectRootUsesTheFullNormalizedPath()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "copilot-context", "Default"));

        var report = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            AgentContextEnabled = true,
            TrustedProjectRootPaths = [root],
        });

        Assert.Contains($"  - {root}", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"{Environment.NewLine}  - Default{Environment.NewLine}", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DriveRootKeepsItsDirectorySeparator()
    {
        var root = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()));
        Assert.False(string.IsNullOrWhiteSpace(root));

        var report = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            AgentContextEnabled = true,
            TrustedProjectRootPaths = [root!],
        });

        Assert.Contains($"  - {root}", report, StringComparison.OrdinalIgnoreCase);
    }
}

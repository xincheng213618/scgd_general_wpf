using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotStatusDiagnosticsTests
{
    [Fact]
    public void ApplicationVersionPreservesFourPartRevision()
    {
        var version = new Version(1, 4, 11, 1);

        var formatted = CopilotStatusDiagnostics.FormatApplicationVersion(version);

        Assert.Equal("1.4.11.1", formatted);
    }

    [Fact]
    public void ApplicationVersionFallsBackWhenAssemblyVersionIsUnavailable()
    {
        Assert.Equal("unknown", CopilotStatusDiagnostics.FormatApplicationVersion(null));
    }
}

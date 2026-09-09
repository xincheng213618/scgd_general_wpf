using System.Windows.Interop;

namespace ColorVision.UI.Tests;

public sealed class StartupRenderingModeTests
{
    [Theory]
    [InlineData("--software-rendering")]
    [InlineData("--SOFTWARE-RENDERING")]
    public void ExplicitSwitchSelectsSoftwareRendering(string argument)
        => Assert.Equal(RenderMode.SoftwareOnly, StartupRenderingMode.Resolve([argument]));

    [Fact]
    public void NormalStartupDoesNotOverrideRenderingMode()
        => Assert.Null(StartupRenderingMode.Resolve([]));

    [Fact]
    public void OtherStartupOptionsDoNotSelectSoftwareRendering()
        => Assert.Null(StartupRenderingMode.Resolve(["--startup-maintenance", "recovery"]));
}

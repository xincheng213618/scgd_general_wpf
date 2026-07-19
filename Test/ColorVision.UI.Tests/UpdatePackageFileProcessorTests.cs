using ColorVision.Update;
using ColorVision.UI;
using System.ComponentModel;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class UpdatePackageFileProcessorTests
{
    [Fact]
    public void UpdatePackagesAreRegisteredAsDirectInstallActions()
    {
        IEnumerable<string> extensions = typeof(PluginPackageFileProcessor).Assembly.GetTypes()
            .Where(type => typeof(IFileOpenActionProcessor).IsAssignableFrom(type) && !type.IsAbstract)
            .Select(type => type.GetCustomAttribute<FileExtensionAttribute>())
            .OfType<FileExtensionAttribute>()
            .SelectMany(attribute => attribute.Extensions);

        Assert.Contains(extensions, extension => string.Equals(extension, ".cvx", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(extensions, extension => string.Equals(extension, ".cvxp", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(@"H:\ColorVision\Update\ColorVision-Update-[1.4.10.93].cvx", (int)StartupUpdatePackageKind.Application)]
    [InlineData(@"H:\ColorVision\Update\ColorVision.Plugin.cvxp", (int)StartupUpdatePackageKind.Plugin)]
    [InlineData(@"H:\ColorVision\Update\COLORVISION.CVXP", (int)StartupUpdatePackageKind.Plugin)]
    [InlineData(@"H:\ColorVision\Update\ColorVision.zip", (int)StartupUpdatePackageKind.None)]
    [InlineData(@"H:\ColorVision\Data\Measurement.cvcie", (int)StartupUpdatePackageKind.None)]
    [InlineData(@"H:\ColorVision\Data\Capture.cvraw", (int)StartupUpdatePackageKind.None)]
    [InlineData(null, (int)StartupUpdatePackageKind.None)]
    public void StartupUpdatePackageClassificationRunsBeforeNormalResourceRouting(
        string? inputPath,
        int expected)
    {
        Assert.Equal((StartupUpdatePackageKind)expected, StartupUpdatePackageHandler.Classify(inputPath));
    }
}

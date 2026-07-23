using ColorVision.Engine.Impl.SolutionImpl;
using ColorVision.UI;
using System.ComponentModel;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class StartupFileOpenPolicyTests
{
    [Theory]
    [InlineData(@"H:\ColorVision\Data\Measurement.cvcie", true)]
    [InlineData(@"H:\ColorVision\Data\Capture.cvraw", true)]
    [InlineData(@"H:\ColorVision\Data\CAPTURE.CVRAW", true)]
    [InlineData(@"H:\ColorVision\Update\ColorVision.cvx", false)]
    [InlineData(@"H:\ColorVision\Update\ColorVision.cvxp", false)]
    [InlineData(@"H:\ColorVision\Data\Image.tif", false)]
    [InlineData(null, false)]
    public void OnlyCvRawAndCvCieUseStandaloneStartupOpen(string? inputPath, bool expected)
    {
        Assert.Equal(expected, StartupFileOpenPolicy.ShouldOpenBeforeMainWindow(inputPath));
    }

    [Fact]
    public void CvRawStandaloneProcessorIsRegisteredForBothExtensions()
    {
        FileExtensionAttribute extensionAttribute = typeof(CVRawStandaloneFileProcessor)
            .GetCustomAttribute<FileExtensionAttribute>()!;

        Assert.Contains(extensionAttribute.Extensions, extension =>
            string.Equals(extension, ".cvraw", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(extensionAttribute.Extensions, extension =>
            string.Equals(extension, ".cvcie", StringComparison.OrdinalIgnoreCase));
    }
}

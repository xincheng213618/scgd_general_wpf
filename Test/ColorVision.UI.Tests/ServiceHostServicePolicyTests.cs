using ColorVisionServiceHost;
using System.IO;

namespace ColorVision.UI.Tests;

public class ServiceHostServicePolicyTests
{
    [Theory]
    [InlineData("ArchivedWindowsService.exe")]
    [InlineData("RegWindowsService.exe")]
    public void ArchiveServiceAllowsCurrentAndLegacyExecutables(string executableName)
    {
        Assert.True(ServiceHostCommandHandler.IsAllowedServiceExecutable(
            "CVArchService",
            Path.Combine(@"C:\ColorVision\RegWindowsService", executableName)));
    }

    [Fact]
    public void ArchiveServiceRejectsAnotherServicesExecutable()
    {
        Assert.False(ServiceHostCommandHandler.IsAllowedServiceExecutable(
            "CVArchService",
            @"C:\ColorVision\CVMainWindowsService_x64\CVMainWindowsService_x64.exe"));
    }
}

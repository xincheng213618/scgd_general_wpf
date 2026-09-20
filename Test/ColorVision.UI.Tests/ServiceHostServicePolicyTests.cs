using ColorVisionServiceHost;
using System.IO;
using System.ServiceProcess;

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

    [Fact]
    public void ServiceLookupDisposesEveryControllerAfterFindingAMatch()
    {
        ServiceController[] services =
        [
            new ServiceController { ServiceName = "UnrelatedService" },
            new ServiceController { ServiceName = "CVArchService" },
            new ServiceController { ServiceName = "AnotherService" },
        ];
        int disposedCount = 0;
        foreach (ServiceController service in services)
        {
            service.Disposed += (_, _) => disposedCount++;
        }

        bool found = ServiceHostCommandHandler.ContainsServiceAndDispose(services, "CVArchService");

        Assert.True(found);
        Assert.Equal(services.Length, disposedCount);
    }
}

using ColorVisionServiceHost;
using System.IO;
using System.ServiceProcess;

namespace ColorVision.UI.Tests;

public class ServiceHostServicePolicyTests
{
    [Theory]
    [InlineData("RegistrationCenterService", "RegWindowsService.exe")]
    [InlineData("CVMainService_x64", "CVMainWindowsService_x64.exe")]
    [InlineData("CVMainService_dev", "CVMainWindowsService_dev.exe")]
    public void ManagedServicesAllowTheirOwnExecutables(string serviceName, string executableName)
    {
        Assert.True(ServiceHostCommandHandler.IsAllowedServiceUninstall(serviceName));
        Assert.True(ServiceHostCommandHandler.IsAllowedServiceExecutable(
            serviceName,
            Path.Combine(@"C:\ColorVision", executableName)));
    }

    [Theory]
    [InlineData("CVArchService")]
    [InlineData("cvarchservice")]
    public void RetiredArchiveServiceOnlyAllowsUninstall(string serviceName)
    {
        Assert.True(ServiceHostCommandHandler.IsAllowedServiceUninstall(serviceName));
        Assert.False(ServiceHostCommandHandler.IsAllowedServiceName(serviceName));
        Assert.False(ServiceHostCommandHandler.IsAllowedServiceExecutable(
            serviceName,
            @"C:\ColorVision\RegWindowsService\ArchivedWindowsService.exe"));
        Assert.False(ServiceHostCommandHandler.IsAllowedServiceExecutable(
            serviceName,
            @"C:\ColorVision\RegWindowsService\RegWindowsService.exe"));
    }

    [Fact]
    public void ManagedServiceRejectsAnotherServicesExecutable()
    {
        Assert.False(ServiceHostCommandHandler.IsAllowedServiceExecutable(
            "RegistrationCenterService",
            @"C:\ColorVision\CVMainWindowsService_x64\CVMainWindowsService_x64.exe"));
    }

    [Fact]
    public void ServiceLookupDisposesEveryControllerAfterFindingAMatch()
    {
        ServiceController[] services =
        [
            new ServiceController { ServiceName = "UnrelatedService" },
            new ServiceController { ServiceName = "RegistrationCenterService" },
            new ServiceController { ServiceName = "AnotherService" },
        ];
        int disposedCount = 0;
        foreach (ServiceController service in services)
        {
            service.Disposed += (_, _) => disposedCount++;
        }

        bool found = ServiceHostCommandHandler.ContainsServiceAndDispose(services, "RegistrationCenterService");

        Assert.True(found);
        Assert.Equal(services.Length, disposedCount);
    }
}

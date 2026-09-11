using System.Reflection;
using WindowsServicePlugin.ServiceManager;

namespace ColorVision.UI.Tests;

public sealed class WindowsServiceConfigPolicyTests
{
    private static readonly MethodInfo BuildMonitorServices = typeof(ServiceManagerViewModel).GetMethod(
        "BuildRegistrationCenterMonitorServices",
        BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Monitor service policy was not found.");

    [Theory]
    [InlineData(false, "MySQL,CVMainService_x64,CVMainService_dev")]
    [InlineData(true, "MySQL,CVMainService_x64,CVMainService_dev,CVArchService")]
    public void MonitorListIncludesArchiveServiceOnlyWhenInstalled(bool archiveServiceInstalled, string expected)
    {
        string actual = Assert.IsType<string>(BuildMonitorServices.Invoke(null, [archiveServiceInstalled]));

        Assert.Equal(expected, actual);
    }
}

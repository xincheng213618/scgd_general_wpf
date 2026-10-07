using ColorVision.Engine;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Types;

namespace ColorVision.UI.Tests;

public sealed class FileServerCompatibilityTests
{
    [Fact]
    public void RetiredFileServerResourcesAreSkippedWithoutChangingTheirConfiguration()
    {
        const string legacyConfig = """{"Code":"DEV.FileServer.Default","Endpoint":"127.0.0.1","PortRange":"6500-6505","FileBasePath":"D:\\CVTest"}""";
        var resource = new SysResourceModel { Id = 42, Pid = 1, Type = 6, Code = "DEV.FileServer.Default", Value = legacyConfig };

        Assert.Equal(6, (int)ServiceTypes.FileServer);
        Assert.Equal(6, (int)CVServiceType.FileServer);
        Assert.False(DeviceServiceFactoryRegistry.TryGetFactory(ServiceTypes.FileServer, out _));
        Assert.Null(DeviceServiceFactoryRegistry.CreateService(resource));
        Assert.Equal(legacyConfig, resource.Value);
        Assert.Equal(6, resource.Type);
        Assert.Equal(42, resource.Id);
        Assert.Equal(1, resource.Pid);
    }
}

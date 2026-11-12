using ColorVision.Engine;
using ColorVision.Engine.Cache;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Types;
using ColorVision.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests;

public sealed class FileServerCompatibilityTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(3)]
    [InlineData(365)]
    public void FileRetentionKeepsExplicitDisableValueWhenLoadingLegacyConfiguration(int? legacyDays)
    {
        var legacy = new JObject
        {
            ["DataBasePath"] = @"E:\InspectionData",
            ["Endpoint"] = "127.0.0.2",
            ["PortRange"] = "6600-6605"
        };
        if (legacyDays.HasValue) legacy["SaveDays"] = legacyDays.Value;

        var config = JsonConvert.DeserializeObject<FileServerCfg>(legacy.ToString())!;
        Assert.Equal(0u, config.SaveDays);

        var settings = new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore };
        var payload = JObject.Parse(JsonConvert.SerializeObject(new { FileServerCfg = config }, settings));
        var saved = Assert.IsType<JObject>(payload["FileServerCfg"]);
        Assert.Equal(0u, saved["SaveDays"]!.Value<uint>());
        Assert.Equal(legacy["DataBasePath"], saved["DataBasePath"]);
        Assert.Equal(legacy["Endpoint"], saved["Endpoint"]);
        Assert.Equal(legacy["PortRange"], saved["PortRange"]);

        var restored = JsonConvert.DeserializeObject<FileServerCfg>(saved.ToString())!;
        restored.SaveDays = 3;
        Assert.Equal(0u, restored.SaveDays);
    }

    [Fact]
    public void FileSettingsEditorKeepsDataPathWithoutRetentionDays()
    {
        var properties = PropertyEditorHelper.GetEditableProperties(typeof(FileServerCfg));

        Assert.Contains(properties, property => property.Name == nameof(FileServerCfg.DataBasePath));
        Assert.DoesNotContain(properties, property => property.Name == nameof(FileServerCfg.SaveDays));
    }

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

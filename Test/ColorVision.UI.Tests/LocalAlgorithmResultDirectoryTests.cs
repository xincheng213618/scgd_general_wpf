using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests;

public sealed class LocalAlgorithmResultDirectoryTests
{
    [Fact]
    public void LegacyAlgorithmDeviceFieldsAreIgnoredAndPoiCctWaveDefaultsOn()
    {
        ConfigAlgorithm config = JsonConvert.DeserializeObject<ConfigAlgorithm>("{\"Code\":\"algorithm\",\"IsCCTWave\":false,\"POI_DBMaxNum\":10000000}")!;
        JObject serialized = JObject.FromObject(config);

        Assert.Equal("algorithm", config.Code);
        Assert.Null(serialized.Property("IsCCTWave"));
        Assert.Null(serialized.Property("POI_DBMaxNum"));
        Assert.True(new PoiDisplayAlgorithmConfig().IsCCTWave);
    }

    [Fact]
    public void UsesConfiguredDefaultServiceRootAndRunDate()
    {
        var config = new ConfigAlgorithm { Code = "DEV.Algorithm.Default", Name = "显示名称" };
        config.FileServerCfg.DataBasePath = @"D:\CVTest";
        Assert.Equal(@"D:\CVTest\DEV.Algorithm.Default\Data\2026-09-18", LocalAlgorithmResultDirectory.Resolve("", [config], new(2026,9,18)));
        config.FileServerCfg.DataBasePath = @"E:\NewRoot";
        Assert.Equal(@"E:\NewRoot\DEV.Algorithm.Default\Data\2026-09-19", LocalAlgorithmResultDirectory.Resolve("", [config], new(2026,9,19)));
    }
    [Fact]
    public void OverrideDoesNotRequireServiceAndDefaultWinsAmongServices()
    {
        Assert.Equal(@"C:\Results", LocalAlgorithmResultDirectory.Resolve(@"C:\Results", [], DateTime.Today));
        var first = new ConfigAlgorithm { Code = "DEV.Algorithm.Other" };
        var preferred = new ConfigAlgorithm { Code = "DEV.Algorithm.Default" };
        preferred.FileServerCfg.DataBasePath = @"E:\Chosen";
        Assert.Equal(@"E:\Chosen\DEV.Algorithm.Default\Data\2026-09-18", LocalAlgorithmResultDirectory.Resolve("", [first, preferred], new(2026,9,18)));
        Assert.Throws<InvalidOperationException>(() => LocalAlgorithmResultDirectory.Resolve("", [], DateTime.Today));
        Assert.Throws<InvalidOperationException>(() => LocalAlgorithmResultDirectory.Resolve("", [first, new ConfigAlgorithm { Code = "Second" }], DateTime.Today));
    }
}

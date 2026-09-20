using ColorVision.Engine;

namespace ProjectARVRPro.Tests;

public sealed class ResultViewRefreshManagerTests
{
    [Fact]
    public void SettingRemainsDraftUntilAppliedAndCanRestoreOriginalValue()
    {
        var config = new ExampleViewConfig { AutoRefreshView = true };
        var item = new ResultViewRefreshSettingItem(config, "示例结果视图", ["相机 A", "相机 B"]);

        Assert.True(item.IsWarning);
        Assert.Equal(2, item.AffectedCount);

        item.IsAutoRefreshEnabled = false;

        Assert.True(config.AutoRefreshView);
        Assert.False(item.IsWarning);

        item.Apply();
        Assert.False(config.AutoRefreshView);

        item.Restore();
        Assert.True(config.AutoRefreshView);
    }

    [Fact]
    public void UnloadedViewDoesNotRaiseRuntimeWarning()
    {
        var config = new ExampleViewConfig { AutoRefreshView = true };
        var item = new ResultViewRefreshSettingItem(config, "示例结果视图", []);

        Assert.False(item.IsWarning);
        Assert.Equal("未加载", item.StatusText);
        Assert.Contains("下次启动", item.InstanceSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void DisplayControlConventionFindsMatchingRefreshConfiguration()
    {
        Type? resolved = ResultViewRefreshDiscovery.ResolveDisplayConfigType(
            "DisplayCamera",
            [typeof(ViewCameraConfig), typeof(ViewAlgorithmConfig)]);

        Assert.Equal(typeof(ViewCameraConfig), resolved);
        Assert.Null(ResultViewRefreshDiscovery.ResolveDisplayConfigType(
            "DisplayUnknown",
            [typeof(ViewCameraConfig), typeof(ViewAlgorithmConfig)]));
    }

    [Fact]
    public void LazilyDiscoveredConfigDoesNotRequirePreseededInstanceBucket()
    {
        var instances = new Dictionary<Type, Dictionary<string, string>>();

        ResultViewRefreshDiscovery.AddInstance(
            instances,
            typeof(ViewCameraConfig),
            "camera:729",
            "729 相机");

        Assert.Equal("729 相机", instances[typeof(ViewCameraConfig)]["camera:729"]);
    }

    private sealed class ExampleViewConfig : ViewConfigBase { }
    private sealed class ViewCameraConfig : ViewConfigBase { }
    private sealed class ViewAlgorithmConfig : ViewConfigBase { }
}

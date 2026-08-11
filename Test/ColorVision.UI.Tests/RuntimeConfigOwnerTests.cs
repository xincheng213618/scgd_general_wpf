using ColorVision.UI;

namespace ColorVision.UI.Tests;

public sealed class RuntimeConfigOwnerTests
{
    [Fact]
    public void ReloadUsesNewSavePathOnlyForNextTaskSnapshot()
    {
        var notifier = new TestConfigReloadNotifier();
        var configA = new SavePathConfig { SavePath = "A" };
        var configB = new SavePathConfig { SavePath = "B" };
        SavePathConfig current = configA;

        using var owner = new RuntimeConfigOwner<SavePathConfig>(() => current, notifier);
        SavePathConfig runningTask = owner.Capture();

        current = configB;
        notifier.RaiseConfigsReloaded();

        Assert.Same(configA, runningTask);
        Assert.Equal("A", runningTask.SavePath);
        Assert.Same(configB, owner.Current);
        Assert.Equal("B", owner.Capture().SavePath);
    }

    private sealed class SavePathConfig : IConfig
    {
        public string SavePath { get; set; } = string.Empty;
    }

    private sealed class TestConfigReloadNotifier : IConfigReloadNotifier
    {
        public event EventHandler? ConfigsReloaded;

        public void RaiseConfigsReloaded() => ConfigsReloaded?.Invoke(this, EventArgs.Empty);
    }
}

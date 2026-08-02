using ColorVision.Copilot;
using System.Collections.ObjectModel;

namespace ColorVision.UI.Tests;

public sealed class CopilotTemporaryProfileSourceTests
{
    [Fact]
    public void SyncRemovesOnlyTheExpiredBuiltInTrialProfile()
    {
        var retained = new CopilotProfileConfig { Id = "custom-profile" };
        var expired = new CopilotProfileConfig { Id = "builtin-minimax-trial-20260527" };
        var profiles = new ObservableCollection<CopilotProfileConfig> { retained, expired };

        var changed = CopilotTemporaryProfileSource.Sync(profiles);

        Assert.True(changed);
        Assert.Same(retained, Assert.Single(profiles));
    }

    [Fact]
    public void SyncReportsNoChangeWhenTheExpiredProfileIsAbsent()
    {
        var retained = new CopilotProfileConfig { Id = "custom-profile" };
        var profiles = new ObservableCollection<CopilotProfileConfig> { retained };

        var changed = CopilotTemporaryProfileSource.Sync(profiles);

        Assert.False(changed);
        Assert.Same(retained, Assert.Single(profiles));
    }
}

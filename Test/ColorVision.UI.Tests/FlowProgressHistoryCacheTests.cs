using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;

namespace ColorVision.UI.Tests;

public sealed class FlowProgressHistoryCacheTests
{
    [Fact]
    public void ReusesStableFlowIdentityAndEvictsTheLeastRecentlyUsedFlow()
    {
        var cache = new FlowProgressHistoryCache();
        var original = cache.GetOrAdd(new FlowIdentity(1, "flow:original", "Original"));
        original.FlowElapsedMs = 123;
        for (int index = 0; index < 150; index++)
            original.NodeDurations[index.ToString()] = index + 1;

        var oldestIdentity = new FlowIdentity(2, "flow:oldest", "Oldest");
        var oldest = cache.GetOrAdd(oldestIdentity);
        for (int index = 2; index < 100; index++)
            cache.GetOrAdd(new FlowIdentity(0, $"flow:{index}", null));

        var renamed = cache.GetOrAdd(new FlowIdentity(101, "flow:original", "Renamed"));
        Assert.Same(original, renamed);
        cache.GetOrAdd(new FlowIdentity(0, "flow:new", null));

        Assert.Same(original, cache.GetOrAdd(new FlowIdentity(1, "flow:original", "Original")));
        Assert.Equal(123, original.FlowElapsedMs);
        Assert.Equal(150, original.NodeDurations.Count);
        Assert.NotSame(oldest, cache.GetOrAdd(oldestIdentity));
    }
}

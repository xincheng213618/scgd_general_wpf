using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class FlowRuntimeEstimateCacheTests
{
    [Fact]
    public void ColdStartHasNoEstimateAndCompletedRunsUpdateIt()
    {
        var cache = new FlowRuntimeEstimateCache();
        var key = new FlowRuntimeEstimateKey("White51", "canvas-v1");
        Assert.Equal(0, cache.GetElapsed(key));
        cache.RecordCompleted(key, 2500);
        Assert.Equal(2500, cache.GetElapsed(key));
        cache.RecordCompleted(key, 2100);
        Assert.Equal(2100, cache.GetElapsed(key));
    }

    [Fact]
    public void EstimatesAreIsolatedByNameContentAndWindow()
    {
        var cache = new FlowRuntimeEstimateCache();
        var original = new FlowRuntimeEstimateKey("White51", "canvas-v1");
        cache.RecordCompleted(original, 2000);
        Assert.Equal(0, cache.GetElapsed(new("White255", "canvas-v1")));
        Assert.Equal(0, cache.GetElapsed(new("White51", "canvas-v2")));
        Assert.Equal(0, new FlowRuntimeEstimateCache().GetElapsed(original));
        cache.RecordCompleted(new("White51", "canvas-v2"), 3000);
        Assert.Equal(0, cache.GetElapsed(original));
        Assert.Equal(3000, cache.GetElapsed(new("White51", "canvas-v2")));
    }

    [Fact]
    public void RuntimeVariantsDoNotShareEstimateForTheSameTemplateAndCanvas()
    {
        var cache = new FlowRuntimeEstimateCache();
        var first = new FlowRuntimeEstimateKey("White51", "canvas-v1", "exposure=15");
        var second = new FlowRuntimeEstimateKey("White51", "canvas-v1", "exposure=100");

        cache.RecordCompleted(first, 2000);

        Assert.Equal(2000, cache.GetElapsed(first));
        Assert.Equal(0, cache.GetElapsed(second));

        cache.RecordCompleted(second, 3000);

        Assert.Equal(0, cache.GetElapsed(first));
        Assert.Equal(3000, cache.GetElapsed(second));
    }

    [Fact]
    public void InvalidDurationsDoNotReplaceTheLastCompletedEstimate()
    {
        var cache = new FlowRuntimeEstimateCache();
        var key = new FlowRuntimeEstimateKey("White51", null);
        cache.RecordCompleted(key, 2000);
        cache.RecordCompleted(key, 0);
        cache.RecordCompleted(key, -1);
        cache.RecordCompleted(default, 1000);
        Assert.Equal(2000, cache.GetElapsed(key));
        Assert.Equal(0, cache.GetElapsed(default));
    }
}

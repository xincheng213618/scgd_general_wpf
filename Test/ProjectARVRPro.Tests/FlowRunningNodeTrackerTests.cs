using ColorVision.UI.Controls;

namespace ProjectARVRPro.Tests;

public sealed class FlowRunningNodeTrackerTests
{
    [Fact]
    public void ParallelNodesStayVisibleUntilEachOneEnds()
    {
        var tracker = new FlowRunningNodeTracker();
        tracker.Reset("run");
        Assert.True(tracker.NodeStarted("run", "camera", "相机", "a"));
        Assert.True(tracker.NodeStarted("run", "algorithm", "算法", "b"));

        var status = FlowExecutionStatusInfo.Running("并行流程", tracker.GetRunningNodeNames(), 100, 2000);
        Assert.Equal("正在执行：相机, 算法", status.Message);
        Assert.Contains("当前节点：相机, 算法", status.Details);

        Assert.True(tracker.NodeEnded("run", "algorithm", "b"));
        Assert.Equal("相机", tracker.GetRunningNodeNames());
        Assert.True(tracker.NodeEnded("run", "camera", "a"));
        Assert.Empty(tracker.GetRunningNodeNames());
        Assert.Equal("正在执行流程", FlowExecutionStatusInfo.Running("并行流程", tracker.GetRunningNodeNames(), 200, 2000).Message);
    }

    [Fact]
    public void OverlappingInvocationsKeepTheNodeVisibleAndIgnoreDuplicateCompletions()
    {
        var tracker = new FlowRunningNodeTracker();
        tracker.Reset("run");
        tracker.NodeStarted("run", "camera", "相机", "a");
        tracker.NodeStarted("run", "camera", "相机", "b");
        Assert.Equal("相机", tracker.GetRunningNodeNames());

        Assert.True(tracker.NodeEnded("run", "camera", "b"));
        Assert.Equal("相机", tracker.GetRunningNodeNames());
        Assert.False(tracker.NodeEnded("run", "camera", "b"));
        Assert.False(tracker.NodeEnded("run", "camera", "unknown"));
        Assert.Equal("相机", tracker.GetRunningNodeNames());
        Assert.True(tracker.NodeEnded("run", "camera", "a"));
        Assert.Empty(tracker.GetRunningNodeNames());
    }

    [Fact]
    public void IdenticalTitlesBelongToSeparateNodes()
    {
        var tracker = new FlowRunningNodeTracker();
        tracker.Reset("run");
        tracker.NodeStarted("run", "left-camera", "相机", "a");
        tracker.NodeStarted("run", "right-camera", "相机", "b");
        Assert.Equal("相机, 相机", tracker.GetRunningNodeNames());
        tracker.NodeEnded("run", "left-camera", "a");
        Assert.Equal("相机", tracker.GetRunningNodeNames());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("request", null)]
    [InlineData(null, "response")]
    public void LegacyEventsWithoutMessageIdsCanStillComplete(string? requestId, string? responseId)
    {
        var tracker = new FlowRunningNodeTracker();
        tracker.Reset("run");
        tracker.NodeStarted("run", "camera", "相机", requestId);
        Assert.True(tracker.NodeEnded("run", "camera", responseId));
        Assert.Empty(tracker.GetRunningNodeNames());
    }

    [Fact]
    public void CompletionAndResetRejectLateEventsFromOtherRuns()
    {
        var tracker = new FlowRunningNodeTracker();
        tracker.Reset("old");
        tracker.NodeStarted("old", "camera", "上一轮相机", "a");
        Assert.Equal("上一轮相机", tracker.CompleteRun());
        Assert.Empty(tracker.GetRunningNodeNames());
        Assert.False(tracker.NodeStarted("old", "algorithm", "旧算法", "b"));
        Assert.False(tracker.NodeEnded("old", "camera", "a"));
        Assert.Equal("上一轮相机", tracker.CompleteRun());

        tracker.Reset("new");
        tracker.NodeStarted("new", "camera", "本轮相机", "new-a");
        Assert.False(tracker.NodeStarted("old", "algorithm", "旧算法", "b"));
        Assert.False(tracker.NodeEnded("old", "camera", "a"));
        Assert.Equal("本轮相机", tracker.GetRunningNodeNames());

        tracker.Reset(null);
        Assert.Empty(tracker.GetRunningNodeNames());
        Assert.False(tracker.NodeStarted("new", "camera", "本轮相机", "new-a"));
        Assert.Empty(tracker.CompleteRun());
    }

    [Fact]
    public void ConcurrentNodeCallbacksDoNotLoseRunningNodes()
    {
        var tracker = new FlowRunningNodeTracker();
        tracker.Reset("run");
        Parallel.For(0, 64, index => tracker.NodeStarted("run", index.ToString(), $"节点{index}", index.ToString()));
        Assert.Equal(64, tracker.GetRunningNodeNames().Split(", ").Length);

        Parallel.For(0, 64, index =>
        {
            Assert.True(tracker.NodeEnded("run", index.ToString(), index.ToString()));
            _ = tracker.GetRunningNodeNames();
        });
        Assert.Empty(tracker.GetRunningNodeNames());
    }
}

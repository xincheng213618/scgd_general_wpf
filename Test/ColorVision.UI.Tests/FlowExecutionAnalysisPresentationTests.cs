#pragma warning disable CA1707
using ColorVision.Engine.FlowProcessing.Diagnostics;

namespace ColorVision.UI.Tests;

public class FlowExecutionAnalysisPresentationTests
{
    [Fact]
    public void BuildDurationItems_RanksSingleBatchExecutionsByElapsedTime()
    {
        DateTime start = new DateTime(2026, 7, 26, 10, 0, 0);
        FlowNodeRecord fast = CreateRecord(1, 10, "fast", "同名节点", start, 100);
        FlowNodeRecord slow = CreateRecord(2, 10, "slow", "同名节点", start.AddMilliseconds(100), 300);

        IReadOnlyList<FlowNodeDurationAnalysis> items =
            FlowExecutionAnalysisPresentation.BuildDurationItems(
                new[] { fast, slow },
                start.AddSeconds(1),
                warningThresholdMs: 1000);

        Assert.Equal(2, items.Count);
        Assert.Same(slow, items[0].Record);
        Assert.Equal(1, items[0].Rank);
        Assert.Equal(75d, items[0].ShareOfNodeWorkPercent, 5);
        Assert.Same(fast, items[1].Record);
    }

    [Fact]
    public void BuildDurationItems_UsesStableNodeIdAcrossBatchComparison()
    {
        DateTime start = new DateTime(2026, 7, 26, 10, 0, 0);
        FlowNodeRecord firstNodeBatch1 = CreateRecord(1, 10, "node-a", "同名节点", start, 100);
        FlowNodeRecord firstNodeBatch2 = CreateRecord(2, 11, "node-a", "同名节点", start, 300);
        FlowNodeRecord secondNodeBatch1 = CreateRecord(3, 10, "node-b", "同名节点", start, 600);
        FlowNodeRecord secondNodeBatch2 = CreateRecord(4, 11, "node-b", "同名节点", start, 800);

        IReadOnlyList<FlowNodeDurationAnalysis> items =
            FlowExecutionAnalysisPresentation.BuildDurationItems(
                new[] { firstNodeBatch1, firstNodeBatch2, secondNodeBatch1, secondNodeBatch2 },
                start.AddSeconds(2),
                warningThresholdMs: 5000);

        Assert.Equal(2, items.Count);
        Assert.Equal("node-b", items[0].NodeId);
        Assert.Equal(700, items[0].AverageElapsedMs);
        Assert.Equal("node-a", items[1].NodeId);
        Assert.Equal(200, items[1].AverageElapsedMs);
    }

    [Fact]
    public void BuildSummary_SeparatesActiveIdleAndParallelTime()
    {
        DateTime start = new DateTime(2026, 7, 26, 10, 0, 0);
        FlowNodeRecord first = CreateRecord(1, 10, "node-a", "A", start, 600);
        FlowNodeRecord parallel = CreateRecord(2, 10, "node-b", "B", start.AddMilliseconds(200), 600);
        FlowNodeRecord afterGap = CreateRecord(3, 10, "node-c", "C", start.AddMilliseconds(1000), 200);
        IReadOnlyList<FlowNodeRecord> records = new[] { first, parallel, afterGap };
        IReadOnlyList<FlowNodeDurationAnalysis> items =
            FlowExecutionAnalysisPresentation.BuildDurationItems(records, start.AddSeconds(2), 5000);

        FlowExecutionAnalysisSummary summary =
            FlowExecutionAnalysisPresentation.BuildSummary(records, items, start.AddSeconds(2));

        Assert.Equal(1200, summary.AverageWallClockMs);
        Assert.Equal(1000, summary.AverageActiveMs);
        Assert.Equal(200, summary.AverageIdleMs);
        Assert.Equal(400, summary.AverageOverlapMs);
        Assert.Equal(1400, summary.AverageNodeWorkMs);
    }

    [Fact]
    public void BuildDurationItems_RunningSlowNodeRetainsBothSignals()
    {
        DateTime start = new DateTime(2026, 7, 26, 10, 0, 0);
        var running = new FlowNodeRecord
        {
            Id = 1,
            BatchId = 10,
            NodeId = "running",
            NodeName = "运行节点",
            StartTime = start
        };

        FlowNodeDurationAnalysis item =
            FlowExecutionAnalysisPresentation.BuildDurationItems(
                new[] { running },
                start.AddSeconds(31),
                warningThresholdMs: 30000)[0];

        Assert.True(item.IsRunning);
        Assert.True(item.IsWarning);
        Assert.Equal(31000, item.AverageElapsedMs);
    }

    private static FlowNodeRecord CreateRecord(
        int id,
        int batchId,
        string nodeId,
        string nodeName,
        DateTime start,
        long elapsedMs)
    {
        return new FlowNodeRecord
        {
            Id = id,
            BatchId = batchId,
            NodeId = nodeId,
            NodeName = nodeName,
            StartTime = start,
            EndTime = start.AddMilliseconds(elapsedMs),
            ElapsedMs = elapsedMs
        };
    }
}

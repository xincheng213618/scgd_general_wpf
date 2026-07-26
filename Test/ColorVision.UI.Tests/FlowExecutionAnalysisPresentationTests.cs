#pragma warning disable CA1707
using ColorVision.Engine.FlowProcessing.Diagnostics;

namespace ColorVision.UI.Tests;

public class FlowExecutionAnalysisPresentationTests
{
    [Fact]
    public void BuildDurationItems_RanksRecordsByDuration()
    {
        DateTime start = new DateTime(2026, 7, 27, 10, 0, 0);
        FlowNodeRecord fast = CreateRecord(1, 260, "run-a", "node-a", "取图", start, 100);
        FlowNodeRecord slow = CreateRecord(2, 260, "run-a", "node-b", "校正", start.AddMilliseconds(100), 300);

        IReadOnlyList<FlowNodeDurationAnalysis> items =
            FlowExecutionAnalysisPresentation.BuildDurationItems(
                new[] { fast, slow },
                start.AddSeconds(1),
                warningThresholdMs: 1000);

        Assert.Equal(2, items.Count);
        Assert.Same(slow, items[0].Record);
        Assert.Equal(300, items[0].ElapsedMs);
        Assert.Equal(75d, items[0].ShareOfNodeWorkPercent, 5);
        Assert.Same(fast, items[1].Record);
    }

    [Fact]
    public void BuildDurationItems_KeepsRepeatedNodeRecordsSeparate()
    {
        DateTime start = new DateTime(2026, 7, 27, 10, 0, 0);
        FlowNodeRecord first = CreateRecord(1, 260, "run-a", "same-node", "循环节点", start, 100);
        FlowNodeRecord second = CreateRecord(2, 260, "run-a", "same-node", "循环节点", start.AddSeconds(1), 250);

        IReadOnlyList<FlowNodeDurationAnalysis> items =
            FlowExecutionAnalysisPresentation.BuildDurationItems(
                new[] { first, second },
                start.AddSeconds(2),
                warningThresholdMs: 1000);

        Assert.Equal(2, items.Count);
        Assert.Same(second, items[0].Record);
        Assert.Same(first, items[1].Record);
    }

    [Fact]
    public void BuildSummary_SeparatesActiveIdleAndParallelTime()
    {
        DateTime start = new DateTime(2026, 7, 27, 10, 0, 0);
        FlowNodeRecord first = CreateRecord(1, 260, "run-a", "node-a", "A", start, 600);
        FlowNodeRecord parallel = CreateRecord(2, 260, "run-a", "node-b", "B", start.AddMilliseconds(200), 600);
        FlowNodeRecord afterGap = CreateRecord(3, 260, "run-a", "node-c", "C", start.AddMilliseconds(1000), 200);
        IReadOnlyList<FlowNodeRecord> records = new[] { first, parallel, afterGap };
        IReadOnlyList<FlowNodeDurationAnalysis> items =
            FlowExecutionAnalysisPresentation.BuildDurationItems(records, start.AddSeconds(2), 5000);

        FlowExecutionAnalysisSummary summary =
            FlowExecutionAnalysisPresentation.BuildSummary(records, items, start.AddSeconds(2));

        Assert.Equal(1200, summary.WallClockMs);
        Assert.Equal(1000, summary.ActiveMs);
        Assert.Equal(200, summary.IdleMs);
        Assert.Equal(400, summary.OverlapMs);
        Assert.Equal(1400, summary.NodeWorkMs);
    }

    [Fact]
    public void BuildDurationItems_RunningSlowNodeRetainsBothSignals()
    {
        DateTime start = new DateTime(2026, 7, 27, 10, 0, 0);
        var running = new FlowNodeRecord
        {
            Id = 1,
            BatchId = 260,
            SerialNumber = "run-a",
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
        Assert.Equal(31000, item.ElapsedMs);
    }

    [Fact]
    public void GetMessagesForNodeExecution_PrefersRecordIdAssociation()
    {
        DateTime start = new DateTime(2026, 7, 27, 10, 0, 0);
        FlowNodeRecord record = CreateRecord(10, 260, "run-a", "same-node", "循环节点", start, 100);
        FlowNodeMessage exact = CreateMessage(1, 260, "run-a", "same-node", start.AddMilliseconds(10));
        exact.NodeRecordId = record.Id;
        FlowNodeMessage otherInvocation = CreateMessage(2, 260, "run-a", "same-node", start.AddMilliseconds(20));
        otherInvocation.NodeRecordId = 11;

        IReadOnlyList<FlowNodeMessage> result =
            FlowExecutionAnalysisPresentation.GetMessagesForNodeExecution(
                record,
                new[] { otherInvocation, exact });

        Assert.Single(result);
        Assert.Same(exact, result[0]);
    }

    [Fact]
    public void GetMessagesForNodeExecution_LegacyFallbackUsesRunAndTimeWindow()
    {
        DateTime start = new DateTime(2026, 7, 27, 10, 0, 0);
        FlowNodeRecord record = CreateRecord(10, 260, "run-a", "same-node", "循环节点", start, 100);
        FlowNodeMessage matching = CreateMessage(1, 260, "run-a", "same-node", start.AddMilliseconds(10));
        FlowNodeMessage wrongRun = CreateMessage(2, 260, "run-b", "same-node", start.AddMilliseconds(20));
        FlowNodeMessage wrongInvocation = CreateMessage(3, 260, "run-a", "same-node", start.AddSeconds(2));

        IReadOnlyList<FlowNodeMessage> result =
            FlowExecutionAnalysisPresentation.GetMessagesForNodeExecution(
                record,
                new[] { matching, wrongRun, wrongInvocation });

        Assert.Single(result);
        Assert.Same(matching, result[0]);
    }

    [Fact]
    public void GetMessagesForNodeExecution_LegacyFallbackAssignsRepeatedNodeMessageOnce()
    {
        DateTime start = new DateTime(2026, 7, 27, 10, 0, 0);
        FlowNodeRecord first = CreateRecord(10, 260, "run-a", "same-node", "循环节点", start, 400);
        FlowNodeRecord second = CreateRecord(
            11,
            260,
            "run-a",
            "same-node",
            "循环节点",
            start.AddMilliseconds(300),
            400);
        FlowNodeMessage message = CreateMessage(
            1,
            260,
            "run-a",
            "same-node",
            start.AddMilliseconds(310));
        IReadOnlyList<FlowNodeRecord> records = new[] { first, second };

        IReadOnlyList<FlowNodeMessage> firstMessages =
            FlowExecutionAnalysisPresentation.GetMessagesForNodeExecution(
                first,
                new[] { message },
                records);
        IReadOnlyList<FlowNodeMessage> secondMessages =
            FlowExecutionAnalysisPresentation.GetMessagesForNodeExecution(
                second,
                new[] { message },
                records);

        Assert.Empty(firstMessages);
        Assert.Single(secondMessages);
        Assert.Same(message, secondMessages[0]);
    }

    private static FlowNodeRecord CreateRecord(
        int id,
        int batchId,
        string serialNumber,
        string nodeId,
        string nodeName,
        DateTime start,
        long elapsedMs)
    {
        return new FlowNodeRecord
        {
            Id = id,
            BatchId = batchId,
            SerialNumber = serialNumber,
            NodeId = nodeId,
            NodeName = nodeName,
            StartTime = start,
            EndTime = start.AddMilliseconds(elapsedMs),
            ElapsedMs = elapsedMs
        };
    }

    private static FlowNodeMessage CreateMessage(
        int id,
        int batchId,
        string serialNumber,
        string nodeId,
        DateTime sendTime)
    {
        return new FlowNodeMessage
        {
            Id = id,
            BatchId = batchId,
            SerialNumber = serialNumber,
            NodeId = nodeId,
            NodeName = "循环节点",
            SendTime = sendTime,
            State = FlowMessageState.Sent
        };
    }
}

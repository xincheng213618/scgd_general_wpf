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
    public void BuildDurationItems_IncompleteNodeIsTimeoutWithoutDuration()
    {
        DateTime start = new DateTime(2026, 7, 27, 10, 0, 0);
        var incomplete = new FlowNodeRecord
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
                new[] { incomplete },
                start.AddSeconds(31),
                warningThresholdMs: 30000)[0];

        Assert.True(item.IsTimedOut);
        Assert.False(item.IsWarning);
        Assert.Equal(0, item.ElapsedMs);
        Assert.Equal("—", item.DurationText);
        Assert.Equal("—", item.ShareText);

        FlowExecutionAnalysisSummary summary =
            FlowExecutionAnalysisPresentation.BuildSummary(
                new[] { incomplete },
                new[] { item },
                start.AddDays(1));
        Assert.Equal(1, summary.TimeoutCount);
        Assert.Equal(0, summary.WallClockMs);
        Assert.Equal(0, summary.NodeWorkMs);
        Assert.Equal("—", summary.SlowestNodeName);
    }

    [Fact]
    public void BuildDurationItems_ExplicitTimeoutDoesNotEnterTimingDistribution()
    {
        DateTime start = new DateTime(2026, 7, 27, 10, 0, 0);
        FlowNodeRecord record = CreateRecord(
            1,
            260,
            "run-a",
            "same-node",
            "循环节点",
            start,
            5000);
        FlowNodeMessage timeout = CreateMessage(
            1,
            260,
            "run-a",
            "same-node",
            start.AddMilliseconds(10));
        timeout.NodeRecordId = record.Id;
        timeout.State = FlowMessageState.Timeout;
        timeout.StatusCode = -2;

        FlowNodeDurationAnalysis item =
            FlowExecutionAnalysisPresentation.BuildDurationItems(
                new[] { record },
                start.AddSeconds(6),
                warningThresholdMs: 30000,
                messages: new[] { timeout })[0];

        Assert.True(item.IsTimedOut);
        Assert.Equal(0, item.ElapsedMs);
        Assert.Equal("—", item.DurationText);

        FlowExecutionAnalysisSummary summary =
            FlowExecutionAnalysisPresentation.BuildSummary(
                new[] { record },
                new[] { item },
                start.AddSeconds(6));
        Assert.Equal(1, summary.TimeoutCount);
        Assert.Equal(0, summary.ActiveMs);
        Assert.Equal(0, summary.NodeWorkMs);
        Assert.Equal("—", summary.SlowestNodeName);
    }

    [Fact]
    public void BuildNodeHistorySummary_SeparatesResultsAndTimingBaselines()
    {
        DateTime start = new DateTime(2026, 7, 27, 10, 0, 0);
        FlowNodeRecord fastSuccess = CreateRecord(1, 260, "run-a", "same-node", "循环节点", start, 100);
        FlowNodeRecord slowSuccess = CreateRecord(2, 260, "run-a", "same-node", "循环节点", start.AddSeconds(1), 300);
        FlowNodeRecord failure = CreateRecord(3, 260, "run-a", "same-node", "循环节点", start.AddSeconds(2), 10000);
        var timedOut = new FlowNodeRecord
        {
            Id = 4,
            BatchId = 260,
            SerialNumber = "run-a",
            NodeId = "same-node",
            NodeName = "循环节点",
            StartTime = start.AddSeconds(3)
        };
        FlowNodeRecord unknown = CreateRecord(5, 260, "run-a", "same-node", "循环节点", start.AddSeconds(4), 500);

        FlowNodeMessage fastMessage = CreateMessage(1, 260, "run-a", "same-node", start.AddMilliseconds(10));
        fastMessage.NodeRecordId = fastSuccess.Id;
        fastMessage.State = FlowMessageState.Success;
        fastMessage.StatusCode = 0;
        FlowNodeMessage slowMessage = CreateMessage(2, 260, "run-a", "same-node", start.AddSeconds(1).AddMilliseconds(10));
        slowMessage.NodeRecordId = slowSuccess.Id;
        slowMessage.State = FlowMessageState.Success;
        slowMessage.StatusCode = 0;
        FlowNodeMessage failureMessage = CreateMessage(3, 260, "run-a", "same-node", start.AddSeconds(2).AddMilliseconds(10));
        failureMessage.NodeRecordId = failure.Id;
        failureMessage.State = FlowMessageState.Fail;
        failureMessage.StatusCode = 12;
        FlowNodeMessage timedOutMessage = CreateMessage(4, 260, "run-a", "same-node", start.AddSeconds(3).AddMilliseconds(10));
        timedOutMessage.NodeRecordId = timedOut.Id;

        IReadOnlyList<FlowNodeHistoryAnalysis> items =
            FlowExecutionAnalysisPresentation.BuildNodeHistoryItems(
                new[] { fastSuccess, slowSuccess, failure, timedOut, unknown },
                new[] { fastMessage, slowMessage, failureMessage, timedOutMessage },
                start.AddSeconds(5));
        FlowNodeHistorySummary summary =
            FlowExecutionAnalysisPresentation.BuildNodeHistorySummary(items);

        Assert.Equal(5, summary.TotalCount);
        Assert.Equal(2, summary.SuccessCount);
        Assert.Equal(2, summary.FailureCount);
        Assert.Equal(1, summary.TimeoutCount);
        Assert.Equal(1, summary.CompletedCount);
        Assert.Equal(200, summary.SuccessAverageMs);
        Assert.Equal(300, summary.SuccessP95Ms);
        Assert.Equal(10000, summary.FailureAverageMs);
        Assert.Equal(10000, summary.FailureP95Ms);
        Assert.Equal(50d, summary.SuccessRatePercent);
        FlowNodeHistoryAnalysis timeoutItem =
            items.Single(item => item.Record.Id == timedOut.Id);
        Assert.Equal("超时", timeoutItem.StatusText);
        Assert.Null(timeoutItem.ElapsedMs);
        Assert.Equal("—", timeoutItem.ElapsedText);
    }

    [Fact]
    public void GetNodeExecutionOutcome_RequiresAllMessagesToSucceed()
    {
        DateTime start = new DateTime(2026, 7, 27, 10, 0, 0);
        FlowNodeRecord record = CreateRecord(1, 260, "run-a", "same-node", "循环节点", start, 100);
        FlowNodeMessage success = CreateMessage(1, 260, "run-a", "same-node", start.AddMilliseconds(10));
        success.State = FlowMessageState.Success;
        success.StatusCode = 0;
        FlowNodeMessage pending = CreateMessage(2, 260, "run-a", "same-node", start.AddMilliseconds(20));

        FlowNodeExecutionOutcome outcome =
            FlowExecutionAnalysisPresentation.GetNodeExecutionOutcome(
                record,
                new[] { success, pending });

        Assert.Equal(FlowNodeExecutionOutcome.Completed, outcome);
    }

    [Fact]
    public void GetNodeExecutionOutcome_DoesNotCountCancellationAsFailure()
    {
        DateTime start =
            new DateTime(2026, 7, 31, 10, 0, 0);
        FlowNodeRecord record = CreateRecord(
            1,
            260,
            "run-a",
            "same-node",
            "循环节点",
            start,
            100);
        FlowNodeMessage canceled = CreateMessage(
            1,
            260,
            "run-a",
            "same-node",
            start.AddMilliseconds(10));
        canceled.State = FlowMessageState.Canceled;
        canceled.StatusCode = -4;

        FlowNodeExecutionOutcome outcome =
            FlowExecutionAnalysisPresentation.GetNodeExecutionOutcome(
                record,
                new[] { canceled });

        Assert.Equal(FlowNodeExecutionOutcome.Canceled, outcome);
    }

    [Fact]
    public void BuildNodeHistoryItems_LabelsTimeoutAsFailure()
    {
        DateTime start = new DateTime(2026, 7, 27, 10, 0, 0);
        FlowNodeRecord record = CreateRecord(1, 260, "run-a", "same-node", "循环节点", start, 100);
        FlowNodeMessage timeout = CreateMessage(1, 260, "run-a", "same-node", start.AddMilliseconds(10));
        timeout.NodeRecordId = record.Id;
        timeout.State = FlowMessageState.Fail;
        timeout.StatusCode = -2;

        FlowNodeHistoryAnalysis item =
            FlowExecutionAnalysisPresentation.BuildNodeHistoryItems(
                new[] { record },
                new[] { timeout },
                start.AddSeconds(1))[0];

        Assert.Equal(FlowNodeExecutionOutcome.Failed, item.Outcome);
        Assert.True(item.IsTimedOut);
        Assert.Equal("超时", item.StatusText);
        Assert.Null(item.ElapsedMs);
        Assert.Equal("—", item.ElapsedText);
    }

    [Fact]
    public void GetNodeExecutionOutcome_TreatsMissingEndAsTimeoutDespiteSuccessMessage()
    {
        DateTime start = new DateTime(2026, 7, 27, 10, 0, 0);
        var record = new FlowNodeRecord
        {
            Id = 1,
            BatchId = 260,
            SerialNumber = "run-a",
            NodeId = "same-node",
            NodeName = "循环节点",
            StartTime = start
        };
        FlowNodeMessage success = CreateMessage(1, 260, "run-a", "same-node", start.AddMilliseconds(10));
        success.NodeRecordId = record.Id;
        success.State = FlowMessageState.Success;
        success.StatusCode = 0;

        FlowNodeExecutionOutcome outcome =
            FlowExecutionAnalysisPresentation.GetNodeExecutionOutcome(
                record,
                new[] { success });

        Assert.Equal(FlowNodeExecutionOutcome.Failed, outcome);

        FlowNodeHistoryAnalysis item =
            FlowExecutionAnalysisPresentation.BuildNodeHistoryItems(
                new[] { record },
                new[] { success },
                start.AddSeconds(1))[0];
        Assert.True(item.IsTimedOut);
        Assert.Null(item.ElapsedMs);
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

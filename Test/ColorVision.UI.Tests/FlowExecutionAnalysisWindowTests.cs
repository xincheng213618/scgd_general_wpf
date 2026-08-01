using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Diagnostics;

namespace ColorVision.UI.Tests;

public class FlowExecutionAnalysisWindowTests
{
    [Fact]
    public void ResolveBatchSerialNumberPrefersExecutedCode()
    {
        var batch = new MeasureBatchModel
        {
            Name = "panel-sn",
            Code = "panel-sn_20260728122431",
        };

        string serialNumber = FlowExecutionAnalysisWindow.ResolveBatchSerialNumber(batch);

        Assert.Equal("panel-sn_20260728122431", serialNumber);
    }

    [Fact]
    public void ResolveBatchSerialNumberFallsBackToNameAndNodeRecord()
    {
        var namedBatch = new MeasureBatchModel { Name = "same-name-and-code" };
        var unnamedBatch = new MeasureBatchModel();
        var record = new FlowNodeRecord { SerialNumber = "recorded-run" };

        Assert.Equal(
            "same-name-and-code",
            FlowExecutionAnalysisWindow.ResolveBatchSerialNumber(namedBatch, record));
        Assert.Equal(
            "recorded-run",
            FlowExecutionAnalysisWindow.ResolveBatchSerialNumber(unnamedBatch, record));
    }

    [Fact]
    public void BuildFlowRunOrderGroupsByBatchAndSerialAndUsesFirstExecutionTime()
    {
        DateTime start = new(2026, 8, 1, 10, 0, 0);
        var records = new[]
        {
            new FlowNodeRecord { Id = 4, BatchId = 12, SerialNumber = "run-b", StartTime = start.AddSeconds(4) },
            new FlowNodeRecord { Id = 2, BatchId = 11, SerialNumber = "run-a", StartTime = start.AddSeconds(2) },
            new FlowNodeRecord { Id = 3, BatchId = 11, SerialNumber = "run-a", StartTime = start.AddSeconds(3) },
            new FlowNodeRecord { Id = 5, BatchId = 13, SerialNumber = null!, StartTime = start.AddSeconds(5) },
            new FlowNodeRecord { Id = 6, BatchId = 13, SerialNumber = "   ", StartTime = start.AddSeconds(6) }
        };

        IReadOnlyList<FlowRunNavigationItem> orderedRuns =
            FlowExecutionAnalysisWindow.BuildFlowRunOrder(records);

        Assert.Collection(
            orderedRuns,
            run => Assert.Equal((11, "run-a"), (run.BatchId, run.SerialNumber)),
            run => Assert.Equal((12, "run-b"), (run.BatchId, run.SerialNumber)),
            run => Assert.Equal((13, string.Empty), (run.BatchId, run.SerialNumber)));
    }

    [Fact]
    public void FindAdjacentRunIndexHonorsPreviousNextAndHistoryBoundaries()
    {
        DateTime start = new(2026, 8, 1, 10, 0, 0);
        IReadOnlyList<FlowRunNavigationItem> orderedRuns =
        [
            new(11, "run-a", start),
            new(12, "run-b", start.AddMinutes(1)),
            new(13, "run-c", start.AddMinutes(2))
        ];

        Assert.Equal(0, FlowExecutionAnalysisWindow.FindAdjacentRunIndex(orderedRuns, 12, "run-b", -1));
        Assert.Equal(2, FlowExecutionAnalysisWindow.FindAdjacentRunIndex(orderedRuns, 12, "run-b", 1));
        Assert.Equal(-1, FlowExecutionAnalysisWindow.FindAdjacentRunIndex(orderedRuns, 11, "run-a", -1));
        Assert.Equal(-1, FlowExecutionAnalysisWindow.FindAdjacentRunIndex(orderedRuns, 13, "run-c", 1));
        Assert.Equal(-1, FlowExecutionAnalysisWindow.FindAdjacentRunIndex(orderedRuns, 12, "unknown", 1));
        Assert.Equal(-1, FlowExecutionAnalysisWindow.FindAdjacentRunIndex(orderedRuns, 12, "run-b", 0));
    }

    [Fact]
    public void BuildSameFlowRunOrderUsesCompletedTimeAndResolvesBatch()
    {
        DateTime start = new(2026, 8, 1, 10, 0, 0);
        var flowRuns = new[]
        {
            new FlowRunRecord
            {
                SerialNumber = "run-new",
                CompletedTime = start.AddMinutes(3)
            },
            new FlowRunRecord
            {
                SerialNumber = "run-old",
                CompletedTime = start.AddMinutes(1)
            },
            new FlowRunRecord
            {
                SerialNumber = "run-without-node-record",
                CompletedTime = start.AddMinutes(2)
            }
        };
        var nodeRecords = new[]
        {
            new FlowNodeRecord
            {
                BatchId = 102,
                SerialNumber = "run-new",
                StartTime = start.AddMinutes(2)
            },
            new FlowNodeRecord
            {
                BatchId = 101,
                SerialNumber = "run-old",
                StartTime = start
            }
        };

        IReadOnlyList<FlowRunNavigationItem> orderedRuns =
            FlowExecutionAnalysisWindow.BuildSameFlowRunOrder(flowRuns, nodeRecords);

        Assert.Collection(
            orderedRuns,
            run =>
            {
                Assert.Equal(101, run.BatchId);
                Assert.Equal("run-old", run.SerialNumber);
                Assert.Equal(start.AddMinutes(1), run.ExecutedTime);
            },
            run =>
            {
                Assert.Equal(102, run.BatchId);
                Assert.Equal("run-new", run.SerialNumber);
                Assert.Equal(start.AddMinutes(3), run.ExecutedTime);
            });
    }

    [Fact]
    public void SameFlowNavigationUsesBatchAndSerialAsRunIdentity()
    {
        DateTime start = new(2026, 8, 1, 10, 0, 0);
        IReadOnlyList<FlowRunNavigationItem> orderedRuns =
        [
            new(101, "same-sn", start),
            new(102, "same-sn", start.AddMinutes(1)),
            new(103, "new-sn", start.AddMinutes(2))
        ];

        Assert.Equal(
            0,
            FlowExecutionAnalysisWindow.FindAdjacentRunIndex(
                orderedRuns,
                102,
                "same-sn",
                -1));
        Assert.Equal(
            2,
            FlowExecutionAnalysisWindow.FindAdjacentRunIndex(
                orderedRuns,
                102,
                "same-sn",
                1));
        Assert.Equal(
            -1,
            FlowExecutionAnalysisWindow.FindAdjacentRunIndex(
                orderedRuns,
                101,
                "same-sn",
                -1));
        Assert.Equal(
            -1,
            FlowExecutionAnalysisWindow.FindAdjacentRunIndex(
                orderedRuns,
                999,
                "same-sn",
                1));
    }
}

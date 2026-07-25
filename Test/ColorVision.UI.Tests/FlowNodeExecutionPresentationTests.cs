#pragma warning disable CA1707
using ColorVision.Engine.FlowProcessing.Diagnostics;

namespace ColorVision.UI.Tests;

public class FlowNodeExecutionPresentationTests
{
    [Fact]
    public void FromRecord_ReturnsCompleteAndStoredElapsedForCompletedNode()
    {
        var record = new FlowNodeRecord
        {
            StartTime = new DateTime(2026, 7, 26, 10, 0, 0),
            EndTime = new DateTime(2026, 7, 26, 10, 0, 1),
            ElapsedMs = 875
        };

        FlowNodeExecutionPresentation presentation = FlowNodeExecutionPresentation.FromRecord(
            record,
            new DateTime(2026, 7, 26, 11, 0, 0));

        Assert.Equal(FlowNodeExecutionState.Complete, presentation.State);
        Assert.Equal(875, presentation.ElapsedMs);
    }

    [Fact]
    public void FromRecord_ReturnsRunningAndCurrentElapsedForActiveNode()
    {
        var record = new FlowNodeRecord
        {
            StartTime = new DateTime(2026, 7, 26, 10, 0, 0)
        };

        FlowNodeExecutionPresentation presentation = FlowNodeExecutionPresentation.FromRecord(
            record,
            new DateTime(2026, 7, 26, 10, 0, 2, 250));

        Assert.Equal(FlowNodeExecutionState.Running, presentation.State);
        Assert.Equal(2250, presentation.ElapsedMs);
    }

    [Fact]
    public void FromRecord_ReturnsNotStartedWhenHistoryIsMissing()
    {
        FlowNodeExecutionPresentation presentation = FlowNodeExecutionPresentation.FromRecord(
            null,
            new DateTime(2026, 7, 26, 10, 0, 0));

        Assert.Equal(FlowNodeExecutionState.NotStarted, presentation.State);
        Assert.Null(presentation.ElapsedMs);
    }
}

using ColorVision.Engine.FlowProcessing.Diagnostics;

namespace ColorVision.UI.Tests;

public sealed class FlowNodeTelemetryTests
{
    [Fact]
    public void StartSnapshotsCannotBeChangedByFastNodeCompletion()
    {
        DateTime started = DateTime.UtcNow;
        var record = new FlowNodeRecord
        {
            BatchId = 42,
            SerialNumber = "SN-1",
            NodeId = "node-1",
            NodeName = "Node 1",
            NodeType = "Test",
            StartTime = started,
            EndTime = started.AddMilliseconds(25),
            ElapsedMs = 25,
        };
        var message = new FlowNodeMessage
        {
            BatchId = 42,
            SerialNumber = "SN-1",
            NodeId = "node-1",
            NodeName = "Node 1",
            MsgId = "message-1",
            EventName = "Run",
            SendTopic = "send",
            SendPayload = "request",
            SendTime = started,
            RecvTopic = "receive",
            RecvPayload = "response",
            RecvTime = started.AddMilliseconds(25),
            StatusCode = 0,
            StatusMessage = "ok",
            State = FlowMessageState.Success,
        };

        FlowNodeRecord startRecord =
            FlowNodeRecordDataBaseHelper.CloneNodeStartRecord(record);
        FlowNodeMessage startMessage =
            FlowNodeRecordDataBaseHelper.CloneNodeStartMessage(message);

        Assert.Equal(started, startRecord.StartTime);
        Assert.Null(startRecord.EndTime);
        Assert.Equal(0, startRecord.ElapsedMs);
        Assert.Equal(FlowMessageState.Sent, startMessage.State);
        Assert.Null(startMessage.RecvTime);
        Assert.Null(startMessage.StatusCode);
        Assert.Null(startMessage.RecvPayload);
    }
}

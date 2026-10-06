using ColorVision.Engine.FlowProcessing.Diagnostics;
using FlowEngineLib;
using FlowEngineLib.Base;
using System.Collections.Concurrent;
using System.IO;

namespace ProjectARVRPro.Tests;

public class FlowNodeExecutionRecorderTests
{
    [Fact]
    public async Task CompletesOwnedRecordingWithoutCurrentResultAndAllowsNextRun()
    {
        var records = new ConcurrentBag<FlowNodeRecord>();
        int nextRecordId = 0;
        using var recorder = new FlowNodeExecutionRecorder(
            record =>
            {
                record.Id = Interlocked.Increment(ref nextRecordId);
                records.Add(record);
                return record.Id;
            },
            _ => { },
            _ => 1,
            _ => { },
            _ => true,
            _ => { });
        var node = new CVDeviceNode("Camera", "CameraType", "CameraNode", "Device");
        recorder.AttachNodes([node]);
        recorder.StartRun(100, "previous-run");
        node.nodeRunEvent.Invoke(node, new FlowEngineNodeRunEventArgs { SerialNumber = "previous-run" });

        // An exception can clear the host's current result and its serial number.
        // Cleanup must still finish the recording owned by that host.
        Assert.True(await recorder.CompleteRunAsync());
        Assert.False(recorder.IsRecording());
        Assert.NotNull(Assert.Single(records).EndTime);

        recorder.StartRun(101, "next-run");
        node.nodeRunEvent.Invoke(node, new FlowEngineNodeRunEventArgs { SerialNumber = "previous-run" });
        node.nodeEndEvent.Invoke(node, new FlowEngineNodeEndEventArgs { SerialNumber = "previous-run" });
        Assert.True(recorder.IsRecording("next-run"));
        node.nodeRunEvent.Invoke(node, new FlowEngineNodeRunEventArgs { SerialNumber = "next-run" });
        Assert.True(await recorder.CompleteRunAsync("next-run"));

        Assert.Equal(2, records.Count);
        FlowNodeRecord nextRecord = Assert.Single(records, record => record.BatchId == 101);
        Assert.Equal("next-run", nextRecord.SerialNumber);
        Assert.NotNull(nextRecord.EndTime);
    }

    [Fact]
    public async Task CompletionForDifferentRunDoesNotStopActiveRecording()
    {
        using var recorder = new FlowNodeExecutionRecorder(_ => 1, _ => { }, _ => 1, _ => { }, _ => true, _ => { });
        recorder.StartRun(100, "active-run");

        Assert.False(await recorder.CompleteRunAsync("previous-run"));
        Assert.True(recorder.IsRecording("active-run"));
        Assert.Throws<InvalidOperationException>(() => recorder.StartRun(101, "next-run"));
        Assert.True(await recorder.CompleteRunAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedDiagnosticsFlushDoesNotBlockNextExecution(bool throwOnFlush)
    {
        using var recorder = new FlowNodeExecutionRecorder(
            _ => 1, _ => { }, _ => 1, _ => { },
            _ => throwOnFlush ? throw new IOException("Synthetic diagnostics storage failure") : false,
            _ => { });
        recorder.StartRun(100, "previous-run");

        if (throwOnFlush)
            await Assert.ThrowsAsync<IOException>(() => recorder.CompleteRunAsync());
        else
            Assert.False(await recorder.CompleteRunAsync());

        Assert.False(recorder.IsRecording());
        recorder.StartRun(101, "next-run");
        Assert.True(recorder.IsRecording("next-run"));
    }

    [Fact]
    public async Task RecordsNodeTimingAndMessageAgainstActiveBatch()
    {
        var insertedRecords = new ConcurrentBag<FlowNodeRecord>();
        var updatedRecords = new ConcurrentBag<FlowNodeRecord>();
        var insertedMessages = new ConcurrentBag<FlowNodeMessage>();
        var updatedMessages = new ConcurrentBag<FlowNodeMessage>();
        int nextRecordId = 0;
        int nextMessageId = 0;
        using var recorder = new FlowNodeExecutionRecorder(
            record =>
            {
                record.Id = Interlocked.Increment(ref nextRecordId);
                insertedRecords.Add(record);
                return record.Id;
            },
            updatedRecords.Add,
            message =>
            {
                message.Id = Interlocked.Increment(ref nextMessageId);
                insertedMessages.Add(message);
                return message.Id;
            },
            updatedMessages.Add,
            _ => true,
            _ => { });
        var node = new CVDeviceNode("Algorithm", "AlgorithmType", "AlgorithmNode", "Device");

        recorder.AttachNodes([node]);
        recorder.StartRun(24586, "run-001");
        node.nodeRunEvent.Invoke(node, new FlowEngineNodeRunEventArgs
        {
            SerialNumber = "run-001",
            SendMsgId = "msg-001",
            SendEventName = "Measure",
            SendTopic = "flow/request",
            SendPayload = """{"value":1}""",
        });
        node.nodeEndEvent.Invoke(node, new FlowEngineNodeEndEventArgs
        {
            SerialNumber = "run-001",
            RecvMsgId = "msg-001",
            RecvTopic = "flow/response",
            RecvPayload = """{"code":0}""",
            RecvStatusCode = 0,
            RecvStatusMessage = "Completed",
        });

        bool flushed = await recorder.CompleteRunAsync("run-001");

        Assert.True(flushed);
        FlowNodeRecord record = Assert.Single(insertedRecords);
        Assert.Equal(24586, record.BatchId);
        Assert.Equal("run-001", record.SerialNumber);
        Assert.Equal(node.NodeID, record.NodeId);
        Assert.NotNull(record.EndTime);
        Assert.True(record.ElapsedMs >= 0);
        Assert.Single(updatedRecords);

        FlowNodeMessage message = Assert.Single(insertedMessages);
        Assert.Equal(record.Id, message.NodeRecordId);
        Assert.Equal(FlowMessageState.Success, message.State);
        Assert.Equal("flow/response", message.RecvTopic);
        Assert.Single(updatedMessages);
        Assert.False(recorder.IsRecording());
    }

    [Fact]
    public async Task FinalizesUnmatchedNodeAndDetachesReplacedNodes()
    {
        var updatedRecords = new ConcurrentBag<FlowNodeRecord>();
        var updatedMessages = new ConcurrentBag<FlowNodeMessage>();
        int insertedRecordCount = 0;
        using var recorder = new FlowNodeExecutionRecorder(
            record =>
            {
                record.Id = Interlocked.Increment(ref insertedRecordCount);
                return record.Id;
            },
            updatedRecords.Add,
            message => 1,
            updatedMessages.Add,
            _ => true,
            _ => { });
        var oldNode = new CVDeviceNode("Old", "Type", "Old", "Device");
        var activeNode = new CVDeviceNode("Active", "Type", "Active", "Device");

        recorder.AttachNodes([oldNode]);
        recorder.AttachNodes([activeNode]);
        recorder.StartRun(99, "run-002");
        oldNode.nodeRunEvent?.Invoke(oldNode, new FlowEngineNodeRunEventArgs
        {
            SerialNumber = "run-002",
            SendMsgId = "old",
        });
        activeNode.nodeRunEvent.Invoke(activeNode, new FlowEngineNodeRunEventArgs
        {
            SerialNumber = "run-002",
            SendMsgId = "active",
            SendEventName = "Measure",
        });

        await recorder.CompleteRunAsync("run-002");

        Assert.Equal(1, insertedRecordCount);
        FlowNodeRecord record = Assert.Single(updatedRecords);
        Assert.Equal(activeNode.NodeID, record.NodeId);
        Assert.NotNull(record.EndTime);
        FlowNodeMessage message = Assert.Single(updatedMessages);
        Assert.Equal(FlowMessageState.Timeout, message.State);
        Assert.NotNull(message.RecvTime);
    }
}

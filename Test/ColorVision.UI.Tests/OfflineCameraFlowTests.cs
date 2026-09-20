using ColorVision.Engine.Services.Devices.Camera.Local;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;
using ColorVision.Engine.FlowProcessing.Nodes;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class OfflineCameraFlowTests
{
    [Fact]
    public void RealLocalImageNodeCompletesWithoutMqttOrResultBatch()
    {
        StaTest.Run(() =>
        {
            string parent = Path.Combine(Path.GetTempPath(), "ColorVision-offline-flow-tests");
            string directory = Path.GetFullPath(Path.Combine(parent, Guid.NewGuid().ToString("N")));
            try
            {
                using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata { Width = 1, Height = 1, Channels = 1, SourceBpp = 16 }, 2, 0);
                LocalFrameFileService.SaveCapture(frame, directory, "synthetic-camera", includeCie: false);
                using var editor = new STNodeEditor();
                using var control = new FlowEngineControl(editor, false, new FlowNodeManager()) { PersistResults = false };
                var start = new DisconnectedMqttStart { NodeName = "offline" };
                var image = new TestMessageBoxNode { ImageFileUrl = frame.CvRawFilePath };
                var end = new CVEndNode();
                foreach (var node in new STNode[] { start, image, end }) { node.Create(); editor.Nodes.Add(node); }
                Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(image.GetAllInputOptions().First()));
                Assert.Equal(ConnectionStatus.Connected, image.GetAllOutputOptions().First().ConnectOption(end.m_in_start));
                var completion = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                control.Finished += (_, result) => completion.TrySetResult(result);
                Assert.True(control.TryStartNode("offline", "image-no-result-database"));
                var result = completion.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                Assert.True(result.Status == StatusTypeEnum.Completed, result.Message);
            }
            finally
            {
                Assert.StartsWith(Path.GetFullPath(parent) + Path.DirectorySeparatorChar, directory, StringComparison.OrdinalIgnoreCase);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisconnectedMqttStartDispatchesLocalFlowButStillRejectsServiceGraph(bool v5)
    {
        StaTest.Run(() =>
        {
            using var editor = new STNodeEditor();
            using var control = new FlowEngineControl(editor, false, new FlowNodeManager()) { PersistResults = false };
            BaseStartNode start = v5 ? new DisconnectedMqttV5Start() : new DisconnectedMqttStart();
            start.NodeName = "offline";
            start.Create();
            var end = new CVEndNode();
            end.Create();
            editor.Nodes.Add(start);
            editor.Nodes.Add(end);
            Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(end.m_in_start));
            Assert.False(start.IsExecutionReady);
            Assert.True(control.EnsureStartNodeReadyAsync("offline", TimeSpan.FromSeconds(1)).GetAwaiter().GetResult());
            FlowEngineEventArgs? completed = null;
            control.Finished += (_, result) => completed = result;
            Assert.True(control.TryStartNode("offline", "offline-test"));
            Assert.Equal(StatusTypeEnum.Completed, completed!.Status);
            Assert.False(start.PersistResults);

            var remote = new RemoteNode();
            remote.Create();
            editor.Nodes.Add(remote);
            Assert.False(control.CanStartNode("offline"));
            Assert.False(control.TryStartNode("offline", "remote-test"));
            editor.Nodes.Remove(remote);
            Assert.True(control.CanStartNode("offline"));
        });
    }

    [Fact]
    public void OfflineCameraResultKeepsFrameAndMetadataWithoutDatabaseIdentity()
    {
        var action = new CVStartCFC("no-database-batch") { PersistResults = false };
        Assert.False(new CVStartCFC(action).PersistResults);
        using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata { Width = 1, Height = 1, Channels = 1, SourceBpp = 8 }, 1, 0);
        var capture = new LocalCameraCaptureResult { Frame = frame, TotalTimeMs = 15 };
        var result = LocalCameraResultService.SaveFlowModel(action, 1, frame, capture, null, null, false);
        Assert.True(result.Id <= 0);
        Assert.Equal(0, result.BatchId);
        Assert.Equal(15, result.TotalTime);
        Assert.True(frame.HasRaw);
    }

    private sealed class DisconnectedMqttStart : MQTTStartNode
    {
        public override Task<bool> EnsureReadyAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override void DoPublishStatus(string message) { }
    }
    private sealed class DisconnectedMqttV5Start : MQTTStartV5Node
    {
        public override Task<bool> EnsureReadyAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override void DoPublishStatus(string message) { }
    }
    private sealed class RemoteNode : CVBaseServerNode
    {
        public RemoteNode() : base("Remote", "Camera", "service", "camera") { }
    }
}

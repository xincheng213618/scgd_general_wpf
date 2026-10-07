using ColorVision.SocketProtocol;
using ColorVision.Testing;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ProjectARVRPro.Flow;
using ProjectARVRPro.Services;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Text;

namespace ProjectARVRPro.Tests;

public sealed class ExternalImageSwitchTests
{
    [Fact]
    public async Task ImmediateAcknowledgementDuringWriteIsNotLost()
    {
        using var stream = new CommandStream();
        var service = new ExternalImageSwitchService(() => stream);
        stream.OnWrite = request => Assert.True(service.TryComplete(stream, Complete(request)));

        ExternalImageSwitchResult result = await service.ExecuteAsync("product-sn", 5000, 0, true, default);

        SocketResponse sent = Assert.Single(stream.Requests);
        Assert.Equal("AoiSwitchPG", sent.EventName);
        Assert.Equal("1.0", sent.Version);
        Assert.Equal(0, sent.Code);
        Assert.Equal("product-sn", sent.SerialNumber);
        Assert.Equal(sent.MsgID, result.MsgID);
        Assert.False(service.TryComplete(stream, Complete(sent)));
    }

    [Fact]
    public async Task OnlyMatchingCompletionCanReleaseTheSinglePendingOperation()
    {
        using var stream = new CommandStream();
        using var stranger = new CommandStream();
        var service = new ExternalImageSwitchService(() => stream);
        Task<ExternalImageSwitchResult> run = service.ExecuteAsync("sn", 5000, 0, true, default);
        SocketResponse request = Assert.Single(stream.Requests);
        Assert.False(run.IsCompleted); // Sending alone is not completion.
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync("sn", 5000, 0, true, default));
        Assert.Single(stream.Requests);

        Assert.False(service.TryComplete(stranger, Complete(request)));
        SocketRequest reply = Complete(request);
        reply.EventName = "SwitchPGCompleted";
        Assert.False(service.TryComplete(stream, reply));
        reply = Complete(request);
        reply.SerialNumber = "another-product";
        Assert.False(service.TryComplete(stream, reply));
        reply = Complete(request);
        reply.MsgID = "another-request";
        Assert.False(service.TryComplete(stream, reply));
        Assert.False(run.IsCompleted);

        Assert.True(service.TryComplete(stream, Complete(request)));
        await run;
        Assert.False(service.TryComplete(stream, Complete(request)));
    }

    [Fact]
    public async Task LegacyAcknowledgementsAndSequentialSwitchesRemainSupported()
    {
        using var stream = new CommandStream();
        var service = new ExternalImageSwitchService(() => stream);
        stream.OnWrite = request => Assert.True(service.TryComplete(stream, new SocketRequest
        {
            EventName = ExternalImageSwitchService.CompletionEvent, MsgID = "client-generated-id"
        }));
        ExternalImageSwitchResult first = await service.ExecuteAsync("sn", 5000, 0, false, default);
        ExternalImageSwitchResult second = await service.ExecuteAsync("sn", 5000, 0, false, default);
        Assert.NotEqual(first.MsgID, second.MsgID);
        Assert.Equal(2, stream.Requests.Count);
    }

    [Fact]
    public async Task TimeoutDoesNotResendAndReleasesThePendingSlot()
    {
        using var stream = new CommandStream();
        var service = new ExternalImageSwitchService(() => stream);
        await Assert.ThrowsAsync<TimeoutException>(() => service.ExecuteAsync("sn", 30, 0, true, default));
        SocketResponse old = Assert.Single(stream.Requests);
        Task<ExternalImageSwitchResult> next = service.ExecuteAsync("sn", 5000, 0, true, default);
        Assert.False(service.TryComplete(stream, Complete(old)));
        Assert.True(service.TryComplete(stream, Complete(stream.Requests[1])));
        await next;
        Assert.Equal(2, stream.Requests.Count);
    }

    [Fact]
    public async Task FlowStopCancelsWaitingAndRejectsTheLateAcknowledgement()
    {
        using var resources = new FlowRuntimeResources();
        using var stream = new CommandStream();
        var service = new ExternalImageSwitchService(() => stream);
        Task<ExternalImageSwitchResult> run = service.ExecuteAsync("sn", 5000, 0, false, resources.StopToken);
        resources.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.False(service.TryComplete(stream, Complete(Assert.Single(stream.Requests))));
    }

    [Fact]
    public async Task SettlingDelayKeepsTheOperationBusyAndCanBeCancelled()
    {
        using var stream = new CommandStream();
        using var cancellation = new CancellationTokenSource();
        var service = new ExternalImageSwitchService(() => stream);
        stream.OnWrite = request => Assert.True(service.TryComplete(stream, Complete(request)));
        Task<ExternalImageSwitchResult> run = service.ExecuteAsync("sn", 5000, 60000, true, cancellation.Token);
        Assert.False(run.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync("sn", 5000, 0, true, default));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        await service.ExecuteAsync("sn", 5000, 0, true, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosedOrReplacedConnectionFailsWithoutAcknowledgement(bool replace)
    {
        using var stream = new CommandStream();
        using var replacement = new CommandStream();
        Stream active = stream;
        var service = new ExternalImageSwitchService(() => Volatile.Read(ref active));
        Task<ExternalImageSwitchResult> run = service.ExecuteAsync("sn", 5000, 0, false, default);
        if (replace) Volatile.Write(ref active, replacement);
        else stream.Dispose();
        await Assert.ThrowsAsync<IOException>(() => run.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.False(service.TryComplete(stream, Complete(Assert.Single(stream.Requests))));
    }

    [Fact]
    public async Task SendFailureAndMissingConnectionDoNotLeaveAnOutstandingRequest()
    {
        Stream? active = null;
        var service = new ExternalImageSwitchService(() => active);
        await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync("sn", 5000, 0, false, default));
        using var broken = new CommandStream { FailWrite = true };
        active = broken;
        await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync("sn", 5000, 0, false, default));
        using var connected = new CommandStream();
        active = connected;
        connected.OnWrite = request => service.TryComplete(connected, Complete(request));
        await service.ExecuteAsync("sn", 5000, 0, true, default);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(5000, -1)]
    public async Task InvalidWaitSettingsNeverSend(int timeoutMs, int delayMs)
    {
        using var stream = new CommandStream();
        var service = new ExternalImageSwitchService(() => stream);
        await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(() => service.ExecuteAsync("sn", timeoutMs, delayMs, false, default));
        Assert.Empty(stream.Requests);
    }

    [Fact]
    public void PluginNodeIsDiscoverableAndItsSettingsSurviveCanvasRoundTrip()
    {
        StaTest.Run(() =>
        {
            Assert.Contains(typeof(ExternalImageSwitchNode), STNodeTypeRegistry.GetTypes());
            using var canvas = new STNodeEditor();
            var node = new ExternalImageSwitchNode();
            node.Create();
            node.TimeoutMs = 4321;
            node.DelayMs = 123;
            node.RequireMatchingMsgId = true;
            canvas.Nodes.Add(node);
            using var snapshot = STNodeEditor.ReadCanvasSnapshot(canvas.GetCanvasData());
            var restored = Assert.Single(snapshot.Nodes.OfType<ExternalImageSwitchNode>());
            Assert.Equal(4321, restored.TimeoutMs);
            Assert.Equal(123, restored.DelayMs);
            Assert.True(restored.RequireMatchingMsgId);
            Assert.Single(restored.GetAllInputOptions());
            Assert.Single(restored.GetAllOutputOptions());
        });
    }

    [Fact]
    public void NodeWaitsForAcknowledgementAndPreservesTheFlowPayload()
    {
        StaTest.Run(() =>
        {
            using var stream = new CommandStream();
            var sent = new TaskCompletionSource<SocketResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            stream.OnWrite = request => sent.TrySetResult(request);
            var service = new ExternalImageSwitchService(() => stream);
            using var canvas = new STNodeEditor();
            var node = new ExternalImageSwitchNode(service, () => "product-sn");
            var source = new FlowProbe();
            var probe = new FlowProbe();
            node.Create();
            source.Create();
            probe.Create();
            canvas.Nodes.Add(source);
            canvas.Nodes.Add(node);
            canvas.Nodes.Add(probe);
            Assert.Equal(ConnectionStatus.Connected, source.GetAllOutputOptions()[0].ConnectOption(node.GetAllInputOptions()[0], false));
            Assert.Equal(ConnectionStatus.Connected, node.GetAllOutputOptions()[0].ConnectOption(probe.GetAllInputOptions()[0], false));
            var action = new CVStartCFC("measurement-run");
            action.Data["MasterId"] = 42;
            action.Data["MasterValue"] = "image.raw";
            using var resources = action.RuntimeResources;
            source.GetAllOutputOptions()[0].TransferData(action);
            SocketResponse request = sent.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            Assert.Equal("product-sn", request.SerialNumber);
            Assert.False(probe.Received.Task.IsCompleted);
            Assert.True(service.TryComplete(stream, Complete(request)));
            CVStartCFC output = probe.Received.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            Assert.True(output.IsRunning);
            Assert.Equal("measurement-run", output.SerialNumber);
            Assert.Equal(42, output.Data["MasterId"]);
            Assert.Equal("image.raw", output.Data["MasterValue"]);
        });
    }

    private static SocketRequest Complete(SocketResponse request) => new()
    {
        EventName = ExternalImageSwitchService.CompletionEvent, MsgID = request.MsgID, SerialNumber = request.SerialNumber
    };

    private sealed class CommandStream : MemoryStream
    {
        internal List<SocketResponse> Requests { get; } = [];
        internal Action<SocketResponse>? OnWrite { get; set; }
        internal bool FailWrite { get; init; }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailWrite) throw new IOException("Disconnected during send.");
            SocketResponse request = JsonConvert.DeserializeObject<SocketResponse>(Encoding.UTF8.GetString(buffer.Span))!;
            Requests.Add(request);
            OnWrite?.Invoke(request);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FlowProbe : STNode
    {
        internal TaskCompletionSource<CVStartCFC> Received { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override void OnCreate()
        {
            base.OnCreate();
            InputOptions.Add("IN", typeof(CVStartCFC), true).DataTransfer += (_, args) =>
            {
                if (args.TargetOption.Data is CVStartCFC action) Received.TrySetResult(action);
            };
            OutputOptions.Add("OUT", typeof(CVStartCFC), false);
        }
    }
}

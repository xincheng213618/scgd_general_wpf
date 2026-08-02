using ColorVision.Engine.FlowProcessing;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using ST.Library.UI.NodeEditor;
using System.Runtime.ExceptionServices;

namespace ColorVision.UI.Tests;

public sealed class FlowHeadlessExecutionServiceTests
{
    [Fact]
    public void RequestOwnsImmutableStnSnapshot()
    {
        RunInSta(async () =>
        {
            byte[] canvas = CreateCanvas(new CVEndNode());
            var request = new FlowHeadlessExecutionRequest(
                canvas,
                "HeadlessStart",
                "SN-IMMUTABLE");
            Array.Fill(canvas, (byte)0x5a);

            FlowHeadlessExecutionResult result =
                await FlowHeadlessExecutionService.Shared.RunAsync(
                    request);

            Assert.True(result.Succeeded);
            Assert.Equal(
                request.ContentHash,
                result.ContentHash);
        });
    }

    [Fact]
    public void SharedServiceRunsDetachedRequestsConcurrently()
    {
        RunInSta(async () =>
        {
            byte[] canvas = CreateCanvas(new CVEndNode());
            var first = new FlowHeadlessExecutionRequest(
                canvas,
                "HeadlessStart",
                "SN-ONE");
            var second = new FlowHeadlessExecutionRequest(
                canvas,
                "HeadlessStart",
                "SN-TWO");

            FlowHeadlessExecutionResult[] results =
                await Task.WhenAll(
                    FlowHeadlessExecutionService.Shared.RunAsync(
                        first),
                    FlowHeadlessExecutionService.Shared.RunAsync(
                        second));

            Assert.All(results, result => Assert.True(result.Succeeded));
            Assert.Equal(
                ["SN-ONE", "SN-TWO"],
                results.Select(result => result.Data.SerialNumber));
        });
    }

    [Fact]
    public void CancellationReturnsStructuredCompatibleResult()
    {
        RunInSta(async () =>
        {
            byte[] canvas =
                CreateCanvas(new HeadlessNeverEndNode());
            var request = new FlowHeadlessExecutionRequest(
                canvas,
                "HeadlessStart",
                "SN-CANCEL");
            using var cancellation =
                new CancellationTokenSource(
                    TimeSpan.FromMilliseconds(50));

            FlowHeadlessExecutionResult result =
                await FlowHeadlessExecutionService.Shared.RunAsync(
                    request,
                    cancellation.Token);
            FlowControlData compatible =
                result.ToFlowControlData();

            Assert.True(result.Started);
            Assert.Equal(
                FlowHeadlessExecutionTermination.Canceled,
                result.Termination);
            Assert.Equal(StatusTypeEnum.Canceled, compatible.Status);
            Assert.Equal("SN-CANCEL", compatible.SerialNumber);
            Assert.Equal(
                result.Data.Message,
                compatible.Message);
        });
    }

    [Fact]
    public void InvalidSnapshotReturnsLoadFailureInsteadOfMutatingUi()
    {
        RunInSta(async () =>
        {
            var request = new FlowHeadlessExecutionRequest(
                [1, 2, 3, 4],
                "HeadlessStart",
                "SN-BAD");

            FlowHeadlessExecutionResult result =
                await FlowHeadlessExecutionService.Shared.RunAsync(
                    request);

            Assert.False(result.Started);
            Assert.False(result.Succeeded);
            Assert.Equal(
                FlowHeadlessExecutionTermination.LoadFailed,
                result.Termination);
            Assert.Equal(StatusTypeEnum.Failed, result.Data.Status);
        });
    }

    [Fact]
    public void PerRunObserverReceivesDetachedNodeDiagnostics()
    {
        RunInSta(async () =>
        {
            byte[] canvas = CreateObservedCanvas();
            var request = new FlowHeadlessExecutionRequest(
                canvas,
                "HeadlessStart",
                "SN-OBSERVED",
                [
                    new MQTTServiceInfo
                    {
                        ServiceType = "TEST",
                        ServiceCode = "Observed",
                        PublishTopic = "test/send",
                        SubscribeTopic = "test/recv",
                    },
                ]);
            int runEvents = 0;
            int endEvents = 0;
            object? observedSender = null;
            var observer = new FlowHeadlessExecutionObserver(
                (sender, args) =>
                {
                    observedSender = sender;
                    Assert.Equal("SN-OBSERVED", args.SerialNumber);
                    Interlocked.Increment(ref runEvents);
                },
                (sender, args) =>
                {
                    Assert.Same(observedSender, sender);
                    Assert.Equal("SN-OBSERVED", args.SerialNumber);
                    Interlocked.Increment(ref endEvents);
                });

            FlowHeadlessExecutionResult result =
                await FlowHeadlessExecutionService.Shared.RunAsync(
                    request,
                    observer,
                    CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.IsType<HeadlessObservedServerNode>(
                observedSender);
            Assert.True(runEvents > 0);
            Assert.Equal(runEvents, endEvents);
        });
    }

    [Fact]
    public void ServiceGraphWithoutServiceSnapshotIsRejected()
    {
        RunInSta(async () =>
        {
            var request = new FlowHeadlessExecutionRequest(
                CreateObservedCanvas(),
                "HeadlessStart",
                "SN-NO-SERVICES");

            FlowHeadlessExecutionResult result =
                await FlowHeadlessExecutionService.Shared.RunAsync(
                    request);

            Assert.False(result.Started);
            Assert.Equal(
                FlowHeadlessExecutionTermination.StartRejected,
                result.Termination);
            Assert.Contains(
                "no MQTT service snapshot",
                result.Data.Message);
        });
    }

    private static byte[] CreateCanvas(STNode terminalNode)
    {
        using var editor = new STNodeEditor();
        var start = new HeadlessTestStartNode();
        start.Create();
        terminalNode.Create();
        editor.Nodes.Add(start);
        editor.Nodes.Add(terminalNode);
        STNodeOption terminalInput = terminalNode
            .GetAllInputOptions()
            .Single(option =>
                option.DataType == typeof(CVStartCFC));
        Assert.Equal(
            ConnectionStatus.Connected,
            start.m_op_start.ConnectOption(terminalInput));
        return editor.GetCanvasData();
    }

    private static byte[] CreateObservedCanvas()
    {
        using var editor = new STNodeEditor();
        var start = new HeadlessTestStartNode();
        var server = new HeadlessObservedServerNode();
        var end = new CVEndNode();
        start.Create();
        server.Create();
        end.Create();
        editor.Nodes.Add(start);
        editor.Nodes.Add(server);
        editor.Nodes.Add(end);
        Assert.Equal(
            ConnectionStatus.Connected,
            start.m_op_start.ConnectOption(server.Input));
        Assert.Equal(
            ConnectionStatus.Connected,
            server.Output.ConnectOption(end.m_in_start));
        return editor.GetCanvasData();
    }

    private static void RunInSta(Func<Task> action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception != null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}

public sealed class HeadlessObservedServerNode :
    CVBaseServerNode
{
    public HeadlessObservedServerNode()
        : base(
            "Observed server",
            "TEST",
            "Observed",
            "DEV")
    {
    }

    public STNodeOption Input => m_in_start;

    public STNodeOption Output => m_op_end;

    protected override void m_in_start_DataTransfer(
        object sender,
        STNodeOptionEventArgs e)
    {
        if (e.TargetOption.Data is not CVStartCFC start)
            return;

        const string messageId = "observed-message";
        nodeRunEvent?.Invoke(
            this,
            new FlowEngineNodeRunEventArgs
            {
                SerialNumber = start.SerialNumber,
                SendMsgId = messageId,
                SendEventName = "Observed",
                SendTopic = "test/send",
                SendPayload = "{}",
            });
        nodeEndEvent?.Invoke(
            this,
            new FlowEngineNodeEndEventArgs
            {
                SerialNumber = start.SerialNumber,
                RecvMsgId = messageId,
                RecvEventName = "Observed",
                RecvTopic = "test/recv",
                RecvPayload = "{}",
                RecvStatusCode = 0,
                RecvStatusMessage = "OK",
            });
        m_op_end.TransferData(start);
    }
}

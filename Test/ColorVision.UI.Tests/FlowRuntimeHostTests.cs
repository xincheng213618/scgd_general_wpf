using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Runtime;
using FlowEngineLib.Start;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Runtime.ExceptionServices;

namespace ColorVision.UI.Tests;

public sealed class FlowRuntimeHostTests
{
    [Fact]
    public void BareRuntimeLoadsAndCompletesWithoutAnEditor()
    {
        RunInSta(async () =>
        {
            byte[] canvas = CreateCanvas(new CVEndNode());
            await using var host = new FlowRuntimeHost();

            await host.LoadAsync(canvas);
            FlowEngineRunResult result = await host.RunAsync(
                "HeadlessStart",
                "SN-BARE");

            Assert.True(result.Started);
            Assert.Equal(FlowEngineRunTermination.Completed, result.Termination);
            Assert.Equal(StatusTypeEnum.Completed, result.Data.Status);
            Assert.Equal(FlowRuntimeHostState.Ready, host.State);
            Assert.NotNull(host.ContentHash);
            Assert.Equal(2, host.Nodes.Count);
        });
    }

    [Fact]
    public void CancellationStopsExactHeadlessRunAndReturnsHostToReady()
    {
        RunInSta(async () =>
        {
            byte[] canvas = CreateCanvas(new HeadlessNeverEndNode());
            await using var host = new FlowRuntimeHost();
            await host.LoadAsync(canvas);
            using var cancellation = new CancellationTokenSource(
                TimeSpan.FromMilliseconds(50));

            FlowEngineRunResult result = await host.RunAsync(
                "HeadlessStart",
                "SN-CANCEL",
                cancellationToken: cancellation.Token);

            Assert.True(result.Started);
            Assert.Equal(FlowEngineRunTermination.Canceled, result.Termination);
            Assert.Equal(StatusTypeEnum.Canceled, result.Data.Status);
            Assert.Equal(FlowRuntimeHostState.Ready, host.State);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedReloadPreservesRunningFlowUntilCompletionOrStop(bool stop)
    {
        RunInSta(async () =>
        {
            byte[] canvas = CreateCanvas(new HeadlessNeverEndNode());
            await using var host = new FlowRuntimeHost();
            await host.LoadAsync(canvas);
            string? originalHash = host.ContentHash;
            BaseStartNode start = Assert.Single(host.Nodes.OfType<BaseStartNode>());
            Task<FlowEngineRunResult> execution = host.RunAsync("HeadlessStart", "SN-REJECT-RELOAD");
            Assert.Equal(FlowRuntimeHostState.Running, host.State);

            await Assert.ThrowsAsync<InvalidOperationException>(() => host.LoadAsync(canvas));

            Assert.Equal(FlowRuntimeHostState.Running, host.State);
            Assert.Equal(originalHash, host.ContentHash);
            Assert.Same(start, Assert.Single(host.Nodes.OfType<BaseStartNode>()));
            if (stop)
            {
                await host.StopAsync();
            }
            else
            {
                CVStartCFC action = start.GetCFC("SN-REJECT-RELOAD");
                action.SetStatusType(StatusTypeEnum.Completed);
                Assert.True(action.TryDoFinishing());
                action.FireFinished();
            }

            FlowEngineRunResult result = await execution.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Started);
            Assert.Equal(stop ? FlowEngineRunTermination.Canceled : FlowEngineRunTermination.Completed, result.Termination);
            Assert.Equal(stop ? StatusTypeEnum.Canceled : StatusTypeEnum.Completed, result.Data.Status);
            Assert.Equal(FlowRuntimeHostState.Ready, host.State);
            Assert.False(start.Running);
        });
    }

    [Fact]
    public void RejectedReloadPreservesStoppingUntilPendingReadinessEnds()
    {
        RunInSta(async () =>
        {
            byte[] canvas = CreateCanvas(new HeadlessNeverEndNode(), new HeadlessPendingReadinessStartNode());
            await using var host = new FlowRuntimeHost();
            await host.LoadAsync(canvas);
            HeadlessPendingReadinessStartNode start = Assert.Single(host.Nodes.OfType<HeadlessPendingReadinessStartNode>());
            Task<FlowEngineRunResult> execution = host.RunAsync("HeadlessStart", "SN-STOPPING-RELOAD");
            Task stopping = host.StopAsync();
            FlowEngineRunResult result;
            try
            {
                Assert.Equal(FlowRuntimeHostState.Stopping, host.State);
                await Assert.ThrowsAsync<InvalidOperationException>(() => host.LoadAsync(canvas));
                Assert.Equal(FlowRuntimeHostState.Stopping, host.State);
                Assert.False(stopping.IsCompleted);
            }
            finally
            {
                // Model a readiness operation acknowledging cancellation after replacement was rejected.
                start.Readiness.TrySetCanceled();
                await stopping;
                result = await execution.WaitAsync(TimeSpan.FromSeconds(2));
            }

            Assert.False(result.Started);
            Assert.Equal(FlowEngineRunTermination.Canceled, result.Termination);
            Assert.Equal(FlowRuntimeHostState.Ready, host.State);
        });
    }

    [Fact]
    public void CancellationDuringReadinessDoesNotDispatchHeadlessFlow()
    {
        RunInSta(async () =>
        {
            byte[] canvas = CreateCanvas(new HeadlessNeverEndNode(), new HeadlessPendingReadinessStartNode());
            await using var host = new FlowRuntimeHost();
            await host.LoadAsync(canvas);
            HeadlessPendingReadinessStartNode start = Assert.Single(host.Nodes.OfType<HeadlessPendingReadinessStartNode>());
            HeadlessNeverEndNode terminal = Assert.Single(host.Nodes.OfType<HeadlessNeverEndNode>());
            using var cancellation = new CancellationTokenSource();
            Task<FlowEngineRunResult> execution = host.RunAsync("HeadlessStart", "SN-CANCELED-READY", cancellationToken: cancellation.Token);

            cancellation.Cancel();
            start.Ready = true;
            start.Readiness.TrySetResult(true);
            FlowEngineRunResult result = await execution.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.False(result.Started);
            Assert.Equal(FlowEngineRunTermination.Canceled, result.Termination);
            Assert.Equal(0, terminal.StartedCount);
            Assert.False(start.Running);
            Assert.Equal(FlowRuntimeHostState.Ready, host.State);

            Task<FlowEngineRunResult> nextExecution = host.RunAsync("HeadlessStart", "SN-FRESH-READY");
            Assert.Equal(1, terminal.StartedCount);
            await host.StopAsync();
            Assert.True((await nextExecution.WaitAsync(TimeSpan.FromSeconds(2))).Started);
        });
    }

    [Fact]
    public void AlreadyCanceledRunnerDoesNotDispatchLocalGraph()
    {
        RunInSta(async () =>
        {
            using var container = new CVNodeContainer();
            using var control = new FlowEngineControl(container, isAutoStartName: false, new FlowNodeManager());
            control.Load(CreateCanvas(new HeadlessNeverEndNode()), waitReady: false);
            var runner = new FlowEngineRunner(control);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            FlowEngineRunResult result = await runner.RunAsync("HeadlessStart", "SN-CANCELED-LOCAL", TimeSpan.FromSeconds(1), cancellationToken: cancellation.Token);

            Assert.False(result.Started);
            Assert.Equal(FlowEngineRunTermination.Canceled, result.Termination);
            Assert.Equal(0, Assert.Single(container.Nodes.Cast<STNode>().OfType<HeadlessNeverEndNode>()).StartedCount);
            Assert.False(control.IsRunning);
        });
    }

    [Fact]
    public void InvalidReloadKeepsPublishedRuntimeGeneration()
    {
        RunInSta(async () =>
        {
            byte[] canvas = CreateCanvas(new CVEndNode());
            byte[] corrupt = (byte[])canvas.Clone();
            corrupt[^1] ^= 0x7f;
            await using var host = new FlowRuntimeHost();
            await host.LoadAsync(canvas);
            string originalHash = host.ContentHash!;

            await Assert.ThrowsAnyAsync<Exception>(
                () => host.LoadAsync(corrupt));

            Assert.Equal(FlowRuntimeHostState.Ready, host.State);
            Assert.Equal(originalHash, host.ContentHash);
            Assert.Equal(2, host.Nodes.Count);
            FlowEngineRunResult result = await host.RunAsync(
                "HeadlessStart",
                "SN-AFTER-INVALID");
            Assert.Equal(FlowEngineRunTermination.Completed, result.Termination);
        });
    }

    [Fact]
    public void CompletionSubscriberFailureDoesNotHideRunnerCompletion()
    {
        RunInSta(async () =>
        {
            byte[] canvas = CreateCanvas(new CVEndNode());
            using var container = new CVNodeContainer();
            using var control = new FlowEngineControl(
                container,
                isAutoStartName: false,
                new FlowNodeManager());
            control.Load(canvas, waitReady: false);
            control.Finished += (_, _) =>
                throw new InvalidOperationException("subscriber failure");
            var runner = new FlowEngineRunner(control);

            FlowEngineRunResult result = await runner.RunAsync(
                "HeadlessStart",
                "SN-SUBSCRIBER",
                TimeSpan.FromSeconds(1));

            Assert.Equal(FlowEngineRunTermination.Completed, result.Termination);
            Assert.Equal(StatusTypeEnum.Completed, result.Data.Status);
        });
    }

    [Fact]
    public void IndependentHostsCanRunTheSameSnapshotConcurrently()
    {
        RunInSta(async () =>
        {
            byte[] canvas = CreateCanvas(new CVEndNode());
            await using var first = new FlowRuntimeHost();
            await using var second = new FlowRuntimeHost();
            await first.LoadAsync(canvas);
            await second.LoadAsync(canvas);

            FlowEngineRunResult[] results = await Task.WhenAll(
                first.RunAsync("HeadlessStart", "SN-ONE"),
                second.RunAsync("HeadlessStart", "SN-TWO"));

            Assert.All(
                results,
                result => Assert.Equal(
                    FlowEngineRunTermination.Completed,
                    result.Termination));
            Assert.NotSame(first.Nodes[0], second.Nodes[0]);
        });
    }

    [Fact]
    public void RuntimeHostsUseIsolatedMqttServiceSnapshots()
    {
        RunInSta(async () =>
        {
            byte[] canvas = CreateCanvas(new HeadlessServiceProbeNode());
            MQTTServiceInfo globalService = CreateService(
                "global/send",
                "global/receive",
                "global-token");
            FlowServiceManager.Instance.AddMQTTService(globalService);
            try
            {
                MQTTServiceInfo firstService = CreateService(
                    "first/send",
                    "first/receive",
                    "first-token");
                MQTTServiceInfo secondService = CreateService(
                    "second/send",
                    "second/receive",
                    "second-token");
                await using var first = new FlowRuntimeHost();
                await using var second = new FlowRuntimeHost();
                await using var unmapped = new FlowRuntimeHost();

                await first.LoadAsync(canvas, new[] { firstService });
                await second.LoadAsync(canvas, new[] { secondService });
                await unmapped.LoadAsync(
                    canvas,
                    Array.Empty<MQTTServiceInfo>());
                firstService.PublishTopic = "mutated/send";
                firstService.SubscribeTopic = "mutated/receive";
                firstService.Token = "mutated-token";

                HeadlessServiceProbeNode firstNode =
                    Assert.Single(first.Nodes.OfType<HeadlessServiceProbeNode>());
                HeadlessServiceProbeNode secondNode =
                    Assert.Single(second.Nodes.OfType<HeadlessServiceProbeNode>());
                HeadlessServiceProbeNode unmappedNode =
                    Assert.Single(unmapped.Nodes.OfType<HeadlessServiceProbeNode>());

                Assert.Equal("first/send", firstNode.GetSendTopic());
                Assert.Equal("first/receive", firstNode.GetRecvTopic());
                Assert.Equal(
                    "first/receive",
                    firstNode.ConnectedReceiveTopic);
                Assert.Equal("first-token", firstNode.ResolvedToken);
                Assert.Equal("second/send", secondNode.GetSendTopic());
                Assert.Equal("second/receive", secondNode.GetRecvTopic());
                Assert.Equal(
                    "second/receive",
                    secondNode.ConnectedReceiveTopic);
                Assert.Equal("second-token", secondNode.ResolvedToken);
                Assert.Equal(
                    unmappedNode.DefaultPublishTopic,
                    unmappedNode.GetSendTopic());
                Assert.Equal(
                    unmappedNode.DefaultSubscribeTopic,
                    unmappedNode.GetRecvTopic());
                Assert.Equal(
                    unmappedNode.DefaultSubscribeTopic,
                    unmappedNode.ConnectedReceiveTopic);
                Assert.Equal(string.Empty, unmappedNode.ResolvedToken);
                Assert.Same(
                    globalService,
                    FlowServiceManager.Instance.GetService(
                        HeadlessServiceProbeNode.ServiceType,
                        HeadlessServiceProbeNode.ServiceCode));

                var uiNode = new HeadlessServiceProbeNode();
                Assert.Equal("global/send", uiNode.GetSendTopic());
                Assert.Equal("global/receive", uiNode.GetRecvTopic());
                Assert.Equal("global-token", uiNode.ResolvedToken);
            }
            finally
            {
                FlowServiceManager.Instance.Clear();
            }
        });
    }

    [Fact]
    public void InvalidReloadKeepsPublishedMqttServiceSnapshot()
    {
        RunInSta(async () =>
        {
            byte[] canvas = CreateCanvas(new HeadlessServiceProbeNode());
            byte[] corrupt = (byte[])canvas.Clone();
            corrupt[^1] ^= 0x7f;
            await using var host = new FlowRuntimeHost();
            await host.LoadAsync(
                canvas,
                new[]
                {
                    CreateService(
                        "published/send",
                        "published/receive",
                        "published-token")
                });

            await Assert.ThrowsAnyAsync<Exception>(
                () => host.LoadAsync(
                    corrupt,
                    new[]
                    {
                        CreateService(
                            "rejected/send",
                            "rejected/receive",
                            "rejected-token")
                    }));

            HeadlessServiceProbeNode node =
                Assert.Single(host.Nodes.OfType<HeadlessServiceProbeNode>());
            Assert.Equal("published/send", node.GetSendTopic());
            Assert.Equal("published/receive", node.GetRecvTopic());
            Assert.Equal(
                "published/receive",
                node.ConnectedReceiveTopic);
            Assert.Equal("published-token", node.ResolvedToken);
            Assert.Equal(FlowRuntimeHostState.Ready, host.State);
        });
    }

    private static MQTTServiceInfo CreateService(
        string publishTopic,
        string subscribeTopic,
        string token)
    {
        return new MQTTServiceInfo
        {
            ServiceType = HeadlessServiceProbeNode.ServiceType,
            ServiceCode = HeadlessServiceProbeNode.ServiceCode,
            PublishTopic = publishTopic,
            SubscribeTopic = subscribeTopic,
            Token = token
        };
    }

    private static byte[] CreateCanvas(STNode terminalNode, BaseStartNode? startNode = null)
    {
        using var editor = new STNodeEditor();
        BaseStartNode start = startNode ?? new HeadlessTestStartNode();
        start.Create();
        terminalNode.Create();
        editor.Nodes.Add(start);
        editor.Nodes.Add(terminalNode);
        STNodeOption terminalInput = terminalNode
            .GetAllInputOptions()
            .Single(option => option.DataType == typeof(CVStartCFC));
        Assert.Equal(
            ConnectionStatus.Connected,
            start.m_op_start.ConnectOption(terminalInput));
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

public sealed class HeadlessPendingReadinessStartNode : BaseStartNode
{
    public HeadlessPendingReadinessStartNode() : base("HeadlessStart")
    {
        NodeName = "HeadlessStart";
    }

    public TaskCompletionSource<bool> Readiness { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override bool RequiresConnectionReady => true;

    public override Task<bool> EnsureReadyAsync(CancellationToken cancellationToken = default) => Readiness.Task;
}

public sealed class HeadlessNeverEndNode : STNode
{
    public int StartedCount { get; private set; }

    protected override void OnCreate()
    {
        base.OnCreate();
        InputOptions.Add("IN", typeof(CVStartCFC), bSingle: true).DataTransfer += (_, e) =>
        {
            if (e.TargetOption.Data is CVStartCFC { IsRunning: true })
                StartedCount++;
        };
    }
}

public sealed class HeadlessServiceProbeNode : CVBaseServerNode
{
    public const string ServiceType = "HEADLESS_SERVICE_RESOLVER_TEST";
    public const string ServiceCode = "HEADLESS_SERVICE_RESOLVER_TEST_S01";

    public HeadlessServiceProbeNode()
        : base(
            "Headless service probe",
            ServiceType,
            ServiceCode,
            "HEADLESS_SERVICE_RESOLVER_TEST_DEV")
    {
    }

    public string ResolvedToken => GetTokenHide();

    public string? ConnectedReceiveTopic { get; private set; }

    public STNodeOption Input => m_in_start;

    public STNodeOption Output => m_op_end;

    protected override void m_in_op_Connected(
        object sender,
        STNodeOptionEventArgs e)
    {
        ConnectedReceiveTopic = GetRecvTopic();
        base.m_in_op_Connected(sender, e);
    }
}

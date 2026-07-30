using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Runtime;
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

public sealed class HeadlessNeverEndNode : STNode
{
    protected override void OnCreate()
    {
        base.OnCreate();
        InputOptions.Add("IN", typeof(CVStartCFC), bSingle: true);
    }
}

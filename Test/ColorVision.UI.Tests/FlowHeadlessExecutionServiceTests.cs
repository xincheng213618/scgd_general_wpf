using ColorVision.Database;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace ColorVision.UI.Tests;

public sealed class FlowHeadlessExecutionServiceTests
{
    [Fact]
    public Task SavedFlowBindingRejectsTwoTemplatesSharingTheSameKey()
    {
        return WpfTestHost.Invoke(async () =>
        {
            string flowKey = $"flow:{Guid.NewGuid():N}";
            string savedCanvas = Convert.ToBase64String(CreateCanvas(new CVEndNode()));
            var first = new TemplateModel<FlowParam>("First", new FlowParam { Id = 4101, FlowKey = flowKey, DataBase64 = savedCanvas });
            var second = new TemplateModel<FlowParam>("Second", new FlowParam { Id = 4102, FlowKey = flowKey, DataBase64 = savedCanvas });
            TemplateFlow.Params.Add(first);
            TemplateFlow.Params.Add(second);
            try
            {
                var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    FlowExecutionCoordinator.Instance.RunSavedFlowHeadlessAsync(flowKey, "HeadlessStart", "SN-AMBIGUOUS"));
                Assert.Contains(flowKey, failure.Message);

                TemplateFlow.Params.Remove(second);
                var unique = await FlowExecutionCoordinator.Instance.RunSavedFlowHeadlessAsync(flowKey, "HeadlessStart", "SN-UNIQUE");
                Assert.True(unique.Succeeded);
            }
            finally
            {
                TemplateFlow.Params.Remove(first);
                TemplateFlow.Params.Remove(second);
            }
        });
    }

    [Theory]
    [InlineData(true, "readinessTimeout")]
    [InlineData(false, "executionTimeout")]
    public void RequestRejectsTimeoutsOutsideTheTimerRange(bool forReadiness, string parameter)
    {
        TimeSpan unsupported = TimeSpan.FromMilliseconds(uint.MaxValue);
        var failure = Assert.Throws<ArgumentOutOfRangeException>(() => new FlowHeadlessExecutionRequest(
            [1], "HeadlessStart", "SN-TIMEOUT-RANGE",
            readinessTimeout: forReadiness ? unsupported : null,
            executionTimeout: forReadiness ? null : unsupported));

        Assert.Equal(parameter, failure.ParamName);
    }

    [Theory]
    [InlineData("SN-IMMUTABLE", "SN-IMMUTABLE")]
    [InlineData("SN?IMMUTABLE", "SN_IMMUTABLE")]
    public void RequestOwnsImmutableStnSnapshot(string requestedSerialNumber, string executionSerialNumber)
    {
        RunInSta(async () =>
        {
            byte[] canvas = CreateCanvas(new CVEndNode());
            var request = new FlowHeadlessExecutionRequest(
                canvas,
                "HeadlessStart",
                requestedSerialNumber);
            Array.Fill(canvas, (byte)0x5a);

            FlowHeadlessExecutionResult result =
                await FlowHeadlessExecutionService.Shared.RunAsync(
                    request);

            Assert.True(result.Succeeded);
            Assert.Equal(executionSerialNumber, request.SerialNumber);
            Assert.Equal(executionSerialNumber, result.Data.SerialNumber);
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
            byte[] canvas = CreateCanvas(new HeadlessObservedServerNode(), new HeadlessTestStartNode());
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
                CreateCanvas(new HeadlessObservedServerNode(), new HeadlessTestStartNode()),
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

    [Fact]
    public void LocalScriptRunsWithDisconnectedMqttStartAndNoServiceSnapshot()
    {
        RunInSta(async () =>
        {
            DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-offline-script-");
            string marker = Path.Combine(directory.FullName, "script-started.txt");
            try
            {
                using var start = new MQTTStartNode { NodeName = "OfflineStart", Server = string.Empty };
                var script = new CommandLineScriptNode
                {
                    ScriptType = CommandLineScriptType.Cmd,
                    CommandText = "echo started>script-started.txt",
                    WorkingDirectory = directory.FullName,
                };
                var request = new FlowHeadlessExecutionRequest(CreateCanvas(script, start), start.NodeName, "SN-OFFLINE-SCRIPT",
                    executionTimeout: TimeSpan.FromSeconds(5));
                Assert.False(start.IsExecutionReady);

                FlowHeadlessExecutionResult result = await FlowHeadlessExecutionService.Shared.RunAsync(request);

                Assert.True(result.Succeeded, result.Data.Message);
                Assert.True(File.Exists(marker));
            }
            finally
            {
                File.Delete(marker);
                directory.Delete();
            }
        });
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, true, null)]
    public void HeadlessPersistenceRequiresExplicitBatchOptInAndAnOnlineConnection(
        bool connected, bool persistResults, bool? expectedNodeFlag)
    {
        RunInSta(async () =>
        {
            MySqlControl mysql = MySqlControl.GetInstance();
            FieldInfo connectedField = typeof(MySqlControl).GetField("_IsConnect", BindingFlags.Instance | BindingFlags.NonPublic)!;
            bool originalConnection = mysql.IsConnect;
            bool? observedPersistence = null;
            // Change only the observed connection state. The probe has no database access.
            connectedField.SetValue(mysql, connected);
            HeadlessPersistenceProbeEndNode.Observe = persist => observedPersistence = persist;
            try
            {
                byte[] canvas = CreateCanvas(new HeadlessPersistenceProbeEndNode());
                TimeSpan timeout = TimeSpan.FromSeconds(5);
                FlowHeadlessExecutionRequest request = persistResults
                    ? new(canvas, "HeadlessStart", "SN-PERSISTENCE", executionTimeout: timeout) { PersistResults = true }
                    : new(canvas, "HeadlessStart", "SN-PERSISTENCE", executionTimeout: timeout);
                FlowHeadlessExecutionResult result = await FlowHeadlessExecutionService.Shared.RunAsync(request);

                if (expectedNodeFlag.HasValue)
                    Assert.True(result.Succeeded, result.Data.Message);
                else
                {
                    Assert.False(result.Started);
                    Assert.Equal(FlowHeadlessExecutionTermination.StartRejected, result.Termination);
                }
                Assert.Equal(expectedNodeFlag, observedPersistence);
            }
            finally
            {
                HeadlessPersistenceProbeEndNode.Observe = null;
                connectedField.SetValue(mysql, originalConnection);
            }
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

    private static byte[] CreateCanvas(CVBaseServerNode node, BaseStartNode start)
    {
        using var editor = new STNodeEditor();
        var end = new CVEndNode();
        start.Create();
        node.Create();
        end.Create();
        editor.Nodes.Add(start);
        editor.Nodes.Add(node);
        editor.Nodes.Add(end);
        Assert.Equal(
            ConnectionStatus.Connected,
            start.m_op_start.ConnectOption(node.GetAllInputOptions().Single(option => option.DataType == typeof(CVStartCFC))));
        Assert.Equal(
            ConnectionStatus.Connected,
            node.GetAllOutputOptions().Single(option => option.DataType == typeof(CVStartCFC)).ConnectOption(end.m_in_start));
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

public sealed class HeadlessPersistenceProbeEndNode : CVEndNode
{
    internal static Action<bool>? Observe { get; set; }

    protected override void DoNodeEnded(CVStartCFC startAction)
    {
        Observe?.Invoke(startAction.PersistResults);
        base.DoNodeEnded(startAction);
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

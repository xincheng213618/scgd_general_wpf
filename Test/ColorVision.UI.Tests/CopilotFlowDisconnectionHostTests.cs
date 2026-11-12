using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Integration;
using ST.Library.UI.NodeEditor;
using System.Reflection;
using System.Text.Json;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CopilotFlowDisconnectionHostTests
{
    [Fact]
    public void DefaultPatchHandlersPreviewAndApplyTheExactConnectionOnTheCurrentHost()
    {
        FlowExecutionEditingTests.WithCopilotGraph((host, editor) => WithCurrentHost(host, instance =>
        {
            var (source, target) = AddConnectedNodes(editor);
            var before = host.CaptureCopilotFlowSnapshot();
            var request = Request(before.Revision, source, target);
            var environment = new CopilotMcpToolEnvironment();

            var preview = environment.PreviewFlowPatchHandler(request, CancellationToken.None).GetAwaiter().GetResult();

            Assert.True(preview.Success, preview.Text);
            Assert.Equal(before.Revision, host.CaptureCopilotFlowSnapshot().Revision);
            using (var json = JsonDocument.Parse(preview.Text))
            {
                Assert.Equal("colorvision.flow-patch-preview.v1", json.RootElement.GetProperty("format").GetString());
                Assert.Equal("disconnect", json.RootElement.GetProperty("change").GetProperty("operation").GetString());
                var edge = json.RootElement.GetProperty("change").GetProperty("edge");
                Assert.Equal(source.Guid.ToString(), edge.GetProperty("sourceNodeId").GetString());
                Assert.Equal(target.Guid.ToString(), edge.GetProperty("targetNodeId").GetString());
                Assert.Equal("out:0", edge.GetProperty("sourcePortId").GetString());
                Assert.Equal("in:0", edge.GetProperty("targetPortId").GetString());
            }

            var applied = environment.ApplyFlowPatchHandler(request, CancellationToken.None).GetAwaiter().GetResult();

            Assert.True(applied.Success, applied.Text);
            Assert.Contains("operation=disconnect", applied.Text, StringComparison.Ordinal);
            Assert.Empty(editor.GetConnections());
            Assert.NotEqual(before.Revision, host.CaptureCopilotFlowSnapshot().Revision);
            var stale = environment.ApplyFlowPatchHandler(request, CancellationToken.None).GetAwaiter().GetResult();
            Assert.False(stale.Success);
            Assert.Equal("flow_patch_failed", stale.ErrorCode);
        }));
    }

    [Fact]
    public void APreDisconnectCallbackCannotBypassAChangedReadOnlyGuard()
    {
        FlowExecutionEditingTests.WithCopilotGraph((host, editor) => WithCurrentHost(host, instance =>
        {
            var (source, target) = AddConnectedNodes(editor);
            editor.EnableHistory = true;
            editor.ClearHistory();
            var before = host.CaptureCopilotFlowSnapshot();
            STNodeEditorOptionEventHandler becomeReadOnly = (_, _) => editor.EnableEdit = false;
            editor.OptionDisConnecting += becomeReadOnly;
            try
            {
                var result = new CopilotMcpToolEnvironment().ApplyFlowPatchHandler(Request(before.Revision, source, target), CancellationToken.None).GetAwaiter().GetResult();
                Assert.False(result.Success);
                Assert.Equal("flow_patch_failed", result.ErrorCode);
                Assert.Contains(target.Input, source.Output.ConnectedOption);
                Assert.Contains(source.Output, target.Input.ConnectedOption);
                Assert.Single(editor.GetConnections());
                Assert.Empty(editor.UndoHistory);
                Assert.False(editor.IsModified);
                Assert.Equal(before.Revision, host.CaptureCopilotFlowSnapshot().Revision);
            }
            finally
            {
                editor.OptionDisConnecting -= becomeReadOnly;
                editor.EnableEdit = true;
            }
        }));
    }

    [Fact]
    public void AQueuedPatchCannotEditThePreviousHostAfterTheCurrentEditorChanges()
    {
        FlowExecutionEditingTests.WithCopilotGraph((host, editor) => WithCurrentHost(host, instance =>
        {
            var (source, target) = AddConnectedNodes(editor);
            var before = host.CaptureCopilotFlowSnapshot();
            var request = Request(before.Revision, source, target);
            var environment = new CopilotMcpToolEnvironment();
            using var queued = new ManualResetEventSlim();
            var pending = Task.Run(async () =>
            {
                Task<CopilotMcpToolCallResult> operation;
                try { operation = environment.ApplyFlowPatchHandler(request, CancellationToken.None); }
                finally { queued.Set(); }
                return await operation;
            });
            // Keep the owning dispatcher occupied until the worker has queued its callback.
            queued.Wait();
            instance.SetValue(null, null);
            PumpUntil(pending, editor.Dispatcher);

            var result = pending.GetAwaiter().GetResult();

            Assert.False(result.Success);
            Assert.Equal("flow_scope_changed", result.ErrorCode);
            Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
            Assert.Single(editor.GetConnections());
            Assert.Equal(before.Revision, host.CaptureCopilotFlowSnapshot().Revision);
        }));
    }

    private static CopilotFlowPatchRequest Request(string revision, PortNode source, PortNode target) => new()
    {
        Operation = "disconnect", ExpectedRevision = revision,
        SourceNodeId = source.Guid.ToString(), SourcePortId = "out:0", TargetNodeId = target.Guid.ToString(), TargetPortId = "in:0",
    };

    private static (PortNode Source, PortNode Target) AddConnectedNodes(STNodeEditor editor)
    {
        var source = new PortNode();
        var target = new PortNode();
        source.Create(); target.Create();
        editor.Nodes.AddRange([source, target]);
        Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(target.Input));
        return (source, target);
    }

    private static void WithCurrentHost(FlowCopilotService host, Action<FieldInfo> action)
    {
        var manager = typeof(FlowCopilotService).GetField("_manager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host);
        var instance = typeof(FlowEngineManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = instance.GetValue(null);
        instance.SetValue(null, manager);
        try { action(instance); }
        finally { instance.SetValue(null, previous); }
    }

    private static void PumpUntil(Task task, Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        _ = task.ContinueWith(_ => { dispatcher.BeginInvoke(new Action(() => frame.Continue = false)); },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }

    private sealed class PortNode : STNode
    {
        public STNodeOption Input { get; private set; } = null!;
        public STNodeOption Output { get; private set; } = null!;
        protected override void OnCreate()
        {
            base.OnCreate();
            Input = InputOptions.Add("IN", typeof(int), false);
            Output = OutputOptions.Add("OUT", typeof(int), false);
        }
    }
}

#pragma warning disable CA1707,CA1861
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;
using System.Runtime.ExceptionServices;

namespace ColorVision.UI.Tests;

public class FlowEngineControlLifecycleTests
{
    [Fact]
    public void AttachIsIdempotentAndNodeRemovalUnsubscribesStartNode()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            editor.EnableHistory = true;
            var nodeManager = new FlowNodeManager();
            using var control = new InspectableFlowEngineControl(false, nodeManager);
            var startNode = CreateStartNode("Start-A");
            editor.Nodes.Add(startNode);

            control.AttachNodeEditor(editor);
            control.AttachNodeEditor(editor);

            int finishedCount = 0;
            control.Finished += (_, _) => finishedCount++;
            startNode.RaiseFinished("SN-1");

            Assert.Equal(1, finishedCount);
            Assert.Equal(new[] { "Start-A" }, control.GetStartNodeNames());

            editor.Nodes.Remove(startNode);
            startNode.RaiseFinished("SN-2");

            Assert.Equal(1, finishedCount);
            Assert.Empty(control.GetStartNodeNames());

            editor.Nodes.Add(startNode);
            startNode.RaiseFinished("SN-3");

            Assert.Equal(2, finishedCount);
            Assert.Equal(new[] { "Start-A" }, control.GetStartNodeNames());

            control.DetachNodeEditor();
            startNode.RaiseFinished("SN-4");

            Assert.Equal(2, finishedCount);
            Assert.Empty(control.GetStartNodeNames());
            Assert.Equal(1, editor.Nodes.Count);
        });
    }

    [Fact]
    public void RemovingServerNodeConvergesServicesAndOnlyItsDeviceRegistration()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            editor.EnableHistory = true;
            var nodeManager = new FlowNodeManager();
            using var control = new InspectableFlowEngineControl(editor, false, nodeManager);
            var serverA = CreateServerNode("Service.Test", "S01", "D01");
            var serverB = CreateServerNode("Service.Test", "S02", "D02");
            editor.Nodes.Add(serverA);
            editor.Nodes.Add(serverB);

            Assert.Equal(new[] { "S01", "S02" }, control.GetServiceCodes("Service.Test"));

            UpdateDevices(nodeManager, ("Service.Test", "S01", "D01", "token-a1"), ("Service.Test", "S02", "D02", "token-b1"));
            Assert.Equal("token-a1", serverA.Token);
            Assert.Equal("token-b1", serverB.Token);

            editor.Nodes.Remove(serverA);

            Assert.Equal(new[] { "S02" }, control.GetServiceCodes("Service.Test"));
            serverA.Token = "removed";
            serverB.Token = "before-second-update";
            UpdateDevices(nodeManager, ("Service.Test", "S01", "D01", "token-a2"), ("Service.Test", "S02", "D02", "token-b2"));

            Assert.Equal("removed", serverA.Token);
            Assert.Equal("token-b2", serverB.Token);

            control.FlowClear();
            control.FlowClear();
            serverB.Token = "cleared";
            UpdateDevices(nodeManager, ("Service.Test", "S02", "D02", "token-b3"));

            Assert.Equal("cleared", serverB.Token);
            Assert.False(editor.CanUndo);
            Assert.False(editor.CanRedo);
            Assert.Empty(control.GetServiceCodes("Service.Test"));
            Assert.Empty(control.GetStartNodeNames());
            Assert.Equal(0, editor.Nodes.Count);
        });
    }

    [Fact]
    public void RemovingResolvedAnonymousServerRemovesItsOriginalRegistration()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            editor.EnableHistory = true;
            var nodeManager = new FlowNodeManager();
            using var control = new InspectableFlowEngineControl(editor, false, nodeManager);
            var server = CreateServerNode("Service.Test", string.Empty, string.Empty);
            editor.Nodes.Add(server);

            UpdateDevices(nodeManager, ("Service.Test", "S01", "D01", "resolved"));

            Assert.Equal("S01", server.NodeName);
            Assert.Equal("D01", server.DeviceCode);
            Assert.Equal("resolved", server.Token);

            editor.Nodes.Remove(server);
            server.Token = "removed";
            UpdateDevices(nodeManager, ("Service.Test", "S01", "D01", "stale"));

            Assert.Equal("removed", server.Token);
        });
    }

    [Fact]
    public void RemovingRunningStartAllowsRemainingStartToRun()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            var nodeManager = new FlowNodeManager();
            using var control = new InspectableFlowEngineControl(editor, false, nodeManager);
            var startA = CreateStartNode("Start-A");
            var startB = CreateStartNode("Start-B");
            var sink = new StartSinkNode();
            sink.Create();
            editor.Nodes.Add(startA);
            editor.Nodes.Add(startB);
            editor.Nodes.Add(sink);
            Assert.Equal(ConnectionStatus.Connected, startA.m_op_start.ConnectOption(sink.Input));
            Assert.Equal(ConnectionStatus.Connected, startB.m_op_start.ConnectOption(sink.Input));

            control.StartByName("Start-A", "SN-A1");
            startA.AddActive("SN-A2");
            Assert.True(control.IsRunning);
            Assert.True(startA.Running);
            Assert.Equal(2, startA.ActiveCount);

            editor.Nodes.Remove(startA);

            Assert.False(control.IsRunning);
            Assert.False(startA.Running);
            Assert.Equal(0, startA.ActiveCount);
            control.StartByName("Start-B", "SN-B");
            Assert.True(control.IsRunning);
            Assert.True(startB.Running);
        });
    }

    [Fact]
    public void AttachingEditorWithRunningStartPreservesSingleRunGate()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            var nodeManager = new FlowNodeManager();
            var runningStart = CreateStartNode("Running");
            var waitingStart = CreateStartNode("Waiting");
            runningStart.Running = true;
            editor.Nodes.Add(runningStart);
            editor.Nodes.Add(waitingStart);
            using var control = new InspectableFlowEngineControl(false, nodeManager);

            control.AttachNodeEditor(editor);
            control.StartByName("Waiting", "SN-Waiting");

            Assert.True(control.IsRunning);
            Assert.False(waitingStart.Running);
        });
    }

    [Fact]
    public void EditorHistoryChangeInvalidatesLoadedCanvasCache()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            editor.EnableHistory = true;
            var nodeManager = new FlowNodeManager();
            using var control = new InspectableFlowEngineControl(editor, false, nodeManager);
            var start = CreateStartNode("Start");
            editor.Nodes.Add(start);
            control.SeedLoadedCanvasCache();

            start.Left += 10;

            Assert.Equal(0, control.LoadedCanvasCount);
        });
    }

    [Fact]
    public void UndoReAddKeepsAutoGeneratedStartName()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            editor.EnableHistory = true;
            var nodeManager = new FlowNodeManager();
            using var control = new InspectableFlowEngineControl(editor, true, nodeManager);
            var start = CreateStartNode("Original");
            editor.Nodes.Add(start);
            string generatedName = start.NodeName;
            Assert.NotEqual("Original", generatedName);
            editor.Nodes.Remove(start);

            editor.Undo();

            Assert.Equal(1, editor.Nodes.Count);
            Assert.Same(start, editor.Nodes[0]);
            Assert.Equal(generatedName, start.NodeName);
            Assert.Equal(new[] { generatedName }, control.GetStartNodeNames());
        });
    }

    [Fact]
    public void SwitchingAndDisposingControlDoNotMutateHostEditors()
    {
        RunInSta(() =>
        {
            using var firstEditor = new STNodeEditor();
            using var secondEditor = new STNodeEditor();
            var nodeManager = new FlowNodeManager();
            var firstStart = CreateStartNode("First");
            var secondStart = CreateStartNode("Second");
            var secondServer = CreateServerNode("Service.Test", "S01", "D01");
            firstEditor.Nodes.Add(firstStart);
            secondEditor.Nodes.Add(secondStart);
            secondEditor.Nodes.Add(secondServer);
            var control = new InspectableFlowEngineControl(false, nodeManager);

            control.FlowClear();
            control.AttachNodeEditor(firstEditor);
            Assert.Equal(new[] { "First" }, control.GetStartNodeNames());

            control.DetachNodeEditor(secondEditor);
            Assert.Equal(new[] { "First" }, control.GetStartNodeNames());

            control.AttachNodeEditor(secondEditor);
            Assert.Equal(new[] { "Second" }, control.GetStartNodeNames());
            Assert.Equal(1, firstEditor.Nodes.Count);
            Assert.Equal(2, secondEditor.Nodes.Count);

            firstEditor.Nodes.Add(CreateStartNode("Ignored"));
            Assert.Equal(new[] { "Second" }, control.GetStartNodeNames());

            control.Dispose();
            control.Dispose();
            secondServer.Token = "disposed";
            UpdateDevices(nodeManager, ("Service.Test", "S01", "D01", "stale"));

            Assert.Empty(control.GetStartNodeNames());
            Assert.Equal("disposed", secondServer.Token);
            Assert.Equal(2, secondEditor.Nodes.Count);
            Assert.Throws<ObjectDisposedException>(() => control.AttachNodeEditor(firstEditor));
        });
    }

    [Fact]
    public void StopConvergesStartAndControlRunningStateAcrossRegistryRebuild()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            var nodeManager = new FlowNodeManager();
            using var control = new InspectableFlowEngineControl(editor, false, nodeManager);
            var start = CreateStartNode("Start");
            var sink = new StartSinkNode();
            sink.Create();
            editor.Nodes.Add(start);
            editor.Nodes.Add(sink);
            Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(sink.Input));

            control.StartByName("Start", "SN-1");
            Assert.True(start.Running);
            Assert.True(control.IsRunning);

            control.StopNode("Start", "SN-1");
            Assert.False(start.Running);
            Assert.False(control.IsRunning);
            Assert.Equal(0, start.ActiveCount);

            var otherStart = CreateStartNode("Other");
            editor.Nodes.Add(otherStart);
            editor.Nodes.Remove(otherStart);

            Assert.False(control.IsRunning);
            control.StartByName("Start", "SN-2");
            Assert.True(start.Running);
            Assert.True(control.IsRunning);
        });
    }

    [Fact]
    public void StartWithoutConnectedOutputDoesNotRemainRunning()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            var nodeManager = new FlowNodeManager();
            using var control = new InspectableFlowEngineControl(editor, false, nodeManager);
            var start = CreateStartNode("Start");
            editor.Nodes.Add(start);

            control.StartByName("Start", "SN-1");

            Assert.Equal(0, start.ActiveCount);
            Assert.False(start.Running);
            Assert.False(control.IsRunning);
        });
    }

    [Fact]
    public void RemovingRunningStartCancelsActionsAndDisposesRuntimeResources()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            var nodeManager = new FlowNodeManager();
            using var control = new InspectableFlowEngineControl(editor, false, nodeManager);
            var start = CreateStartNode("Start");
            editor.Nodes.Add(start);
            var action = start.AddActive("SN-1");
            var resource = new DisposableProbe();
            action.RuntimeResources.Set("probe", resource);

            editor.Nodes.Remove(start);

            Assert.Equal(StatusTypeEnum.Canceled, action.FlowStatus);
            Assert.True(action.RuntimeResources.IsDisposed);
            Assert.True(resource.IsDisposed);
            Assert.Equal(0, start.ActiveCount);
            Assert.False(start.Running);
            Assert.False(control.IsRunning);
        });
    }

    [Fact]
    public void StartRenameAndUndoRebuildStartRegistry()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            editor.EnableHistory = true;
            var nodeManager = new FlowNodeManager();
            using var control = new InspectableFlowEngineControl(editor, false, nodeManager);
            var start = CreateStartNode("Before");
            editor.Nodes.Add(start);
            editor.ClearHistory();

            start.NodeName = "After";

            Assert.Equal(new[] { "After" }, control.GetStartNodeNames());
            editor.Undo();
            Assert.Equal("Before", start.NodeName);
            Assert.Equal(new[] { "Before" }, control.GetStartNodeNames());
        });
    }

    [Fact]
    public void ServerIdentityChangesReindexServicesAndDeviceRegistration()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            editor.EnableHistory = true;
            var nodeManager = new FlowNodeManager();
            using var control = new InspectableFlowEngineControl(editor, false, nodeManager);
            var server = CreateServerNode("Service.Old", "S01", "D01");
            editor.Nodes.Add(server);
            editor.ClearHistory();

            server.NodeType = "Service.New";
            server.NodeName = "S02";
            server.DeviceCode = "D02";

            Assert.Empty(control.GetServiceCodes("Service.Old"));
            Assert.Equal(new[] { "S02" }, control.GetServiceCodes("Service.New"));
            server.Token = "unchanged";
            UpdateDevices(nodeManager, ("Service.Old", "S01", "D01", "stale"));
            Assert.Equal("unchanged", server.Token);
            UpdateDevices(nodeManager, ("Service.New", "S02", "D02", "current"));
            Assert.Equal("current", server.Token);

            editor.Undo();
            Assert.Equal("D01", server.DeviceCode);
            editor.Undo();
            Assert.Equal("S01", server.NodeName);
            server.NodeType = "Service.Old";
            Assert.Equal("Service.Old", server.NodeType);
            Assert.Equal(new[] { "S01" }, control.GetServiceCodes("Service.Old"));
        });
    }

    [Fact]
    public void GraphChangesInvalidateCacheWhenHistoryIsDisabled()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            Assert.False(editor.EnableHistory);
            var nodeManager = new FlowNodeManager();
            using var control = new InspectableFlowEngineControl(editor, false, nodeManager);
            var start = CreateStartNode("Start");
            var sink = new StartSinkNode();
            sink.Create();
            editor.Nodes.Add(start);
            editor.Nodes.Add(sink);
            control.SeedLoadedCanvasCache();

            sink.Title = "Changed";
            Assert.Equal(0, control.LoadedCanvasCount);

            control.SeedLoadedCanvasCache();
            sink.Left += 10;
            Assert.Equal(0, control.LoadedCanvasCount);

            control.SeedLoadedCanvasCache();
            Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(sink.Input));

            Assert.Equal(0, control.LoadedCanvasCount);
        });
    }

    [Fact]
    public void StopAllRejectsNewActionsUntilTeardownCompletes()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            var start = CreateStartNode("Start");
            var sink = new StartSinkNode();
            sink.Create();
            editor.Nodes.Add(start);
            editor.Nodes.Add(sink);
            Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(sink.Input));
            var activeAction = start.AddActive("SN-1");
            using var resource = new BlockingDisposableProbe();
            activeAction.RuntimeResources.Set("blocking", resource);

            Task stopTask = Task.Run(start.StopAll);
            Assert.True(resource.Entered.Wait(TimeSpan.FromSeconds(5)));

            start.Start("SN-2");
            resource.Release.Set();

            Assert.True(stopTask.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(0, start.ActiveCount);
            Assert.False(start.Running);
            Assert.True(activeAction.RuntimeResources.IsDisposed);
        });
    }

    [Fact]
    public void StopAllCleansEveryActionWhenFinishingOneActionFails()
    {
        RunInSta(() =>
        {
            var start = CreateStartNode("Start");
            var firstAction = start.AddActive("SN-1");
            var secondAction = start.AddActive("SN-2");
            var firstResource = new DisposableProbe();
            var secondResource = new DisposableProbe();
            firstAction.RuntimeResources.Set("first", firstResource);
            secondAction.RuntimeResources.Set("second", secondResource);
            start.ThrowOnPublish = true;

            Assert.Throws<InvalidOperationException>(start.StopAll);

            Assert.Equal(0, start.ActiveCount);
            Assert.False(start.Running);
            Assert.True(firstResource.IsDisposed);
            Assert.True(secondResource.IsDisposed);
        });
    }

    [Fact]
    public void FinishedAndNodeChurnAreSerialized()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor();
            var nodeManager = new FlowNodeManager();
            using var control = new InspectableFlowEngineControl(editor, false, nodeManager);
            var stableStart = CreateStartNode("Stable");
            editor.Nodes.Add(stableStart);
            var churnNodes = Enumerable.Range(0, 64).Select(index => CreateStartNode($"Churn-{index}")).ToArray();
            foreach (var node in churnNodes)
            {
                editor.Nodes.Add(node);
            }
            using var gate = new ManualResetEventSlim();
            Task completionTask = Task.Run(() =>
            {
                gate.Wait();
                for (int i = 0; i < 2_000; i++)
                {
                    stableStart.RaiseFinished($"SN-{i}");
                }
            });

            gate.Set();
            for (int i = 0; i < 2_000; i++)
            {
                TestStartNode node = churnNodes[i % churnNodes.Length];
                editor.Nodes.Remove(node);
                editor.Nodes.Add(node);
            }

            Assert.True(completionTask.Wait(TimeSpan.FromSeconds(10)));
        });
    }

    private static TestStartNode CreateStartNode(string name)
    {
        var node = new TestStartNode(name);
        node.Create();
        return node;
    }

    private static TestServerNode CreateServerNode(string serviceType, string serviceCode, string deviceCode)
    {
        var node = new TestServerNode(serviceType, serviceCode, deviceCode);
        node.Create();
        return node;
    }

    private static void UpdateDevices(FlowNodeManager nodeManager, params (string Type, string Code, string Device, string Token)[] devices)
    {
        var services = devices
            .GroupBy(device => (device.Type, device.Code, device.Token))
            .Select(group =>
            {
                var service = new MQTTServiceInfo
                {
                    ServiceType = group.Key.Type,
                    ServiceCode = group.Key.Code,
                    Token = group.Key.Token
                };
                foreach (var device in group)
                {
                    service.AddDevice(device.Device, device.Device);
                }
                return service;
            })
            .ToList();
        nodeManager.UpdateDevice(services);
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
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
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private sealed class InspectableFlowEngineControl : FlowEngineControl
    {
        public InspectableFlowEngineControl(bool isAutoStartName, FlowNodeManager nodeManager)
            : base(isAutoStartName, nodeManager)
        {
        }

        public InspectableFlowEngineControl(STNodeEditor nodeEditor, bool isAutoStartName, FlowNodeManager nodeManager)
            : base(nodeEditor, isAutoStartName, nodeManager)
        {
        }

        public string[] GetServiceCodes(string serviceType)
        {
            return services.TryGetValue(serviceType, out ServiceNode? service)
                ? service.MQTTServices.Keys.OrderBy(code => code).ToArray()
                : Array.Empty<string>();
        }

        public void StartByName(string name, string serialNumber)
        {
            StartNode(name, serialNumber);
        }

        public int LoadedCanvasCount => loadedCanvas.Count;

        public void SeedLoadedCanvasCache()
        {
            loadedCanvas["cached"] = new byte[] { 1 };
        }
    }

    private sealed class TestStartNode : BaseStartNode
    {
        public TestStartNode(string name)
            : base("Test start")
        {
            NodeName = name;
        }

        public void RaiseFinished(string serialNumber)
        {
            FireFinished(new CVStartCFC(serialNumber));
        }

        public int ActiveCount => startActions.Count;

        public bool ThrowOnPublish { get; set; }

        public CVStartCFC AddActive(string serialNumber)
        {
            var action = new CVStartCFC(this, ActionTypeEnum.Start, serialNumber);
            startActions.Add(serialNumber, action);
            Running = true;
            return action;
        }

        public override void DoPublishStatus(string msg)
        {
            if (ThrowOnPublish)
            {
                throw new InvalidOperationException("Test finishing failure.");
            }
        }
    }

    private sealed class DisposableProbe : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class BlockingDisposableProbe : IDisposable
    {
        public ManualResetEventSlim Entered { get; } = new();

        public ManualResetEventSlim Release { get; } = new();

        public void Dispose()
        {
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class TestServerNode : CVBaseServerNode
    {
        public TestServerNode(string serviceType, string serviceCode, string deviceCode)
            : base("Test server", serviceType, serviceCode, deviceCode)
        {
        }
    }

    private sealed class StartSinkNode : STNode
    {
        public STNodeOption Input { get; private set; } = STNodeOption.Empty;

        protected override void OnCreate()
        {
            base.OnCreate();
            Input = InputOptions.Add("IN", typeof(CVStartCFC), bSingle: false);
        }
    }
}

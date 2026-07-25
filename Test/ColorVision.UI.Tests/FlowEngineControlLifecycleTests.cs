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

        public void AddActive(string serialNumber)
        {
            startActions.Add(serialNumber, new CVStartCFC(this, ActionTypeEnum.Start, serialNumber));
            Running = true;
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

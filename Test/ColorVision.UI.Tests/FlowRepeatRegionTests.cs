using ColorVision.Engine.Services.Devices.Camera.Local;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Repeat;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using Point = System.Drawing.Point;
using System.IO;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Themes;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorVision.Engine.FlowProcessing.Nodes;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class FlowRepeatRegionTests
{
    [Fact]
    public async Task ServiceNodeRepeatsThroughExistingResponsePathAndCompletesOnce()
    {
        using var container = new CVNodeContainer();
        var start = new RuntimeTestStartNode();
        var sensor = new TempCommonSensorNode();
        var end = new RuntimeTestEndNode();
        foreach (STNode node in new STNode[] { start, sensor, end }) { node.Create(); container.Nodes.Add(node); }
        start.m_op_start.ConnectOption(sensor.GetAllInputOptions().Single(port => port.DataType == typeof(CVStartCFC)), false);
        sensor.GetAllOutputOptions().Single(port => port.DataType == typeof(CVStartCFC)).ConnectOption(end.m_in_start, false);
        Assert.True(FlowRepeatRegion.TryCreate(new[] { sensor }, out _, out _));
        sensor.SetRepeatRegion(Guid.NewGuid().ToString(), 4, 1);
        using var control = new FlowEngineControl(container, false, new FlowNodeManager());
        var finished = new TaskCompletionSource<StatusTypeEnum>(TaskCreationOptions.RunContinuationsAsynchronously);
        control.Finished += (_, args) => finished.TrySetResult(args.Status);
        start.OnPublish = command =>
        {
            CVMQTTRequest request = JsonConvert.DeserializeObject<CVMQTTRequest>(command.Message)!;
            Assert.True(sensor.DoServerStatusRecv(new CVBaseDataFlowResp
            {
                Code = 0, Message = "ok", Version = request.Version, ServiceName = request.ServiceCode,
                DeviceNodeCode = sensor.NodeID, EventName = request.EventName, SerialNumber = request.SerialNumber,
                MsgID = request.MsgID, SendTime = DateTime.Now, ZIndex = request.ZIndex
            }));
        };
        Assert.True(control.TryStartNode(start.NodeName, "repeat-service"));
        Assert.Equal(StatusTypeEnum.Completed, await finished.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(4, start.PublishCount);
        Assert.Equal(1, end.RunningCount);
    }

    [Fact]
    public async Task AsynchronousLocalNodeRetainsItsInputForEveryRound()
    {
        var body = new LocalProbeNode();
        var after = new ProbeNode();
        body.Create(); after.Create();
        body.SetRepeatRegion(Guid.NewGuid().ToString(), 3, 1);
        body.GetAllOutputOptions().Single().ConnectOption(after.Input, false);
        using var action = new RunAction();
        action.Value.MasterValue("input", 17, 1);
        var completed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        after.Work = result => completed.TrySetResult((int)result.Data["MasterId"]);
        body.GetAllInputOptions().Single().ReceiveData(action.Value);
        Assert.Equal(20, await completed.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(new[] { 17, 17, 17 }, body.MasterInputs);
        Assert.Equal(1, after.Count);
    }

    [Fact]
    public void CountEditorRejectsInvalidInputAndRegionMenuKeepsNodesWhenRemoving()
    {
        WpfTestHost.Invoke(() =>
        {
            foreach (var uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) });
            var dialog = new FlowRepeatCountWindow(10) { Left = -16000, Top = -16000, ShowActivated = false };
            try
            {
                dialog.Show();
                var panel = (StackPanel)dialog.FindName("CountPanel");
                TextBox input = panel.Children.OfType<DockPanel>().Single().Children.OfType<TextBox>().Single();
                foreach (string invalid in new[] { "abc", "0", "-2", "1.5" })
                {
                    input.Text = invalid;
                    Assert.False(dialog.ValidateCount());
                }
                input.Text = "12";
                Assert.True(dialog.ValidateCount());
                Assert.Equal(12, dialog.Count);
                SaveImage(dialog, "repeat-count.png");
            }
            finally { dialog.Close(); }

            using var editor = new STNodeEditor { EnableHistory = true, VisualTheme = STNodeVisualTheme.Light, ShowLocation = false, ShowCanvasDragLockButton = false };
            var nodes = Enumerable.Range(0, 3).Select(index => new ProbeNode { Left = 45 + index * 245, Top = 110 }).ToArray();
            foreach (ProbeNode node in nodes) { node.Create(); editor.Nodes.Add(node); }
            nodes[0].Title = "L/BV相机"; nodes[1].Title = "发光区定位"; nodes[2].Title = "FOV计算";
            nodes[0].Output.ConnectOption(nodes[1].Input); nodes[1].Output.ConnectOption(nodes[2].Input);
            Repeat(nodes.Take(1), 10);
            Repeat(nodes.Skip(1), 3);
            var window = new Window { Content = editor, Width = 820, Height = 360, Left = -16000, Top = -16000, ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                Assert.Equal(2, editor.GetNodeGroups().Length);
                SaveImage(window, "repeat-regions.png");
                using var menus = new FlowNodeContextMenuService(editor, new FlowExecutionNavigator(editor), () => { });
                var menu = new ContextMenu();
                typeof(FlowNodeContextMenuService).GetMethod("AddRepeatMenuItems", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(menus, new object?[] { menu.Items, nodes[0] });
                MenuItem remove = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, FlowRepeatRegion.Text("取消重复框")));
                remove.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Empty(nodes[0].RepeatRegionId);
                Assert.Equal(3, editor.Nodes.Count);
                Assert.Equal(1, nodes[0].Output.ConnectionCount);
                editor.Undo();
                Assert.Equal(2, editor.GetNodeGroups().Length);
            }
            finally { window.Close(); }
        });
    }

    private static void SaveImage(Window window, string file)
    {
        window.UpdateLayout();
        var content = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        string directory = Path.Combine(AppContext.BaseDirectory, "editor-evidence");
        Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory, file));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(10000)]
    public void SingleRegionEmitsOnlyFinalResultWithoutRecursiveStackGrowth(int count)
    {
        using var graph = new Graph(3);
        Repeat(graph.Nodes.Take(1), count);
        graph.Nodes[0].Work = action => action.Data["value"] = graph.Nodes[0].Count;
        using var action = new RunAction();
        graph.Nodes[0].Input.ReceiveData(action.Value);
        Assert.Equal(count, graph.Nodes[0].Count);
        Assert.Equal(1, graph.Nodes[1].Count);
        Assert.Equal(count, Assert.Single(graph.Nodes[1].Inputs));
        Assert.Equal(1, graph.Nodes[2].Count);
    }

    [Fact]
    public void WholeRegionRepeatsInOrderFromOriginalInputAndRegionsHaveIndependentCounts()
    {
        using var graph = new Graph(5);
        Repeat(graph.Nodes.Take(2), 3);
        Repeat(graph.Nodes.Skip(2).Take(2), 2);
        var order = new List<int>();
        for (int i = 0; i < graph.Nodes.Length; i++)
        {
            int index = i;
            graph.Nodes[i].Work = action => { order.Add(index); action.Data["value"] = (int)action.Data["value"] + 1; };
        }
        using var action = new RunAction(7);
        graph.Nodes[0].Input.ReceiveData(action.Value);
        Assert.Equal(new[] { 0, 1, 0, 1, 0, 1, 2, 3, 2, 3, 4 }, order);
        Assert.Equal(new[] { 7, 7, 7 }, graph.Nodes[0].Inputs);
        Assert.Equal(new[] { 9, 9 }, graph.Nodes[2].Inputs);
        Assert.Equal(11, Assert.Single(graph.Nodes[4].Inputs));
    }

    [Fact]
    public void DelayedAndDuplicateResponsesDoNotLeakIntermediateResults()
    {
        using var graph = new Graph(2);
        Repeat(graph.Nodes.Take(1), 2);
        ProbeNode body = graph.Nodes[0];
        body.Manual = true;
        using var action = new RunAction();
        body.Input.ReceiveData(action.Value);
        Assert.Equal(1, body.Count);
        Assert.Equal(0, graph.Nodes[1].Count);
        CVStartCFC first = body.Pending.Dequeue();
        body.Output.TransferData(first);
        Assert.Equal(2, body.Count);
        Assert.Equal(0, graph.Nodes[1].Count);
        body.Output.TransferData(first);
        Assert.Equal(2, body.Count);
        CVStartCFC last = body.Pending.Dequeue();
        body.Output.TransferData(last);
        body.Output.TransferData(last);
        Assert.Equal(1, graph.Nodes[1].Count);
    }

    [Theory]
    [InlineData(StatusTypeEnum.Canceled)]
    [InlineData(StatusTypeEnum.Failed)]
    [InlineData(StatusTypeEnum.OverTime)]
    public void TerminalStatusDoesNotRestartOrRunDownstreamWork(StatusTypeEnum status)
    {
        using var graph = new Graph(2);
        Repeat(graph.Nodes.Take(1), 10);
        graph.Nodes[0].Work = action => action.FlowStatus = status;
        using var action = new RunAction();
        graph.Nodes[0].Input.ReceiveData(action.Value);
        Assert.Equal(1, graph.Nodes[0].Count);
        Assert.Equal(0, graph.Nodes[1].Count);
        Assert.Equal(status, graph.Nodes[1].LastReceived!.FlowStatus);
    }

    [Fact]
    public void DisposedRunIgnoresLateResponseAndFreshRunStartsAtOne()
    {
        using var graph = new Graph(2);
        Repeat(graph.Nodes.Take(1), 3);
        ProbeNode body = graph.Nodes[0];
        body.Manual = true;
        using (var first = new RunAction())
        {
            body.Input.ReceiveData(first.Value);
            CVStartCFC late = body.Pending.Dequeue();
            first.Dispose();
            body.Output.TransferData(late);
            Assert.Equal(1, body.Count);
            Assert.Equal(0, graph.Nodes[1].Count);
        }
        body.Manual = false;
        using var second = new RunAction();
        body.Input.ReceiveData(second.Value);
        Assert.Equal(4, body.Count);
        Assert.Equal(1, graph.Nodes[1].Count);
    }

    [Fact]
    public void IntermediateFramesAreReleasedAndUpstreamFrameIsReused()
    {
        using var graph = new Graph(2);
        Repeat(graph.Nodes.Take(1), 3);
        using var action = new RunAction();
        var original = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 4, 0);
        action.Value.SetCurrentFrame(original);
        var frames = new List<LocalFlowFrame>();
        graph.Nodes[0].Work = current =>
        {
            Assert.True(current.TryGetCurrentFrame(out LocalFlowFrame? input));
            Assert.Same(original, input);
            if (frames.Count > 0) Assert.Throws<ObjectDisposedException>(() => frames[^1].Acquire());
            var next = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 4, 0);
            frames.Add(next);
            current.SetCurrentFrame(next);
        };
        graph.Nodes[0].Input.ReceiveData(action.Value);
        Assert.Equal(3, frames.Count);
        Assert.True(graph.Nodes[1].LastReceived!.TryGetCurrentFrame(out LocalFlowFrame? final));
        Assert.Same(frames[2], final);
        using var originalLease = original.Acquire();
        using var finalLease = final!.Acquire();
    }

    [Fact]
    public void InvalidOrIncompleteRegionFailsBeforeExecutingAnyBodyNode()
    {
        using var graph = new Graph(3);
        Assert.False(FlowRepeatRegion.TryCreate(new[] { graph.Nodes[0], graph.Nodes[2] }, out _, out _));
        Repeat(graph.Nodes.Take(2), 3);
        graph.Nodes[1].SetRepeatRegion(string.Empty, 1, 0);
        Assert.False(FlowRepeatRegion.TryValidate(graph.Nodes, out _));
        using var action = new RunAction();
        graph.Nodes[0].Input.ReceiveData(action.Value);
        Assert.Equal(0, graph.Nodes[0].Count);
        Assert.Equal(StatusTypeEnum.Failed, action.Value.FlowStatus);
    }

    [Fact]
    public void RegionSettingsSurviveSaveUndoAndCopyWithoutMergingRegions()
    {
        StaTest.Run(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true };
            var first = new ProbeNode { Left = 40, Top = 70 };
            var second = new ProbeNode { Left = 240, Top = 70 };
            first.Create();
            second.Create();
            editor.Nodes.Add(first);
            editor.Nodes.Add(second);
            first.Output.ConnectOption(second.Input);
            editor.ExecuteEditTransaction("repeat", () => Repeat(new[] { first, second }, 4));
            string id = first.RepeatRegionId;
            Assert.Single(editor.GetNodeGroups());
            editor.Undo();
            Assert.Empty(editor.GetNodeGroups());
            editor.Redo();
            Assert.Equal(id, first.RepeatRegionId);
            editor.ExecuteEditTransaction("count", () => { first.SetRepeatRegion(id, 7, 2); second.SetRepeatRegion(id, 7, 2); });
            editor.Undo();
            Assert.Equal(4, first.RepeatCount);
            using var saved = new MemoryStream();
            editor.SaveCanvas(saved);
            using var container = new CVNodeContainer();
            container.LoadCanvas(saved.ToArray());
            ProbeNode[] loaded = container.Nodes.OfType<ProbeNode>().ToArray();
            Assert.All(loaded, node => { Assert.Equal(id, node.RepeatRegionId); Assert.Equal(4, node.RepeatCount); });
            using var action = new RunAction();
            loaded.Single(node => node.Left == 40).Input.ReceiveData(action.Value);
            Assert.All(loaded, node => Assert.Equal(4, node.Count));
            var copy = editor.ImportSelectionData(editor.GetNodesData(new[] { first, second }), new Point(40, 240)).Cast<ProbeNode>().ToArray();
            Assert.Equal(2, editor.GetNodeGroups().Length);
            Assert.NotEqual(id, copy[0].RepeatRegionId);
            Assert.Equal(copy[0].RepeatRegionId, copy[1].RepeatRegionId);
            var partial = Assert.IsType<ProbeNode>(Assert.Single(editor.ImportSelectionData(editor.GetNodesData(new[] { first }), new Point(440, 240))));
            Assert.Empty(partial.RepeatRegionId);
            editor.ExecuteEditTransaction("remove", () => { first.SetRepeatRegion("", 1, 0); second.SetRepeatRegion("", 1, 0); });
            Assert.Equal(1, first.Output.ConnectionCount);
            editor.Undo();
            Assert.Equal(id, first.RepeatRegionId);
        });
    }

    private static void Repeat(IEnumerable<ProbeNode> nodes, int count)
    {
        ProbeNode[] members = nodes.ToArray();
        string id = Guid.NewGuid().ToString();
        foreach (ProbeNode node in members) node.SetRepeatRegion(id, count, members.Length);
    }

    private sealed class RunAction : IDisposable
    {
        public CVStartCFC Value { get; }
        public RunAction(int input = 0)
        {
            Value = new CVStartCFC("repeat-test");
            Value.FlowStatus = StatusTypeEnum.Runing;
            Value.Data["value"] = input;
        }
        public void Dispose() => Value.RuntimeResources.Dispose();
    }

    private sealed class Graph : IDisposable
    {
        public ProbeNode[] Nodes { get; }
        public Graph(int count)
        {
            Nodes = Enumerable.Range(0, count).Select(_ => new ProbeNode()).ToArray();
            foreach (ProbeNode node in Nodes) node.Create();
            for (int i = 0; i < count - 1; i++) Nodes[i].Output.ConnectOption(Nodes[i + 1].Input, false);
        }
        public void Dispose() { foreach (ProbeNode node in Nodes) node.Output.DisConnectionAll(); }
    }

    [STNode("Tests")]
    public sealed class ProbeNode : CVCommonNode
    {
        public STNodeOption Input { get; private set; } = null!;
        public STNodeOption Output { get; private set; } = null!;
        public int Count { get; private set; }
        public List<int> Inputs { get; } = new();
        public Queue<CVStartCFC> Pending { get; } = new();
        public CVStartCFC? LastReceived { get; private set; }
        public Action<CVStartCFC>? Work { get; set; }
        public bool Manual { get; set; }
        public ProbeNode() : base("Probe", "Probe", "Probe") { }
        protected override void OnCreate()
        {
            base.OnCreate();
            Input = InputOptions.Add("IN", typeof(CVStartCFC), true);
            Output = OutputOptions.Add("OUT", typeof(CVStartCFC), false);
            Input.DataTransfer += (_, args) =>
            {
                if (args.TargetOption.Data is not CVStartCFC received) return;
                LastReceived = received;
                if (!received.IsRunning) { Output.TransferData(received); return; }
                var action = new CVStartCFC(received);
                Inputs.Add((int)action.Data["value"]);
                Count++;
                if (Work != null) Work(action);
                else action.Data["value"] = (int)action.Data["value"] + 1;
                if (Manual) Pending.Enqueue(action);
                else Output.TransferData(action);
            };
        }
    }

    private sealed class LocalProbeNode : LocalFlowNodeBase
    {
        private int count;
        public System.Collections.Concurrent.ConcurrentQueue<int> MasterInputs { get; } = new();
        public LocalProbeNode() : base("Local probe", "LocalProbe", "LocalProbe") { }
        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            if (!TryGetInputMasterResult(action, 0, out int masterId, out _, out _))
                throw new InvalidOperationException("The current iteration lost its input snapshot.");
            MasterInputs.Enqueue(masterId);
            action.MasterValue("output", masterId + Interlocked.Increment(ref count), 1);
            return new LocalNodeExecutionResult();
        }
    }
}

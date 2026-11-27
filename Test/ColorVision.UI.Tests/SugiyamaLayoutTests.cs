using ColorVision.Engine.FlowProcessing.Editor;
using FlowEngineLib.Base;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;
using System.Drawing;

namespace ColorVision.UI.Tests;

public sealed class SugiyamaLayoutTests
{
    [Fact]
    public void AutoLayoutReducesPortCrossingsAndReordersMergeInputs()
    {
        StaTest.Run(() =>
        {
            LayoutNode start = new("Start", 3);
            LayoutNode first = new("First capture", 6);
            LayoutNode second = new("Second capture", 6);
            LayoutNode upper = new("Upper analysis", 6);
            LayoutNode lower = new("Lower analysis", 6);
            LayoutNode merge = new("LogicalAND", 3);
            LayoutNode end = new("End", 2);
            LayoutNode disconnected = new("Disconnected", 2) { Left = 17, Top = 25 };
            foreach (LayoutNode node in new[] { start, first, second, upper, lower, merge, end, disconnected })
                node.Create();

            ConnectionInfo[] connections =
            [
                Connect(second, 5, lower, 4),
                Connect(start, 0, first, 0),
                Connect(start, 1, second, 0),
                Connect(merge, 0, end, 0),
                Connect(second, 0, upper, 2),
                new() { Output = second.DataOut, Input = upper.DataIn },
                Connect(first, 5, upper, 5),
                Connect(lower, 0, merge, 0),
                new() { Output = first.DataOut, Input = lower.DataIn },
                Connect(first, 0, lower, 2),
                Connect(upper, 0, merge, 1),
            ];

            new SugiyamaLayout(connections, 40, 35, 220, 80).Execute(start);

            // The crossing-reduction swaps reverse the capture order produced by the barycenter sweeps.
            Assert.True(second.Top < first.Top);
            Assert.True(upper.Top < lower.Top);
            Assert.Equal(new[] { merge.Inputs[1], merge.Inputs[0], merge.Inputs[2], merge.DataIn }, merge.GetAllInputOptions());
            Assert.Equal((17, 25), (disconnected.Left, disconnected.Top));
        });
    }

    [Fact]
    public void AttachedAutoLayoutUpdatesCanvasBoundsAndRemainsUndoable()
    {
        StaTest.Run(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true, ClientSize = new Size(1200, 800) };
            using var start = new LayoutStartNode { Location = new Point(800, 600) };
            var next = new LayoutNode("Capture", 2) { Location = new Point(-450, -300) };
            var disconnected = new LayoutNode("Disconnected", 1) { Location = new Point(300, 200) };
            foreach (STNode node in new STNode[] { start, next, disconnected })
            {
                node.Create();
                editor.Nodes.Add(node);
            }
            Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(next.Inputs[0]));
            editor.ClearHistory();
            Point[] before = editor.Nodes.Cast<STNode>().Select(node => node.Location).ToArray();
            Rectangle boundsBefore = editor.CanvasValidBounds;

            new FlowGraphLayoutService(editor).Apply(40, 35);

            Point[] after = editor.Nodes.Cast<STNode>().Select(node => node.Location).ToArray();
            Rectangle expectedBounds = Rectangle.Union(Rectangle.Union(start.Rectangle, next.Rectangle), disconnected.Rectangle);
            expectedBounds.Inflate(60, 60);
            Assert.Equal(expectedBounds, editor.CanvasValidBounds);
            Assert.NotEqual(boundsBefore, editor.CanvasValidBounds);
            Assert.Equal(before[2], disconnected.Location);
            Assert.Single(editor.GetConnections());

            editor.Undo();
            Assert.Equal(before, editor.Nodes.Cast<STNode>().Select(node => node.Location));
            Assert.Equal(boundsBefore, editor.CanvasValidBounds);
            editor.Redo();
            Assert.Equal(after, editor.Nodes.Cast<STNode>().Select(node => node.Location));
            Assert.Equal(expectedBounds, editor.CanvasValidBounds);
        });
    }

    private sealed class LayoutStartNode() : BaseStartNode("Start") { }

    private static ConnectionInfo Connect(LayoutNode from, int output, LayoutNode to, int input)
        => new() { Output = from.Outputs[output], Input = to.Inputs[input] };

    private sealed class LayoutNode(string title, int portCount) : STNode
    {
        internal List<STNodeOption> Inputs { get; } = [];
        internal List<STNodeOption> Outputs { get; } = [];
        internal STNodeOption DataIn { get; private set; } = null!;
        internal STNodeOption DataOut { get; private set; } = null!;

        protected override void OnCreate()
        {
            base.OnCreate();
            Title = title;
            for (int port = 0; port < portCount; port++)
            {
                Inputs.Add(InputOptions.Add(port == 0 ? "IN" : $"IN_LOOP{port}", typeof(CVStartCFC), false));
                Outputs.Add(OutputOptions.Add(port == 0 ? "OUT" : $"OUT_LOOP{port}", typeof(CVStartCFC), false));
            }

            DataIn = InputOptions.Add("VALUE_IN", typeof(float), false);
            DataOut = OutputOptions.Add("VALUE_OUT", typeof(float), false);
        }
    }
}

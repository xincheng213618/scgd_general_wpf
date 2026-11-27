using ST.Library.UI.NodeEditor;
using System.Reflection;
using System.Windows.Input;
using Drawing = System.Drawing;

namespace ColorVision.UI.Tests;

public class STNodeConnectionInteractionTests
{
    [Fact]
    public void HoverFindsTheFirstOverlappingConnectionAlongItsDetour()
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new ConnectionEditor { EnableEdit = true, LimitCanvasToContentBounds = false };
            var first = new FlowMenuTestNode();
            var second = new FlowMenuTestNode();
            var target = new FlowMenuTestNode();
            foreach (var node in new[] { first, second, target })
                node.Create();
            first.Left = second.Left = 400;
            target.Left = 80;
            first.Top = second.Top = target.Top = 70;
            editor.Nodes.AddRange([first, second, target]);

            var firstOutput = first.GetAllOutputOptions().Single();
            var secondOutput = second.GetAllOutputOptions().Single();
            // Use distinct inputs at identical coordinates so the two curves overlap.
            var otherTarget = new FlowMenuTestNode();
            otherTarget.Create();
            otherTarget.Location = target.Location;
            editor.Nodes.Add(otherTarget);
            var input = target.GetAllInputOptions().Single();
            var otherInput = otherTarget.GetAllInputOptions().Single();
            Assert.Equal(ConnectionStatus.Connected, firstOutput.ConnectOption(input));
            Assert.Equal(ConnectionStatus.Connected, secondOutput.ConnectOption(otherInput));
            editor.DrawConnections();

            var detour = new Drawing.PointF(
                (firstOutput.DotLeft + firstOutput.DotSize + input.DotLeft - 1) / 2f,
                Math.Max(firstOutput.DotTop + firstOutput.DotSize / 2, input.DotTop + input.DotSize / 2) + 70);
            Assert.False(editor.SetHoveredConnection(new Drawing.PointF(-500, -500)));
            Assert.True(editor.SetHoveredConnection(detour));
            Assert.Equal(ConnectionStatus.DisConnected, editor.DisconnectHovered());
            var remaining = Assert.Single(editor.GetConnections());
            Assert.Same(secondOutput, remaining.Output);
        });
    }

    [Fact]
    public void BecomingReadOnlyDuringConnectionDragCancelsTheEditAndLeavesHistoryClean()
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new ConnectionEditor { EnableHistory = true, LimitCanvasToContentBounds = false };
            var source = new FlowMenuTestNode();
            var target = new FlowMenuTestNode();
            source.Create(); target.Create();
            source.Left = 100;
            target.Left = 400;
            editor.Nodes.AddRange([source, target]);
            STNodeOption output = source.GetAllOutputOptions().Single();
            STNodeOption input = target.GetAllInputOptions().Single();
            editor.ClearHistory();

            editor.BeginConnection(output);
            editor.EnableEdit = false;
            editor.ReleaseOver(input);

            Assert.Empty(editor.GetConnections());
            Assert.False(editor.IsModified);
            Assert.False(editor.CanUndo);

            editor.EnableEdit = true;
            editor.BeginConnection(output);
            editor.ReleaseOver(input);

            Assert.Single(editor.GetConnections());
            Assert.True(editor.IsModified);
            editor.Undo();
            Assert.Empty(editor.GetConnections());
            Assert.False(editor.CanUndo);
        });
    }

    private sealed class ConnectionEditor : STNodeEditor
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        public void DrawConnections()
        {
            using var bitmap = new Drawing.Bitmap(800, 300);
            using var graphics = Drawing.Graphics.FromImage(bitmap);
            OnDrawConnectedLine(new DrawingTools { Graphics = graphics });
        }

        public bool SetHoveredConnection(Drawing.PointF point)
        {
            object? path = typeof(STNodeEditor).GetMethod("FindHoveredConnection", PrivateInstance)!.Invoke(this, [point]);
            typeof(STNodeEditor).GetField("m_gp_hover", PrivateInstance)!.SetValue(this, path);
            return path != null;
        }

        public ConnectionStatus DisconnectHovered()
            => (ConnectionStatus)typeof(STNodeEditor).GetMethod("DisConnectionHover", PrivateInstance)!.Invoke(this, null)!;

        public void BeginConnection(STNodeOption output)
        {
            m_pt_down_in_control = new System.Drawing.Point(-10000, -10000);
            typeof(STNodeEditor).GetMethod("StartConnect", PrivateInstance)!.Invoke(this, [output]);
        }

        public void ReleaseOver(STNodeOption input)
        {
            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left);
            System.Windows.Point pointer = args.GetPosition(this);
            MoveCanvas((float)pointer.X - input.DotLeft - input.DotSize / 2f,
                (float)pointer.Y - input.DotTop - input.DotSize / 2f, false, CanvasMoveArgs.All);
            OnMouseUp(args);
        }
    }
}

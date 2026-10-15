using ST.Library.UI.NodeEditor;
using System.Reflection;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

public class STNodeConnectionInteractionTests
{
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
        public void BeginConnection(STNodeOption output)
        {
            m_pt_down_in_control = new System.Drawing.Point(-10000, -10000);
            typeof(STNodeEditor).GetMethod("StartConnect", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this, [output]);
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

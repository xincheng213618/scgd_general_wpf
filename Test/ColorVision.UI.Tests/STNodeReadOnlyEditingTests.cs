using ST.Library.UI.NodeEditor;
using System.Drawing;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class STNodeReadOnlyEditingTests
{
    [Fact]
    public void ReadOnlyKeepsUndoRedoAndSavedStateUntilEditingResumes()
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true };
            var node = new STNodeHub { Location = Point.Empty };
            node.Create();
            editor.Nodes.Add(node);
            editor.ClearHistory();
            editor.ExecuteEditTransaction("Move horizontally", () => node.Left = 100);
            editor.MarkSaved();
            editor.ExecuteEditTransaction("Move vertically", () => node.Top = 200);
            editor.Undo();
            STNodeEditHistoryEntry undo = Assert.Single(editor.UndoHistory);
            STNodeEditHistoryEntry redo = Assert.Single(editor.RedoHistory);
            Assert.Equal(new Point(100, 0), node.Location);
            Assert.False(editor.IsModified);

            editor.EnableEdit = false;
            Assert.False(editor.CanUndo);
            Assert.False(editor.CanRedo);
            editor.Undo();
            editor.Redo();

            Assert.Equal(new Point(100, 0), node.Location);
            Assert.Same(undo, Assert.Single(editor.UndoHistory));
            Assert.Same(redo, Assert.Single(editor.RedoHistory));
            Assert.False(editor.IsModified);

            editor.EnableEdit = true;
            Assert.True(editor.CanUndo);
            Assert.True(editor.CanRedo);
            editor.Redo();
            Assert.Equal(new Point(100, 200), node.Location);
            Assert.True(editor.IsModified);
            editor.Undo();
            Assert.Equal(new Point(100, 0), node.Location);
            Assert.False(editor.IsModified);
            editor.Undo();
            Assert.Equal(Point.Empty, node.Location);
            Assert.True(editor.IsModified);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ReadOnlyImportsRejectBeforeDecodingAndRemainAvailableAfterUnlock(int importKind)
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true };
            editor.LoadAssembly(typeof(STNodeHub).Assembly);
            var node = new STNodeHub { Location = Point.Empty };
            node.Create();
            editor.Nodes.Add(node);
            node.SetSelected(true, false);
            editor.ClearHistory();
            byte[] data = importKind == 2 ? editor.GetCanvasData() : editor.GetSelectedNodesData();
            byte[] malformed = [0x13, 0x37, 0xFF, 0xFF];
            IReadOnlyList<STNode> Import(byte[] bytes) => importKind switch
            {
                0 => editor.ImportSelectionData(bytes),
                1 => editor.ImportSelectionData(bytes, new Point(400, 200)),
                _ => editor.ImportCanvasAsModule(bytes, new Point(400, 200)),
            };
            editor.EnableEdit = false;

            // Malformed data must not even reach the decoder while editing is unavailable.
            Assert.Empty(Import(malformed));
            Assert.Empty(Import(data));
            Assert.Same(node, Assert.Single(editor.Nodes.Cast<STNode>()));
            Assert.Same(node, Assert.Single(editor.GetSelectedNode()));
            Assert.Equal(Point.Empty, node.Location);
            Assert.Empty(editor.UndoHistory);
            Assert.Empty(editor.RedoHistory);
            Assert.False(editor.IsModified);

            editor.EnableEdit = true;
            Assert.Throws<InvalidDataException>(() => Import(malformed));
            STNode imported = Assert.Single(Import(data));
            Assert.NotSame(node, imported);
            Assert.NotEqual(node.Guid, imported.Guid);
            Assert.Equal(2, editor.Nodes.Count);
            Assert.True(editor.IsModified);
            editor.Undo();
            Assert.Same(node, Assert.Single(editor.Nodes.Cast<STNode>()));
            Assert.False(editor.IsModified);
        });
    }

    [Fact]
    public void ReadOnlyAllowsSelectionAndViewportChangesWithoutChangingGraphOrHistory()
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true, LimitCanvasToContentBounds = false };
            var first = new STNodeHub { Location = new Point(0, 0) };
            var second = new STNodeHub { Location = new Point(300, 0) };
            first.Create(); second.Create();
            editor.Nodes.AddRange([first, second]);
            editor.ClearHistory();
            editor.EnableEdit = false;

            editor.SelectAllNodes();
            editor.SetActiveNode(second);
            editor.ScaleCanvas(1.25f, 0, 0);
            editor.MoveCanvas(125, 75, bAnimation: false, CanvasMoveArgs.All);

            Assert.True(editor.GetSelectedNode().ToHashSet().SetEquals([first, second]));
            Assert.Same(second, editor.ActiveNode);
            Assert.Equal(1.25f, editor.CanvasScale);
            Assert.Equal(125f, editor.CanvasOffsetX);
            Assert.Equal(75f, editor.CanvasOffsetY);
            Assert.Equal(new Point(0, 0), first.Location);
            Assert.Equal(new Point(300, 0), second.Location);
            Assert.Empty(editor.UndoHistory);
            Assert.Empty(editor.RedoHistory);
            Assert.False(editor.IsModified);
            Assert.False(editor.EnableEdit);
        });
    }
}

using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Themes;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System.Drawing;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Point = System.Drawing.Point;

namespace ColorVision.UI.Tests;

public class STNodeGroupInteractionTests
{
    [Fact]
    public void CanvasBoundsIncludeCurrentGroupAndMarkExtents()
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new GroupEditor();
            var nodes = CreateNodes(editor);
            nodes[0].Mark = "Camera setup";
            nodes[2].SetRepeatRegion("second-region", 3, 1);
            nodes[2].Location = new Point(-400, -250);
            AssertBounds();

            nodes[0].Location = new Point(-500, -340);
            AssertBounds();

            nodes[2].SetRepeatRegion(string.Empty, 1, 0);
            nodes[2].Location = new Point(900, 210);
            AssertBounds();

            void AssertBounds()
            {
                Rectangle[] rectangles = nodes.Select(node => node.Rectangle)
                    .Concat(editor.GetNodeGroups().Select(group => group.Bounds)).ToArray();
                Assert.Equal(Rectangle.FromLTRB(rectangles.Min(rectangle => rectangle.Left) - 60,
                    rectangles.Min(rectangle => rectangle.Top) - 60, rectangles.Max(rectangle => rectangle.Right) + 60,
                    rectangles.Max(rectangle => rectangle.Bottom) + 60), editor.CanvasValidBounds);
            }
        });
    }

    [Fact]
    public void GroupHeaderKeepsNodeCommentsAndExternalPortsAccessible()
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new GroupEditor { EnableHistory = true };
            var nodes = CreateNodes(editor);
            editor.ClearHistory();
            nodes[0].Mark = "Camera setup";
            Assert.True(editor.IsModified);
            editor.Undo();
            Assert.Null(nodes[0].Mark);
            Assert.False(editor.IsModified);
            editor.Redo();
            STNodeGroup group = Assert.Single(editor.GetNodeGroups());
            Assert.True(group.HeaderBounds.Bottom < nodes[0].MarkRectangle.Top);
            var markPoint = new Point(nodes[0].MarkRectangle.Left + 10, nodes[0].MarkRectangle.Top + 10);
            Assert.Equal(nodes[0].Mark, editor.FindNodeFromPoint(markPoint).Mark);
            Assert.False(editor.PressAt(markPoint));
            Assert.False(editor.HoversGroupAt(markPoint));

            var outsider = new FlowMenuTestNode();
            outsider.Create();
            editor.Nodes.Add(outsider);
            outsider.Left = group.HeaderBounds.Left - outsider.Width + 4;
            STNodeOption port = outsider.GetAllOutputOptions().Single();
            outsider.Top += group.HeaderBounds.Top + 8 - port.DotTop;
            var portPoint = new Point(outsider.Right + 2, port.DotTop + 3);
            Assert.True(group.HeaderBounds.Contains(portPoint));
            Assert.Null(editor.FindNodeFromPoint(portPoint).Node);
            Assert.Same(port, editor.FindNodeFromPoint(portPoint).NodeOption);
            Assert.False(editor.PressAt(portPoint));
            Assert.False(editor.HoversGroupAt(portPoint));

            editor.Measure(new System.Windows.Size(800, 360));
            editor.Arrange(new Rect(0, 0, 800, 360));
            editor.FitCanvasToNodes();
            Rectangle visibleGroup = editor.CanvasToControl(group.Bounds);
            Assert.True(visibleGroup.Top >= 0 && visibleGroup.Left >= 0);
            Assert.True(visibleGroup.Bottom <= editor.ActualHeight && visibleGroup.Right <= editor.ActualWidth);
        });
    }

    [Fact]
    public void GroupHeaderHoverUsesCurrentLocationsAndMembership()
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new GroupEditor();
            var nodes = CreateNodes(editor);
            Rectangle header = Assert.Single(editor.GetNodeGroups()).HeaderBounds;
            var originalPoint = new Point(header.Left + 10, header.Top + 10);
            Assert.True(editor.HoversGroupAt(originalPoint));

            nodes[0].Top -= 80;
            var movedPoint = new Point(originalPoint.X, originalPoint.Y - 80);
            Assert.True(editor.HoversGroupAt(movedPoint));
            Assert.False(editor.HoversGroupAt(originalPoint));

            nodes[0].SetRepeatRegion(string.Empty, 1, 0);
            Assert.False(editor.HoversGroupAt(movedPoint));
        });
    }

    [Fact]
    public void HeaderDragMovesOnlyItsMembersAndUndoRestoresWholeGroup()
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new GroupEditor { EnableHistory = true, ShowMagnet = false };
            var nodes = CreateNodes(editor);
            nodes[2].SetSelected(true, false);
            editor.ScaleCanvas(0.5f, 0, 0);
            editor.ClearHistory();
            Point[] initial = nodes.Select(node => node.Location).ToArray();
            Assert.True(editor.PressHeader());
            Assert.Equal(nodes.Take(2), editor.GetSelectedNode().OrderBy(node => node.Left));
            editor.DragBy(20, 10);
            editor.DragBy(40, 20);
            // Losing capture must close the entire drag's history transaction.
            editor.FinishDrag();
            Assert.Equal(new Point(initial[0].X + 80, initial[0].Y + 40), nodes[0].Location);
            Assert.Equal(new Point(initial[1].X + 80, initial[1].Y + 40), nodes[1].Location);
            Assert.Equal(initial[2], nodes[2].Location);
            editor.Undo();
            Assert.Equal(initial, nodes.Select(node => node.Location));
            Assert.False(editor.CanUndo);
            editor.Redo();
            Assert.Equal(new Point(initial[1].X + 80, initial[1].Y + 40), nodes[1].Location);
            Assert.Single(editor.GetNodeGroups());
        });
    }

    [Fact]
    public void HeaderDragSnapsToFirstCenterGuideWithinFiveUnits()
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new GroupEditor();
            var nodes = CreateNodes(editor);
            Point first = nodes[0].Location;
            Point second = nodes[1].Location;
            Point delta = new(-560, -300);
            Point unsnapped = new(first.X + delta.X, first.Y + delta.Y);
            Point center = new(unsnapped.X + nodes[0].Width / 2, unsnapped.Y + nodes[0].Height / 2);
            // The first outsider matches the moving edges, but the moving center takes priority.
            nodes[2].Location = new Point(unsnapped.X - nodes[2].Width, unsnapped.Y - nodes[2].Height);
            AddGuide(new Point(center.X - 6, center.Y + 6));
            AddGuide(new Point(center.X - 5, center.Y + 5));
            AddGuide(center);
            AddGuide(new Point(center.X - 5, center.Y + 5));

            Assert.True(editor.PressHeader());
            editor.DragBy(delta.X, delta.Y);
            editor.FinishDrag();

            Assert.Equal(new Point(unsnapped.X - 5, unsnapped.Y + 5), nodes[0].Location);
            Assert.Equal(new Point(second.X + delta.X - 5, second.Y + delta.Y + 5), nodes[1].Location);

            void AddGuide(Point location)
            {
                var guide = new FlowMenuTestNode();
                guide.Create();
                guide.Location = location;
                editor.Nodes.Add(guide);
            }
        });
    }

    [Fact]
    public void LockedMemberPreventsGroupFromSplittingAndReadOnlyPreventsEditing()
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new GroupEditor { EnableHistory = true, ShowMagnet = false };
            var nodes = CreateNodes(editor);
            Point[] initial = nodes.Select(node => node.Location).ToArray();
            nodes[1].LockLocation = true;
            editor.ClearHistory();
            Assert.True(editor.PressHeader());
            editor.DragBy(60, 30);
            editor.FinishDrag();
            Assert.Equal(initial, nodes.Select(node => node.Location));
            Assert.False(editor.CanUndo);
            nodes[1].LockLocation = false;
            editor.ClearHistory();
            int doubleClicks = 0;
            editor.NodeGroupDoubleClick += (_, _) => doubleClicks++;
            editor.EnableEdit = false;
            Assert.True(editor.PressHeader(2));
            editor.DragBy(60, 30);
            editor.FinishDrag();
            Assert.Equal(0, doubleClicks);
            Assert.Equal(initial, nodes.Select(node => node.Location));
            Assert.False(editor.CanUndo);
        });
    }

    [Fact]
    public void DoubleClickUsesExistingCountDialogAndCountEditIsOneUndoStep()
    {
        WpfTestHost.Invoke(() =>
        {
            foreach (var uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) });
            using var editor = new GroupEditor { EnableHistory = true };
            var nodes = CreateNodes(editor);
            using var menu = new FlowNodeContextMenuService(editor, new FlowExecutionNavigator(editor));
            editor.ClearHistory();
            var window = new Window { Content = editor, Width = 800, Height = 300, Left = -16000, Top = -16000, ShowActivated = false, ShowInTaskbar = false };
            DispatcherOperation? editOperation = null;
            try
            {
                window.Show();
                bool edited = false;
                editOperation = editor.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    var dialog = Application.Current.Windows.OfType<FlowRepeatCountWindow>().Single();
                    try
                    {
                        var panel = (StackPanel)dialog.FindName("CountPanel");
                        panel.Children.OfType<DockPanel>().Single().Children.OfType<TextBox>().Single().Text = "7";
                        ((Button)dialog.FindName("AcceptButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        edited = dialog.DialogResult == true;
                    }
                    finally { dialog.Close(); }
                }));
                Assert.True(editor.PressHeader(2));
                Assert.True(edited);
                Assert.All(nodes.Take(2), node => Assert.Equal(7, node.RepeatCount));
                editor.Undo();
                Assert.All(nodes.Take(2), node => Assert.Equal(3, node.RepeatCount));
                Assert.False(editor.CanUndo);
            }
            finally { editOperation?.Abort(); window.Close(); }
        });
    }

    private static CVCommonNode[] CreateNodes(STNodeEditor editor)
    {
        var nodes = Enumerable.Range(0, 3).Select(index => new CVCommonNode("Node", "Test", "Node" + index) { Left = 60 + index * 240, Top = 100 }).ToArray();
        foreach (CVCommonNode node in nodes) { node.Create(); editor.Nodes.Add(node); }
        string id = Guid.NewGuid().ToString();
        foreach (CVCommonNode node in nodes.Take(2)) node.SetRepeatRegion(id, 3, 2);
        return nodes;
    }

    private sealed class GroupEditor : STNodeEditor
    {
        private CanvasAction CurrentAction
        {
            get => (CanvasAction)typeof(STNodeEditor).GetField("m_ca", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this)!;
            set => typeof(STNodeEditor).GetField("m_ca", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, value);
        }

        public bool PressHeader(int clicks = 1)
        {
            Rectangle header = GetNodeGroups().Single().HeaderBounds;
            return PressAt(new Point(header.Left + 10, header.Top + 10), clicks);
        }

        public bool PressAt(Point point, int clicks = 1)
        {
            m_pt_down_in_canvas = point;
            m_pt_down_in_control = CanvasToControl(Point.Round(m_pt_down_in_canvas));
            CurrentAction = CanvasAction.None;
            return (bool)typeof(STNodeEditor).GetMethod("TryBeginNodeGroupInteraction", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this,
                [new STNodeMouseEventArgs(STMouseButtons.Left, clicks, m_pt_down_in_control.X, m_pt_down_in_control.Y, 0), FindNodeFromPoint(point)])!;
        }

        public bool HoversGroupAt(Point point)
        {
            LimitCanvasToContentBounds = false;
            var args = new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount);
            System.Windows.Point pointer = args.GetPosition(this);
            MoveCanvas((int)pointer.X - point.X * CanvasScale, (int)pointer.Y - point.Y * CanvasScale, false, CanvasMoveArgs.All);
            OnMouseMove(args);
            return Cursor == Cursors.SizeAll;
        }

        public void DragBy(int x, int y)
        {
            if (CurrentAction == CanvasAction.MoveNode && EnableEdit)
                typeof(STNodeEditor).GetMethod("MoveNode", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this,
                    [new Point(m_pt_down_in_control.X + x, m_pt_down_in_control.Y + y)]);
        }

        public void FinishDrag() => OnLostMouseCapture(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount));
    }
}

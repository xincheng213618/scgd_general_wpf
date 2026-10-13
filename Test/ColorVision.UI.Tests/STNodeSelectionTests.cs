using ColorVision.Engine.FlowProcessing.Editor;
using ST.Library.UI.NodeEditor;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

public sealed class STNodeSelectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectionNotificationsObserveUpdatedCollectionExactlyOnce(bool useNodeMethod)
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true };
            var node = new SelectionNode();
            node.Create();
            editor.Nodes.Add(node);
            editor.ClearHistory();
            var nodeNotifications = new List<bool>();
            var editorNotifications = new List<bool>();
            void AssertSelectionIsConsistent()
                => Assert.Equal(node.IsSelected, editor.GetSelectedNode().Contains(node));
            node.SelectionChanged = () =>
            {
                AssertSelectionIsConsistent();
                nodeNotifications.Add(node.IsSelected);
            };
            editor.SelectedChanged += (_, _) =>
            {
                AssertSelectionIsConsistent();
                editorNotifications.Add(node.IsSelected);
            };

            if (useNodeMethod)
            {
                node.SetSelected(true, false);
                node.SetSelected(true, false);
                node.SetSelected(false, false);
                node.SetSelected(false, false);
            }
            else
            {
                Assert.True(editor.AddSelectedNode(node));
                Assert.False(editor.AddSelectedNode(node));
                Assert.True(editor.RemoveSelectedNode(node));
                Assert.False(editor.RemoveSelectedNode(node));
            }

            Assert.Equal(new[] { true, false }, nodeNotifications);
            Assert.Equal(new[] { true, false }, editorNotifications);
            Assert.Empty(editor.GetSelectedNode());
            Assert.False(editor.IsModified);
            Assert.Empty(editor.UndoHistory);
            Assert.Empty(editor.RedoHistory);
        });
    }

    [Fact]
    public void RemovingSelectedNodeClearsCollectionBeforeItsSelectionNotification()
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new STNodeEditor();
            var node = new SelectionNode();
            node.Create();
            editor.Nodes.Add(node);
            editor.AddSelectedNode(node);
            int notifications = 0;
            node.SelectionChanged = () =>
            {
                notifications++;
                Assert.False(node.IsSelected);
                Assert.DoesNotContain(node, editor.GetSelectedNode());
            };

            editor.Nodes.Remove(node);

            Assert.Equal(1, notifications);
            Assert.Empty(editor.GetSelectedNode());
        });
    }

    [Fact]
    public void SelectAllHidesSingleNodeInspectorAndDeselectingAnotherNodeRestoresIt()
    {
        WpfTestHost.Invoke(() =>
        {
            var theme = new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(theme);
            try
            {
                using var canvas = new FlowEditorCanvas();
                canvas.Measure(new Size(1000, 600));
                canvas.Arrange(new Rect(0, 0, 1000, 600));
                canvas.UpdateLayout();
                STNodeEditor editor = canvas.NodeEditor;
                var first = new STNodeHub { Title = "First" };
                var second = new STNodeHub { Title = "Second", Left = 300 };
                first.Create(); second.Create();
                editor.Nodes.AddRange([first, second]);
                editor.ClearHistory();
                editor.SetActiveNode(first);
                Assert.Equal(Visibility.Visible, canvas.NodePropertyPanelContainer.Visibility);

                // Ctrl+A uses this routed command; no explicit inspector refresh may mask a stale selection event.
                ApplicationCommands.SelectAll.Execute(null, editor);

                Assert.Equal(2, editor.GetSelectedNode().Length);
                Assert.Same(first, editor.ActiveNode);
                Assert.Equal(Visibility.Collapsed, canvas.NodePropertyPanelContainer.Visibility);

                editor.RemoveSelectedNode(second);

                Assert.Same(first, Assert.Single(editor.GetSelectedNode()));
                Assert.Equal(Visibility.Visible, canvas.NodePropertyPanelContainer.Visibility);
                Assert.False(editor.IsModified);
                Assert.Empty(editor.UndoHistory);
                Assert.Empty(editor.RedoHistory);
            }
            finally
            {
                Application.Current.Resources.MergedDictionaries.Remove(theme);
            }
        });
    }

    private sealed class SelectionNode : STNodeHub
    {
        public Action? SelectionChanged { get; set; }
        protected override void OnSelectedChanged() => SelectionChanged?.Invoke();
    }
}

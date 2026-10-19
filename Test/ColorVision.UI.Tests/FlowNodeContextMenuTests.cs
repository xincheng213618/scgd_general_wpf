using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Nodes;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class FlowNodeContextMenuTests
{
    [Fact]
    public void LocalAlgorithmOrderMatchesInContextMenuAndNodeCatalog()
    {
        WpfTestHost.Invoke(() =>
        {
            Type[] expected = [typeof(LocalFindLuminousAreaNode), typeof(LocalFovNode), typeof(LocalGridDistortionNode), typeof(LocalRgbCrossNode)];
            using var editor = new STNodeEditor();
            using var menus = new FlowNodeContextMenuService(editor, new FlowExecutionNavigator(editor), () => { });
            var creationMenu = new ContextMenu();
            Invoke(menus, "AddNodeCreationMenuItems", creationMenu.Items);
            string algorithmHeader = FlowNodeContextMenuService.LocalizeNodeMenuPath("Flow_LocalAlgorithms");
            string deviceHeader = FlowNodeContextMenuService.LocalizeNodeMenuPath("Flow_LocalDevices");
            MenuItem algorithms = creationMenu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, algorithmHeader));
            Assert.Single(creationMenu.Items.OfType<MenuItem>().Where(item => Equals(item.Header, deviceHeader)));
            Assert.DoesNotContain(creationMenu.Items.OfType<MenuItem>(), item => Equals(item.Header, FlowNodeContextMenuService.LocalizeNodeMenuPath("Flow_CustomNodes")));
            MenuItem[] firstAlgorithms = algorithms.Items.OfType<MenuItem>().Take(expected.Length).ToArray();
            Assert.Equal(expected.Length, firstAlgorithms.Length);
            for (int index = 0; index < expected.Length; index++)
            {
                firstAlgorithms[index].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Equal(expected[index], editor.Nodes.Cast<STNode>().Last().GetType());
            }

            using var catalog = new STNodeTreeView();
            Assert.Equal("ColorVision.Engine/Flow_LocalAlgorithms", catalog.NodeTypes[typeof(LocalFovNode)]);
            Assert.Equal("ColorVision.Engine/Flow_LocalDevices", catalog.NodeTypes[typeof(LocalImageNode)]);
            var tree = (TreeView)typeof(STNodeTreeView).GetField("m_tree", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(catalog)!;
            TreeViewItem algorithmFolder = DescendantTreeItems(tree.Items)
                .Single(item => item.Items.OfType<TreeViewItem>().Any(child => Equals(child.Tag, typeof(LocalFovNode))));
            Assert.Equal(expected, algorithmFolder.Items.OfType<TreeViewItem>().Take(expected.Length).Select(item => (Type)item.Tag));
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CopyAndDeleteTargetSelectionOnlyWhenClickedNodeIsSelected(bool clickedSelection)
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true };
            var nodes = Enumerable.Range(0, 3).Select(index => new FlowMenuTestNode { Left = index * 220 }).ToArray();
            foreach (FlowMenuTestNode node in nodes) { node.Create(); editor.Nodes.Add(node); }
            nodes[0].GetAllOutputOptions().Single().ConnectOption(nodes[1].GetAllInputOptions().Single());
            nodes[0].SetSelected(true, false);
            nodes[1].SetSelected(true, false);
            editor.ClearHistory();
            using var menus = new FlowNodeContextMenuService(editor, new FlowExecutionNavigator(editor), () => { });
            MenuItem[] edits = CreateEditItems(menus, nodes[clickedSelection ? 0 : 2], out _);

            edits[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            STNode[] copies = editor.Nodes.Cast<STNode>().Except(nodes).OrderBy(node => node.Left).ToArray();
            Assert.Equal(clickedSelection ? 2 : 1, copies.Length);
            Assert.Equal(clickedSelection ? new[] { 30, 250 } : new[] { 470 }, copies.Select(node => node.Left));
            Assert.Equal(clickedSelection ? 2 : 1, editor.GetConnectionInfo().Length);
            editor.Undo();
            Assert.Equal(nodes, editor.Nodes.Cast<STNode>());
            Assert.Single(editor.GetConnectionInfo());
            Assert.False(editor.CanUndo);

            edits[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(clickedSelection ? new[] { nodes[2] } : nodes.Take(2), editor.Nodes.Cast<STNode>());
            editor.Undo();
            Assert.Equal(nodes, editor.Nodes.Cast<STNode>());
            Assert.Single(editor.GetConnectionInfo());
            Assert.False(editor.CanUndo);
            Assert.False(editor.IsModified);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EditMenusCannotChangeDisabledEditorIncludingAlreadyOpenedMenus(bool disabledBeforeOpening)
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true };
            var node = new FlowMenuTestNode();
            node.Create();
            editor.Nodes.Add(node);
            editor.ClearHistory();
            editor.EnableEdit = !disabledBeforeOpening;
            int imports = 0;
            using var menus = new FlowNodeContextMenuService(editor, new FlowExecutionNavigator(editor), () => imports++);

            MenuItem[] edits = CreateEditItems(menus, node, out MenuItem history);
            Assert.True(history.IsEnabled);
            Assert.All(edits, item => Assert.Equal(!disabledBeforeOpening, item.IsEnabled));
            editor.EnableEdit = false;

            foreach (MenuItem item in edits)
            {
                // Raise the callback directly as well: a menu may have opened before editing was disabled.
                item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Same(node, Assert.Single(editor.Nodes.Cast<STNode>()));
                Assert.False(node.LockOption);
                Assert.False(node.LockLocation);
                Assert.False(editor.CanUndo);
                Assert.False(editor.IsModified);
                Assert.Equal(0, imports);
            }

            Assert.True(history.IsEnabled);
        });
    }

    [Fact]
    public void EnabledEditMenusStillCreateCopyDeleteAndUndo()
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true };
            var node = new FlowMenuTestNode();
            node.Create();
            editor.Nodes.Add(node);
            editor.ClearHistory();
            int imports = 0;
            using var menus = new FlowNodeContextMenuService(editor, new FlowExecutionNavigator(editor), () => imports++);

            MenuItem[] edits = CreateEditItems(menus, node, out _);
            edits[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(2, editor.Nodes.Count);
            editor.Undo();
            Assert.Same(node, Assert.Single(editor.Nodes.Cast<STNode>()));

            edits[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Empty(editor.Nodes.Cast<STNode>());
            editor.Undo();
            Assert.Same(node, Assert.Single(editor.Nodes.Cast<STNode>()));

            edits[2].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.True(node.LockOption);
            editor.Undo();
            Assert.False(node.LockOption);

            edits[3].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.True(node.LockLocation);
            editor.Undo();
            Assert.False(node.LockLocation);

            edits[4].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(2, editor.Nodes.Count);
            Assert.All(editor.Nodes.Cast<STNode>(), item => Assert.IsType<FlowMenuTestNode>(item));
            editor.Undo();
            Assert.Same(node, Assert.Single(editor.Nodes.Cast<STNode>()));

            edits[5].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(1, imports);
        });
    }

    private static MenuItem[] CreateEditItems(FlowNodeContextMenuService menus, STNode node, out MenuItem history)
    {
        var nodeMenu = new ContextMenu();
        Invoke(menus, "AddNodeMenuItems", nodeMenu.Items, node);
        MenuItem historyItem = nodeMenu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, ColorVision.Engine.Properties.Resources.Flow_NodeExecutionDetails));
        history = historyItem;
        MenuItem[] nodeEdits = nodeMenu.Items.OfType<MenuItem>()
            .Where(item => !ReferenceEquals(item, historyItem) && !Equals(item.Header, FlowEngineLib.Repeat.FlowRepeatRegion.Text("重复执行…")))
            .ToArray();
        Assert.Equal(4, nodeEdits.Length);

        var creationMenu = new ContextMenu();
        Invoke(menus, "AddNodeCreationMenuItems", creationMenu.Items);
        MenuItem create = Descendants(creationMenu.Items).Single(item => Equals(item.Header, FlowMenuTestNode.MenuTitle));

        var importMenu = new ContextMenu();
        Invoke(menus, "AddImportModuleContextMenu", importMenu.Items);
        return [.. nodeEdits, create, Assert.Single(importMenu.Items.OfType<MenuItem>())];
    }

    private static IEnumerable<MenuItem> Descendants(ItemCollection items)
    {
        foreach (MenuItem item in items.OfType<MenuItem>())
        {
            yield return item;
            foreach (MenuItem descendant in Descendants(item.Items))
                yield return descendant;
        }
    }

    private static IEnumerable<TreeViewItem> DescendantTreeItems(ItemCollection items)
    {
        foreach (TreeViewItem item in items.OfType<TreeViewItem>())
        {
            yield return item;
            foreach (TreeViewItem descendant in DescendantTreeItems(item.Items))
                yield return descendant;
        }
    }

    private static void Invoke(FlowNodeContextMenuService menus, string name, params object[] args)
        => typeof(FlowNodeContextMenuService).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(menus, args);
}

[STNode("Editor menu test")]
public sealed class FlowMenuTestNode : CVCommonNode
{
    public const string MenuTitle = "Context menu test node";

    public FlowMenuTestNode() : base(MenuTitle, nameof(FlowMenuTestNode), MenuTitle) { }

    protected override void OnCreate()
    {
        base.OnCreate();
        InputOptions.Add("IN", typeof(string), true);
        OutputOptions.Add("OUT", typeof(string), false);
    }
}

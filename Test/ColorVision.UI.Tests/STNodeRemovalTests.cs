using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public sealed class STNodeRemovalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClearNotifiesAfterEachNodeAndItsConnectionsLeaveEitherHost(bool hosted)
    {
        StaTest.Run(() =>
        {
            using var editor = hosted ? new STNodeEditor() : null;
            using var container = new CVNodeContainer();
            System.Collections.IList collection = hosted ? editor!.Nodes : container.Nodes;
            var nodes = Enumerable.Range(0, 3).Select(_ => new RemovalTestNode()).ToArray();
            foreach (RemovalTestNode node in nodes) { node.Create(); collection.Add(node); }
            Assert.Equal(ConnectionStatus.Connected, nodes[0].Output.ConnectOption(nodes[1].Input, hosted));
            Assert.Equal(ConnectionStatus.Connected, nodes[1].Output.ConnectOption(nodes[2].Input, hosted));
            foreach (RemovalTestNode node in nodes) node.LockOption = true;
            if (hosted) editor!.OptionDisConnecting += (_, args) => args.Continue = false;
            var removed = new HashSet<STNode>();
            void ObserveRemoval(object sender, STNodeEditorEventArgs args)
            {
                Assert.True(removed.Add(args.Node));
                Assert.False(collection.Contains(args.Node));
                Assert.Equal(-1, collection.IndexOf(args.Node));
                Assert.Equal(nodes.Length - removed.Count, collection.Count);
                Assert.Null(args.Node.Owner);
                Assert.All(args.Node.GetAllInputOptions().Concat(args.Node.GetAllOutputOptions()), port => Assert.Empty(port.ConnectedOption));
                STNode[] remaining = collection.Cast<STNode>().ToArray();
                Assert.DoesNotContain(args.Node, remaining);
                foreach (STNode node in remaining)
                    foreach (STNodeOption port in node.GetAllInputOptions().Concat(node.GetAllOutputOptions()))
                        Assert.All(port.ConnectedOption, peer => Assert.Contains(peer.Owner, remaining));
            }
            if (hosted) editor!.NodeRemoved += ObserveRemoval;
            else container.NodeRemoved += ObserveRemoval;

            collection.Clear();

            Assert.Equal(nodes.Length, removed.Count);
            Assert.Empty(collection.Cast<STNode>());
            Assert.All(nodes, node =>
            {
                Assert.True(node.LockOption);
                Assert.Empty(node.Input.ConnectedOption);
                Assert.Empty(node.Output.ConnectedOption);
            });
        });
    }

    [Fact]
    public void NodeRemovalCommitsGraphBeforeNotificationAndUndoRestoresLockedConnections()
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true };
            var source = new RemovalTestNode();
            var middle = new RemovalTestNode();
            var target = new RemovalTestNode();
            foreach (RemovalTestNode node in new[] { source, middle, target }) { node.Create(); editor.Nodes.Add(node); }
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(middle.Input));
            Assert.Equal(ConnectionStatus.Connected, middle.Output.ConnectOption(target.Input));
            editor.OptionDisConnecting += (_, args) => args.Continue = false;
            Assert.Equal(ConnectionStatus.Reject, source.Output.DisConnectOption(middle.Input));
            foreach (RemovalTestNode node in new[] { source, middle, target }) node.LockOption = true;
            editor.SetActiveNode(middle);
            STNode[] originalOrder = editor.Nodes.Cast<STNode>().ToArray();
            editor.ClearHistory();
            int removed = 0;
            int disconnected = 0;
            editor.OptionDisConnected += (_, args) =>
            {
                if (args.Status != ConnectionStatus.DisConnected) return;
                disconnected++;
                Assert.DoesNotContain(args.TargetOption, args.CurrentOption.ConnectedOption);
                Assert.DoesNotContain(args.CurrentOption, args.TargetOption.ConnectedOption);
            };
            editor.NodeRemoved += (_, args) =>
            {
                if (!ReferenceEquals(args.Node, middle)) return;
                removed++;
                Assert.Null(middle.Owner);
                Assert.False(editor.Nodes.Contains(middle));
                Assert.Equal(-1, editor.Nodes.IndexOf(middle));
                Assert.DoesNotContain(middle, editor.Nodes.Cast<STNode>());
                Assert.DoesNotContain(middle, editor.GetSelectedNode());
                Assert.NotSame(middle, editor.ActiveNode);
                Assert.Equal(2, editor.Nodes.Count);
                Assert.Empty(editor.GetConnections());
                Assert.Empty(source.Output.ConnectedOption);
                Assert.Empty(middle.Input.ConnectedOption);
                Assert.Empty(middle.Output.ConnectedOption);
                Assert.Empty(target.Input.ConnectedOption);
            };

            // Ordinary disconnect can be vetoed; removing a node must still release its graph edges.
            editor.Nodes.Remove(middle);
            Assert.Equal(1, removed);
            Assert.Equal(2, disconnected);
            Assert.True(editor.CanUndo);

            editor.Undo();
            Assert.Equal(originalOrder, editor.Nodes.Cast<STNode>());
            Assert.Same(editor, middle.Owner);
            Assert.All(new[] { source, middle, target }, node => Assert.True(node.LockOption));
            Assert.Equal(2, editor.GetConnections().Length);
            Assert.Same(middle.Input, Assert.Single(source.Output.ConnectedOption));
            Assert.Same(source.Output, Assert.Single(middle.Input.ConnectedOption));
            Assert.Same(target.Input, Assert.Single(middle.Output.ConnectedOption));
            Assert.Same(middle.Output, Assert.Single(target.Input.ConnectedOption));
            Assert.False(editor.CanUndo);

            editor.Redo();
            Assert.Equal(2, removed);
            Assert.Equal(4, disconnected);
            Assert.True(source.LockOption);
            Assert.True(middle.LockOption);
            Assert.True(target.LockOption);
            Assert.Empty(editor.GetConnections());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovingAndClearingPortsDetachEveryCounterpartWithOrWithoutEditor(bool hosted)
    {
        StaTest.Run(() =>
        {
            using var editor = hosted ? new STNodeEditor() : null;
            using var container = new CVNodeContainer();
            var source = new RemovalTestNode();
            var middle = new RemovalTestNode();
            var target = new RemovalTestNode();
            foreach (RemovalTestNode node in new[] { source, middle, target })
            {
                node.Create();
                if (hosted) editor!.Nodes.Add(node);
                else container.Nodes.Add(node);
            }
            STNodeOption extraOutput = middle.AddOutput();
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(middle.Input, hosted));
            Assert.Equal(ConnectionStatus.Connected, middle.Output.ConnectOption(target.Input, hosted));
            Assert.Equal(ConnectionStatus.Connected, extraOutput.ConnectOption(target.Input, hosted));
            foreach (RemovalTestNode node in new[] { source, middle, target }) node.LockOption = true;
            int disconnected = 0;
            if (hosted)
            {
                editor!.OptionDisConnecting += (_, args) => args.Continue = false;
                editor.OptionDisConnected += (_, args) =>
                {
                    Assert.Equal(ConnectionStatus.DisConnected, args.Status);
                    disconnected++;
                    Assert.DoesNotContain(args.TargetOption, args.CurrentOption.ConnectedOption);
                    Assert.DoesNotContain(args.CurrentOption, args.TargetOption.ConnectedOption);
                };
            }

            middle.RemoveFirstInput();
            Assert.Empty(middle.GetAllInputOptions());
            Assert.Null(middle.Input.Owner);
            Assert.Empty(middle.Input.ConnectedOption);
            Assert.Empty(source.Output.ConnectedOption);
            Assert.Equal(2, target.Input.ConnectionCount);

            middle.ClearOutputs();
            Assert.Empty(middle.GetAllOutputOptions());
            Assert.Null(middle.Output.Owner);
            Assert.Null(extraOutput.Owner);
            Assert.Empty(middle.Output.ConnectedOption);
            Assert.Empty(extraOutput.ConnectedOption);
            Assert.Empty(target.Input.ConnectedOption);
            if (hosted)
            {
                Assert.Empty(editor!.GetConnections());
                Assert.Equal(3, disconnected);
            }
        });
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ManualDisconnectRequiresBothEndpointsUnlocked(bool sourceLocked, bool targetLocked)
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true };
            var source = new RemovalTestNode();
            var target = new RemovalTestNode();
            source.Create(); target.Create();
            editor.Nodes.AddRange([source, target]);
            source.Output.Data = "existing result";
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(target.Input));
            source.LockOption = sourceLocked;
            target.LockOption = targetLocked;
            editor.ClearHistory();
            int transfers = 0;
            target.Input.DataTransfer += (_, _) => transfers++;

            Assert.Equal(ConnectionStatus.Locked, source.Output.DisConnectOption(target.Input));
            Assert.Equal(ConnectionStatus.Locked, target.Input.DisConnectOption(source.Output));
            Assert.Same(target.Input, Assert.Single(source.Output.ConnectedOption));
            Assert.Same(source.Output, Assert.Single(target.Input.ConnectedOption));
            Assert.Equal("existing result", source.Output.Data);
            Assert.Equal(0, transfers);
            Assert.False(editor.CanUndo);
            Assert.False(editor.IsModified);

            source.LockOption = false;
            target.LockOption = false;
            Assert.Equal(ConnectionStatus.DisConnected, source.Output.DisConnectOption(target.Input));
            Assert.Empty(source.Output.ConnectedOption);
            Assert.Empty(target.Input.ConnectedOption);
            Assert.Equal(1, transfers);
            Assert.True(editor.CanUndo);
        });
    }
}

[STNode("Removal lifecycle test")]
public sealed class RemovalTestNode : STNode
{
    public STNodeOption Input { get; private set; } = null!;
    public STNodeOption Output { get; private set; } = null!;

    protected override void OnCreate()
    {
        base.OnCreate();
        Input = InputOptions.Add("IN", typeof(string), false);
        Output = OutputOptions.Add("OUT", typeof(string), false);
    }

    public STNodeOption AddOutput() => OutputOptions.Add("EXTRA", typeof(string), false);
    public void RemoveFirstInput() => InputOptions.RemoveAt(0);
    public void ClearOutputs() => OutputOptions.Clear();
}

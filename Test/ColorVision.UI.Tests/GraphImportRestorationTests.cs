using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Drawing;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class GraphImportRestorationTests
{
    [Theory]
    [InlineData(0)] // Copy/paste selection.
    [InlineData(1)] // Import a saved canvas as a module.
    [InlineData(2)] // Open a saved canvas in the editor.
    [InlineData(3)] // Load the same canvas without an editor.
    public void HubChainRestoresAfterActivatingItsSourceChangesSerializedConnectionOrder(int importKind)
    {
        WpfTestHost.Invoke(() =>
        {
            using var source = new STNodeEditor();
            source.LoadAssembly(typeof(GraphImportStringNode).Assembly);
            var start = new GraphImportStringNode { Title = "Source" };
            var first = new STNodeHub { Title = "First hub" };
            var second = new STNodeHub { Title = "Second hub" };
            var sink = new GraphImportStringNode { Title = "Sink" };
            STNode[] original = [start, first, second, sink];
            foreach (STNode node in original) node.Create();
            source.Nodes.AddRange(original);
            Assert.Equal(ConnectionStatus.Connected, start.Output.ConnectOption(first.GetAllInputOptions()[0]));
            Assert.Equal(ConnectionStatus.Connected, first.GetAllOutputOptions()[0].ConnectOption(second.GetAllInputOptions()[0]));
            Assert.Equal(ConnectionStatus.Connected, second.GetAllOutputOptions()[0].ConnectOption(sink.Input));
            foreach (STNode node in original) node.LockOption = true;
            first.LockLocation = true;
            second.LockLocation = true;

            // An ordinary click brings the source to the front. Saving then lists the Hub-to-Hub edge first.
            source.SetActiveNode(start);
            Assert.Same(start, source.Nodes[source.Nodes.Count - 1]);
            Assert.Same(first, source.GetConnections()[0].Output.Owner);
            byte[] data = importKind == 0 ? source.GetNodesData(original) : source.GetCanvasData();

            if (importKind == 3)
            {
                var container = new CVNodeContainer();
                container.LoadCanvas(data);
                AssertRestoredChain(container.Nodes.Cast<STNode>().ToArray());
                container.Nodes.Clear();
                return;
            }

            using var target = new STNodeEditor { EnableHistory = true };
            var existing = new GraphImportStringNode { Title = "Existing" };
            existing.Create();
            target.Nodes.Add(existing);
            target.SetActiveNode(existing);
            target.ClearHistory();
            if (importKind == 2)
            {
                target.LoadCanvas(data);
                AssertRestoredChain(target.Nodes.Cast<STNode>().ToArray());
                return;
            }

            STNode[] imported = (importKind == 0
                ? target.ImportSelectionData(data, new Point(400, 200))
                : target.ImportCanvasAsModule(data, new Point(400, 200))).ToArray();
            AssertRestoredChain(imported);
            Assert.Equal(5, target.Nodes.Count);
            Assert.Single(target.UndoHistory);
            Assert.True(target.IsModified);
            target.Undo();
            Assert.Same(existing, Assert.Single(target.Nodes.Cast<STNode>()));
            Assert.Empty(target.GetConnections());
            Assert.False(target.IsModified);
            target.Redo();
            Assert.Equal(5, target.Nodes.Count);
            AssertRestoredChain(imported);
            Assert.Equal(3, target.GetConnections().Length);
            Assert.Single(target.UndoHistory);
        });
    }

    [Theory]
    [InlineData(false)] // An untyped Hub-only component cannot make progress.
    [InlineData(true)] // Typed neighbours resolve to incompatible string/int ports.
    public void InvalidHubModulePreservesExistingGraphSelectionAndHistory(bool incompatibleTypes)
    {
        WpfTestHost.Invoke(() =>
        {
            using var target = new STNodeEditor { EnableHistory = true };
            target.LoadAssembly(typeof(GraphImportStringNode).Assembly);
            var source = new GraphImportStringNode { Title = "Existing source" };
            var sink = new GraphImportStringNode { Title = "Existing sink" };
            source.Create(); sink.Create();
            target.Nodes.AddRange([source, sink]);
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(sink.Input));
            source.LockOption = sink.LockOption = true;
            target.SetActiveNode(source);
            target.ClearHistory();
            target.ExecuteEditTransaction("Move existing source", () => source.Left += 40);
            target.ExecuteEditTransaction("Move existing sink", () => sink.Top += 40);
            target.Undo();
            STNode[] nodesBefore = target.Nodes.Cast<STNode>().ToArray();
            STNode[] selectionBefore = target.GetSelectedNode();
            byte[] canvasBefore = target.GetCanvasData();
            var undoBefore = target.UndoHistory.ToArray();
            var redoBefore = target.RedoHistory.ToArray();

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
                target.ImportCanvasAsModule(CreateInvalidHubCanvas(incompatibleTypes), new Point(400, 200)));

            Assert.Contains(nameof(ConnectionStatus.ErrorType), failure.Message);
            Assert.Equal(nodesBefore, target.Nodes.Cast<STNode>());
            Assert.Equal(selectionBefore, target.GetSelectedNode());
            Assert.Same(source, target.ActiveNode);
            Assert.Equal(canvasBefore, target.GetCanvasData());
            Assert.Equal(undoBefore, target.UndoHistory);
            Assert.Equal(redoBefore, target.RedoHistory);
            Assert.True(target.IsModified);
            Assert.Same(sink.Input, Assert.Single(source.Output.ConnectedOption));
            Assert.True(source.LockOption);
            Assert.True(sink.LockOption);
        });
    }

    private static byte[] CreateInvalidHubCanvas(bool incompatibleTypes)
    {
        var first = new STNodeHub();
        var second = new STNodeHub();
        first.Create(); second.Create();
        var connections = new List<ConnectionInfo>
        {
            new() { Output = first.GetAllOutputOptions()[0], Input = second.GetAllInputOptions()[0] }
        };
        var nodes = new List<STNode> { first, second };
        if (incompatibleTypes)
        {
            var source = new GraphImportStringNode();
            var sink = new GraphImportIntNode();
            source.Create(); sink.Create();
            nodes.Add(source);
            nodes.Add(sink);
            connections.Add(new ConnectionInfo { Output = source.Output, Input = first.GetAllInputOptions()[0] });
            connections.Add(new ConnectionInfo { Output = second.GetAllOutputOptions()[0], Input = sink.Input });
        }
        using var stream = new MemoryStream();
        STNodeCanvasWriter.Write(stream, nodes, connections, 0, 0, 1);
        return stream.ToArray();
    }

    private static void AssertRestoredChain(STNode[] nodes)
    {
        Assert.Equal(4, nodes.Length);
        var start = Assert.IsType<GraphImportStringNode>(nodes.Single(node => node.Title == "Source"));
        var first = Assert.IsType<STNodeHub>(nodes.Single(node => node.Title == "First hub"));
        var second = Assert.IsType<STNodeHub>(nodes.Single(node => node.Title == "Second hub"));
        var sink = Assert.IsType<GraphImportStringNode>(nodes.Single(node => node.Title == "Sink"));
        Assert.Same(first.GetAllInputOptions()[0], Assert.Single(start.Output.ConnectedOption));
        Assert.Same(second.GetAllInputOptions()[0], Assert.Single(first.GetAllOutputOptions()[0].ConnectedOption));
        Assert.Same(sink.Input, Assert.Single(second.GetAllOutputOptions()[0].ConnectedOption));
        Assert.All(nodes, node => Assert.True(node.LockOption));
        Assert.True(first.LockLocation);
        Assert.True(second.LockLocation);
        sink.Received = null;
        start.Output.TransferData("restored result");
        Assert.Equal("restored result", sink.Received);
    }
}

[STNode("Graph import string test")]
public sealed class GraphImportStringNode : STNode
{
    public STNodeOption Input { get; private set; } = null!;
    public STNodeOption Output { get; private set; } = null!;
    public string? Received { get; set; }

    protected override void OnCreate()
    {
        base.OnCreate();
        Input = InputOptions.Add("IN", typeof(string), false);
        Output = OutputOptions.Add("OUT", typeof(string), false);
        Input.DataTransfer += (_, args) => Received = args.Status == ConnectionStatus.Connected ? args.TargetOption.Data as string : null;
    }
}

[STNode("Graph import int test")]
public sealed class GraphImportIntNode : STNode
{
    public STNodeOption Input { get; private set; } = null!;

    protected override void OnCreate()
    {
        base.OnCreate();
        Input = InputOptions.Add("IN", typeof(int), false);
    }
}

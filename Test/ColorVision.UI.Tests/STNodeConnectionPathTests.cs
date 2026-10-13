using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public class STNodeConnectionPathTests
{
    [Fact]
    public void ExistingCycleTerminatesSearchAndStillFindsItsExit()
    {
        var first = new PathNode();
        var second = new PathNode();
        var exit = new PathNode();
        var unrelated = new PathNode();
        first.Output.LinkUnchecked(second.Input);
        second.Output.LinkUnchecked(first.Input);
        second.Output.LinkUnchecked(exit.Input);

        Assert.False(STNodeEditor.CanFindNodePath(first, unrelated));
        Assert.True(STNodeEditor.CanFindNodePath(first, exit));
        Assert.True(STNodeEditor.CanFindNodePath(first, first));
        Assert.False(STNodeEditor.CanFindNodePath(exit, first));
        Assert.False(STNodeEditor.CanFindNodePath(unrelated, unrelated));
    }

    [Fact]
    public void MergingBranchesPreserveDirectedReachability()
    {
        var start = new PathNode();
        var left = new PathNode();
        var right = new PathNode();
        var merge = new PathNode();
        var end = new PathNode();
        start.Output.LinkUnchecked(left.Input);
        start.Output.LinkUnchecked(right.Input);
        left.Output.LinkUnchecked(merge.Input);
        right.Output.LinkUnchecked(merge.Input);
        merge.Output.LinkUnchecked(end.Input);

        Assert.True(STNodeEditor.CanFindNodePath(start, end));
        Assert.True(STNodeEditor.CanFindNodePath(right, end));
        Assert.False(STNodeEditor.CanFindNodePath(left, right));
        Assert.False(STNodeEditor.CanFindNodePath(end, start));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    // Untyped Hub outputs bind their type when the output initiates, as in the editor's drop handler.
    [InlineData(true, false)]
    public void ConnectionChecksRejectNewLoopsButAllowAnEntryIntoAnExistingCycle(bool hubOutput, bool inputInitiates)
    {
        var first = new PathNode();
        var second = new PathNode();
        var source = new PathNode();
        first.Output.LinkUnchecked(second.Input);
        second.Output.LinkUnchecked(first.Input);
        STNodeOption candidate = source.AddOutput(hubOutput);

        Assert.Equal(ConnectionStatus.Connected, candidate.CanConnect(first.Input));
        Assert.Equal(ConnectionStatus.Connected, Connect(candidate, first.Input, inputInitiates));
        Assert.Same(candidate, first.Input.ConnectedOption.Single(port => port.Owner == source));

        STNodeOption closing = second.AddOutput(hubOutput);
        Assert.Equal(ConnectionStatus.Loop, closing.CanConnect(source.Input));
        Assert.Equal(ConnectionStatus.Loop, Connect(closing, source.Input, inputInitiates));
        Assert.Empty(closing.ConnectedOption);
        Assert.Empty(source.Input.ConnectedOption);
    }

    private static ConnectionStatus Connect(STNodeOption output, STNodeOption input, bool inputInitiates)
        => inputInitiates ? input.ConnectOption(output, false) : output.ConnectOption(input, false);

    private sealed class PathNode : STNode
    {
        public PathOption Input { get; } = new();
        public PathOption Output { get; } = new();

        public PathNode()
        {
            InputOptions.Add(Input);
            OutputOptions.Add(Output);
        }

        public STNodeOption AddOutput(bool hub)
        {
            STNodeOption option = hub ? new STNodeHubOption("OUT", typeof(object), false) : new STNodeOption("OUT", typeof(string), false);
            OutputOptions.Add(option);
            return option;
        }
    }

    private sealed class PathOption : STNodeOption
    {
        public PathOption() : base("Path", typeof(string), false) { }

        // Model an existing graph without asking the connection validator to create a new cycle.
        public void LinkUnchecked(PathOption input)
        {
            m_hs_connected.Add(input);
            input.m_hs_connected.Add(this);
        }
    }
}

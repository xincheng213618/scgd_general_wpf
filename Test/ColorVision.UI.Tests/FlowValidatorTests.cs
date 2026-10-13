using ColorVision.Engine.FlowProcessing.Editor;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Logical;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public sealed class FlowValidatorTests
{
    [Fact]
    public void MultipleStartsAndEndsAreValidatedIndependentlyOfSelectionAndDrawingOrder()
    {
        WpfTestHost.Invoke(() =>
        {
            using var graph = new Graph();
            var first = graph.Add(new ValidationStartNode());
            var second = graph.Add(new ValidationStartNode());
            var secondEnd = graph.Add(new CVEndNode());
            var firstEnd = graph.Add(new CVEndNode());
            Connect(first, firstEnd);
            Connect(second, secondEnd);

            foreach (STNode active in new STNode[] { first, firstEnd, secondEnd, second })
            {
                graph.Editor.SetActiveNode(active);
                Assert.True(FlowValidator.TryValidate(graph.Editor, out string error, out STNode? invalid));
                Assert.Empty(error);
                Assert.Null(invalid);
            }
        });
    }

    [Fact]
    public void OneCompleteExecutionPathIsEnoughWhenOtherNodesAndBranchesAreIncomplete()
    {
        WpfTestHost.Invoke(() =>
        {
            using var graph = new Graph();
            graph.Add(new ValidationStartNode());
            var start = graph.Add(new ValidationStartNode());
            graph.Add(new CVEndNode());
            var end = graph.Add(new CVEndNode());
            var branch = graph.Add(new ConditionBranchNode());
            var unfinished = graph.Add(new FlowRerouteNode());
            Connect(start, branch);
            branch.GetAllOutputOptions()[0].ConnectOption(end.m_in_start);
            branch.GetAllOutputOptions()[1].ConnectOption(unfinished.GetAllInputOptions().Single());

            Assert.True(FlowValidator.TryValidate(graph.Editor, out _, out _));
        });
    }

    [Theory]
    [InlineData(0, true, "Flow_NoStartNode")]
    [InlineData(1, false, "Flow_NoEndNode")]
    [InlineData(1, true, "Flow_NoPathFromStartToEnd")]
    [InlineData(2, true, "Flow_NoPathFromStartToEnd")]
    public void MissingPathsKeepExistingErrorsAndIdentifyOnlyAnUnambiguousStart(int startCount, bool hasEnd, string resource)
    {
        WpfTestHost.Invoke(() =>
        {
            using var graph = new Graph();
            ValidationStartNode[] starts = Enumerable.Range(0, startCount).Select(_ => graph.Add(new ValidationStartNode())).ToArray();
            if (hasEnd) graph.Add(new CVEndNode());

            Assert.False(FlowValidator.TryValidate(graph.Editor, out string error, out STNode? invalid));
            Assert.Equal(ColorVision.Engine.Properties.Resources.ResourceManager.GetString(resource), error);
            Assert.Same(startCount == 1 && hasEnd ? starts[0] : null, invalid);
        });
    }

    [Fact]
    public void ExplicitTerminationIsAnExecutionEndpoint()
    {
        WpfTestHost.Invoke(() =>
        {
            using var graph = new Graph();
            var start = graph.Add(new ValidationStartNode());
            var end = graph.Add(new FlowTerminationNode { TerminationStatus = FlowTerminationStatus.Failed });
            Connect(start, end);

            Assert.True(FlowValidator.TryValidate(graph.Editor, out _, out _));
        });
    }

    [Fact]
    public void LoopControlConnectionCannotSubstituteForAStartToEndExecutionPath()
    {
        WpfTestHost.Invoke(() =>
        {
            using var graph = new Graph();
            var start = graph.Add(new ValidationStartNode());
            var end = graph.Add(new CVEndNode());
            Assert.Equal(ConnectionStatus.Connected, start.GetAllOutputOptions().First(port => port.DataType == typeof(CVLoopCFC))
                .ConnectOption(end.GetAllInputOptions().First(port => port.DataType == typeof(CVLoopCFC))));

            Assert.False(FlowValidator.TryValidate(graph.Editor, out string error, out STNode? invalid));
            Assert.Equal(ColorVision.Engine.Properties.Resources.Flow_NoPathFromStartToEnd, error);
            Assert.Same(start, invalid);
            Assert.False(start.CanAcceptStart);
        });
    }

    [Fact]
    public void LegacyLoopAndBoundHubRemainValidAlongTheirExecutionPorts()
    {
        WpfTestHost.Invoke(() =>
        {
            using var graph = new Graph();
            var start = graph.Add(new ValidationStartNode());
            var loop = graph.Add(new LoopNode());
            var next = graph.Add(new LoopNextNode());
            var hub = graph.Add(new STNodeHub());
            var end = graph.Add(new CVEndNode());
            Connect(start, loop);
            Connect(loop, next);
            Assert.Equal(ConnectionStatus.Connected, next.GetAllOutputOptions().Single(port => port.DataType == typeof(CVStartCFC))
                .ConnectOption(hub.GetAllInputOptions()[0]));
            Assert.Equal(ConnectionStatus.Connected, hub.GetAllOutputOptions()[0].ConnectOption(end.m_in_start));

            Assert.Equal(typeof(CVStartCFC), hub.GetAllInputOptions()[0].DataType);
            Assert.Equal(typeof(CVStartCFC), hub.GetAllOutputOptions()[0].DataType);
            Assert.True(FlowValidator.TryValidate(graph.Editor, out _, out _));
        });
    }

    [Fact]
    public void InvalidRepeatRegionStillPreventsSavingAnOtherwiseCompletePath()
    {
        WpfTestHost.Invoke(() =>
        {
            using var graph = new Graph();
            var start = graph.Add(new ValidationStartNode());
            var body = graph.Add(new FlowRerouteNode());
            var end = graph.Add(new CVEndNode());
            Connect(start, body);
            Connect(body, end);
            body.SetRepeatRegion(Guid.NewGuid().ToString(), 3, 2);

            Assert.False(FlowValidator.TryValidate(graph.Editor, out string error, out STNode? invalid));
            Assert.NotEmpty(error);
            Assert.Null(invalid);
        });
    }

    private static void Connect(STNode from, STNode to)
        => Assert.Equal(ConnectionStatus.Connected, from.GetAllOutputOptions().Single(port => port.DataType == typeof(CVStartCFC))
            .ConnectOption(to.GetAllInputOptions().Single(port => port.DataType == typeof(CVStartCFC))));

    private sealed class ValidationStartNode : BaseStartNode
    {
        public ValidationStartNode() : base("Validation start") { }
    }

    private sealed class Graph : IDisposable
    {
        public STNodeEditor Editor { get; } = new();

        public T Add<T>(T node) where T : STNode
        {
            node.Create();
            Editor.Nodes.Add(node);
            return node;
        }

        public void Dispose()
        {
            foreach (BaseStartNode start in Editor.Nodes.OfType<BaseStartNode>()) start.Dispose();
            Editor.Dispose();
        }
    }
}

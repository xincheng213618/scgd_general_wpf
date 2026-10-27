using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Integration;
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
        WithGraph(graph =>
        {
            var first = graph.Add(new ValidationStartNode());
            var second = graph.Add(new ValidationStartNode());
            var secondEnd = graph.Add(new CVEndNode());
            var firstEnd = graph.Add(new CVEndNode());
            Connect(first, firstEnd);
            Connect(second, secondEnd);

            foreach (STNode active in new STNode[] { first, firstEnd, secondEnd, second })
            {
                graph.Editor.SetActiveNode(active);
                Assert.True(graph.TryValidate(out string error, out STNode? invalid));
                Assert.Empty(error);
                Assert.Null(invalid);
            }
        });
    }

    [Fact]
    public void OneCompleteExecutionPathIsEnoughWhenOtherNodesAndBranchesAreIncomplete()
    {
        WithGraph(graph =>
        {
            graph.Add(new ValidationStartNode());
            var start = graph.Add(new ValidationStartNode());
            graph.Add(new CVEndNode());
            var end = graph.Add(new CVEndNode());
            var branch = graph.Add(new ConditionBranchNode());
            var unfinished = graph.Add(new FlowRerouteNode());
            Connect(start, branch);
            branch.GetAllOutputOptions()[0].ConnectOption(end.m_in_start);
            branch.GetAllOutputOptions()[1].ConnectOption(unfinished.GetAllInputOptions().Single());

            Assert.True(graph.TryValidate(out _, out _));
        });
    }

    [Theory]
    [InlineData(0, true, "Flow_NoStartNode")]
    [InlineData(1, false, "Flow_NoEndNode")]
    [InlineData(1, true, "Flow_NoPathFromStartToEnd")]
    [InlineData(2, true, "Flow_NoPathFromStartToEnd")]
    public void MissingPathsKeepExistingErrorsAndIdentifyOnlyAnUnambiguousStart(int startCount, bool hasEnd, string resource)
    {
        WithGraph(graph =>
        {
            ValidationStartNode[] starts = Enumerable.Range(0, startCount).Select(_ => graph.Add(new ValidationStartNode())).ToArray();
            if (hasEnd) graph.Add(new CVEndNode());

            Assert.False(graph.TryValidate(out string error, out STNode? invalid));
            Assert.Equal(ColorVision.Engine.Properties.Resources.ResourceManager.GetString(resource), error);
            Assert.Same(startCount == 1 && hasEnd ? starts[0] : null, invalid);
        });
    }

    [Fact]
    public void ExplicitTerminationIsAnExecutionEndpoint()
    {
        WithGraph(graph =>
        {
            var start = graph.Add(new ValidationStartNode());
            var end = graph.Add(new FlowTerminationNode { TerminationStatus = FlowTerminationStatus.Failed });
            Connect(start, end);

            Assert.True(graph.TryValidate(out _, out _));
        });
    }

    [Fact]
    public void LoopControlConnectionCannotSubstituteForAStartToEndExecutionPath()
    {
        WithGraph(graph =>
        {
            var start = graph.Add(new ValidationStartNode());
            var end = graph.Add(new CVEndNode());
            Assert.Equal(ConnectionStatus.Connected, start.GetAllOutputOptions().First(port => port.DataType == typeof(CVLoopCFC))
                .ConnectOption(end.GetAllInputOptions().First(port => port.DataType == typeof(CVLoopCFC))));

            Assert.False(graph.TryValidate(out string error, out STNode? invalid));
            Assert.Equal(ColorVision.Engine.Properties.Resources.Flow_NoPathFromStartToEnd, error);
            Assert.Same(start, invalid);
            Assert.False(start.CanAcceptStart);
        });
    }

    [Fact]
    public void LegacyLoopAndBoundHubRemainValidAlongTheirExecutionPorts()
    {
        WithGraph(graph =>
        {
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
            Assert.True(graph.TryValidate(out _, out _));
        });
    }

    [Fact]
    public void InvalidRepeatRegionStillPreventsSavingAnOtherwiseCompletePath()
    {
        WithGraph(graph =>
        {
            var start = graph.Add(new ValidationStartNode());
            var body = graph.Add(new FlowRerouteNode());
            var end = graph.Add(new CVEndNode());
            Connect(start, body);
            Connect(body, end);
            body.SetRepeatRegion(Guid.NewGuid().ToString(), 3, 2);

            Assert.False(graph.TryValidate(out string error, out STNode? invalid));
            Assert.NotEmpty(error);
            Assert.Null(invalid);
        });
    }

    private static void Connect(STNode from, STNode to)
        => Assert.Equal(ConnectionStatus.Connected, from.GetAllOutputOptions().Single(port => port.DataType == typeof(CVStartCFC))
            .ConnectOption(to.GetAllInputOptions().Single(port => port.DataType == typeof(CVStartCFC))));

    private static void WithGraph(Action<Graph> action)
        => FlowExecutionEditingTests.WithCopilotGraph((host, editor) =>
        {
            using var graph = new Graph(host, editor);
            action(graph);
        });

    private sealed class ValidationStartNode : BaseStartNode
    {
        public ValidationStartNode() : base("Validation start") { }
    }

    private sealed class Graph : IDisposable
    {
        private readonly FlowCopilotService _host;
        public STNodeEditor Editor { get; }

        public Graph(FlowCopilotService host, STNodeEditor editor)
        {
            _host = host;
            Editor = editor;
        }

        public bool TryValidate(out string error, out STNode? invalid)
        {
            bool valid = FlowValidator.TryValidate(Editor, out error, out invalid);
            string revision = _host.CaptureCopilotFlowSnapshot().Revision;
            var result = _host.ValidateCopilotFlowGraph(revision);
            Assert.Equal(revision, result.Revision);
            Assert.True(result.RevisionMatches);
            Assert.Equal(valid, result.IsValid);
            Assert.Equal(error, result.Diagnostic);
            Assert.Equal(invalid?.Guid, result.InvalidNodeId);
            Assert.Equal(Editor.Nodes.Count, result.NodeCount);
            Assert.Equal(Editor.GetConnectionInfo().Length, result.ConnectionCount);
            Assert.False(result.IsExecutionActive);
            return valid;
        }

        public T Add<T>(T node) where T : STNode
        {
            node.Create();
            Editor.Nodes.Add(node);
            return node;
        }

        public void Dispose()
        {
            foreach (BaseStartNode start in Editor.Nodes.OfType<BaseStartNode>()) start.Dispose();
            // The shared real-host fixture owns the editor lifetime.
        }
    }
}

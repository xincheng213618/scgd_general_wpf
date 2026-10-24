using System.Reflection;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Repeat;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public sealed class FlowRepeatDagTests
{
    [Fact]
    public void CameraFanOutAndRoiJoinAreTopologicallyOrderedAndRepeatTogether()
    {
        var graph = new DagGraph(3);
        Assert.True(FlowRepeatRegion.TryCreate(new STNode[] { graph.Join, graph.Branch, graph.Source }, out var region, out _));
        Assert.Equal(new[] { graph.Source, graph.Branch, graph.Join }, region.Nodes);
        Assert.Same(graph.Source.Inputs[0], region.Input);
        Assert.Same(graph.Join.Output, region.Output);
        var action = Action();
        try
        {
            graph.Source.Inputs[0].ReceiveData(action);
            Assert.Equal(3, graph.Source.Count);
            Assert.Equal(3, graph.Branch.Count);
            Assert.Equal(new[] { new[] { 1, 101 }, new[] { 2, 102 }, new[] { 3, 103 } }, graph.Join.InputValues);
            Assert.Equal(1, graph.After.Count);
            Assert.Equal(106, Assert.Single(graph.After.InputValues)[0]);
            Assert.All(graph.Members, node => Assert.Equal(STNodeGroupState.Completed, ((ISTNodeGroupMember)node).GroupState));
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void DistinctInputsFromSameSourceAreBothRequiredWithoutDuplicatingTheJoin()
    {
        var source = Node("camera");
        var join = Node("join", 2);
        var after = Node("after");
        Connect(source.Output, join.Inputs[0]);
        Connect(source.Output, join.Inputs[1]);
        Connect(join.Output, after.Inputs[0]);
        Repeat(new[] { source, join }, 3);
        Assert.True(FlowRepeatRegion.TryCreate(new[] { join, source }, out _, out _));
        var action = Action();
        try
        {
            source.Inputs[0].ReceiveData(action);
            Assert.Equal(3, source.Count);
            Assert.Equal(3, join.Count);
            Assert.Equal(6, join.ArrivalCount);
            Assert.Equal(1, after.Count);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void DelayedAndDuplicateBranchesDoNotMixRoundsOrOverwriteAcceptedPortData()
    {
        var graph = new DagGraph(3);
        graph.Branch.Manual = true;
        var action = Action();
        try
        {
            graph.Source.Inputs[0].ReceiveData(action);
            CVStartCFC sourceFirst = graph.Source.LastOutput!;
            CVStartCFC branchFirst = graph.Branch.Pending.Dequeue();
            graph.Source.Output.TransferData(sourceFirst);
            graph.Join.Inputs[0].ReceiveData(new CVStartCFC(sourceFirst));
            Assert.Equal(1, graph.Branch.Count);
            Assert.Equal(0, graph.Join.Count);
            Assert.Equal(1, Value(graph.Join.Inputs[0]));
            graph.Branch.Output.TransferData(branchFirst);
            Assert.Equal(2, graph.Source.Count);
            Assert.Equal(1, graph.Join.Count);
            Assert.Equal(2, Value(graph.Join.Inputs[0]));
            Assert.Null(graph.Join.Inputs[1].Data);

            // TransferData writes source.Data before the repeat guard, so a stale response must restore it.
            graph.Source.Output.TransferData(sourceFirst);
            Assert.Equal(2, Value(graph.Source.Output));
            graph.Branch.Output.TransferData(branchFirst);
            Assert.Null(graph.Branch.Output.Data);
            graph.Join.Inputs[1].ReceiveData(branchFirst);
            Assert.Null(graph.Join.Inputs[1].Data);
            Assert.Equal(1, graph.Join.Count);
            graph.Branch.Output.TransferData(graph.Branch.Pending.Dequeue());
            Assert.Equal(3, graph.Source.Count);
            graph.Branch.Output.TransferData(graph.Branch.Pending.Dequeue());
            graph.Branch.Output.TransferData(branchFirst);
            Assert.Equal(103, Value(graph.Branch.Output));
            Assert.Equal(new[] { new[] { 1, 101 }, new[] { 2, 102 }, new[] { 3, 103 } }, graph.Join.InputValues);
            Assert.Equal(1, graph.After.Count);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void ExitCannotAdvanceUntilEveryRequiredBranchAndInputHasCompleted()
    {
        var graph = new DagGraph(2);
        graph.Branch.Manual = true;
        var action = Action();
        try
        {
            graph.Source.Inputs[0].ReceiveData(action);
            graph.Join.Output.TransferData(new CVStartCFC(graph.Source.LastOutput!));
            Assert.Equal(1, graph.Source.Count);
            Assert.Equal(0, graph.After.Count);
            Assert.Null(graph.Join.Output.Data);
            graph.Join.Inputs[1].ReceiveData(new CVStartCFC(graph.Source.LastOutput!));
            Assert.Equal(0, graph.Join.Count);
            graph.Branch.Output.TransferData(graph.Branch.Pending.Dequeue());
            graph.Branch.Output.TransferData(graph.Branch.Pending.Dequeue());
            Assert.Equal(2, graph.Join.Count);
            Assert.Equal(1, graph.After.Count);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void OldRunResponsesRestoreNewRunsPortsInsteadOfReintroducingOldInput()
    {
        var graph = new DagGraph(2);
        graph.Branch.Manual = true;
        var old = Action();
        graph.Source.Inputs[0].ReceiveData(old);
        CVStartCFC oldSource = graph.Source.LastOutput!;
        CVStartCFC oldBranch = graph.Branch.Pending.Dequeue();
        old.RuntimeResources.Dispose();
        var current = Action();
        try
        {
            graph.Source.Inputs[0].ReceiveData(current);
            Assert.Equal(2, Value(graph.Join.Inputs[0]));
            graph.Source.Output.TransferData(oldSource);
            graph.Join.Inputs[0].ReceiveData(oldSource);
            graph.Branch.Output.TransferData(oldBranch);
            Assert.Equal(2, Value(graph.Source.Output));
            Assert.Equal(2, Value(graph.Join.Inputs[0]));
            Assert.Null(graph.Branch.Output.Data);
            Assert.Null(graph.Join.Inputs[1].Data);
            graph.Branch.Output.TransferData(graph.Branch.Pending.Dequeue());
            graph.Branch.Output.TransferData(graph.Branch.Pending.Dequeue());
            Assert.Equal(new[] { new[] { 2, 102 }, new[] { 3, 103 } }, graph.Join.InputValues);
            Assert.Equal(1, graph.After.Count);
        }
        finally { current.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void BranchResourcesAreReleasedBetweenRoundsAndFinalResourcesSurviveUntilFlowFinishes()
    {
        var graph = new DagGraph(3);
        var sourceResources = new List<Resource>();
        var branchResources = new List<Resource>();
        var upstream = new Resource();
        graph.Source.Work = (node, action) =>
        {
            if (sourceResources.Count > 0) Assert.Equal(1, sourceResources[^1].DisposeCount);
            if (branchResources.Count > 0) Assert.Equal(1, branchResources[^1].DisposeCount);
            Assert.Equal(0, upstream.DisposeCount);
            action.Data["value"] = node.Count;
            var resource = new Resource();
            sourceResources.Add(resource);
            action.SetRuntimeResource("source-" + node.Count, resource);
        };
        graph.Branch.Work = (node, action) =>
        {
            action.Data["value"] = (int)action.Data["value"] + 100;
            var resource = new Resource();
            branchResources.Add(resource);
            action.SetRuntimeResource("branch-" + node.Count, resource);
        };
        var action = Action();
        action.RuntimeResources.Set("upstream", upstream);
        try
        {
            graph.Source.Inputs[0].ReceiveData(action);
            Assert.All(sourceResources.Take(2).Concat(branchResources.Take(2)), resource => Assert.Equal(1, resource.DisposeCount));
            Assert.Equal(0, sourceResources[^1].DisposeCount);
            Assert.Equal(0, branchResources[^1].DisposeCount);
            Assert.Equal(1, graph.After.Count);
        }
        finally { action.RuntimeResources.Dispose(); }
        Assert.All(sourceResources.Concat(branchResources).Append(upstream), resource => Assert.Equal(1, resource.DisposeCount));
    }

    [Fact]
    public void IncompleteBranchesExternalInputsMultipleEntriesExitsAndCyclesAreRejected()
    {
        var graph = new DagGraph(2);
        Assert.False(FlowRepeatRegion.TryCreate(new[] { graph.Source, graph.Join }, out _, out _));
        Assert.False(FlowRepeatRegion.TryCreate(new[] { graph.Source, graph.Branch }, out _, out _));
        var outside = Node("outside");
        Connect(graph.Branch.Output, outside.Inputs[0]);
        Assert.False(FlowRepeatRegion.TryCreate(graph.Members, out _, out _));

        var left = Node("left");
        var right = Node("right");
        var join = Node("join", 2);
        Connect(left.Output, join.Inputs[0]);
        Connect(right.Output, join.Inputs[1]);
        Assert.False(FlowRepeatRegion.TryCreate(new[] { left, right, join }, out _, out _));
        var source = Node("source");
        var branch1 = Node("branch 1");
        var branch2 = Node("branch 2");
        Connect(source.Output, branch1.Inputs[0]);
        Connect(source.Output, branch2.Inputs[0]);
        Assert.False(FlowRepeatRegion.TryCreate(new[] { source, branch1, branch2 }, out _, out _));

        var incomplete = Node("unwired input", 2);
        var single = Node("single");
        Connect(single.Output, incomplete.Inputs[0]);
        Assert.False(FlowRepeatRegion.TryCreate(new[] { single, incomplete }, out _, out _));
        Assert.False(FlowRepeatRegion.TryCreate(new[] { Node("two external inputs", 2) }, out _, out _));

        var cycle = new DagGraph(2);
        // The editor normally rejects cycles; simulate an invalid saved graph to exercise region validation.
        cycle.Join.Output.ConnectedOption.Add(cycle.Source.Inputs[0]);
        cycle.Source.Inputs[0].ConnectedOption.Add(cycle.Join.Output);
        Assert.False(FlowRepeatRegion.TryCreate(cycle.Members, out _, out _));
    }

    private static CVStartCFC Action()
    {
        var action = new CVStartCFC(Guid.NewGuid().ToString()) { FlowStatus = StatusTypeEnum.Runing };
        action.Data["value"] = 0;
        return action;
    }

    private static int Round(CVStartCFC action) => (int)typeof(CVStartCFC)
        .GetProperty("RepeatRound", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(action)!;

    private static object? Run(CVStartCFC action) => typeof(CVStartCFC)
        .GetProperty("RepeatRun", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(action);

    private static int Value(STNodeOption option) => (int)((CVStartCFC)option.Data).Data["value"];
    private static void Connect(STNodeOption output, STNodeOption input) => Assert.Equal(ConnectionStatus.Connected, output.ConnectOption(input, false));
    private static Probe Node(string name, int inputs = 1) { var node = new Probe(name, inputs); node.Create(); return node; }
    private static void Repeat(Probe[] nodes, int count)
    {
        string id = Guid.NewGuid().ToString();
        foreach (var node in nodes) node.SetRepeatRegion(id, count, nodes.Length);
    }

    private sealed class Resource : IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }

    private sealed class DagGraph
    {
        public Probe Source { get; } = Node("camera");
        public Probe Branch { get; } = Node("ROI");
        public Probe Join { get; } = Node("AI", 2);
        public Probe After { get; } = Node("after");
        public Probe[] Members => new[] { Source, Branch, Join };
        public DagGraph(int count)
        {
            Connect(Source.Output, Branch.Inputs[0]);
            Connect(Source.Output, Join.Inputs[0]);
            Connect(Branch.Output, Join.Inputs[1]);
            Connect(Join.Output, After.Inputs[0]);
            Repeat(Members, count);
            Source.Work = (node, action) => action.Data["value"] = node.Count;
            Branch.Work = (_, action) => action.Data["value"] = (int)action.Data["value"] + 100;
            Join.Work = (node, action) => action.Data["value"] = node.InputValues[^1].Sum();
        }
    }

    private sealed class Probe : CVCommonNode
    {
        private readonly int inputCount;
        public STNodeOption[] Inputs { get; private set; } = null!;
        public STNodeOption Output { get; private set; } = null!;
        public int Count { get; private set; }
        public int ArrivalCount { get; private set; }
        public bool Manual { get; set; }
        public CVStartCFC? LastOutput { get; private set; }
        public List<int[]> InputValues { get; } = new();
        public Queue<CVStartCFC> Pending { get; } = new();
        public Action<Probe, CVStartCFC>? Work { get; set; }
        public Probe(string title, int inputCount) : base(title, "DagProbe", title) => this.inputCount = inputCount;
        protected override void OnCreate()
        {
            base.OnCreate();
            Inputs = Enumerable.Range(0, inputCount).Select(index => InputOptions.Add("IN_" + index, typeof(CVStartCFC), true)).ToArray();
            Output = OutputOptions.Add("OUT", typeof(CVStartCFC), false);
            foreach (var input in Inputs)
                input.DataTransfer += (_, args) =>
                {
                    if (args.TargetOption.Data is not CVStartCFC received || !received.IsRunning) return;
                    input.Data = new CVStartCFC(received);
                    ArrivalCount++;
                    if (Inputs.Any(port => port.Data is not CVStartCFC value || Round(value) != Round(received) || !ReferenceEquals(Run(value), Run(received)))) return;
                    InputValues.Add(Inputs.Select(Value).ToArray());
                    Count++;
                    var action = new CVStartCFC(received);
                    Work?.Invoke(this, action);
                    LastOutput = action;
                    if (Manual) Pending.Enqueue(action);
                    else Output.TransferData(action);
                };
        }
    }
}

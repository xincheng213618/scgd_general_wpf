using ColorVision.Engine.FlowProcessing.Compilation;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Start;
using System.IO;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public class FlowSubflowCompilerTests
{
    [Fact]
    public void Compile_ExpandsSingleChild_AndPreservesOpaqueProperties()
    {
        TestGraph parent = CreateLinearGraph(
            "parent-before",
            "parent-after");
        TestGraph child = CreateLinearGraph(
            "child-node",
            secondNodeName: null,
            firstDeviceCode: "DEV.Child");
        var resolver = new DictionaryResolver(
            new ResolvedFlowDefinition(
                "child",
                "7",
                null,
                child.CanvasData));
        FlowCompilationResult result =
            new FlowSubflowCompiler(resolver).Compile(
                parent.CanvasData,
                CreateCallSidecar(
                    "insert-child",
                    parent.First!,
                    parent.Second!,
                    new FlowDefinitionReference(
                        "child",
                        "7")));

        using var container = new CVNodeContainer();
        container.LoadAssembly(
            typeof(FlowSubflowTestStartNode).Assembly);
        container.LoadCanvas(result.CanvasData);

        FlowSubflowTestNode imported = Assert.Single(
            container.Nodes
                .Cast<STNode>()
                .OfType<FlowSubflowTestNode>()
                .Where(node =>
                    node.NodeName == "child-node"));
        Assert.Equal("DEV.Child", imported.DeviceCode);
        Assert.Equal(5, container.Nodes.Count);
        Assert.Equal(
            4,
            STNodeCanvasWriter.GetConnections(
                container.Nodes.Cast<STNode>()).Length);
        Assert.DoesNotContain(
            imported.Guid,
            new[] { child.First!.Guid });
        Assert.Equal(1, result.Map.Calls.Count);
    }

    [Fact]
    public void Compile_ExpandsNestedChildren()
    {
        TestGraph parent = CreateLinearGraph(
            "parent-before",
            "parent-after");
        TestGraph childA = CreateLinearGraph(
            "a-before",
            "a-after");
        TestGraph childB = CreateLinearGraph(
            "b-node",
            secondNodeName: null);
        var resolver = new DictionaryResolver(
            new ResolvedFlowDefinition(
                "A",
                "1",
                null,
                childA.CanvasData,
                CreateCallSidecar(
                    "A-to-B",
                    childA.First!,
                    childA.Second!,
                    new FlowDefinitionReference("B", "3"))),
            new ResolvedFlowDefinition(
                "B",
                "3",
                null,
                childB.CanvasData));

        FlowCompilationResult result =
            new FlowSubflowCompiler(resolver).Compile(
                parent.CanvasData,
                CreateCallSidecar(
                    "root-to-A",
                    parent.First!,
                    parent.Second!,
                    new FlowDefinitionReference("A", "1")));

        using var container = new CVNodeContainer();
        container.LoadAssembly(
            typeof(FlowSubflowTestStartNode).Assembly);
        container.LoadCanvas(result.CanvasData);
        string[] names = container.Nodes
            .Cast<STNode>()
            .OfType<FlowSubflowTestNode>()
            .Select(node => node.NodeName)
            .ToArray();

        Assert.Contains("a-before", names);
        Assert.Contains("b-node", names);
        Assert.Contains("a-after", names);
        Assert.Equal(7, container.Nodes.Count);
        Assert.Equal(2, result.Map.Calls.Count);
    }

    [Fact]
    public void Compile_PassesAndVerifiesFixedRevision()
    {
        TestGraph parent = CreateLinearGraph(
            "parent-before",
            "parent-after");
        TestGraph child = CreateLinearGraph(
            "child-node",
            secondNodeName: null);
        var resolver = new DictionaryResolver(
            new ResolvedFlowDefinition(
                "child",
                "fixed-r17",
                null,
                child.CanvasData));
        var requested = new FlowDefinitionReference(
            "child",
            "fixed-r17");

        FlowCompilationResult result =
            new FlowSubflowCompiler(resolver).Compile(
                parent.CanvasData,
                CreateCallSidecar(
                    "fixed",
                    parent.First!,
                    parent.Second!,
                    requested));

        Assert.Equal(
            "fixed-r17",
            Assert.Single(resolver.Requests).Revision);
        Assert.Equal(
            "fixed-r17",
            Assert.Single(result.Map.Calls)
                .ResolvedRevision);
    }

    [Fact]
    public void Compile_RejectsRecursiveReferences()
    {
        TestGraph parent = CreateLinearGraph(
            "parent-before",
            "parent-after");
        TestGraph graphA = CreateLinearGraph(
            "a-before",
            "a-after");
        TestGraph graphB = CreateLinearGraph(
            "b-before",
            "b-after");
        var resolver = new DictionaryResolver(
            new ResolvedFlowDefinition(
                "A",
                "1",
                null,
                graphA.CanvasData,
                CreateCallSidecar(
                    "A-to-B",
                    graphA.First!,
                    graphA.Second!,
                    new FlowDefinitionReference("B", "1"))),
            new ResolvedFlowDefinition(
                "B",
                "1",
                null,
                graphB.CanvasData,
                CreateCallSidecar(
                    "B-to-A",
                    graphB.First!,
                    graphB.Second!,
                    new FlowDefinitionReference("A", "1"))));

        FlowCompilationException exception = Assert.Throws<
            FlowCompilationException>(() =>
                new FlowSubflowCompiler(resolver).Compile(
                    parent.CanvasData,
                    CreateCallSidecar(
                        "root-to-A",
                        parent.First!,
                        parent.Second!,
                        new FlowDefinitionReference("A", "1"))));

        Assert.Equal(
            FlowCompilationError.RecursiveReference,
            exception.Error);
    }

    [Fact]
    public void Compile_RejectsMissingChild()
    {
        TestGraph parent = CreateLinearGraph(
            "parent-before",
            "parent-after");
        var resolver = new DictionaryResolver();

        FlowCompilationException exception = Assert.Throws<
            FlowCompilationException>(() =>
                new FlowSubflowCompiler(resolver).Compile(
                    parent.CanvasData,
                    CreateCallSidecar(
                        "missing",
                        parent.First!,
                        parent.Second!,
                        new FlowDefinitionReference(
                            "does-not-exist",
                            "1"))));

        Assert.Equal(
            FlowCompilationError.MissingDefinition,
            exception.Error);
    }

    [Fact]
    public void Compile_RejectsChildWithMultipleEntries()
    {
        TestGraph parent = CreateLinearGraph(
            "parent-before",
            "parent-after");
        FlowSubflowTestStartNode childStart =
            CreateNode<FlowSubflowTestStartNode>();
        FlowSubflowTestNode childA =
            CreatePassNode("child-a", "DEV.A");
        FlowSubflowTestNode childB =
            CreatePassNode("child-b", "DEV.B");
        CVEndNode childEnd = CreateNode<CVEndNode>();
        byte[] invalidChild = WriteCanvas(
            new STNode[]
            {
                childStart,
                childA,
                childB,
                childEnd,
            },
            Connect(
                childStart.m_op_start,
                childA.Input),
            Connect(
                childStart.m_op_start,
                childB.Input),
            Connect(
                childA.Output,
                childEnd.m_in_start));
        var resolver = new DictionaryResolver(
            new ResolvedFlowDefinition(
                "multi-entry",
                "1",
                null,
                invalidChild));

        FlowCompilationException exception = Assert.Throws<
            FlowCompilationException>(() =>
                new FlowSubflowCompiler(resolver).Compile(
                    parent.CanvasData,
                    CreateCallSidecar(
                        "multi-entry",
                        parent.First!,
                        parent.Second!,
                        new FlowDefinitionReference(
                            "multi-entry",
                            "1"))));

        Assert.Equal(
            FlowCompilationError.InvalidChildBoundary,
            exception.Error);
    }

    [Fact]
    public void Compile_RejectsLoopBoundary()
    {
        TestGraph parent = CreateLinearGraph(
            "parent-before",
            "parent-after");
        FlowSubflowTestStartNode childStart =
            CreateNode<FlowSubflowTestStartNode>();
        FlowSubflowTestNode childNode =
            CreatePassNode("child", "DEV.Child");
        FlowSubflowLoopSinkNode loopSink =
            CreateNode<FlowSubflowLoopSinkNode>();
        CVEndNode childEnd = CreateNode<CVEndNode>();
        byte[] invalidChild = WriteCanvas(
            new STNode[]
            {
                childStart,
                childNode,
                loopSink,
                childEnd,
            },
            Connect(
                childStart.m_op_start,
                childNode.Input),
            Connect(
                childNode.Output,
                childEnd.m_in_start),
            Connect(
                childStart.GetAllOutputOptions()[1],
                loopSink.Input));
        var resolver = new DictionaryResolver(
            new ResolvedFlowDefinition(
                "loop-boundary",
                "1",
                null,
                invalidChild));

        FlowCompilationException exception = Assert.Throws<
            FlowCompilationException>(() =>
                new FlowSubflowCompiler(resolver).Compile(
                    parent.CanvasData,
                    CreateCallSidecar(
                        "loop-boundary",
                        parent.First!,
                        parent.Second!,
                        new FlowDefinitionReference(
                            "loop-boundary",
                            "1"))));

        Assert.Equal(
            FlowCompilationError.LoopBoundaryNotAllowed,
            exception.Error);
    }

    [Fact]
    public void Compile_IsByteAndHashDeterministic()
    {
        TestGraph parent = CreateLinearGraph(
            "parent-before",
            "parent-after");
        TestGraph child = CreateLinearGraph(
            "child-node",
            secondNodeName: null);
        var resolver = new DictionaryResolver(
            new ResolvedFlowDefinition(
                "child",
                "1",
                null,
                child.CanvasData));
        FlowSubflowSidecar sidecar = CreateCallSidecar(
            "stable-call",
            parent.First!,
            parent.Second!,
            new FlowDefinitionReference("child", "1"));
        var compiler = new FlowSubflowCompiler(resolver);

        FlowCompilationResult first =
            compiler.Compile(parent.CanvasData, sidecar);
        FlowCompilationResult second =
            compiler.Compile(parent.CanvasData, sidecar);

        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.Equal(first.CanvasData, second.CanvasData);
        Assert.Equal(
            first.Map.Nodes.Select(node =>
                node.CompiledNodeId),
            second.Map.Nodes.Select(node =>
                node.CompiledNodeId));
    }

    [Fact]
    public void Compile_OutputRemainsReadableByLegacyContainer()
    {
        TestGraph parent = CreateLinearGraph(
            "parent-before",
            "parent-after");
        TestGraph child = CreateLinearGraph(
            "child-node",
            secondNodeName: null);
        var resolver = new DictionaryResolver(
            new ResolvedFlowDefinition(
                "child",
                "1",
                null,
                child.CanvasData));
        FlowCompilationResult result =
            new FlowSubflowCompiler(resolver).Compile(
                parent.CanvasData,
                CreateCallSidecar(
                    "legacy-readable",
                    parent.First!,
                    parent.Second!,
                    new FlowDefinitionReference("child", "1")));

        using var container = new CVNodeContainer();
        container.LoadAssembly(
            typeof(FlowSubflowTestStartNode).Assembly);
        container.LoadCanvas(result.CanvasData);
        byte[] roundTripped = container.GetCanvasData();
        using var secondContainer = new CVNodeContainer();
        secondContainer.LoadAssembly(
            typeof(FlowSubflowTestStartNode).Assembly);
        secondContainer.LoadCanvas(roundTripped);

        Assert.Equal(
            container.Nodes.Count,
            secondContainer.Nodes.Count);
        Assert.Equal(
            STNodeConstant.NodeFlag,
            result.CanvasData.Take(
                STNodeConstant.NodeFlag.Length));
        Assert.Equal(
            STNodeConstant.Version,
            result.CanvasData[
                STNodeConstant.NodeFlag.Length]);
    }

    private static TestGraph CreateLinearGraph(
        string firstNodeName,
        string? secondNodeName,
        string firstDeviceCode = "DEV.Test")
    {
        FlowSubflowTestStartNode start =
            CreateNode<FlowSubflowTestStartNode>();
        FlowSubflowTestNode first =
            CreatePassNode(
                firstNodeName,
                firstDeviceCode);
        CVEndNode end = CreateNode<CVEndNode>();
        if (secondNodeName == null)
        {
            return new TestGraph(
                WriteCanvas(
                    new STNode[] { start, first, end },
                    Connect(start.m_op_start, first.Input),
                    Connect(first.Output, end.m_in_start)),
                start,
                first,
                null,
                end);
        }

        FlowSubflowTestNode second =
            CreatePassNode(
                secondNodeName,
                "DEV.Test");
        return new TestGraph(
            WriteCanvas(
                new STNode[]
                {
                    start,
                    first,
                    second,
                    end,
                },
                Connect(start.m_op_start, first.Input),
                Connect(first.Output, second.Input),
                Connect(second.Output, end.m_in_start)),
            start,
            first,
            second,
            end);
    }

    private static T CreateNode<T>()
        where T : STNode, new()
    {
        var node = new T();
        node.Create();
        return node;
    }

    private static FlowSubflowTestNode CreatePassNode(
        string nodeName,
        string deviceCode)
    {
        FlowSubflowTestNode node =
            CreateNode<FlowSubflowTestNode>();
        node.NodeName = nodeName;
        node.DeviceCode = deviceCode;
        return node;
    }

    private static ConnectionInfo Connect(
        STNodeOption output,
        STNodeOption input)
    {
        return new ConnectionInfo
        {
            Output = output,
            Input = input,
        };
    }

    private static byte[] WriteCanvas(
        IReadOnlyList<STNode> nodes,
        params ConnectionInfo[] connections)
    {
        using var stream = new MemoryStream();
        STNodeCanvasWriter.Write(
            stream,
            nodes,
            connections,
            canvasOffsetX: 10,
            canvasOffsetY: 10,
            canvasScale: 1);
        return stream.ToArray();
    }

    private static FlowSubflowSidecar CreateCallSidecar(
        string callId,
        FlowSubflowTestNode source,
        FlowSubflowTestNode target,
        FlowDefinitionReference child)
    {
        return new FlowSubflowSidecar(
            new[]
            {
                new FlowSubflowCall(
                    callId,
                    new FlowPortReference(
                        source.Guid,
                        OptionIndex: 0),
                    new FlowPortReference(
                        target.Guid,
                        OptionIndex: 0),
                    child),
            });
    }

    private sealed record TestGraph(
        byte[] CanvasData,
        FlowSubflowTestStartNode Start,
        FlowSubflowTestNode? First,
        FlowSubflowTestNode? Second,
        CVEndNode End);

    private sealed class DictionaryResolver :
        IFlowSubflowResolver
    {
        private readonly Dictionary<string, ResolvedFlowDefinition>
            _definitions;

        public DictionaryResolver(
            params ResolvedFlowDefinition[] definitions)
        {
            _definitions = definitions.ToDictionary(
                definition => definition.FlowKey,
                StringComparer.OrdinalIgnoreCase);
        }

        public List<FlowDefinitionReference> Requests { get; } =
            new();

        public ResolvedFlowDefinition? Resolve(
            FlowDefinitionReference reference)
        {
            Requests.Add(reference);
            return _definitions.GetValueOrDefault(
                reference.FlowKey);
        }
    }
}

public sealed class FlowSubflowTestStartNode : BaseStartNode
{
    public FlowSubflowTestStartNode()
        : base("SubflowTestStart")
    {
    }
}

public sealed class FlowSubflowTestNode : CVCommonNode
{
    public FlowSubflowTestNode()
        : base(
            "SubflowTest",
            "SubflowTest",
            "Node",
            "DEV.Test")
    {
    }

    public STNodeOption Input { get; private set; } = null!;

    public STNodeOption Output { get; private set; } = null!;

    protected override void OnCreate()
    {
        base.OnCreate();
        Input = InputOptions.Add(
            "IN",
            typeof(CVStartCFC),
            bSingle: true);
        Output = OutputOptions.Add(
            "OUT",
            typeof(CVStartCFC),
            bSingle: false);
    }
}

public sealed class FlowSubflowLoopSinkNode : STNode
{
    public STNodeOption Input { get; private set; } = null!;

    protected override void OnCreate()
    {
        base.OnCreate();
        Input = InputOptions.Add(
            "LOOP",
            typeof(CVLoopCFC),
            bSingle: true);
    }
}

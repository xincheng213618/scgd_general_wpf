using ColorVision.Engine.FlowProcessing.Artifacts;
using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Templates.Flow.Routing;
using FlowEngineLib.End;
using FlowEngineLib.Runtime;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Security.Cryptography;

namespace ColorVision.UI.Tests;

public sealed class FlowArtifactBuilderTests
{
    [Fact]
    public void Build_IsDeterministicForEquivalentAuthoringInput()
    {
        TestGraph graph = CreateLinearGraph(
            "first",
            "second");
        var firstPolicy = new FlowArtifactPolicy(
            retryPolicies:
            [
                CreateRetry(graph.First),
                CreateRetry(graph.Second!),
            ]);
        var reorderedPolicy = new FlowArtifactPolicy(
            retryPolicies:
            [
                CreateRetry(graph.Second!),
                CreateRetry(graph.First),
            ]);
        var builder = new FlowArtifactBuilder();

        FlowArtifactBundle first = builder.Build(
            new FlowArtifactDraft(
                "flow:deterministic",
                "12",
                graph.CanvasData,
                authoringPolicy: firstPolicy));
        FlowArtifactBundle second = builder.Build(
            new FlowArtifactDraft(
                " flow:deterministic ",
                "12",
                graph.CanvasData,
                authoringPolicy: reorderedPolicy));

        Assert.Equal(first.Manifest, second.Manifest);
        Assert.Equal(
            first.Executable.CompiledStn,
            second.Executable.CompiledStn);
        Assert.Equal(
            first.Manifest.ArtifactHash,
            second.Manifest.ArtifactHash);
        FlowArtifactSerializedParts firstParts =
            FlowArtifactSerializer.Serialize(first);
        FlowArtifactSerializedParts secondParts =
            FlowArtifactSerializer.Serialize(second);
        Assert.Equal(
            firstParts.Manifest,
            secondParts.Manifest);
        Assert.Equal(
            firstParts.AuthoringPolicy,
            secondParts.AuthoringPolicy);
        Assert.Equal(
            firstParts.CompilationMap,
            secondParts.CompilationMap);
        Assert.Equal(
            first.Manifest.SourceHash,
            Hash(firstParts.AuthoringStn));
        Assert.Equal(
            first.Manifest.SubflowHash,
            Hash(firstParts.SubflowSidecar));
        Assert.Equal(
            first.Manifest.PolicyHash,
            Hash(firstParts.AuthoringPolicy));
        Assert.Equal(
            first.Manifest.CompiledStnHash,
            Hash(firstParts.CompiledStn));
        Assert.Equal(
            first.Manifest.EffectivePolicyHash,
            Hash(firstParts.EffectivePolicy));
        Assert.Equal(
            first.Manifest.CompilationMapHash,
            Hash(firstParts.CompilationMap));
        Assert.All(
            GetManifestHashes(first.Manifest),
            value => Assert.Matches("^[0-9a-f]{64}$", value));
    }

    [Fact]
    public void Build_AnyAuthoringOrDependencyChange_ChangesArtifactHash()
    {
        TestGraph parent = CreateLinearGraph(
            "parent-first",
            "parent-second");
        TestGraph changedSource = CreateLinearGraph(
            "changed-first",
            "parent-second");
        TestGraph child = CreateLinearGraph(
            "child",
            secondNodeName: null);
        TestGraph changedChild = CreateLinearGraph(
            "changed-child",
            secondNodeName: null);
        FlowSubflowSidecar sidecar = CreateCallSidecar(
            parent,
            callId: "insert",
            new FlowDefinitionReference("flow:child"));

        string baseline = new FlowArtifactBuilder().Build(
            new FlowArtifactDraft(
                "flow:root",
                "1",
                parent.CanvasData)).Manifest.ArtifactHash;
        string sourceChanged = new FlowArtifactBuilder().Build(
            new FlowArtifactDraft(
                "flow:root",
                "1",
                changedSource.CanvasData)).Manifest.ArtifactHash;
        string policyChanged = new FlowArtifactBuilder().Build(
            new FlowArtifactDraft(
                "flow:root",
                "1",
                parent.CanvasData,
                authoringPolicy: new FlowArtifactPolicy(
                    retryPolicies:
                    [
                        CreateRetry(parent.First),
                    ]))).Manifest.ArtifactHash;
        string dependencyRevisionOne =
            new FlowArtifactBuilder(
                new DictionaryArtifactResolver(
                    new FlowArtifactDependencyDefinition(
                        "flow:child",
                        "1",
                        child.CanvasData))).Build(
                new FlowArtifactDraft(
                    "flow:root",
                    "1",
                    parent.CanvasData,
                    sidecar)).Manifest.ArtifactHash;
        string sidecarChanged =
            new FlowArtifactBuilder(
                new DictionaryArtifactResolver(
                    new FlowArtifactDependencyDefinition(
                        "flow:child",
                        "1",
                        child.CanvasData))).Build(
                new FlowArtifactDraft(
                    "flow:root",
                    "1",
                    parent.CanvasData,
                    CreateCallSidecar(
                        parent,
                        callId: "insert-renamed",
                        new FlowDefinitionReference(
                            "flow:child")))).Manifest.ArtifactHash;
        string dependencyRevisionTwo =
            new FlowArtifactBuilder(
                new DictionaryArtifactResolver(
                    new FlowArtifactDependencyDefinition(
                        "flow:child",
                        "2",
                        child.CanvasData))).Build(
                new FlowArtifactDraft(
                    "flow:root",
                    "1",
                    parent.CanvasData,
                    sidecar)).Manifest.ArtifactHash;
        string dependencyContentChanged =
            new FlowArtifactBuilder(
                new DictionaryArtifactResolver(
                    new FlowArtifactDependencyDefinition(
                        "flow:child",
                        "1",
                        changedChild.CanvasData))).Build(
                new FlowArtifactDraft(
                    "flow:root",
                    "1",
                    parent.CanvasData,
                    sidecar)).Manifest.ArtifactHash;
        string dependencyPolicyChanged =
            new FlowArtifactBuilder(
                new DictionaryArtifactResolver(
                    new FlowArtifactDependencyDefinition(
                        "flow:child",
                        "1",
                        child.CanvasData,
                        authoringPolicy: new FlowArtifactPolicy(
                            retryPolicies:
                            [
                                CreateRetry(child.First),
                            ])))).Build(
                new FlowArtifactDraft(
                    "flow:root",
                    "1",
                    parent.CanvasData,
                    sidecar)).Manifest.ArtifactHash;

        Assert.Equal(
            8,
            new[]
            {
                baseline,
                sourceChanged,
                policyChanged,
                dependencyRevisionOne,
                sidecarChanged,
                dependencyRevisionTwo,
                dependencyContentChanged,
                dependencyPolicyChanged,
            }.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Build_LegacyFlowWithoutSubflows_PreservesExactStnBytes()
    {
        TestGraph graph = CreateLinearGraph(
            "legacy-first",
            "legacy-second");

        FlowArtifactBundle artifact =
            new FlowArtifactBuilder().Build(
                new FlowArtifactDraft(
                    "flow:legacy",
                    revision: null,
                    graph.CanvasData));

        Assert.Equal(
            graph.CanvasData,
            artifact.Executable.CompiledStn);
        Assert.Equal(
            Hash(graph.CanvasData),
            artifact.Manifest.SourceHash);
        Assert.Equal(
            artifact.Manifest.SourceHash,
            artifact.Manifest.CompiledStnHash);
        Assert.Empty(artifact.Executable.Dependencies);
        FlowArtifactValidator.Validate(artifact);
    }

    [Fact]
    public void Build_LocksDependenciesAndRemapsRootAndChildPolicies()
    {
        TestGraph parent = CreateLinearGraph(
            "parent-before",
            "parent-after");
        TestGraph child = CreateLinearGraph(
            "child-node",
            secondNodeName: null);
        var rootPolicy = new FlowArtifactPolicy(
            errorRoutes:
            [
                CreateRoute(parent.First, parent.Second!),
            ],
            retryPolicies:
            [
                CreateRetry(parent.First),
            ]);
        var childPolicy = new FlowArtifactPolicy(
            retryPolicies:
            [
                CreateRetry(child.First),
            ]);
        var resolver = new DictionaryArtifactResolver(
            new FlowArtifactDependencyDefinition(
                "flow:child",
                "7",
                child.CanvasData,
                authoringPolicy: childPolicy));
        FlowSubflowSidecar sidecar = CreateCallSidecar(
            parent,
            "insert-child",
            new FlowDefinitionReference("flow:child"));

        FlowArtifactBundle artifact =
            new FlowArtifactBuilder(resolver).Build(
                new FlowArtifactDraft(
                    "flow:root",
                    "20",
                    parent.CanvasData,
                    sidecar,
                    rootPolicy));

        FlowArtifactDependencyLock dependency = Assert.Single(
            artifact.Executable.Dependencies);
        Assert.Equal("flow:child", dependency.FlowKey);
        Assert.Equal("7", dependency.Revision);
        Assert.Equal(
            Hash(child.CanvasData),
            dependency.ContentHash);
        Assert.Matches(
            "^[0-9a-f]{64}$",
            dependency.DefinitionHash);
        Assert.NotEqual(
            parent.CanvasData,
            artifact.Executable.CompiledStn);
        Assert.Equal(
            2,
            artifact.Executable.EffectivePolicy
                .RetryPolicies.Count);
        Assert.Single(
            artifact.Executable.EffectivePolicy.ErrorRoutes);

        FlowCompiledNodeMap childMap = Assert.Single(
            artifact.Executable.CompilationMap.Nodes.Where(
                item => item.SourceFlowKey == "flow:child"
                    && item.SourceNodeId == child.First.Guid));
        Assert.Contains(
            artifact.Executable.EffectivePolicy.RetryPolicies,
            item => item.NodeId
                == childMap.CompiledNodeId.ToString("D"));
        FlowArtifactValidator.Validate(artifact);
    }

    [Fact]
    public void Build_RejectsDependencyWithoutConcreteRevision()
    {
        TestGraph parent = CreateLinearGraph(
            "parent-before",
            "parent-after");
        TestGraph child = CreateLinearGraph(
            "child",
            secondNodeName: null);
        var resolver = new DictionaryArtifactResolver(
            new FlowArtifactDependencyDefinition(
                "flow:child",
                revision: " ",
                child.CanvasData));

        FlowArtifactException exception = Assert.Throws<
            FlowArtifactException>(() =>
                new FlowArtifactBuilder(resolver).Build(
                    new FlowArtifactDraft(
                        "flow:root",
                        "1",
                        parent.CanvasData,
                        CreateCallSidecar(
                            parent,
                            "child",
                            new FlowDefinitionReference(
                                "flow:child")))));

        Assert.Equal(
            FlowArtifactError.UnpinnedDependency,
            exception.Error);
        Assert.Equal("dependencies", exception.Component);
    }

    [Fact]
    public void Build_RejectsPolicyThatCannotMapElidedChildBoundary()
    {
        TestGraph parent = CreateLinearGraph(
            "parent-before",
            "parent-after");
        TestGraph child = CreateLinearGraph(
            "child",
            secondNodeName: null);
        var resolver = new DictionaryArtifactResolver(
            new FlowArtifactDependencyDefinition(
                "flow:child",
                "1",
                child.CanvasData,
                authoringPolicy: new FlowArtifactPolicy(
                    retryPolicies:
                    [
                        new FlowRetryPolicy(
                            child.Start.Guid.ToString("D"),
                            maxAttempts: 2,
                            initialDelayMs: 0,
                            backoff: 1,
                            maxDelayMs: 0,
                            retryableKinds:
                            [
                                FlowFailureKind.Technical,
                            ]),
                    ])));

        FlowArtifactException exception = Assert.Throws<
            FlowArtifactException>(() =>
                new FlowArtifactBuilder(resolver).Build(
                    new FlowArtifactDraft(
                        "flow:root",
                        "1",
                        parent.CanvasData,
                        CreateCallSidecar(
                            parent,
                            "child",
                            new FlowDefinitionReference(
                                "flow:child")))));

        Assert.Equal(
            FlowArtifactError.PolicyMappingUnavailable,
            exception.Error);
        Assert.Equal("effectivePolicy", exception.Component);
    }

    [Fact]
    public void Validate_DetectsExecutableAndManifestTampering()
    {
        TestGraph graph = CreateLinearGraph(
            "first",
            "second");
        FlowArtifactBundle artifact =
            new FlowArtifactBuilder().Build(
                new FlowArtifactDraft(
                    "flow:tamper",
                    "1",
                    graph.CanvasData));

        byte[] tamperedBytes = artifact.Executable.CompiledStn;
        tamperedBytes[^1] ^= 0x5a;
        FlowArtifactBundle executableTampered = artifact with
        {
            Executable = new FlowExecutableBundle(
                tamperedBytes,
                artifact.Executable.EffectivePolicy,
                artifact.Executable.CompilationMap,
                artifact.Executable.Dependencies),
        };
        FlowArtifactException executableException = Assert.Throws<
            FlowArtifactException>(() =>
                FlowArtifactValidator.Validate(
                    executableTampered));
        Assert.Equal(
            FlowArtifactError.HashMismatch,
            executableException.Error);
        Assert.Equal(
            "compiledStn",
            executableException.Component);

        FlowArtifactBundle clean =
            new FlowArtifactBuilder().Build(
                new FlowArtifactDraft(
                    "flow:tamper",
                    "1",
                    graph.CanvasData));
        FlowArtifactBundle manifestTampered = clean with
        {
            Manifest = clean.Manifest with
            {
                ArtifactHash = new string('0', 64),
            },
        };
        FlowArtifactException manifestException = Assert.Throws<
            FlowArtifactException>(() =>
                FlowArtifactValidator.Validate(
                    manifestTampered));
        Assert.Equal(
            FlowArtifactError.HashMismatch,
            manifestException.Error);
        Assert.Equal("artifact", manifestException.Component);
    }

    [Fact]
    public void Build_InvalidStndV1FailsWithStructuredError()
    {
        FlowArtifactException exception = Assert.Throws<
            FlowArtifactException>(() =>
                new FlowArtifactBuilder().Build(
                    new FlowArtifactDraft(
                        "flow:invalid",
                        "1",
                        [1, 2, 3, 4])));

        Assert.Equal(
            FlowArtifactError.InvalidAuthoringCanvas,
            exception.Error);
        Assert.Equal("root", exception.Component);
        Assert.IsType<FlowCompilationException>(
            exception.InnerException);
    }

    private static IReadOnlyList<string> GetManifestHashes(
        FlowArtifactManifest manifest)
    {
        return
        [
            manifest.SourceHash,
            manifest.SubflowHash,
            manifest.PolicyHash,
            manifest.SemanticHash,
            manifest.LayoutHash,
            manifest.DefinitionHash,
            manifest.DependencyHash,
            manifest.CompiledStnHash,
            manifest.EffectivePolicyHash,
            manifest.CompilationMapHash,
            manifest.CompilerHash,
            manifest.ArtifactHash,
        ];
    }

    private static FlowRetryPolicy CreateRetry(
        FlowSubflowTestNode node)
    {
        return new FlowRetryPolicy(
            node.Guid.ToString("D"),
            maxAttempts: 3,
            initialDelayMs: 10,
            backoff: 2,
            maxDelayMs: 100,
            retryableKinds:
            [
                FlowFailureKind.Technical,
                FlowFailureKind.Timeout,
            ]);
    }

    private static FlowErrorRoutePolicy CreateRoute(
        FlowSubflowTestNode source,
        FlowSubflowTestNode target)
    {
        return new FlowErrorRoutePolicy(
            source.Guid.ToString("D"),
            target.Guid.ToString("D"),
            targetInputIndex: 0,
            failureKinds:
            [
                FlowFailureKind.Technical,
            ]);
    }

    private static FlowSubflowSidecar CreateCallSidecar(
        TestGraph parent,
        string callId,
        FlowDefinitionReference child)
    {
        return new FlowSubflowSidecar(
        [
            new FlowSubflowCall(
                callId,
                new FlowPortReference(
                    parent.First.Guid,
                    OptionIndex: 0),
                new FlowPortReference(
                    parent.Second!.Guid,
                    OptionIndex: 0),
                child),
        ]);
    }

    private static TestGraph CreateLinearGraph(
        string firstNodeName,
        string? secondNodeName)
    {
        FlowSubflowTestStartNode start =
            CreateNode<FlowSubflowTestStartNode>();
        FlowSubflowTestNode first =
            CreatePassNode(firstNodeName);
        CVEndNode end = CreateNode<CVEndNode>();
        if (secondNodeName == null)
        {
            return new TestGraph(
                WriteCanvas(
                    [start, first, end],
                    Connect(start.m_op_start, first.Input),
                    Connect(first.Output, end.m_in_start)),
                start,
                first,
                null,
                end);
        }

        FlowSubflowTestNode second =
            CreatePassNode(secondNodeName);
        return new TestGraph(
            WriteCanvas(
                [start, first, second, end],
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
        string nodeName)
    {
        FlowSubflowTestNode node =
            CreateNode<FlowSubflowTestNode>();
        node.NodeName = nodeName;
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
            canvasOffsetY: 20,
            canvasScale: 1);
        return stream.ToArray();
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes))
            .ToLowerInvariant();
    }

    private sealed record TestGraph(
        byte[] CanvasData,
        FlowSubflowTestStartNode Start,
        FlowSubflowTestNode First,
        FlowSubflowTestNode? Second,
        CVEndNode End);

    private sealed class DictionaryArtifactResolver :
        IFlowArtifactDependencyResolver
    {
        private readonly Dictionary<string, FlowArtifactDependencyDefinition>
            definitions;

        public DictionaryArtifactResolver(
            params FlowArtifactDependencyDefinition[] definitions)
        {
            this.definitions = definitions.ToDictionary(
                item => item.FlowKey,
                StringComparer.OrdinalIgnoreCase);
        }

        public FlowArtifactDependencyDefinition? Resolve(
            FlowDefinitionReference reference)
        {
            return definitions.GetValueOrDefault(
                reference.FlowKey);
        }
    }
}

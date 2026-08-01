using ColorVision.Engine.FlowProcessing.Artifacts;
using ColorVision.Engine.FlowProcessing.Artifacts.Persistence;
using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Flow.Routing;
using ColorVision.Engine.Templates.Flow.Search;
using ColorVision.Engine.Templates.Flow.Versioning;
using FlowEngineLib.End;
using ST.Library.UI.NodeEditor;
using System.Globalization;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FlowArtifactRuntimeResolutionTests :
    IDisposable
{
    private readonly string rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "ColorVision.Tests",
        nameof(FlowArtifactRuntimeResolutionTests),
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_NoSharedReference_AllowsLegacy()
    {
        using TestContext context = CreateContext("legacy");
        TestGraph graph = CreateLinearGraph(
            "legacy-first",
            "legacy-second");
        var flowParam = new FlowParam
        {
            FlowKey = "flow:legacy",
            TemplateRevision = 77,
            TemplateContentHash = new string('e', 64),
            DataBase64 = Convert.ToBase64String(
                graph.CanvasData),
        };

        FlowRuntimeArtifactResolution result =
            context.Service.ResolveForExecution(flowParam);

        Assert.Equal(
            FlowRuntimeArtifactResolutionKind.Legacy,
            result.Kind);
        Assert.Null(result.Executable);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void Resolve_MatchingDraftWithoutSubflows_AllowsUiLegacy()
    {
        using TestContext context =
            CreateContext("draft-without-subflow");
        TestGraph graph = CreateLinearGraph(
            "draft-first",
            "draft-second");
        var flowParam = new FlowParam
        {
            FlowKey = "flow:draft-without-subflow",
            DataBase64 = Convert.ToBase64String(
                graph.CanvasData),
        };
        context.Service.SaveDraft(flowParam);

        FlowRuntimeArtifactResolution result =
            context.Service.ResolveForExecution(flowParam);

        Assert.Equal(
            FlowRuntimeArtifactResolutionKind.Legacy,
            result.Kind);
        Assert.NotNull(result.Head);
        Assert.Equal(
            FlowArtifactRevisionState.Draft,
            result.Head!.State);
        Assert.Null(result.Executable);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void Resolve_ChangedAuthoringStnAfterPlainPublish_AllowsUiLegacy()
    {
        using TestContext context =
            CreateContext("plain-published-authoring-change");
        TestGraph publishedGraph = CreateLinearGraph(
            "published-first",
            "published-second");
        var flowParam = new FlowParam
        {
            FlowKey = "flow:plain-published-authoring-change",
            DataBase64 = Convert.ToBase64String(
                publishedGraph.CanvasData),
        };
        context.Service.SavePublished(flowParam);
        TestGraph changedGraph = CreateLinearGraph(
            "changed-first",
            "changed-second");
        flowParam.DataBase64 = Convert.ToBase64String(
            changedGraph.CanvasData);

        FlowRuntimeArtifactResolution result =
            context.Service.ResolveForExecution(flowParam);

        Assert.Equal(
            FlowRuntimeArtifactResolutionKind
                .LegacyRequiresLocalSubflowCheck,
            result.Kind);
        Assert.NotNull(result.Head);
        Assert.Null(result.Executable);
        Assert.Null(result.FailureReason);
    }

    [Theory]
    [InlineData(
        FlowRuntimeArtifactResolutionKind.Legacy,
        true,
        false)]
    [InlineData(
        FlowRuntimeArtifactResolutionKind.Legacy,
        false,
        true)]
    [InlineData(
        FlowRuntimeArtifactResolutionKind.Legacy,
        null,
        true)]
    [InlineData(
        FlowRuntimeArtifactResolutionKind
            .LegacyRequiresLocalSubflowCheck,
        true,
        false)]
    [InlineData(
        FlowRuntimeArtifactResolutionKind
            .LegacyRequiresLocalSubflowCheck,
        false,
        true)]
    [InlineData(
        FlowRuntimeArtifactResolutionKind
            .LegacyRequiresLocalSubflowCheck,
        null,
        false)]
    public void LegacyFallbackRequiresCurrentSubflowProofWhenNeeded(
        FlowRuntimeArtifactResolutionKind kind,
        bool? currentRevisionHasSubflows,
        bool expected)
    {
        bool result =
            FlowRuntimeArtifactFallbackPolicy.CanUseLegacy(
                kind,
                currentRevisionHasSubflows,
                out string? failureReason);

        Assert.Equal(expected, result);
        Assert.Equal(expected, failureReason == null);
    }

    [Fact]
    public void LegacyFallback_MissingLocalRevisionEvidence_Blocks()
    {
        using TestContext context =
            CreateContext("missing-local-revision-evidence");
        bool? hasSubflows =
            FlowArtifactServiceProvider
                .GetAuthoringSubflowPresence(
                    context.Catalog,
                    context.Sidecars,
                    "flow:missing-local-revision-evidence",
                    7);

        bool allowed =
            FlowRuntimeArtifactFallbackPolicy.CanUseLegacy(
                FlowRuntimeArtifactResolutionKind
                    .LegacyRequiresLocalSubflowCheck,
                hasSubflows,
                out string? failureReason);

        Assert.Null(hasSubflows);
        Assert.False(allowed);
        Assert.Contains("无法验证", failureReason);
    }

    [Fact]
    public void Resolve_MatchingDraftWithSubflows_BlocksUntilPublished()
    {
        using TestContext context = CreateContext("draft-subflow");
        RootDefinition root =
            CreateRootWithChild(context, "flow:draft-root");
        FlowArtifactRevision draft =
            context.Service.SaveDraft(
                root.FlowParam,
                root.Sidecar);

        FlowRuntimeArtifactResolution result =
            context.Service.ResolveForExecution(root.FlowParam);

        Assert.Equal(
            FlowArtifactRevisionState.Draft,
            draft.State);
        Assert.Equal(
            FlowRuntimeArtifactResolutionKind.Blocked,
            result.Kind);
        Assert.Contains("子流程", result.FailureReason);
        Assert.Contains("发布", result.FailureReason);
        Assert.Null(result.Executable);
    }

    [Fact]
    public void Resolve_MatchingPublished_IgnoresLocalCatalogRevision()
    {
        using TestContext context =
            CreateContext("published-cross-machine");
        RootDefinition root =
            CreateRootWithChild(context, "flow:published-root");
        FlowArtifactRevision published =
            context.Service.SavePublished(
                root.FlowParam,
                root.Sidecar);
        root.FlowParam.TemplateRevision = null;
        root.FlowParam.TemplateContentHash = null;

        FlowRuntimeArtifactResolution withoutRevision =
            context.Service.ResolveForExecution(root.FlowParam);
        root.FlowParam.TemplateRevision = 99_999;
        root.FlowParam.TemplateContentHash =
            new string('f', 64);
        FlowRuntimeArtifactResolution differentRevision =
            context.Service.ResolveForExecution(root.FlowParam);

        Assert.Equal(
            FlowRuntimeArtifactResolutionKind.Published,
            withoutRevision.Kind);
        Assert.Equal(
            published.Revision,
            withoutRevision.Executable!.Revision.Revision);
        Assert.True(withoutRevision.Executable.HasSubflows);
        Assert.Equal(
            FlowRuntimeArtifactResolutionKind.Published,
            differentRevision.Kind);
        Assert.Equal(
            published.Revision,
            differentRevision.Executable!.Revision.Revision);
    }

    [Fact]
    public void Resolve_PublishedServiceGraph_ReportsServiceRequirement()
    {
        using TestContext context =
            CreateContext("published-service-graph");
        var flowParam = new FlowParam
        {
            FlowKey = "flow:published-service-graph",
            DataBase64 = Convert.ToBase64String(
                CreateServiceGraph()),
        };
        context.Service.SavePublished(flowParam);

        FlowRuntimeArtifactResolution result =
            context.Service.ResolveForExecution(flowParam);

        Assert.Equal(
            FlowRuntimeArtifactResolutionKind.Published,
            result.Kind);
        Assert.True(result.Executable!.RequiresServices);
    }

    [Fact]
    public void Resolve_PublishedSubflowServiceGraph_ReportsServiceRequirement()
    {
        using TestContext context =
            CreateContext("published-subflow-service-graph");
        RootDefinition root = CreateRootWithServiceChild(
            context,
            "flow:published-subflow-service-graph");
        context.Service.SavePublished(
            root.FlowParam,
            root.Sidecar);

        FlowRuntimeArtifactResolution result =
            context.Service.ResolveForExecution(root.FlowParam);

        Assert.Equal(
            FlowRuntimeArtifactResolutionKind.Published,
            result.Kind);
        Assert.True(result.Executable!.HasSubflows);
        Assert.True(result.Executable.RequiresServices);
    }

    [Fact]
    public void Resolve_SharedHeadSourceMismatch_FailsClosed()
    {
        using TestContext context =
            CreateContext("source-mismatch");
        RootDefinition root =
            CreateRootWithChild(context, "flow:mismatch-root");
        context.Service.SavePublished(
            root.FlowParam,
            root.Sidecar);
        TestGraph changed = CreateLinearGraph(
            "changed-first",
            "changed-second");
        root.FlowParam.DataBase64 =
            Convert.ToBase64String(changed.CanvasData);

        FlowRuntimeArtifactResolution result =
            context.Service.ResolveForExecution(root.FlowParam);

        Assert.Equal(
            FlowRuntimeArtifactResolutionKind.Blocked,
            result.Kind);
        Assert.Contains("源内容不一致", result.FailureReason);
        Assert.Null(result.Executable);
    }

    [Fact]
    public void Resolve_NewerMatchingDraft_BlocksOlderPublished()
    {
        using TestContext context =
            CreateContext("draft-over-published");
        RootDefinition root =
            CreateRootWithChild(context, "flow:draft-over-published");
        context.Service.SavePublished(
            root.FlowParam,
            root.Sidecar);
        FlowSubflowCall original =
            Assert.Single(root.Sidecar.Calls);
        var changedSidecar = new FlowSubflowSidecar(
        [
            original with
            {
                CallId = "renamed-call",
            },
        ]);
        FlowArtifactRevision draft =
            context.Service.SaveDraft(
                root.FlowParam,
                changedSidecar);

        FlowRuntimeArtifactResolution result =
            context.Service.ResolveForExecution(root.FlowParam);

        Assert.Equal(
            FlowArtifactRevisionState.Draft,
            draft.State);
        Assert.Equal(
            FlowRuntimeArtifactResolutionKind.Blocked,
            result.Kind);
        Assert.Equal(draft.Revision, result.Head!.Revision);
        Assert.Contains("发布", result.FailureReason);
    }

    [Fact]
    public void Build_NestedSemanticCallWithoutSidecar_Fails()
    {
        using TestContext context =
            CreateContext("missing-nested-sidecar");
        RootDefinition root = CreateNestedRoot(
            context,
            persistChildSidecar: false);

        FlowArtifactException exception =
            Assert.Throws<FlowArtifactException>(() =>
                context.Service.Build(
                    root.FlowParam,
                    root.Sidecar));

        Assert.Equal(
            FlowArtifactError.MissingDependency,
            exception.Error);
        InvalidOperationException cause =
            Assert.IsType<InvalidOperationException>(
                exception.InnerException);
        Assert.Contains("声明了嵌套调用", cause.Message);
        Assert.Contains("侧车不存在", cause.Message);
    }

    [Fact]
    public void Build_CorruptedNestedSidecar_Fails()
    {
        using TestContext context =
            CreateContext("corrupted-nested-sidecar");
        RootDefinition root = CreateNestedRoot(
            context,
            persistChildSidecar: true);
        string sidecarPath = Assert.Single(
            Directory.GetFiles(
                context.Sidecars.RootDirectory,
                "subflow.json",
                SearchOption.AllDirectories));
        File.WriteAllText(sidecarPath, "{ invalid");

        FlowArtifactException exception =
            Assert.Throws<FlowArtifactException>(() =>
                context.Service.Build(
                    root.FlowParam,
                    root.Sidecar));

        Assert.Equal(
            FlowArtifactError.MissingDependency,
            exception.Error);
        InvalidOperationException cause =
            Assert.IsType<InvalidOperationException>(
                exception.InnerException);
        Assert.Contains("损坏或无法读取", cause.Message);
        Assert.NotNull(cause.InnerException);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
            Directory.Delete(rootDirectory, recursive: true);
    }

    private TestContext CreateContext(string name)
    {
        string directory = Path.Combine(rootDirectory, name);
        return new TestContext(
            new FlowCatalogService(
                new InMemoryFlowRevisionStore(),
                new InMemoryFlowNodeSearchIndex()),
            new JsonFlowSubflowDefinitionStore(
                Path.Combine(directory, "sidecars")),
            new JsonFlowExecutionPolicyStore(
                Path.Combine(directory, "policies")),
            new InMemoryFlowArtifactStore());
    }

    private static RootDefinition CreateRootWithChild(
        TestContext context,
        string rootFlowKey)
    {
        TestGraph child = CreateLinearGraph(
            "child-node",
            secondNodeName: null);
        FlowRevision childRevision = Record(
            context.Catalog,
            "flow:child",
            child.CanvasData,
            FlowSubflowSidecar.Empty);
        TestGraph root = CreateLinearGraph(
            "root-before",
            "root-after");
        FlowSubflowSidecar rootSidecar = CreateCall(
            root,
            "root-to-child",
            childRevision);
        FlowRevision rootRevision = Record(
            context.Catalog,
            rootFlowKey,
            root.CanvasData,
            rootSidecar);
        context.Sidecars.Append(
            rootFlowKey,
            rootRevision.Revision,
            rootSidecar);
        return new RootDefinition(
            new FlowParam
            {
                Name = rootFlowKey,
                FlowKey = rootFlowKey,
                TemplateRevision = rootRevision.Revision,
                TemplateContentHash =
                    rootRevision.BinaryHash,
                DataBase64 = Convert.ToBase64String(
                    root.CanvasData),
            },
            rootSidecar);
    }

    private static RootDefinition CreateRootWithServiceChild(
        TestContext context,
        string rootFlowKey)
    {
        byte[] childCanvas = CreateServiceGraph();
        FlowRevision childRevision = Record(
            context.Catalog,
            "flow:service-child",
            childCanvas,
            FlowSubflowSidecar.Empty);
        TestGraph root = CreateLinearGraph(
            "root-before-service",
            "root-after-service");
        FlowSubflowSidecar rootSidecar = CreateCall(
            root,
            "root-to-service-child",
            childRevision);
        FlowRevision rootRevision = Record(
            context.Catalog,
            rootFlowKey,
            root.CanvasData,
            rootSidecar);
        context.Sidecars.Append(
            rootFlowKey,
            rootRevision.Revision,
            rootSidecar);
        return new RootDefinition(
            new FlowParam
            {
                Name = rootFlowKey,
                FlowKey = rootFlowKey,
                TemplateRevision = rootRevision.Revision,
                TemplateContentHash =
                    rootRevision.BinaryHash,
                DataBase64 = Convert.ToBase64String(
                    root.CanvasData),
            },
            rootSidecar);
    }

    private static RootDefinition CreateNestedRoot(
        TestContext context,
        bool persistChildSidecar)
    {
        TestGraph grandchild = CreateLinearGraph(
            "grandchild",
            secondNodeName: null);
        FlowRevision grandchildRevision = Record(
            context.Catalog,
            "flow:grandchild",
            grandchild.CanvasData,
            FlowSubflowSidecar.Empty);
        TestGraph child = CreateLinearGraph(
            "child-before",
            "child-after");
        FlowSubflowSidecar childSidecar = CreateCall(
            child,
            "child-to-grandchild",
            grandchildRevision);
        FlowRevision childRevision = Record(
            context.Catalog,
            "flow:nested-child",
            child.CanvasData,
            childSidecar);
        if (persistChildSidecar)
        {
            context.Sidecars.Append(
                childRevision.FlowKey,
                childRevision.Revision,
                childSidecar);
        }

        TestGraph root = CreateLinearGraph(
            "root-before",
            "root-after");
        FlowSubflowSidecar rootSidecar = CreateCall(
            root,
            "root-to-child",
            childRevision);
        FlowRevision rootRevision = Record(
            context.Catalog,
            "flow:nested-root",
            root.CanvasData,
            rootSidecar);
        return new RootDefinition(
            new FlowParam
            {
                Name = "nested-root",
                FlowKey = rootRevision.FlowKey,
                TemplateRevision = rootRevision.Revision,
                TemplateContentHash =
                    rootRevision.BinaryHash,
                DataBase64 = Convert.ToBase64String(
                    root.CanvasData),
            },
            rootSidecar);
    }

    private static FlowRevision Record(
        FlowCatalogService catalog,
        string flowKey,
        byte[] canvas,
        FlowSubflowSidecar sidecar)
    {
        FlowCanvasCatalogBuildResult projection =
            new FlowCanvasCatalogBuilder().Build(
                canvas,
                sidecar);
        return catalog.RecordEditorSave(
            flowKey,
            canvas,
            projection.SemanticDocument,
            projection.SearchDocuments);
    }

    private static FlowSubflowSidecar CreateCall(
        TestGraph parent,
        string callId,
        FlowRevision child)
    {
        if (parent.Second == null)
        {
            throw new InvalidOperationException(
                "父流程必须包含两个普通节点。");
        }
        return new FlowSubflowSidecar(
        [
            new FlowSubflowCall(
                callId,
                new FlowPortReference(
                    parent.First.Guid,
                    0),
                new FlowPortReference(
                    parent.Second.Guid,
                    0),
                new FlowDefinitionReference(
                    child.FlowKey,
                    child.Revision.ToString(
                        CultureInfo.InvariantCulture),
                    child.BinaryHash)),
        ]);
    }

    private static TestGraph CreateLinearGraph(
        string firstNodeName,
        string? secondNodeName)
    {
        FlowSubflowTestStartNode start =
            CreateNode<FlowSubflowTestStartNode>();
        FlowSubflowTestNode first =
            CreateNode<FlowSubflowTestNode>();
        first.NodeName = firstNodeName;
        FlowSubflowTestNode? second =
            secondNodeName == null
                ? null
                : CreateNode<FlowSubflowTestNode>();
        if (second != null)
            second.NodeName = secondNodeName;
        CVEndNode end = CreateNode<CVEndNode>();
        var connections = new List<ConnectionInfo>
        {
            new()
            {
                Output = start.m_op_start,
                Input = first.Input,
            },
        };
        if (second == null)
        {
            connections.Add(new ConnectionInfo
            {
                Output = first.Output,
                Input = end.m_in_start,
            });
        }
        else
        {
            connections.Add(new ConnectionInfo
            {
                Output = first.Output,
                Input = second.Input,
            });
            connections.Add(new ConnectionInfo
            {
                Output = second.Output,
                Input = end.m_in_start,
            });
        }

        using var stream = new MemoryStream();
        STNodeCanvasWriter.Write(
            stream,
            second == null
                ? [start, first, end]
                : [start, first, second, end],
            connections,
            canvasOffsetX: 0,
            canvasOffsetY: 0,
            canvasScale: 1);
        return new TestGraph(
            stream.ToArray(),
            first,
            second);
    }

    private static byte[] CreateServiceGraph()
    {
        HeadlessTestStartNode start =
            CreateNode<HeadlessTestStartNode>();
        HeadlessServiceProbeNode service =
            CreateNode<HeadlessServiceProbeNode>();
        CVEndNode end = CreateNode<CVEndNode>();
        using var stream = new MemoryStream();
        STNodeCanvasWriter.Write(
            stream,
            [start, service, end],
            [
                new ConnectionInfo
                {
                    Output = start.m_op_start,
                    Input = service.Input,
                },
                new ConnectionInfo
                {
                    Output = service.Output,
                    Input = end.m_in_start,
                },
            ],
            canvasOffsetX: 0,
            canvasOffsetY: 0,
            canvasScale: 1);
        return stream.ToArray();
    }

    private static T CreateNode<T>()
        where T : STNode, new()
    {
        var node = new T();
        node.Create();
        return node;
    }

    private sealed record TestGraph(
        byte[] CanvasData,
        FlowSubflowTestNode First,
        FlowSubflowTestNode? Second);

    private sealed record RootDefinition(
        FlowParam FlowParam,
        FlowSubflowSidecar Sidecar);

    private sealed class TestContext : IDisposable
    {
        public TestContext(
            FlowCatalogService catalog,
            JsonFlowSubflowDefinitionStore sidecars,
            JsonFlowExecutionPolicyStore policies,
            InMemoryFlowArtifactStore store)
        {
            Catalog = catalog;
            Sidecars = sidecars;
            Service = new FlowArtifactApplicationService(
                store,
                catalog,
                sidecars,
                policies);
        }

        public FlowCatalogService Catalog { get; }

        public JsonFlowSubflowDefinitionStore Sidecars { get; }

        public FlowArtifactApplicationService Service { get; }

        public void Dispose()
        {
            Service.Dispose();
        }
    }
}

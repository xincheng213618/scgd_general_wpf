using ColorVision.Engine.FlowProcessing.Artifacts;
using ColorVision.Engine.FlowProcessing.Artifacts.Persistence;
using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Flow.Routing;
using ColorVision.Engine.Templates.Flow.Search;
using ColorVision.Engine.Templates.Flow.Versioning;
using FlowEngineLib.End;
using ST.Library.UI.NodeEditor;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FlowArtifactApplicationServiceTests :
    IDisposable
{
    private readonly string rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "ColorVision.Tests",
        nameof(FlowArtifactApplicationServiceTests),
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SavePublishAndRead_ProducesValidatedExecutableSnapshot()
    {
        byte[] canvas = CreateCanvas();
        var revisionStore = new InMemoryFlowRevisionStore();
        var catalog = new FlowCatalogService(
            revisionStore,
            new InMemoryFlowNodeSearchIndex());
        FlowCanvasCatalogBuildResult projection =
            new FlowCanvasCatalogBuilder().Build(canvas);
        FlowRevision catalogRevision =
            catalog.RecordEditorSave(
                "flow:root",
                canvas,
                projection.SemanticDocument,
                projection.SearchDocuments);
        var sidecars = new JsonFlowSubflowDefinitionStore(
            Path.Combine(rootDirectory, "sidecars"));
        var policies = new JsonFlowExecutionPolicyStore(
            Path.Combine(rootDirectory, "policies"));
        var artifactStore = new InMemoryFlowArtifactStore();
        using var service =
            new FlowArtifactApplicationService(
                artifactStore,
                catalog,
                sidecars,
                policies);
        var flowParam = new FlowParam
        {
            Name = "root",
            FlowKey = "flow:root",
            TemplateRevision = catalogRevision.Revision,
            TemplateContentHash = catalogRevision.BinaryHash,
            DataBase64 = Convert.ToBase64String(canvas),
        };

        FlowArtifactRevision draft =
            service.SaveDraft(flowParam);
        FlowArtifactRevision repeated =
            service.SaveDraft(flowParam);
        Assert.Equal(draft.Revision, repeated.Revision);
        Assert.Equal(
            FlowArtifactRevisionState.Draft,
            draft.State);
        Assert.Equal(7, draft.Artifacts.Count);

        FlowArtifactRevision published =
            service.PublishHead("flow:root");
        Assert.Equal(
            FlowArtifactRevisionState.Published,
            published.State);
        FlowPublishedExecutable executable =
            service.GetPublishedExecutable("flow:root");

        Assert.Equal(canvas, executable.CompiledStn);
        Assert.Equal("flow:root", executable.Manifest.FlowKey);
        Assert.Equal(
            catalogRevision.Revision.ToString(),
            executable.Manifest.Revision);
        Assert.Empty(executable.ExecutionPolicy.ErrorRoutes);
        Assert.Empty(executable.ExecutionPolicy.RetryPolicies);
        Assert.NotEmpty(executable.CompilationMap);
        Assert.True(
            executable.IsCompatibleWith(
                flowParam,
                out string compatibleFailure),
            compatibleFailure);
        Assert.Equal(
            published.Revision,
            service.GetCompatiblePublishedExecutable(
                flowParam).Revision.Revision);

        flowParam.TemplateRevision =
            catalogRevision.Revision + 1;
        Assert.False(
            executable.IsCompatibleWith(
                flowParam,
                out string revisionFailure));
        Assert.Contains("不是当前流程版本", revisionFailure);
        Assert.Throws<InvalidOperationException>(
            () => service.GetCompatiblePublishedExecutable(
                flowParam));
        flowParam.TemplateRevision =
            catalogRevision.Revision;

        flowParam.DataBase64 =
            Convert.ToBase64String([1, 2, 3]);
        Assert.False(
            executable.IsCompatibleWith(
                flowParam,
                out string sourceFailure));
        Assert.Contains("源 STN", sourceFailure);
    }

    [Fact]
    public void SidecarInspection_IsBoundToExactCatalogRevision()
    {
        var catalog = new FlowCatalogService(
            new InMemoryFlowRevisionStore(),
            new InMemoryFlowNodeSearchIndex());
        var sidecars = new JsonFlowSubflowDefinitionStore(
            Path.Combine(rootDirectory, "sidecars-inspect"));
        var policies = new JsonFlowExecutionPolicyStore(
            Path.Combine(rootDirectory, "policies-inspect"));
        var call = new FlowSubflowCall(
            "call-1",
            new FlowPortReference(Guid.NewGuid(), 0),
            new FlowPortReference(Guid.NewGuid(), 0),
            new FlowDefinitionReference(
                "flow:child",
                "2",
                new string('a', 64)));
        sidecars.Append(
            "flow:root",
            3,
            new FlowSubflowSidecar([call]));
        using var service =
            new FlowArtifactApplicationService(
                new InMemoryFlowArtifactStore(),
                catalog,
                sidecars,
                policies);

        Assert.True(
            service.HasAuthoringSubflows(
                "flow:root",
                3));
        Assert.False(
            service.HasAuthoringSubflows(
                "flow:root",
                4));
        Assert.Equal(
            "call-1",
            Assert.Single(
                service.GetAuthoringSidecar(
                    "flow:root",
                    3).Calls).CallId);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
            Directory.Delete(rootDirectory, recursive: true);
    }

    private static byte[] CreateCanvas()
    {
        FlowSubflowTestStartNode start =
            CreateNode<FlowSubflowTestStartNode>();
        FlowSubflowTestNode node =
            CreateNode<FlowSubflowTestNode>();
        node.NodeName = "root-node";
        CVEndNode end = CreateNode<CVEndNode>();
        using var stream = new MemoryStream();
        STNodeCanvasWriter.Write(
            stream,
            [start, node, end],
            [
                new ConnectionInfo
                {
                    Output = start.m_op_start,
                    Input = node.Input,
                },
                new ConnectionInfo
                {
                    Output = node.Output,
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
}

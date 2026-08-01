using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Templates.Flow.Routing;
using ColorVision.Engine.Templates.Flow.Versioning;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlowFailureKind = FlowEngineLib.Runtime.FlowFailureKind;

namespace ColorVision.UI.Tests;

public sealed class FlowCanvasCatalogBuilderTests
{
    [Fact]
    public void BuildSeparatesLayoutHashesPropertiesAndSearchAllowlist()
    {
        TestCanvas canvas = CreateCanvas();
        FlowCanvasCatalogBuildResult before =
            new FlowCanvasCatalogBuilder().Build(canvas.Data);

        FlowSemanticNode semanticNode = before.SemanticDocument.Nodes
            .Single(node => node.NodeId == canvas.Source.Guid.ToString("D"));
        Assert.Equal(
            Hash("Visible Node"),
            semanticNode.Properties["NodeName"]);
        Assert.Equal(
            Hash("DEV.Camera01"),
            semanticNode.Properties["DeviceCode"]);
        Assert.DoesNotContain("Guid", semanticNode.Properties.Keys);
        Assert.DoesNotContain("Left", semanticNode.Properties.Keys);
        Assert.DoesNotContain("Top", semanticNode.Properties.Keys);
        Assert.DoesNotContain("Width", semanticNode.Properties.Keys);
        Assert.DoesNotContain("Height", semanticNode.Properties.Keys);
        Assert.DoesNotContain("Password", semanticNode.Properties.Keys);
        Assert.DoesNotContain("ApiToken", semanticNode.Properties.Keys);
        Assert.DoesNotContain("Payload", semanticNode.Properties.Keys);
        Assert.All(
            semanticNode.Properties.Values,
            value => Assert.Matches("^[0-9a-f]{64}$", value!));

        FlowNodeLayout layout = before.SemanticDocument.Layout.Nodes
            .Single(node => node.NodeId == semanticNode.NodeId);
        Assert.Equal(15, layout.X);
        Assert.Equal(25, layout.Y);
        Assert.Equal(canvas.Source.Width, layout.Width);
        Assert.Equal(canvas.Source.Height, layout.Height);
        Assert.Equal(12, before.SemanticDocument.Layout.ViewportX);
        Assert.Equal(34, before.SemanticDocument.Layout.ViewportY);
        Assert.Equal(1.25, before.SemanticDocument.Layout.Scale);

        FlowSemanticEdge edge = Assert.Single(
            before.SemanticDocument.Edges);
        Assert.Equal("0", edge.SourcePort);
        Assert.Equal("0", edge.TargetPort);

        var search = before.SearchDocuments
            .Single(node => node.SourceNodeGuid == canvas.Source.Guid);
        Assert.Equal("Visible Node", search.DisplayName);
        Assert.Equal("Visible Title", search.Title);
        Assert.Equal("Visible Template", search.TemplateName);
        Assert.Equal("DEV.Camera01", search.DeviceCode);
        Assert.Equal("SVR.Camera", search.ServiceCode);
        Assert.Equal(
            $"root/nodes/{canvas.Source.Guid:N}",
            search.NodePath);
        Assert.Null(before.SearchDocuments
            .Single(node => node.SourceNodeGuid == canvas.Target.Guid)
            .TemplateName);

        string semanticJson = JsonSerializer.Serialize(
            before.SemanticDocument);
        string searchJson = JsonSerializer.Serialize(
            before.SearchDocuments);
        Assert.DoesNotContain("super-secret-password", semanticJson);
        Assert.DoesNotContain("raw-token-value", semanticJson);
        Assert.DoesNotContain("mqtt-private-payload", semanticJson);
        Assert.DoesNotContain("super-secret-password", searchJson);
        Assert.DoesNotContain("raw-token-value", searchJson);
        Assert.DoesNotContain("mqtt-private-payload", searchJson);
        Assert.DoesNotContain("unsafe-template-secret", searchJson);

        canvas.Source.Left = 315;
        canvas.Source.Top = 425;
        FlowCanvasCatalogBuildResult after =
            new FlowCanvasCatalogBuilder().Build(
                WriteCanvas(canvas.Source, canvas.Target));
        Assert.Equal(
            FlowSemanticHash.ComputeSemanticHash(
                before.SemanticDocument),
            FlowSemanticHash.ComputeSemanticHash(
                after.SemanticDocument));
        Assert.NotEqual(
            FlowSemanticHash.ComputeLayoutHash(
                before.SemanticDocument),
            FlowSemanticHash.ComputeLayoutHash(
                after.SemanticDocument));
    }

    [Fact]
    public void BuildProjectsNumericSubflowRevisionAndRejectsStringRevision()
    {
        TestCanvas canvas = CreateCanvas();
        FlowSubflowCall call = CreateSubflowCall(
            canvas,
            revision: "17");

        FlowCanvasCatalogBuildResult result =
            new FlowCanvasCatalogBuilder().Build(
                canvas.Data,
                new FlowSubflowSidecar([call]));

        FlowSubflowReference subflow = Assert.Single(
            result.SemanticDocument.Subflows);
        Assert.Equal("child-call", subflow.CallNodeId);
        Assert.Equal("flow:child", subflow.FlowKey);
        Assert.Equal("PinnedRevision", subflow.Binding);
        Assert.Equal(17, subflow.Revision);
        Assert.Equal(
            $"{canvas.Source.Guid:N}/outputs/0",
            subflow.InputMappings["parentSource"]);
        Assert.Equal(
            $"{canvas.Target.Guid:N}/inputs/0",
            subflow.OutputMappings["parentTarget"]);

        FlowCanvasCatalogException exception =
            Assert.Throws<FlowCanvasCatalogException>(() =>
                new FlowCanvasCatalogBuilder().Build(
                    canvas.Data,
                    new FlowSubflowSidecar(
                    [
                        CreateSubflowCall(
                            canvas,
                            revision: "fixed-r17"),
                    ])));
        Assert.Equal(
            FlowCanvasCatalogError.UnsupportedSubflowRevision,
            exception.Error);
    }

    [Fact]
    public void BuildProjectsErrorPortsAndRetryPoliciesIntoVersionSemantics()
    {
        TestCanvas canvas = CreateCanvas();
        using TempDirectory directory = new();
        var store = new JsonFlowExecutionPolicyStore(directory.Path);
        FlowExecutionPolicySnapshot firstPolicy = store.Save(
            CreatePolicyRequest(
                canvas,
                expectedRevision: 0,
                maxAttempts: 3));

        FlowSemanticDocument first =
            new FlowCanvasCatalogBuilder().Build(
                canvas.Data,
                executionPolicy: firstPolicy)
            .SemanticDocument;

        Assert.Collection(
            first.ErrorRoutes.OrderBy(route => route.ErrorCode),
            route =>
            {
                Assert.Equal("Business", route.ErrorCode);
                Assert.Equal("in:0", route.TargetPort);
                Assert.Equal(
                    canvas.Source.Guid.ToString("D"),
                    route.SourceNodeId);
                Assert.Equal(
                    canvas.Target.Guid.ToString("D"),
                    route.TargetNodeId);
            },
            route =>
            {
                Assert.Equal("Technical", route.ErrorCode);
                Assert.Equal("in:0", route.TargetPort);
            });
        FlowRetryPolicyReference retry = Assert.Single(
            first.RetryPolicies);
        Assert.Equal(canvas.Source.Guid.ToString("D"), retry.NodeId);
        Assert.Equal(3, retry.MaxAttempts);
        Assert.Equal(100, retry.InitialDelayMs);
        Assert.Equal(2, retry.Backoff);
        Assert.Equal(2_000, retry.MaxDelayMs);
        Assert.Equal(
            ["Technical", "Timeout"],
            retry.RetryableKinds);

        FlowExecutionPolicySnapshot secondPolicy = store.Save(
            CreatePolicyRequest(
                canvas,
                expectedRevision: firstPolicy.Revision,
                maxAttempts: 5));
        FlowSemanticDocument second =
            new FlowCanvasCatalogBuilder().Build(
                canvas.Data,
                executionPolicy: secondPolicy)
            .SemanticDocument;
        Assert.NotEqual(
            FlowSemanticHash.ComputeSemanticHash(first),
            FlowSemanticHash.ComputeSemanticHash(second));
        FlowSemanticDiffResult diff =
            FlowSemanticDiff.Compare(first, second);
        Assert.Single(diff.RemovedRetryPolicies);
        Assert.Single(diff.AddedRetryPolicies);
    }

    [Fact]
    public void BuildRejectsPolicyReferencesOutsideCanvas()
    {
        TestCanvas canvas = CreateCanvas();
        using TempDirectory directory = new();
        var store = new JsonFlowExecutionPolicyStore(directory.Path);
        FlowExecutionPolicySnapshot policy = store.Save(
            new FlowExecutionPolicySaveRequest(
                "flow:catalog",
                expectedRevision: 0,
                errorRoutes:
                [
                    new FlowErrorRoutePolicy(
                        Guid.NewGuid().ToString("D"),
                        canvas.Target.Guid.ToString("D"),
                        targetInputIndex: 0,
                        [FlowFailureKind.Technical]),
                ]));

        FlowCanvasCatalogException exception =
            Assert.Throws<FlowCanvasCatalogException>(() =>
                new FlowCanvasCatalogBuilder().Build(
                    canvas.Data,
                    executionPolicy: policy));
        Assert.Equal(
            FlowCanvasCatalogError.InvalidExecutionPolicy,
            exception.Error);
    }

    private static FlowExecutionPolicySaveRequest CreatePolicyRequest(
        TestCanvas canvas,
        long expectedRevision,
        int maxAttempts)
    {
        return new FlowExecutionPolicySaveRequest(
            "flow:catalog",
            expectedRevision,
            errorRoutes:
            [
                new FlowErrorRoutePolicy(
                    canvas.Source.Guid.ToString("D"),
                    canvas.Target.Guid.ToString("D"),
                    targetInputIndex: 0,
                    [
                        FlowFailureKind.Technical,
                        FlowFailureKind.Business,
                    ]),
            ],
            retryPolicies:
            [
                new FlowRetryPolicy(
                    canvas.Source.Guid.ToString("D"),
                    maxAttempts,
                    initialDelayMs: 100,
                    backoff: 2,
                    maxDelayMs: 2_000,
                    retryableKinds:
                    [
                        FlowFailureKind.Timeout,
                        FlowFailureKind.Technical,
                    ]),
            ]);
    }

    private static FlowSubflowCall CreateSubflowCall(
        TestCanvas canvas,
        string revision)
    {
        return new FlowSubflowCall(
            "child-call",
            new FlowPortReference(
                canvas.Source.Guid,
                OptionIndex: 0),
            new FlowPortReference(
                canvas.Target.Guid,
                OptionIndex: 0),
            new FlowDefinitionReference(
                "flow:child",
                revision));
    }

    private static TestCanvas CreateCanvas()
    {
        FlowCanvasCatalogTestNode source = CreateNode();
        source.Left = 15;
        source.Top = 25;
        source.Title = "Visible Title";
        source.NodeName = "Visible Node";
        source.DeviceCode = "DEV.Camera01";
        source.TemplateName = "Visible Template";
        source.ServiceCode = "SVR.Camera";
        source.Password = "super-secret-password";
        source.ApiToken = "raw-token-value";
        source.Payload = "mqtt-private-payload";

        FlowCanvasCatalogTestNode target = CreateNode();
        target.Left = 215;
        target.Top = 25;
        target.NodeName = "Target Node";
        target.TemplateName =
            """{"secret":"unsafe-template-secret"}""";
        return new TestCanvas(
            WriteCanvas(source, target),
            source,
            target);
    }

    private static FlowCanvasCatalogTestNode CreateNode()
    {
        var node = new FlowCanvasCatalogTestNode();
        node.Create();
        return node;
    }

    private static byte[] WriteCanvas(
        FlowCanvasCatalogTestNode source,
        FlowCanvasCatalogTestNode target)
    {
        using var stream = new MemoryStream();
        STNodeCanvasWriter.Write(
            stream,
            new STNode[]
            {
                source,
                target,
            },
            new[]
            {
                new ConnectionInfo
                {
                    Output = source.Output,
                    Input = target.Input,
                },
            },
            canvasOffsetX: 12,
            canvasOffsetY: 34,
            canvasScale: 1.25f);
        return stream.ToArray();
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }

    private sealed record TestCanvas(
        byte[] Data,
        FlowCanvasCatalogTestNode Source,
        FlowCanvasCatalogTestNode Target);

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"colorvision-flow-catalog-{Guid.NewGuid():N}");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}

public sealed class FlowCanvasCatalogTestNode : CVCommonNode
{
    public FlowCanvasCatalogTestNode()
        : base(
            "FlowCanvasCatalogTest",
            "FlowCanvasCatalogTest",
            "Node",
            "DEV.Test")
    {
    }

    public STNodeOption Input { get; private set; } = null!;

    public STNodeOption Output { get; private set; } = null!;

    [STNodeProperty("TemplateName", "TemplateName")]
    public string TemplateName { get; set; } = string.Empty;

    [STNodeProperty("ServiceCode", "ServiceCode")]
    public string ServiceCode { get; set; } = string.Empty;

    [STNodeProperty("Password", "Password")]
    public string Password { get; set; } = string.Empty;

    [STNodeProperty("ApiToken", "ApiToken")]
    public string ApiToken { get; set; } = string.Empty;

    [STNodeProperty("Payload", "Payload")]
    public string Payload { get; set; } = string.Empty;

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

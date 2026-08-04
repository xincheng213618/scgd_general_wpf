using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Templates.Flow.Versioning;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

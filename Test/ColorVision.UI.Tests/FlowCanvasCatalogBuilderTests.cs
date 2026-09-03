using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Templates.Flow.Versioning;
using FlowEngineLib.Base;
using FlowEngineLib.Logical;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class FlowCanvasCatalogBuilderTests
{
    [Fact]
    public void BuildIndexesNewLogicalAndAfterConnectingMultipleInputs()
    {
        StaTest.Run(() =>
        {
            using var editor = new STNodeEditor();
            var hub = new LogicalANDNode();
            hub.Create();
            FlowCanvasCatalogTestNode first = CreateNode();
            FlowCanvasCatalogTestNode second = CreateNode();
            FlowCanvasCatalogTestNode target = CreateNode();
            STNode[] nodes = [hub, target, first, second];
            editor.Nodes.AddRange(nodes);
            Assert.Equal(ConnectionStatus.Connected, first.Output.ConnectOption(hub.GetAllInputOptions()[0]));
            Assert.Equal(ConnectionStatus.Connected, second.Output.ConnectOption(hub.GetAllInputOptions()[1]));
            Assert.Equal(ConnectionStatus.Connected, hub.GetAllOutputOptions()[0].ConnectOption(target.Input));
            Assert.Equal(3, hub.InputOptionsCount);

            ConnectionInfo[] connections = STNodeCanvasWriter.GetConnections(nodes);
            byte[] data = WriteCanvas(nodes, connections);
            AssertCatalogMatchesConnections(data, nodes, connections);
            Assert.Equal(data, WriteCanvas(nodes, connections));
        });
    }

    [Theory]
    [InlineData(typeof(LogicalANDNode))]
    [InlineData(typeof(InputMergeNode))]
    [InlineData(typeof(STNodeInHub))]
    [InlineData(typeof(STNodeOutHub))]
    [InlineData(typeof(STNodeHub))]
    public void BuildRestoresEachHubInstanceCountWithoutChangingCachedDefaults(Type hubType)
    {
        FlowCanvasCatalogTestNode source = CreateNode();
        FlowCanvasCatalogTestNode target = CreateNode();
        var nodes = new List<STNode>();
        var connections = new List<ConnectionInfo>();
        foreach (int count in new[] { 3, 5, 1 })
        {
            var hub = (STNode)Activator.CreateInstance(hubType)!;
            hub.Create();
            hub.OnLoadNode(new Dictionary<string, byte[]> { ["count"] = BitConverter.GetBytes(count) });
            nodes.Add(hub);
            STNodeOption[] inputs = hub.GetAllInputOptions();
            STNodeOption[] outputs = hub.GetAllOutputOptions();
            if (inputs.Length > 0)
                connections.Add(new ConnectionInfo { Output = source.Output, Input = inputs[^1] });
            if (outputs.Length > 0)
                connections.Add(new ConnectionInfo { Output = outputs[^1], Input = target.Input });
        }
        nodes.Add(source);
        nodes.Add(target);

        byte[] data = WriteCanvas(nodes, connections);
        AssertCatalogMatchesConnections(data, nodes, connections);
        NeutralCanvas decoded = StnV1NeutralCodec.Decode(data, new StnV1CodecOptions());
        for (int i = 0; i < nodes.Count; i++)
        {
            Assert.Equal(nodes[i].InputOptionsCount, decoded.Nodes[i].Inputs.Length);
            Assert.Equal(nodes[i].OutputOptionsCount, decoded.Nodes[i].Outputs.Length);
        }
        AssertCatalogMatchesConnections(
            StnV1NeutralCodec.Encode(decoded, new StnV1CodecOptions()),
            nodes,
            connections);
    }

    [Fact]
    public void BuildRestoresHubSchemaWithoutLoadingPropertiesOrConnectingOptions()
    {
        var hub = new FlowCanvasCatalogGuardHub();
        hub.Create();
        byte[] data = WriteCanvas([hub], []);

        NeutralCanvas decoded = StnV1NeutralCodec.Decode(data, new StnV1CodecOptions());

        Assert.Equal(3, Assert.Single(decoded.Nodes).Inputs.Length);
        Assert.Single(new FlowCanvasCatalogBuilder().Build(data).SearchDocuments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("030000")]
    [InlineData("00000000")]
    [InlineData("FFFFFFFF")]
    [InlineData("41420F00")]
    [InlineData("FFFFFF7F")]
    public void BuildRejectsMalformedOrExcessiveDynamicPortCounts(string? countHex)
    {
        var hub = new FlowCanvasCatalogGuardHub
        {
            SerializedCount = countHex == null ? null : Convert.FromHexString(countHex),
        };
        hub.Create();
        byte[] data = WriteCanvas([hub], []);

        FlowCompilationException exception = Assert.Throws<FlowCompilationException>(
            () => new FlowCanvasCatalogBuilder().Build(data));

        Assert.Equal(FlowCompilationError.InvalidCanvas, exception.Error);
    }

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

    private static byte[] WriteCanvas(IEnumerable<STNode> nodes, IEnumerable<ConnectionInfo> connections)
    {
        using var stream = new MemoryStream();
        STNodeCanvasWriter.Write(stream, nodes, connections, 0, 0, 1);
        return stream.ToArray();
    }

    private static void AssertCatalogMatchesConnections(
        byte[] data,
        IReadOnlyCollection<STNode> nodes,
        IEnumerable<ConnectionInfo> connections)
    {
        FlowCanvasCatalogBuildResult result = new FlowCanvasCatalogBuilder().Build(data);
        Assert.Equal(nodes.Count, result.SemanticDocument.Nodes.Count);
        Assert.Equal(nodes.Count, result.SearchDocuments.Count);
        string[] expected = connections.Select(connection =>
            $"{connection.Output.Owner.Guid:D}:{Array.IndexOf(connection.Output.Owner.GetAllOutputOptions(), connection.Output)}" +
            $"->{connection.Input.Owner.Guid:D}:{Array.IndexOf(connection.Input.Owner.GetAllInputOptions(), connection.Input)}")
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] actual = result.SemanticDocument.Edges.Select(edge =>
            $"{edge.SourceNodeId}:{edge.SourcePort}->{edge.TargetNodeId}:{edge.TargetPort}")
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actual);
    }

    private sealed record TestCanvas(
        byte[] Data,
        FlowCanvasCatalogTestNode Source,
        FlowCanvasCatalogTestNode Target);

}

public sealed class FlowCanvasCatalogGuardHub : STNodeInHub
{
    public byte[]? SerializedCount { get; set; } = BitConverter.GetBytes(3);

    protected override void OnSaveNode(Dictionary<string, byte[]> properties)
    {
        base.OnSaveNode(properties);
        if (SerializedCount == null)
            properties.Remove("count");
        else
            properties["count"] = SerializedCount;
    }

    public override void OnLoadNode(Dictionary<string, byte[]> properties)
        => throw new InvalidOperationException("Catalog indexing must not load node properties.");

    protected override void DoInputConnected(STNodeOption sender, STNodeOptionEventArgs e)
        => throw new InvalidOperationException("Catalog indexing must not connect live options.");
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

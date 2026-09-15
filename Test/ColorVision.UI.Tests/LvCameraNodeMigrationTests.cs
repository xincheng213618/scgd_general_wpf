using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Templates.Flow;
using FlowEngineLib;
using FlowEngineLib.Algorithm;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class LvCameraNodeMigrationTests
{
    // Saved by the actual FlowEngineLib.LVCameraNode before its move (7aeaea04e).
    // Two connected nodes, with non-default camera, template and position values.
    private const string LegacyCanvas = "U1RORAEfiwgAAAAAAAAKxZI7bxNBFIVPAgQUXiG/IEVKxszu2ut1BcGOIZKTQDDh1TCze9cZsd6x7LUJiALRRgKJFopI0NIhQPyg0NCmBt2NTeyIAiEhitVoz33MvWc+YGEJ+ff88jSAF9PAxXpiHy+nLZNSw+hClCTPJpXGZlW1qavWbESLMgqKbqh9EUpdEZ7ytKj4ZVeQlDp2Ytfzi85xANf6JpoDsHtz54e8W6h/ePXw02Bnfp9jDYozPm8AOAagaTv8GwE4AeCOibItFnYBzAC4Tqa1lVfcBnAKwFI/s7fMU5oCMDXLLW34aL2TGZuyhDNDqWFD9UvMCwetqu2nGQtePqcyKTvhlE8CWN7uNE2bWCi63KSqEtOkdmdNtWkeQKgSo7t5T5FQS4VPuGs9MZ1VG+Xj3DvNm62vjKp4uo41w+wLB8G6STLqjlLOAohzZTJrgwamR+NZ3VwZZnHnGg1MSFUbEdfUljcLB29VaOQp53gFm2Ym7dN6WlcmYYPrKukRr7uqtnld9qEkpeRV+JH5tvMADq4ReiBi0+1lM8PwSm2RB44DXXKUFKXAk6LouyRUFMTCl9ov+4F23LjEFfdX0oi22VHh8OVNkyW5KY1LVze/vfmy9/brxj+nUOwtzL7eennl87v9B98/xrvjFL6f+q8UOhMUyjEKeSjH9Y5iOGoygo7r12xKR7gDgN/ShsPAJGAcmGSKiR9jqkax6ifZ30PFT3EIVY9Cm0aTVEnlSvLdSPhBxRVFGZDQZeWJuFTSFOqi7yv5h1SxuZzArv8EoqkQGPgEAAA=";

    [Theory]
    [InlineData(null)]
    [InlineData("FlowEngineLib.dll|FlowEngineLib.LVCameraNode")]
    [InlineData("FlowEngineLib.dll|LVCameraNode")]
    [InlineData("OlderEngine.dll|Older.Namespace.LVCameraNode")]
    public void LegacyCanvasLoadsAndRoundTripsThroughEditorRuntimeAndCompiler(string? legacyModel) => StaTest.Run(() =>
    {
        Assert.Same(typeof(DeviceCamera).Assembly, typeof(LVCameraNode).Assembly);
        Assert.Null(typeof(CVCameraNode).Assembly.GetType("FlowEngineLib.LVCameraNode"));
        byte[] canvas = ReadLegacyCanvas(legacyModel);
        byte[] original = canvas.ToArray();
        using var container = new CVNodeContainer();
        container.LoadCanvas(canvas);
        AssertGraph(container.Nodes.Cast<STNode>().ToArray());

        using var editor = new STNodeEditor();
        editor.LoadCanvas(canvas);
        AssertGraph(editor.Nodes.Cast<STNode>().ToArray());

        NeutralCanvas decoded = StnV1NeutralCodec.Decode(canvas, new());
        Assert.Equal(2, decoded.Nodes.Count);
        Assert.Single(decoded.Connections);
        Assert.All(decoded.Nodes, node => Assert.Equal(typeof(LVCameraNode), node.Schema.NodeType));
        byte[] compiled = StnV1NeutralCodec.Encode(decoded, new());
        container.LoadCanvas(compiled);
        AssertGraph(container.Nodes.Cast<STNode>().ToArray());

        byte[] saved = container.GetCanvasData();
        NeutralCanvas resaved = StnV1NeutralCodec.Decode(saved, new());
        Assert.All(resaved.Nodes, node => Assert.Equal(
            "ColorVision.Engine.dll|ColorVision.Engine.FlowProcessing.Nodes.LVCameraNode", node.ModelKey));
        Assert.Equal(decoded.Nodes.Select(node => node.NodeId), resaved.Nodes.Select(node => node.NodeId));
        container.LoadCanvas(saved);
        AssertGraph(container.Nodes.Cast<STNode>().ToArray());
        Assert.Equal(original, canvas);
    });

    [Fact]
    public void UnknownNodeNameStillFailsToLoad() => StaTest.Run(() =>
    {
        byte[] canvas = ReadLegacyCanvas("Unknown.dll|MissingCameraNode");
        using var container = new CVNodeContainer();
        Assert.ThrowsAny<Exception>(() => container.LoadCanvas(canvas));
        Assert.Equal(FlowCompilationError.UnknownNodeType,
            Assert.Throws<FlowCompilationException>(() => StnV1NeutralCodec.Decode(canvas, new())).Error);
    });

    private static void AssertGraph(STNode[] nodes)
    {
        Assert.Equal(2, nodes.Length);
        var first = Assert.IsType<LVCameraNode>(nodes[0]);
        var second = Assert.IsType<LVCameraNode>(nodes[1]);
        Assert.Equal("legacy-bv-first", first.NodeName);
        Assert.Equal("DEV.Camera.Legacy", first.DeviceCode);
        Assert.Equal(42, first.ExpTime);
        Assert.Equal(17, first.Gain);
        Assert.Equal(3, first.AvgCount);
        Assert.Equal("calibration-legacy", first.CaliTempName);
        Assert.Equal("poi-legacy", first.POITempName);
        Assert.Equal("filter-legacy", first.POIFilterTempName);
        Assert.Equal("revise-legacy", first.POIReviseTempName);
        Assert.Equal(CVImageFlipMode.Y, first.FlipMode);
        Assert.Equal(80, first.Left);
        Assert.Equal(100, first.Top);
        Assert.Equal("legacy-bv-second", second.NodeName);
        Assert.Equal(123, second.ExpTime);
        Assert.Equal(420, second.Left);
        Assert.Same(second.GetAllInputOptions()[0], Assert.Single(first.GetAllOutputOptions()[0].ConnectedOption));
    }

    [Fact]
    public void NameFallbackRequiresOneTypeAndPrefersTheFullName() => StaTest.Run(() =>
    {
        string unknownGuid = "00000000-0000-0000-0000-000000000001";
        Assert.True(STNodeTypeRegistry.TryGetNodeType(unknownGuid,
            "Old.dll|" + typeof(First.DuplicatedNode).FullName, out Type resolved));
        Assert.Equal(typeof(First.DuplicatedNode), resolved);
        Assert.False(STNodeTypeRegistry.TryGetNodeType(unknownGuid, "Old.dll|DuplicatedNode", out _));
        byte[] canvas = ReadLegacyCanvas("Old.dll|Old.Namespace.DuplicatedNode");
        using var container = new CVNodeContainer();
        Assert.ThrowsAny<Exception>(() => container.LoadCanvas(canvas));
        Assert.Equal(FlowCompilationError.UnknownNodeType,
            Assert.Throws<FlowCompilationException>(() => StnV1NeutralCodec.Decode(canvas, new())).Error);
    });

    public static class First { public sealed class DuplicatedNode : STNode { } }
    public static class Second { public sealed class DuplicatedNode : STNode { } }

    private static byte[] ReadLegacyCanvas(string? model)
    {
        byte[] canvas = Convert.FromBase64String(LegacyCanvas);
        if (model == null) return canvas;
        using var reader = new BinaryReader(new MemoryStream(FlowPackageStnValidator.ValidateAndDecompress(canvas)));
        float x = reader.ReadSingle(), y = reader.ReadSingle(), scale = reader.ReadSingle();
        int count = reader.ReadInt32();
        var payloads = new List<byte[]>();
        for (int i = 0; i < count; i++)
        {
            byte[] payload = reader.ReadBytes(reader.ReadInt32());
            int typeOffset = 1 + payload[0];
            int propertyOffset = typeOffset + 1 + payload[typeOffset];
            using var node = new MemoryStream();
            byte[] modelBytes = Encoding.UTF8.GetBytes(model);
            byte[] unknownType = Encoding.UTF8.GetBytes("00000000-0000-0000-0000-000000000001");
            node.WriteByte((byte)modelBytes.Length); node.Write(modelBytes);
            node.WriteByte((byte)unknownType.Length); node.Write(unknownType);
            node.Write(payload.AsSpan(propertyOffset));
            payloads.Add(node.ToArray());
        }
        int connectionCount = reader.ReadInt32();
        long[] connections = Enumerable.Range(0, connectionCount).Select(_ => reader.ReadInt64()).ToArray();
        using var output = new MemoryStream();
        STNodeCanvasWriter.WriteRaw(output, payloads, connections, x, y, scale);
        return output.ToArray();
    }
}

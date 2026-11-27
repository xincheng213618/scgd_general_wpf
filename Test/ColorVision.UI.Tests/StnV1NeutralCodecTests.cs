using ColorVision.Engine.FlowProcessing.Compilation;
using ST.Library.UI.NodeEditor;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class StnV1NeutralCodecTests
{
    [Fact]
    public void DecodedNodeOwnsPayloadAcrossSubsequentDecodes()
    {
        byte[] firstPayload = CreatePayload(0x2c);
        byte[] secondPayload = CreatePayload(0x86);
        var options = new StnV1CodecOptions();

        var first = StnV1NeutralCodec.Decode(CreateCanvas(firstPayload), options);
        var second = StnV1NeutralCodec.Decode(CreateCanvas(secondPayload), options);

        Assert.Equal(firstPayload, Assert.Single(first.Nodes).Payload);
        Assert.Equal(secondPayload, Assert.Single(second.Nodes).Payload);
    }

    private static byte[] CreatePayload(byte value)
    {
        Type nodeType = typeof(DecodeFixtureNode);
        byte[] content = new byte[200_000];
        Array.Fill(content, value);
        return STNodeSavedState.Write(
            $"{nodeType.Module.Name}|{nodeType.FullName}",
            nodeType.GUID.ToString(),
            new Dictionary<string, byte[]>
            {
                ["Guid"] = Guid.NewGuid().ToByteArray(),
                ["Fixture"] = content,
            });
    }

    private static byte[] CreateCanvas(byte[] payload)
    {
        using var canvas = new MemoryStream();
        STNodeCanvasWriter.WriteRaw(canvas, [payload], [], 0, 0, 1);
        return canvas.ToArray();
    }

    private sealed class DecodeFixtureNode : STNode { }
}

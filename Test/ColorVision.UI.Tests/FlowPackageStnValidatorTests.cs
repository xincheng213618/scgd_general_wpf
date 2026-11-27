using ColorVision.Engine.Templates.Flow;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ColorVision.UI.Tests;

public class FlowPackageStnValidatorTests
{
    [Fact]
    public void CanvasWriterKeepsV1Utf8LengthsPropertyOrderAndConnectionBits()
    {
        byte[] node = STNodeSavedState.Write("模", "T", new Dictionary<string, byte[]>
        {
            ["点"] = [0xfe, 0x13],
            ["Empty"] = [],
        });
        byte[] expectedNode =
        [
            3, .. "模"u8, 1, (byte)'T',
            3, 0, 0, 0, .. "点"u8, 2, 0, 0, 0, 0xfe, 0x13,
            5, 0, 0, 0, .. "Empty"u8, 0, 0, 0, 0,
        ];
        Assert.Equal(expectedNode, node);

        const long packedConnection = unchecked((long)0xfedcba9800000001UL);
        using var canvas = new MemoryStream();
        STNodeCanvasWriter.WriteRaw(canvas, [node], [packedConnection], 0, 0, 1);
        Assert.False(canvas.CanWrite);

        using var body = new MemoryStream();
        WriteCanvasHeader(body, nodeCount: 1);
        body.Write(BitConverter.GetBytes(expectedNode.Length));
        body.Write(expectedNode);
        body.Write(BitConverter.GetBytes(1));
        body.Write(BitConverter.GetBytes(packedConnection));
        Assert.Equal(body.ToArray(), FlowPackageStnValidator.ValidateAndDecompress(canvas.ToArray()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void ValidateAndDecompressAcceptsVersionOneCanvas(int extraValueBytes)
    {
        byte[] nodeData = CreateNodeData(extraValueBytes);
        byte[] body = CreateCanvasBody(nodeData);
        byte[] stnData = CompressStndV1(body);

        byte[] result =
            FlowPackageStnValidator.ValidateAndDecompress(stnData);

        Assert.Equal(body, result);
    }

    [Fact]
    public void ValidateAndDecompressRejectsUnknownVersion()
    {
        byte[] stnData = CompressStndV1(CreateCanvasBody());
        stnData[4] = 2;

        Assert.Throws<InvalidDataException>(
            () => FlowPackageStnValidator.ValidateAndDecompress(stnData));
    }

    [Fact]
    public void ValidateAndDecompressRejectsOverflowingNodeLength()
    {
        using var body = new MemoryStream();
        WriteCanvasHeader(body, nodeCount: 1);
        body.Write(BitConverter.GetBytes(int.MaxValue));

        Assert.Throws<InvalidDataException>(
            () => FlowPackageStnValidator.ValidateAndDecompress(
                CompressStndV1(body.ToArray())));
    }

    [Fact]
    public void ValidateAndDecompressRejectsNegativeNodeLength()
    {
        using var body = new MemoryStream();
        WriteCanvasHeader(body, nodeCount: 1);
        body.Write(BitConverter.GetBytes(-1));

        Assert.Throws<InvalidDataException>(
            () => FlowPackageStnValidator.ValidateAndDecompress(
                CompressStndV1(body.ToArray())));
    }

    [Fact]
    public void ValidateAndDecompressRejectsNegativeNodeCount()
    {
        using var body = new MemoryStream();
        WriteCanvasHeader(body, nodeCount: -1);
        body.Write(BitConverter.GetBytes(0));

        Assert.Throws<InvalidDataException>(
            () => FlowPackageStnValidator.ValidateAndDecompress(
                CompressStndV1(body.ToArray())));
    }

    [Fact]
    public void ValidateAndDecompressRejectsTruncatedTypeName()
    {
        byte[] nodeData =
        [
            1,
            (byte)'M',
            byte.MaxValue,
            (byte)'T',
        ];

        Assert.Throws<InvalidDataException>(
            () => FlowPackageStnValidator.ValidateAndDecompress(
                CompressStndV1(CreateCanvasBody(nodeData))));
    }

    [Fact]
    public void ValidateAndDecompressRejectsNegativePropertyLength()
    {
        using var node = new MemoryStream();
        WriteByteLengthString(node, "Model");
        WriteByteLengthString(node, "Type");
        node.Write(BitConverter.GetBytes(-1));

        Assert.Throws<InvalidDataException>(
            () => FlowPackageStnValidator.ValidateAndDecompress(
                CompressStndV1(CreateCanvasBody(node.ToArray()))));
    }

    [Fact]
    public void ValidateAndDecompressRejectsOverflowingPropertyLength()
    {
        using var node = new MemoryStream();
        WriteByteLengthString(node, "Model");
        WriteByteLengthString(node, "Type");
        node.Write(BitConverter.GetBytes(int.MaxValue));

        Assert.Throws<InvalidDataException>(
            () => FlowPackageStnValidator.ValidateAndDecompress(
                CompressStndV1(CreateCanvasBody(node.ToArray()))));
    }

    [Fact]
    public void ValidateAndDecompressRejectsMissingConnectionCount()
    {
        using var body = new MemoryStream();
        WriteCanvasHeader(body, nodeCount: 0);

        Assert.Throws<InvalidDataException>(
            () => FlowPackageStnValidator.ValidateAndDecompress(
                CompressStndV1(body.ToArray())));
    }

    [Fact]
    public void ValidateAndDecompressRejectsConnectionTail()
    {
        byte[] body = CreateCanvasBody();
        Array.Resize(ref body, body.Length + 1);

        Assert.Throws<InvalidDataException>(
            () => FlowPackageStnValidator.ValidateAndDecompress(
                CompressStndV1(body)));
    }

    [Fact]
    public void ValidateAndDecompressRejectsGZipBomb()
    {
        byte[] body =
            new byte[FlowPackageStnValidator.MaximumDecompressedLength + 1];

        Assert.Throws<InvalidDataException>(
            () => FlowPackageStnValidator.ValidateAndDecompress(
                CompressStndV1(body)));
    }

    [Fact]
    public void ValidateAndDecompressPreservesHighlyCompressedDataAtTheCallerLimit()
    {
        byte[] body = CreateCanvasBody(CreateNodeData(512 * 1024));
        byte[] stnData = CompressStndV1(body);
        Assert.Equal(body, FlowPackageStnValidator.ValidateAndDecompress(stnData, body.Length));
        Assert.Throws<InvalidDataException>(() => FlowPackageStnValidator.ValidateAndDecompress(stnData, body.Length - 1));
    }

    private static byte[] CreateCanvasBody(params byte[][] nodeData)
    {
        using var body = new MemoryStream();
        WriteCanvasHeader(body, nodeData.Length);
        foreach (byte[] node in nodeData)
        {
            body.Write(BitConverter.GetBytes(node.Length));
            body.Write(node);
        }
        body.Write(BitConverter.GetBytes(0));
        return body.ToArray();
    }

    private static void WriteCanvasHeader(
        Stream stream,
        int nodeCount)
    {
        stream.Write(BitConverter.GetBytes(0f));
        stream.Write(BitConverter.GetBytes(0f));
        stream.Write(BitConverter.GetBytes(1f));
        stream.Write(BitConverter.GetBytes(nodeCount));
    }

    private static byte[] CreateNodeData(int extraValueBytes)
    {
        using var node = new MemoryStream();
        WriteByteLengthString(node, "Model");
        WriteByteLengthString(node, "Type");
        byte[] key = Encoding.UTF8.GetBytes("TemplateName");
        byte[] value = Encoding.UTF8.GetBytes("Camera.Default" + new string('x', extraValueBytes));
        node.Write(BitConverter.GetBytes(key.Length));
        node.Write(key);
        node.Write(BitConverter.GetBytes(value.Length));
        node.Write(value);
        return node.ToArray();
    }

    private static void WriteByteLengthString(
        Stream stream,
        string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        stream.WriteByte(checked((byte)bytes.Length));
        stream.Write(bytes);
    }

    private static byte[] CompressStndV1(byte[] body)
    {
        using var output = new MemoryStream();
        output.Write("STND"u8);
        output.WriteByte(1);
        using (var gzip = new GZipStream(
            output,
            CompressionMode.Compress,
            leaveOpen: true))
        {
            gzip.Write(body);
        }
        return output.ToArray();
    }
}

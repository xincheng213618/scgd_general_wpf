using ColorVision.Engine.Templates.Flow;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ColorVision.UI.Tests;

public class FlowPackageStnValidatorTests
{
    [Fact]
    public void ValidateAndDecompressAcceptsVersionOneCanvas()
    {
        byte[] nodeData = CreateNodeData();
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

    private static byte[] CreateNodeData()
    {
        using var node = new MemoryStream();
        WriteByteLengthString(node, "Model");
        WriteByteLengthString(node, "Type");
        byte[] key = Encoding.UTF8.GetBytes("TemplateName");
        byte[] value = Encoding.UTF8.GetBytes("Camera.Default");
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

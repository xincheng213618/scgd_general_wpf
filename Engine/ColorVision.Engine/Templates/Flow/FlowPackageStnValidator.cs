using System;
using System.IO;
using System.IO.Compression;

namespace ColorVision.Engine.Templates.Flow;

/// <summary>
/// Validates the unchanged STND v1 envelope used inside a flow package without
/// loading node types or mutating a live editor canvas.
/// </summary>
internal static class FlowPackageStnValidator
{
    internal const int MaximumStndLength =
        5 + 160 * 1024 * 1024;
    internal const int MaximumDecompressedLength =
        160 * 1024 * 1024;
    private const int MaximumNodeCount = 10_000;
    private const int MaximumConnectionCount = 100_000;
    private const int MaximumNodeDataLength = 16 * 1024 * 1024;
    private const long MaximumTotalNodeDataLength =
        128L * 1024 * 1024;
    private const int CanvasHeaderLength = 16;
    private const int ConnectionDataLength = sizeof(long);
    private static readonly byte[] StndHeader = [83, 84, 78, 68, 1];
    private static readonly uint[] Crc32Table = CreateCrc32Table();

    /// <summary>
    /// Validates and decompresses a complete STND v1 document.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The envelope, compressed body, canvas framing, or node framing is invalid.
    /// </exception>
    internal static byte[] ValidateAndDecompress(byte[] stnData)
    {
        ArgumentNullException.ThrowIfNull(stnData);
        ValidateEnvelope(stnData);

        byte[] decompressed;
        try
        {
            using var input = new MemoryStream(
                stnData,
                StndHeader.Length,
                stnData.Length - StndHeader.Length,
                writable: false);
            using var gzip = new GZipStream(
                input,
                CompressionMode.Decompress,
                leaveOpen: true);
            using var output = new MemoryStream();
            byte[] buffer = new byte[81_920];
            while (true)
            {
                int read = gzip.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;
                if (read > MaximumDecompressedLength - output.Length)
                {
                    throw new InvalidDataException(
                        "flow.stn 解压后超过 160 MiB 限制。");
                }
                output.Write(buffer, 0, read);
            }
            decompressed = output.ToArray();
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is IOException
            or ArgumentException)
        {
            throw new InvalidDataException(
                "flow.stn 的 gzip 数据已损坏。",
                ex);
        }

        ValidateGZipTrailer(stnData, decompressed);
        ValidateCanvasBody(decompressed);
        return decompressed;
    }

    private static void ValidateEnvelope(byte[] stnData)
    {
        // A gzip member has at least a 10-byte header and an 8-byte trailer.
        if (stnData.Length < StndHeader.Length + 18
            || stnData.Length > MaximumStndLength)
            throw new InvalidDataException("flow.stn 数据不完整。");

        for (int i = 0; i < StndHeader.Length; i++)
        {
            if (stnData[i] != StndHeader[i])
            {
                throw new InvalidDataException(
                    "flow.stn 不是受支持的 STND v1 数据。");
            }
        }
    }

    private static void ValidateGZipTrailer(
        byte[] stnData,
        byte[] decompressed)
    {
        uint expectedCrc = BitConverter.ToUInt32(
            stnData,
            stnData.Length - 8);
        uint expectedLength = BitConverter.ToUInt32(
            stnData,
            stnData.Length - 4);
        if (expectedLength != unchecked((uint)decompressed.Length)
            || expectedCrc != ComputeCrc32(decompressed))
        {
            throw new InvalidDataException(
                "flow.stn 的 gzip 校验信息无效或包含尾随数据。");
        }
    }

    private static void ValidateCanvasBody(byte[] data)
    {
        if (data.Length < CanvasHeaderLength)
            throw new InvalidDataException("flow.stn 缺少画布元数据。");

        float offsetX = BitConverter.ToSingle(data, 0);
        float offsetY = BitConverter.ToSingle(data, 4);
        float scale = BitConverter.ToSingle(data, 8);
        if (!float.IsFinite(offsetX)
            || !float.IsFinite(offsetY)
            || !float.IsFinite(scale)
            || scale <= 0)
        {
            throw new InvalidDataException("flow.stn 的画布视图参数无效。");
        }

        int nodeCount = BitConverter.ToInt32(data, 12);
        if (nodeCount < 0 || nodeCount > MaximumNodeCount)
        {
            throw new InvalidDataException(
                $"flow.stn 的节点数量无效：{nodeCount}。");
        }

        int position = CanvasHeaderLength;
        long totalNodeDataLength = 0;
        for (int i = 0; i < nodeCount; i++)
        {
            int nodeDataLength = ReadInt32(
                data,
                ref position,
                $"第 {i + 1} 个节点数据长度");
            if (nodeDataLength <= 0
                || nodeDataLength > MaximumNodeDataLength
                || nodeDataLength > data.Length - position)
            {
                throw new InvalidDataException(
                    $"第 {i + 1} 个节点数据长度无效：{nodeDataLength}。");
            }
            totalNodeDataLength += nodeDataLength;
            if (totalNodeDataLength > MaximumTotalNodeDataLength)
            {
                throw new InvalidDataException(
                    "flow.stn 的节点数据总长度超过限制。");
            }

            ValidateNodeData(
                data.AsSpan(position, nodeDataLength),
                i);
            position += nodeDataLength;
        }

        int connectionCount = ReadInt32(
            data,
            ref position,
            "连接数量");
        if (connectionCount < 0
            || connectionCount > MaximumConnectionCount)
        {
            throw new InvalidDataException(
                $"flow.stn 的连接数量无效：{connectionCount}。");
        }

        int remainingLength = data.Length - position;
        if (connectionCount > remainingLength / ConnectionDataLength
            || remainingLength != connectionCount * ConnectionDataLength)
        {
            throw new InvalidDataException(
                "flow.stn 的连接数据不完整或包含未识别的尾部内容。");
        }
    }

    private static void ValidateNodeData(
        ReadOnlySpan<byte> nodeData,
        int nodeIndex)
    {
        int position = 0;
        ReadByteLengthString(
            nodeData,
            ref position,
            nodeIndex,
            "模型名称");
        ReadByteLengthString(
            nodeData,
            ref position,
            nodeIndex,
            "类型标识");

        while (position < nodeData.Length)
        {
            int keyLength = ReadInt32(
                nodeData,
                ref position,
                nodeIndex,
                "属性名称长度");
            if (keyLength <= 0
                || keyLength > nodeData.Length - position)
            {
                throw InvalidNodeDataLength(
                    nodeIndex,
                    "属性名称",
                    keyLength);
            }
            position += keyLength;

            int valueLength = ReadInt32(
                nodeData,
                ref position,
                nodeIndex,
                "属性值长度");
            if (valueLength < 0
                || valueLength > nodeData.Length - position)
            {
                throw InvalidNodeDataLength(
                    nodeIndex,
                    "属性值",
                    valueLength);
            }
            position += valueLength;
        }
    }

    private static void ReadByteLengthString(
        ReadOnlySpan<byte> data,
        ref int position,
        int nodeIndex,
        string fieldName)
    {
        if (position >= data.Length)
        {
            throw new InvalidDataException(
                $"第 {nodeIndex + 1} 个节点缺少{fieldName}长度。");
        }

        int length = data[position++];
        if (length <= 0 || length > data.Length - position)
        {
            throw InvalidNodeDataLength(
                nodeIndex,
                fieldName,
                length);
        }
        position += length;
    }

    private static int ReadInt32(
        byte[] data,
        ref int position,
        string fieldName)
    {
        if (position < 0
            || position > data.Length
            || sizeof(int) > data.Length - position)
        {
            throw new InvalidDataException(
                $"flow.stn 缺少{fieldName}。");
        }

        int value = BitConverter.ToInt32(data, position);
        position += sizeof(int);
        return value;
    }

    private static int ReadInt32(
        ReadOnlySpan<byte> data,
        ref int position,
        int nodeIndex,
        string fieldName)
    {
        if (position < 0
            || position > data.Length
            || sizeof(int) > data.Length - position)
        {
            throw new InvalidDataException(
                $"第 {nodeIndex + 1} 个节点缺少{fieldName}。");
        }

        int value = BitConverter.ToInt32(
            data.Slice(position, sizeof(int)));
        position += sizeof(int);
        return value;
    }

    private static InvalidDataException InvalidNodeDataLength(
        int nodeIndex,
        string fieldName,
        int length)
    {
        return new InvalidDataException(
            $"第 {nodeIndex + 1} 个节点的{fieldName}长度无效：{length}。");
    }

    private static uint ComputeCrc32(byte[] data)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in data)
        {
            crc = Crc32Table[(crc ^ value) & byte.MaxValue]
                ^ crc >> 8;
        }
        return ~crc;
    }

    private static uint[] CreateCrc32Table()
    {
        uint[] table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            uint value = i;
            for (int bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0
                    ? 0xEDB88320u ^ value >> 1
                    : value >> 1;
            }
            table[i] = value;
        }
        return table;
    }
}

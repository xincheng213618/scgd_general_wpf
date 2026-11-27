using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ST.Library.UI.NodeEditor;

/// <summary>
/// Decodes the version-1 STN envelope without mutating a live canvas.
/// Keeping decoding separate from commit makes truncated or corrupt input
/// fail before the currently displayed/runtime graph is cleared.
/// </summary>
internal static class STNodeCanvasReader
{
    private const int MaximumNodeCount = 10_000;
    private const int MaximumConnectionCount = 100_000;
    private const int MaximumNodeDataLength = 16 * 1024 * 1024;
    private const long MaximumTotalNodeDataLength = 128L * 1024 * 1024;
    private const int MaximumCompressedGraphLength = 160 * 1024 * 1024;
    private const int MaximumDecompressedGraphLength = 160 * 1024 * 1024;
    private static readonly uint[] Crc32Table = CreateCrc32Table();

    internal sealed class Document
    {
        public float CanvasOffsetX { get; init; }
        public float CanvasOffsetY { get; init; }
        public float CanvasScale { get; init; }
        public List<STNode> Nodes { get; } = new();
        public List<Connection> Connections { get; } = new();

        public void ConnectDetachedNodes()
        {
            ConnectionStatus status = STNodeConnectionRestorer.Restore(
                Connections.Select(connection => (connection.Output, connection.Input)),
                isOwnerOfOwner: false);
            if (status != ConnectionStatus.Connected)
                throw new InvalidDataException($"无法恢复流程连接：{status}");
        }
    }

    internal sealed class Connection
    {
        public Connection(STNodeOption output, STNodeOption input)
        {
            Output = output;
            Input = input;
        }

        public STNodeOption Output { get; }

        public STNodeOption Input { get; }
    }

    public static Document Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        byte[] header = ReadBytes(stream, STNodeConstant.NodeFlag.Length + 1);
        for (int i = 0; i < STNodeConstant.NodeFlag.Length; i++)
        {
            if (header[i] != STNodeConstant.NodeFlag[i])
                throw new InvalidDataException("无法识别的文件类型");
        }
        if (header[STNodeConstant.NodeFlag.Length] != STNodeConstant.Version)
            throw new InvalidDataException("无法识别的文件版本号");

        ReadOnlySpan<byte> body = DecompressGZip(ReadCompressed(stream)).AsSpan();
        var document = new Document
        {
            CanvasOffsetX = ReadSingle(ref body),
            CanvasOffsetY = ReadSingle(ref body),
            CanvasScale = ReadSingle(ref body),
        };
        if (float.IsNaN(document.CanvasOffsetX)
            || float.IsInfinity(document.CanvasOffsetX)
            || float.IsNaN(document.CanvasOffsetY)
            || float.IsInfinity(document.CanvasOffsetY)
            || float.IsNaN(document.CanvasScale)
            || float.IsInfinity(document.CanvasScale)
            || document.CanvasScale <= 0)
        {
            throw new InvalidDataException("画布视图参数无效");
        }

        int nodeCount = ReadCount(ref body, MaximumNodeCount, "节点");
        var options = new List<STNodeOption>();
        var indexedOptions = new HashSet<STNodeOption>();
        STNodeLegacyConnections legacyConnections = null;
        var portLayouts = new (bool HasSavedLayout, bool HasLegacyPlaceholder)[nodeCount];
        var nodeIds = new HashSet<Guid>();
        long totalNodeDataLength = 0;
        for (int i = 0; i < nodeCount; i++)
        {
            int nodeDataLength = ReadInt32(ref body);
            if (nodeDataLength <= 0 || nodeDataLength > MaximumNodeDataLength)
            {
                throw new InvalidDataException(
                    $"第 {i + 1} 个节点数据长度无效：{nodeDataLength}");
            }
            totalNodeDataLength += nodeDataLength;
            if (totalNodeDataLength > MaximumTotalNodeDataLength)
                throw new InvalidDataException("节点数据总长度超过限制");

            STNode node;
            try
            {
                STNodeSavedState state = STNodeSavedState.Parse(ReadBodyBytes(ref body, nodeDataLength));
                if (state.Properties.TryGetValue(STNodeSavedState.LegacyConnectionsKey, out byte[] archive))
                {
                    if (legacyConnections != null) throw new InvalidDataException("旧流程连线记录重复。");
                    legacyConnections = STNodeLegacyConnections.Deserialize(archive);
                }
                node = state.CreateNode();
                portLayouts[i] = (state.Properties.ContainsKey(STNodeSavedState.PortLayoutKey),
                    state.Properties.ContainsKey(STNodeSavedState.LegacyPlaceholderKey));
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    $"第 {i + 1} 个节点无法加载",
                    ex);
            }
            document.Nodes.Add(node);
            if (!nodeIds.Add(node.Guid)) throw new InvalidDataException("流程中存在重复的结点标识。");
        }

        var legacyPlaceholderIds = legacyConnections?.Nodes.Where(span => span.InputCount < 0)
            .Select(span => span.NodeId).ToHashSet();
        bool archiveOriginalConnections = false;
        for (int i = 0; i < document.Nodes.Count; i++)
        {
            STNode node = document.Nodes[i];
            var (hasSavedLayout, hasLegacyPlaceholder) = portLayouts[i];
            // A saved legacy placeholder had no visible ports. If its plugin has
            // returned, those newly restored ports belong to the archived map,
            // not to the current saved map's global indices.
            bool wasLegacyPlaceholder = hasLegacyPlaceholder
                || legacyConnections != null && !hasSavedLayout && legacyPlaceholderIds.Contains(node.Guid);
            if (!wasLegacyPlaceholder)
            {
                AddOptions(options, indexedOptions, node.InputOptions);
                AddOptions(options, indexedOptions, node.OutputOptions);
                if (legacyConnections == null && node is STMissingNode { HasSavedPortLayout: false })
                    archiveOriginalConnections = true;
            }
            if (options.Count > 1_000_000) throw new InvalidDataException("流程端口总数量超过限制。");
        }

        int connectionCount = ReadCount(
            ref body,
            MaximumConnectionCount,
            "连接");
        // Nearby port indices often differ only by a few bits. Hash the two indices as a pair.
        var connectionKeys = new HashSet<(int Output, int Input)>();
        long[] packedConnections = archiveOriginalConnections ? new long[connectionCount] : null;
        for (int i = 0; i < connectionCount; i++)
        {
            long packed = ReadInt64(ref body);
            int outputIndex = (int)(packed >> 32);
            int inputIndex = unchecked((int)packed);
            if (outputIndex is < 0 or >= 1_000_000 || inputIndex is < 0 or >= 1_000_000
                || !connectionKeys.Add((outputIndex, inputIndex)))
                throw new InvalidDataException($"第 {i + 1} 条连接编号无效或重复");
            if (packedConnections != null)
            {
                packedConnections[i] = packed;
                continue;
            }
            if (outputIndex >= options.Count || inputIndex >= options.Count)
            {
                throw new InvalidDataException(
                    $"第 {i + 1} 条连接引用了不存在的端口");
            }
            STNodeOption output = options[outputIndex], input = options[inputIndex];
            if (output.IsInput || !input.IsInput || output.Owner == input.Owner)
            {
                throw new InvalidDataException(
                    $"第 {i + 1} 条连接方向无效");
            }
            document.Connections.Add(new Connection(output, input));
        }

        if (!body.IsEmpty)
            throw new InvalidDataException("流程数据包含未识别的尾部内容");

        if (packedConnections != null)
            legacyConnections = STNodeLegacyConnections.Capture(document.Nodes, packedConnections);
        if (legacyConnections != null && !legacyConnections.TryRestore(document.Nodes, document.Connections))
            foreach (STMissingNode node in document.Nodes.OfType<STMissingNode>()) node.LegacyConnections = legacyConnections;

        return document;
    }

    private static ArraySegment<byte> ReadCompressed(Stream stream)
    {
        if (stream is MemoryStream memory && memory.GetType() == typeof(MemoryStream)
            && memory.TryGetBuffer(out ArraySegment<byte> buffer))
        {
            int position = (int)memory.Position;
            int remaining = buffer.Count - position;
            if ((uint)remaining <= MaximumCompressedGraphLength)
            {
                memory.Position = memory.Length;
                return buffer.Slice(position, remaining);
            }
        }

        return ReadBounded(stream, MaximumCompressedGraphLength, "压缩流程数据超过限制");
    }

    private static ArraySegment<byte> ReadBounded(Stream stream, int maximumLength, string limitMessage)
    {
        using var output = new MemoryStream();
        const int bufferLength = 64 * 1024;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferLength);
        try
        {
            while (true)
            {
                int read = stream.Read(buffer, 0, bufferLength);
                if (read <= 0)
                    break;
                if (output.Length + read > maximumLength)
                    throw new InvalidDataException(limitMessage);
                output.Write(buffer, 0, read);
            }
            return new ArraySegment<byte>(output.GetBuffer(), 0, checked((int)output.Length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static ArraySegment<byte> DecompressGZip(ArraySegment<byte> compressed)
    {
        if (compressed.Count < 18)
            throw new InvalidDataException("压缩流程数据不完整");

        using var input = new MemoryStream(compressed.Array, compressed.Offset, compressed.Count, writable: false);
        using var gzip = new GZipStream(
            input,
            CompressionMode.Decompress,
            leaveOpen: true);
        ArraySegment<byte> decompressed = ReadBounded(gzip, MaximumDecompressedGraphLength, "解压后的流程数据超过限制");

        ReadOnlySpan<byte> trailer = compressed.AsSpan(compressed.Count - 8);
        uint expectedCrc = BitConverter.ToUInt32(trailer);
        uint expectedLength = BitConverter.ToUInt32(trailer[4..]);
        if (expectedLength != unchecked((uint)decompressed.Count)
            || expectedCrc != ComputeCrc32(decompressed.AsSpan()))
        {
            throw new InvalidDataException("压缩流程数据校验失败");
        }
        return decompressed;
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        int offset = 0;
        for (; offset <= data.Length - sizeof(ulong); offset += sizeof(ulong))
        {
            ulong block = BinaryPrimitives.ReadUInt64LittleEndian(data[offset..]) ^ crc;
            crc = Crc32Table[7 * 256 + (byte)block] ^ Crc32Table[6 * 256 + (byte)(block >> 8)]
                ^ Crc32Table[5 * 256 + (byte)(block >> 16)] ^ Crc32Table[4 * 256 + (byte)(block >> 24)]
                ^ Crc32Table[3 * 256 + (byte)(block >> 32)] ^ Crc32Table[2 * 256 + (byte)(block >> 40)]
                ^ Crc32Table[256 + (byte)(block >> 48)] ^ Crc32Table[(byte)(block >> 56)];
        }
        foreach (byte value in data[offset..])
        {
            crc = Crc32Table[(crc ^ value) & byte.MaxValue]
                ^ crc >> 8;
        }
        return ~crc;
    }

    private static uint[] CreateCrc32Table()
    {
        uint[] table = new uint[8 * 256];
        for (uint i = 0; i < 256; i++)
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

        // Each slice accounts for one additional byte between the input and the final CRC.
        for (int slice = 1; slice < 8; slice++)
            for (int i = 0; i < 256; i++)
            {
                uint previous = table[(slice - 1) * 256 + i];
                table[slice * 256 + i] = table[previous & byte.MaxValue] ^ (previous >> 8);
            }
        return table;
    }

    private static void AddOptions(
        List<STNodeOption> options,
        HashSet<STNodeOption> indexedOptions,
        STNodeOptionCollection ports)
    {
        for (int i = 0; i < ports.Count; i++)
        {
            STNodeOption option = ports[i];
            if (option != null && indexedOptions.Add(option))
                options.Add(option);
        }
    }

    private static int ReadCount(
        ref ReadOnlySpan<byte> body,
        int maximum,
        string valueName)
    {
        int count = ReadInt32(ref body);
        if (count < 0 || count > maximum)
            throw new InvalidDataException($"{valueName}数量无效：{count}");
        return count;
    }

    private static float ReadSingle(ref ReadOnlySpan<byte> body) =>
        BitConverter.ToSingle(ReadBodyBytes(ref body, sizeof(float)));

    private static int ReadInt32(ref ReadOnlySpan<byte> body) =>
        BitConverter.ToInt32(ReadBodyBytes(ref body, sizeof(int)));

    private static long ReadInt64(ref ReadOnlySpan<byte> body) =>
        BitConverter.ToInt64(ReadBodyBytes(ref body, sizeof(long)));

    private static ReadOnlySpan<byte> ReadBodyBytes(ref ReadOnlySpan<byte> body, int count)
    {
        if (count > body.Length)
            throw new EndOfStreamException("流程数据意外结束");

        ReadOnlySpan<byte> value = body[..count];
        body = body[count..];
        return value;
    }

    private static byte[] ReadBytes(Stream stream, int count)
    {
        byte[] buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(buffer, offset, count - offset);
            if (read <= 0)
                throw new EndOfStreamException("流程数据意外结束");
            offset += read;
        }
        return buffer;
    }

}

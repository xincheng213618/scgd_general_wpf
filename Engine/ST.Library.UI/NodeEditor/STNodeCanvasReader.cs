using System;
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

        byte[] compressed = ReadToEnd(
            stream,
            MaximumCompressedGraphLength);
        byte[] decompressed = DecompressGZip(compressed);
        using var bodyStream = new MemoryStream(
            decompressed,
            writable: false);
        var document = new Document
        {
            CanvasOffsetX = ReadSingle(bodyStream),
            CanvasOffsetY = ReadSingle(bodyStream),
            CanvasScale = ReadSingle(bodyStream),
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

        int nodeCount = ReadCount(bodyStream, MaximumNodeCount, "节点");
        var options = new Dictionary<long, STNodeOption>();
        var indexedOptions = new HashSet<STNodeOption>();
        STNodeLegacyConnections legacyConnections = null;
        var states = new List<STNodeSavedState>();
        var nodeIds = new HashSet<Guid>();
        long totalNodeDataLength = 0;
        for (int i = 0; i < nodeCount; i++)
        {
            int nodeDataLength = ReadInt32(bodyStream);
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
                STNodeSavedState state = STNodeSavedState.Parse(ReadBytes(bodyStream, nodeDataLength));
                states.Add(state);
                if (state.Properties.TryGetValue(STNodeSavedState.LegacyConnectionsKey, out byte[] archive))
                {
                    if (legacyConnections != null) throw new InvalidDataException("旧流程连线记录重复。");
                    legacyConnections = STNodeLegacyConnections.Deserialize(archive);
                }
                node = state.CreateNode();
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
        for (int i = 0; i < document.Nodes.Count; i++)
        {
            STNode node = document.Nodes[i];
            // A saved legacy placeholder had no visible ports. If its plugin has
            // returned, those newly restored ports belong to the archived map,
            // not to the current saved map's global indices.
            bool wasLegacyPlaceholder = states[i].Properties.ContainsKey(STNodeSavedState.LegacyPlaceholderKey)
                || legacyConnections != null && !states[i].Properties.ContainsKey(STNodeSavedState.PortLayoutKey)
                    && legacyPlaceholderIds.Contains(node.Guid);
            if (!wasLegacyPlaceholder) AddOptions(options, indexedOptions, node);
            if (options.Count > 1_000_000) throw new InvalidDataException("流程端口总数量超过限制。");
        }

        int connectionCount = ReadCount(
            bodyStream,
            MaximumConnectionCount,
            "连接");
        var connectionKeys = new HashSet<long>();
        bool archiveOriginalConnections = legacyConnections == null && document.Nodes.Select((node, index) => (node, index))
            .Any(item => item.node is STMissingNode { HasSavedPortLayout: false }
                && !states[item.index].Properties.ContainsKey(STNodeSavedState.LegacyPlaceholderKey));
        var packedConnections = new long[connectionCount];
        for (int i = 0; i < connectionCount; i++)
        {
            long packed = ReadInt64(bodyStream);
            packedConnections[i] = packed;
            long outputIndex = packed >> 32;
            long inputIndex = unchecked((uint)packed);
            if (outputIndex < 0 || outputIndex >= 1_000_000 || inputIndex >= 1_000_000 || !connectionKeys.Add(packed))
                throw new InvalidDataException($"第 {i + 1} 条连接编号无效或重复");
            if (archiveOriginalConnections) continue;
            if (!options.TryGetValue(outputIndex, out STNodeOption output)
                || !options.TryGetValue(inputIndex, out STNodeOption input))
            {
                throw new InvalidDataException(
                    $"第 {i + 1} 条连接引用了不存在的端口");
            }
            if (output.IsInput || !input.IsInput || output.Owner == input.Owner)
            {
                throw new InvalidDataException(
                    $"第 {i + 1} 条连接方向无效");
            }
            document.Connections.Add(new Connection(output, input));
        }

        if (bodyStream.ReadByte() != -1)
            throw new InvalidDataException("流程数据包含未识别的尾部内容");

        if (archiveOriginalConnections)
            legacyConnections = STNodeLegacyConnections.Capture(document.Nodes, packedConnections);
        if (legacyConnections != null && !legacyConnections.TryRestore(document.Nodes, document.Connections))
            foreach (STMissingNode node in document.Nodes.OfType<STMissingNode>()) node.LegacyConnections = legacyConnections;

        return document;
    }

    private static byte[] ReadToEnd(Stream stream, int maximumLength)
    {
        using var output = new MemoryStream();
        byte[] buffer = new byte[81_920];
        while (true)
        {
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0)
                break;
            if (output.Length + read > maximumLength)
                throw new InvalidDataException("压缩流程数据超过限制");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static byte[] DecompressGZip(byte[] compressed)
    {
        if (compressed.Length < 18)
            throw new InvalidDataException("压缩流程数据不完整");

        using var input = new MemoryStream(compressed, writable: false);
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
            if (output.Length + read > MaximumDecompressedGraphLength)
                throw new InvalidDataException("解压后的流程数据超过限制");
            output.Write(buffer, 0, read);
        }

        byte[] decompressed = output.ToArray();
        uint expectedCrc = BitConverter.ToUInt32(
            compressed,
            compressed.Length - 8);
        uint expectedLength = BitConverter.ToUInt32(
            compressed,
            compressed.Length - 4);
        if (expectedLength != unchecked((uint)decompressed.Length)
            || expectedCrc != ComputeCrc32(decompressed))
        {
            throw new InvalidDataException("压缩流程数据校验失败");
        }
        return decompressed;
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

    private static void AddOptions(
        Dictionary<long, STNodeOption> options,
        HashSet<STNodeOption> indexedOptions,
        STNode node)
    {
        foreach (STNodeOption option in node.GetAllInputOptions())
        {
            if (option != null && indexedOptions.Add(option))
                options.Add(options.Count, option);
        }
        foreach (STNodeOption option in node.GetAllOutputOptions())
        {
            if (option != null && indexedOptions.Add(option))
                options.Add(options.Count, option);
        }
    }

    private static int ReadCount(
        Stream stream,
        int maximum,
        string valueName)
    {
        int count = ReadInt32(stream);
        if (count < 0 || count > maximum)
            throw new InvalidDataException($"{valueName}数量无效：{count}");
        return count;
    }

    private static float ReadSingle(Stream stream)
    {
        return BitConverter.ToSingle(ReadBytes(stream, sizeof(float)), 0);
    }

    private static int ReadInt32(Stream stream)
    {
        return BitConverter.ToInt32(ReadBytes(stream, sizeof(int)), 0);
    }

    private static long ReadInt64(Stream stream)
    {
        return BitConverter.ToInt64(ReadBytes(stream, sizeof(long)), 0);
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

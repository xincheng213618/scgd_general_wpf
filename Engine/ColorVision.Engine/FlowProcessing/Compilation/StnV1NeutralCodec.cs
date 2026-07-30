using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;

namespace ColorVision.Engine.FlowProcessing.Compilation;

/// <summary>
/// STND v1 codec used by compilation. It retains opaque node payloads and
/// materializes nodes only long enough to discover their option schema; it
/// never connects options or invokes OnEditorLoadCompleted.
/// </summary>
internal static class StnV1NeutralCodec
{
    private const int MaximumNodeDataLength = 16 * 1024 * 1024;
    private const long MaximumTotalNodeDataLength = 128L * 1024 * 1024;
    private const int MaximumCompressedLength = 160 * 1024 * 1024;
    private const int MaximumDecompressedLength = 160 * 1024 * 1024;
    private static readonly ConcurrentDictionary<Type, Lazy<NeutralNodeSchema>>
        SchemaCache = new();
    private static readonly ConcurrentDictionary<string, Type>
        NodeTypeCache = new(StringComparer.Ordinal);

    internal static NeutralCanvas Decode(
        byte[] canvasData,
        FlowSubflowCompilerOptions options)
    {
        ArgumentNullException.ThrowIfNull(canvasData);
        ArgumentNullException.ThrowIfNull(options);
        try
        {
            return DecodeCore(canvasData, options);
        }
        catch (FlowCompilationException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is InvalidDataException
            or EndOfStreamException
            or IOException
            or ArgumentException)
        {
            throw new FlowCompilationException(
                FlowCompilationError.InvalidCanvas,
                "STND v1 流程数据无效。",
                ex);
        }
    }

    internal static byte[] Encode(
        NeutralCanvas canvas,
        FlowSubflowCompilerOptions options)
    {
        ValidateCounts(canvas, options);
        var nodeIndices = canvas.Nodes
            .Select((node, index) => (node, index))
            .ToDictionary(item => item.node, item => item.index);
        Dictionary<NeutralPort, long> optionIndices =
            BuildOptionIndices(canvas.Nodes);

        NeutralConnection[] connections = canvas.Connections
            .Distinct(NeutralConnectionComparer.Instance)
            .OrderBy(connection => nodeIndices[connection.Output.Node])
            .ThenBy(connection => connection.Output.LocalIndex)
            .ThenBy(connection => nodeIndices[connection.Input.Node])
            .ThenBy(connection => connection.Input.LocalIndex)
            .ToArray();
        var packedConnections = new long[connections.Length];
        for (int i = 0; i < connections.Length; i++)
        {
            NeutralConnection connection = connections[i];
            ValidateConnection(connection, nodeIndices);
            if (!optionIndices.TryGetValue(
                    connection.Output,
                    out long outputIndex)
                || !optionIndices.TryGetValue(
                    connection.Input,
                    out long inputIndex))
            {
                throw new FlowCompilationException(
                    FlowCompilationError.InvalidCanvas,
                    "流程连接引用了不存在的端口。");
            }

            packedConnections[i] =
                outputIndex << 32 | unchecked((uint)inputIndex);
        }

        using var output = new MemoryStream();
        STNodeCanvasWriter.WriteRaw(
            output,
            canvas.Nodes.Select(node => node.Payload).ToArray(),
            packedConnections,
            canvas.CanvasOffsetX,
            canvas.CanvasOffsetY,
            canvas.CanvasScale);
        return output.ToArray();
    }

    internal static string ComputeHash(byte[] data)
    {
        return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }

    internal static Guid CreateDeterministicGuid(
        string rootHash,
        string logicalPath,
        Guid sourceNodeId)
    {
        using var stream = new MemoryStream();
        WriteLengthPrefixed(stream, Encoding.UTF8.GetBytes(rootHash));
        WriteLengthPrefixed(stream, Encoding.UTF8.GetBytes(logicalPath));
        WriteLengthPrefixed(stream, sourceNodeId.ToByteArray());
        byte[] hash = SHA256.HashData(stream.ToArray());
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, guidBytes.Length).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }

    internal static void ValidateCounts(
        NeutralCanvas canvas,
        FlowSubflowCompilerOptions options)
    {
        if (canvas.Nodes.Count > options.MaximumNodeCount)
        {
            throw new FlowCompilationException(
                FlowCompilationError.NodeLimitExceeded,
                $"编译后节点数 {canvas.Nodes.Count} 超过限制 " +
                $"{options.MaximumNodeCount}。");
        }
        if (canvas.Connections.Count > options.MaximumConnectionCount)
        {
            throw new FlowCompilationException(
                FlowCompilationError.ConnectionLimitExceeded,
                $"编译后连接数 {canvas.Connections.Count} 超过限制 " +
                $"{options.MaximumConnectionCount}。");
        }
    }

    private static NeutralCanvas DecodeCore(
        byte[] canvasData,
        FlowSubflowCompilerOptions options)
    {
        if (canvasData.Length > MaximumCompressedLength)
            throw new InvalidDataException("压缩流程数据超过限制。");

        using var input = new MemoryStream(canvasData, writable: false);
        byte[] header = ReadBytes(
            input,
            STNodeConstant.NodeFlag.Length + 1);
        if (!header.AsSpan(0, STNodeConstant.NodeFlag.Length)
                .SequenceEqual(STNodeConstant.NodeFlag)
            || header[^1] != STNodeConstant.Version)
        {
            throw new InvalidDataException("仅支持 STND v1 流程数据。");
        }

        using var gzip = new GZipStream(
            input,
            CompressionMode.Decompress,
            leaveOpen: true);
        using var body = ReadBounded(gzip, MaximumDecompressedLength);
        if (input.Position != input.Length)
            throw new InvalidDataException("STND 包含未识别的尾部内容。");

        var canvas = new NeutralCanvas(
            ReadSingle(body),
            ReadSingle(body),
            ReadSingle(body));
        if (!float.IsFinite(canvas.CanvasOffsetX)
            || !float.IsFinite(canvas.CanvasOffsetY)
            || !float.IsFinite(canvas.CanvasScale)
            || canvas.CanvasScale <= 0)
        {
            throw new InvalidDataException("画布视图参数无效。");
        }

        int nodeCount = ReadCount(
            body,
            options.MaximumNodeCount,
            "节点");
        long totalNodeDataLength = 0;
        var nodeIds = new HashSet<Guid>();
        for (int i = 0; i < nodeCount; i++)
        {
            int payloadLength = ReadInt32(body);
            if (payloadLength <= 0
                || payloadLength > MaximumNodeDataLength)
            {
                throw new InvalidDataException(
                    $"第 {i + 1} 个节点数据长度无效：{payloadLength}。");
            }
            totalNodeDataLength += payloadLength;
            if (totalNodeDataLength > MaximumTotalNodeDataLength)
                throw new InvalidDataException("节点数据总长度超过限制。");

            NeutralNode node = ParseNode(
                ReadBytes(body, payloadLength));
            if (!nodeIds.Add(node.NodeId))
            {
                throw new InvalidDataException(
                    $"节点 ID 重复：{node.NodeId}。");
            }
            canvas.Nodes.Add(node);
        }

        Dictionary<long, NeutralPort> optionsByIndex =
            BuildOptionsByIndex(canvas.Nodes);
        Dictionary<NeutralNode, int> nodeIndices = canvas.Nodes
            .Select((node, index) => (node, index))
            .ToDictionary(item => item.node, item => item.index);
        int connectionCount = ReadCount(
            body,
            options.MaximumConnectionCount,
            "连接");
        var packedConnections = new HashSet<long>();
        for (int i = 0; i < connectionCount; i++)
        {
            long packed = ReadInt64(body);
            long outputIndex = packed >> 32;
            long inputIndex = unchecked((uint)packed);
            if (!optionsByIndex.TryGetValue(
                    outputIndex,
                    out NeutralPort? output)
                || !optionsByIndex.TryGetValue(
                    inputIndex,
                    out NeutralPort? inputPort))
            {
                throw new InvalidDataException(
                    $"第 {i + 1} 条连接引用了不存在的端口。");
            }
            var connection = new NeutralConnection(output, inputPort);
            ValidateConnection(
                connection,
                nodeIndices);
            if (!packedConnections.Add(packed))
            {
                throw new InvalidDataException(
                    $"第 {i + 1} 条连接重复。");
            }
            canvas.Connections.Add(connection);
        }
        if (body.ReadByte() != -1)
            throw new InvalidDataException("流程数据包含未识别的尾部内容。");

        return canvas;
    }

    private static NeutralNode ParseNode(byte[] payload)
    {
        int offset = 0;
        string modelKey = ReadByteLengthString(
            payload,
            ref offset,
            "节点类型");
        string typeKey = ReadByteLengthString(
            payload,
            ref offset,
            "节点类型标识");
        int guidValueOffset = -1;
        Guid nodeId = Guid.Empty;
        var propertyNames = new HashSet<string>(
            StringComparer.Ordinal);
        while (offset < payload.Length)
        {
            int keyLength = ReadInt32(
                payload,
                ref offset,
                "属性名称长度");
            string propertyName = Encoding.UTF8.GetString(
                ReadBytes(
                    payload,
                    ref offset,
                    keyLength,
                    "属性名称"));
            if (!propertyNames.Add(propertyName))
            {
                throw new InvalidDataException(
                    $"节点数据包含重复属性：{propertyName}。");
            }
            int valueLength = ReadInt32(
                payload,
                ref offset,
                "属性值长度");
            int valueOffset = offset;
            byte[] value = ReadBytes(
                payload,
                ref offset,
                valueLength,
                "属性值");
            if (string.Equals(
                    propertyName,
                    "Guid",
                    StringComparison.Ordinal))
            {
                if (value.Length != 16)
                    throw new InvalidDataException("节点 Guid 长度无效。");
                nodeId = new Guid(value);
                guidValueOffset = valueOffset;
            }
        }
        if (guidValueOffset < 0 || nodeId == Guid.Empty)
            throw new InvalidDataException("节点缺少有效 Guid。");

        Type type = ResolveNodeType(typeKey, modelKey);
        NeutralNodeSchema schema;
        try
        {
            schema = SchemaCache.GetOrAdd(
                type,
                key => new Lazy<NeutralNodeSchema>(
                    () => CreateSchema(key),
                    isThreadSafe: true)).Value;
        }
        catch (Exception ex)
        {
            throw new FlowCompilationException(
                FlowCompilationError.UnknownNodeType,
                $"无法读取节点端口结构：{type.FullName}。",
                ex);
        }
        return new NeutralNode(
            payload,
            guidValueOffset,
            nodeId,
            modelKey,
            typeKey,
            schema);
    }

    private static Type ResolveNodeType(
        string typeKey,
        string modelKey)
    {
        string cacheKey = $"{typeKey}\0{modelKey}";
        return NodeTypeCache.GetOrAdd(
            cacheKey,
            _ => ResolveNodeTypeCore(typeKey, modelKey));
    }

    private static Type ResolveNodeTypeCore(
        string typeKey,
        string modelKey)
    {
        Type[] candidates = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .SelectMany(GetLoadableTypes)
            .Where(type =>
                type != null
                && type.IsClass
                && !type.IsAbstract
                && typeof(STNode).IsAssignableFrom(type))
            .ToArray();

        Type? resolved = candidates.FirstOrDefault(type =>
            string.Equals(
                type.GUID.ToString(),
                typeKey,
                StringComparison.OrdinalIgnoreCase));
        resolved ??= candidates.FirstOrDefault(type =>
            string.Equals(
                $"{type.Module.Name}|{type.FullName}",
                modelKey,
                StringComparison.Ordinal)
            || string.Equals(
                $"{type.Module.Name}|{type.Name}",
                modelKey,
                StringComparison.Ordinal));
        if (resolved == null)
        {
            throw new FlowCompilationException(
                FlowCompilationError.UnknownNodeType,
                $"无法解析节点类型：{modelKey} ({typeKey})。");
        }
        return resolved;
    }

    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types
                .Where(type => type != null)
                .Cast<Type>()
                .ToArray();
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    private static NeutralNodeSchema CreateSchema(Type type)
    {
        STNode? node = null;
        try
        {
            node = (STNode?)Activator.CreateInstance(
                type,
                nonPublic: true);
            if (node == null)
                throw new InvalidOperationException("节点构造函数返回空。");
            node.Create();
            STNodeOption[] inputs = node.GetAllInputOptions();
            STNodeOption[] outputs = node.GetAllOutputOptions();
            int primaryStartOutputIndex = -1;
            int primaryEndInputIndex = -1;
            if (node is BaseStartNode start)
            {
                primaryStartOutputIndex = Array.FindIndex(
                    outputs,
                    option => ReferenceEquals(
                        option,
                        start.m_op_start));
            }
            if (node is CVEndNode end)
            {
                primaryEndInputIndex = Array.FindIndex(
                    inputs,
                    option => ReferenceEquals(
                        option,
                        end.m_in_start));
            }
            return new NeutralNodeSchema(
                type,
                inputs.Select((option, index) =>
                    new NeutralPortSchema(
                        IsInput: true,
                        index,
                        option.DataType,
                        option.Text,
                        ReferenceEquals(
                            option,
                            STNodeOption.Empty))).ToArray(),
                outputs.Select((option, index) =>
                    new NeutralPortSchema(
                        IsInput: false,
                        index,
                        option.DataType,
                        option.Text,
                        ReferenceEquals(
                            option,
                            STNodeOption.Empty))).ToArray(),
                typeof(BaseStartNode).IsAssignableFrom(type),
                typeof(CVEndNode).IsAssignableFrom(type),
                primaryStartOutputIndex,
                primaryEndInputIndex);
        }
        finally
        {
            if (node is IDisposable disposable)
                disposable.Dispose();
        }
    }

    private static Dictionary<long, NeutralPort> BuildOptionsByIndex(
        IReadOnlyList<NeutralNode> nodes)
    {
        var options = new Dictionary<long, NeutralPort>();
        NeutralPort? sharedEmpty = null;
        long nextIndex = 0;
        foreach (NeutralNode node in nodes)
        {
            foreach (NeutralPort port in node.Inputs.Concat(node.Outputs))
            {
                if (port.Schema.IsEmpty)
                {
                    if (sharedEmpty == null)
                    {
                        sharedEmpty = port;
                        options.Add(nextIndex++, port);
                    }
                    continue;
                }
                options.Add(nextIndex++, port);
            }
        }
        return options;
    }

    private static Dictionary<NeutralPort, long> BuildOptionIndices(
        IReadOnlyList<NeutralNode> nodes)
    {
        var options = new Dictionary<NeutralPort, long>();
        long nextIndex = 0;
        long? sharedEmptyIndex = null;
        foreach (NeutralNode node in nodes)
        {
            foreach (NeutralPort port in node.Inputs.Concat(node.Outputs))
            {
                if (port.Schema.IsEmpty)
                {
                    sharedEmptyIndex ??= nextIndex++;
                    options.Add(port, sharedEmptyIndex.Value);
                }
                else
                {
                    options.Add(port, nextIndex++);
                }
            }
        }
        return options;
    }

    private static void ValidateConnection(
        NeutralConnection connection,
        Dictionary<NeutralNode, int> nodeIndices)
    {
        if (connection.Output.IsInput
            || !connection.Input.IsInput
            || connection.Output.Schema.IsEmpty
            || connection.Input.Schema.IsEmpty
            || ReferenceEquals(
                connection.Output.Node,
                connection.Input.Node)
            || !nodeIndices.ContainsKey(connection.Output.Node)
            || !nodeIndices.ContainsKey(connection.Input.Node))
        {
            throw new InvalidDataException("流程连接方向无效。");
        }
    }

    private static MemoryStream ReadBounded(
        Stream source,
        int maximumLength)
    {
        var output = new MemoryStream();
        byte[] buffer = new byte[81_920];
        while (true)
        {
            int read = source.Read(buffer, 0, buffer.Length);
            if (read <= 0)
                break;
            if (output.Length + read > maximumLength)
            {
                output.Dispose();
                throw new InvalidDataException(
                    "解压后的流程数据超过限制。");
            }
            output.Write(buffer, 0, read);
        }
        output.Position = 0;
        return output;
    }

    private static int ReadCount(
        Stream stream,
        int maximum,
        string name)
    {
        int value = ReadInt32(stream);
        if (value < 0 || value > maximum)
            throw new InvalidDataException($"{name}数量无效：{value}。");
        return value;
    }

    private static float ReadSingle(Stream stream)
    {
        return BitConverter.ToSingle(
            ReadBytes(stream, sizeof(float)),
            0);
    }

    private static int ReadInt32(Stream stream)
    {
        return BitConverter.ToInt32(
            ReadBytes(stream, sizeof(int)),
            0);
    }

    private static long ReadInt64(Stream stream)
    {
        return BitConverter.ToInt64(
            ReadBytes(stream, sizeof(long)),
            0);
    }

    private static byte[] ReadBytes(Stream stream, int count)
    {
        byte[] value = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(
                value,
                offset,
                count - offset);
            if (read <= 0)
                throw new EndOfStreamException("流程数据意外结束。");
            offset += read;
        }
        return value;
    }

    private static string ReadByteLengthString(
        byte[] data,
        ref int offset,
        string name)
    {
        if (offset >= data.Length)
            throw new InvalidDataException($"{name}缺失。");
        int length = data[offset++];
        return Encoding.UTF8.GetString(
            ReadBytes(data, ref offset, length, name));
    }

    private static int ReadInt32(
        byte[] data,
        ref int offset,
        string name)
    {
        return BitConverter.ToInt32(
            ReadBytes(
                data,
                ref offset,
                sizeof(int),
                name),
            0);
    }

    private static byte[] ReadBytes(
        byte[] data,
        ref int offset,
        int length,
        string name)
    {
        if (length < 0
            || offset < 0
            || offset > data.Length - length)
        {
            throw new InvalidDataException(
                $"{name}长度无效：{length}。");
        }
        byte[] value = new byte[length];
        Buffer.BlockCopy(data, offset, value, 0, length);
        offset += length;
        return value;
    }

    private static void WriteLengthPrefixed(
        Stream stream,
        byte[] value)
    {
        byte[] length = BitConverter.GetBytes(value.Length);
        stream.Write(length, 0, length.Length);
        stream.Write(value, 0, value.Length);
    }
}

internal sealed class NeutralCanvas
{
    public NeutralCanvas(
        float canvasOffsetX,
        float canvasOffsetY,
        float canvasScale)
    {
        CanvasOffsetX = canvasOffsetX;
        CanvasOffsetY = canvasOffsetY;
        CanvasScale = canvasScale;
    }

    public float CanvasOffsetX { get; }

    public float CanvasOffsetY { get; }

    public float CanvasScale { get; }

    public List<NeutralNode> Nodes { get; } = new();

    public List<NeutralConnection> Connections { get; } = new();
}

internal sealed class NeutralNode
{
    public NeutralNode(
        byte[] payload,
        int guidValueOffset,
        Guid nodeId,
        string modelKey,
        string typeKey,
        NeutralNodeSchema schema)
    {
        Payload = payload;
        GuidValueOffset = guidValueOffset;
        NodeId = nodeId;
        ModelKey = modelKey;
        TypeKey = typeKey;
        Schema = schema;
        Inputs = schema.Inputs
            .Select(port => new NeutralPort(this, port))
            .ToArray();
        Outputs = schema.Outputs
            .Select(port => new NeutralPort(this, port))
            .ToArray();
    }

    public byte[] Payload { get; }

    public int GuidValueOffset { get; }

    public Guid NodeId { get; }

    public string ModelKey { get; }

    public string TypeKey { get; }

    public NeutralNodeSchema Schema { get; }

    public NeutralPort[] Inputs { get; }

    public NeutralPort[] Outputs { get; }

    public NeutralNode WithNodeId(Guid nodeId)
    {
        byte[] payload = (byte[])Payload.Clone();
        nodeId.ToByteArray().CopyTo(payload, GuidValueOffset);
        return new NeutralNode(
            payload,
            GuidValueOffset,
            nodeId,
            ModelKey,
            TypeKey,
            Schema);
    }
}

internal sealed record NeutralNodeSchema(
    Type NodeType,
    NeutralPortSchema[] Inputs,
    NeutralPortSchema[] Outputs,
    bool IsStart,
    bool IsEnd,
    int PrimaryStartOutputIndex,
    int PrimaryEndInputIndex);

internal sealed record NeutralPortSchema(
    bool IsInput,
    int LocalIndex,
    Type DataType,
    string Text,
    bool IsEmpty)
{
    public bool IsLoop =>
        typeof(CVLoopCFC).IsAssignableFrom(DataType);
}

internal sealed class NeutralPort
{
    public NeutralPort(
        NeutralNode node,
        NeutralPortSchema schema)
    {
        Node = node;
        Schema = schema;
    }

    public NeutralNode Node { get; }

    public NeutralPortSchema Schema { get; }

    public bool IsInput => Schema.IsInput;

    public int LocalIndex => Schema.LocalIndex;

    public Type DataType => Schema.DataType;
}

internal sealed record NeutralConnection(
    NeutralPort Output,
    NeutralPort Input);

internal sealed class NeutralConnectionComparer :
    IEqualityComparer<NeutralConnection>
{
    public static NeutralConnectionComparer Instance { get; } = new();

    public bool Equals(
        NeutralConnection? x,
        NeutralConnection? y)
    {
        return x != null
            && y != null
            && ReferenceEquals(x.Output, y.Output)
            && ReferenceEquals(x.Input, y.Input);
    }

    public int GetHashCode(NeutralConnection obj)
    {
        return HashCode.Combine(obj.Output, obj.Input);
    }
}

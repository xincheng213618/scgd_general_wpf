using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ST.Library.UI.NodeEditor;

/// <summary>Opaque STND v1 node data, independent of the original node assembly.</summary>
public sealed class STNodeSavedState
{
    public const string PortLayoutKey = "__STNode.PortLayout";
    public const string LegacyPlaceholderKey = "__STNode.LegacyPlaceholder";
    internal const string LegacyConnectionsKey = "__STNode.LegacyConnections";
    public string ModelKey { get; }
    public string TypeKey { get; }
    public Dictionary<string, byte[]> Properties { get; }

    private STNodeSavedState(string modelKey, string typeKey, Dictionary<string, byte[]> properties)
    {
        ModelKey = modelKey;
        TypeKey = typeKey;
        Properties = properties;
    }

    public static STNodeSavedState Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Parse(data.AsSpan());
    }

    internal static STNodeSavedState Parse(ReadOnlySpan<byte> data)
    {
        string model = ReadText(ref data, ReadLengthByte(ref data));
        string type = ReadText(ref data, ReadLengthByte(ref data));
        var properties = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        while (!data.IsEmpty)
        {
            string name = ReadText(ref data, ReadLengthInt32(ref data));
            byte[] value = ReadBytes(ref data, ReadLengthInt32(ref data)).ToArray();
            if (!properties.TryAdd(name, value))
                throw new InvalidDataException($"节点数据包含重复属性：{name}");
        }
        return new STNodeSavedState(model, type, properties);
    }

    private static string ReadText(ref ReadOnlySpan<byte> data, int length) =>
        Encoding.UTF8.GetString(ReadBytes(ref data, length));

    private static byte ReadLengthByte(ref ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            ThrowTruncatedLength();
        byte length = data[0];
        data = data[1..];
        return length;
    }

    private static int ReadLengthInt32(ref ReadOnlySpan<byte> data)
    {
        if (data.Length < sizeof(int))
            ThrowTruncatedLength();
        int length = BinaryPrimitives.ReadInt32LittleEndian(data);
        data = data[sizeof(int)..];
        return length;
    }

    // ReadExactly supplies the same localized EOF exception as BinaryReader.
    private static void ThrowTruncatedLength() => Stream.Null.ReadExactly(stackalloc byte[1]);

    private static ReadOnlySpan<byte> ReadBytes(ref ReadOnlySpan<byte> data, int length)
    {
        if (length < 0 || length > data.Length)
            throw new InvalidDataException("节点属性长度无效。");
        ReadOnlySpan<byte> value = data[..length];
        data = data[length..];
        return value;
    }

    public static byte[] Write(string modelKey, string typeKey, IReadOnlyDictionary<string, byte[]> properties)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        foreach (string header in new[] { modelKey, typeKey })
        {
            byte[] value = Encoding.UTF8.GetBytes(header);
            if (value.Length > byte.MaxValue)
                throw new InvalidDataException("节点类型名称过长。");
            writer.Write((byte)value.Length);
            writer.Write(value);
        }
        foreach (var property in properties)
        {
            byte[] name = Encoding.UTF8.GetBytes(property.Key);
            writer.Write(name.Length);
            writer.Write(name);
            writer.Write(property.Value.Length);
            writer.Write(property.Value);
        }
        return stream.ToArray();
    }

    internal STNode CreateNode()
    {
        STNodeTypeRegistry.TryGetNodeType(TypeKey, ModelKey, out Type type);
        if (type == null)
            return new STMissingNode(this);
        var node = (STNode)Activator.CreateInstance(type);
        node.Create();
        node.OnLoadNode(Properties);
        return node;
    }

    public static STNodePortLayout ReadPortLayout(IReadOnlyDictionary<string, byte[]> properties)
    {
        if (!properties.TryGetValue(PortLayoutKey, out byte[] data))
            return null;
        try
        {
            var layout = JsonSerializer.Deserialize<STNodePortLayout>(data);
            if (layout == null || layout.Version != 1 || layout.Inputs == null || layout.Outputs == null
                || (long)layout.Inputs.Length + layout.Outputs.Length > 100_000
                || layout.Inputs.Concat(layout.Outputs).Any(port => port == null))
                throw new InvalidDataException("节点端口记录无效或超过限制。");
            return layout;
        }
        catch (JsonException ex) { throw new InvalidDataException("节点端口记录无效。", ex); }
    }

    internal static byte[] CapturePortLayout(STNode node) => JsonSerializer.SerializeToUtf8Bytes(new STNodePortLayout
    {
        Title = node.Title,
        Inputs = Capture(node.GetAllInputOptions()),
        Outputs = Capture(node.GetAllOutputOptions())
    });

    private static STNodeSavedPort[] Capture(STNodeOption[] ports) => ports.Select(port => new STNodeSavedPort
    {
        Name = port.Text,
        TypeName = port.DataType?.FullName,
        IsSingle = port.IsSingle,
        IsEmpty = ReferenceEquals(port, STNodeOption.Empty)
    }).ToArray();
}

public sealed class STNodePortLayout
{
    public int Version { get; init; } = 1;
    public string Title { get; init; }
    public STNodeSavedPort[] Inputs { get; init; }
    public STNodeSavedPort[] Outputs { get; init; }
}

public sealed class STNodeSavedPort
{
    public string Name { get; init; }
    public string TypeName { get; init; }
    public bool IsSingle { get; init; }
    public bool IsEmpty { get; init; }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ST.Library.UI.NodeEditor;

// STND v1 predates saved port layouts. Preserve its global indices rather than
// guessing how many ports an unavailable type used to have and miswiring neighbours.
internal sealed class STNodeLegacyConnections
{
    public STNodeLegacyConnections() { }
    public STNodeLegacySpan[] Nodes { get; init; }
    public long[] Connections { get; init; }

    public static STNodeLegacyConnections Capture(IReadOnlyList<STNode> nodes, long[] connections) => new()
    {
        Nodes = nodes.Select(node => new STNodeLegacySpan
        {
            NodeId = node.Guid,
            InputCount = node is STMissingNode { HasSavedPortLayout: false } ? -1 : node.GetAllInputOptions().Length,
            OutputCount = node is STMissingNode { HasSavedPortLayout: false } ? -1 : node.GetAllOutputOptions().Length
        }).ToArray(),
        Connections = connections
    };

    public byte[] Serialize() => JsonSerializer.SerializeToUtf8Bytes(this);

    public static STNodeLegacyConnections Deserialize(byte[] data)
    {
        try
        {
            var archive = JsonSerializer.Deserialize<STNodeLegacyConnections>(data);
            if (archive?.Nodes == null || archive.Connections == null || archive.Nodes.Length > 10_000
                || archive.Connections.Length > 100_000 || archive.Nodes.Any(node => node == null || node.NodeId == Guid.Empty
                    || node.InputCount < -1 || node.OutputCount < -1 || node.InputCount > 100_000 || node.OutputCount > 100_000)
                || archive.Nodes.Select(node => node.NodeId).Distinct().Count() != archive.Nodes.Length
                || archive.Connections.Any(packed => packed < 0 || (packed >> 32) >= 1_000_000 || unchecked((uint)packed) >= 1_000_000)
                || archive.Connections.Distinct().Count() != archive.Connections.Length)
                throw new InvalidDataException("旧流程连线记录无效。");
            return archive;
        }
        catch (JsonException ex) { throw new InvalidDataException("旧流程连线记录无效。", ex); }
    }

    public bool TryRestore(IReadOnlyList<STNode> nodes, List<STNodeCanvasReader.Connection> connections)
    {
        var byId = nodes.ToDictionary(node => node.Guid);
        var options = new List<STNodeOption>();
        var seen = new HashSet<STNodeOption>();
        foreach (STNodeLegacySpan span in Nodes)
        {
            if (!byId.TryGetValue(span.NodeId, out STNode node)
                || node is STMissingNode { HasSavedPortLayout: false }) return false;
            STNodeOption[] inputs = node.GetAllInputOptions(), outputs = node.GetAllOutputOptions();
            int inputCount = span.InputCount < 0 ? inputs.Length : span.InputCount;
            int outputCount = span.OutputCount < 0 ? outputs.Length : span.OutputCount;
            if (inputCount > inputs.Length || outputCount > outputs.Length)
                throw new InvalidDataException("原结点端口结构发生变化，无法恢复旧连线。");
            foreach (STNodeOption option in inputs.Take(inputCount).Concat(outputs.Take(outputCount)))
                if (seen.Add(option)) options.Add(option);
            if (options.Count > 1_000_000) throw new InvalidDataException("流程端口总数量超过限制。");
        }
        var restored = new List<STNodeCanvasReader.Connection>();
        foreach (long packed in Connections)
        {
            long outputIndex = packed >> 32, inputIndex = unchecked((uint)packed);
            if (outputIndex < 0 || outputIndex >= options.Count || inputIndex >= options.Count)
                throw new InvalidDataException("旧流程连线引用了不存在的端口。");
            STNodeOption output = options[(int)outputIndex], input = options[(int)inputIndex];
            if (output.IsInput || !input.IsInput || output.Owner == input.Owner)
                throw new InvalidDataException("旧流程连线方向无效。");
            restored.Add(new STNodeCanvasReader.Connection(output, input));
        }
        var existingConnections = connections.Select(connection => (connection.Output, connection.Input)).ToHashSet();
        foreach (var connection in restored)
            if (existingConnections.Add((connection.Output, connection.Input)))
                connections.Add(connection);
        return true;
    }
}

internal sealed class STNodeLegacySpan
{
    public STNodeLegacySpan() { }
    public Guid NodeId { get; init; }
    public int InputCount { get; init; }
    public int OutputCount { get; init; }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ST.Library.UI.NodeEditor;

/// <summary>
/// A detached canvas graph for configuration inspection. Reading it does not
/// connect options, attach the nodes to an editor, or complete editor loading.
/// Node creation and persisted-property loading still run so callers can inspect
/// the configured values.
/// </summary>
public sealed class STNodeCanvasSnapshot : IDisposable
{
    private readonly Dictionary<STNode, IReadOnlyList<STNode>> _outputNodes;
    private bool _isDisposed;

    internal STNodeCanvasSnapshot(STNodeCanvasReader.Document document)
    {
        Nodes = document.Nodes.ToArray();
        _outputNodes = document.Connections
            .GroupBy(connection => connection.Output.Owner)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<STNode>)group.Select(connection => connection.Input.Owner).ToArray());
    }

    public IReadOnlyList<STNode> Nodes { get; }

    public IReadOnlyList<STNode> GetConnectedOutputNodes(STNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _outputNodes.TryGetValue(node, out IReadOnlyList<STNode>? nodes)
            ? nodes
            : Array.Empty<STNode>();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        foreach (IDisposable node in Nodes.OfType<IDisposable>())
            node.Dispose();
    }
}

public partial class STNodeEditor
{
    public static STNodeCanvasSnapshot ReadCanvasSnapshot(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var stream = new MemoryStream(data, writable: false);
        return ReadCanvasSnapshot(stream);
    }

    public static STNodeCanvasSnapshot ReadCanvasSnapshot(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new STNodeCanvasSnapshot(STNodeCanvasReader.Read(stream));
    }
}

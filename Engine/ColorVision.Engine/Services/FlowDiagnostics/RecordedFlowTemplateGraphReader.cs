using ColorVision.Engine.Templates.Flow;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace ColorVision.Engine.Services.FlowDiagnostics;

public sealed record RecordedFlowTemplateNode(Guid NodeId, string ModelKey, string TypeKey, string? Title, IReadOnlyList<string> PropertyNames);

public sealed record RecordedFlowTemplateEdge(Guid SourceNodeId, int SourcePortIndex, Guid TargetNodeId, int TargetPortIndex);

public sealed record RecordedFlowTemplateGraph(
    IReadOnlyList<RecordedFlowTemplateNode> Nodes,
    IReadOnlyList<RecordedFlowTemplateEdge> Edges,
    int TotalConnectionCount,
    bool ConnectionsComplete);

/// <summary>Reads saved graph metadata without resolving or constructing any node implementation.</summary>
public static class RecordedFlowTemplateGraphReader
{
    private const int MaximumDecompressedLength = 32 * 1024 * 1024;
    private const int MaximumTotalPortCount = 1_000_000;
    private const int MaximumNodePortCount = 100_000;
    private const int MaximumNodePropertyCount = 4096;
    private const int MaximumTotalPropertyCount = 100_000;
    private const string LegacyConnectionsKey = "__STNode.LegacyConnections";

    public static RecordedFlowTemplateGraph Read(byte[] content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var body = FlowPackageStnValidator.ValidateAndDecompress(content, MaximumDecompressedLength, cancellationToken);
        using var reader = new BinaryReader(new MemoryStream(body, writable: false));
        reader.BaseStream.Position = 3 * sizeof(float);
        var nodeCount = reader.ReadInt32();
        var nodes = new List<RecordedFlowTemplateNode>(nodeCount);
        var nodeIds = new HashSet<Guid>();
        var ports = new List<SavedPort>();
        var totalPorts = 0;
        var totalProperties = 0;
        var hasEmpty = false;
        var hasLegacyConnectionState = false;
        var layoutsComplete = true;
        for (var index = 0; index < nodeCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = reader.ReadBytes(reader.ReadInt32());
            totalProperties += ValidatePropertyBudget(payload, MaximumTotalPropertyCount - totalProperties, cancellationToken);
            var state = STNodeSavedState.Parse(payload);
            if (!state.Properties.TryGetValue("Guid", out var guidBytes) || guidBytes.Length != 16)
                throw new InvalidDataException("The recorded flow node has no valid Guid.");
            var nodeId = new Guid(guidBytes);
            if (nodeId == Guid.Empty || !nodeIds.Add(nodeId))
                throw new InvalidDataException("Recorded flow node Guids must be nonempty and unique.");

            STNodePortLayout? layout = null;
            if (state.Properties.TryGetValue(STNodeSavedState.PortLayoutKey, out var layoutBytes))
            {
                ValidatePortBudget(layoutBytes, MaximumTotalPortCount - totalPorts, cancellationToken);
                layout = STNodeSavedState.ReadPortLayout(state.Properties)
                    ?? throw new InvalidDataException("The recorded flow saved port layout is missing.");
                totalPorts += layout.Inputs.Length + layout.Outputs.Length;
                AddPorts(ports, nodeId, layout.Inputs, isInput: true, ref hasEmpty);
                AddPorts(ports, nodeId, layout.Outputs, isInput: false, ref hasEmpty);
            }
            if (state.Properties.ContainsKey(STNodeSavedState.LegacyPlaceholderKey) || state.Properties.ContainsKey(LegacyConnectionsKey))
            {
                hasLegacyConnectionState = true;
                layoutsComplete = false;
            }
            if (layout == null)
                layoutsComplete = false;

            nodes.Add(new RecordedFlowTemplateNode(nodeId, state.ModelKey, state.TypeKey, layout?.Title,
                Array.AsReadOnly(state.Properties.Keys.Order(StringComparer.Ordinal).ToArray())));
        }

        var connectionCount = reader.ReadInt32();
        var edges = new List<RecordedFlowTemplateEdge>(layoutsComplete ? connectionCount : 0);
        var connections = new HashSet<long>();
        for (var index = 0; index < connectionCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var packed = reader.ReadInt64();
            var outputIndex = packed >> 32;
            var inputIndex = unchecked((uint)packed);
            if (outputIndex < 0 || outputIndex >= MaximumTotalPortCount || inputIndex >= MaximumTotalPortCount
                || !connections.Add(packed))
                throw new InvalidDataException("The recorded flow has invalid or duplicate connection indices.");
            if (!layoutsComplete)
                continue;
            if (outputIndex >= ports.Count || inputIndex >= ports.Count)
                throw new InvalidDataException("The recorded flow connection references a missing saved port.");
            var output = ports[(int)outputIndex];
            var input = ports[(int)inputIndex];
            if (output.IsInput || !input.IsInput || output.IsEmpty || input.IsEmpty || output.NodeId == input.NodeId)
                throw new InvalidDataException("The recorded flow connection has an invalid saved direction.");
            edges.Add(new RecordedFlowTemplateEdge(output.NodeId, output.LocalIndex, input.NodeId, input.LocalIndex));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new RecordedFlowTemplateGraph(nodes.AsReadOnly(), edges.AsReadOnly(), connectionCount,
            !hasLegacyConnectionState && (layoutsComplete || connectionCount == 0));
    }

    // Envelope validation has already checked every frame length. Count names without decoding or copying values.
    private static int ValidatePropertyBudget(ReadOnlySpan<byte> payload, int remainingProperties, CancellationToken cancellationToken)
    {
        var position = 1 + payload[0];
        position += 1 + payload[position];
        var properties = 0;
        while (position < payload.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++properties > Math.Min(MaximumNodePropertyCount, remainingProperties))
                throw new InvalidDataException("The recorded flow saved property count exceeds its limit.");
            var nameLength = BitConverter.ToInt32(payload.Slice(position, sizeof(int)));
            position += sizeof(int) + nameLength;
            var valueLength = BitConverter.ToInt32(payload.Slice(position, sizeof(int)));
            position += sizeof(int) + valueLength;
        }
        return properties;
    }

    private static void AddPorts(List<SavedPort> ports, Guid nodeId, STNodeSavedPort[] saved, bool isInput, ref bool hasEmpty)
    {
        for (var index = 0; index < saved.Length; index++)
        {
            if (saved[index].IsEmpty)
            {
                if (hasEmpty)
                    continue;
                hasEmpty = true;
            }
            ports.Add(new SavedPort(nodeId, index, isInput, saved[index].IsEmpty));
        }
    }

    // Check array sizes while streaming before the shared saved-layout reader allocates port objects.
    private static void ValidatePortBudget(byte[] data, int remainingPorts, CancellationToken cancellationToken)
    {
        try
        {
            var reader = new Utf8JsonReader(data);
            var ports = 0;
            var pendingPorts = false;
            var inPortArray = false;
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1)
                {
                    pendingPorts = reader.ValueTextEquals("Inputs") || reader.ValueTextEquals("Outputs");
                    continue;
                }
                if (pendingPorts)
                {
                    inPortArray = reader.TokenType == JsonTokenType.StartArray;
                    pendingPorts = false;
                }
                else if (inPortArray && reader.CurrentDepth == 2 && reader.TokenType != JsonTokenType.EndArray)
                {
                    // Each direct array value starts here; nested object properties have a greater depth.
                    if (reader.TokenType != JsonTokenType.EndObject && ++ports > Math.Min(MaximumNodePortCount, remainingPorts))
                        throw new InvalidDataException("The recorded flow saved port count exceeds its limit.");
                }
                if (reader.TokenType == JsonTokenType.EndArray && reader.CurrentDepth == 1)
                    inPortArray = false;
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The recorded flow saved port layout is invalid.", exception);
        }
    }

    private sealed record SavedPort(Guid NodeId, int LocalIndex, bool IsInput, bool IsEmpty);
}

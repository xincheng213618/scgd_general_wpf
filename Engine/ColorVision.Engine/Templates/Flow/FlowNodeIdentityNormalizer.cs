using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ColorVision.Engine.Templates.Flow;

/// <summary>Updates persisted type identities without constructing nodes or rewriting their properties.</summary>
internal static class FlowNodeIdentityNormalizer
{
    internal static string Normalize(string dataBase64, out int updatedNodes, out int unresolvedNodes)
    {
        updatedNodes = 0;
        unresolvedNodes = 0;
        if (dataBase64.Length > ((FlowPackageStnValidator.MaximumStndLength + 2L) / 3 * 4))
            throw new InvalidDataException("流程数据超过 STND 大小限制。");

        byte[] body = FlowPackageStnValidator.ValidateAndDecompress(Convert.FromBase64String(dataBase64));
        using var reader = new BinaryReader(new MemoryStream(body));
        float x = reader.ReadSingle(), y = reader.ReadSingle(), scale = reader.ReadSingle();
        int nodeCount = reader.ReadInt32();
        var payloads = new List<byte[]>(nodeCount);
        for (int i = 0; i < nodeCount; i++)
        {
            byte[] payload = reader.ReadBytes(reader.ReadInt32());
            int guidOffset = 1 + payload[0];
            int propertyOffset = guidOffset + 1 + payload[guidOffset];
            string model = Encoding.UTF8.GetString(payload, 1, payload[0]);
            string guid = Encoding.UTF8.GetString(payload, guidOffset + 1, payload[guidOffset]);
            if (!STNodeTypeRegistry.TryGetNodeType(guid, model, out Type type))
            {
                unresolvedNodes++;
            }
            else
            {
                string currentModel = STNodeTypeRegistry.GetModelByType(type);
                string currentGuid = type.GUID.ToString();
                if (model != currentModel || guid != currentGuid)
                {
                    byte[] modelBytes = Encoding.UTF8.GetBytes(currentModel);
                    byte[] guidBytes = Encoding.UTF8.GetBytes(currentGuid);
                    if (modelBytes.Length > byte.MaxValue || guidBytes.Length > byte.MaxValue)
                        throw new InvalidDataException("当前节点类型标识超过 STND v1 长度限制。");

                    using var node = new MemoryStream();
                    node.WriteByte((byte)modelBytes.Length);
                    node.Write(modelBytes);
                    node.WriteByte((byte)guidBytes.Length);
                    node.Write(guidBytes);
                    // Keep opaque plugin properties, node IDs and option order byte-for-byte.
                    node.Write(payload.AsSpan(propertyOffset));
                    payload = node.ToArray();
                    updatedNodes++;
                }
            }
            payloads.Add(payload);
        }

        if (updatedNodes == 0)
            return dataBase64;

        int connectionCount = reader.ReadInt32();
        var connections = new long[connectionCount];
        for (int i = 0; i < connectionCount; i++)
            connections[i] = reader.ReadInt64();
        using var output = new MemoryStream();
        STNodeCanvasWriter.WriteRaw(output, payloads, connections, x, y, scale);
        return Convert.ToBase64String(output.ToArray());
    }
}

using ColorVision.Engine.Templates.Flow.Routing;
using ColorVision.Engine.Templates.Flow.Search;
using ColorVision.Engine.Templates.Flow.Versioning;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FlowFailureKind = FlowEngineLib.Runtime.FlowFailureKind;

namespace ColorVision.Engine.FlowProcessing.Compilation;

public enum FlowCanvasCatalogError
{
    InvalidNodePayload,
    InvalidExecutionPolicy,
}

public sealed class FlowCanvasCatalogException : Exception
{
    public FlowCanvasCatalogException(
        FlowCanvasCatalogError error,
        string message)
        : base(message)
    {
        Error = error;
    }

    public FlowCanvasCatalogException(
        FlowCanvasCatalogError error,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Error = error;
    }

    public FlowCanvasCatalogError Error { get; }
}

public sealed record FlowCanvasCatalogBuildResult(
    FlowSemanticDocument SemanticDocument,
    IReadOnlyList<FlowNodeSearchDocument> SearchDocuments);

/// <summary>
/// Builds version/search projections directly from an STND v1 canvas. It
/// never materializes a live editor graph and never writes the source canvas.
/// </summary>
public sealed class FlowCanvasCatalogBuilder
{
    private const int MaximumSearchValueBytes = 4_096;
    private const string RootNodePathPrefix = "root/nodes/";

    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly HashSet<string> LayoutPropertyNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Guid",
            "Left",
            "Top",
            "Width",
            "Height",
        };

    private static readonly string[] SensitivePropertyMarkers =
    [
        "secret",
        "token",
        "password",
        "passwd",
        "payload",
        "credential",
        "authorization",
        "apikey",
        "privatekey",
        "cookie",
    ];

    private readonly StnV1CodecOptions options;

    public FlowCanvasCatalogBuilder(
        StnV1CodecOptions? options = null)
    {
        this.options = options ?? new StnV1CodecOptions();
    }

    public FlowCanvasCatalogBuildResult Build(
        byte[] canvasData,
        FlowExecutionPolicySnapshot? executionPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(canvasData);
        NeutralCanvas canvas = StnV1NeutralCodec.Decode(
            canvasData,
            options);

        var document = new FlowSemanticDocument
        {
            Layout = new FlowLayoutDocument
            {
                ViewportX = canvas.CanvasOffsetX,
                ViewportY = canvas.CanvasOffsetY,
                Scale = canvas.CanvasScale,
            },
        };
        var searchDocuments = new List<FlowNodeSearchDocument>(
            canvas.Nodes.Count);
        var nodesById = canvas.Nodes.ToDictionary(
            node => node.NodeId);

        foreach (NeutralNode node in canvas.Nodes
            .OrderBy(item => item.NodeId))
        {
            IReadOnlyDictionary<string, byte[]> properties =
                ParseProperties(node);
            document.Nodes.Add(CreateSemanticNode(node, properties));
            document.Layout.Nodes.Add(
                CreateLayoutNode(node, properties));
            searchDocuments.Add(
                CreateSearchDocument(node, properties));
        }

        foreach (NeutralConnection connection in canvas.Connections
            .OrderBy(item => item.Output.Node.NodeId)
            .ThenBy(item => item.Output.LocalIndex)
            .ThenBy(item => item.Input.Node.NodeId)
            .ThenBy(item => item.Input.LocalIndex))
        {
            document.Edges.Add(new FlowSemanticEdge
            {
                SourceNodeId =
                    connection.Output.Node.NodeId.ToString("D"),
                SourcePort = connection.Output.LocalIndex.ToString(
                    CultureInfo.InvariantCulture),
                TargetNodeId =
                    connection.Input.Node.NodeId.ToString("D"),
                TargetPort = connection.Input.LocalIndex.ToString(
                    CultureInfo.InvariantCulture),
            });
        }

        AddExecutionPolicy(
            document,
            nodesById,
            executionPolicy);
        FlowSemanticDocumentValidator.Validate(document);

        return new FlowCanvasCatalogBuildResult(
            document,
            new ReadOnlyCollection<FlowNodeSearchDocument>(
                searchDocuments));
    }

    private static FlowSemanticNode CreateSemanticNode(
        NeutralNode node,
        IReadOnlyDictionary<string, byte[]> properties)
    {
        var semanticProperties = new Dictionary<string, string?>(
            StringComparer.Ordinal);
        foreach (KeyValuePair<string, byte[]> property in properties
            .OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (LayoutPropertyNames.Contains(property.Key)
                || IsSensitivePropertyName(property.Key))
            {
                continue;
            }

            semanticProperties[property.Key] =
                Convert.ToHexString(
                    SHA256.HashData(property.Value))
                .ToLowerInvariant();
        }

        return new FlowSemanticNode
        {
            NodeId = node.NodeId.ToString("D"),
            TypeKey = node.TypeKey,
            Properties = semanticProperties,
        };
    }

    private static FlowNodeLayout CreateLayoutNode(
        NeutralNode node,
        IReadOnlyDictionary<string, byte[]> properties)
    {
        int width = ReadLayoutValue(
            node,
            properties,
            "Width");
        int height = ReadLayoutValue(
            node,
            properties,
            "Height");
        if (width <= 0 || height <= 0)
        {
            throw InvalidNode(
                node,
                "节点宽高必须大于零。");
        }

        return new FlowNodeLayout
        {
            NodeId = node.NodeId.ToString("D"),
            X = ReadLayoutValue(node, properties, "Left"),
            Y = ReadLayoutValue(node, properties, "Top"),
            Width = width,
            Height = height,
        };
    }

    private static FlowNodeSearchDocument CreateSearchDocument(
        NeutralNode node,
        IReadOnlyDictionary<string, byte[]> properties)
    {
        string? displayName = ReadSearchValue(
            properties,
            "DisplayName",
            maximumLength: 256)
            ?? ReadSearchValue(
                properties,
                "NodeName",
                maximumLength: 256);
        string nodeTypeKey =
            node.Schema.NodeType.FullName ?? node.TypeKey;
        nodeTypeKey = FlowSearchSafety.NormalizeRequiredSafeText(
            nodeTypeKey,
            nameof(nodeTypeKey),
            maximumLength: 256);

        return new FlowNodeSearchDocument
        {
            SourceNodeGuid = node.NodeId,
            NodePath = RootNodePathPrefix + node.NodeId.ToString("N"),
            NodeTypeKey = nodeTypeKey,
            DisplayName = displayName,
            Title = ReadSearchValue(
                properties,
                "Title",
                maximumLength: 256),
            TemplateName = ReadSearchValue(
                properties,
                "TemplateName",
                maximumLength: 256),
            DeviceCode = ReadSearchValue(
                properties,
                "DeviceCode",
                maximumLength: 128),
            ServiceCode = ReadSearchValue(
                properties,
                "ServiceCode",
                maximumLength: 128),
            Tags = Array.Empty<string>(),
        };
    }

    private static void AddExecutionPolicy(
        FlowSemanticDocument document,
        Dictionary<Guid, NeutralNode> nodesById,
        FlowExecutionPolicySnapshot? executionPolicy)
    {
        if (executionPolicy == null)
            return;

        foreach (FlowRetryPolicy retry in executionPolicy.RetryPolicies)
        {
            if (!Guid.TryParse(retry.NodeId, out Guid retryNodeId)
                || !nodesById.ContainsKey(retryNodeId))
            {
                throw new FlowCanvasCatalogException(
                    FlowCanvasCatalogError.InvalidExecutionPolicy,
                    $"重试策略引用了不存在的节点：{retry.NodeId}。");
            }
            document.RetryPolicies.Add(
                new FlowRetryPolicyReference
                {
                    NodeId = retryNodeId.ToString("D"),
                    MaxAttempts = retry.MaxAttempts,
                    InitialDelayMs = retry.InitialDelayMs,
                    Backoff = retry.Backoff,
                    MaxDelayMs = retry.MaxDelayMs,
                    RetryableKinds = retry.RetryableKinds
                        .Select(kind => kind.ToString())
                        .OrderBy(kind => kind, StringComparer.Ordinal)
                        .ToList(),
                });
        }

        foreach (FlowErrorRoutePolicy route in
            executionPolicy.ErrorRoutes
                .OrderBy(item => item.SourceNodeId, StringComparer.Ordinal)
                .ThenBy(item => item.TargetNodeId, StringComparer.Ordinal)
                .ThenBy(item => item.TargetInputIndex))
        {
            if (!Guid.TryParse(route.SourceNodeId, out Guid sourceNodeId)
                || !nodesById.ContainsKey(sourceNodeId))
            {
                throw new FlowCanvasCatalogException(
                    FlowCanvasCatalogError.InvalidExecutionPolicy,
                    $"错误路由引用了不存在的来源节点："
                    + $"{route.SourceNodeId}。");
            }
            if (!Guid.TryParse(route.TargetNodeId, out Guid targetNodeId)
                || !nodesById.TryGetValue(
                    targetNodeId,
                    out NeutralNode? targetNode))
            {
                throw new FlowCanvasCatalogException(
                    FlowCanvasCatalogError.InvalidExecutionPolicy,
                    $"错误路由引用了不存在的目标节点："
                    + $"{route.TargetNodeId}。");
            }
            if (route.TargetInputIndex < 0
                || route.TargetInputIndex >= targetNode.Inputs.Length)
            {
                throw new FlowCanvasCatalogException(
                    FlowCanvasCatalogError.InvalidExecutionPolicy,
                    $"错误路由目标输入不存在：{route.TargetNodeId}/"
                    + $"{route.TargetInputIndex}。");
            }

            foreach (FlowFailureKind kind in route.FailureKinds
                .OrderBy(item => item.ToString(), StringComparer.Ordinal))
            {
                if (!Enum.IsDefined(kind))
                {
                    throw new FlowCanvasCatalogException(
                        FlowCanvasCatalogError.InvalidExecutionPolicy,
                        $"错误路由包含无法识别的失败类型：{kind}。");
                }
                document.ErrorRoutes.Add(new FlowErrorRoute
                {
                    SourceNodeId = sourceNodeId.ToString("D"),
                    ErrorCode = kind.ToString(),
                    TargetNodeId = targetNodeId.ToString("D"),
                    TargetPort = $"in:{route.TargetInputIndex}",
                    IsInterrupting = true,
                });
            }
        }
    }

    private static string? ReadSearchValue(
        IReadOnlyDictionary<string, byte[]> properties,
        string propertyName,
        int maximumLength)
    {
        KeyValuePair<string, byte[]> match = properties.FirstOrDefault(
            item => string.Equals(
                item.Key,
                propertyName,
                StringComparison.OrdinalIgnoreCase));
        if (match.Key == null
            || match.Value.Length > MaximumSearchValueBytes)
        {
            return null;
        }

        string value;
        try
        {
            value = StrictUtf8.GetString(match.Value);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
        return FlowSearchSafety.NormalizeOptionalSafeText(
            value,
            maximumLength);
    }

    private static int ReadLayoutValue(
        NeutralNode node,
        IReadOnlyDictionary<string, byte[]> properties,
        string propertyName)
    {
        if (!properties.TryGetValue(
                propertyName,
                out byte[]? value)
            || value.Length != sizeof(int))
        {
            throw InvalidNode(
                node,
                $"布局属性 {propertyName} 缺失或长度无效。");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(value);
    }

    private static Dictionary<string, byte[]> ParseProperties(
        NeutralNode node)
    {
        try
        {
            byte[] payload = node.Payload;
            int offset = 0;
            SkipByteLengthString(payload, ref offset, "节点类型");
            SkipByteLengthString(payload, ref offset, "节点类型标识");
            var properties = new Dictionary<string, byte[]>(
                StringComparer.Ordinal);
            while (offset < payload.Length)
            {
                int nameLength = ReadInt32(
                    payload,
                    ref offset,
                    "属性名称长度");
                string name = StrictUtf8.GetString(
                    ReadBytes(
                        payload,
                        ref offset,
                        nameLength,
                        "属性名称"));
                int valueLength = ReadInt32(
                    payload,
                    ref offset,
                    "属性值长度");
                byte[] value = ReadBytes(
                    payload,
                    ref offset,
                    valueLength,
                    "属性值");
                if (!properties.TryAdd(name, value))
                {
                    throw new InvalidOperationException(
                        $"属性 {name} 重复。");
                }
            }
            return properties;
        }
        catch (Exception ex)
            when (ex is ArgumentException
                || ex is DecoderFallbackException
                || ex is InvalidOperationException)
        {
            throw InvalidNode(
                node,
                "无法读取节点属性。",
                ex);
        }
    }

    private static bool IsSensitivePropertyName(string propertyName)
    {
        string normalized = new(
            propertyName
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        return SensitivePropertyMarkers.Any(marker =>
            normalized.Contains(marker, StringComparison.Ordinal));
    }

    private static void SkipByteLengthString(
        byte[] payload,
        ref int offset,
        string fieldName)
    {
        if (offset >= payload.Length)
            throw new ArgumentException($"{fieldName}缺失。");
        int length = payload[offset++];
        _ = ReadBytes(
            payload,
            ref offset,
            length,
            fieldName);
    }

    private static int ReadInt32(
        byte[] payload,
        ref int offset,
        string fieldName)
    {
        byte[] value = ReadBytes(
            payload,
            ref offset,
            sizeof(int),
            fieldName);
        return BinaryPrimitives.ReadInt32LittleEndian(value);
    }

    private static byte[] ReadBytes(
        byte[] payload,
        ref int offset,
        int length,
        string fieldName)
    {
        if (length < 0
            || offset < 0
            || offset > payload.Length - length)
        {
            throw new ArgumentException(
                $"{fieldName}长度无效：{length}。");
        }

        byte[] value = payload.AsSpan(offset, length).ToArray();
        offset += length;
        return value;
    }

    private static FlowCanvasCatalogException InvalidNode(
        NeutralNode node,
        string message,
        Exception? innerException = null)
    {
        string fullMessage =
            $"节点 {node.NodeId:D} 的目录投影无效：{message}";
        return innerException == null
            ? new FlowCanvasCatalogException(
                FlowCanvasCatalogError.InvalidNodePayload,
                fullMessage)
            : new FlowCanvasCatalogException(
                FlowCanvasCatalogError.InvalidNodePayload,
                fullMessage,
                innerException);
    }
}

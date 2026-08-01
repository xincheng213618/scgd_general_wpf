using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Templates.Flow.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorVision.Engine.FlowProcessing.Artifacts;

/// <summary>
/// Stable byte representation of every blob-backed artifact part. Database
/// stores and package exporters should persist these bytes instead of defining
/// another JSON shape or canonicalization rule.
/// </summary>
public sealed class FlowArtifactSerializedParts
{
    private readonly byte[] authoringStn;
    private readonly byte[] compiledStn;
    private readonly byte[] manifest;
    private readonly byte[] subflowSidecar;
    private readonly byte[] authoringPolicy;
    private readonly byte[] effectivePolicy;
    private readonly byte[] compilationMap;

    internal FlowArtifactSerializedParts(
        byte[] authoringStn,
        byte[] compiledStn,
        byte[] manifest,
        byte[] subflowSidecar,
        byte[] authoringPolicy,
        byte[] effectivePolicy,
        byte[] compilationMap)
    {
        this.authoringStn = (byte[])authoringStn.Clone();
        this.compiledStn = (byte[])compiledStn.Clone();
        this.manifest = (byte[])manifest.Clone();
        this.subflowSidecar = (byte[])subflowSidecar.Clone();
        this.authoringPolicy = (byte[])authoringPolicy.Clone();
        this.effectivePolicy = (byte[])effectivePolicy.Clone();
        this.compilationMap = (byte[])compilationMap.Clone();
    }

    public byte[] AuthoringStn => (byte[])authoringStn.Clone();

    public byte[] CompiledStn => (byte[])compiledStn.Clone();

    public byte[] Manifest => (byte[])manifest.Clone();

    public byte[] SubflowSidecar => (byte[])subflowSidecar.Clone();

    public byte[] AuthoringPolicy => (byte[])authoringPolicy.Clone();

    public byte[] EffectivePolicy => (byte[])effectivePolicy.Clone();

    public byte[] CompilationMap => (byte[])compilationMap.Clone();
}

public static class FlowArtifactSerializer
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters =
            {
                new JsonStringEnumConverter(),
            },
        };

    public static FlowArtifactSerializedParts Serialize(
        FlowArtifactBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        FlowArtifactValidator.Validate(bundle);
        return new FlowArtifactSerializedParts(
            bundle.Draft.AuthoringStn,
            bundle.Executable.CompiledStn,
            SerializeManifest(bundle.Manifest),
            SerializeSubflowSidecar(
                bundle.Draft.SubflowSidecar),
            SerializePolicy(
                bundle.Draft.FlowKey,
                bundle.Draft.AuthoringPolicy.ErrorRoutes,
                bundle.Draft.AuthoringPolicy.RetryPolicies),
            SerializePolicy(
                bundle.Executable.EffectivePolicy.FlowKey,
                bundle.Executable.EffectivePolicy.ErrorRoutes,
                bundle.Executable.EffectivePolicy.RetryPolicies),
            SerializeCompilationMap(
                bundle.Executable.CompilationMap));
    }

    public static byte[] SerializeManifest(
        FlowArtifactManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.SerializeToUtf8Bytes(
            manifest,
            CanonicalJsonOptions);
    }

    public static FlowArtifactManifest DeserializeManifest(
        byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return JsonSerializer.Deserialize<FlowArtifactManifest>(
            content,
            CanonicalJsonOptions)
            ?? throw new JsonException("流程 artifact manifest 为空。");
    }

    public static byte[] SerializeSubflowSidecar(
        FlowSubflowSidecar sidecar)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        FlowSubflowSidecar normalized =
            FlowSubflowSidecarPersistence.Normalize(sidecar);
        return FlowSubflowSidecarPersistence.SerializeCanonical(
            normalized);
    }

    public static FlowSubflowSidecar DeserializeSubflowSidecar(
        byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        CanonicalSidecarDocument document =
            JsonSerializer.Deserialize<CanonicalSidecarDocument>(
                content,
                CanonicalJsonOptions)
            ?? throw new JsonException(
                "流程 artifact 子流程侧车为空。");
        IReadOnlyList<CanonicalCallDocument> calls =
            document.Calls
            ?? throw new JsonException(
                "流程 artifact 子流程调用集合为空。");
        return FlowSubflowSidecarPersistence.Normalize(
            new FlowSubflowSidecar(
                calls.Select(call =>
                {
                    if (!Guid.TryParseExact(
                            call.SourceNodeId,
                            "D",
                            out Guid sourceNodeId)
                        || !Guid.TryParseExact(
                            call.TargetNodeId,
                            "D",
                            out Guid targetNodeId))
                    {
                        throw new JsonException(
                            "流程 artifact 子流程端口包含无效节点 GUID。");
                    }
                    return new FlowSubflowCall(
                        call.CallId,
                        new FlowPortReference(
                            sourceNodeId,
                            call.SourceOptionIndex),
                        new FlowPortReference(
                            targetNodeId,
                            call.TargetOptionIndex),
                        new FlowDefinitionReference(
                            call.ChildFlowKey,
                            call.ChildRevision,
                            call.ChildContentHash));
                }).ToArray()));
    }

    public static byte[] SerializeAuthoringPolicy(
        FlowArtifactDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return SerializePolicy(
            draft.FlowKey,
            draft.AuthoringPolicy.ErrorRoutes,
            draft.AuthoringPolicy.RetryPolicies);
    }

    public static FlowArtifactPolicy DeserializeAuthoringPolicy(
        byte[] content,
        string expectedFlowKey)
    {
        PolicyDocument document =
            DeserializePolicyDocument(content);
        NormalizedFlowExecutionPolicy normalized =
            FlowExecutionPolicyRules.Normalize(
                document.FlowKey,
                document.ErrorRoutes,
                document.RetryPolicies);
        if (!string.Equals(
                normalized.FlowKey,
                expectedFlowKey,
                StringComparison.Ordinal))
        {
            throw new JsonException(
                "流程 artifact 作者策略的 FlowKey 不一致。");
        }
        return new FlowArtifactPolicy(
            normalized.ErrorRoutes,
            normalized.RetryPolicies);
    }

    public static byte[] SerializeEffectivePolicy(
        FlowExecutableBundle executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        return SerializePolicy(
            executable.EffectivePolicy.FlowKey,
            executable.EffectivePolicy.ErrorRoutes,
            executable.EffectivePolicy.RetryPolicies);
    }

    public static FlowExecutionPolicySnapshot DeserializeEffectivePolicy(
        byte[] content)
    {
        PolicyDocument document = DeserializePolicyDocument(content);
        NormalizedFlowExecutionPolicy normalized =
            FlowExecutionPolicyRules.Normalize(
                document.FlowKey,
                document.ErrorRoutes,
                document.RetryPolicies);
        return FlowArtifactCanonical.CreateSnapshot(normalized);
    }

    public static byte[] SerializeCompilationMap(
        FlowCompilationMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var document = new CompilationMapDocument
        {
            Nodes = map.Nodes
                .OrderBy(item => item.CompiledNodeId)
                .ThenBy(
                    item => item.LogicalPath,
                    StringComparer.Ordinal)
                .Select(item => new CompiledNodeDocument
                {
                    CompiledNodeId =
                        item.CompiledNodeId.ToString("D"),
                    SourceNodeId =
                        item.SourceNodeId.ToString("D"),
                    SourceFlowKey = item.SourceFlowKey,
                    SourceRevision = item.SourceRevision,
                    SourceContentHash =
                        item.SourceContentHash,
                    LogicalPath = item.LogicalPath,
                })
                .ToArray(),
            Calls = map.Calls
                .OrderBy(
                    item => item.LogicalCallPath,
                    StringComparer.Ordinal)
                .ThenBy(
                    item => item.ResolvedFlowKey,
                    StringComparer.Ordinal)
                .ThenBy(
                    item => item.ResolvedRevision,
                    StringComparer.Ordinal)
                .ThenBy(
                    item => item.ResolvedContentHash,
                    StringComparer.Ordinal)
                .Select(item => new CompiledCallDocument
                {
                    LogicalCallPath = item.LogicalCallPath,
                    RequestedFlowKey = item.Requested.FlowKey,
                    RequestedRevision =
                        item.Requested.Revision,
                    RequestedContentHash =
                        item.Requested.ContentHash,
                    ResolvedFlowKey =
                        item.ResolvedFlowKey,
                    ResolvedRevision =
                        item.ResolvedRevision,
                    ResolvedContentHash =
                        item.ResolvedContentHash,
                })
                .ToArray(),
        };
        return JsonSerializer.SerializeToUtf8Bytes(
            document,
            CanonicalJsonOptions);
    }

    public static FlowCompilationMap DeserializeCompilationMap(
        byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        CompilationMapDocument document =
            JsonSerializer.Deserialize<CompilationMapDocument>(
                content,
                CanonicalJsonOptions)
            ?? throw new JsonException(
                "流程 artifact compilation map 为空。");
        IReadOnlyList<CompiledNodeDocument> nodes =
            document.Nodes
            ?? throw new JsonException(
                "流程 artifact compilation map 节点集合为空。");
        IReadOnlyList<CompiledCallDocument> calls =
            document.Calls
            ?? throw new JsonException(
                "流程 artifact compilation map 调用集合为空。");
        return new FlowCompilationMap(
            nodes.Select(item => new FlowCompiledNodeMap(
                ParseGuid(item.CompiledNodeId, "compiledNodeId"),
                ParseGuid(item.SourceNodeId, "sourceNodeId"),
                item.SourceFlowKey,
                item.SourceRevision,
                item.SourceContentHash,
                item.LogicalPath)).ToArray(),
            calls.Select(item => new FlowCompiledCallMap(
                item.LogicalCallPath,
                new FlowDefinitionReference(
                    item.RequestedFlowKey,
                    item.RequestedRevision,
                    item.RequestedContentHash),
                item.ResolvedFlowKey,
                item.ResolvedRevision,
                item.ResolvedContentHash)).ToArray());
    }

    internal static byte[] SerializePolicy(
        string flowKey,
        IEnumerable<FlowErrorRoutePolicy> errorRoutes,
        IEnumerable<FlowRetryPolicy> retryPolicies)
    {
        NormalizedFlowExecutionPolicy normalized =
            FlowExecutionPolicyRules.Normalize(
                flowKey,
                errorRoutes,
                retryPolicies);
        var document = new PolicyDocument
        {
            FlowKey = normalized.FlowKey,
            ErrorRoutes = normalized.ErrorRoutes,
            RetryPolicies = normalized.RetryPolicies,
        };
        return JsonSerializer.SerializeToUtf8Bytes(
            document,
            CanonicalJsonOptions);
    }

    private static PolicyDocument DeserializePolicyDocument(
        byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        PolicyDocument document =
            JsonSerializer.Deserialize<PolicyDocument>(
                content,
                CanonicalJsonOptions)
            ?? throw new JsonException("流程 artifact 执行策略为空。");
        if (document.ErrorRoutes == null
            || document.RetryPolicies == null)
        {
            throw new JsonException(
                "流程 artifact 执行策略集合为空。");
        }
        return document;
    }

    private static Guid ParseGuid(
        string value,
        string fieldName)
    {
        if (!Guid.TryParseExact(value, "D", out Guid result))
        {
            throw new JsonException(
                $"流程 artifact {fieldName} 不是有效 GUID。");
        }
        return result;
    }

    private sealed class CanonicalSidecarDocument
    {
        public IReadOnlyList<CanonicalCallDocument> Calls { get; init; } =
            Array.Empty<CanonicalCallDocument>();
    }

    private sealed class CanonicalCallDocument
    {
        public string CallId { get; init; } = string.Empty;

        public string SourceNodeId { get; init; } = string.Empty;

        public int SourceOptionIndex { get; init; }

        public string TargetNodeId { get; init; } = string.Empty;

        public int TargetOptionIndex { get; init; }

        public string ChildFlowKey { get; init; } = string.Empty;

        public string? ChildRevision { get; init; }

        public string? ChildContentHash { get; init; }
    }

    private sealed class PolicyDocument
    {
        public string FlowKey { get; init; } = string.Empty;

        public IReadOnlyList<FlowErrorRoutePolicy> ErrorRoutes
        {
            get;
            init;
        } = Array.Empty<FlowErrorRoutePolicy>();

        public IReadOnlyList<FlowRetryPolicy> RetryPolicies
        {
            get;
            init;
        } = Array.Empty<FlowRetryPolicy>();
    }

    private sealed class CompilationMapDocument
    {
        public IReadOnlyList<CompiledNodeDocument> Nodes { get; init; } =
            Array.Empty<CompiledNodeDocument>();

        public IReadOnlyList<CompiledCallDocument> Calls { get; init; } =
            Array.Empty<CompiledCallDocument>();
    }

    private sealed class CompiledNodeDocument
    {
        public string CompiledNodeId { get; init; } = string.Empty;

        public string SourceNodeId { get; init; } = string.Empty;

        public string SourceFlowKey { get; init; } = string.Empty;

        public string? SourceRevision { get; init; }

        public string SourceContentHash { get; init; } = string.Empty;

        public string LogicalPath { get; init; } = string.Empty;
    }

    private sealed class CompiledCallDocument
    {
        public string LogicalCallPath { get; init; } = string.Empty;

        public string RequestedFlowKey { get; init; } = string.Empty;

        public string? RequestedRevision { get; init; }

        public string? RequestedContentHash { get; init; }

        public string ResolvedFlowKey { get; init; } = string.Empty;

        public string? ResolvedRevision { get; init; }

        public string ResolvedContentHash { get; init; } =
            string.Empty;
    }
}

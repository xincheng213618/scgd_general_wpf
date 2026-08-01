using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Templates.Flow.Routing;
using ColorVision.Engine.Templates.Flow.Versioning;
using FlowEngineLib.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Artifacts;

/// <summary>
/// Produces an immutable authoring definition plus a legacy-compatible,
/// editor-free executable bundle.
/// </summary>
public sealed class FlowArtifactBuilder
{
    public const int CurrentFormatVersion = 1;
    public const string CompilerName =
        "ColorVision.FlowArtifactCompiler";
    public const string CompilerVersion = "1";

    private readonly IFlowArtifactDependencyResolver dependencyResolver;
    private readonly FlowSubflowCompilerOptions options;

    public FlowArtifactBuilder(
        IFlowArtifactDependencyResolver? dependencyResolver = null,
        FlowSubflowCompilerOptions? options = null)
    {
        this.dependencyResolver = dependencyResolver
            ?? MissingDependencyResolver.Instance;
        this.options = options ?? new FlowSubflowCompilerOptions();
    }

    public FlowArtifactBundle Build(FlowArtifactDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        PreparedDefinition root = PrepareRoot(draft);
        var resolver = new CapturingResolver(
            dependencyResolver,
            options);

        FlowCompilationResult compilation;
        try
        {
            compilation = new FlowSubflowCompiler(
                resolver,
                options).Compile(
                    root.AuthoringStn,
                    root.Sidecar,
                    root.FlowKey,
                    root.Revision);
        }
        catch (FlowArtifactException)
        {
            throw;
        }
        catch (FlowCompilationException ex)
        {
            throw new FlowArtifactException(
                FlowArtifactError.CompilationFailed,
                "compiledStn",
                "流程 artifact 编译失败。",
                ex);
        }

        // A legacy flow without authoring-only calls must retain its exact
        // STND bytes. Re-encoding gzip data would otherwise create needless
        // binary revisions even though the graph is unchanged.
        byte[] compiledStn = root.Sidecar.Calls.Count == 0
            ? (byte[])root.AuthoringStn.Clone()
            : (byte[])compilation.CanvasData.Clone();
        ValidateCompiledStn(compiledStn);

        IReadOnlyList<FlowArtifactDependencyLock> dependencies =
            CreateDependencyLocks(compilation.Map, resolver);
        FlowExecutionPolicySnapshot effectivePolicy =
            BuildEffectivePolicy(
                root,
                resolver.Definitions,
                compilation.Map);
        var executable = new FlowExecutableBundle(
            compiledStn,
            effectivePolicy,
            compilation.Map,
            dependencies);
        FlowArtifactCompilerDescriptor compiler =
            CreateCompilerDescriptor(options);
        string compilerHash =
            FlowArtifactCanonical.ComputeCompilerHash(compiler);
        string dependencyHash =
            FlowArtifactCanonical.ComputeDependencyHash(dependencies);
        string compiledHash =
            FlowArtifactCanonical.ComputeHash(compiledStn);
        string effectivePolicyHash =
            FlowArtifactCanonical.ComputeHash(
                FlowArtifactSerializer.SerializeEffectivePolicy(
                    executable));
        string mapHash =
            FlowArtifactCanonical.ComputeCompilationMapHash(
                compilation.Map);

        var incompleteManifest = new FlowArtifactManifest(
            CurrentFormatVersion,
            root.FlowKey,
            root.Revision,
            root.SourceHash,
            root.SubflowHash,
            root.PolicyHash,
            root.SemanticHash,
            root.LayoutHash,
            root.DefinitionHash,
            dependencyHash,
            compiledHash,
            effectivePolicyHash,
            mapHash,
            compilerHash,
            ArtifactHash: string.Empty,
            compiler);
        FlowArtifactManifest manifest = incompleteManifest with
        {
            ArtifactHash =
                FlowArtifactCanonical.ComputeArtifactHash(
                    incompleteManifest),
        };
        var normalizedDraft = new FlowArtifactDraft(
            root.FlowKey,
            root.Revision,
            root.AuthoringStn,
            root.Sidecar,
            new FlowArtifactPolicy(
                root.Policy.ErrorRoutes,
                root.Policy.RetryPolicies));
        var bundle = new FlowArtifactBundle(
            normalizedDraft,
            manifest,
            executable);
        FlowArtifactValidator.Validate(bundle);
        return bundle;
    }

    internal static FlowArtifactCompilerDescriptor CreateCompilerDescriptor(
        FlowSubflowCompilerOptions options)
    {
        return new FlowArtifactCompilerDescriptor(
            CompilerName,
            CompilerVersion,
            StndVersion: 1,
            options.MaximumDepth,
            options.MaximumNodeCount,
            options.MaximumConnectionCount);
    }

    internal static PreparedDefinition PrepareDefinition(
        string flowKey,
        string? revision,
        byte[] authoringStn,
        FlowSubflowSidecar sidecar,
        FlowArtifactPolicy policy,
        FlowSubflowCompilerOptions options,
        string component)
    {
        string normalizedFlowKey;
        string? normalizedRevision;
        FlowSubflowSidecar normalizedSidecar;
        NormalizedFlowExecutionPolicy normalizedPolicy;
        try
        {
            normalizedFlowKey =
                FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
            normalizedRevision = NormalizeRevision(revision);
            normalizedSidecar =
                FlowSubflowSidecarPersistence.Normalize(sidecar);
            normalizedPolicy =
                FlowArtifactCanonical.NormalizePolicy(
                    normalizedFlowKey,
                    policy);
        }
        catch (Exception ex) when (
            ex is ArgumentException
            or InvalidOperationException)
        {
            throw new FlowArtifactException(
                FlowArtifactError.InvalidDraft,
                component,
                $"流程 artifact 的 {component} 定义无效。",
                ex);
        }

        byte[] source = (byte[])authoringStn.Clone();
        var normalizedArtifactPolicy = new FlowArtifactPolicy(
            normalizedPolicy.ErrorRoutes,
            normalizedPolicy.RetryPolicies);
        FlowExecutionPolicySnapshot policySnapshot =
            FlowArtifactCanonical.CreateSnapshot(normalizedPolicy);
        FlowCanvasCatalogBuildResult catalog;
        try
        {
            catalog = new FlowCanvasCatalogBuilder(options).Build(
                source,
                normalizedSidecar,
                policySnapshot);
        }
        catch (Exception ex) when (
            ex is FlowCompilationException
            or FlowCanvasCatalogException
            or ArgumentException)
        {
            throw new FlowArtifactException(
                FlowArtifactError.InvalidAuthoringCanvas,
                component,
                $"流程 artifact 的 {component} STND 或侧车无效。",
                ex);
        }

        string sourceHash =
            FlowArtifactCanonical.ComputeHash(source);
        string subflowHash =
            FlowArtifactCanonical.ComputeHash(
                FlowArtifactSerializer.SerializeSubflowSidecar(
                    normalizedSidecar));
        string policyHash =
            FlowArtifactCanonical.ComputePolicyHash(
                normalizedFlowKey,
                normalizedArtifactPolicy);
        if (!string.Equals(
                policyHash,
                normalizedPolicy.ContentHash,
                StringComparison.Ordinal))
        {
            throw new FlowArtifactException(
                FlowArtifactError.InvalidManifest,
                component,
                "执行策略 canonical bytes 与策略内容哈希不一致。");
        }
        string semanticHash =
            FlowSemanticHash.ComputeSemanticHash(
                catalog.SemanticDocument);
        string layoutHash =
            FlowSemanticHash.ComputeLayoutHash(
                catalog.SemanticDocument);
        string definitionHash =
            FlowArtifactCanonical.ComputeDefinitionHash(
                normalizedFlowKey,
                normalizedRevision,
                sourceHash,
                subflowHash,
                policyHash,
                semanticHash,
                layoutHash);
        return new PreparedDefinition(
            normalizedFlowKey,
            normalizedRevision,
            source,
            normalizedSidecar,
            normalizedArtifactPolicy,
            sourceHash,
            subflowHash,
            policyHash,
            semanticHash,
            layoutHash,
            definitionHash);
    }

    private PreparedDefinition PrepareRoot(FlowArtifactDraft draft)
    {
        return PrepareDefinition(
            draft.FlowKey,
            draft.Revision,
            draft.AuthoringStn,
            draft.SubflowSidecar,
            draft.AuthoringPolicy,
            options,
            "root");
    }

    private void ValidateCompiledStn(byte[] compiledStn)
    {
        try
        {
            _ = StnV1NeutralCodec.Decode(compiledStn, options);
        }
        catch (FlowCompilationException ex)
        {
            throw new FlowArtifactException(
                FlowArtifactError.InvalidCompiledCanvas,
                "compiledStn",
                "编译输出不是有效的 STND v1 流程。",
                ex);
        }
    }

    private static IReadOnlyList<FlowArtifactDependencyLock>
        CreateDependencyLocks(
            FlowCompilationMap map,
            CapturingResolver resolver)
    {
        var locks = new List<FlowArtifactDependencyLock>(
            map.Calls.Count);
        foreach (FlowCompiledCallMap call in map.Calls
            .OrderBy(item => item.LogicalCallPath, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(call.ResolvedRevision)
                || string.IsNullOrWhiteSpace(call.ResolvedContentHash))
            {
                throw new FlowArtifactException(
                    FlowArtifactError.UnpinnedDependency,
                    "dependencies",
                    $"子流程 {call.ResolvedFlowKey} 未解析到具体 revision 和 hash。");
            }

            PreparedDefinition definition = resolver.Find(
                call.ResolvedFlowKey,
                call.ResolvedRevision,
                call.ResolvedContentHash);
            locks.Add(new FlowArtifactDependencyLock(
                call.LogicalCallPath,
                definition.FlowKey,
                definition.Revision!,
                definition.SourceHash,
                definition.DefinitionHash));
        }
        return locks.AsReadOnly();
    }

    private static FlowExecutionPolicySnapshot BuildEffectivePolicy(
        PreparedDefinition root,
        IReadOnlyList<PreparedDefinition> dependencies,
        FlowCompilationMap map)
    {
        var routes = new List<FlowErrorRoutePolicy>();
        var retries = new List<FlowRetryPolicy>();
        ApplyPolicy(root, map, routes, retries);
        foreach (PreparedDefinition dependency in dependencies)
            ApplyPolicy(dependency, map, routes, retries);

        NormalizedFlowExecutionPolicy normalized;
        try
        {
            normalized = FlowExecutionPolicyRules.Normalize(
                root.FlowKey,
                routes,
                retries);
        }
        catch (ArgumentException ex)
        {
            throw new FlowArtifactException(
                FlowArtifactError.PolicyMappingUnavailable,
                "effectivePolicy",
                "编译后的执行策略无法规范化。",
                ex);
        }
        return FlowArtifactCanonical.CreateSnapshot(normalized);
    }

    private static void ApplyPolicy(
        PreparedDefinition definition,
        FlowCompilationMap map,
        ICollection<FlowErrorRoutePolicy> routes,
        ICollection<FlowRetryPolicy> retries)
    {
        if (definition.Policy.ErrorRoutes.Count == 0
            && definition.Policy.RetryPolicies.Count == 0)
        {
            return;
        }

        FlowCompiledNodeMap[] definitionMaps = map.Nodes
            .Where(item =>
                string.Equals(
                    item.SourceFlowKey,
                    definition.FlowKey,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    item.SourceRevision,
                    definition.Revision,
                    StringComparison.Ordinal)
                && string.Equals(
                    item.SourceContentHash,
                    definition.SourceHash,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (FlowRetryPolicy retry in
            definition.Policy.RetryPolicies)
        {
            Guid sourceNodeId = ParsePolicyNodeId(
                retry.NodeId,
                definition);
            FlowCompiledNodeMap[] matches = definitionMaps
                .Where(item => item.SourceNodeId == sourceNodeId)
                .ToArray();
            EnsureMapped(matches, definition, retry.NodeId);
            foreach (FlowCompiledNodeMap match in matches)
            {
                retries.Add(new FlowRetryPolicy(
                    match.CompiledNodeId.ToString("D"),
                    retry.MaxAttempts,
                    retry.InitialDelayMs,
                    retry.Backoff,
                    retry.MaxDelayMs,
                    retry.RetryableKinds));
            }
        }

        foreach (FlowErrorRoutePolicy route in
            definition.Policy.ErrorRoutes)
        {
            Guid sourceNodeId = ParsePolicyNodeId(
                route.SourceNodeId,
                definition);
            Guid targetNodeId = ParsePolicyNodeId(
                route.TargetNodeId,
                definition);
            FlowCompiledNodeMap[] sourceMaps = definitionMaps
                .Where(item => item.SourceNodeId == sourceNodeId)
                .ToArray();
            FlowCompiledNodeMap[] targetMaps = definitionMaps
                .Where(item => item.SourceNodeId == targetNodeId)
                .ToArray();
            EnsureMapped(sourceMaps, definition, route.SourceNodeId);
            EnsureMapped(targetMaps, definition, route.TargetNodeId);

            foreach (FlowCompiledNodeMap source in sourceMaps)
            {
                string instancePath = GetDefinitionInstancePath(
                    source.LogicalPath);
                FlowCompiledNodeMap[] matchingTargets = targetMaps
                    .Where(item => string.Equals(
                        GetDefinitionInstancePath(item.LogicalPath),
                        instancePath,
                        StringComparison.Ordinal))
                    .ToArray();
                if (matchingTargets.Length != 1)
                {
                    throw PolicyMappingFailure(
                        definition,
                        route.TargetNodeId,
                        $"调用实例 {instancePath} 中找到 "
                        + $"{matchingTargets.Length} 个目标映射");
                }
                routes.Add(new FlowErrorRoutePolicy(
                    source.CompiledNodeId.ToString("D"),
                    matchingTargets[0].CompiledNodeId.ToString("D"),
                    route.TargetInputIndex,
                    route.FailureKinds));
            }
        }
    }

    private static void EnsureMapped(
        IReadOnlyCollection<FlowCompiledNodeMap> matches,
        PreparedDefinition definition,
        string nodeId)
    {
        if (matches.Count == 0)
        {
            throw PolicyMappingFailure(
                definition,
                nodeId,
                "编译映射中不存在该节点；边界节点策略不能被静默丢弃");
        }
    }

    private static FlowArtifactException PolicyMappingFailure(
        PreparedDefinition definition,
        string nodeId,
        string reason)
    {
        return new FlowArtifactException(
            FlowArtifactError.PolicyMappingUnavailable,
            "effectivePolicy",
            $"流程 {definition.FlowKey}@{definition.Revision ?? "<draft>"} "
            + $"的节点 {nodeId} 无法映射执行策略：{reason}。");
    }

    private static Guid ParsePolicyNodeId(
        string value,
        PreparedDefinition definition)
    {
        if (Guid.TryParse(value, out Guid nodeId))
            return nodeId;
        throw PolicyMappingFailure(
            definition,
            value,
            "节点 ID 不是 GUID");
    }

    private static string GetDefinitionInstancePath(string logicalPath)
    {
        int index = logicalPath.LastIndexOf(
            "/nodes/",
            StringComparison.Ordinal);
        if (index <= 0)
        {
            throw new FlowArtifactException(
                FlowArtifactError.PolicyMappingUnavailable,
                "compilationMap",
                $"节点映射路径无效：{logicalPath}。");
        }
        return logicalPath[..index];
    }

    private static string? NormalizeRevision(string? revision)
    {
        if (string.IsNullOrWhiteSpace(revision))
            return null;
        string normalized = revision.Trim();
        if (normalized.Length > 128
            || normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                "artifact revision 无效。",
                nameof(revision));
        }
        return normalized;
    }

    internal sealed record PreparedDefinition(
        string FlowKey,
        string? Revision,
        byte[] AuthoringStn,
        FlowSubflowSidecar Sidecar,
        FlowArtifactPolicy Policy,
        string SourceHash,
        string SubflowHash,
        string PolicyHash,
        string SemanticHash,
        string LayoutHash,
        string DefinitionHash);

    private sealed class CapturingResolver : IFlowSubflowResolver
    {
        private readonly IFlowArtifactDependencyResolver inner;
        private readonly FlowSubflowCompilerOptions options;
        private readonly Dictionary<RequestCacheKey, PreparedDefinition>
            requestCache = new();
        private readonly Dictionary<DefinitionIdentityKey, PreparedDefinition>
            identityCache = new();

        public CapturingResolver(
            IFlowArtifactDependencyResolver inner,
            FlowSubflowCompilerOptions options)
        {
            this.inner = inner;
            this.options = options;
        }

        public IReadOnlyList<PreparedDefinition> Definitions =>
            identityCache.Values
                .OrderBy(item => item.FlowKey, StringComparer.Ordinal)
                .ThenBy(item => item.Revision, StringComparer.Ordinal)
                .ThenBy(item => item.SourceHash, StringComparer.Ordinal)
                .ToArray();

        public ResolvedFlowDefinition? Resolve(
            FlowDefinitionReference reference)
        {
            RequestCacheKey requestKey = CreateRequestKey(reference);
            if (!requestCache.TryGetValue(
                requestKey,
                out PreparedDefinition? prepared))
            {
                FlowArtifactDependencyDefinition? resolved;
                try
                {
                    resolved = inner.Resolve(reference);
                }
                catch (FlowArtifactException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new FlowArtifactException(
                        FlowArtifactError.MissingDependency,
                        "dependencies",
                        $"解析子流程 {reference.FlowKey} 失败。",
                        ex);
                }
                if (resolved == null)
                {
                    throw new FlowArtifactException(
                        FlowArtifactError.MissingDependency,
                        "dependencies",
                        $"找不到子流程 {reference.FlowKey}。");
                }
                prepared = PrepareResolved(reference, resolved);
                requestCache.Add(requestKey, prepared);
            }

            return new ResolvedFlowDefinition(
                prepared.FlowKey,
                prepared.Revision,
                prepared.SourceHash,
                (byte[])prepared.AuthoringStn.Clone(),
                prepared.Sidecar);
        }

        public PreparedDefinition Find(
            string flowKey,
            string revision,
            string contentHash)
        {
            DefinitionIdentityKey identityKey = CreateIdentityKey(
                flowKey,
                revision,
                contentHash);
            if (identityCache.TryGetValue(
                identityKey,
                out PreparedDefinition? definition))
            {
                return definition;
            }
            throw new FlowArtifactException(
                FlowArtifactError.DependencyIdentityMismatch,
                "dependencies",
                $"编译映射引用了未捕获的依赖 "
                + $"{flowKey}@{revision}#{contentHash}。");
        }

        private PreparedDefinition PrepareResolved(
            FlowDefinitionReference requested,
            FlowArtifactDependencyDefinition resolved)
        {
            string requestedFlowKey;
            string resolvedFlowKey;
            try
            {
                requestedFlowKey =
                    FlowRevisionStoreRules.NormalizeFlowKey(
                        requested.FlowKey);
                resolvedFlowKey =
                    FlowRevisionStoreRules.NormalizeFlowKey(
                        resolved.FlowKey);
            }
            catch (ArgumentException ex)
            {
                throw new FlowArtifactException(
                    FlowArtifactError.DependencyIdentityMismatch,
                    "dependencies",
                    "子流程 FlowKey 无效。",
                    ex);
            }
            if (!string.Equals(
                requestedFlowKey,
                resolvedFlowKey,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new FlowArtifactException(
                    FlowArtifactError.DependencyIdentityMismatch,
                    "dependencies",
                    $"请求 {requestedFlowKey}，解析器返回 "
                    + $"{resolvedFlowKey}。");
            }

            string revision = resolved.Revision.Trim();
            if (revision.Length == 0)
            {
                throw new FlowArtifactException(
                    FlowArtifactError.UnpinnedDependency,
                    "dependencies",
                    $"子流程 {resolvedFlowKey} 没有具体 revision。");
            }
            if (!string.IsNullOrWhiteSpace(requested.Revision)
                && !string.Equals(
                    requested.Revision.Trim(),
                    revision,
                    StringComparison.Ordinal))
            {
                throw new FlowArtifactException(
                    FlowArtifactError.DependencyIdentityMismatch,
                    "dependencies",
                    $"子流程 {resolvedFlowKey} 请求 revision "
                    + $"{requested.Revision}，解析为 {revision}。");
            }

            string actualHash =
                FlowArtifactCanonical.ComputeHash(
                    resolved.AuthoringStn);
            ValidateOptionalHash(
                requested.ContentHash,
                actualHash,
                resolvedFlowKey,
                "请求");
            ValidateOptionalHash(
                resolved.ContentHash,
                actualHash,
                resolvedFlowKey,
                "解析");
            PreparedDefinition prepared = PrepareDefinition(
                resolvedFlowKey,
                revision,
                resolved.AuthoringStn,
                resolved.SubflowSidecar,
                resolved.AuthoringPolicy,
                options,
                $"dependency:{resolvedFlowKey}@{revision}");
            DefinitionIdentityKey identityKey = CreateIdentityKey(
                prepared.FlowKey,
                prepared.Revision!,
                prepared.SourceHash);
            if (identityCache.TryGetValue(
                identityKey,
                out PreparedDefinition? existing))
            {
                if (!string.Equals(
                    existing.DefinitionHash,
                    prepared.DefinitionHash,
                    StringComparison.Ordinal))
                {
                    throw new FlowArtifactException(
                        FlowArtifactError.NondeterministicDependency,
                        "dependencies",
                        $"依赖 {prepared.FlowKey}@{prepared.Revision}"
                        + $"#{prepared.SourceHash} 在同一次构建中返回了"
                        + "不同定义。");
                }
                return existing;
            }
            identityCache.Add(identityKey, prepared);
            return prepared;
        }

        private static void ValidateOptionalHash(
            string? declared,
            string actual,
            string flowKey,
            string source)
        {
            if (string.IsNullOrWhiteSpace(declared))
                return;
            string normalized = declared.Trim();
            const string prefix = "sha256:";
            if (normalized.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[prefix.Length..];
            }
            if (!string.Equals(
                normalized,
                actual,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new FlowArtifactException(
                    FlowArtifactError.DependencyContentMismatch,
                    "dependencies",
                    $"子流程 {flowKey} 的{source} hash 为 "
                    + $"{normalized}，实际为 {actual}。");
            }
        }

        private static RequestCacheKey CreateRequestKey(
            FlowDefinitionReference reference)
        {
            return new RequestCacheKey(
                reference.FlowKey.Trim().ToUpperInvariant(),
                reference.Revision?.Trim(),
                reference.ContentHash?.Trim().ToLowerInvariant());
        }

        private static DefinitionIdentityKey CreateIdentityKey(
            string flowKey,
            string revision,
            string contentHash)
        {
            return new DefinitionIdentityKey(
                flowKey.ToUpperInvariant(),
                revision,
                contentHash.ToLowerInvariant());
        }

        private readonly record struct RequestCacheKey(
            string FlowKey,
            string? Revision,
            string? ContentHash);

        private readonly record struct DefinitionIdentityKey(
            string FlowKey,
            string Revision,
            string ContentHash);
    }

    private sealed class MissingDependencyResolver :
        IFlowArtifactDependencyResolver
    {
        public static MissingDependencyResolver Instance { get; } =
            new();

        public FlowArtifactDependencyDefinition? Resolve(
            FlowDefinitionReference reference)
        {
            return null;
        }
    }
}

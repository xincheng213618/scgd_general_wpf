using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Templates.Flow.Routing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Artifacts;

/// <summary>
/// Recomputes every portable hash and validates that the executable payload is
/// structurally valid STND v1. Validation never loads a live editor graph.
/// </summary>
public static class FlowArtifactValidator
{
    public static void Validate(FlowArtifactBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (bundle.Draft == null
            || bundle.Manifest == null
            || bundle.Executable == null)
        {
            throw new FlowArtifactException(
                FlowArtifactError.InvalidManifest,
                "bundle",
                "artifact bundle 缺少 draft、manifest 或 executable。");
        }

        FlowSubflowCompilerOptions options =
            ValidateCompiler(bundle.Manifest);
        FlowArtifactBuilder.PreparedDefinition root =
            FlowArtifactBuilder.PrepareDefinition(
                bundle.Draft.FlowKey,
                bundle.Draft.Revision,
                bundle.Draft.AuthoringStn,
                bundle.Draft.SubflowSidecar,
                bundle.Draft.AuthoringPolicy,
                options,
                "root");
        ValidateManifestIdentity(bundle.Manifest, root);
        ValidateDependencies(
            bundle.Manifest,
            bundle.Executable.Dependencies);
        ValidateCompiledStn(
            bundle.Manifest,
            bundle.Draft,
            bundle.Executable,
            options);
        ValidateEffectivePolicy(
            bundle.Manifest,
            root.FlowKey,
            bundle.Executable.EffectivePolicy,
            bundle.Executable.CompiledStn,
            options);
        ValidateCompilationMap(
            bundle.Manifest,
            bundle.Executable.CompilationMap,
            bundle.Executable.Dependencies,
            bundle.Executable.CompiledStn,
            options);
        RequireHash(
            "artifact",
            bundle.Manifest.ArtifactHash,
            FlowArtifactCanonical.ComputeArtifactHash(
                bundle.Manifest));
    }

    private static FlowSubflowCompilerOptions ValidateCompiler(
        FlowArtifactManifest manifest)
    {
        if (manifest.FormatVersion
                != FlowArtifactBuilder.CurrentFormatVersion
            || manifest.Compiler == null
            || !string.Equals(
                manifest.Compiler.Name,
                FlowArtifactBuilder.CompilerName,
                StringComparison.Ordinal)
            || !string.Equals(
                manifest.Compiler.Version,
                FlowArtifactBuilder.CompilerVersion,
                StringComparison.Ordinal)
            || manifest.Compiler.StndVersion != 1
            || manifest.Compiler.MaximumDepth <= 0
            || manifest.Compiler.MaximumDepth
                > FlowSubflowCompilerOptions.DefaultMaximumDepth
            || manifest.Compiler.MaximumNodeCount <= 0
            || manifest.Compiler.MaximumNodeCount
                > FlowSubflowCompilerOptions.DefaultMaximumNodeCount
            || manifest.Compiler.MaximumConnectionCount <= 0
            || manifest.Compiler.MaximumConnectionCount
                > FlowSubflowCompilerOptions
                    .DefaultMaximumConnectionCount)
        {
            throw new FlowArtifactException(
                FlowArtifactError.InvalidManifest,
                "compiler",
                "artifact compiler 描述或格式版本不受支持。");
        }

        RequireHash(
            "compiler",
            manifest.CompilerHash,
            FlowArtifactCanonical.ComputeCompilerHash(
                manifest.Compiler));
        return new FlowSubflowCompilerOptions
        {
            MaximumDepth = manifest.Compiler.MaximumDepth,
            MaximumNodeCount =
                manifest.Compiler.MaximumNodeCount,
            MaximumConnectionCount =
                manifest.Compiler.MaximumConnectionCount,
        };
    }

    private static void ValidateManifestIdentity(
        FlowArtifactManifest manifest,
        FlowArtifactBuilder.PreparedDefinition root)
    {
        if (!string.Equals(
                manifest.FlowKey,
                root.FlowKey,
                StringComparison.Ordinal)
            || !string.Equals(
                manifest.Revision,
                root.Revision,
                StringComparison.Ordinal))
        {
            throw new FlowArtifactException(
                FlowArtifactError.InvalidManifest,
                "definition",
                "manifest 标识与 authoring draft 不一致。");
        }

        RequireHash("source", manifest.SourceHash, root.SourceHash);
        RequireHash(
            "subflow",
            manifest.SubflowHash,
            root.SubflowHash);
        RequireHash("policy", manifest.PolicyHash, root.PolicyHash);
        RequireHash(
            "semantic",
            manifest.SemanticHash,
            root.SemanticHash);
        RequireHash(
            "layout",
            manifest.LayoutHash,
            root.LayoutHash);
        RequireHash(
            "definition",
            manifest.DefinitionHash,
            root.DefinitionHash);
    }

    private static void ValidateDependencies(
        FlowArtifactManifest manifest,
        IReadOnlyList<FlowArtifactDependencyLock> dependencies)
    {
        var logicalPaths = new HashSet<string>(
            StringComparer.Ordinal);
        foreach (FlowArtifactDependencyLock dependency in dependencies)
        {
            if (string.IsNullOrWhiteSpace(dependency.LogicalCallPath)
                || string.IsNullOrWhiteSpace(dependency.FlowKey)
                || string.IsNullOrWhiteSpace(dependency.Revision)
                || !logicalPaths.Add(dependency.LogicalCallPath))
            {
                throw new FlowArtifactException(
                    FlowArtifactError.UnpinnedDependency,
                    "dependencies",
                    "artifact 依赖缺少具体标识或调用路径重复。");
            }
            ValidateHashShape(
                dependency.ContentHash,
                "dependency.content");
            ValidateHashShape(
                dependency.DefinitionHash,
                "dependency.definition");
        }

        RequireHash(
            "dependencies",
            manifest.DependencyHash,
            FlowArtifactCanonical.ComputeDependencyHash(
                dependencies));
    }

    private static void ValidateCompiledStn(
        FlowArtifactManifest manifest,
        FlowArtifactDraft draft,
        FlowExecutableBundle executable,
        FlowSubflowCompilerOptions options)
    {
        RequireHash(
            "compiledStn",
            manifest.CompiledStnHash,
            FlowArtifactCanonical.ComputeHash(
                executable.CompiledStn));
        if (draft.SubflowSidecar.Calls.Count == 0
            && !draft.AuthoringStn.AsSpan().SequenceEqual(
                executable.CompiledStn))
        {
            throw new FlowArtifactException(
                FlowArtifactError.HashMismatch,
                "compiledStn",
                "无子流程的 legacy artifact 未保留原始 STND bytes。");
        }

        try
        {
            _ = StnV1NeutralCodec.Decode(
                executable.CompiledStn,
                options);
        }
        catch (FlowCompilationException ex)
        {
            throw new FlowArtifactException(
                FlowArtifactError.InvalidCompiledCanvas,
                "compiledStn",
                "artifact executable 不是有效 STND v1。",
                ex);
        }
    }

    private static void ValidateEffectivePolicy(
        FlowArtifactManifest manifest,
        string flowKey,
        FlowExecutionPolicySnapshot policy,
        byte[] compiledStn,
        FlowSubflowCompilerOptions options)
    {
        if (!string.Equals(
                policy.FlowKey,
                flowKey,
                StringComparison.Ordinal))
        {
            throw new FlowArtifactException(
                FlowArtifactError.InvalidManifest,
                "effectivePolicy",
                "effective policy 的 FlowKey 与 artifact 不一致。");
        }

        NormalizedFlowExecutionPolicy normalized;
        try
        {
            normalized = FlowExecutionPolicyRules.Normalize(
                policy.FlowKey,
                policy.ErrorRoutes,
                policy.RetryPolicies);
        }
        catch (ArgumentException ex)
        {
            throw new FlowArtifactException(
                FlowArtifactError.InvalidManifest,
                "effectivePolicy",
                "effective policy 无效。",
                ex);
        }
        RequireHash(
            "effectivePolicy.snapshot",
            policy.ContentHash,
            normalized.ContentHash);
        RequireHash(
            "effectivePolicy",
            manifest.EffectivePolicyHash,
            FlowArtifactCanonical.ComputeHash(
                FlowArtifactSerializer.SerializePolicy(
                    policy.FlowKey,
                    policy.ErrorRoutes,
                    policy.RetryPolicies)));

        NeutralCanvas canvas = StnV1NeutralCodec.Decode(
            compiledStn,
            options);
        Dictionary<Guid, NeutralNode> nodesById =
            canvas.Nodes.ToDictionary(item => item.NodeId);
        foreach (FlowRetryPolicy retry in policy.RetryPolicies)
        {
            if (!Guid.TryParse(retry.NodeId, out Guid nodeId)
                || !nodesById.ContainsKey(nodeId))
            {
                throw new FlowArtifactException(
                    FlowArtifactError.InvalidManifest,
                    "effectivePolicy",
                    $"重试策略引用了 executable 中不存在的节点："
                    + $"{retry.NodeId}。");
            }
        }
        foreach (FlowErrorRoutePolicy route in policy.ErrorRoutes)
        {
            if (!Guid.TryParse(
                    route.SourceNodeId,
                    out Guid sourceNodeId)
                || !nodesById.ContainsKey(sourceNodeId)
                || !Guid.TryParse(
                    route.TargetNodeId,
                    out Guid targetNodeId)
                || !nodesById.TryGetValue(
                    targetNodeId,
                    out NeutralNode? target)
                || route.TargetInputIndex < 0
                || route.TargetInputIndex >= target.Inputs.Length)
            {
                throw new FlowArtifactException(
                    FlowArtifactError.InvalidManifest,
                    "effectivePolicy",
                    $"错误路由不属于 executable："
                    + $"{route.SourceNodeId} -> "
                    + $"{route.TargetNodeId}/"
                    + $"{route.TargetInputIndex}。");
            }
        }
    }

    private static void ValidateCompilationMap(
        FlowArtifactManifest manifest,
        FlowCompilationMap map,
        IReadOnlyList<FlowArtifactDependencyLock> dependencies,
        byte[] compiledStn,
        FlowSubflowCompilerOptions options)
    {
        RequireHash(
            "compilationMap",
            manifest.CompilationMapHash,
            FlowArtifactCanonical.ComputeCompilationMapHash(map));

        NeutralCanvas canvas;
        try
        {
            canvas = StnV1NeutralCodec.Decode(
                compiledStn,
                options);
        }
        catch (FlowCompilationException ex)
        {
            throw new FlowArtifactException(
                FlowArtifactError.InvalidCompiledCanvas,
                "compilationMap",
                "无法验证 compilation map。",
                ex);
        }
        Guid[] canvasNodeIds = canvas.Nodes
            .Select(item => item.NodeId)
            .OrderBy(item => item)
            .ToArray();
        Guid[] mappedNodeIds = map.Nodes
            .Select(item => item.CompiledNodeId)
            .OrderBy(item => item)
            .ToArray();
        if (!canvasNodeIds.SequenceEqual(mappedNodeIds)
            || mappedNodeIds.Distinct().Count()
                != mappedNodeIds.Length)
        {
            throw new FlowArtifactException(
                FlowArtifactError.InvalidManifest,
                "compilationMap",
                "compilation map 与 executable 节点集合不一致。");
        }
        ValidateMapAttribution(
            manifest,
            map,
            dependencies);

        var callPaths = new HashSet<string>(
            StringComparer.Ordinal);
        Dictionary<string, FlowArtifactDependencyLock> locksByPath =
            dependencies.ToDictionary(
                item => item.LogicalCallPath,
                StringComparer.Ordinal);
        foreach (FlowCompiledCallMap call in map.Calls)
        {
            if (string.IsNullOrWhiteSpace(call.LogicalCallPath)
                || string.IsNullOrWhiteSpace(call.ResolvedFlowKey)
                || string.IsNullOrWhiteSpace(call.ResolvedRevision)
                || string.IsNullOrWhiteSpace(
                    call.ResolvedContentHash)
                || !callPaths.Add(call.LogicalCallPath))
            {
                throw new FlowArtifactException(
                    FlowArtifactError.UnpinnedDependency,
                    "compilationMap",
                    "compilation map 包含未锁定或重复的子流程调用。");
            }
            if (!locksByPath.TryGetValue(
                    call.LogicalCallPath,
                    out FlowArtifactDependencyLock? dependency)
                || !string.Equals(
                    dependency.FlowKey,
                    call.ResolvedFlowKey,
                    StringComparison.Ordinal)
                || !string.Equals(
                    dependency.Revision,
                    call.ResolvedRevision,
                    StringComparison.Ordinal)
                || !string.Equals(
                    dependency.ContentHash,
                    call.ResolvedContentHash,
                    StringComparison.Ordinal))
            {
                throw new FlowArtifactException(
                    FlowArtifactError.InvalidManifest,
                    "dependencies",
                    $"依赖锁与 compilation map 不一致："
                    + $"{call.LogicalCallPath}。");
            }
        }
        if (callPaths.Count != locksByPath.Count)
        {
            throw new FlowArtifactException(
                FlowArtifactError.InvalidManifest,
                "dependencies",
                "依赖锁数量与 compilation map 不一致。");
        }
    }

    private static void ValidateMapAttribution(
        FlowArtifactManifest manifest,
        FlowCompilationMap map,
        IReadOnlyList<FlowArtifactDependencyLock> dependencies)
    {
        FlowArtifactDependencyLock[] locksByDescendingPath =
            dependencies
                .OrderByDescending(
                    item => item.LogicalCallPath.Length)
                .ToArray();
        foreach (FlowCompiledNodeMap node in map.Nodes)
        {
            string expectedSuffix =
                $"/nodes/{node.SourceNodeId:N}";
            if (string.IsNullOrWhiteSpace(node.LogicalPath)
                || !node.LogicalPath.EndsWith(
                    expectedSuffix,
                    StringComparison.Ordinal))
            {
                throw InvalidMapAttribution(node);
            }

            if (node.LogicalPath.StartsWith(
                    "$root/nodes/",
                    StringComparison.Ordinal))
            {
                if (!string.Equals(
                        node.SourceFlowKey,
                        manifest.FlowKey,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        node.SourceRevision,
                        manifest.Revision,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        node.SourceContentHash,
                        manifest.SourceHash,
                        StringComparison.Ordinal))
                {
                    throw InvalidMapAttribution(node);
                }
                continue;
            }

            FlowArtifactDependencyLock? dependency =
                locksByDescendingPath.FirstOrDefault(item =>
                    node.LogicalPath.StartsWith(
                        item.LogicalCallPath + "/",
                        StringComparison.Ordinal));
            if (dependency == null
                || !string.Equals(
                    node.SourceFlowKey,
                    dependency.FlowKey,
                    StringComparison.Ordinal)
                || !string.Equals(
                    node.SourceRevision,
                    dependency.Revision,
                    StringComparison.Ordinal)
                || !string.Equals(
                    node.SourceContentHash,
                    dependency.ContentHash,
                    StringComparison.Ordinal))
            {
                throw InvalidMapAttribution(node);
            }
        }
    }

    private static FlowArtifactException InvalidMapAttribution(
        FlowCompiledNodeMap node)
    {
        return new FlowArtifactException(
            FlowArtifactError.InvalidManifest,
            "compilationMap",
            $"节点 {node.CompiledNodeId} 的来源归因无效："
            + $"{node.LogicalPath}。");
    }

    private static void RequireHash(
        string component,
        string declared,
        string actual)
    {
        ValidateHashShape(declared, component);
        if (!string.Equals(
                declared,
                actual,
                StringComparison.Ordinal))
        {
            throw new FlowArtifactException(
                FlowArtifactError.HashMismatch,
                component,
                $"{component} hash 不匹配；声明 {declared}，"
                + $"实际 {actual}。");
        }
    }

    private static void ValidateHashShape(
        string value,
        string component)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length != 64
            || value.Any(item => !Uri.IsHexDigit(item))
            || !string.Equals(
                value,
                value.ToLowerInvariant(),
                StringComparison.Ordinal))
        {
            throw new FlowArtifactException(
                FlowArtifactError.InvalidManifest,
                component,
                $"{component} 不是规范的小写 SHA-256。");
        }
    }
}

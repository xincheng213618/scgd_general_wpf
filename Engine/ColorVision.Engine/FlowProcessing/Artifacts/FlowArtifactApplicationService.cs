using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Artifacts.Persistence;
using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Flow.Routing;
using ColorVision.Engine.Templates.Flow.Versioning;
using FlowEngineLib.Base;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Engine.FlowProcessing.Artifacts;

/// <summary>
/// Validated, immutable payload loaded from a published artifact revision.
/// It can be handed directly to the headless execution service.
/// </summary>
public sealed class FlowPublishedExecutable
{
    private readonly byte[] compiledStn;
    private readonly byte[] compilationMap;

    internal FlowPublishedExecutable(
        FlowArtifactRevision revision,
        FlowArtifactManifest manifest,
        byte[] compiledStn,
        FlowExecutionPolicySnapshot executionPolicy,
        byte[] compilationMap,
        bool hasSubflows,
        bool requiresServices)
    {
        Revision = revision.DeepClone();
        Manifest = manifest;
        this.compiledStn = (byte[])compiledStn.Clone();
        ExecutionPolicy = executionPolicy;
        this.compilationMap = (byte[])compilationMap.Clone();
        HasSubflows = hasSubflows;
        RequiresServices = requiresServices;
    }

    public FlowArtifactRevision Revision { get; }

    public FlowArtifactManifest Manifest { get; }

    public byte[] CompiledStn => (byte[])compiledStn.Clone();

    public FlowExecutionPolicySnapshot ExecutionPolicy { get; }

    public byte[] CompilationMap => (byte[])compilationMap.Clone();

    public bool HasSubflows { get; }

    public bool RequiresServices { get; }

    /// <summary>
    /// Verifies that this published payload was compiled from the exact
    /// catalog revision and STN snapshot currently selected by the caller.
    /// A mismatch must not fall back to the authoring STN when it contains
    /// subflow calls because that STN deliberately has no compiled children.
    /// </summary>
    public bool IsCompatibleWith(
        FlowParam? flowParam,
        out string failureReason)
    {
        if (flowParam == null)
        {
            failureReason = "当前流程为空。";
            return false;
        }
        if (string.IsNullOrWhiteSpace(flowParam.FlowKey)
            || !string.Equals(
                Manifest.FlowKey,
                flowParam.FlowKey.Trim(),
                StringComparison.Ordinal))
        {
            failureReason = "已发布 artifact 的 FlowKey 与当前流程不一致。";
            return false;
        }
        if (flowParam.TemplateRevision is not int revision
            || revision <= 0
            || !string.Equals(
                Manifest.Revision,
                revision.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
        {
            failureReason = "已发布 artifact 不是当前流程版本。";
            return false;
        }
        if (string.IsNullOrWhiteSpace(flowParam.DataBase64))
        {
            failureReason = "当前流程没有 STN 数据。";
            return false;
        }

        string currentHash;
        try
        {
            currentHash = FlowArtifactCanonical.ComputeHash(
                Convert.FromBase64String(flowParam.DataBase64));
        }
        catch (FormatException)
        {
            failureReason = "当前流程的 STN 数据不是有效的 Base64。";
            return false;
        }
        if (!string.Equals(
                Manifest.SourceHash,
                currentHash,
                StringComparison.Ordinal)
            || (!string.IsNullOrWhiteSpace(
                    flowParam.TemplateContentHash)
                && !string.Equals(
                    currentHash,
                    flowParam.TemplateContentHash.Trim(),
                    StringComparison.OrdinalIgnoreCase)))
        {
            failureReason = "已发布 artifact 的源 STN 与当前流程不一致。";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }
}

public enum FlowRuntimeArtifactResolutionKind
{
    Legacy,
    LegacyRequiresLocalSubflowCheck,
    Published,
    Blocked,
}

/// <summary>
/// Shared-store authority decision for one execution attempt. A draft without
/// subflows can still use the authoring STN in the UI execution path; any
/// subflow-bearing revision requires a validated published executable.
/// </summary>
public sealed class FlowRuntimeArtifactResolution
{
    private FlowRuntimeArtifactResolution(
        FlowRuntimeArtifactResolutionKind kind,
        string flowKey,
        string currentSourceHash,
        FlowArtifactRevision? head,
        FlowPublishedExecutable? executable,
        string? failureReason)
    {
        Kind = kind;
        FlowKey = flowKey;
        CurrentSourceHash = currentSourceHash;
        Head = head?.DeepClone();
        Executable = executable;
        FailureReason = failureReason;
    }

    public FlowRuntimeArtifactResolutionKind Kind { get; }

    public string FlowKey { get; }

    public string CurrentSourceHash { get; }

    public FlowArtifactRevision? Head { get; }

    public FlowPublishedExecutable? Executable { get; }

    public string? FailureReason { get; }

    internal static FlowRuntimeArtifactResolution Legacy(
        string flowKey,
        string currentSourceHash,
        FlowArtifactRevision? head = null)
    {
        return new FlowRuntimeArtifactResolution(
            FlowRuntimeArtifactResolutionKind.Legacy,
            flowKey,
            currentSourceHash,
            head,
            executable: null,
            failureReason: null);
    }

    internal static FlowRuntimeArtifactResolution
        LegacyRequiresLocalSubflowCheck(
            string flowKey,
            string currentSourceHash,
            FlowArtifactRevision head)
    {
        return new FlowRuntimeArtifactResolution(
            FlowRuntimeArtifactResolutionKind
                .LegacyRequiresLocalSubflowCheck,
            flowKey,
            currentSourceHash,
            head,
            executable: null,
            failureReason: null);
    }

    internal static FlowRuntimeArtifactResolution Published(
        string flowKey,
        string currentSourceHash,
        FlowArtifactRevision head,
        FlowPublishedExecutable executable)
    {
        return new FlowRuntimeArtifactResolution(
            FlowRuntimeArtifactResolutionKind.Published,
            flowKey,
            currentSourceHash,
            head,
            executable,
            failureReason: null);
    }

    internal static FlowRuntimeArtifactResolution Blocked(
        string flowKey,
        string currentSourceHash,
        FlowArtifactRevision? head,
        string failureReason)
    {
        return new FlowRuntimeArtifactResolution(
            FlowRuntimeArtifactResolutionKind.Blocked,
            flowKey,
            currentSourceHash,
            head,
            executable: null,
            failureReason);
    }
}

internal static class FlowRuntimeArtifactFallbackPolicy
{
    public static bool CanUseLegacy(
        FlowRuntimeArtifactResolutionKind kind,
        bool? currentRevisionHasSubflows,
        out string? failureReason)
    {
        if (kind is not (
                FlowRuntimeArtifactResolutionKind.Legacy
                or FlowRuntimeArtifactResolutionKind
                    .LegacyRequiresLocalSubflowCheck))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "当前 Artifact 解析结果不是 legacy fallback。");
        }

        if (currentRevisionHasSubflows == true)
        {
            failureReason =
                "当前流程已配置子流程，但共享 Artifact 尚未创建。"
                + "请重新保存并发布后再运行。";
            return false;
        }
        if (kind == FlowRuntimeArtifactResolutionKind
                .LegacyRequiresLocalSubflowCheck
            && !currentRevisionHasSubflows.HasValue)
        {
            failureReason =
                "共享 Artifact 与当前 STN 不一致，且无法验证当前"
                + "本地版本是否包含子流程。请先保存并重新发布。";
            return false;
        }

        failureReason = null;
        return true;
    }
}

/// <summary>
/// Product-facing orchestration around deterministic artifact compilation and
/// the revision store. The legacy STN is an input blob and is never rewritten.
/// </summary>
public sealed class FlowArtifactApplicationService : IDisposable
{
    private readonly IFlowArtifactStore store;
    private readonly FlowCatalogService catalog;
    private readonly IFlowSubflowDefinitionStore subflowStore;
    private readonly IFlowExecutionPolicyStore policyStore;
    private readonly IDisposable? ownedResource;
    private bool disposed;

    public FlowArtifactApplicationService(
        IFlowArtifactStore store,
        FlowCatalogService catalog,
        IFlowSubflowDefinitionStore subflowStore,
        IFlowExecutionPolicyStore policyStore)
        : this(
            store,
            catalog,
            subflowStore,
            policyStore,
            ownedResource: null)
    {
    }

    internal FlowArtifactApplicationService(
        IFlowArtifactStore store,
        FlowCatalogService catalog,
        IFlowSubflowDefinitionStore subflowStore,
        IFlowExecutionPolicyStore policyStore,
        IDisposable? ownedResource)
    {
        this.store = store
            ?? throw new ArgumentNullException(nameof(store));
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
        this.subflowStore = subflowStore
            ?? throw new ArgumentNullException(nameof(subflowStore));
        this.policyStore = policyStore
            ?? throw new ArgumentNullException(nameof(policyStore));
        this.ownedResource = ownedResource;
    }

    public FlowArtifactBundle Build(
        FlowParam flowParam,
        FlowSubflowSidecar? subflowSidecar = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(flowParam);
        if (string.IsNullOrWhiteSpace(flowParam.FlowKey))
        {
            throw new InvalidOperationException(
                "当前流程没有稳定 FlowKey，不能生成 artifact。");
        }
        if (string.IsNullOrWhiteSpace(flowParam.DataBase64))
        {
            throw new InvalidOperationException(
                "当前流程没有可编译的 STN 数据。");
        }

        byte[] authoringStn =
            Convert.FromBase64String(flowParam.DataBase64);
        FlowSubflowSidecar sidecar =
            subflowSidecar
            ?? LoadSidecar(flowParam);
        FlowExecutionPolicySnapshot policy =
            LoadPolicy(flowParam.FlowKey);
        var draft = new FlowArtifactDraft(
            flowParam.FlowKey,
            flowParam.TemplateRevision?.ToString(
                CultureInfo.InvariantCulture),
            authoringStn,
            sidecar,
            new FlowArtifactPolicy(
                policy.ErrorRoutes,
                policy.RetryPolicies));
        return new FlowArtifactBuilder(
            new CatalogDependencyResolver(
                catalog,
                subflowStore,
                policyStore))
            .Build(draft);
    }

    public FlowArtifactRevision SaveDraft(
        FlowParam flowParam,
        FlowSubflowSidecar? subflowSidecar = null,
        string? author = null,
        string? message = null)
    {
        return Save(
            Build(flowParam, subflowSidecar),
            publishImmediately: false,
            author,
            message);
    }

    public FlowArtifactRevision SavePublished(
        FlowParam flowParam,
        FlowSubflowSidecar? subflowSidecar = null,
        string? author = null,
        string? message = null)
    {
        return Save(
            Build(flowParam, subflowSidecar),
            publishImmediately: true,
            author,
            message);
    }

    public FlowArtifactRevision PublishHead(
        string flowKey,
        string? actor = null,
        string? message = null)
    {
        ThrowIfDisposed();
        FlowArtifactRevision head = store.GetHead(flowKey)
            ?? throw new InvalidOperationException(
                $"流程 {flowKey} 还没有可发布的 artifact 草稿。");
        if (head.State == FlowArtifactRevisionState.Published)
            return head;
        return store.Publish(
            new FlowArtifactRevisionTransitionRequest
            {
                FlowKey = head.FlowKey,
                Revision = head.Revision,
                ExpectedHead =
                    FlowArtifactHeadCondition.FromRevision(head),
                Actor = actor,
                Message = message,
            });
    }

    public FlowSubflowSidecar GetAuthoringSidecar(
        string flowKey,
        int revision)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
        return subflowStore.GetRevision(flowKey, revision)
            ?.Sidecar
            ?? FlowSubflowSidecar.Empty;
    }

    public bool HasAuthoringSubflows(
        string flowKey,
        int revision)
    {
        return GetAuthoringSidecar(
            flowKey,
            revision).Calls.Count > 0;
    }

    public FlowPublishedExecutable GetPublishedExecutable(
        string flowKey)
    {
        ThrowIfDisposed();
        FlowArtifactRevision revision =
            store.GetPublished(flowKey)
            ?? throw new InvalidOperationException(
                $"流程 {flowKey} 没有已发布的 artifact。");
        return ReadValidatedExecutable(revision);
    }

    /// <summary>
    /// Resolves execution exclusively from the shared artifact authority.
    /// Local catalog revisions and sidecars are intentionally not consulted.
    /// </summary>
    public FlowRuntimeArtifactResolution ResolveForExecution(
        string flowKey,
        byte[] currentAuthoringStn)
    {
        ThrowIfDisposed();
        string normalizedFlowKey =
            FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
        ArgumentNullException.ThrowIfNull(currentAuthoringStn);
        if (currentAuthoringStn.Length == 0)
        {
            throw new ArgumentException(
                "当前流程 STN 不能为空。",
                nameof(currentAuthoringStn));
        }
        string currentSourceHash =
            FlowArtifactCanonical.ComputeHash(currentAuthoringStn);

        FlowArtifactRevision? head = null;
        try
        {
            FlowArtifactReference? reference =
                store.GetReference(normalizedFlowKey);
            if (reference == null)
            {
                return FlowRuntimeArtifactResolution.Legacy(
                    normalizedFlowKey,
                    currentSourceHash);
            }
            if (reference.LastRevision <= 0)
            {
                return FlowRuntimeArtifactResolution.Blocked(
                    normalizedFlowKey,
                    currentSourceHash,
                    head: null,
                    "共享 Artifact 引用存在，但没有有效版本。");
            }

            head = store.GetHead(normalizedFlowKey);
            if (head == null)
            {
                return FlowRuntimeArtifactResolution.Blocked(
                    normalizedFlowKey,
                    currentSourceHash,
                    head: null,
                    "共享 Artifact 已有版本历史，但当前 head 不存在。");
            }
            if (reference.HeadRevision != head.Revision
                || !string.Equals(
                    reference.HeadRevisionHash,
                    head.RevisionHash,
                    StringComparison.Ordinal))
            {
                return FlowRuntimeArtifactResolution.Blocked(
                    normalizedFlowKey,
                    currentSourceHash,
                    head,
                    "共享 Artifact 引用与当前 head 不一致。");
            }

            FlowPublishedExecutable executable =
                ReadValidatedExecutable(head);
            if (!string.Equals(
                    executable.Manifest.SourceHash,
                    currentSourceHash,
                    StringComparison.Ordinal))
            {
                if (!executable.HasSubflows)
                {
                    return FlowRuntimeArtifactResolution
                        .LegacyRequiresLocalSubflowCheck(
                        normalizedFlowKey,
                        currentSourceHash,
                        head);
                }
                return FlowRuntimeArtifactResolution.Blocked(
                    normalizedFlowKey,
                    currentSourceHash,
                    head,
                    "当前 STN 与共享 Artifact head 的源内容不一致；"
                    + "请先保存并发布当前流程。");
            }

            if (head.State != FlowArtifactRevisionState.Published)
            {
                if (!executable.HasSubflows)
                {
                    return FlowRuntimeArtifactResolution.Legacy(
                        normalizedFlowKey,
                        currentSourceHash,
                        head);
                }
                return FlowRuntimeArtifactResolution.Blocked(
                    normalizedFlowKey,
                    currentSourceHash,
                    head,
                    "当前匹配的 Artifact 草稿包含子流程，"
                    + "必须先发布后才能执行。");
            }

            FlowArtifactRevision? published =
                store.GetPublished(normalizedFlowKey);
            if (published == null
                || published.Revision != head.Revision
                || !string.Equals(
                    published.RevisionHash,
                    head.RevisionHash,
                    StringComparison.Ordinal)
                || reference.PublishedRevision
                    != published.Revision
                || !string.Equals(
                    reference.PublishedRevisionHash,
                    published.RevisionHash,
                    StringComparison.Ordinal))
            {
                return FlowRuntimeArtifactResolution.Blocked(
                    normalizedFlowKey,
                    currentSourceHash,
                    head,
                    "共享 Artifact 的 published 指针与当前 head 不一致。");
            }

            return FlowRuntimeArtifactResolution.Published(
                normalizedFlowKey,
                currentSourceHash,
                head,
                executable);
        }
        catch (Exception ex)
        {
            return FlowRuntimeArtifactResolution.Blocked(
                normalizedFlowKey,
                currentSourceHash,
                head,
                "读取或验证共享 Artifact 失败："
                + ex.Message);
        }
    }

    public FlowRuntimeArtifactResolution ResolveForExecution(
        FlowParam flowParam)
    {
        ArgumentNullException.ThrowIfNull(flowParam);
        if (string.IsNullOrWhiteSpace(flowParam.FlowKey))
        {
            throw new InvalidOperationException(
                "当前流程没有稳定 FlowKey。");
        }
        if (string.IsNullOrWhiteSpace(flowParam.DataBase64))
        {
            throw new InvalidOperationException(
                "当前流程没有 STN 数据。");
        }
        byte[] currentAuthoringStn;
        try
        {
            currentAuthoringStn =
                Convert.FromBase64String(flowParam.DataBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "当前流程 STN 不是有效 Base64。",
                ex);
        }
        return ResolveForExecution(
            flowParam.FlowKey,
            currentAuthoringStn);
    }

    private FlowPublishedExecutable ReadValidatedExecutable(
        FlowArtifactRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);
        byte[] authoringStn = ReadRequiredPart(
            revision,
            FlowArtifactRoles.AuthoringCanvas);
        byte[] compiledStn = ReadRequiredPart(
            revision,
            FlowArtifactRoles.CompiledCanvas);
        byte[] sidecarBytes = ReadRequiredPart(
            revision,
            FlowArtifactRoles.SubflowSidecar);
        byte[] authoringPolicyBytes = ReadRequiredPart(
            revision,
            FlowArtifactRoles.AuthoringPolicy);
        byte[] manifestBytes = ReadRequiredPart(
            revision,
            FlowArtifactRoles.CompilationManifest);
        byte[] policyBytes = ReadRequiredPart(
            revision,
            FlowArtifactRoles.ExecutionPolicy);
        byte[] mapBytes = ReadRequiredPart(
            revision,
            FlowArtifactRoles.CompilationMap);

        FlowArtifactManifest manifest;
        FlowSubflowSidecar sidecar;
        FlowArtifactPolicy authoringPolicy;
        FlowExecutionPolicySnapshot policy;
        FlowCompilationMap map;
        try
        {
            manifest =
                FlowArtifactSerializer.DeserializeManifest(
                    manifestBytes);
            sidecar =
                FlowArtifactSerializer.DeserializeSubflowSidecar(
                    sidecarBytes);
            authoringPolicy =
                FlowArtifactSerializer.DeserializeAuthoringPolicy(
                    authoringPolicyBytes,
                    manifest.FlowKey);
            policy =
                FlowArtifactSerializer.DeserializeEffectivePolicy(
                    policyBytes);
            map =
                FlowArtifactSerializer.DeserializeCompilationMap(
                    mapBytes);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "已发布 artifact 的 JSON 内容无效。",
                ex);
        }

        if (!string.Equals(
                manifest.FlowKey,
                revision.FlowKey,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "已发布 artifact 与 compilation manifest 不一致。");
        }

        FlowArtifactDependencyLock[] dependencies =
            revision.Dependencies
                .Select(ToDependencyLock)
                .ToArray();
        var bundle = new FlowArtifactBundle(
            new FlowArtifactDraft(
                manifest.FlowKey,
                manifest.Revision,
                authoringStn,
                sidecar,
                authoringPolicy),
            manifest,
            new FlowExecutableBundle(
                compiledStn,
                policy,
                map,
                dependencies));
        FlowArtifactValidator.Validate(bundle);
        bool requiresServices =
            StnV1NeutralCodec.Decode(
                    compiledStn,
                    new FlowSubflowCompilerOptions
                    {
                        MaximumDepth =
                            manifest.Compiler.MaximumDepth,
                        MaximumNodeCount =
                            manifest.Compiler.MaximumNodeCount,
                        MaximumConnectionCount =
                            manifest.Compiler
                                .MaximumConnectionCount,
                    })
                .Nodes.Any(node =>
                    typeof(CVBaseServerNode).IsAssignableFrom(
                        node.Schema.NodeType));
        return new FlowPublishedExecutable(
            revision,
            manifest,
            compiledStn,
            policy,
            mapBytes,
            sidecar.Calls.Count > 0,
            requiresServices);
    }

    public FlowPublishedExecutable GetCompatiblePublishedExecutable(
        FlowParam flowParam)
    {
        ArgumentNullException.ThrowIfNull(flowParam);
        if (string.IsNullOrWhiteSpace(flowParam.FlowKey))
        {
            throw new InvalidOperationException(
                "当前流程没有稳定 FlowKey。");
        }

        FlowPublishedExecutable executable =
            GetPublishedExecutable(flowParam.FlowKey);
        if (!executable.IsCompatibleWith(
                flowParam,
                out string failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }
        return executable;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        ownedResource?.Dispose();
    }

    private FlowArtifactRevision Save(
        FlowArtifactBundle bundle,
        bool publishImmediately,
        string? author,
        string? message)
    {
        ThrowIfDisposed();
        FlowArtifactSerializedParts parts =
            FlowArtifactSerializer.Serialize(bundle);
        FlowArtifactRevision? head =
            store.GetHead(bundle.Draft.FlowKey);
        var request = new FlowArtifactRevisionWriteRequest
        {
            FlowKey = bundle.Draft.FlowKey,
            Artifacts =
            [
                Part(
                    FlowArtifactRoles.AuthoringCanvas,
                    parts.AuthoringStn,
                    "application/vnd.colorvision.stnd"),
                Part(
                    FlowArtifactRoles.CompiledCanvas,
                    parts.CompiledStn,
                    "application/vnd.colorvision.stnd"),
                Part(
                    FlowArtifactRoles.SubflowSidecar,
                    parts.SubflowSidecar,
                    "application/json"),
                Part(
                    FlowArtifactRoles.AuthoringPolicy,
                    parts.AuthoringPolicy,
                    "application/json"),
                Part(
                    FlowArtifactRoles.ExecutionPolicy,
                    parts.EffectivePolicy,
                    "application/json"),
                Part(
                    FlowArtifactRoles.CompilationMap,
                    parts.CompilationMap,
                    "application/json"),
                Part(
                    FlowArtifactRoles.CompilationManifest,
                    parts.Manifest,
                    "application/json"),
            ],
            Dependencies = bundle.Executable.Dependencies
                .Select(ToPersistedDependency)
                .ToArray(),
            ExpectedHead = head == null
                ? FlowArtifactHeadCondition.Initial
                : FlowArtifactHeadCondition.FromRevision(head),
            PublishImmediately = publishImmediately,
            Source = "flow-editor",
            Author = author,
            Message = message,
            ExternalVersion = bundle.Draft.Revision,
        };
        PreparedFlowArtifactRevision prepared =
            FlowArtifactStoreRules.Prepare(request);
        if (head != null
            && string.Equals(
                head.RevisionHash,
                prepared.RevisionHash,
                StringComparison.Ordinal))
        {
            if (publishImmediately
                && head.State == FlowArtifactRevisionState.Draft)
            {
                return store.Publish(
                    new FlowArtifactRevisionTransitionRequest
                    {
                        FlowKey = head.FlowKey,
                        Revision = head.Revision,
                        ExpectedHead =
                            FlowArtifactHeadCondition.FromRevision(
                                head),
                        Actor = author,
                        Message = message,
                    });
            }
            return head;
        }
        return store.Append(request);
    }

    private FlowSubflowSidecar LoadSidecar(FlowParam flowParam)
    {
        if (flowParam.TemplateRevision is not int revision
            || revision <= 0)
        {
            return FlowSubflowSidecar.Empty;
        }
        return subflowStore.GetRevision(
                flowParam.FlowKey!,
                revision)
            ?.Sidecar
            ?? FlowSubflowSidecar.Empty;
    }

    private FlowExecutionPolicySnapshot LoadPolicy(string flowKey)
    {
        if (!policyStore.TryLoad(
                flowKey,
                out FlowExecutionPolicySnapshot snapshot,
                out string? failureReason))
        {
            throw new InvalidOperationException(
                $"流程 {flowKey} 的执行策略无法读取："
                + $"{failureReason}");
        }
        return snapshot;
    }

    private byte[] ReadRequiredPart(
        FlowArtifactRevision revision,
        string role)
    {
        FlowArtifactDescriptor descriptor =
            revision.Artifacts.SingleOrDefault(item =>
                string.Equals(
                    item.Role,
                    role,
                    StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"artifact revision 缺少 {role}。");
        FlowArtifactBlob blob = store.GetArtifact(descriptor.Hash)
            ?? throw new InvalidOperationException(
                $"artifact blob {descriptor.Hash} 不存在。");
        if (blob.Content.Length != descriptor.ContentLength
            || !string.Equals(
                FlowArtifactCanonical.ComputeHash(blob.Content),
                descriptor.Hash,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"artifact blob {descriptor.Hash} 校验失败。");
        }
        return blob.Content;
    }

    private static FlowArtifactContent Part(
        string role,
        byte[] content,
        string contentType)
    {
        return new FlowArtifactContent
        {
            Role = role,
            Content = content,
            ContentType = contentType,
        };
    }

    private static FlowArtifactDependency ToPersistedDependency(
        FlowArtifactDependencyLock dependency)
    {
        if (!int.TryParse(
                dependency.Revision,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int revision)
            || revision <= 0)
        {
            throw new InvalidOperationException(
                $"依赖 {dependency.FlowKey} 的 revision "
                + $"{dependency.Revision} 不是正整数。");
        }
        return new FlowArtifactDependency
        {
            DependencyKey = dependency.LogicalCallPath,
            FlowKey = dependency.FlowKey,
            Revision = revision,
            ContentHash = dependency.ContentHash,
            DefinitionHash = dependency.DefinitionHash,
        };
    }

    private static FlowArtifactDependencyLock ToDependencyLock(
        FlowArtifactDependency dependency)
    {
        return new FlowArtifactDependencyLock(
            dependency.DependencyKey,
            dependency.FlowKey,
            dependency.Revision.ToString(
                CultureInfo.InvariantCulture),
            dependency.ContentHash,
            dependency.DefinitionHash);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            disposed,
            nameof(FlowArtifactApplicationService));
    }

    private sealed class CatalogDependencyResolver :
        IFlowArtifactDependencyResolver
    {
        private readonly FlowCatalogService catalog;
        private readonly IFlowSubflowDefinitionStore subflowStore;
        private readonly IFlowExecutionPolicyStore policyStore;

        public CatalogDependencyResolver(
            FlowCatalogService catalog,
            IFlowSubflowDefinitionStore subflowStore,
            IFlowExecutionPolicyStore policyStore)
        {
            this.catalog = catalog;
            this.subflowStore = subflowStore;
            this.policyStore = policyStore;
        }

        public FlowArtifactDependencyDefinition? Resolve(
            FlowDefinitionReference reference)
        {
            ArgumentNullException.ThrowIfNull(reference);
            string flowKey =
                FlowRevisionStoreRules.NormalizeFlowKey(
                    reference.FlowKey);
            int? requestedRevision =
                ParseRevision(reference.Revision);
            string? requestedHash =
                NormalizeOptionalHash(reference.ContentHash);
            FlowRevision? revision = requestedRevision.HasValue
                ? catalog.GetRevision(
                    flowKey,
                    requestedRevision.Value)
                : requestedHash == null
                    ? catalog.GetHead(flowKey)
                    : catalog.List(flowKey).FirstOrDefault(item =>
                        string.Equals(
                            item.BinaryHash,
                            requestedHash,
                            StringComparison.Ordinal));
            if (revision == null)
                return null;
            if (requestedHash != null
                && !string.Equals(
                    revision.BinaryHash,
                    requestedHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"子流程 {flowKey}@{revision.Revision} 的内容哈希"
                    + "与固定引用不一致。");
            }
            FlowSubflowSidecar childSidecar =
                LoadDependencySidecar(revision);
            if (!policyStore.TryLoad(
                    flowKey,
                    out FlowExecutionPolicySnapshot policy,
                    out string? failureReason))
            {
                throw new InvalidOperationException(
                    $"子流程 {flowKey} 的执行策略无法读取："
                    + $"{failureReason}");
            }

            return new FlowArtifactDependencyDefinition(
                revision.FlowKey,
                revision.Revision.ToString(
                    CultureInfo.InvariantCulture),
                revision.FullSnapshot,
                revision.BinaryHash,
                childSidecar,
                new FlowArtifactPolicy(
                    policy.ErrorRoutes,
                    policy.RetryPolicies));
        }

        private FlowSubflowSidecar LoadDependencySidecar(
            FlowRevision revision)
        {
            StoredFlowSubflowDefinition? stored;
            try
            {
                stored = subflowStore.GetRevision(
                    revision.FlowKey,
                    revision.Revision);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"子流程 {revision.FlowKey}@{revision.Revision} "
                    + "的侧车已损坏或无法读取。",
                    ex);
            }

            IReadOnlyList<FlowSubflowReference> declarations =
                revision.SemanticDocument.Subflows
                ?? throw new InvalidOperationException(
                    $"子流程 {revision.FlowKey}@{revision.Revision} "
                    + "的语义调用声明为空。");
            if (stored == null)
            {
                if (declarations.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"子流程 {revision.FlowKey}@{revision.Revision} "
                        + "声明了嵌套调用，但对应侧车不存在。");
                }
                return FlowSubflowSidecar.Empty;
            }

            FlowSubflowSidecar sidecar =
                FlowSubflowSidecarPersistence.Normalize(
                    stored.Sidecar);
            if (!MatchesDeclarations(
                    declarations,
                    sidecar))
            {
                throw new InvalidOperationException(
                    $"子流程 {revision.FlowKey}@{revision.Revision} "
                    + "的语义调用声明与侧车不一致。");
            }
            return sidecar;
        }

        private static bool MatchesDeclarations(
            IReadOnlyList<FlowSubflowReference> declarations,
            FlowSubflowSidecar sidecar)
        {
            if (declarations.Count != sidecar.Calls.Count)
                return false;
            Dictionary<string, FlowSubflowReference> byCallId;
            try
            {
                byCallId = declarations.ToDictionary(
                    item => item.CallNodeId,
                    StringComparer.Ordinal);
            }
            catch (ArgumentException)
            {
                return false;
            }

            foreach (FlowSubflowCall call in sidecar.Calls)
            {
                if (!byCallId.TryGetValue(
                        call.CallId,
                        out FlowSubflowReference? declaration)
                    || !string.Equals(
                        declaration.FlowKey,
                        call.Child.FlowKey,
                        StringComparison.Ordinal)
                    || declaration.Revision
                        != ParseRevision(call.Child.Revision)
                    || !declaration.WaitForCompletion
                    || !declaration.CancelWithParent
                    || !declaration.InputMappings.TryGetValue(
                        "parentSource",
                        out string? source)
                    || !string.Equals(
                        source,
                        $"{call.Source.NodeId:N}/outputs/"
                        + call.Source.OptionIndex.ToString(
                            CultureInfo.InvariantCulture),
                        StringComparison.Ordinal)
                    || !declaration.OutputMappings.TryGetValue(
                        "parentTarget",
                        out string? target)
                    || !string.Equals(
                        target,
                        $"{call.Target.NodeId:N}/inputs/"
                        + call.Target.OptionIndex.ToString(
                            CultureInfo.InvariantCulture),
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private static int? ParseRevision(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            if (!int.TryParse(
                    value.Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int revision)
                || revision <= 0)
            {
                throw new InvalidOperationException(
                    $"子流程 revision 无效：{value}。");
            }
            return revision;
        }

        private static string? NormalizeOptionalHash(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            string normalized = value.Trim();
            const string prefix = "sha256:";
            if (normalized.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[prefix.Length..];
            }
            return FlowRevisionStoreRules.NormalizeHash(
                normalized,
                nameof(value));
        }
    }
}

internal static class FlowArtifactServiceProvider
{
    /// <summary>
    /// Reads exact-revision authoring metadata without opening the artifact
    /// database. A missing catalog revision and missing sidecar is unknown,
    /// not proof that the revision has no subflows.
    /// </summary>
    public static bool? GetAuthoringSubflowPresence(
        string flowKey,
        int revision)
    {
        return GetAuthoringSubflowPresence(
            FlowCatalogProvider.Shared,
            FlowSubflowDefinitionStoreProvider.Shared,
            flowKey,
            revision);
    }

    internal static bool? GetAuthoringSubflowPresence(
        FlowCatalogService catalog,
        IFlowSubflowDefinitionStore sidecars,
        string flowKey,
        int revision)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(sidecars);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
        FlowRevision? catalogRevision =
            catalog.GetRevision(
                flowKey,
                revision);
        StoredFlowSubflowDefinition? sidecarRevision =
            sidecars.GetRevision(
                flowKey,
                revision);
        if (catalogRevision == null && sidecarRevision == null)
            return null;

        return catalogRevision?.SemanticDocument.Subflows.Count > 0
            || sidecarRevision?.Sidecar.Calls.Count > 0;
    }

    public static FlowArtifactApplicationService Create(
        bool ensureSchema = true)
    {
        var db = new SqlSugarClient(
            new ConnectionConfig
            {
                ConnectionString =
                    MySqlControl.GetConnectionString(),
                DbType = DbType.MySql,
                IsAutoCloseConnection = true,
            });
        try
        {
            var store = new SqlSugarFlowArtifactStore(
                db,
                ensureSchema);
            return new FlowArtifactApplicationService(
                store,
                FlowCatalogProvider.Shared,
                FlowSubflowDefinitionStoreProvider.Shared,
                FlowExecutionPolicyStoreProvider.Shared,
                db);
        }
        catch
        {
            db.Dispose();
            throw;
        }
    }
}

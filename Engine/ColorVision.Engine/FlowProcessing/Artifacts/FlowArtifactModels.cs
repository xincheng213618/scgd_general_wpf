using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Templates.Flow.Routing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Artifacts;

/// <summary>
/// Authoring policy detached from a mutable policy store revision. Artifact
/// identity is based only on the normalized policy content.
/// </summary>
public sealed class FlowArtifactPolicy
{
    public FlowArtifactPolicy(
        IEnumerable<FlowErrorRoutePolicy>? errorRoutes = null,
        IEnumerable<FlowRetryPolicy>? retryPolicies = null)
    {
        ErrorRoutes = new ReadOnlyCollection<FlowErrorRoutePolicy>(
            errorRoutes?.ToArray()
                ?? Array.Empty<FlowErrorRoutePolicy>());
        RetryPolicies = new ReadOnlyCollection<FlowRetryPolicy>(
            retryPolicies?.ToArray()
                ?? Array.Empty<FlowRetryPolicy>());
    }

    public static FlowArtifactPolicy Empty { get; } = new();

    public IReadOnlyList<FlowErrorRoutePolicy> ErrorRoutes { get; }

    public IReadOnlyList<FlowRetryPolicy> RetryPolicies { get; }
}

/// <summary>
/// Complete authoring input for one immutable artifact revision. The STND
/// source, subflow sidecar, and author policy remain separate so the published
/// compatibility STND can be regenerated without changing the legacy format.
/// </summary>
public sealed class FlowArtifactDraft
{
    private readonly byte[] authoringStn;

    public FlowArtifactDraft(
        string flowKey,
        string? revision,
        byte[] authoringStn,
        FlowSubflowSidecar? subflowSidecar = null,
        FlowArtifactPolicy? authoringPolicy = null)
    {
        FlowKey = flowKey ?? string.Empty;
        Revision = revision;
        this.authoringStn = (byte[])(authoringStn
            ?? throw new ArgumentNullException(nameof(authoringStn))).Clone();
        SubflowSidecar = FlowSubflowSidecarPersistence.Normalize(
            subflowSidecar ?? FlowSubflowSidecar.Empty);
        AuthoringPolicy = authoringPolicy ?? FlowArtifactPolicy.Empty;
    }

    public string FlowKey { get; }

    public string? Revision { get; }

    public byte[] AuthoringStn => (byte[])authoringStn.Clone();

    public FlowSubflowSidecar SubflowSidecar { get; }

    public FlowArtifactPolicy AuthoringPolicy { get; }
}

/// <summary>
/// Resolver output used while producing a dependency lock. Revision is
/// mandatory; ContentHash may be omitted by a resolver because the builder
/// always computes and locks the actual source hash.
/// </summary>
public sealed class FlowArtifactDependencyDefinition
{
    private readonly byte[] authoringStn;

    public FlowArtifactDependencyDefinition(
        string flowKey,
        string revision,
        byte[] authoringStn,
        string? contentHash = null,
        FlowSubflowSidecar? subflowSidecar = null,
        FlowArtifactPolicy? authoringPolicy = null)
    {
        FlowKey = flowKey ?? string.Empty;
        Revision = revision ?? string.Empty;
        ContentHash = contentHash;
        this.authoringStn = (byte[])(authoringStn
            ?? throw new ArgumentNullException(nameof(authoringStn))).Clone();
        SubflowSidecar = FlowSubflowSidecarPersistence.Normalize(
            subflowSidecar ?? FlowSubflowSidecar.Empty);
        AuthoringPolicy = authoringPolicy ?? FlowArtifactPolicy.Empty;
    }

    public string FlowKey { get; }

    public string Revision { get; }

    public string? ContentHash { get; }

    public byte[] AuthoringStn => (byte[])authoringStn.Clone();

    public FlowSubflowSidecar SubflowSidecar { get; }

    public FlowArtifactPolicy AuthoringPolicy { get; }
}

public interface IFlowArtifactDependencyResolver
{
    FlowArtifactDependencyDefinition? Resolve(
        FlowDefinitionReference reference);
}

/// <summary>
/// Exact dependency selected for one logical call site.
/// </summary>
public sealed record FlowArtifactDependencyLock(
    string LogicalCallPath,
    string FlowKey,
    string Revision,
    string ContentHash,
    string DefinitionHash);

public sealed record FlowArtifactCompilerDescriptor(
    string Name,
    string Version,
    int StndVersion,
    int MaximumDepth,
    int MaximumNodeCount,
    int MaximumConnectionCount);

/// <summary>
/// Hash-only identity for an artifact. No wall-clock or machine-local values
/// participate, so equivalent inputs produce the same manifest.
/// </summary>
public sealed record FlowArtifactManifest(
    int FormatVersion,
    string FlowKey,
    string? Revision,
    string SourceHash,
    string SubflowHash,
    string PolicyHash,
    string SemanticHash,
    string LayoutHash,
    string DefinitionHash,
    string DependencyHash,
    string CompiledStnHash,
    string EffectivePolicyHash,
    string CompilationMapHash,
    string CompilerHash,
    string ArtifactHash,
    FlowArtifactCompilerDescriptor Compiler);

/// <summary>
/// Runtime payload consumed without an editor. CompiledStn remains ordinary
/// STND v1 data readable by legacy runtimes.
/// </summary>
public sealed class FlowExecutableBundle
{
    private readonly byte[] compiledStn;

    public FlowExecutableBundle(
        byte[] compiledStn,
        FlowExecutionPolicySnapshot effectivePolicy,
        FlowCompilationMap compilationMap,
        IReadOnlyList<FlowArtifactDependencyLock> dependencies)
    {
        this.compiledStn = (byte[])(compiledStn
            ?? throw new ArgumentNullException(nameof(compiledStn))).Clone();
        EffectivePolicy = effectivePolicy
            ?? throw new ArgumentNullException(nameof(effectivePolicy));
        ArgumentNullException.ThrowIfNull(compilationMap);
        CompilationMap = new FlowCompilationMap(
            new ReadOnlyCollection<FlowCompiledNodeMap>(
                compilationMap.Nodes.ToArray()),
            new ReadOnlyCollection<FlowCompiledCallMap>(
                compilationMap.Calls.ToArray()));
        Dependencies = new ReadOnlyCollection<FlowArtifactDependencyLock>(
            (dependencies
                ?? throw new ArgumentNullException(nameof(dependencies)))
            .ToArray());
    }

    public byte[] CompiledStn => (byte[])compiledStn.Clone();

    public FlowExecutionPolicySnapshot EffectivePolicy { get; }

    public FlowCompilationMap CompilationMap { get; }

    public IReadOnlyList<FlowArtifactDependencyLock> Dependencies { get; }
}

public sealed record FlowArtifactBundle(
    FlowArtifactDraft Draft,
    FlowArtifactManifest Manifest,
    FlowExecutableBundle Executable);

public enum FlowArtifactError
{
    InvalidDraft,
    InvalidAuthoringCanvas,
    MissingDependency,
    UnpinnedDependency,
    DependencyIdentityMismatch,
    DependencyContentMismatch,
    NondeterministicDependency,
    CompilationFailed,
    PolicyMappingUnavailable,
    InvalidCompiledCanvas,
    HashMismatch,
    InvalidManifest,
}

public sealed class FlowArtifactException : Exception
{
    public FlowArtifactException(
        FlowArtifactError error,
        string component,
        string message)
        : base(message)
    {
        Error = error;
        Component = component;
    }

    public FlowArtifactException(
        FlowArtifactError error,
        string component,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Error = error;
        Component = component;
    }

    public FlowArtifactError Error { get; }

    public string Component { get; }
}

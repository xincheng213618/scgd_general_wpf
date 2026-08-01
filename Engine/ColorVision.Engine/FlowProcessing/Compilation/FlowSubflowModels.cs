using System;
using System.Collections.Generic;

namespace ColorVision.Engine.FlowProcessing.Compilation;

/// <summary>
/// Identifies an immutable child-flow artifact. A revision or content hash may
/// be omitted while authoring, but the resolver must return the concrete
/// identity used by the compiler.
/// </summary>
public sealed record FlowDefinitionReference(
    string FlowKey,
    string? Revision = null,
    string? ContentHash = null);

/// <summary>
/// References a persisted STN option by node id and its local option index.
/// The index is relative to the node's input or output collection, not the
/// global STND option table.
/// </summary>
public sealed record FlowPortReference(Guid NodeId, int OptionIndex);

/// <summary>
/// Authoring-only replacement for one existing parent connection. It is kept
/// in a sidecar and is never serialized as an STNode.
/// </summary>
public sealed record FlowSubflowCall(
    string CallId,
    FlowPortReference Source,
    FlowPortReference Target,
    FlowDefinitionReference Child);

public sealed record FlowSubflowSidecar(
    IReadOnlyList<FlowSubflowCall> Calls)
{
    public static FlowSubflowSidecar Empty { get; } =
        new(Array.Empty<FlowSubflowCall>());
}

public sealed record ResolvedFlowDefinition(
    string FlowKey,
    string? Revision,
    string? ContentHash,
    byte[] CanvasData,
    FlowSubflowSidecar? Sidecar = null);

public interface IFlowSubflowResolver
{
    ResolvedFlowDefinition? Resolve(FlowDefinitionReference reference);
}

public sealed class FlowSubflowCompilerOptions
{
    public const int DefaultMaximumDepth = 16;
    public const int DefaultMaximumNodeCount = 10_000;
    public const int DefaultMaximumConnectionCount = 100_000;

    public int MaximumDepth { get; init; } = DefaultMaximumDepth;

    public int MaximumNodeCount { get; init; } = DefaultMaximumNodeCount;

    public int MaximumConnectionCount { get; init; } =
        DefaultMaximumConnectionCount;
}

public enum FlowCompilationError
{
    InvalidCanvas,
    UnknownNodeType,
    MissingDefinition,
    ReferenceMismatch,
    HashMismatch,
    RecursiveReference,
    MaximumDepthExceeded,
    NodeLimitExceeded,
    ConnectionLimitExceeded,
    DuplicateCall,
    InvalidCallSite,
    InvalidChildBoundary,
    LoopBoundaryNotAllowed,
    IncompatibleBoundaryType,
}

public sealed class FlowCompilationException : Exception
{
    public FlowCompilationException(
        FlowCompilationError error,
        string message)
        : base(message)
    {
        Error = error;
    }

    public FlowCompilationException(
        FlowCompilationError error,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Error = error;
    }

    public FlowCompilationError Error { get; }
}

public sealed record FlowCompiledNodeMap(
    Guid CompiledNodeId,
    Guid SourceNodeId,
    string SourceFlowKey,
    string? SourceRevision,
    string SourceContentHash,
    string LogicalPath);

public sealed record FlowCompiledCallMap(
    string LogicalCallPath,
    FlowDefinitionReference Requested,
    string ResolvedFlowKey,
    string? ResolvedRevision,
    string ResolvedContentHash);

public sealed record FlowCompilationMap(
    IReadOnlyList<FlowCompiledNodeMap> Nodes,
    IReadOnlyList<FlowCompiledCallMap> Calls);

public sealed record FlowCompilationResult(
    byte[] CanvasData,
    string ContentHash,
    FlowCompilationMap Map);

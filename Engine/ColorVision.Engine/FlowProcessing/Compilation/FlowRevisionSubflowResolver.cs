using ColorVision.Engine.Templates.Flow.Versioning;
using System;
using System.Globalization;

namespace ColorVision.Engine.FlowProcessing.Compilation;

public enum FlowSubflowResolutionError
{
    InvalidRevision,
    RevisionHashMismatch,
}

public sealed class FlowSubflowResolutionException :
    InvalidOperationException
{
    public FlowSubflowResolutionException(
        FlowSubflowResolutionError error,
        string message)
        : base(message)
    {
        Error = error;
    }

    public FlowSubflowResolutionError Error { get; }
}

/// <summary>
/// Resolves child definitions from the immutable flow revision store and
/// attaches the authoring sidecar saved for that exact revision.
/// </summary>
public sealed class FlowRevisionSubflowResolver :
    IFlowSubflowResolver
{
    private readonly IFlowRevisionStore revisionStore;
    private readonly IFlowSubflowDefinitionStore sidecarStore;

    public FlowRevisionSubflowResolver(
        IFlowRevisionStore revisionStore,
        IFlowSubflowDefinitionStore sidecarStore)
    {
        this.revisionStore = revisionStore
            ?? throw new ArgumentNullException(nameof(revisionStore));
        this.sidecarStore = sidecarStore
            ?? throw new ArgumentNullException(nameof(sidecarStore));
    }

    public ResolvedFlowDefinition? Resolve(
        FlowDefinitionReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        string flowKey =
            FlowRevisionStoreRules.NormalizeFlowKey(reference.FlowKey);
        int? requestedRevision = ParseRevision(reference.Revision);
        string? requestedHash =
            NormalizeOptionalHash(reference.ContentHash);

        FlowRevision? revision;
        if (requestedRevision.HasValue)
        {
            revision = revisionStore.GetRevision(
                flowKey,
                requestedRevision.Value);
            if (revision == null)
                return null;
            if (requestedHash != null
                && !string.Equals(
                    requestedHash,
                    revision.BinaryHash,
                    StringComparison.Ordinal))
            {
                throw new FlowSubflowResolutionException(
                    FlowSubflowResolutionError.RevisionHashMismatch,
                    $"子流程 {flowKey} 的版本 {requestedRevision.Value} "
                    + $"哈希为 {revision.BinaryHash}，与请求 "
                    + $"{requestedHash} 不一致。");
            }
        }
        else if (requestedHash != null)
        {
            revision = revisionStore.FindByBinaryHash(
                flowKey,
                requestedHash);
        }
        else
        {
            revision = revisionStore.GetHead(flowKey);
        }

        if (revision == null)
            return null;

        StoredFlowSubflowDefinition? storedSidecar =
            sidecarStore.GetRevision(flowKey, revision.Revision);
        FlowSubflowSidecar sidecar = storedSidecar?.Sidecar
            ?? FlowSubflowSidecar.Empty;
        return new ResolvedFlowDefinition(
            revision.FlowKey,
            revision.Revision.ToString(
                CultureInfo.InvariantCulture),
            revision.BinaryHash,
            (byte[])revision.FullSnapshot.Clone(),
            FlowSubflowSidecarPersistence.Clone(sidecar));
    }

    private static int? ParseRevision(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        string normalized = value.Trim();
        if (!int.TryParse(
                normalized,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int revision)
            || revision <= 0
            || !string.Equals(
                normalized,
                revision.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
        {
            throw new FlowSubflowResolutionException(
                FlowSubflowResolutionError.InvalidRevision,
                $"流程版本必须是规范的正整数：{value}。");
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

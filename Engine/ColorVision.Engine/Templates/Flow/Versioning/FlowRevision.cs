using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow.Versioning
{
    public enum FlowRevisionSource
    {
        Editor,
        Import,
        External,
        Publish,
        Rollback,
        Recovery,
    }

    public sealed class FlowRevision
    {
        public string FlowKey { get; init; } = string.Empty;

        public int Revision { get; init; }

        public int? ParentRevision { get; init; }

        public string? BaseBinaryHash { get; init; }

        public FlowRevisionSource Source { get; init; }

        public bool IsPublished { get; init; }

        public string SemanticHash { get; init; } = string.Empty;

        public string LayoutHash { get; init; } = string.Empty;

        public string BinaryHash { get; init; } = string.Empty;

        public byte[] FullSnapshot { get; init; } = Array.Empty<byte>();

        public FlowSemanticDocument SemanticDocument { get; init; } = new();

        public string? Author { get; init; }

        public string? Message { get; init; }

        public string? ExternalVersion { get; init; }

        public int? RollbackOfRevision { get; init; }

        public DateTime CreatedTimeUtc { get; init; }

        public FlowRevision DeepClone()
        {
            return new FlowRevision
            {
                FlowKey = FlowKey,
                Revision = Revision,
                ParentRevision = ParentRevision,
                BaseBinaryHash = BaseBinaryHash,
                Source = Source,
                IsPublished = IsPublished,
                SemanticHash = SemanticHash,
                LayoutHash = LayoutHash,
                BinaryHash = BinaryHash,
                FullSnapshot = (byte[])FullSnapshot.Clone(),
                SemanticDocument = SemanticDocument.DeepClone(),
                Author = Author,
                Message = Message,
                ExternalVersion = ExternalVersion,
                RollbackOfRevision = RollbackOfRevision,
                CreatedTimeUtc = CreatedTimeUtc,
            };
        }
    }

    public sealed record FlowRevisionWriteCondition(
        int? ParentRevision,
        string? BaseBinaryHash)
    {
        public static FlowRevisionWriteCondition Initial { get; } =
            new(null, null);

        public static FlowRevisionWriteCondition FromHead(FlowRevision head)
        {
            ArgumentNullException.ThrowIfNull(head);
            return new FlowRevisionWriteCondition(
                head.Revision,
                head.BinaryHash);
        }
    }

    public sealed class FlowRevisionCreateRequest
    {
        public string FlowKey { get; init; } = string.Empty;

        public byte[] FullSnapshot { get; init; } = Array.Empty<byte>();

        public FlowSemanticDocument SemanticDocument { get; init; } = new();

        public FlowRevisionSource Source { get; init; } =
            FlowRevisionSource.Editor;

        public bool IsPublished { get; init; }

        public FlowRevisionWriteCondition Condition { get; init; } =
            FlowRevisionWriteCondition.Initial;

        public string? Author { get; init; }

        public string? Message { get; init; }

        public string? ExternalVersion { get; init; }

        public DateTime? CreatedTimeUtc { get; init; }
    }

    public sealed class FlowRevisionAppendRequest
    {
        public string FlowKey { get; init; } = string.Empty;

        public byte[] FullSnapshot { get; init; } = Array.Empty<byte>();

        public FlowSemanticDocument SemanticDocument { get; init; } = new();

        public FlowRevisionSource Source { get; init; }

        public bool IsPublished { get; init; }

        public FlowRevisionWriteCondition Condition { get; init; } =
            FlowRevisionWriteCondition.Initial;

        public string SemanticHash { get; init; } = string.Empty;

        public string LayoutHash { get; init; } = string.Empty;

        public string BinaryHash { get; init; } = string.Empty;

        public string? Author { get; init; }

        public string? Message { get; init; }

        public string? ExternalVersion { get; init; }

        public int? RollbackOfRevision { get; init; }

        public DateTime CreatedTimeUtc { get; init; }
    }

    public interface IFlowRevisionStore
    {
        FlowRevision? GetHead(string flowKey);

        FlowRevision? GetRevision(string flowKey, int revision);

        FlowRevision? FindByBinaryHash(string flowKey, string binaryHash);

        FlowRevision? FindByContentHashes(
            string flowKey,
            string binaryHash,
            string semanticHash,
            string layoutHash)
        {
            string normalizedBinaryHash =
                FlowRevisionStoreRules.NormalizeHash(
                    binaryHash,
                    nameof(binaryHash));
            string normalizedSemanticHash =
                FlowRevisionStoreRules.NormalizeHash(
                    semanticHash,
                    nameof(semanticHash));
            string normalizedLayoutHash =
                FlowRevisionStoreRules.NormalizeHash(
                    layoutHash,
                    nameof(layoutHash));
            return List(flowKey)
                .LastOrDefault(item =>
                    string.Equals(
                        item.BinaryHash,
                        normalizedBinaryHash,
                        StringComparison.Ordinal)
                    && string.Equals(
                        item.SemanticHash,
                        normalizedSemanticHash,
                        StringComparison.Ordinal)
                    && string.Equals(
                        item.LayoutHash,
                        normalizedLayoutHash,
                        StringComparison.Ordinal));
        }

        IReadOnlyList<FlowRevision> List(string flowKey);

        FlowRevision Append(FlowRevisionAppendRequest request);
    }

    public sealed class FlowRevisionConflictException : InvalidOperationException
    {
        public FlowRevisionConflictException(
            string flowKey,
            FlowRevisionWriteCondition expected,
            FlowRevision? actual)
            : base(CreateMessage(flowKey, expected, actual))
        {
            FlowKey = flowKey;
            Expected = expected;
            Actual = actual?.DeepClone();
        }

        public string FlowKey { get; }

        public FlowRevisionWriteCondition Expected { get; }

        public FlowRevision? Actual { get; }

        private static string CreateMessage(
            string flowKey,
            FlowRevisionWriteCondition expected,
            FlowRevision? actual)
        {
            string actualValue = actual == null
                ? "<none>"
                : $"{actual.Revision}/{actual.BinaryHash}";
            return $"流程 {flowKey} 的版本基线已变化；期望 "
                + $"{expected.ParentRevision?.ToString() ?? "<none>"}/"
                + $"{expected.BaseBinaryHash ?? "<none>"}，实际 {actualValue}。";
        }
    }

    public enum FlowExternalReconcileStatus
    {
        Created,
        Unchanged,
        HistoricalContent,
        Conflict,
    }

    public sealed class FlowExternalUpdateRequest
    {
        public string FlowKey { get; init; } = string.Empty;

        public byte[] FullSnapshot { get; init; } = Array.Empty<byte>();

        public FlowSemanticDocument SemanticDocument { get; init; } = new();

        /// <summary>
        /// Binary hash of the revision on which the external edit was based.
        /// Null means that the external system believes it is creating the
        /// first revision.
        /// </summary>
        public string? BaseBinaryHash { get; init; }

        public bool IsPublished { get; init; }

        public string? ExternalVersion { get; init; }

        public string? Author { get; init; }

        public string? Message { get; init; }

        public DateTime? CreatedTimeUtc { get; init; }
    }

    public sealed class FlowExternalReconcileResult
    {
        public FlowExternalReconcileStatus Status { get; init; }

        public FlowRevision? Revision { get; init; }

        public FlowRevision? CurrentHead { get; init; }

        public FlowRevision? MatchingHistoricalRevision { get; init; }
    }
}

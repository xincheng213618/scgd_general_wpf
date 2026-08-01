using System;

namespace ColorVision.Engine.Templates.Flow.Versioning
{
    public sealed class FlowRevisionService
    {
        private readonly IFlowRevisionStore store;

        public FlowRevisionService(IFlowRevisionStore store)
        {
            this.store = store ??
                throw new ArgumentNullException(nameof(store));
        }

        public FlowRevision CreateRevision(FlowRevisionCreateRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return store.Append(CreateAppendRequest(
                request.FlowKey,
                request.FullSnapshot,
                request.SemanticDocument,
                request.Source,
                request.IsPublished,
                request.Condition,
                request.Author,
                request.Message,
                request.ExternalVersion,
                rollbackOfRevision: null,
                request.CreatedTimeUtc));
        }

        public FlowRevision Rollback(
            string flowKey,
            int targetRevision,
            FlowRevisionWriteCondition condition,
            string? author = null,
            string? message = null,
            DateTime? createdTimeUtc = null)
        {
            string normalizedFlowKey =
                FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                targetRevision);
            ArgumentNullException.ThrowIfNull(condition);

            FlowRevision target = store.GetRevision(
                normalizedFlowKey,
                targetRevision)
                ?? throw new InvalidOperationException(
                    $"找不到流程 {normalizedFlowKey} 的版本 {targetRevision}。");

            return store.Append(CreateAppendRequest(
                normalizedFlowKey,
                target.FullSnapshot,
                target.SemanticDocument,
                FlowRevisionSource.Rollback,
                target.IsPublished,
                condition,
                author,
                message ?? $"Rollback to revision {targetRevision}",
                externalVersion: null,
                rollbackOfRevision: targetRevision,
                createdTimeUtc));
        }

        public FlowRevision PublishCurrent(
            string flowKey,
            FlowRevisionWriteCondition condition,
            string? author = null,
            string? message = null,
            DateTime? createdTimeUtc = null)
        {
            string normalizedFlowKey =
                FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
            ArgumentNullException.ThrowIfNull(condition);
            FlowRevision head = store.GetHead(normalizedFlowKey)
                ?? throw new InvalidOperationException(
                    $"流程 {normalizedFlowKey} 还没有可发布的版本。");
            FlowRevisionStoreRules.EnsureExpectedHead(
                normalizedFlowKey,
                condition,
                head);
            if (head.IsPublished)
                return head;

            return store.Append(CreateAppendRequest(
                normalizedFlowKey,
                head.FullSnapshot,
                head.SemanticDocument,
                FlowRevisionSource.Publish,
                isPublished: true,
                condition: condition,
                author: author,
                message: message ?? $"Publish revision {head.Revision}",
                externalVersion: null,
                rollbackOfRevision: null,
                createdTimeUtc: createdTimeUtc));
        }

        public FlowExternalReconcileResult ReconcileExternalUpdate(
            FlowExternalUpdateRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            string flowKey =
                FlowRevisionStoreRules.NormalizeFlowKey(request.FlowKey);
            byte[] snapshot = request.FullSnapshot ??
                throw new ArgumentNullException(nameof(request));
            FlowSemanticDocument document = request.SemanticDocument ??
                throw new ArgumentNullException(
                    nameof(request));
            string binaryHash =
                FlowSemanticHash.ComputeBinaryHash(snapshot);
            string semanticHash =
                FlowSemanticHash.ComputeSemanticHash(document);
            string layoutHash =
                FlowSemanticHash.ComputeLayoutHash(document);
            FlowRevision? head = store.GetHead(flowKey);

            if (MatchesContent(
                head,
                binaryHash,
                semanticHash,
                layoutHash))
            {
                FlowRevision matchingHead = head!;
                return new FlowExternalReconcileResult
                {
                    Status = FlowExternalReconcileStatus.Unchanged,
                    Revision = matchingHead,
                    CurrentHead = matchingHead,
                };
            }

            FlowRevision? historical = store.FindByContentHashes(
                flowKey,
                binaryHash,
                semanticHash,
                layoutHash);
            if (historical != null)
            {
                return new FlowExternalReconcileResult
                {
                    Status = FlowExternalReconcileStatus.HistoricalContent,
                    CurrentHead = head,
                    MatchingHistoricalRevision = historical,
                };
            }

            string? normalizedBase = string.IsNullOrWhiteSpace(
                request.BaseBinaryHash)
                ? null
                : FlowRevisionStoreRules.NormalizeHash(
                    request.BaseBinaryHash,
                    nameof(request.BaseBinaryHash));
            if ((head == null && normalizedBase != null)
                || (head != null
                    && !string.Equals(
                        head.BinaryHash,
                        normalizedBase,
                        StringComparison.Ordinal)))
            {
                return new FlowExternalReconcileResult
                {
                    Status = FlowExternalReconcileStatus.Conflict,
                    CurrentHead = head,
                };
            }

            var condition = head == null
                ? FlowRevisionWriteCondition.Initial
                : FlowRevisionWriteCondition.FromHead(head);
            try
            {
                FlowRevision created = store.Append(CreateAppendRequest(
                    flowKey,
                    snapshot,
                    document,
                    FlowRevisionSource.External,
                    request.IsPublished,
                    condition,
                    request.Author,
                    request.Message,
                    request.ExternalVersion,
                    rollbackOfRevision: null,
                    request.CreatedTimeUtc));
                return new FlowExternalReconcileResult
                {
                    Status = FlowExternalReconcileStatus.Created,
                    Revision = created,
                    CurrentHead = created,
                };
            }
            catch (FlowRevisionConflictException)
            {
                return new FlowExternalReconcileResult
                {
                    Status = FlowExternalReconcileStatus.Conflict,
                    CurrentHead = store.GetHead(flowKey),
                };
            }
        }

        public FlowSemanticDiffResult Compare(
            string flowKey,
            int beforeRevision,
            int afterRevision)
        {
            string normalizedFlowKey =
                FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
            FlowRevision before = store.GetRevision(
                normalizedFlowKey,
                beforeRevision)
                ?? throw new InvalidOperationException(
                    $"找不到版本 {beforeRevision}。");
            FlowRevision after = store.GetRevision(
                normalizedFlowKey,
                afterRevision)
                ?? throw new InvalidOperationException(
                    $"找不到版本 {afterRevision}。");
            return FlowSemanticDiff.Compare(
                before.SemanticDocument,
                after.SemanticDocument);
        }

        private static FlowRevisionAppendRequest CreateAppendRequest(
            string flowKey,
            byte[] fullSnapshot,
            FlowSemanticDocument semanticDocument,
            FlowRevisionSource source,
            bool isPublished,
            FlowRevisionWriteCondition condition,
            string? author,
            string? message,
            string? externalVersion,
            int? rollbackOfRevision,
            DateTime? createdTimeUtc)
        {
            ArgumentNullException.ThrowIfNull(fullSnapshot);
            ArgumentNullException.ThrowIfNull(semanticDocument);
            ArgumentNullException.ThrowIfNull(condition);
            byte[] stableSnapshot = (byte[])fullSnapshot.Clone();
            FlowSemanticDocument stableDocument =
                semanticDocument.DeepClone();
            return new FlowRevisionAppendRequest
            {
                FlowKey = FlowRevisionStoreRules.NormalizeFlowKey(flowKey),
                FullSnapshot = stableSnapshot,
                SemanticDocument = stableDocument,
                Source = source,
                IsPublished = isPublished,
                Condition = condition,
                SemanticHash =
                    FlowSemanticHash.ComputeSemanticHash(stableDocument),
                LayoutHash =
                    FlowSemanticHash.ComputeLayoutHash(stableDocument),
                BinaryHash =
                    FlowSemanticHash.ComputeBinaryHash(stableSnapshot),
                Author = author,
                Message = message,
                ExternalVersion = externalVersion,
                RollbackOfRevision = rollbackOfRevision,
                CreatedTimeUtc = createdTimeUtc ?? DateTime.UtcNow,
            };
        }

        private static bool MatchesContent(
            FlowRevision? revision,
            string binaryHash,
            string semanticHash,
            string layoutHash)
        {
            return revision != null
                && string.Equals(
                    revision.BinaryHash,
                    binaryHash,
                    StringComparison.Ordinal)
                && string.Equals(
                    revision.SemanticHash,
                    semanticHash,
                    StringComparison.Ordinal)
                && string.Equals(
                    revision.LayoutHash,
                    layoutHash,
                    StringComparison.Ordinal);
        }
    }
}

using ColorVision.Engine.Templates.Flow.Search;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace ColorVision.Engine.Templates.Flow.Versioning
{
    /// <summary>
    /// Coordinates append-only flow revisions with the safe node search index.
    /// The legacy MySQL resource remains the compatibility source; this
    /// catalog is a recoverable sidecar and never changes STN/CVFlow bytes.
    /// </summary>
    public sealed class FlowCatalogService
    {
        private readonly IFlowRevisionStore revisionStore;
        private readonly IFlowNodeSearchIndex searchIndex;
        private readonly FlowRevisionService revisions;

        public FlowCatalogService(
            IFlowRevisionStore revisionStore,
            IFlowNodeSearchIndex searchIndex)
        {
            this.revisionStore = revisionStore
                ?? throw new ArgumentNullException(nameof(revisionStore));
            this.searchIndex = searchIndex
                ?? throw new ArgumentNullException(nameof(searchIndex));
            revisions = new FlowRevisionService(revisionStore);
        }

        public FlowRevision RecordEditorSave(
            string flowKey,
            byte[] snapshot,
            FlowSemanticDocument semanticDocument,
            IReadOnlyCollection<FlowNodeSearchDocument> searchDocuments,
            string? author = null,
            string? message = null)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(semanticDocument);
            ArgumentNullException.ThrowIfNull(searchDocuments);
            string binaryHash =
                FlowSemanticHash.ComputeBinaryHash(snapshot);
            string semanticHash =
                FlowSemanticHash.ComputeSemanticHash(semanticDocument);
            string layoutHash =
                FlowSemanticHash.ComputeLayoutHash(semanticDocument);

            for (int attempt = 0; attempt < 3; attempt++)
            {
                FlowRevision? head = revisionStore.GetHead(flowKey);
                if (MatchesContent(
                    head,
                    binaryHash,
                    semanticHash,
                    layoutHash))
                {
                    FlowRevision matchingHead = head!;
                    searchIndex.ReplaceRevision(
                        matchingHead.FlowKey,
                        matchingHead.Revision,
                        searchDocuments);
                    return matchingHead;
                }

                FlowRevision? historical =
                    revisionStore.FindByContentHashes(
                        flowKey,
                        binaryHash,
                        semanticHash,
                        layoutHash);
                try
                {
                    FlowRevision created;
                    if (historical != null)
                    {
                        created = revisions.Rollback(
                            flowKey,
                            historical.Revision,
                            head == null
                                ? FlowRevisionWriteCondition.Initial
                                : FlowRevisionWriteCondition.FromHead(head),
                            author,
                            message
                                ?? $"Editor restored revision "
                                + $"{historical.Revision}");
                    }
                    else
                    {
                        created = revisions.CreateRevision(
                            new FlowRevisionCreateRequest
                            {
                                FlowKey = flowKey,
                                FullSnapshot = snapshot,
                                SemanticDocument = semanticDocument,
                                Source = FlowRevisionSource.Editor,
                                Condition = head == null
                                    ? FlowRevisionWriteCondition.Initial
                                    : FlowRevisionWriteCondition.FromHead(head),
                                Author = author,
                                Message = message,
                            });
                    }

                    searchIndex.ReplaceRevision(
                        created.FlowKey,
                        created.Revision,
                        searchDocuments);
                    return created;
                }
                catch (FlowRevisionConflictException) when (attempt < 2)
                {
                    // Another editor saved between GetHead and Append.
                    // Re-read the head and retry the optimistic write.
                }
            }

            throw new InvalidOperationException(
                $"流程 {flowKey} 在连续并发保存后仍无法建立版本。");
        }

        public FlowRevision? FindRevision(
            string flowKey,
            byte[] snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            string binaryHash =
                FlowSemanticHash.ComputeBinaryHash(snapshot);
            FlowRevision? head = revisionStore.GetHead(flowKey);
            return head != null
                && string.Equals(
                    head.BinaryHash,
                    binaryHash,
                    StringComparison.Ordinal)
                ? head
                : revisionStore.FindByBinaryHash(flowKey, binaryHash);
        }

        public FlowRevision? GetHead(string flowKey)
        {
            return revisionStore.GetHead(flowKey);
        }

        public FlowRevision? GetRevision(
            string flowKey,
            int revision)
        {
            return revisionStore.GetRevision(flowKey, revision);
        }

        public IReadOnlyList<FlowRevision> List(string flowKey)
        {
            return revisionStore.List(flowKey);
        }

        public FlowSemanticDiffResult Compare(
            string flowKey,
            int beforeRevision,
            int afterRevision)
        {
            return revisions.Compare(
                flowKey,
                beforeRevision,
                afterRevision);
        }

        public IReadOnlyList<FlowNodeSearchEntry> SearchLatest(
            string text,
            int limit = 30)
        {
            return searchIndex.Search(new FlowNodeSearchQuery
            {
                Text = text,
                LatestOnly = true,
                Limit = Math.Clamp(limit, 1, 100),
            });
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

    internal static class FlowCatalogProvider
    {
        private static readonly Lazy<FlowCatalogService> SharedCatalog =
            new(CreateCatalog);

        public static FlowCatalogService Shared => SharedCatalog.Value;

        private static FlowCatalogService CreateCatalog()
        {
            string directory = Path.Combine(
                ColorVision.UI.Environments.DirAppData,
                "Config");
            Directory.CreateDirectory(directory);
            string databasePath = Path.Combine(
                directory,
                "FlowCatalog.db");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
            }.ToString();
            return new FlowCatalogService(
                new SqliteFlowRevisionStore(connectionString),
                new SqliteFlowNodeSearchIndex(connectionString));
        }
    }
}

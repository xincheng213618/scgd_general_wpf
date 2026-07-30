using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow.Versioning
{
    public sealed class InMemoryFlowRevisionStore : IFlowRevisionStore
    {
        private readonly object sync = new();
        private readonly Dictionary<string, List<FlowRevision>> revisions =
            new(StringComparer.Ordinal);

        public FlowRevision? GetHead(string flowKey)
        {
            string key = FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
            lock (sync)
            {
                return revisions.TryGetValue(
                    key,
                    out List<FlowRevision>? values)
                    ? values[^1].DeepClone()
                    : null;
            }
        }

        public FlowRevision? GetRevision(string flowKey, int revision)
        {
            string key = FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
            lock (sync)
            {
                return revisions.TryGetValue(
                    key,
                    out List<FlowRevision>? values)
                    ? values.FirstOrDefault(
                        item => item.Revision == revision)?.DeepClone()
                    : null;
            }
        }

        public FlowRevision? FindByBinaryHash(
            string flowKey,
            string binaryHash)
        {
            string key = FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
            string hash = FlowRevisionStoreRules.NormalizeHash(
                binaryHash,
                nameof(binaryHash));
            lock (sync)
            {
                return revisions.TryGetValue(
                    key,
                    out List<FlowRevision>? values)
                    ? values.LastOrDefault(
                        item => string.Equals(
                            item.BinaryHash,
                            hash,
                            StringComparison.Ordinal))?.DeepClone()
                    : null;
            }
        }

        public FlowRevision? FindByContentHashes(
            string flowKey,
            string binaryHash,
            string semanticHash,
            string layoutHash)
        {
            string key = FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
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
            lock (sync)
            {
                return revisions.TryGetValue(
                    key,
                    out List<FlowRevision>? values)
                    ? values.LastOrDefault(item =>
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
                            StringComparison.Ordinal))?.DeepClone()
                    : null;
            }
        }

        public IReadOnlyList<FlowRevision> List(string flowKey)
        {
            string key = FlowRevisionStoreRules.NormalizeFlowKey(flowKey);
            lock (sync)
            {
                return revisions.TryGetValue(
                    key,
                    out List<FlowRevision>? values)
                    ? values.Select(item => item.DeepClone()).ToArray()
                    : Array.Empty<FlowRevision>();
            }
        }

        public FlowRevision Append(FlowRevisionAppendRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            string key =
                FlowRevisionStoreRules.NormalizeFlowKey(request.FlowKey);
            lock (sync)
            {
                if (!revisions.TryGetValue(
                    key,
                    out List<FlowRevision>? values))
                {
                    values = new List<FlowRevision>();
                    revisions.Add(key, values);
                }

                FlowRevision? head = values.Count == 0
                    ? null
                    : values[^1];
                FlowRevision revision =
                    FlowRevisionStoreRules.CreateRevision(request, head);
                values.Add(revision);
                return revision.DeepClone();
            }
        }
    }
}

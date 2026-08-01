using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow.Search
{
    public sealed class InMemoryFlowNodeSearchIndex :
        IFlowNodeSearchIndex
    {
        private readonly object sync = new();
        private readonly Dictionary<string, List<FlowNodeSearchEntry>> entries =
            new(StringComparer.Ordinal);

        public void ReplaceRevision(
            string flowKey,
            int revision,
            IReadOnlyCollection<FlowNodeSearchDocument> nodes)
        {
            string key = FlowSearchSafety.NormalizeFlowKey(flowKey);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
            ArgumentNullException.ThrowIfNull(nodes);

            List<FlowNodeSearchEntry> stable =
                FlowNodeSearchIndexer.Build(key, revision, nodes)
                .Select(entry => Clone(entry))
                .ToList();
            lock (sync)
            {
                entries[CreateRevisionKey(key, revision)] = stable;
            }
        }

        public IReadOnlyList<FlowNodeSearchEntry> Search(
            FlowNodeSearchQuery query)
        {
            FlowNodeSearchQuery normalized =
                FlowSearchSafety.NormalizeQuery(query);
            lock (sync)
            {
                IEnumerable<FlowNodeSearchEntry> candidates =
                    entries.Values.SelectMany(item => item);
                if (normalized.FlowKey != null)
                {
                    candidates = candidates.Where(item =>
                        string.Equals(
                            item.FlowKey,
                            normalized.FlowKey,
                            StringComparison.Ordinal));
                }
                if (normalized.Revision != null)
                {
                    candidates = candidates.Where(item =>
                        item.Revision == normalized.Revision.Value);
                }
                else if (normalized.LatestOnly)
                {
                    Dictionary<string, int> latestRevisions = candidates
                        .GroupBy(item => item.FlowKey, StringComparer.Ordinal)
                        .ToDictionary(
                            group => group.Key,
                            group => group.Max(item => item.Revision),
                            StringComparer.Ordinal);
                    candidates = candidates.Where(item =>
                        latestRevisions.TryGetValue(
                            item.FlowKey,
                            out int latestRevision)
                        && item.Revision == latestRevision);
                }
                if (normalized.NodeTypeKey != null)
                {
                    candidates = candidates.Where(item =>
                        string.Equals(
                            item.NodeTypeKey,
                            normalized.NodeTypeKey,
                            StringComparison.Ordinal));
                }
                if (normalized.Text != null)
                {
                    candidates = candidates.Where(item =>
                        item.SearchText.Contains(
                            normalized.Text,
                            StringComparison.OrdinalIgnoreCase));
                }
                return candidates
                    .OrderByDescending(item => item.Revision)
                    .ThenBy(item => item.FlowKey, StringComparer.Ordinal)
                    .ThenBy(item => item.NodePath, StringComparer.Ordinal)
                    .Take(normalized.Limit)
                    .Select(Clone)
                    .ToArray();
            }
        }

        internal static FlowNodeSearchEntry Clone(FlowNodeSearchEntry entry)
        {
            return new FlowNodeSearchEntry
            {
                FlowKey = entry.FlowKey,
                Revision = entry.Revision,
                SourceNodeGuid = entry.SourceNodeGuid,
                NodePath = entry.NodePath,
                NodeTypeKey = entry.NodeTypeKey,
                DisplayName = entry.DisplayName,
                Title = entry.Title,
                TemplateName = entry.TemplateName,
                DeviceCode = entry.DeviceCode,
                ServiceCode = entry.ServiceCode,
                Tags = entry.Tags,
                SearchText = entry.SearchText,
            };
        }

        private static string CreateRevisionKey(
            string flowKey,
            int revision)
        {
            return $"{flowKey}\u001f{revision}";
        }
    }
}

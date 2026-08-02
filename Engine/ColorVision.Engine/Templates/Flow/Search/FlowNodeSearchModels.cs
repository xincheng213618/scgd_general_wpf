using System;
using System.Collections.Generic;
using System.Globalization;

namespace ColorVision.Engine.Templates.Flow.Search
{
    /// <summary>
    /// Typed allowlist for searchable node metadata. There is deliberately no
    /// property bag, JSON field, MQTT payload, token, or physical path field.
    /// </summary>
    public sealed class FlowNodeSearchDocument
    {
        public Guid SourceNodeGuid { get; init; }

        public string NodePath { get; init; } = string.Empty;

        public string NodeTypeKey { get; init; } = string.Empty;

        public string? DisplayName { get; init; }

        public string? Title { get; init; }

        public string? TemplateName { get; init; }

        public string? DeviceCode { get; init; }

        public string? ServiceCode { get; init; }

        public IReadOnlyList<string> Tags { get; init; } =
            Array.Empty<string>();
    }

    public sealed class FlowNodeSearchEntry
    {
        public string FlowKey { get; init; } = string.Empty;

        public int Revision { get; init; }

        public Guid SourceNodeGuid { get; init; }

        public string NodePath { get; init; } = string.Empty;

        public string NodeTypeKey { get; init; } = string.Empty;

        public string? DisplayName { get; init; }

        public string? Title { get; init; }

        public string? TemplateName { get; init; }

        public string? DeviceCode { get; init; }

        public string? ServiceCode { get; init; }

        public string Tags { get; init; } = string.Empty;

        public string SearchText { get; init; } = string.Empty;

        public FlowDeepLink DeepLink => new(
            FlowKey,
            Revision,
            SourceNodeGuid,
            NodePath);
    }

    public sealed record FlowDeepLink(
        string FlowKey,
        int Revision,
        Guid SourceNodeGuid,
        string NodePath)
    {
        public Uri ToUri()
        {
            string query =
                $"flowKey={Uri.EscapeDataString(FlowKey)}"
                + $"&revision={Revision.ToString(CultureInfo.InvariantCulture)}"
                + $"&node={SourceNodeGuid:N}"
                + $"&nodePath={Uri.EscapeDataString(NodePath)}";
            return new Uri($"colorvision-flow://open?{query}");
        }

        public override string ToString()
        {
            return ToUri().AbsoluteUri;
        }

        public static bool TryParse(
            string? value,
            out FlowDeepLink? deepLink)
        {
            deepLink = null;
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
                || !string.Equals(
                    uri.Scheme,
                    "colorvision-flow",
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    uri.Host,
                    "open",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Dictionary<string, string> query;
            try
            {
                query = ParseQuery(uri.Query);
            }
            catch (UriFormatException)
            {
                return false;
            }
            if (!query.TryGetValue("flowKey", out string? flowKey)
                || !query.TryGetValue("revision", out string? revisionText)
                || !query.TryGetValue("node", out string? nodeText)
                || !query.TryGetValue("nodePath", out string? nodePath)
                || !int.TryParse(
                    revisionText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int revision)
                || revision <= 0
                || !Guid.TryParse(nodeText, out Guid nodeGuid))
            {
                return false;
            }

            try
            {
                flowKey = FlowSearchSafety.NormalizeFlowKey(flowKey);
                nodePath = FlowSearchSafety.NormalizeNodePath(nodePath);
            }
            catch (ArgumentException)
            {
                return false;
            }
            deepLink = new FlowDeepLink(
                flowKey,
                revision,
                nodeGuid,
                nodePath);
            return true;
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(
                StringComparer.Ordinal);
            foreach (string pair in query.TrimStart('?').Split(
                '&',
                StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = pair.IndexOf('=');
                if (separator <= 0)
                    continue;
                string key = Uri.UnescapeDataString(pair[..separator]);
                string value = Uri.UnescapeDataString(pair[(separator + 1)..]);
                result[key] = value;
            }
            return result;
        }
    }

    public sealed class FlowNodeSearchQuery
    {
        public string? Text { get; init; }

        public string? FlowKey { get; init; }

        public int? Revision { get; init; }

        public string? NodeTypeKey { get; init; }

        public bool LatestOnly { get; init; }

        public int Limit { get; init; } = 50;
    }

    public interface IFlowNodeSearchIndex
    {
        void ReplaceRevision(
            string flowKey,
            int revision,
            IReadOnlyCollection<FlowNodeSearchDocument> nodes);

        IReadOnlyList<FlowNodeSearchEntry> Search(
            FlowNodeSearchQuery query);
    }
}

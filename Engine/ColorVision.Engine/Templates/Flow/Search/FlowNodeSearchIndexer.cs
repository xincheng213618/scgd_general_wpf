using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColorVision.Engine.Templates.Flow.Search
{
    public static class FlowNodeSearchIndexer
    {
        public static IReadOnlyList<FlowNodeSearchEntry> Build(
            string flowKey,
            int revision,
            IEnumerable<FlowNodeSearchDocument> nodes)
        {
            string key = FlowSearchSafety.NormalizeFlowKey(flowKey);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
            ArgumentNullException.ThrowIfNull(nodes);

            var entries = new List<FlowNodeSearchEntry>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (FlowNodeSearchDocument node in nodes)
            {
                ArgumentNullException.ThrowIfNull(node);
                if (node.SourceNodeGuid == Guid.Empty)
                {
                    throw new ArgumentException(
                        "搜索节点必须具有来源节点 GUID。",
                        nameof(nodes));
                }
                string nodePath =
                    FlowSearchSafety.NormalizeNodePath(node.NodePath);
                string nodeType = FlowSearchSafety.NormalizeRequiredSafeText(
                    node.NodeTypeKey,
                    nameof(node.NodeTypeKey),
                    256);
                string identity = $"{node.SourceNodeGuid:N}\u001f{nodePath}";
                if (!identities.Add(identity))
                {
                    throw new ArgumentException(
                        $"搜索节点重复：{node.SourceNodeGuid:N}/{nodePath}",
                        nameof(nodes));
                }

                string? displayName =
                    FlowSearchSafety.NormalizeOptionalSafeText(
                        node.DisplayName,
                        256);
                string? title = FlowSearchSafety.NormalizeOptionalSafeText(
                    node.Title,
                    256);
                string? templateName =
                    FlowSearchSafety.NormalizeOptionalSafeText(
                        node.TemplateName,
                        256);
                string? deviceCode =
                    FlowSearchSafety.NormalizeOptionalSafeText(
                        node.DeviceCode,
                        128);
                string? serviceCode =
                    FlowSearchSafety.NormalizeOptionalSafeText(
                        node.ServiceCode,
                        128);
                string tags = NormalizeTags(node.Tags);
                string searchText = BuildSearchText(
                    nodeType,
                    displayName,
                    title,
                    templateName,
                    deviceCode,
                    serviceCode,
                    tags);
                entries.Add(new FlowNodeSearchEntry
                {
                    FlowKey = key,
                    Revision = revision,
                    SourceNodeGuid = node.SourceNodeGuid,
                    NodePath = nodePath,
                    NodeTypeKey = nodeType,
                    DisplayName = displayName,
                    Title = title,
                    TemplateName = templateName,
                    DeviceCode = deviceCode,
                    ServiceCode = serviceCode,
                    Tags = tags,
                    SearchText = searchText,
                });
            }
            return entries;
        }

        private static string NormalizeTags(IReadOnlyList<string>? tags)
        {
            if (tags == null || tags.Count == 0)
                return string.Empty;
            return string.Join(
                ' ',
                tags.Take(16)
                    .Select(item =>
                        FlowSearchSafety.NormalizeOptionalSafeText(item, 64))
                    .Where(item => item != null)
                    .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string BuildSearchText(params string?[] values)
        {
            var result = new StringBuilder();
            foreach (string? value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                if (result.Length > 0)
                    result.Append(' ');
                result.Append(value);
            }
            return result.ToString();
        }
    }

    internal static class FlowSearchSafety
    {
        private static readonly string[] SecretMarkers =
        [
            "token=",
            "\"token\"",
            "bearer ",
            "password=",
            "\"password\"",
            "secret=",
            "\"secret\"",
            "payload=",
            "\"payload\"",
        ];

        public static string NormalizeFlowKey(string flowKey)
        {
            if (string.IsNullOrWhiteSpace(flowKey))
                throw new ArgumentException("FlowKey 不能为空。", nameof(flowKey));
            string normalized = flowKey.Trim();
            if (normalized.Length > 256
                || normalized.IndexOfAny(['\r', '\n', '\0']) >= 0
                || normalized.StartsWith('/')
                || normalized.StartsWith('\\')
                || normalized.Contains(":\\", StringComparison.Ordinal)
                || normalized.Contains("file://", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("FlowKey 无效。", nameof(flowKey));
            }
            return normalized;
        }

        public static string NormalizeNodePath(string nodePath)
        {
            if (string.IsNullOrWhiteSpace(nodePath))
                throw new ArgumentException("NodePath 不能为空。", nameof(nodePath));
            string normalized = nodePath.Trim();
            if (normalized.Length > 512
                || normalized.StartsWith('/')
                || normalized.StartsWith('\\')
                || normalized.Contains('\\')
                || normalized.Contains("://", StringComparison.Ordinal)
                || normalized.Contains("..", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "NodePath 必须是逻辑节点路径，不能是物理文件路径。",
                    nameof(nodePath));
            }
            foreach (string segment in normalized.Split('/'))
            {
                if (segment.Length == 0 || segment.Length > 128)
                    throw new ArgumentException("NodePath 分段无效。", nameof(nodePath));
                foreach (char value in segment)
                {
                    if (!char.IsLetterOrDigit(value)
                        && value != '-'
                        && value != '_'
                        && value != '.')
                    {
                        throw new ArgumentException(
                            "NodePath 包含非法字符。",
                            nameof(nodePath));
                    }
                }
            }
            return normalized;
        }

        public static string NormalizeRequiredSafeText(
            string? value,
            string parameterName,
            int maximumLength)
        {
            string? normalized = NormalizeOptionalSafeText(
                value,
                maximumLength);
            if (normalized == null)
            {
                throw new ArgumentException(
                    "搜索字段为空或包含不允许索引的敏感内容。",
                    parameterName);
            }
            return normalized;
        }

        public static string? NormalizeOptionalSafeText(
            string? value,
            int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            string normalized = value.Trim();
            if (normalized.Length > maximumLength)
                normalized = normalized[..maximumLength];
            if (ContainsControlCharacter(normalized)
                || LooksLikeJson(normalized)
                || LooksLikePhysicalPath(normalized)
                || ContainsSecretMarker(normalized))
            {
                return null;
            }
            return normalized;
        }

        public static FlowNodeSearchQuery NormalizeQuery(
            FlowNodeSearchQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);
            if (query.Limit is < 1 or > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(query),
                    "搜索结果上限必须介于 1 和 100 之间。");
            }
            if (query.Revision is <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(query),
                    "Revision 必须是正整数。");
            }
            string? text = NormalizeQueryText(query.Text);
            string? flowKey = string.IsNullOrWhiteSpace(query.FlowKey)
                ? null
                : NormalizeFlowKey(query.FlowKey);
            string? nodeType = string.IsNullOrWhiteSpace(query.NodeTypeKey)
                ? null
                : NormalizeRequiredSafeText(
                    query.NodeTypeKey,
                    nameof(query.NodeTypeKey),
                    256);
            return new FlowNodeSearchQuery
            {
                Text = text,
                FlowKey = flowKey,
                Revision = query.Revision,
                NodeTypeKey = nodeType,
                LatestOnly = query.LatestOnly,
                Limit = query.Limit,
            };
        }

        private static string? NormalizeQueryText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            string normalized = value.Trim();
            if (normalized.Length > 128)
                throw new ArgumentException("搜索文本长度不能超过 128。");
            if (ContainsControlCharacter(normalized))
                throw new ArgumentException("搜索文本包含控制字符。");
            return normalized;
        }

        private static bool LooksLikeJson(string value)
        {
            return value.Length >= 2
                && ((value[0] == '{' && value[^1] == '}')
                    || (value[0] == '[' && value[^1] == ']'));
        }

        private static bool LooksLikePhysicalPath(string value)
        {
            return value.StartsWith('/')
                || value.StartsWith('\\')
                || value.Contains(":\\", StringComparison.Ordinal)
                || value.Contains(":/", StringComparison.Ordinal)
                || value.Contains("\\\\", StringComparison.Ordinal)
                || value.Contains("file://", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsSecretMarker(string value)
        {
            return SecretMarkers.Any(marker =>
                value.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsControlCharacter(string value)
        {
            return value.Any(character =>
                char.IsControl(character)
                && character is not '\t');
        }
    }
}

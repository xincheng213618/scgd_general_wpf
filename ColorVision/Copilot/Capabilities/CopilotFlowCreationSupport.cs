using System;

namespace ColorVision.Copilot
{
    public static class CopilotFlowCreationSupport
    {
        private static readonly string[] CreateIntentMarkers =
        {
            "创建新的流程",
            "创建一个新的流程",
            "创建一个新流程",
            "创建新流程",
            "创建流程",
            "新建流程",
            "新建一个流程",
            "新增流程",
            "create a new flow",
            "create new flow",
            "create flow",
            "new flow",
            "create a new workflow",
            "create new workflow",
            "create workflow",
            "new workflow",
        };

        private static readonly string[] NonActionMarkers =
        {
            "不要创建",
            "别创建",
            "不用创建",
            "取消创建流程",
            "如何创建",
            "怎么创建",
            "怎样创建",
            "是否支持创建流程",
            "do not create flow",
            "do not create a flow",
            "don't create flow",
            "don't create a flow",
            "how to create flow",
            "how to create a flow",
            "how do i create a flow",
        };

        public static bool HasCreateIntent(string? text)
        {
            var value = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (Array.Exists(NonActionMarkers, marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                return false;

            return Array.Exists(CreateIntentMarkers, marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        public static string ResolveFlowName(string? userText, string? suggestedName, DateTime? now = null)
        {
            var candidate = NormalizeName(suggestedName);
            if (!string.IsNullOrWhiteSpace(candidate) && !HasCreateIntent(candidate))
                return candidate;

            candidate = ExtractNamedFlow(userText);
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;

            return $"Flow_{(now ?? DateTime.Now):yyyyMMdd_HHmmss}";
        }

        private static string ExtractNamedFlow(string? text)
        {
            var value = (text ?? string.Empty).Trim();
            foreach (var marker in new[] { "名称为", "名字为", "名为", "叫做", "named" })
            {
                var markerIndex = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0)
                    continue;

                var candidate = NormalizeName(value[(markerIndex + marker.Length)..]);
                if (!string.IsNullOrWhiteSpace(candidate))
                    return candidate;
            }

            return string.Empty;
        }

        private static string NormalizeName(string? value)
        {
            var name = (value ?? string.Empty).Trim()
                .Trim('"', '\'', '“', '”', '‘', '’', '。', '.', '，', ',', '：', ':');
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            name = name.Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Replace("\t", " ", StringComparison.Ordinal)
                .Trim();
            return name.Length <= 64 ? name : name[..64].TrimEnd();
        }
    }
}

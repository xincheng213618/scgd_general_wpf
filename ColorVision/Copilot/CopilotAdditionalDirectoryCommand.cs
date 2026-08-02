using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal enum CopilotAdditionalDirectoryCommandAction
    {
        List,
        Add,
        Remove,
        Clear,
        Invalid,
    }

    internal sealed record CopilotAdditionalDirectoryCommandRequest(
        CopilotAdditionalDirectoryCommandAction Action,
        string Path = "",
        int Ordinal = 0);

    internal static class CopilotAdditionalDirectoryCommand
    {
        public const int MaximumDirectories = 8;
        public const int MaximumPathCharacters = 1_024;
        public const string Usage = "/add-dir [绝对目录|list|remove N|clear]";

        public static CopilotAdditionalDirectoryCommandRequest Parse(string? arguments)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0
                || string.Equals(normalized, "list", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotAdditionalDirectoryCommandRequest(
                    CopilotAdditionalDirectoryCommandAction.List);
            }

            if (string.Equals(normalized, "clear", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotAdditionalDirectoryCommandRequest(
                    CopilotAdditionalDirectoryCommandAction.Clear);
            }

            if (TryReadActionValue(normalized, "remove", out var removeValue))
            {
                return int.TryParse(removeValue, NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal)
                    && ordinal > 0
                    ? new CopilotAdditionalDirectoryCommandRequest(
                        CopilotAdditionalDirectoryCommandAction.Remove,
                        Ordinal: ordinal)
                    : Invalid();
            }

            if (string.Equals(normalized, "remove", StringComparison.OrdinalIgnoreCase))
                return Invalid();

            if (TryReadActionValue(normalized, "add", out var addValue))
            {
                return addValue.Length > 0
                    ? new CopilotAdditionalDirectoryCommandRequest(
                        CopilotAdditionalDirectoryCommandAction.Add,
                        TrimMatchingQuotes(addValue))
                    : Invalid();
            }

            if (string.Equals(normalized, "add", StringComparison.OrdinalIgnoreCase))
                return Invalid();

            return new CopilotAdditionalDirectoryCommandRequest(
                CopilotAdditionalDirectoryCommandAction.Add,
                TrimMatchingQuotes(normalized));
        }

        public static bool TryNormalizeExistingDirectory(
            string? path,
            out string normalizedPath,
            out string errorMessage)
        {
            normalizedPath = string.Empty;
            errorMessage = string.Empty;
            var candidate = TrimMatchingQuotes((path ?? string.Empty).Trim());
            if (candidate.Length == 0)
            {
                errorMessage = $"目录不能为空。用法：{Usage}";
                return false;
            }
            if (candidate.Length > MaximumPathCharacters || candidate.Any(char.IsControl))
            {
                errorMessage = $"目录路径必须不超过 {MaximumPathCharacters:N0} 个字符，且不能包含控制字符。";
                return false;
            }

            try
            {
                if (!Path.IsPathFullyQualified(candidate))
                {
                    errorMessage = "请提供现有目录的绝对路径；相对路径不会扩大当前会话的读取范围。";
                    return false;
                }

                var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
                if (!Directory.Exists(fullPath))
                {
                    errorMessage = "目录不存在或当前进程无法访问：" + fullPath;
                    return false;
                }

                normalizedPath = fullPath;
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                errorMessage = "目录路径无效。";
                return false;
            }
        }

        public static string[] NormalizeStoredPaths(IEnumerable<string>? paths)
        {
            return CopilotWorkspaceSearchSupport.NormalizeSearchRoots(paths)
                .Where(path => path.Length <= MaximumPathCharacters && !path.Any(char.IsControl))
                .Take(MaximumDirectories)
                .ToArray();
        }

        public static string Format(IReadOnlyList<string>? paths)
        {
            var normalized = NormalizeStoredPaths(paths);
            var builder = new StringBuilder()
                .AppendLine("附加只读目录 · 当前会话");
            if (normalized.Length == 0)
            {
                builder.AppendLine("当前没有附加目录。");
            }
            else
            {
                foreach (var (path, index) in normalized.Select((path, index) => (path, index)))
                {
                    builder.Append(index + 1)
                        .Append(". ")
                        .AppendLine(path);
                }
            }

            builder.AppendLine()
                .AppendLine($"用法：{Usage}")
                .AppendLine($"边界：最多 {MaximumDirectories:N0} 个；仅进入后续 Agent 请求的搜索与读取范围，不进入可写范围。")
                .Append("附加目录不会成为项目指令、Skill、Hook、MCP 或其他配置来源；Chat 模式也不会启用文件工具。");
            return builder.ToString();
        }

        private static bool TryReadActionValue(string arguments, string action, out string value)
        {
            value = string.Empty;
            if (!arguments.StartsWith(action, StringComparison.OrdinalIgnoreCase)
                || arguments.Length <= action.Length
                || !char.IsWhiteSpace(arguments[action.Length]))
            {
                return false;
            }

            value = arguments[action.Length..].Trim();
            return true;
        }

        private static string TrimMatchingQuotes(string value)
        {
            var normalized = value.Trim();
            if (normalized.Length < 2)
                return normalized;

            return (normalized[0], normalized[^1]) switch
            {
                ('"', '"') or ('\'', '\'') or ('“', '”') => normalized[1..^1].Trim(),
                _ => normalized,
            };
        }

        private static CopilotAdditionalDirectoryCommandRequest Invalid() =>
            new(CopilotAdditionalDirectoryCommandAction.Invalid);
    }
}

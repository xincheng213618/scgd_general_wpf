using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot.Mcp
{
    internal sealed record CopilotAutomaticApprovalDenialCommandResult(
        CopilotAutomaticApprovalDenialSnapshot? Denial,
        string Report)
    {
        public bool AuthorizesRetry => Denial != null;
    }

    internal static class CopilotAutomaticApprovalDenialCommand
    {
        private const int MaximumToolNameCharacters = 80;

        internal static CopilotAutomaticApprovalDenialCommandResult Evaluate(
            IEnumerable<CopilotAutomaticApprovalDenialSnapshot>? recentDenials,
            string? arguments,
            DateTimeOffset nowUtc)
        {
            var denials = (recentDenials ?? Array.Empty<CopilotAutomaticApprovalDenialSnapshot>())
                .Where(item => item != null)
                .OrderByDescending(item => item.DeniedAtUtc)
                .Take(CopilotAutomaticApprovalOverrideStore.MaximumRecentDenialsPerConversation)
                .ToArray();
            var selector = (arguments ?? string.Empty).Trim();
            if (denials.Length == 0)
            {
                return new CopilotAutomaticApprovalDenialCommandResult(
                    null,
                    "当前会话没有仍可授权精确重试的自动审查拒绝记录。");
            }

            if (selector.Length == 0)
                return new CopilotAutomaticApprovalDenialCommandResult(null, FormatList(denials, nowUtc));

            if (!int.TryParse(selector, NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal)
                || ordinal < 1
                || ordinal > denials.Length)
            {
                return new CopilotAutomaticApprovalDenialCommandResult(
                    null,
                    $"参数无效。请输入 /approve N，其中 N 为 1 到 {denials.Length.ToString("N0", CultureInfo.CurrentCulture)}。"
                    + Environment.NewLine
                    + Environment.NewLine
                    + FormatList(denials, nowUtc));
            }

            return new CopilotAutomaticApprovalDenialCommandResult(denials[ordinal - 1], string.Empty);
        }

        private static string FormatList(
            CopilotAutomaticApprovalDenialSnapshot[] denials,
            DateTimeOffset nowUtc)
        {
            var builder = new StringBuilder()
                .Append("当前会话自动审查拒绝 · ")
                .Append(denials.Length.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine()
                .AppendLine()
                .AppendLine("输入 /approve N，为一条精确拒绝动作授权一次重试。重试仍会经过自动审查，并可能再次被拒绝。");
            for (var index = 0; index < denials.Length; index++)
            {
                var denial = denials[index];
                builder.Append((index + 1).ToString(CultureInfo.InvariantCulture))
                    .Append(". ")
                    .Append(FormatToolName(denial.ToolName))
                    .Append(" · ")
                    .AppendLine(FormatAge(nowUtc - denial.DeniedAtUtc));
            }

            return builder
                .AppendLine()
                .Append("列表不显示操作 ID、参数、摘要指纹或工作区路径；授权只匹配原工具及精确参数的一次后续调用，不会批准相似操作。")
                .ToString();
        }

        private static string FormatToolName(string? toolName)
        {
            var text = CopilotMcpAuditLogger.RedactText(toolName ?? string.Empty);
            var normalized = string.Join(
                " ",
                text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (normalized.Length == 0)
                return "受保护操作";
            return normalized.Length <= MaximumToolNameCharacters
                ? normalized
                : normalized[..MaximumToolNameCharacters] + "…";
        }

        private static string FormatAge(TimeSpan age)
        {
            if (age < TimeSpan.Zero || age.TotalSeconds < 60)
                return "刚刚拒绝";
            if (age.TotalMinutes < 60)
                return $"{Math.Max(1, (int)age.TotalMinutes)} 分钟前拒绝";
            return $"{Math.Max(1, (int)age.TotalHours)} 小时前拒绝";
        }
    }
}

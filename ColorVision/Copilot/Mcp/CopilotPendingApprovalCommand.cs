using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot.Mcp
{
    internal sealed record CopilotPendingApprovalCommandResult(
        ConfirmableAction? Action,
        string Report)
    {
        public bool OpensReview => Action != null;
    }

    internal static class CopilotPendingApprovalCommand
    {
        private const int MaximumListedActions = 16;
        private const int MaximumToolNameCharacters = 80;

        public static CopilotPendingApprovalCommandResult Evaluate(
            IEnumerable<ConfirmableAction>? reviewableActions,
            string? arguments,
            DateTimeOffset nowUtc)
        {
            var actions = (reviewableActions ?? Array.Empty<ConfirmableAction>())
                .Where(action => action != null
                    && action.Status == ConfirmableActionStatus.Pending
                    && action.ExpiresAt > nowUtc)
                .OrderBy(action => action.ExpiresAt)
                .ThenBy(action => action.CreatedAt)
                .ToArray();
            var selector = (arguments ?? string.Empty).Trim();
            if (actions.Length == 0)
            {
                return new CopilotPendingApprovalCommandResult(
                    null,
                    "当前会话没有仍有效且可审核的待确认操作。");
            }

            if (selector.Length == 0)
            {
                return actions.Length == 1
                    ? new CopilotPendingApprovalCommandResult(actions[0], string.Empty)
                    : new CopilotPendingApprovalCommandResult(null, FormatList(actions, nowUtc));
            }

            if (!int.TryParse(selector, NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal)
                || ordinal < 1
                || ordinal > actions.Length)
            {
                var report = new StringBuilder();
                report.Append("参数无效。请输入 /approve N，其中 N 为 1 到 ")
                    .Append(actions.Length.ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine("。")
                    .AppendLine()
                    .Append(FormatList(actions, nowUtc));
                return new CopilotPendingApprovalCommandResult(null, report.ToString());
            }

            return new CopilotPendingApprovalCommandResult(actions[ordinal - 1], string.Empty);
        }

        private static string FormatList(
            ConfirmableAction[] actions,
            DateTimeOffset nowUtc)
        {
            var builder = new StringBuilder();
            builder.Append("当前会话待确认 · ")
                .Append(actions.Length.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine()
                .AppendLine()
                .AppendLine("输入 /approve N 打开原生审查窗口；该命令本身不会批准或执行操作。");
            for (var index = 0; index < Math.Min(actions.Length, MaximumListedActions); index++)
            {
                var action = actions[index];
                builder.Append((index + 1).ToString(CultureInfo.InvariantCulture))
                    .Append(". ")
                    .Append(FormatToolName(action.ToolName))
                    .Append(" · ")
                    .Append(FormatRisk(action.RiskLevel))
                    .Append(" · ")
                    .AppendLine(FormatRemainingLifetime(action.ExpiresAt - nowUtc));
            }
            if (actions.Length > MaximumListedActions)
            {
                builder.Append("…另有 ")
                    .Append((actions.Length - MaximumListedActions).ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 条操作未显示，仍可按编号打开。");
            }

            builder.AppendLine()
                .Append("列表按到期时间优先排列，不包含操作 ID、参数、完整审查详情或工作区路径；批准前必须在原生窗口中核对完整详情并再次确认。");
            return builder.ToString();
        }

        private static string FormatToolName(string? toolName)
        {
            var text = CopilotMcpAuditLogger.RedactText(toolName ?? string.Empty);
            var builder = new StringBuilder(Math.Min(text.Length, MaximumToolNameCharacters));
            var pendingSpace = false;
            foreach (var character in text)
            {
                if (char.IsWhiteSpace(character) || char.IsControl(character)
                    || char.GetUnicodeCategory(character) == UnicodeCategory.Format)
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace && builder.Length < MaximumToolNameCharacters)
                    builder.Append(' ');
                if (builder.Length >= MaximumToolNameCharacters)
                    break;
                builder.Append(character);
                pendingSpace = false;
            }

            var normalized = builder.ToString().Trim();
            return normalized.Length == 0 ? "受保护操作" : normalized;
        }

        private static string FormatRisk(string? riskLevel)
        {
            return (riskLevel ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "low" => "LOW",
                "medium" => "MEDIUM",
                "high" => "HIGH",
                "confirmation-required" => "受保护操作",
                _ => "风险待复核",
            };
        }

        private static string FormatRemainingLifetime(TimeSpan remaining)
        {
            if (remaining.TotalSeconds < 60)
                return $"剩余 {Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds))} 秒";
            if (remaining.TotalMinutes < 60)
                return $"剩余 {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))} 分钟";
            return $"剩余 {Math.Max(1, (int)Math.Ceiling(remaining.TotalHours))} 小时";
        }
    }
}

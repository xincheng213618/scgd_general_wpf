using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal sealed record CopilotWorkspaceRollbackPoint(
        int Ordinal,
        CopilotAgentTraceEntry Trace,
        CopilotChatMessage AssistantMessage,
        string Summary,
        int ChangedFileCount,
        DateTimeOffset ExpiresAtUtc);

    internal static class CopilotWorkspaceRollbackPointService
    {
        private const int MaximumListedPoints = 10;
        private const int MaximumListedFilesPerPoint = 2;

        public static IReadOnlyList<CopilotWorkspaceRollbackPoint> GetPoints(
            CopilotConversationRecord? conversation)
        {
            if (conversation == null)
                return Array.Empty<CopilotWorkspaceRollbackPoint>();

            var unavailableChangeSetIds = conversation.Messages
                .Where(message => message != null)
                .SelectMany(message => message.AgentTraceEntries ?? [])
                .Where(trace => IsActiveWorkspaceRollback(trace)
                    || trace?.IsCompletedWorkspaceRollback == true)
                .Select(trace => trace.WorkspaceChangeSetId)
                .ToHashSet(StringComparer.Ordinal);
            var seenChangeSetIds = new HashSet<string>(StringComparer.Ordinal);
            var points = new List<CopilotWorkspaceRollbackPoint>();
            for (var messageIndex = conversation.Messages.Count - 1; messageIndex >= 0; messageIndex--)
            {
                var message = conversation.Messages[messageIndex];
                if (message?.IsUser != false || message.AgentTraceEntries == null)
                    continue;

                for (var traceIndex = message.AgentTraceEntries.Count - 1; traceIndex >= 0; traceIndex--)
                {
                    var trace = message.AgentTraceEntries[traceIndex];
                    if (trace == null
                        || !string.Equals(trace.ToolName, "ApplyWorkspacePatchEnvelope", StringComparison.Ordinal)
                        || string.IsNullOrWhiteSpace(trace.WorkspaceChangeSetId)
                        || !seenChangeSetIds.Add(trace.WorkspaceChangeSetId)
                        || trace.CanRequestWorkspaceRollback != true
                        || unavailableChangeSetIds.Contains(trace.WorkspaceChangeSetId)
                        || !trace.WorkspaceChangeSetExpiresAtUtc.HasValue)
                    {
                        continue;
                    }

                    points.Add(new CopilotWorkspaceRollbackPoint(
                        points.Count + 1,
                        trace,
                        message,
                        BuildSummary(trace),
                        trace.WorkspaceChangedFiles?.Count ?? 0,
                        trace.WorkspaceChangeSetExpiresAtUtc.Value));
                }
            }
            return points;
        }

        public static bool TryResolve(
            CopilotConversationRecord? conversation,
            string? requestedOrdinal,
            out CopilotWorkspaceRollbackPoint point)
        {
            point = null!;
            if (!int.TryParse(
                    (requestedOrdinal ?? string.Empty).Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var ordinal)
                || ordinal <= 0)
            {
                return false;
            }

            point = GetPoints(conversation).FirstOrDefault(candidate => candidate.Ordinal == ordinal)!;
            return point != null;
        }

        public static string Format(CopilotConversationRecord? conversation)
        {
            var points = GetPoints(conversation);
            if (points.Count == 0)
            {
                return "当前会话没有仍可安全回滚的精确文件修改。"
                    + Environment.NewLine
                    + "只有通过安全工作区补丁完成、尚未撤销且未过期的修改会出现在这里。";
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var builder = new StringBuilder();
            builder.Append("安全文件回滚点 · ")
                .Append(points.Count.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine();
            builder.AppendLine();
            builder.AppendLine("输入 /rollback N 打开精确绑定的原生审批；1 表示最近一组修改。");
            foreach (var point in points.Take(MaximumListedPoints))
            {
                builder.Append(point.Ordinal.ToString(CultureInfo.InvariantCulture))
                    .Append(" · ")
                    .Append(point.Summary)
                    .Append(" · ")
                    .Append(FormatExpiry(point.ExpiresAtUtc, nowUtc))
                    .AppendLine();
            }
            if (points.Count > MaximumListedPoints)
            {
                builder.Append("…另有 ")
                    .Append((points.Count - MaximumListedPoints).ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 个更早回滚点，可直接输入对应序号。");
            }

            builder.AppendLine();
            builder.Append("回滚不调用模型，也不改变会话历史；只撤销所选快照中的文件修改。命令行、外部应用及其他非快照操作不在范围内。");
            return builder.ToString();
        }

        private static bool IsActiveWorkspaceRollback(CopilotAgentTraceEntry? trace)
        {
            return trace != null
                && string.Equals(trace.ToolName, "RollbackWorkspacePatchEnvelope", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(trace.WorkspaceChangeSetId)
                && trace.State is CopilotToolExecutionState.Pending
                    or CopilotToolExecutionState.Running
                    or CopilotToolExecutionState.AwaitingApproval;
        }

        private static string BuildSummary(CopilotAgentTraceEntry trace)
        {
            var files = trace.WorkspaceChangedFiles?
                .Where(file => file != null)
                .ToArray()
                ?? [];
            if (files.Length == 0)
                return "精确文件修改";

            var builder = new StringBuilder();
            foreach (var file in files.Take(MaximumListedFilesPerPoint))
            {
                if (builder.Length > 0)
                    builder.Append('、');
                builder.Append(file.DisplayLabel);
            }
            if (files.Length > MaximumListedFilesPerPoint)
            {
                builder.Append(" 等 ")
                    .Append(files.Length.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 个文件");
            }
            return builder.ToString();
        }

        private static string FormatExpiry(DateTimeOffset expiresAtUtc, DateTimeOffset nowUtc)
        {
            var remainingMinutes = Math.Max(
                1,
                (int)Math.Ceiling((expiresAtUtc - nowUtc).TotalMinutes));
            return remainingMinutes.ToString("N0", CultureInfo.CurrentCulture) + " 分钟后过期";
        }
    }
}

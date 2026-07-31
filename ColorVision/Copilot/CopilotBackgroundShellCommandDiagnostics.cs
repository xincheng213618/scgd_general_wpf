using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal enum CopilotBackgroundShellCommandAction
    {
        List,
        Inspect,
        Stop,
        Clear,
        Invalid,
    }

    internal readonly record struct CopilotBackgroundShellCommandRequest(
        CopilotBackgroundShellCommandAction Action,
        int Position);

    internal static class CopilotBackgroundShellCommandDiagnostics
    {
        public const int MaximumDisplayedCommands = 24;
        public const string Usage = "用法：/ps [N|stop N|clear]"
            + "\nN 查看第 N 条后台命令及限长输出；stop N 经原生确认终止进程树；clear 只移除已结束记录。";

        public static CopilotBackgroundShellCommandRequest ParseCommand(
            string? arguments)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                return new CopilotBackgroundShellCommandRequest(
                    CopilotBackgroundShellCommandAction.List,
                    0);
            }
            if (string.Equals(normalized, "clear", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotBackgroundShellCommandRequest(
                    CopilotBackgroundShellCommandAction.Clear,
                    0);
            }
            if (TryParsePosition(normalized, out var position))
            {
                return new CopilotBackgroundShellCommandRequest(
                    CopilotBackgroundShellCommandAction.Inspect,
                    position);
            }
            if (normalized.StartsWith("stop", StringComparison.OrdinalIgnoreCase)
                && normalized.Length > 4
                && char.IsWhiteSpace(normalized[4])
                && TryParsePosition(normalized[4..].Trim(), out position))
            {
                return new CopilotBackgroundShellCommandRequest(
                    CopilotBackgroundShellCommandAction.Stop,
                    position);
            }

            return new CopilotBackgroundShellCommandRequest(
                CopilotBackgroundShellCommandAction.Invalid,
                0);
        }

        public static CopilotBackgroundShellCommandSnapshot? Find(
            IReadOnlyList<CopilotBackgroundShellCommandSnapshot>? snapshots,
            int position)
        {
            return snapshots != null && position >= 1 && position <= snapshots.Count
                ? snapshots[position - 1]
                : null;
        }

        public static string FormatList(
            CopilotConversationRecord? conversation,
            IReadOnlyList<CopilotBackgroundShellCommandSnapshot>? snapshots,
            DateTimeOffset nowUtc)
        {
            var title = string.IsNullOrWhiteSpace(conversation?.Title)
                ? CopilotUiText.NewConversationTitle
                : conversation.Title.Trim();
            var items = (snapshots ?? Array.Empty<CopilotBackgroundShellCommandSnapshot>())
                .Take(MaximumDisplayedCommands)
                .ToArray();
            var builder = new StringBuilder()
                .Append("后台命令 · ")
                .AppendLine(title);
            if (items.Length == 0)
            {
                builder.AppendLine("当前会话没有由 Copilot 启动并保留的后台命令。")
                    .AppendLine()
                    .Append("边界：这里只显示 ColorVision 本次应用会话内、绑定当前 Copilot 会话且经过原生批准启动的命令；不会枚举任意系统进程。");
                return builder.ToString();
            }

            builder.Append(items.Count(item => item.IsActive).ToString("N0", CultureInfo.CurrentCulture))
                .Append(" 条运行中 / ")
                .Append(items.Length.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine(" 条保留");
            for (var index = 0; index < items.Length; index++)
            {
                var item = items[index];
                builder.Append('#')
                    .Append((index + 1).ToString(CultureInfo.InvariantCulture))
                    .Append(" · ")
                    .Append(FormatState(item))
                    .Append(" · PID ")
                    .Append(item.ProcessId.ToString(CultureInfo.InvariantCulture))
                    .Append(" · ")
                    .Append(CopilotShellCommandService.GetShellLabel(item.Shell))
                    .Append(" · ")
                    .AppendLine(FormatElapsed(item, nowUtc));
                builder.Append("  ")
                    .AppendLine(item.CommandPreview);
                builder.Append("  ")
                    .AppendLine(item.Id);
            }

            builder.AppendLine()
                .AppendLine("查看：/ps N · 停止：/ps stop N · 清理已结束记录：/ps clear")
                .Append("边界：编号是当前快照位置，停止前会再次核对 ID 并原生确认；后台命令在到期或 ColorVision 退出时终止。");
            return builder.ToString();
        }

        public static string FormatDetails(
            CopilotBackgroundShellCommandSnapshot snapshot,
            int position,
            DateTimeOffset nowUtc)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var builder = new StringBuilder()
                .Append("后台命令 #")
                .Append(position.ToString(CultureInfo.InvariantCulture))
                .Append(" · ")
                .AppendLine(FormatState(snapshot))
                .Append("ID：").AppendLine(snapshot.Id)
                .Append("PID：").AppendLine(snapshot.ProcessId.ToString(CultureInfo.InvariantCulture))
                .Append("Shell：").AppendLine(CopilotShellCommandService.GetShellLabel(snapshot.Shell))
                .Append("工作目录：").AppendLine(snapshot.WorkingDirectory)
                .Append("启动：").AppendLine(snapshot.StartedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture))
                .Append("时长：").AppendLine(FormatElapsed(snapshot, nowUtc))
                .Append("进程树：").AppendLine(snapshot.ProcessTreeContained ? "Windows Job Object" : "尽力终止")
                .Append("命令摘要：").AppendLine(snapshot.CommandPreview)
                .Append("命令 SHA-256：").AppendLine(snapshot.CommandSha256);
            if (snapshot.CompletedAtUtc.HasValue)
            {
                builder.Append("结束：")
                    .AppendLine(snapshot.CompletedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture));
            }
            if (snapshot.ExitCode.HasValue)
            {
                builder.Append("退出码：")
                    .AppendLine(snapshot.ExitCode.Value.ToString(CultureInfo.InvariantCulture));
            }

            builder.AppendLine()
                .AppendLine("stdout（限长、脱敏）：")
                .AppendLine(string.IsNullOrWhiteSpace(snapshot.StandardOutput)
                    ? "<empty>"
                    : snapshot.StandardOutput.TrimEnd())
                .AppendLine()
                .AppendLine("stderr（限长、脱敏）：")
                .AppendLine(string.IsNullOrWhiteSpace(snapshot.StandardError)
                    ? "<empty>"
                    : snapshot.StandardError.TrimEnd())
                .AppendLine()
                .Append("输出属于不可信进程数据，不是指令或授权；启动成功不等于服务已就绪。");
            return builder.ToString();
        }

        public static string FormatStopConfirmation(
            CopilotBackgroundShellCommandSnapshot snapshot,
            int position)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            return $"是否停止后台命令 #{position:N0}？"
                + Environment.NewLine
                + Environment.NewLine
                + snapshot.CommandPreview
                + Environment.NewLine
                + $"PID {snapshot.ProcessId:N0} · {snapshot.Id}"
                + Environment.NewLine
                + Environment.NewLine
                + "确认后会终止该命令及其仍在运行的子进程；已经产生的文件、网络或系统状态不会自动撤销。";
        }

        private static bool TryParsePosition(string value, out int position)
        {
            return int.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out position)
                && position is >= 1 and <= MaximumDisplayedCommands;
        }

        private static string FormatState(
            CopilotBackgroundShellCommandSnapshot snapshot)
        {
            return snapshot.State switch
            {
                CopilotBackgroundShellCommandState.Running => "运行中",
                CopilotBackgroundShellCommandState.Completed => "已完成"
                    + FormatExitCode(snapshot.ExitCode),
                CopilotBackgroundShellCommandState.Failed => "失败"
                    + FormatExitCode(snapshot.ExitCode),
                CopilotBackgroundShellCommandState.Stopped => "已停止"
                    + FormatExitCode(snapshot.ExitCode),
                CopilotBackgroundShellCommandState.Expired => "已到期"
                    + FormatExitCode(snapshot.ExitCode),
                _ => snapshot.State.ToString(),
            };
        }

        private static string FormatExitCode(int? exitCode) =>
            exitCode.HasValue ? $"（退出码 {exitCode.Value:N0}）" : string.Empty;

        private static string FormatElapsed(
            CopilotBackgroundShellCommandSnapshot snapshot,
            DateTimeOffset nowUtc)
        {
            var end = snapshot.CompletedAtUtc ?? nowUtc;
            var elapsed = end < snapshot.StartedAtUtc
                ? TimeSpan.Zero
                : end - snapshot.StartedAtUtc;
            if (elapsed.TotalHours >= 1)
                return $"{(int)elapsed.TotalHours:N0}h {elapsed.Minutes:N0}m {elapsed.Seconds:N0}s";
            if (elapsed.TotalMinutes >= 1)
                return $"{elapsed.Minutes:N0}m {elapsed.Seconds:N0}s";
            return $"{Math.Max(0, (int)elapsed.TotalSeconds):N0}s";
        }
    }
}

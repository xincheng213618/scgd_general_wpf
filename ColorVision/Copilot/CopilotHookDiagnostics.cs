using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal sealed class CopilotHookDiagnosticSnapshot
    {
        public CopilotToolExecutionHookRegistrySnapshot? HookSurface { get; init; }

        public CopilotToolExecutionHookBackgroundActivitySnapshot? BackgroundActivity { get; init; }

        public CopilotCodexAsyncHookActivitySnapshot? AsyncCommandActivity { get; init; }

        public IReadOnlyList<CopilotAgentExtensionSourceSnapshot> ExtensionSources { get; init; } =
            Array.Empty<CopilotAgentExtensionSourceSnapshot>();

        public IReadOnlyList<CopilotAgentExtensionIssue> ExtensionIssues { get; init; } =
            Array.Empty<CopilotAgentExtensionIssue>();

        public IReadOnlyList<string> ConfiguredHookFilePaths { get; init; } =
            Array.Empty<string>();

        public IReadOnlyList<CopilotCodexConfiguredHookIssue> ConfiguredHookIssues { get; init; } =
            Array.Empty<CopilotCodexConfiguredHookIssue>();

        public IReadOnlyList<CopilotToolExecutionAuditEntry> RecentToolExecutions { get; init; } =
            Array.Empty<CopilotToolExecutionAuditEntry>();
    }

    internal static class CopilotHookDiagnostics
    {
        private const int MaxEffectiveHooks = 32;
        private const int MaxExtensionSources = 16;
        private const int MaxExtensionHooks = 32;
        private const int MaxExtensionIssues = 12;
        private const int MaxRecentRuns = 20;

        public static string Format(CopilotHookDiagnosticSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var builder = new StringBuilder();
            builder.AppendLine("/hooks · 工具 Hook 快照");
            builder.AppendLine("本地只读诊断：不调用模型、工具或 MCP，不加载外部脚本，也不修改 Hook 或审批策略。");
            builder.AppendLine();
            AppendEffectiveHooks(builder, snapshot.HookSurface);
            AppendBackgroundActivity(builder, snapshot.BackgroundActivity);
            AppendAsyncCommandActivity(builder, snapshot.AsyncCommandActivity);
            builder.AppendLine();
            AppendExtensionSources(builder, snapshot.ExtensionSources, snapshot.ExtensionIssues);
            builder.AppendLine();
            AppendConfiguredHookSources(
                builder,
                snapshot.ConfiguredHookFilePaths,
                snapshot.ConfiguredHookIssues);
            builder.AppendLine();
            AppendRecentHealth(builder, snapshot.RecentToolExecutions);
            builder.AppendLine();
            builder.Append("安全边界：这里只显示来源、匹配器、状态、耗时与稳定失败码；不显示工具参数、结果正文或审批内容。");
            return builder.ToString().TrimEnd();
        }

        private static void AppendConfiguredHookSources(
            StringBuilder builder,
            IReadOnlyList<string>? sourceFilePaths,
            IReadOnlyList<CopilotCodexConfiguredHookIssue>? issues)
        {
            sourceFilePaths ??= Array.Empty<string>();
            issues ??= Array.Empty<CopilotCodexConfiguredHookIssue>();
            builder.Append("hooks.json：")
                .Append(FormatCount(sourceFilePaths.Count))
                .Append(" 个受信任来源 · ")
                .Append(FormatCount(issues.Count))
                .AppendLine(" 个配置问题");
            foreach (var path in sourceFilePaths.Take(8))
                builder.Append("  - ").AppendLine(FormatInline(path, "unknown", 260));
            if (sourceFilePaths.Count > 8)
            {
                builder.Append("  - ...另有 ")
                    .Append(FormatCount(sourceFilePaths.Count - 8))
                    .AppendLine(" 个来源未展开");
            }
            foreach (var issue in issues.Take(MaxExtensionIssues))
            {
                builder.Append("  ! ")
                    .Append(FormatInline(issue.SourceFilePath, "unknown", 180))
                    .Append(": ")
                    .AppendLine(FormatInline(issue.Message, "Invalid hook configuration.", 300));
            }
            if (issues.Count > MaxExtensionIssues)
            {
                builder.Append("  ! ...另有 ")
                    .Append(FormatCount(issues.Count - MaxExtensionIssues))
                    .AppendLine(" 个配置问题未展开");
            }
        }

        private static void AppendBackgroundActivity(
            StringBuilder builder,
            CopilotToolExecutionHookBackgroundActivitySnapshot? activity)
        {
            if (activity?.IsStructurallyValid() != true)
            {
                builder.AppendLine("后台活动：无有效运行时快照");
                return;
            }

            var value = activity.Value;
            builder.Append("后台活动：运行 ")
                .Append(FormatCount(value.RunningCount))
                .Append('/')
                .Append(FormatCount(value.MaximumConcurrency))
                .Append(" · 排队 ")
                .Append(FormatCount(value.QueuedCount))
                .Append(" · 未完成 ")
                .Append(FormatCount(value.OutstandingCount))
                .Append('/')
                .Append(FormatCount(value.MaximumPending))
                .Append(" · 超时占槽 ")
                .Append(FormatCount(value.TimedOutRetainedCount))
                .AppendLine();
        }

        private static void AppendAsyncCommandActivity(
            StringBuilder builder,
            CopilotCodexAsyncHookActivitySnapshot? activity)
        {
            if (activity?.IsStructurallyValid() != true)
            {
                builder.AppendLine("异步命令 Hook：无有效运行时快照");
                return;
            }

            var value = activity.Value;
            builder.Append("异步命令 Hook：会话 ")
                .Append(FormatCount(value.SessionCount))
                .Append(" · 运行 ")
                .Append(FormatCount(value.RunningCount))
                .Append(" · 排队 ")
                .Append(FormatCount(value.QueuedCount))
                .Append(" · 待投递 ")
                .Append(FormatCount(value.CompletedResultCount))
                .Append(" · 丢弃 ")
                .Append(FormatCount(value.DroppedResultCount))
                .Append(" · 单会话上限 ")
                .Append(FormatCount(value.MaximumConcurrencyPerSession))
                .Append('/')
                .Append(FormatCount(value.MaximumPendingPerSession))
                .AppendLine();
        }

        private static void AppendEffectiveHooks(
            StringBuilder builder,
            CopilotToolExecutionHookRegistrySnapshot? hookSurface)
        {
            if (hookSurface?.IsStructurallyValid() != true)
            {
                builder.AppendLine("生效定义：无有效运行时快照");
                return;
            }

            builder.Append("生效定义：")
                .Append(FormatCount(hookSurface.Entries.Count))
                .Append(" 个 · revision ")
                .Append(FormatCount(hookSurface.Revision))
                .Append(" · fingerprint ")
                .Append(hookSurface.Fingerprint[..Math.Min(12, hookSurface.Fingerprint.Length)])
                .AppendLine();
            foreach (var hook in hookSurface.Entries.Take(MaxEffectiveHooks))
            {
                builder.Append("  - ")
                    .Append(FormatInline(hook.SourceId, "unknown", 160))
                    .Append(" · matcher ")
                    .Append(FormatInline(hook.ToolNamePattern, "*", 160))
                    .Append(" · order ")
                    .Append(hook.Order.ToString(CultureInfo.InvariantCulture))
                    .Append(" · type ")
                    .Append(FormatInline(hook.HookType, "unknown", 200))
                    .Append(" · mode ")
                    .Append(FormatHookMode(hook.ExecutionMode))
                    .AppendLine();
            }
            if (hookSurface.Entries.Count > MaxEffectiveHooks)
            {
                builder.Append("  - ...另有 ")
                    .Append(FormatCount(hookSurface.Entries.Count - MaxEffectiveHooks))
                    .AppendLine(" 个生效定义未展开");
            }
        }

        private static void AppendExtensionSources(
            StringBuilder builder,
            IReadOnlyList<CopilotAgentExtensionSourceSnapshot>? extensionSources,
            IReadOnlyList<CopilotAgentExtensionIssue>? extensionIssues)
        {
            extensionSources ??= Array.Empty<CopilotAgentExtensionSourceSnapshot>();
            extensionIssues ??= Array.Empty<CopilotAgentExtensionIssue>();
            builder.Append("模块来源：")
                .Append(FormatCount(extensionSources.Count))
                .Append(" 个 · Hook ")
                .Append(FormatCount(extensionSources.Sum(source => Math.Max(0, source.ActiveHookCount))))
                .Append('/')
                .Append(FormatCount(extensionSources.Sum(source => Math.Max(0, source.DeclaredHookCount))))
                .AppendLine(" 个已生效/声明");

            foreach (var source in extensionSources.Take(MaxExtensionSources))
            {
                builder.Append("  - ")
                    .Append(FormatInline(source.SourceName, "Unnamed extension", 120));
                if (!string.IsNullOrWhiteSpace(source.SourceVersion))
                    builder.Append(" · v").Append(FormatInline(source.SourceVersion, string.Empty, 64));
                builder.Append(" · source ")
                    .Append(FormatInline(source.SourceId, "unknown", 120))
                    .Append(" · hooks ")
                    .Append(FormatCount(Math.Max(0, source.ActiveHookCount)))
                    .Append('/')
                    .Append(FormatCount(Math.Max(0, source.DeclaredHookCount)))
                    .AppendLine();
            }
            if (extensionSources.Count > MaxExtensionSources)
            {
                builder.Append("  - ...另有 ")
                    .Append(FormatCount(extensionSources.Count - MaxExtensionSources))
                    .AppendLine(" 个模块来源未展开");
            }

            var extensionHooks = extensionSources
                .SelectMany(source => source.Hooks ?? Array.Empty<CopilotAgentExtensionHookSnapshot>())
                .Take(MaxExtensionHooks + 1)
                .ToArray();
            if (extensionHooks.Length > 0)
            {
                builder.AppendLine("声明明细：");
                foreach (var hook in extensionHooks.Take(MaxExtensionHooks))
                {
                    builder.Append("  - ")
                        .Append(FormatInline(hook.SourceId, "unknown", 160))
                        .Append(" · ")
                        .Append(hook.IsActive ? "active" : "inactive")
                        .Append(" · matcher ")
                        .Append(FormatInline(hook.ToolNamePattern, "*", 160))
                        .Append(" · order ")
                        .Append(hook.Order.ToString(CultureInfo.InvariantCulture))
                        .Append(" · mode ")
                        .Append(FormatHookMode(hook.ExecutionMode))
                        .AppendLine();
                }
                if (extensionHooks.Length > MaxExtensionHooks)
                    builder.AppendLine("  - ...另有模块 Hook 声明未展开");
            }

            foreach (var issue in extensionIssues.Take(MaxExtensionIssues))
            {
                var failureCode = CopilotToolFailureCode.Normalize(issue.FailureCode);
                if (failureCode.Length == 0)
                    failureCode = CopilotAgentExtensionFailureCodes.ActivationFailed;
                builder.Append("  ! ")
                    .Append(FormatInline(issue.SourceId, "unknown", 120))
                    .Append(" · code ")
                    .Append(failureCode);
                if (!string.IsNullOrWhiteSpace(issue.CapabilityName))
                {
                    builder.Append(" · capability ")
                        .Append(FormatInline(issue.CapabilityName, "unknown", 120));
                }
                builder.Append(": ")
                    .Append(FormatInline(issue.Message, "No details provided.", 300))
                    .AppendLine();
            }
            if (extensionIssues.Count > MaxExtensionIssues)
            {
                builder.Append("  ! ...另有 ")
                    .Append(FormatCount(extensionIssues.Count - MaxExtensionIssues))
                    .AppendLine(" 个激活问题未展开");
            }
        }

        private static void AppendRecentHealth(
            StringBuilder builder,
            IReadOnlyList<CopilotToolExecutionAuditEntry>? recentToolExecutions)
        {
            recentToolExecutions ??= Array.Empty<CopilotToolExecutionAuditEntry>();
            var observations = recentToolExecutions
                .Where(entry => entry != null)
                .SelectMany(entry => (entry.HookRuns ?? Array.Empty<CopilotToolExecutionHookRun>())
                    .Where(run => run?.IsStructurallyValid() == true)
                    .Select(run => new HookObservation(entry, run)))
                .ToArray();
            builder.Append("最近健康度：")
                .Append(FormatCount(recentToolExecutions.Count))
                .Append(" 次工具调用 · ")
                .Append(FormatCount(observations.Length))
                .Append(" 次 Hook 运行");
            if (observations.Length > 0)
            {
                builder.Append("（完成 ")
                    .Append(FormatCount(CountState(observations, CopilotToolExecutionHookState.Completed)))
                    .Append("，后台已调度 ")
                    .Append(FormatCount(CountState(observations, CopilotToolExecutionHookState.Scheduled)))
                    .Append("，拒绝 ")
                    .Append(FormatCount(CountState(observations, CopilotToolExecutionHookState.Denied)))
                    .Append("，失败 ")
                    .Append(FormatCount(CountState(observations, CopilotToolExecutionHookState.Failed)))
                    .Append("，超时 ")
                    .Append(FormatCount(CountState(observations, CopilotToolExecutionHookState.TimedOut)))
                    .Append("，取消 ")
                    .Append(FormatCount(CountState(observations, CopilotToolExecutionHookState.Cancelled)))
                    .Append("，跳过 ")
                    .Append(FormatCount(CountState(observations, CopilotToolExecutionHookState.Skipped)))
                    .Append("，阻止 ")
                    .Append(FormatCount(CountState(observations, CopilotToolExecutionHookState.Blocked)))
                    .Append("，停止 ")
                    .Append(FormatCount(CountState(observations, CopilotToolExecutionHookState.Stopped)))
                    .Append('）');
            }
            builder.AppendLine();

            if (observations.Length == 0)
            {
                builder.AppendLine("  - 尚无可显示的逐 Hook 运行记录");
                return;
            }

            builder.AppendLine("最近逐 Hook 结果（按新到旧）：");
            foreach (var observation in observations.Reverse().Take(MaxRecentRuns))
            {
                var entry = observation.Entry;
                var run = observation.Run;
                builder.Append("  - ")
                    .Append(entry.StartedAtUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture))
                    .Append(" · ")
                    .Append(FormatInline(entry.ToolName, "unknown-tool", 100))
                    .Append('/')
                    .Append(entry.State)
                    .Append(" · ")
                    .Append(FormatHookPhase(run.Phase))
                    .Append(" · ")
                    .Append(FormatInline(run.SourceId, "unknown", 160))
                    .Append(" · ")
                    .Append(FormatHookMode(run.ExecutionMode))
                    .Append(" · ")
                    .Append(FormatHookState(run.State))
                    .Append(" · ")
                    .Append(FormatCount(run.DurationMs))
                    .Append(" ms");
                if (!string.IsNullOrWhiteSpace(run.FailureCode))
                    builder.Append(" · ").Append(FormatInline(run.FailureCode, "unknown_failure", 120));
                builder.AppendLine();
            }
            if (observations.Length > MaxRecentRuns)
            {
                builder.Append("  - ...另有 ")
                    .Append(FormatCount(observations.Length - MaxRecentRuns))
                    .AppendLine(" 次较早的 Hook 运行未展开");
            }
        }

        private static int CountState(
            IReadOnlyList<HookObservation> observations,
            CopilotToolExecutionHookState state) =>
            observations.Count(observation => observation.Run.State == state);

        private static string FormatHookState(CopilotToolExecutionHookState state) => state switch
        {
            CopilotToolExecutionHookState.Scheduled => "scheduled",
            CopilotToolExecutionHookState.Completed => "completed",
            CopilotToolExecutionHookState.Denied => "denied",
            CopilotToolExecutionHookState.Failed => "failed",
            CopilotToolExecutionHookState.TimedOut => "timed_out",
            CopilotToolExecutionHookState.Cancelled => "cancelled",
            CopilotToolExecutionHookState.Skipped => "skipped",
            CopilotToolExecutionHookState.Blocked => "blocked",
            CopilotToolExecutionHookState.Stopped => "stopped",
            _ => "unknown",
        };

        private static string FormatHookMode(CopilotToolExecutionHookMode mode) => mode switch
        {
            CopilotToolExecutionHookMode.Sync => "sync",
            CopilotToolExecutionHookMode.Async => "async",
            _ => "unknown",
        };

        private static string FormatHookPhase(CopilotToolExecutionHookPhase phase) => phase switch
        {
            CopilotToolExecutionHookPhase.PermissionRequest => "permission",
            CopilotToolExecutionHookPhase.BeforeExecute => "before",
            CopilotToolExecutionHookPhase.AfterExecute => "after",
            _ => "unknown",
        };

        private static string FormatInline(string? value, string fallback, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            var builder = new StringBuilder(Math.Min(value.Length, maxLength));
            var pendingSpace = false;
            foreach (var character in value.Trim())
            {
                if (char.IsWhiteSpace(character) || char.IsControl(character))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }
                if (pendingSpace && builder.Length < maxLength)
                    builder.Append(' ');
                pendingSpace = false;
                if (builder.Length >= maxLength)
                    break;
                builder.Append(character);
            }
            return builder.Length == 0 ? fallback : builder.ToString();
        }

        private static string FormatCount(long value) =>
            Math.Max(0, value).ToString("N0", CultureInfo.InvariantCulture);

        private sealed record HookObservation(
            CopilotToolExecutionAuditEntry Entry,
            CopilotToolExecutionHookRun Run);
    }
}

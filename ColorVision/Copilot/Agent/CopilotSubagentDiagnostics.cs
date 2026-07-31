using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal enum CopilotSubagentDiagnosticAction
    {
        Overview,
        Roles,
        Runs,
        Invalid,
        Stop,
        Steer,
        Show,
        Close,
        Active,
        Done,
    }

    internal readonly record struct CopilotSubagentDiagnosticRequest(
        CopilotSubagentDiagnosticAction Action,
        int Limit,
        string RunId,
        string Message);

    internal enum CopilotSubagentCloseResult
    {
        Closed,
        AlreadyClosed,
        Active,
        NotFound,
    }

    internal sealed record CopilotSubagentRunDiagnostic(
        string RunId,
        string ResumeFromRunId,
        string RoleId,
        bool Closed,
        CopilotToolExecutionState State,
        string Activity,
        CopilotAgentStopReason StopReason,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        long DurationMs,
        long QueueDurationMs,
        int RequestTokenBudget,
        long ConsumedTokens,
        int ProviderCalls,
        int ToolCalls,
        int DeliveredSteeringCount,
        int UndeliveredSteeringCount,
        int RegisteredToolCount,
        int AvailableToolCount,
        int AvailableToolDefinitionCharacters,
        int HarnessInstructionCharacters,
        CopilotToolFailureKind FailureKind,
        string FailureCode,
        bool RetryEligible,
        string AnswerText,
        bool AnswerHasSuccessfulEvidence,
        bool AnswerWasTruncated);

    internal static class CopilotSubagentDiagnostics
    {
        internal const int DefaultDisplayedRuns = 8;
        internal const int MaximumDisplayedRuns = 20;
        private const int MaximumRunSuggestionCharacters = 160;
        internal const string Usage = "用法：/agents [roles|runs [N]|active [N]|done [N]|show <run_id>|close <run_id>|steer <run_id> <message>|stop <run_id>]"
            + "\nN 可取 1–20；active 与 done 分别查看活动和已结束运行；show 查看单个子运行；close 从默认列表关闭已结束运行但保留结果与审计；steer 向运行中子代理排入新指令；stop 只停止该子代理；父 Agent 均继续运行；/subagents 为同义命令。";

        public static CopilotSubagentDiagnosticRequest ParseCommand(string? arguments)
        {
            var tokens = (arguments ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length == 0)
            {
                return new CopilotSubagentDiagnosticRequest(
                    CopilotSubagentDiagnosticAction.Overview,
                    DefaultDisplayedRuns,
                    string.Empty,
                    string.Empty);
            }
            if (tokens.Length == 1
                && string.Equals(tokens[0], "roles", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotSubagentDiagnosticRequest(
                    CopilotSubagentDiagnosticAction.Roles,
                    0,
                    string.Empty,
                    string.Empty);
            }
            CopilotSubagentDiagnosticAction? listAction = tokens[0].ToLowerInvariant() switch
            {
                "runs" => CopilotSubagentDiagnosticAction.Runs,
                "active" => CopilotSubagentDiagnosticAction.Active,
                "done" => CopilotSubagentDiagnosticAction.Done,
                _ => null,
            };
            if (listAction.HasValue
                && (tokens.Length == 1
                    || tokens.Length == 2
                    && int.TryParse(
                        tokens[1],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var limit)
                    && limit is >= 1 and <= MaximumDisplayedRuns))
            {
                return new CopilotSubagentDiagnosticRequest(
                    listAction.Value,
                    tokens.Length == 1
                        ? DefaultDisplayedRuns
                        : int.Parse(tokens[1], CultureInfo.InvariantCulture),
                    string.Empty,
                    string.Empty);
            }
            if (tokens.Length == 2
                && string.Equals(tokens[0], "show", StringComparison.OrdinalIgnoreCase)
                && IsValidRunId(tokens[1]))
            {
                return new CopilotSubagentDiagnosticRequest(
                    CopilotSubagentDiagnosticAction.Show,
                    0,
                    tokens[1],
                    string.Empty);
            }
            if (tokens.Length == 2
                && string.Equals(tokens[0], "close", StringComparison.OrdinalIgnoreCase)
                && IsValidRunId(tokens[1]))
            {
                return new CopilotSubagentDiagnosticRequest(
                    CopilotSubagentDiagnosticAction.Close,
                    0,
                    tokens[1],
                    string.Empty);
            }
            if (tokens.Length == 2
                && string.Equals(tokens[0], "stop", StringComparison.OrdinalIgnoreCase)
                && IsValidRunId(tokens[1]))
            {
                return new CopilotSubagentDiagnosticRequest(
                    CopilotSubagentDiagnosticAction.Stop,
                    0,
                    tokens[1],
                    string.Empty);
            }
            if (tokens.Length >= 3
                && string.Equals(tokens[0], "steer", StringComparison.OrdinalIgnoreCase)
                && IsValidRunId(tokens[1]))
            {
                var commandText = (arguments ?? string.Empty).Trim();
                var afterCommand = commandText[tokens[0].Length..].TrimStart();
                var message = afterCommand[tokens[1].Length..].Trim();
                if (message.Length is > 0 and <= CopilotSteeringMessagePolicy.MaximumMessageCharacters)
                {
                    return new CopilotSubagentDiagnosticRequest(
                        CopilotSubagentDiagnosticAction.Steer,
                        0,
                        tokens[1],
                        message);
                }
            }

            return new CopilotSubagentDiagnosticRequest(
                CopilotSubagentDiagnosticAction.Invalid,
                0,
                string.Empty,
                string.Empty);
        }

        public static string FormatSteeringResult(
            string runId,
            CopilotSteeringAdmissionResult result)
        {
            return result.Reason switch
            {
                CopilotSteeringAdmissionReason.Accepted when result.IsAccepted =>
                    $"已将新指令排入子代理 {runId}；父 Agent 将继续运行。",
                CopilotSteeringAdmissionReason.InvalidInput =>
                    "子代理运行中指令无效或过长；请提供有效 run_id 与不超过 16,000 字符的非空指令。",
                CopilotSteeringAdmissionReason.PendingUserQuestion =>
                    $"子代理 {runId} 正在等待用户回答，暂不能接收普通运行中指令。",
                CopilotSteeringAdmissionReason.QueueFull =>
                    $"子代理 {runId} 的运行中指令队列已满；请等待已有指令送达后重试。",
                CopilotSteeringAdmissionReason.RuntimeUnavailable =>
                    $"子代理 {runId} 仍在启动或切换阶段，运行时暂未准备好接收指令。",
                _ =>
                    $"当前会话中没有可接收指令的运行中子代理 {runId}；它可能已结束、正在停止或 run_id 已失效。",
            };
        }

        public static string FormatCancelResult(
            string runId,
            CopilotSubagentCancelResult result)
        {
            return result switch
            {
                CopilotSubagentCancelResult.Requested =>
                    $"已请求停止子代理 {runId}；父 Agent 将继续运行。",
                CopilotSubagentCancelResult.AlreadyRequested =>
                    $"子代理 {runId} 已在停止中；父 Agent 保持运行。",
                _ =>
                    $"当前会话中没有正在运行的子代理 {runId}；它可能已结束或 run_id 已失效。",
            };
        }

        public static CopilotSubagentCloseResult CloseRun(
            CopilotConversationRecord? conversation,
            string runId)
        {
            if (conversation == null || !IsValidRunId(runId))
                return CopilotSubagentCloseResult.NotFound;

            var matches = conversation.Messages
                .SelectMany(message =>
                    message?.AgentTraceEntries.AsEnumerable()
                        ?? Enumerable.Empty<CopilotAgentTraceEntry>())
                .Where(trace => trace != null
                    && string.Equals(trace.DelegatedRunId, runId, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
                return CopilotSubagentCloseResult.NotFound;
            if (matches.Any(trace => IsActive(trace.State)))
                return CopilotSubagentCloseResult.Active;
            if (matches.All(trace => trace.DelegatedRunClosed))
                return CopilotSubagentCloseResult.AlreadyClosed;

            foreach (var trace in matches)
                trace.DelegatedRunClosed = true;
            return CopilotSubagentCloseResult.Closed;
        }

        public static string FormatCloseResult(
            string runId,
            CopilotSubagentCloseResult result)
        {
            return result switch
            {
                CopilotSubagentCloseResult.Closed =>
                    $"已关闭子代理 {runId}；它将从默认运行列表与补全中隐藏，回答和审计指标仍保留，可继续用 /agents show {runId} 查看。",
                CopilotSubagentCloseResult.AlreadyClosed =>
                    $"子代理 {runId} 已关闭；可继续用 /agents show {runId} 查看保留详情。",
                CopilotSubagentCloseResult.Active =>
                    $"子代理 {runId} 仍在运行，不能关闭；可先等待完成或用 /agents stop {runId} 单独停止。",
                _ =>
                    $"当前会话没有可关闭的子代理 {runId}；请先用 /agents runs 查找有效 run_id。",
            };
        }

        public static IReadOnlyList<CopilotSubagentRunDiagnostic> CaptureRuns(
            CopilotConversationRecord? conversation,
            CopilotSubagentRoleCatalog? catalog = null,
            bool includeClosed = false)
        {
            if (conversation == null)
                return Array.Empty<CopilotSubagentRunDiagnostic>();

            catalog ??= CopilotSubagentRoleCatalog.Default;
            var runs = new List<CopilotSubagentRunDiagnostic>();
            for (var messageIndex = conversation.Messages.Count - 1; messageIndex >= 0; messageIndex--)
            {
                var message = conversation.Messages[messageIndex];
                if (message?.IsUser != false || message.AgentTraceEntries == null)
                    continue;

                for (var traceIndex = message.AgentTraceEntries.Count - 1; traceIndex >= 0; traceIndex--)
                {
                    var trace = message.AgentTraceEntries[traceIndex];
                    if (trace == null
                        || trace.DelegatedRunClosed && !includeClosed
                        || !TryResolveRoleId(trace, catalog, out var roleId))
                        continue;

                    runs.Add(new CopilotSubagentRunDiagnostic(
                        trace.DelegatedRunId,
                        trace.DelegatedResumeFromRunId,
                        roleId,
                        trace.DelegatedRunClosed,
                        trace.State,
                        trace.ProgressMessage,
                        trace.DelegatedStopReason,
                        trace.StartedAtUtc,
                        trace.CompletedAtUtc,
                        Math.Max(0, trace.DurationMs),
                        Math.Max(0, trace.DelegatedQueueDurationMs),
                        Math.Max(0, trace.DelegatedRequestTokenBudget),
                        Math.Max(0, trace.DelegatedConsumedTokens),
                        Math.Max(0, trace.DelegatedProviderCalls),
                        Math.Max(0, trace.DelegatedToolCalls),
                        Math.Max(0, trace.DelegatedDeliveredSteeringCount),
                        Math.Max(0, trace.DelegatedUndeliveredSteeringCount),
                        Math.Max(0, trace.DelegatedRegisteredToolCount),
                        Math.Clamp(
                            trace.DelegatedAvailableToolCount,
                            0,
                            Math.Max(0, trace.DelegatedRegisteredToolCount)),
                        Math.Max(0, trace.DelegatedAvailableToolDefinitionCharacters),
                        Math.Max(0, trace.DelegatedHarnessInstructionCharacters),
                        trace.FailureKind,
                        trace.FailureCode,
                        trace.RetryEligible,
                        trace.DelegatedAnswerText,
                        trace.DelegatedAnswerHasSuccessfulEvidence,
                        trace.DelegatedAnswerWasTruncated));
                }
            }
            return runs
                .OrderByDescending(run => IsActive(run.State))
                .ToArray();
        }

        internal static IReadOnlyList<CopilotLocalCommandArgument> BuildRunArguments(
            CopilotConversationRecord? conversation,
            string? action)
        {
            var normalizedAction = (action ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedAction is not ("show" or "close" or "stop" or "steer"))
                return Array.Empty<CopilotLocalCommandArgument>();

            var requiresActiveRun = normalizedAction is "stop" or "steer";
            var requiresCompletedRun = normalizedAction == "close";
            return CaptureRuns(conversation)
                .Where(run => IsValidRunId(run.RunId))
                .Where(run => !requiresActiveRun || IsActive(run.State))
                .Where(run => !requiresCompletedRun || !IsActive(run.State))
                .DistinctBy(run => run.RunId, StringComparer.Ordinal)
                .Take(MaximumDisplayedRuns)
                .Select(run => new CopilotLocalCommandArgument(
                    normalizedAction + " " + run.RunId,
                    FormatRunSuggestion(run),
                    AcceptsArguments: normalizedAction == "steer"))
                .ToArray();
        }

        public static string Format(
            CopilotConversationRecord? conversation,
            string? arguments,
            CopilotSubagentRoleCatalog? catalog = null)
        {
            var request = ParseCommand(arguments);
            if (request.Action is CopilotSubagentDiagnosticAction.Invalid
                or CopilotSubagentDiagnosticAction.Stop
                or CopilotSubagentDiagnosticAction.Steer
                or CopilotSubagentDiagnosticAction.Close)
                return Usage;

            catalog ??= CopilotSubagentRoleCatalog.Default;
            var title = string.IsNullOrWhiteSpace(conversation?.Title)
                ? CopilotUiText.NewConversationTitle
                : conversation.Title.Trim();
            var builder = new StringBuilder()
                .Append("子代理 · ")
                .AppendLine(title)
                .Append("运行模型：请求级只读委派 · 并发硬上限 ")
                .Append(CopilotSubagentCoordinator.MaximumConcurrentRuns.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" · 单次硬上限 ")
                .Append(FormatTokens(CopilotSubagentCoordinator.MaximumRunTokenBudget))
                .Append(" tokens · 请求合计硬上限 ")
                .Append(FormatTokens(CopilotSubagentCoordinator.MaximumTotalTokenBudget))
                .AppendLine(" tokens")
                .Append("角色目录：")
                .Append(catalog.Roles.Count.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" 个 · revision ")
                .AppendLine(catalog.Revision.ToString(CultureInfo.InvariantCulture))
                .AppendLine("实际子运行 Token 预算还会根据父任务预算下调。");

            if (request.Action is CopilotSubagentDiagnosticAction.Overview
                or CopilotSubagentDiagnosticAction.Roles)
            {
                AppendRoles(builder, catalog);
            }

            if (request.Action is CopilotSubagentDiagnosticAction.Overview
                or CopilotSubagentDiagnosticAction.Runs)
            {
                AppendRuns(
                    builder,
                    CaptureRuns(conversation, catalog),
                    request.Limit,
                    "运行概览",
                    "当前会话没有可见的子代理运行轨迹。",
                    "运行中优先，同状态新到旧",
                    "较低优先级或较早");
            }
            else if (request.Action == CopilotSubagentDiagnosticAction.Active)
            {
                AppendRuns(
                    builder,
                    CaptureRuns(conversation, catalog).Where(run => IsActive(run.State)).ToArray(),
                    request.Limit,
                    "活动运行",
                    "当前会话没有活动的子代理运行。",
                    "新到旧",
                    "较早");
            }
            else if (request.Action == CopilotSubagentDiagnosticAction.Done)
            {
                AppendRuns(
                    builder,
                    CaptureRuns(conversation, catalog).Where(run => !IsActive(run.State)).ToArray(),
                    request.Limit,
                    "已结束运行",
                    "当前会话没有可见的已结束子代理运行。",
                    "新到旧",
                    "较早");
            }
            else if (request.Action == CopilotSubagentDiagnosticAction.Show)
            {
                AppendRunDetails(
                    builder,
                    CaptureRuns(conversation, catalog, includeClosed: true),
                    request.RunId);
            }

            builder.AppendLine()
                .Append("边界：子代理由父 Agent 按请求创建并回传结果；运行期间可按 run_id 排入新指令或单独停止，父 Agent 继续运行；同一父请求内，可用完成结果给出的 run_id 续跑同角色且具有有效 checkpoint 的子代理。")
                .Append("已结束运行可从默认列表关闭，但回答与审计仍保留，并可按已知 run_id 直接查看。")
                .Append("它仍不是可切换、跨请求或应用重启后可恢复的独立会话。")
                .Append("runs、active、done 列表仅显示限长运行元数据；show 只显示当前会话保存的、已脱敏且限长的子代理回答，不显示任务提示、工具参数、原始工具结果或隐藏推理。");
            return builder.ToString();
        }

        private static bool IsValidRunId(string value)
        {
            return value.Length is >= 1 and <= 120
                && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
        }

        private static bool IsActive(CopilotToolExecutionState state) =>
            state is CopilotToolExecutionState.Pending
                or CopilotToolExecutionState.Running
                or CopilotToolExecutionState.AwaitingApproval;

        private static string FormatRunSuggestion(CopilotSubagentRunDiagnostic run)
        {
            var state = run.State switch
            {
                CopilotToolExecutionState.Pending => "正在启动",
                CopilotToolExecutionState.Running => "运行中",
                CopilotToolExecutionState.AwaitingApproval => "等待确认",
                CopilotToolExecutionState.Completed => "已完成",
                CopilotToolExecutionState.Cancelled => "已停止",
                CopilotToolExecutionState.TimedOut => "已超时",
                CopilotToolExecutionState.Interrupted => "已中断",
                CopilotToolExecutionState.Denied => "已拒绝",
                _ => "失败",
            };
            var result = string.IsNullOrWhiteSpace(run.AnswerText)
                ? string.Empty
                : " · 有结果";
            var activity = IsActive(run.State) && !string.IsNullOrWhiteSpace(run.Activity)
                ? " · " + run.Activity.Trim()
                : string.Empty;
            var description = run.RoleId + " · " + state + result + activity;
            return description.Length <= MaximumRunSuggestionCharacters
                ? description
                : description[..(MaximumRunSuggestionCharacters - 3)].TrimEnd() + "...";
        }

        private static bool TryResolveRoleId(
            CopilotAgentTraceEntry trace,
            CopilotSubagentRoleCatalog catalog,
            out string roleId)
        {
            roleId = (trace.DelegatedRoleId ?? string.Empty).Trim();
            if (roleId.Length > 0)
                return true;
            if (!string.IsNullOrWhiteSpace(trace.ToolName)
                && catalog.TryGetByToolName(trace.ToolName, out var role))
            {
                roleId = role!.Id;
                return true;
            }
            if (!string.IsNullOrWhiteSpace(trace.DelegatedRunId)
                && trace.ToolName.StartsWith("Delegate", StringComparison.Ordinal))
            {
                roleId = "unknown";
                return true;
            }
            return false;
        }

        private static void AppendRoles(
            StringBuilder builder,
            CopilotSubagentRoleCatalog catalog)
        {
            builder.AppendLine()
                .AppendLine("角色");
            foreach (var role in catalog.Roles.OrderBy(role => role.Id, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append("- ")
                    .Append(role.Id)
                    .Append(" · ")
                    .Append(role.DisplayName)
                    .Append(" · ")
                    .Append(FormatContextScope(role.ContextScope))
                    .Append(" · 子模式 ")
                    .AppendLine(role.ChildMode.ToString())
                    .Append("  来源：")
                    .Append(role.SourceName)
                    .Append(" [")
                    .Append(role.SourceId)
                    .Append("] v")
                    .Append(role.SourceVersion)
                    .Append(" · tool ")
                    .AppendLine(role.ToolName)
                    .Append("  工具：")
                    .AppendLine(FormatCapabilities(role.ReadCapabilities))
                    .Append("  边界：最多 ")
                    .Append(role.MaximumToolCalls.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 次工具调用 · ")
                    .Append(role.MaximumAgentPasses.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 个 Agent pass · ")
                    .Append(FormatDuration((long)role.MaximumDuration.TotalMilliseconds))
                    .Append(" · 回答 ")
                    .Append(role.MaximumAnswerCharacters.ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 字符")
                    .Append("  父模式：")
                    .AppendLine(string.Join(", ", role.ParentModes));
            }
        }

        private static void AppendRuns(
            StringBuilder builder,
            IReadOnlyList<CopilotSubagentRunDiagnostic> runs,
            int limit,
            string heading,
            string emptyMessage,
            string orderingDescription,
            string hiddenRunDescription)
        {
            builder.AppendLine()
                .AppendLine(heading);
            if (runs.Count == 0)
            {
                builder.AppendLine(emptyMessage);
                return;
            }

            var visibleRuns = runs.Take(limit).ToArray();
            builder.Append("显示 ")
                .Append(visibleRuns.Length.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" / ")
                .Append(runs.Count.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" 次（")
                .Append(orderingDescription)
                .AppendLine("）");
            for (var index = 0; index < visibleRuns.Length; index++)
            {
                var run = visibleRuns[index];
                builder.Append('#')
                    .Append((index + 1).ToString(CultureInfo.InvariantCulture))
                    .Append(" · ")
                    .Append(run.RoleId)
                    .Append(" · ")
                    .Append(string.IsNullOrWhiteSpace(run.RunId) ? "ID 待回传" : run.RunId)
                    .Append(" · state=")
                    .Append(run.State);
                if (!string.IsNullOrWhiteSpace(run.ResumeFromRunId))
                    builder.Append(" · resumed_from=").Append(run.ResumeFromRunId);
                if (run.StopReason != CopilotAgentStopReason.None)
                    builder.Append(" · stop=").Append(run.StopReason);
                if (run.State == CopilotToolExecutionState.Running
                    && !string.IsNullOrWhiteSpace(run.Activity))
                {
                    builder.Append(" · activity=").Append(run.Activity);
                }
                builder.AppendLine();

                builder.Append("  ");
                var hasDetail = false;
                if (run.StartedAtUtc != default)
                {
                    builder.Append(run.StartedAtUtc.ToLocalTime().ToString("MM-dd HH:mm:ss", CultureInfo.CurrentCulture));
                    hasDetail = true;
                }
                if (run.DurationMs > 0)
                {
                    AppendSeparator(builder, ref hasDetail);
                    builder.Append("耗时 ").Append(FormatDuration(run.DurationMs));
                }
                if (run.QueueDurationMs > 0)
                {
                    AppendSeparator(builder, ref hasDetail);
                    builder.Append("排队 ").Append(FormatDuration(run.QueueDurationMs));
                }
                if (run.RequestTokenBudget > 0 || run.ConsumedTokens > 0)
                {
                    AppendSeparator(builder, ref hasDetail);
                    builder.Append("tokens ")
                        .Append(FormatTokens(run.ConsumedTokens))
                        .Append('/')
                        .Append(FormatTokens(run.RequestTokenBudget));
                }
                if (run.ProviderCalls > 0 || run.ToolCalls > 0)
                {
                    AppendSeparator(builder, ref hasDetail);
                    builder.Append("模型 ")
                        .Append(run.ProviderCalls.ToString("N0", CultureInfo.CurrentCulture))
                        .Append(" · 工具 ")
                        .Append(run.ToolCalls.ToString("N0", CultureInfo.CurrentCulture));
                }
                if (run.RegisteredToolCount > 0)
                {
                    AppendSeparator(builder, ref hasDetail);
                    builder.Append("工具面 ")
                        .Append(run.AvailableToolCount.ToString("N0", CultureInfo.CurrentCulture))
                        .Append('/')
                        .Append(run.RegisteredToolCount.ToString("N0", CultureInfo.CurrentCulture));
                }
                if (!hasDetail)
                    builder.Append("等待子运行回传用量");
                builder.AppendLine();
            }

            if (visibleRuns.Length < runs.Count)
            {
                builder.Append("另有 ")
                    .Append((runs.Count - visibleRuns.Length).ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 次")
                    .Append(hiddenRunDescription)
                    .AppendLine("运行未显示。");
            }
        }

        private static void AppendRunDetails(
            StringBuilder builder,
            IReadOnlyList<CopilotSubagentRunDiagnostic> runs,
            string runId)
        {
            builder.AppendLine()
                .AppendLine("运行详情");
            var run = runs.FirstOrDefault(candidate =>
                string.Equals(candidate.RunId, runId, StringComparison.Ordinal));
            if (run == null)
            {
                builder.Append("当前会话没有可查看的子代理运行 ")
                    .Append(runId)
                    .AppendLine("；请先用 /agents runs 查找有效 run_id。");
                return;
            }

            builder.Append(run.RoleId)
                .Append(" · ")
                .Append(run.RunId)
                .Append(" · state=")
                .Append(run.State);
            if (run.Closed)
                builder.Append(" · closed=true");
            if (!string.IsNullOrWhiteSpace(run.ResumeFromRunId))
                builder.Append(" · resumed_from=").Append(run.ResumeFromRunId);
            if (run.StopReason != CopilotAgentStopReason.None)
                builder.Append(" · stop=").Append(run.StopReason);
            builder.AppendLine();

            var hasTiming = false;
            if (run.StartedAtUtc != default)
            {
                builder.Append("开始 ")
                    .Append(run.StartedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture));
                hasTiming = true;
            }
            if (run.CompletedAtUtc.HasValue)
            {
                AppendSeparator(builder, ref hasTiming);
                builder.Append("完成 ")
                    .Append(run.CompletedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture));
            }
            if (run.DurationMs > 0)
            {
                AppendSeparator(builder, ref hasTiming);
                builder.Append("耗时 ").Append(FormatDuration(run.DurationMs));
            }
            if (run.QueueDurationMs > 0)
            {
                AppendSeparator(builder, ref hasTiming);
                builder.Append("排队 ").Append(FormatDuration(run.QueueDurationMs));
            }
            if (hasTiming)
                builder.AppendLine();

            builder.Append("用量：tokens ")
                .Append(FormatTokens(run.ConsumedTokens))
                .Append('/')
                .Append(FormatTokens(run.RequestTokenBudget))
                .Append(" · 模型 ")
                .Append(run.ProviderCalls.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" · 工具 ")
                .Append(run.ToolCalls.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine();
            builder.Append("工具面：")
                .Append(run.AvailableToolCount.ToString("N0", CultureInfo.CurrentCulture))
                .Append('/')
                .Append(run.RegisteredToolCount.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" · 定义 ")
                .Append(run.AvailableToolDefinitionCharacters.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" 字符 · harness ")
                .Append(run.HarnessInstructionCharacters.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine(" 字符");
            builder.Append("运行中指令：已送达 ")
                .Append(run.DeliveredSteeringCount.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" · 未送达 ")
                .Append(run.UndeliveredSteeringCount.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine();
            if (run.FailureKind != CopilotToolFailureKind.None)
            {
                builder.Append("失败：")
                    .Append(run.FailureKind);
                if (!string.IsNullOrWhiteSpace(run.FailureCode))
                    builder.Append(" · code=").Append(run.FailureCode);
                builder.Append(" · retry=")
                    .AppendLine(run.RetryEligible ? "yes" : "no");
            }

            builder.Append("结果：");
            if (string.IsNullOrWhiteSpace(run.AnswerText))
            {
                builder.AppendLine(run.State is CopilotToolExecutionState.Pending or CopilotToolExecutionState.Running
                    ? "子代理仍在运行，尚未回传可展示回答。"
                    : "该运行没有保存可展示回答。");
                return;
            }

            builder.AppendLine()
                .AppendLine(run.AnswerText)
                .Append("结果证明：")
                .Append(run.AnswerHasSuccessfulEvidence ? "已取得成功工具证据" : "未取得成功工具证据")
                .Append(" · 输出截断：")
                .AppendLine(run.AnswerWasTruncated ? "是" : "否");
        }

        private static void AppendSeparator(StringBuilder builder, ref bool hasValue)
        {
            if (hasValue)
                builder.Append(" · ");
            hasValue = true;
        }

        private static string FormatContextScope(CopilotSubagentContextScope scope) =>
            scope switch
            {
                CopilotSubagentContextScope.WorkspaceReadOnly => "工作区只读",
                CopilotSubagentContextScope.PublicWeb => "公共网页只读",
                _ => scope.ToString(),
            };

        private static string FormatCapabilities(CopilotSubagentReadCapabilities capabilities)
        {
            var values = new List<string>();
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.SearchFiles))
                values.Add("SearchFiles");
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.GrepText))
                values.Add("GrepText");
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.ReadLocalFile))
                values.Add("ReadLocalFile");
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.ListDirectory))
                values.Add("ListDirectory");
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.WebSearch))
                values.Add("WebSearch");
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.FetchUrl))
                values.Add("FetchUrl");
            return values.Count == 0 ? "无" : string.Join(", ", values);
        }

        private static string FormatDuration(long milliseconds)
        {
            var normalized = Math.Max(0, milliseconds);
            if (normalized < 1000)
                return normalized.ToString("N0", CultureInfo.CurrentCulture) + "ms";
            if (normalized < 60_000)
                return (normalized / 1000d).ToString("0.#", CultureInfo.CurrentCulture) + "s";

            var duration = TimeSpan.FromMilliseconds(Math.Min(
                normalized,
                TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond));
            return duration.TotalHours < 1
                ? $"{(int)duration.TotalMinutes:N0}m {duration.Seconds:N0}s"
                : $"{(int)duration.TotalHours:N0}h {duration.Minutes:N0}m";
        }

        private static string FormatTokens(long value) =>
            Math.Max(0, value).ToString("N0", CultureInfo.CurrentCulture);
    }
}

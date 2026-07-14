using ColorVision.Common.MVVM;
using ColorVision.Copilot.Mcp;
using Newtonsoft.Json;
using System;
using System.Text;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentTraceEntry : ViewModelBase
    {
        public const int CurrentSchemaVersion = 4;
        private const int MaxSummaryLength = 800;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public string CallId { get; set; } = string.Empty;

        public int Round { get; set; }

        public int Attempt { get; set; } = 1;

        public int MaxAttempts { get; set; } = 1;

        public string RuntimeName { get; set; } = string.Empty;

        public string ToolName { get; set; } = string.Empty;

        public CopilotToolAccess Access { get; set; }

        public CopilotToolRiskLevel RiskLevel { get; set; }

        public CopilotToolApprovalMode ApprovalMode { get; set; }

        public CopilotToolIdempotency Idempotency { get; set; }

        public CopilotToolConcurrencyMode ConcurrencyMode { get; set; }

        public string ConcurrencyKey { get; set; } = string.Empty;

        public string ApprovalActionId { get; set; } = string.Empty;

        public CopilotToolExecutionState State { get; set; } = CopilotToolExecutionState.Pending;

        public CopilotToolFailureKind FailureKind { get; set; }

        public bool RetryEligible { get; set; }

        public DateTimeOffset StartedAtUtc { get; set; }

        public DateTimeOffset? CompletedAtUtc { get; set; }

        public long DurationMs { get; set; }

        public long QueueDurationMs { get; set; }

        public long TimeoutMs { get; set; }

        public string ArgumentSummary { get; set; } = string.Empty;

        public string ResultSummary { get; set; } = string.Empty;

        public string ErrorMessage { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsFailure => State is CopilotToolExecutionState.Failed
            or CopilotToolExecutionState.TimedOut
            or CopilotToolExecutionState.Denied
            or CopilotToolExecutionState.Cancelled
            or CopilotToolExecutionState.Interrupted;

        [JsonIgnore]
        public bool IsVisibleInActivity => !IsFailedSearchAttempt();

        [JsonIgnore]
        public string ActivityGlyph => State switch
        {
            CopilotToolExecutionState.Completed => "✓",
            CopilotToolExecutionState.Failed or CopilotToolExecutionState.TimedOut => "!",
            CopilotToolExecutionState.Denied or CopilotToolExecutionState.Cancelled or CopilotToolExecutionState.Interrupted => "×",
            CopilotToolExecutionState.AwaitingApproval => "?",
            _ => "·",
        };

        [JsonIgnore]
        public string ActivityLabel => BuildActivityLabel();

        [JsonIgnore]
        public string ActivityDurationLabel => CompletedAtUtc != null && DurationMs > 0 ? FormatDuration(DurationMs) : string.Empty;

        [JsonIgnore]
        public string ActivityDescription
        {
            get
            {
                var text = IsFailure ? BuildFriendlyFailureSummary() : BuildFriendlySuccessSummary();
                return TrimForActivity(text, 180);
            }
        }

        [JsonIgnore]
        public string DiagnosticDetails
        {
            get
            {
                var builder = new StringBuilder();
                builder.Append("[Round ").Append(Math.Max(1, Round)).Append(" · ").Append(ToolName);
                if (Attempt > 1 || RetryEligible)
                    builder.Append(" · Attempt ").Append(Attempt).Append('/').Append(MaxAttempts);
                builder.Append("] ").Append(FormatDiagnosticState(State));
                if (CompletedAtUtc != null && DurationMs > 0)
                    builder.Append(" · ").Append(FormatDuration(DurationMs));
                if (QueueDurationMs > 0)
                    builder.Append(" · queued ").Append(FormatDuration(QueueDurationMs));
                if (!string.IsNullOrWhiteSpace(RuntimeName))
                    builder.AppendLine().Append("Runtime: ").Append(RuntimeName)
                        .Append(" · Access: ").Append(Access)
                        .Append(" · Risk: ").Append(RiskLevel)
                        .Append(" · Approval: ").Append(ApprovalMode)
                        .Append(" · Idempotency: ").Append(Idempotency)
                        .Append(" · Concurrency: ").Append(ConcurrencyMode);
                if (!string.IsNullOrWhiteSpace(ConcurrencyKey))
                    builder.AppendLine().Append("Resource: ").Append(ConcurrencyKey);
                if (FailureKind != CopilotToolFailureKind.None)
                    builder.AppendLine().Append("Failure: ").Append(FailureKind)
                        .Append(" · Retry eligible: ").Append(RetryEligible ? "yes" : "no");
                if (!string.IsNullOrWhiteSpace(ApprovalActionId))
                    builder.AppendLine().Append("Approval action: ").Append(ApprovalActionId);
                if (!string.IsNullOrWhiteSpace(ArgumentSummary) && ArgumentSummary != "(none)")
                    builder.AppendLine().Append("Arguments: ").Append(ArgumentSummary);
                if (!string.IsNullOrWhiteSpace(ResultSummary))
                    builder.AppendLine().Append(ResultSummary);
                if (!string.IsNullOrWhiteSpace(ErrorMessage))
                    builder.AppendLine().Append("Error: ").Append(ErrorMessage);
                return builder.ToString().TrimEnd();
            }
        }

        public static CopilotAgentTraceEntry FromStarted(CopilotToolExecutionInfo execution)
        {
            ArgumentNullException.ThrowIfNull(execution);
            return FromExecution(execution);
        }

        public static CopilotAgentTraceEntry FromResult(CopilotToolExecutionInfo execution, CopilotToolResult? result)
        {
            ArgumentNullException.ThrowIfNull(execution);
            var entry = FromExecution(execution);
            if (result != null)
            {
                var summary = !string.IsNullOrWhiteSpace(result.Summary) ? result.Summary : result.Content;
                entry.ResultSummary = Sanitize(summary);
                entry.ErrorMessage = result.Success ? string.Empty : Sanitize(result.ErrorMessage);
            }

            return entry;
        }

        public bool EnsureValid(DateTimeOffset recoveredAtUtc)
        {
            var changed = false;
            var originalSchemaVersion = SchemaVersion;
            var originalCallId = CallId;
            var originalRuntimeName = RuntimeName;
            var originalToolName = ToolName;
            var originalArgumentSummary = ArgumentSummary;
            var originalApprovalActionId = ApprovalActionId;
            var originalConcurrencyKey = ConcurrencyKey;
            var originalResultSummary = ResultSummary;
            var originalErrorMessage = ErrorMessage;
            var originalRound = Round;
            var originalAttempt = Attempt;
            var originalMaxAttempts = MaxAttempts;
            var originalDurationMs = DurationMs;
            var originalQueueDurationMs = QueueDurationMs;
            var originalTimeoutMs = TimeoutMs;
            SchemaVersion = CurrentSchemaVersion;
            CallId = SanitizeIdentifier(CallId);
            RuntimeName = SanitizeIdentifier(RuntimeName);
            ToolName = SanitizeIdentifier(ToolName);
            ArgumentSummary = Sanitize(ArgumentSummary);
            ApprovalActionId = SanitizeIdentifier(ApprovalActionId);
            ConcurrencyKey = SanitizeIdentifier(ConcurrencyKey);
            ResultSummary = Sanitize(ResultSummary);
            ErrorMessage = Sanitize(ErrorMessage);
            Round = Math.Max(1, Round);
            Attempt = Math.Max(1, Attempt);
            MaxAttempts = Math.Max(Attempt, MaxAttempts);
            DurationMs = Math.Max(0, DurationMs);
            QueueDurationMs = Math.Max(0, QueueDurationMs);
            TimeoutMs = Math.Max(0, TimeoutMs);
            if (originalSchemaVersion < 4)
            {
                ConcurrencyMode = Access == CopilotToolAccess.Write || Idempotency != CopilotToolIdempotency.Idempotent
                    ? CopilotToolConcurrencyMode.Exclusive
                    : CopilotToolConcurrencyMode.SharedRead;
                if (string.IsNullOrWhiteSpace(ConcurrencyKey) && !string.IsNullOrWhiteSpace(ToolName))
                    ConcurrencyKey = "legacy:" + ToolName;
            }
            changed |= originalSchemaVersion != SchemaVersion
                || !string.Equals(originalCallId, CallId, StringComparison.Ordinal)
                || !string.Equals(originalRuntimeName, RuntimeName, StringComparison.Ordinal)
                || !string.Equals(originalToolName, ToolName, StringComparison.Ordinal)
                || !string.Equals(originalArgumentSummary, ArgumentSummary, StringComparison.Ordinal)
                || !string.Equals(originalApprovalActionId, ApprovalActionId, StringComparison.Ordinal)
                || !string.Equals(originalConcurrencyKey, ConcurrencyKey, StringComparison.Ordinal)
                || !string.Equals(originalResultSummary, ResultSummary, StringComparison.Ordinal)
                || !string.Equals(originalErrorMessage, ErrorMessage, StringComparison.Ordinal)
                || originalRound != Round
                || originalAttempt != Attempt
                || originalMaxAttempts != MaxAttempts
                || originalDurationMs != DurationMs
                || originalQueueDurationMs != QueueDurationMs
                || originalTimeoutMs != TimeoutMs;

            if (!Enum.IsDefined(State))
            {
                State = CopilotToolExecutionState.Failed;
                changed = true;
            }

            if (!Enum.IsDefined(RiskLevel))
            {
                RiskLevel = CopilotToolRiskLevel.Low;
                changed = true;
            }

            if (!Enum.IsDefined(ApprovalMode))
            {
                ApprovalMode = CopilotToolApprovalMode.Never;
                changed = true;
            }

            if (!Enum.IsDefined(Idempotency))
            {
                Idempotency = CopilotToolIdempotency.Unknown;
                changed = true;
            }

            if (!Enum.IsDefined(ConcurrencyMode))
            {
                ConcurrencyMode = Access == CopilotToolAccess.Write ? CopilotToolConcurrencyMode.Exclusive : CopilotToolConcurrencyMode.SharedRead;
                changed = true;
            }

            if (!Enum.IsDefined(FailureKind))
            {
                FailureKind = CopilotToolFailureKind.Unspecified;
                changed = true;
            }

            if (State is CopilotToolExecutionState.Pending or CopilotToolExecutionState.Running or CopilotToolExecutionState.AwaitingApproval)
            {
                var wasAwaitingApproval = State == CopilotToolExecutionState.AwaitingApproval;
                State = CopilotToolExecutionState.Interrupted;
                CompletedAtUtc = recoveredAtUtc;
                if (StartedAtUtc != default)
                    DurationMs = Math.Max(DurationMs, (long)Math.Max(0, (recoveredAtUtc - StartedAtUtc).TotalMilliseconds));
                ErrorMessage = wasAwaitingApproval
                    ? "Approval was interrupted before a decision was recorded. Submit the request again to create a fresh approval."
                    : "Execution was interrupted before completion.";
                changed = true;
            }

            return changed;
        }

        private static CopilotAgentTraceEntry FromExecution(CopilotToolExecutionInfo execution)
        {
            return new CopilotAgentTraceEntry
            {
                CallId = SanitizeIdentifier(execution.CallId),
                Round = Math.Max(1, execution.Round),
                Attempt = Math.Max(1, execution.Attempt),
                MaxAttempts = Math.Max(Math.Max(1, execution.Attempt), execution.MaxAttempts),
                RuntimeName = SanitizeIdentifier(execution.RuntimeName),
                ToolName = SanitizeIdentifier(execution.ToolName),
                Access = execution.Access,
                RiskLevel = execution.RiskLevel,
                ApprovalMode = execution.ApprovalMode,
                Idempotency = execution.Idempotency,
                ConcurrencyMode = execution.ConcurrencyMode,
                ConcurrencyKey = SanitizeIdentifier(execution.ConcurrencyKey),
                ApprovalActionId = SanitizeIdentifier(execution.ApprovalActionId),
                State = execution.State,
                FailureKind = execution.FailureKind,
                RetryEligible = execution.RetryEligible,
                StartedAtUtc = execution.StartedAtUtc,
                CompletedAtUtc = execution.CompletedAtUtc,
                DurationMs = Math.Max(0, execution.DurationMs),
                QueueDurationMs = Math.Max(0, execution.QueueDurationMs),
                TimeoutMs = Math.Max(0, execution.TimeoutMs),
                ArgumentSummary = Sanitize(execution.ArgumentSummary),
            };
        }

        internal static string Sanitize(string? value)
        {
            var redacted = CopilotMcpAuditLogger.RedactText(value ?? string.Empty).Trim();
            return redacted.Length <= MaxSummaryLength ? redacted : redacted[..MaxSummaryLength] + "...";
        }

        private static string SanitizeIdentifier(string? value)
        {
            var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 120 ? text : text[..120];
        }

        private string BuildActivityLabel()
        {
            var (running, completed) = ToolName switch
            {
                "FetchUrl" => ("正在读取网页", "读取了网页"),
                "WebSearch" => ("正在搜索网页", "搜索了网页"),
                "ReadLocalFile" or "ReadAttachedFile" => ("正在读取文件", "读取了文件"),
                "ListDirectory" or "SearchFiles" or "GrepText" or "SearchDocs" => ("正在搜索文件", "搜索了文件"),
                "GetRecentLog" => ("正在读取日志", "读取了日志"),
                "QueryFlowExecutionStats" or "QueryDatabaseSql" => ("正在查询数据库", "查询了数据库"),
                "ExecuteDatabaseSql" => ("正在执行数据库 SQL", "执行了数据库 SQL"),
                "RunShellCommand" => ("正在运行命令", "运行了命令"),
                "PreviewWorkspacePatch" or "PreviewCreateWorkspaceFile" or "PreviewWorkspaceChangeSet" => ("正在准备修改", "准备了修改"),
                "ApplyWorkspacePatch" or "ApplyCreateWorkspaceFile" or "ApplyWorkspaceChangeSet" => ("正在修改文件", "修改了文件"),
                "RollbackWorkspacePatch" or "RollbackWorkspaceChangeSet" => ("正在回滚修改", "回滚了修改"),
                "CreateFlow" => ("正在创建流程", "创建了流程"),
                "ApplyTemplatePatch" or "TemplatePatch" => ("正在修改模板", "修改了模板"),
                "ExecuteMenu" => ("正在执行应用操作", "执行了应用操作"),
                "SetLanguage" or "SetTheme" => ("正在修改应用设置", "修改了应用设置"),
                _ => ($"正在运行 {ToolName}", $"运行了 {ToolName}"),
            };

            return State switch
            {
                CopilotToolExecutionState.Pending or CopilotToolExecutionState.Running => running,
                CopilotToolExecutionState.AwaitingApproval => completed + " · 等待批准",
                CopilotToolExecutionState.Failed or CopilotToolExecutionState.TimedOut => completed + " · 失败",
                CopilotToolExecutionState.Denied => completed + " · 未批准",
                CopilotToolExecutionState.Cancelled => completed + " · 已取消",
                CopilotToolExecutionState.Interrupted => completed + " · 已中断",
                _ => completed,
            };
        }

        private bool IsFailedSearchAttempt()
        {
            if (!IsFailure)
                return false;

            return string.Equals(ToolName, "SearchFiles", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ToolName, "GrepText", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ToolName, "SearchDocs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ToolName, "WebSearch", StringComparison.OrdinalIgnoreCase);
        }

        private string BuildFriendlyFailureSummary()
        {
            return FailureKind switch
            {
                CopilotToolFailureKind.NotFound => "没有找到可用结果。",
                CopilotToolFailureKind.Validation => "工具输入不符合要求。",
                CopilotToolFailureKind.Authorization => "当前操作没有获得授权。",
                CopilotToolFailureKind.Transient => "暂时无法完成，Agent 可以重试。",
                CopilotToolFailureKind.Cancelled => "操作已取消。",
                _ => !string.IsNullOrWhiteSpace(ResultSummary) ? ResultSummary : ErrorMessage,
            };
        }

        private string BuildFriendlySuccessSummary()
        {
            if (State is CopilotToolExecutionState.Pending or CopilotToolExecutionState.Running)
                return string.Empty;

            return ToolName switch
            {
                "FetchUrl" => "已读取网页正文。",
                "WebSearch" => "已获得网页搜索结果。",
                "ReadLocalFile" or "ReadAttachedFile" => "已读取文件内容。",
                "ListDirectory" or "SearchFiles" or "GrepText" or "SearchDocs" => "已完成文件搜索。",
                "GetRecentLog" => "已读取最近日志。",
                "QueryFlowExecutionStats" or "QueryDatabaseSql" => "已获得数据库查询结果。",
                "ExecuteDatabaseSql" => "数据库 SQL 已执行。",
                "RunShellCommand" => "命令已执行。",
                "PreviewWorkspacePatch" or "PreviewCreateWorkspaceFile" or "PreviewWorkspaceChangeSet" => "文件修改预览已准备。",
                "ApplyWorkspacePatch" or "ApplyCreateWorkspaceFile" or "ApplyWorkspaceChangeSet" => "文件修改已完成。",
                "RollbackWorkspacePatch" or "RollbackWorkspaceChangeSet" => "文件修改已回滚。",
                "CreateFlow" => "流程已创建。",
                "ApplyTemplatePatch" or "TemplatePatch" => "模板修改已完成。",
                "ExecuteMenu" => "应用操作已执行。",
                "SetLanguage" or "SetTheme" => "应用设置已更新。",
                _ => ResultSummary,
            };
        }

        private static string TrimForActivity(string? value, int maxLength)
        {
            var normalized = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            while (normalized.Contains("  ", StringComparison.Ordinal))
                normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
        }

        private static string FormatDuration(long durationMs)
        {
            return durationMs < 1000 ? $"{Math.Max(0, durationMs)}ms" : $"{durationMs / 1000d:0.#}s";
        }

        private static string FormatDiagnosticState(CopilotToolExecutionState state) => state switch
        {
            CopilotToolExecutionState.Pending => "Pending",
            CopilotToolExecutionState.Running => "Running...",
            CopilotToolExecutionState.Completed => "Completed",
            CopilotToolExecutionState.Failed => "Failed",
            CopilotToolExecutionState.TimedOut => "Timed out",
            CopilotToolExecutionState.Denied => "Denied",
            CopilotToolExecutionState.Cancelled => "Cancelled",
            CopilotToolExecutionState.Interrupted => "Interrupted",
            CopilotToolExecutionState.AwaitingApproval => "Awaiting approval",
            _ => "Unknown",
        };
    }
}

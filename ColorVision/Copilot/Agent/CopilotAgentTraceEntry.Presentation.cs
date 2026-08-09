using ColorVision.Common.MVVM;
using ColorVision.Copilot.Mcp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotAgentTraceEntry : ViewModelBase
    {
        internal static string Sanitize(string? value)
        {
            var redacted = CopilotMcpAuditLogger.RedactText(value ?? string.Empty).Trim();
            return redacted.Length <= MaxSummaryLength ? redacted : redacted[..MaxSummaryLength] + "...";
        }

        private static string SanitizeDelegatedAnswer(string? value, out bool wasTruncated)
        {
            var redacted = CopilotMcpAuditLogger.RedactText(value ?? string.Empty).Trim();
            wasTruncated = redacted.Length > MaxDelegatedAnswerLength;
            return wasTruncated
                ? redacted[..MaxDelegatedAnswerLength].TrimEnd() + "\n...<子代理结果预览已截断>"
                : redacted;
        }

        private static string SanitizeIdentifier(string? value)
        {
            var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 120 ? text : text[..120];
        }

        private static long? NormalizeProgressCount(long? value)
        {
            return value.HasValue ? Math.Clamp(value.Value, 0, 1_000_000_000) : null;
        }

        private static string SanitizeProgressUnit(string? value)
        {
            var text = string.Join(" ", SanitizeIdentifier(value)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text.Length <= 24 ? text : text[..24];
        }

        private static string FormatProgressUnit(string unit)
        {
            return unit switch
            {
                "files" => "个文件",
                "items" => "项",
                _ => unit,
            };
        }

        private string BuildActivityLabel()
        {
            var (running, completed) = ToolName switch
            {
                "FetchUrl" => ("正在读取网页", "读取了网页"),
                "WebSearch" => ("正在搜索网页", "搜索了网页"),
                "ReadLocalFile" or "ReadAttachedFile" => ("正在读取文件", "读取了文件"),
                "ListDirectory" or "SearchFiles" or "GrepText" or "SearchDocs" => ("正在搜索文件", "搜索了文件"),
                "DelegateExplore" => ("正在委派代码探索", "委派了代码探索"),
                "DelegateScout" => ("正在查阅外部资料", "查阅了外部资料"),
                _ when ToolName.StartsWith("Delegate", StringComparison.Ordinal) => ("正在委派子任务", "委派了子任务"),
                "GetRecentLog" => ("正在读取日志", "读取了日志"),
                "QueryFlowExecutionStats" or "QueryDatabaseSql" => ("正在查询数据库", "查询了数据库"),
                "ExecuteDatabaseSql" => ("正在执行数据库 SQL", "执行了数据库 SQL"),
                "InspectWindowsSystem" => ("正在检查系统", "检查了系统"),
                "InspectWindowsProcesses" => ("正在检查进程", "检查了进程"),
                "InspectWindowsServices" => ("正在检查服务", "检查了服务"),
                "InspectTcpPort" => ("正在检查端口", "检查了端口"),
                "InspectGitWorkingTree" => ("正在检查工作树", "检查了工作树"),
                "InspectGitDiff" => ("正在读取 Git 差异", "读取了 Git 差异"),
                "RunShellCommand" => ("正在运行命令", "运行了命令"),
                "ReadShellCommandOutput" => ("正在读取命令输出", "读取了命令输出"),
                "StartBackgroundShellCommand" => ("正在启动后台命令", "启动了后台命令"),
                "InspectBackgroundShellCommands" => ("正在检查后台命令", "检查了后台命令"),
                "ReadBackgroundShellCommandOutput" => ("正在读取后台输出", "读取了后台输出"),
                "MonitorBackgroundShellCommandOutput" => ("正在监控后台输出", "监控了后台输出"),
                "StopBackgroundShellCommandOutputMonitor" => ("正在停止后台输出监控", "停止了后台输出监控"),
                "WaitForBackgroundShellCommand" => ("正在等待后台命令", "等待了后台命令"),
                "WaitForBackgroundShellCommands" => ("正在等待多个后台命令", "等待了多个后台命令"),
                "StopBackgroundShellCommand" => ("正在停止后台命令", "停止了后台命令"),
                "ConvertBatchImages" => ("正在转换图像", "转换了图像"),
                "PreviewWorkspacePatchEnvelope" => ("正在准备修改", "准备了修改"),
                "ApplyWorkspacePatchEnvelope" => ("正在修改文件", "修改了文件"),
                "RollbackWorkspacePatchEnvelope" => ("正在回滚修改", "回滚了修改"),
                "CreateFlow" => ("正在创建流程", "创建了流程"),
                "ApplyTemplatePatch" or "TemplatePatch" => ("正在修改模板", "修改了模板"),
                "ExecuteMenu" => ("正在执行应用操作", "执行了应用操作"),
                "SetLanguage" or "SetTheme" => ("正在修改应用设置", "修改了应用设置"),
                _ => ($"正在运行 {ToolName}", $"运行了 {ToolName}"),
            };

            if (State == CopilotToolExecutionState.Completed
                && WorkspaceChangeSetRolledBack
                && string.Equals(ToolName, "ApplyWorkspacePatchEnvelope", StringComparison.Ordinal))
            {
                return completed + " · 已撤销";
            }

            return State switch
            {
                CopilotToolExecutionState.Pending => BuildWaitingActivityLabel(running),
                CopilotToolExecutionState.Running => running,
                CopilotToolExecutionState.AwaitingApproval => completed + " · 等待批准",
                CopilotToolExecutionState.Failed or CopilotToolExecutionState.TimedOut => completed + " · 失败",
                CopilotToolExecutionState.Denied => completed + " · 未批准",
                CopilotToolExecutionState.Cancelled => completed + " · 已取消",
                CopilotToolExecutionState.Interrupted => completed + " · 已中断",
                _ => completed,
            };
        }

        private static string BuildWaitingActivityLabel(string runningLabel)
        {
            const string runningPrefix = "正在";
            return runningLabel.StartsWith(runningPrefix, StringComparison.Ordinal)
                ? "等待" + runningLabel[runningPrefix.Length..]
                : "等待运行";
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
                return ResultSummary;

            return ToolName switch
            {
                "FetchUrl" => "已读取网页正文。",
                "WebSearch" => "已获得网页搜索结果。",
                "ReadLocalFile" or "ReadAttachedFile" => "已读取文件内容。",
                "ListDirectory" or "SearchFiles" or "GrepText" or "SearchDocs" => "已完成文件搜索。",
                "DelegateExplore" => "只读 Explore 子 Agent 已返回结果。",
                "DelegateScout" => "只读 Scout 子 Agent 已返回外部资料。",
                _ when ToolName.StartsWith("Delegate", StringComparison.Ordinal) => ResultSummary,
                "GetRecentLog" => "已读取最近日志。",
                "QueryFlowExecutionStats" or "QueryDatabaseSql" => "已获得数据库查询结果。",
                "ExecuteDatabaseSql" => "数据库 SQL 已执行。",
                "InspectWindowsSystem" => "Windows 系统信息检查完成。",
                "InspectWindowsProcesses" => "Windows 进程检查完成。",
                "InspectWindowsServices" => "Windows 服务检查完成。",
                "InspectTcpPort" => "端口检查完成。",
                "InspectGitWorkingTree" => "Git 工作树检查完成。",
                "InspectGitDiff" => "Git 差异读取完成。",
                "RunShellCommand" => "命令已执行。",
                "ReadShellCommandOutput" => "命令输出读取完成。",
                "StartBackgroundShellCommand" => ResultSummary,
                "InspectBackgroundShellCommands" => "后台命令状态检查完成。",
                "ReadBackgroundShellCommandOutput" => "后台命令输出读取完成。",
                "MonitorBackgroundShellCommandOutput" => ResultSummary,
                "StopBackgroundShellCommandOutputMonitor" => ResultSummary,
                "WaitForBackgroundShellCommand" => ResultSummary,
                "WaitForBackgroundShellCommands" => ResultSummary,
                "StopBackgroundShellCommand" => ResultSummary,
                "PreviewWorkspacePatchEnvelope" => "文件修改预览已准备。",
                "ApplyWorkspacePatchEnvelope" => WorkspaceChangeSetRolledBack
                    ? "这组文件修改已撤销。"
                    : WorkspaceChangedFiles.Count > 0
                        ? $"已完成 {WorkspaceChangedFiles.Count} 个文件的修改，可逐个打开核对。"
                        : "文件修改已完成。",
                "RollbackWorkspacePatchEnvelope" => "文件修改已回滚。",
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

        private static string FormatHookPhase(CopilotToolExecutionHookPhase phase) => phase switch
        {
            CopilotToolExecutionHookPhase.PermissionRequest => "permission",
            CopilotToolExecutionHookPhase.BeforeExecute => "before",
            CopilotToolExecutionHookPhase.AfterExecute => "after",
            _ => "unknown",
        };

        private static string FormatHookState(CopilotToolExecutionHookState state) => state switch
        {
            CopilotToolExecutionHookState.Scheduled => "scheduled",
            CopilotToolExecutionHookState.Completed => "completed",
            CopilotToolExecutionHookState.Denied => "denied",
            CopilotToolExecutionHookState.Failed => "failed",
            CopilotToolExecutionHookState.TimedOut => "timed out",
            CopilotToolExecutionHookState.Cancelled => "cancelled",
            CopilotToolExecutionHookState.Skipped => "skipped",
            _ => "unknown",
        };

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

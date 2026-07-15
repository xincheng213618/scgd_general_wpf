using System;
using System.Collections.Generic;
using System.Linq;
using ColorVision.UI;

namespace ColorVision.Copilot
{
    internal static class CopilotToolIntentPolicy
    {
        private static readonly TimeSpan FollowUpToolLeaseDuration = TimeSpan.FromHours(24);
        private const int MaximumFollowUpCharacters = 300;
        private const int VisibleHistoryEvidenceLimit = 4;

        private static readonly string[] LocalEvidenceMarkers =
        {
            "当前项目", "这个项目", "本项目", "项目里", "工程里", "工作区", "仓库", "代码", "源码",
            "文件", "类", "方法", "函数", "实现位置", "在哪里实现", "在哪实现", "查找实现", "搜索代码",
            "修改", "改一下", "修复", "排查", "重构", "新增", "添加", "删除", "报错", "异常",
            "current project", "this project", "workspace", "repository", "repo", "source code", "codebase",
            "find in files", "search the code", "where is", "fix", "debug", "refactor", "modify",
        };

        private static readonly string[] WorkspaceEditMarkers =
        {
            "请修改", "帮我修改", "修改这个", "修改代码", "修改文件", "帮我改", "改一下", "修复这个", "修复代码", "重构", "替换代码", "编辑文件", "更新代码", "应用补丁", "写入文件",
            "删除文件", "删除旧文件", "删除这个文件", "移除文件", "移除旧文件", "移除这个文件",
            "please modify", "please edit", "edit the file", "fix this", "fix the code", "refactor", "replace the code", "update the file", "apply the patch", "delete file", "delete the file", "remove file", "remove the file",
        };

        private static readonly string[] WorkspaceEditOptOutMarkers =
        {
            "不要修改", "不用修改", "无需修改", "只说明", "只解释", "只分析", "不要写文件",
            "do not modify", "don't modify", "do not edit", "don't edit", "explain only", "analysis only", "read only",
        };

        private static readonly string[] WorkspaceChangeSetMarkers =
        {
            "多文件", "多个文件", "两个文件", "三个文件", "一组文件", "所有这些文件", "批量修改文件", "批量创建文件",
            "multiple files", "two files", "three files", "several files", "a set of files", "all these files", "batch edit files",
        };

        private static readonly string[] WorkspaceRollbackMarkers =
        {
            "撤销修改", "撤销刚才", "回滚修改", "回滚刚才", "回滚补丁", "还原文件", "恢复原文件",
            "undo the change", "undo that change", "rollback the change", "roll back the change", "revert the file",
        };

        private static readonly string[] WorkspaceCreateMarkers =
        {
            "新建文件", "创建文件", "新增文件", "添加文件", "新建类", "创建类", "新增类", "添加类",
            "新建多个文件", "创建多个文件", "新增多个文件", "添加多个文件", "新建两个文件", "创建两个文件",
            "create a file", "create the file", "add a file", "add the file", "create a class", "add a class", "new source file",
        };

        private static readonly string[] WorkspaceValidationMarkers =
        {
            "编译项目", "编译一下", "构建项目", "构建一下", "运行测试", "跑测试", "执行测试", "测试一下", "验证修改", "验证一下", "检查构建",
            "build the project", "run the build", "run tests", "run the tests", "test the project", "verify the changes", "validate the changes",
        };

        private static readonly string[] WorkspaceValidationExplanationMarkers =
        {
            "怎么构建", "如何构建", "构建原理", "怎么测试", "如何测试", "测试原理",
            "how to build", "how do i build", "how to test", "how do i test", "explain the build", "explain the test",
        };

        private static readonly string[] PublicWebMarkers =
        {
            "搜索网页", "网上搜索", "联网搜索", "查网页", "查官网", "官网", "公开资料", "公开信息",
            "最新消息", "最新版本", "近期新闻", "当前价格", "实时", "网页资料",
            "search the web", "web search", "search online", "look online", "official website",
            "latest news", "current price", "public information",
        };

        private static readonly string[] ExplicitPublicWebSearchMarkers =
        {
            "搜索网页", "网上搜索", "联网搜索", "查网页", "查官网", "搜索一下", "查询一下公开信息",
            "search the web", "web search", "search online", "look online", "search the public web",
        };

        private static readonly string[] PublicWebOptOutMarkers =
        {
            "不要联网", "不用联网", "无需联网", "不访问网页", "不要访问网页", "不要搜索", "不用搜索", "无需搜索",
            "do not browse", "don't browse", "without browsing", "do not search the web", "don't search the web",
            "no web search", "offline only",
        };

        private static readonly string[] ExternalLocalSearchMarkers =
        {
            "search_files", "searchfiles", "find_files", "findfiles", "grep_text", "greptext",
            "search_code", "codesearch", "search the workspace", "search local files", "search source code",
        };

        private static readonly string[] ExternalWebSearchMarkers =
        {
            "web_search", "websearch", "search_web", "internet_search", "search_online",
            "search the web", "search public web", "search the public web",
        };

        private static readonly string[] ExternalUrlFetchMarkers =
        {
            "fetch_url", "fetchurl", "read_url", "readurl", "get_url", "geturl",
            "fetch a url", "fetch web page", "read web page",
        };

        private static readonly string[] NewTopicMarkers =
        {
            "换个话题", "另一个问题", "另外一个问题", "另外，", "另外,", "顺便问", "不相关",
            "new topic", "another question", "unrelated", "by the way",
        };

        private static readonly string[] FollowUpMarkers =
        {
            "继续", "再看", "再查", "再检查", "再试", "现在呢", "然后呢", "还有呢", "刚才的", "上一个",
            "continue", "again", "check again", "what about", "then", "the previous", "that result",
        };

        private static readonly string[] FlowGraphMarkers =
        {
            "流程图", "工作流程", "流程节点", "流程里", "流程中", "节点连线", "相机节点", "算法节点", ".stn",
            "flow graph", "flow editor", "workflow", "flow node", "camera node", "algorithm node",
        };

        private static readonly string[] FlowMutationMarkers =
        {
            "添加", "新增", "创建", "插入", "连接", "修改", "设置", "移动",
            "add node", "create node", "insert node", "connect node", "set node", "update node", "move node",
        };

        private static readonly string[] MutationExplanationMarkers =
        {
            "如何", "怎么", "怎样", "是什么", "为什么", "介绍", "解释", "原理", "教程",
            "how to", "how do", "what is", "why", "explain", "tutorial",
        };

        private static readonly string[] FlowStatisticsMarkers =
        {
            "流程统计", "流程执行数", "流程执行次数", "执行了多少次流程", "多少次流程", "流程运行数", "流程完成率", "流程成功率", "流程平均耗时", "今天流程", "昨天流程",
            "flow statistics", "flow count", "flow completion rate", "flow success rate", "flow average duration",
        };

        private static readonly string[] DatabaseMarkers =
        {
            "数据库", "数据库表", "数据表", "SQL", "MySQL", "查询数据", "数据量", "记录数", "行数",
            "database", "database table", "table schema", "query data", "row count",
        };

        private static readonly string[] DatabaseMutationMarkers =
        {
            "修改数据库", "更新数据库", "写入数据库", "插入数据", "删除数据", "清理数据库", "创建数据库表", "修改数据库表", "删除数据库表",
            "insert into", "update database", "update table", "delete from", "create table", "alter table", "drop table", "truncate table", "rename table",
        };

        private static readonly string[] DatabaseExplanationMarkers =
        {
            "数据库是什么", "数据库原理", "SQL是什么", "SQL 是什么", "解释SQL", "解释 SQL",
            "what is a database", "what is database", "what is sql", "explain sql",
        };

        private static readonly string[] RecentLogMarkers =
        {
            "日志", "最近错误", "最近异常", "报错", "错误日志", "异常日志", "崩溃", "失败原因",
            "application log", "recent log", "error log", "exception log", "crash log",
        };

        private static readonly string[] WindowsSystemMarkers =
        {
            "Windows版本", "Windows 版本", "操作系统", "系统版本", "系统的版本", "系统信息", "系统架构", ".NET版本", ".NET 版本",
            "windows version", "operating system", "os version", "system information", "build number", ".net runtime",
        };

        private static readonly string[] WindowsProcessMarkers =
        {
            "进程", "进程号", "PID", "CPU占用", "CPU 占用", "内存占用", "程序很卡", "应用很卡", "卡顿",
            "process", "process id", "cpu usage", "memory usage", "working set",
        };

        private static readonly string[] WindowsServiceMarkers =
        {
            "Windows服务", "Windows 服务", "系统服务", "服务列表", "服务状态", "服务是否运行", "服务是否在运行", "服务现在运行", "服务运行吗",
            "windows service", "service name", "service status", "list services",
        };

        private static readonly string[] TcpPortMarkers =
        {
            "端口", "TCP", "监听地址", "端口占用", "port", "tcp listener", "listening port",
        };

        private static readonly string[] ShellMarkers =
        {
            "运行PowerShell", "运行 PowerShell", "执行PowerShell", "执行 PowerShell", "用PowerShell", "用 PowerShell", "使用PowerShell", "使用 PowerShell",
            "运行pwsh", "运行 pwsh", "执行pwsh", "执行 pwsh", "用pwsh", "用 pwsh",
            "运行CMD", "运行 CMD", "执行CMD", "执行 CMD", "用CMD", "用 CMD", "使用CMD", "使用 CMD",
            "在命令行运行", "在命令行执行", "在终端运行", "在终端执行", "运行命令", "执行命令", "运行脚本", "执行脚本",
            "run powershell", "use powershell", "execute powershell", "run pwsh", "use pwsh", "run cmd", "use cmd", "execute cmd",
            "shell command", "terminal command", "run command", "execute command", "run script", "execute script",
        };

        private static readonly string[] FollowUpWebToolNames =
        {
            "FetchUrl", "WebSearch", "DelegateScout",
        };

        public static bool NeedsLocalEvidence(CopilotAgentRequest? request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat)
                return false;

            if (request.Mode is CopilotAgentMode.Diagnose or CopilotAgentMode.Review
                || request.ReadableLocalFilePaths.Count > 0
                || request.ReadableLocalDirectoryPaths.Count > 0)
            {
                return true;
            }

            return ContainsAny(request.UserText, LocalEvidenceMarkers);
        }

        public static bool NeedsWorkspaceEdit(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || request.Mode == CopilotAgentMode.Review
                || request.WritableLocalRootPaths.Count == 0 && request.WritableLocalFilePaths.Count == 0
                || ContainsAny(request.UserText, WorkspaceEditOptOutMarkers))
            {
                return false;
            }

            return ContainsAny(request.UserText, WorkspaceEditMarkers);
        }

        public static bool NeedsWorkspaceRollback(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || request.Mode == CopilotAgentMode.Review
                || request.WritableLocalRootPaths.Count == 0 && request.WritableLocalFilePaths.Count == 0)
            {
                return false;
            }

            return ContainsAny(request.UserText, WorkspaceRollbackMarkers);
        }

        public static bool NeedsWorkspaceChangeSet(CopilotAgentRequest? request)
        {
            return request != null
                && (NeedsWorkspaceEdit(request) || NeedsWorkspaceCreate(request) || NeedsWorkspaceRollback(request))
                && ContainsAny(request.UserText, WorkspaceChangeSetMarkers);
        }

        public static bool NeedsWorkspaceCreate(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || request.Mode == CopilotAgentMode.Review
                || request.WritableLocalRootPaths.Count == 0
                || ContainsAny(request.UserText, WorkspaceEditOptOutMarkers))
            {
                return false;
            }

            return ContainsAny(request.UserText, WorkspaceCreateMarkers);
        }

        public static bool NeedsWorkspaceValidation(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || request.Mode == CopilotAgentMode.Review
                || request.WritableLocalRootPaths.Count == 0
                || ContainsAny(request.UserText, WorkspaceValidationExplanationMarkers))
            {
                return false;
            }

            return ContainsAny(request.UserText, WorkspaceValidationMarkers);
        }

        public static bool NeedsPublicWebSearch(CopilotAgentRequest? request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat || ExplicitlyDisallowsPublicWebAccess(request))
                return false;

            return request.Mode == CopilotAgentMode.Web
                || CopilotWebPageToolSupport.ExtractHttpUrls(request.UserText).Count > 0
                || ContainsAny(request.UserText, PublicWebMarkers);
        }

        public static bool NeedsUrlFetch(CopilotAgentRequest? request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat || ExplicitlyDisallowsPublicWebAccess(request))
                return false;

            return request.Mode == CopilotAgentMode.Web
                || CopilotWebPageToolSupport.ExtractHttpUrls(request.UserText).Count > 0;
        }

        public static bool NeedsFlowGraph(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (HasFlowContext(request!)
                    || MatchesCurrentOrContinuation(request!, FlowGraphMarkers,
                        "InspectFlowGraph", "SearchFlowNodeCatalog", "PreviewFlowPatch", "ApplyFlowPatch"));
        }

        public static bool NeedsFlowMutation(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && ContainsAny(request!.UserText, FlowMutationMarkers)
                && !ContainsAny(request.UserText, MutationExplanationMarkers)
                && (HasFlowContext(request) || ContainsAny(request.UserText, FlowGraphMarkers));
        }

        public static bool NeedsFlowExecutionStatistics(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && MatchesCurrentOrContinuation(request!, FlowStatisticsMarkers, "QueryFlowExecutionStats");
        }

        public static bool NeedsDatabaseRead(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && !ContainsAny(request!.UserText, DatabaseExplanationMarkers)
                && MatchesCurrentOrContinuation(request, DatabaseMarkers, "QueryDatabaseSql", "ExecuteDatabaseSql");
        }

        public static bool NeedsDatabaseWrite(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && ContainsAny(request!.UserText, DatabaseMarkers)
                && ContainsAny(request.UserText, DatabaseMutationMarkers)
                && !ContainsAny(request.UserText, MutationExplanationMarkers);
        }

        public static bool NeedsRecentLogs(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (request!.Mode == CopilotAgentMode.Diagnose
                    || MatchesCurrentOrContinuation(request, RecentLogMarkers, "GetRecentLog"));
        }

        public static bool NeedsWindowsSystemInspection(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (request!.Mode == CopilotAgentMode.Diagnose
                    || MatchesCurrentOrContinuation(request, WindowsSystemMarkers, "InspectWindowsSystem"));
        }

        public static bool NeedsWindowsProcessInspection(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (request!.Mode == CopilotAgentMode.Diagnose
                    || MatchesCurrentOrContinuation(request, WindowsProcessMarkers, "InspectWindowsProcesses"));
        }

        public static bool NeedsWindowsServiceInspection(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (request!.Mode == CopilotAgentMode.Diagnose
                    || MatchesCurrentOrContinuation(request, WindowsServiceMarkers, "InspectWindowsServices"));
        }

        public static bool NeedsTcpPortInspection(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (request!.Mode == CopilotAgentMode.Diagnose
                    || MatchesCurrentOrContinuation(request, TcpPortMarkers, "InspectTcpPort"));
        }

        public static bool NeedsShellExecution(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request) && ContainsAny(request!.UserText, ShellMarkers);
        }

        internal static bool ExplicitlyRequiresPublicWebSearch(CopilotAgentRequest? request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat || ExplicitlyDisallowsPublicWebAccess(request))
                return false;

            return request.Mode == CopilotAgentMode.Web
                || ContainsAny(request.UserText, ExplicitPublicWebSearchMarkers);
        }

        internal static bool ExplicitlyDisallowsPublicWebAccess(CopilotAgentRequest? request)
        {
            return request != null && ContainsAny(request.UserText, PublicWebOptOutMarkers);
        }

        internal static bool IsUrlFetchTool(ICopilotTool? tool)
        {
            if (tool == null)
                return false;

            if (string.Equals(tool.Name, "FetchUrl", StringComparison.OrdinalIgnoreCase))
                return true;

            return ContainsAny($"{tool.Name} {tool.Description}", ExternalUrlFetchMarkers);
        }

        internal static bool IsPublicWebSearchTool(ICopilotTool? tool)
        {
            if (tool == null)
                return false;

            if (string.Equals(tool.Name, "WebSearch", StringComparison.OrdinalIgnoreCase))
                return true;

            return ContainsAny($"{tool.Name} {tool.Description}", ExternalWebSearchMarkers);
        }

        internal static bool IsWorkspaceApplyTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "ApplyWorkspacePatchEnvelope", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tool?.Name, "ApplyWorkspacePatch", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tool?.Name, "ApplyWorkspaceChangeSet", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsWorkspaceChangeSetApplyTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "ApplyWorkspacePatchEnvelope", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tool?.Name, "ApplyWorkspaceChangeSet", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsWorkspaceRollbackTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "RollbackWorkspacePatchEnvelope", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tool?.Name, "RollbackWorkspacePatch", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tool?.Name, "RollbackWorkspaceChangeSet", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsWorkspaceChangeSetRollbackTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "RollbackWorkspacePatchEnvelope", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tool?.Name, "RollbackWorkspaceChangeSet", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsWorkspaceCreateApplyTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "ApplyWorkspacePatchEnvelope", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tool?.Name, "ApplyCreateWorkspaceFile", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tool?.Name, "ApplyWorkspaceChangeSet", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsWorkspaceValidationTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "RunWorkspaceValidation", StringComparison.OrdinalIgnoreCase);
        }

        public static bool CanExposeExternalTool(CopilotAgentRequest? request, string? toolName, string? description)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat)
                return false;

            var identity = $"{toolName} {description}";
            if (ContainsAny(identity, ExternalWebSearchMarkers))
                return NeedsPublicWebSearch(request);
            if (ContainsAny(identity, ExternalUrlFetchMarkers))
                return NeedsUrlFetch(request);
            if (ContainsAny(identity, ExternalLocalSearchMarkers))
                return NeedsLocalEvidence(request);

            return true;
        }

        public static bool CanRetainForFollowUp(CopilotAgentRequest? request, ICopilotTool? tool)
        {
            if (request == null || tool == null || request.Mode != CopilotAgentMode.Auto)
                return false;
            if (request.History.Count == 0
                || string.IsNullOrWhiteSpace(request.UserText)
                || request.UserText.Length > MaximumFollowUpCharacters)
                return false;

            if (ContainsAny(request.UserText, NewTopicMarkers)
                || ContainsAny(request.UserText, LocalEvidenceMarkers))
                return false;
            var capability = tool.Capability;
            if (capability.Access != CopilotToolAccess.ReadOnly
                || capability.Idempotency != CopilotToolIdempotency.Idempotent
                || capability.ApprovalMode != CopilotToolApprovalMode.Never)
                return false;

            if (HasRecentCheckpointToolEvidence(request.SessionCheckpoint, tool.Name))
                return true;

            return IsWebEvidenceTool(tool)
                && (HasRecentCheckpointWebEvidence(request.SessionCheckpoint)
                    || HasVisibleWebEvidence(request.History));
        }

        private static bool HasRecentCheckpointToolEvidence(CopilotAgentSessionCheckpoint? checkpoint, string toolName)
        {
            if (checkpoint?.IsStructurallyValid() != true
                || string.IsNullOrWhiteSpace(toolName)
                || DateTimeOffset.UtcNow - checkpoint.UpdatedAtUtc > FollowUpToolLeaseDuration)
            {
                return false;
            }

            var previousStop = checkpoint.TaskEventJournal.Events
                .LastOrDefault(item => item.Type == CopilotAgentTaskEventType.RunStopped);
            if (previousStop == null)
                return false;

            return checkpoint.TaskEventJournal.Events.Any(item =>
                string.Equals(item.RunId, previousStop.RunId, StringComparison.Ordinal)
                && item.Type == CopilotAgentTaskEventType.ToolCompleted
                && string.Equals(item.ToolName, toolName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool MatchesCurrentOrContinuation(CopilotAgentRequest request, string[] markers, params string[] toolNames)
        {
            if (ContainsAny(request.UserText, markers))
                return true;
            if (!IsExplicitContinuation(request))
                return false;
            if ((request.History ?? Array.Empty<CopilotRequestMessage>())
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .TakeLast(VisibleHistoryEvidenceLimit)
                .Any(message => ContainsAny(message.Content, markers)))
            {
                return true;
            }
            return toolNames.Any(toolName => HasRecentCheckpointToolEvidence(request.SessionCheckpoint, toolName));
        }

        private static bool IsExplicitContinuation(CopilotAgentRequest request)
        {
            return request.History.Count > 0
                && request.UserText.Length <= MaximumFollowUpCharacters
                && ContainsAny(request.UserText, FollowUpMarkers)
                && !ContainsAny(request.UserText, NewTopicMarkers);
        }

        private static bool HasFlowContext(CopilotAgentRequest request)
        {
            return request.ContextItems.Any(item =>
                (item.Id ?? string.Empty).EndsWith(":flow", StringComparison.OrdinalIgnoreCase)
                || (item.Title ?? string.Empty).StartsWith("Flow context", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsAgentRequest(CopilotAgentRequest? request)
        {
            return request != null && request.Mode != CopilotAgentMode.Chat;
        }

        private static bool HasRecentCheckpointWebEvidence(CopilotAgentSessionCheckpoint? checkpoint)
        {
            if (checkpoint?.IsStructurallyValid() != true
                || DateTimeOffset.UtcNow - checkpoint.UpdatedAtUtc > FollowUpToolLeaseDuration)
                return false;

            var previousStop = checkpoint.TaskEventJournal.Events
                .LastOrDefault(item => item.Type == CopilotAgentTaskEventType.RunStopped);
            if (previousStop == null)
                return false;

            return checkpoint.TaskEventJournal.Events.Any(item =>
                string.Equals(item.RunId, previousStop.RunId, StringComparison.Ordinal)
                && item.Type is CopilotAgentTaskEventType.ToolStarted or CopilotAgentTaskEventType.ToolCompleted
                && IsFollowUpWebToolIdentity(item.ToolName, string.Empty));
        }

        private static bool HasVisibleWebEvidence(IReadOnlyList<CopilotRequestMessage> history)
        {
            return (history ?? Array.Empty<CopilotRequestMessage>())
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .TakeLast(VisibleHistoryEvidenceLimit)
                .Any(message => CopilotWebPageToolSupport.ExtractHttpUrls(message.Content).Count > 0
                    || ContainsAny(message.Content, PublicWebMarkers));
        }

        internal static bool IsWebEvidenceTool(ICopilotTool? tool)
        {
            if (tool == null)
                return false;

            return (tool is CopilotDelegateSubagentTool delegatedTool
                    && delegatedTool.Role.ContextScope == CopilotSubagentContextScope.PublicWeb)
                || string.Equals(tool.Name, "DelegateScout", StringComparison.OrdinalIgnoreCase)
                || IsUrlFetchTool(tool)
                || IsPublicWebSearchTool(tool);
        }

        private static bool IsFollowUpWebToolIdentity(string? name, string? description)
        {
            if (FollowUpWebToolNames.Contains(name ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                return true;
            if (CopilotSubagentRoleCatalog.Default.TryGetByToolName(name ?? string.Empty, out var role)
                && role?.ContextScope == CopilotSubagentContextScope.PublicWeb)
                return true;

            var identity = $"{name} {description}";
            return ContainsAny(identity, ExternalWebSearchMarkers)
                || ContainsAny(identity, ExternalUrlFetchMarkers);
        }

        private static bool ContainsAny(string? text, string[] markers)
        {
            var value = text ?? string.Empty;
            return markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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

        private static readonly string[] FlowExecutionStatisticsMarkers =
        {
            "今天执行了多少次流程", "今天跑了多少次流程", "流程执行次数", "流程运行次数", "流程统计", "查询流程记录", "查看流程记录",
            "成功了多少次", "失败了多少次", "流程成功率", "流程完成率", "昨天执行了多少次流程", "最近七天流程",
            "flow execution count", "flow run count", "flow statistics", "flow success rate", "flows ran today", "flows run today",
        };

        private static readonly string[] FlowExecutionStatisticsExplanationMarkers =
        {
            "怎么统计流程", "如何统计流程", "流程统计原理", "怎么查询流程", "如何查询流程",
            "how to count flow", "how are flow statistics", "explain flow statistics",
        };

        private static readonly string[] DatabaseSqlQueryMarkers =
        {
            "查询sql", "查询 sql", "执行查询", "查询数据库", "查数据库", "运行查询", "数据库查询", "查看表结构", "查看数据库表",
            "query sql", "run query", "query database", "database query", "show tables", "describe table",
        };

        private static readonly string[] DatabaseInstanceMarkers =
        {
            "数据库里", "数据库中", "数据库现在", "当前数据库", "这个数据库", "业务数据库", "库里", "库中", "数据表里", "数据表中",
            "in the database", "current database", "database rows", "table rows",
        };

        private static readonly string[] DatabaseObservationMarkers =
        {
            "多少", "数量", "数据量", "记录数", "行数", "占用", "大小", "有哪些", "有啥", "统计",
            "how many", "count", "record count", "row count", "size", "statistics",
        };

        private static readonly string[] DatabaseSqlMutationMarkers =
        {
            "执行sql", "执行 sql", "运行sql", "运行 sql", "清理sql", "清理 sql", "清理数据库", "删除数据库记录", "删除表数据", "清空表",
            "更新数据库", "修改数据库", "创建表", "删除表", "execute sql", "run sql", "clean database", "delete database records", "truncate table", "drop table",
        };

        private static readonly string[] DatabaseSqlExplanationMarkers =
        {
            "sql是什么", "sql 是什么", "如何写sql", "如何写 sql", "怎么写sql", "怎么写 sql", "解释sql", "解释 sql", "sql原理", "sql 原理",
            "what is sql", "how to write sql", "explain sql",
        };

        private static readonly string[] ShellExecutionMarkers =
        {
            "执行命令", "运行命令", "执行cmd", "执行 cmd", "运行cmd", "运行 cmd", "执行powershell", "执行 powershell",
            "运行powershell", "运行 powershell", "命令行执行", "终端执行", "检查端口", "查询端口", "查看端口", "端口占用",
            "端口有没有被占用", "端口是否被占用", "端口是否占用", "端口被占用", "查看进程", "检查进程", "查看服务状态", "检查服务状态", "查询本机ip", "查看本机ip",
            "检查系统版本", "查看系统版本", "查询系统版本", "检查windows版本", "查看windows版本", "查询windows版本", "操作系统版本", "本机系统版本", "当前系统版本",
            "检查当前系统", "查看当前系统", "查询当前系统", "检查本机系统", "查看本机系统", "查询本机系统", "检查本机的windows", "查看本机的windows",
            "检查系统信息", "查看系统信息", "查询系统信息", "检查电脑配置", "查看电脑配置", "查询电脑配置", "查看本机 windows", "检查本机 windows",
            "run command", "execute command", "run cmd", "run powershell", "check port", "port usage", "port in use",
            "list processes", "check process", "check service", "machine diagnostics", "check windows version", "windows version",
            "operating system version", "system information", "computer configuration",
        };

        private static readonly string[] RecentLogInspectionMarkers =
        {
            "读取日志", "查看日志", "查询日志", "检查日志", "最近日志", "应用日志", "运行日志", "错误日志", "异常日志",
            "排查报错", "排查异常", "分析报错", "分析异常", "崩溃原因", "失败原因",
            "read logs", "view logs", "check logs", "recent logs", "application logs", "error log", "exception log",
            "diagnose error", "diagnose exception", "crash reason", "failure reason",
        };

        private static readonly string[] RecentLogExplanationMarkers =
        {
            "日志是什么", "日志原理", "如何记录日志", "怎么记录日志", "如何写日志", "怎么写日志",
            "what is a log", "logging basics", "how to log", "how to write logs",
        };

        private static readonly string[] ShellExplanationMarkers =
        {
            "cmd是什么", "cmd 是什么", "powershell是什么", "powershell 是什么", "怎么写cmd", "怎么写 cmd", "如何写cmd", "如何写 cmd",
            "怎么写powershell", "怎么写 powershell", "如何写powershell", "如何写 powershell", "命令怎么写", "命令示例",
            "怎么检查端口", "如何检查端口", "检查端口的命令", "端口检查命令",
            "what is cmd", "what is powershell", "how to write a command", "command example",
        };

        private static readonly string[] TcpPortInspectionMarkers =
        {
            "检查", "查询", "查看", "检测", "占用", "被占", "监听", "哪个进程", "什么进程", "谁在用", "谁占用",
            "check", "inspect", "query", "in use", "occupied", "listening", "which process", "what process",
        };

        private static readonly string[] TcpPortExplanationMarkers =
        {
            "怎么检查", "如何检查", "怎样检查", "怎么查询", "如何查询", "怎样查询", "怎么查看", "如何查看", "检查命令", "查询命令",
            "how to check", "how do i check", "check command", "which command",
        };

        private static readonly string[] TcpPortExplicitShellMarkers =
        {
            "执行cmd", "执行 cmd", "运行cmd", "运行 cmd", "用cmd", "用 cmd", "使用cmd", "使用 cmd", "cmd命令", "cmd 命令",
            "执行powershell", "执行 powershell", "运行powershell", "运行 powershell", "用powershell", "用 powershell", "使用powershell", "使用 powershell",
            "run cmd", "using cmd", "run powershell", "using powershell",
        };

        private static readonly string[] DiagnosticRefreshFollowUpMarkers =
        {
            "现在呢", "现在怎么样", "现在是什么状态", "再检查", "重新检查", "再查", "重新查", "再检测", "重新检测", "刷新一下", "再看一下", "再试一次",
            "what about now", "check again", "inspect again", "query again", "refresh", "try again",
        };

        private static readonly Regex TcpPortPattern = new(
            @"(?:(?<before>\d{1,5})\s*端口|端口\s*[:#]?\s*(?<chineseAfter>\d{1,5})|(?:tcp\s*)?port\s*[:#]?\s*(?<after>\d{1,5})|(?<tail>\d{1,5})\s*(?:tcp\s*)?port\b)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex TcpPortListPattern = new(
            @"(?:(?:端口|ports?)\s*[:#]?\s*\d{1,5}|\d{1,5})\s*(?:和|与|、|,|，|/|\band\b)\s*\d{1,5}(?:\s*(?:端口|ports?))?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

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

        private static readonly string[] FollowUpWebToolNames =
        {
            "FetchUrl", "WebSearch",
        };

        public static bool NeedsLocalEvidence(CopilotAgentRequest? request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat)
                return false;

            if (request.Mode == CopilotAgentMode.Diagnose
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
                || request.WritableLocalRootPaths.Count == 0
                || ContainsAny(request.UserText, WorkspaceValidationExplanationMarkers))
            {
                return false;
            }

            return ContainsAny(request.UserText, WorkspaceValidationMarkers);
        }

        public static bool NeedsFlowExecutionStatistics(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || ContainsAny(request.UserText, FlowExecutionStatisticsExplanationMarkers))
            {
                return false;
            }

            return ContainsAny(request.UserText, FlowExecutionStatisticsMarkers);
        }

        public static bool NeedsDatabaseSqlQuery(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || ContainsAny(request.UserText, DatabaseSqlExplanationMarkers))
            {
                return false;
            }

            var text = request.UserText.TrimStart();
            if (TryAnalyzeEmbeddedSql(text, out var analysis))
                return analysis!.Kind == CopilotDatabaseSqlStatementKind.Query;
            return ContainsAny(request.UserText, DatabaseSqlQueryMarkers)
                || ContainsAny(request.UserText, DatabaseInstanceMarkers) && ContainsAny(request.UserText, DatabaseObservationMarkers)
                || StartsWithAny(text, "select ", "show ", "describe ", "desc ", "explain ", "with ", "table ");
        }

        public static bool NeedsRecentLogInspection(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || ContainsAny(request.UserText, RecentLogExplanationMarkers))
            {
                return false;
            }

            return request.Mode == CopilotAgentMode.Diagnose
                || ContainsAny(request.UserText, RecentLogInspectionMarkers);
        }

        public static bool NeedsDatabaseSqlMutation(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || ContainsAny(request.UserText, DatabaseSqlExplanationMarkers))
            {
                return false;
            }

            var text = request.UserText.TrimStart();
            if (TryAnalyzeEmbeddedSql(text, out var analysis))
                return analysis!.Kind != CopilotDatabaseSqlStatementKind.Query;
            return ContainsAny(request.UserText, DatabaseSqlMutationMarkers)
                || StartsWithAny(text, "insert ", "update ", "delete ", "replace ", "truncate ", "create ", "alter ", "drop ", "rename ");
        }

        public static bool NeedsShellCommand(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || ContainsAny(request.UserText, ShellExplanationMarkers)
                || NeedsTcpPortInspection(request))
            {
                return false;
            }

            return ContainsAny(request.UserText, ShellExecutionMarkers)
                || ContainsAny(request.UserText, TcpPortExplicitShellMarkers);
        }

        public static bool NeedsTcpPortInspection(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || ContainsAny(request.UserText, TcpPortExplanationMarkers)
                || ContainsAny(request.UserText, TcpPortExplicitShellMarkers))
            {
                return false;
            }

            if (ContainsAny(request.UserText, TcpPortInspectionMarkers)
                && TryExtractTcpPort(request.UserText, out _))
            {
                return true;
            }

            return IsDiagnosticRefreshFollowUp(request.UserText)
                && TryExtractTcpPortFromRecentConversation(request, out _);
        }

        internal static bool TryExtractTcpPort(string? text, out int port)
        {
            port = 0;
            if (string.IsNullOrWhiteSpace(text) || TcpPortListPattern.IsMatch(text))
                return false;

            var ports = new HashSet<int>();
            foreach (Match match in TcpPortPattern.Matches(text))
            {
                var valueGroup = match.Groups["before"];
                if (!valueGroup.Success)
                    valueGroup = match.Groups["chineseAfter"];
                if (!valueGroup.Success)
                    valueGroup = match.Groups["after"];
                if (!valueGroup.Success)
                    valueGroup = match.Groups["tail"];
                var value = valueGroup.Value;
                if (int.TryParse(value, out var candidate) && candidate is >= 1 and <= 65535)
                    ports.Add(candidate);
            }

            if (ports.Count != 1)
                return false;
            port = ports.Single();
            return true;
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

        internal static bool IsFlowExecutionStatisticsTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "QueryFlowExecutionStats", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsDatabaseSqlQueryTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "QueryDatabaseSql", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsDatabaseSqlMutationTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "ExecuteDatabaseSql", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsShellCommandTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "RunShellCommand", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsTcpPortInspectionTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "InspectTcpPort", StringComparison.OrdinalIgnoreCase);
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

        private static bool IsDiagnosticRefreshFollowUp(string? text)
        {
            return !string.IsNullOrWhiteSpace(text)
                && text.Length <= MaximumFollowUpCharacters
                && ContainsAny(text, DiagnosticRefreshFollowUpMarkers);
        }

        private static bool TryExtractTcpPortFromRecentConversation(CopilotAgentRequest request, out int port)
        {
            foreach (var message in (request.History ?? Array.Empty<CopilotRequestMessage>()).TakeLast(VisibleHistoryEvidenceLimit).Reverse())
            {
                if (TryExtractTcpPort(message.Content, out port))
                    return true;
            }

            foreach (var message in (request.SessionCheckpoint?.ConversationMemory ?? Array.Empty<CopilotRequestMessage>()).TakeLast(VisibleHistoryEvidenceLimit).Reverse())
            {
                if (TryExtractTcpPort(message.Content, out port))
                    return true;
            }

            port = 0;
            return false;
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

            return IsUrlFetchTool(tool) || IsPublicWebSearchTool(tool);
        }

        private static bool IsFollowUpWebToolIdentity(string? name, string? description)
        {
            if (FollowUpWebToolNames.Contains(name ?? string.Empty, StringComparer.OrdinalIgnoreCase))
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

        private static bool StartsWithAny(string text, params string[] prefixes)
        {
            return prefixes.Any(prefix => text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryAnalyzeEmbeddedSql(string text, out CopilotDatabaseSqlAnalysis? analysis)
        {
            analysis = null;
            var keywords = new[]
            {
                "WITH", "SELECT", "SHOW", "DESCRIBE", "DESC", "EXPLAIN", "TABLE", "INSERT", "UPDATE",
                "DELETE", "REPLACE", "TRUNCATE", "CREATE", "ALTER", "DROP", "RENAME",
            };
            var start = -1;
            foreach (var keyword in keywords)
            {
                var searchStart = 0;
                while (searchStart < text.Length)
                {
                    var index = text.IndexOf(keyword, searchStart, StringComparison.OrdinalIgnoreCase);
                    if (index < 0)
                        break;
                    var beforeIsIdentifier = index > 0 && (char.IsLetterOrDigit(text[index - 1]) || text[index - 1] is '_' or '$');
                    var end = index + keyword.Length;
                    var afterIsIdentifier = end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] is '_' or '$');
                    if (!beforeIsIdentifier && !afterIsIdentifier)
                    {
                        start = start < 0 ? index : Math.Min(start, index);
                        break;
                    }
                    searchStart = index + keyword.Length;
                }
            }
            return start >= 0 && CopilotDatabaseSqlPolicy.TryAnalyze(text[start..], out analysis, out _);
        }
    }
}

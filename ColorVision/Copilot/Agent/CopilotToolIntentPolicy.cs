using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColorVision.UI;

namespace ColorVision.Copilot
{
    internal static class CopilotToolIntentPolicy
    {
        private static readonly TimeSpan FollowUpToolLeaseDuration = TimeSpan.FromHours(24);
        private const int MaximumFollowUpCharacters = 300;
        private const int VisibleHistoryEvidenceLimit = 4;

        private static readonly string[] LocalScopeMarkers =
        {
            "当前项目", "这个项目", "本项目", "项目里", "工程里", "工作区", "仓库", "当前代码", "这份代码",
            "current project", "this project", "workspace", "repository", "repo", "codebase", "this code",
        };

        private static readonly string[] LocalArtifactMarkers =
        {
            "代码", "源码", "文件", "目录", "类", "方法", "函数", "实现",
            "code", "source", "file", "directory", "class", "method", "function", "implementation",
        };

        private static readonly string[] LocalInspectionMarkers =
        {
            "查看", "读取", "查找", "搜索", "检查", "审计", "评审", "排查", "定位", "实现位置", "在哪里实现", "在哪实现", "报错", "异常",
            "inspect", "read", "find", "search", "audit", "review", "examine", "locate", "where is", "error", "exception",
        };

        private static readonly string[] ConceptualQuestionMarkers =
        {
            "是什么", "什么是", "原理", "概念", "区别", "介绍", "解释", "为什么", "如何", "怎么", "怎样",
            "what is", "what are", "concept", "difference", "explain", "why", "how to", "how do",
        };

        private static readonly string[] WorkspaceEditMarkers =
        {
            "请修改", "帮我修改", "修改这个", "修改代码", "修改文件", "帮我改", "改一下", "修复这个", "修复代码", "重构", "替换代码", "编辑文件", "更新代码", "应用补丁", "写入文件",
            "实现一下", "帮我实现", "实现功能", "开发功能",
            "删除文件", "删除旧文件", "删除这个文件", "移除文件", "移除旧文件", "移除这个文件",
            "please modify", "please edit", "edit the file", "fix this", "fix the code", "please implement", "implement a", "implement the", "implement this", "make this change", "change the code", "refactor", "replace the code", "update the file", "apply the patch", "delete file", "delete the file", "remove file", "remove the file",
        };

        private static readonly string[] WorkspaceEditExplanationMarkers =
        {
            "如何修改", "怎么修改", "怎样修改", "如何修复", "怎么修复", "怎样修复", "如何实现", "怎么实现", "怎样实现", "如何重构", "怎么重构", "怎样重构",
            "how to modify", "how do i modify", "how to fix", "how do i fix", "how to implement", "how do i implement", "how to refactor", "how do i refactor",
        };

        private static readonly string[] WorkspaceEditOptOutMarkers =
        {
            "不要修改", "不用修改", "无需修改", "只说明", "只解释", "只分析", "不要写文件",
            "do not modify", "don't modify", "do not edit", "don't edit", "explain only", "analysis only", "read only",
        };

        private static readonly string[] ExplicitReadOnlyRequestMarkers =
        {
            "只读审计", "只读 审计", "只读检查", "只读 检查", "只读分析", "只读 分析", "只读评审", "只读 评审",
            "仅做只读", "只做只读",
            "不要修改任何文件", "不修改任何文件", "不得修改任何文件",
            "不要改动任何文件", "不改动任何文件", "不得改动任何文件",
            "不要写入任何文件", "不写入任何文件", "不得写入任何文件",
            "不要执行写操作", "不执行写操作", "不得执行写操作", "禁止执行写操作", "禁止写操作",
            "不做任何修改", "不作任何修改",
            "read-only audit", "read only audit", "read-only inspection", "read only inspection",
            "read-only review", "read only review", "read-only analysis", "read only analysis",
            "do not modify any file", "don't modify any file", "do not edit any file", "don't edit any file",
            "do not write any file", "don't write any file", "no file modifications", "no write operations",
            "do not perform write operations", "without modifying any file",
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
            "新建脚本", "创建脚本", "写一个脚本", "写个脚本", "编写脚本", "生成脚本",
            "create a file", "create the file", "add a file", "add the file", "create a class", "add a class", "new source file",
            "create a script", "create the script", "write a script", "generate a script",
        };

        private static readonly string[] ScriptCreateActionMarkers =
        {
            "新建", "创建", "写一个", "写个", "编写", "生成",
            "create", "write", "generate", "add",
        };

        private static readonly string[] WorkspaceCreateExplanationMarkers =
        {
            "如何新建", "怎么新建", "怎样新建", "如何创建", "怎么创建", "怎样创建", "如何编写", "怎么编写", "怎样编写",
            "how to create", "how do i create", "how to write", "how do i write", "explain how to create",
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
            "继续", "执行", "运行", "开始", "做吧", "就这么做", "按这个来",
            "再看", "再查", "再检查", "再试", "现在呢", "然后呢", "还有呢", "刚才的", "上一个",
            "continue", "execute", "run it", "start", "do it", "go ahead",
            "again", "check again", "what about", "then", "the previous", "that result",
        };

        private static readonly string[] ExplicitPlanningMarkers =
        {
            "先规划", "先计划", "制定计划", "给出计划", "列出计划", "执行计划", "分步骤", "任务清单", "多步骤", "复杂任务",
            "plan first", "make a plan", "create a plan", "execution plan", "step by step", "task list", "multi-step", "complex task",
        };

        private static readonly string[] MultiPartTaskMarkers =
        {
            "然后", "接着", "同时", "并且", "最后", "分别", "以及", "完成后", "修复后", "验证后",
            "and then", "then", "also", "finally", "as well as", "after fixing", "after that",
        };

        private static readonly string[] FlowGraphMarkers =
        {
            "流程图", "工作流程", "流程节点", "流程里", "流程中", "节点连线", "相机节点", "算法节点", ".stn",
            "flow graph", "flow editor", "workflow", "flow node", "camera node", "algorithm node",
        };

        private static readonly string[] CurrentSurfaceReferenceMarkers =
        {
            "当前", "这个", "这里", "选中", "活动", "刚才",
            "current", "this", "here", "selected", "active",
        };

        private static readonly string[] CurrentSurfaceProblemMarkers =
        {
            "失败", "错误", "异常", "超时",
            "failed", "failure", "error", "exception", "timeout",
        };

        private static readonly string[] DefinitionQuestionMarkers =
        {
            "是什么", "什么是", "介绍", "原理", "概念",
            "what is", "what are", "explain", "concept",
        };

        private static readonly string[] SavedTemplateContextMarkers =
        {
            "[ColorVision saved template reference]", "已保存模板", "保存模板", "saved template", "template code:",
        };

        private static readonly string[] TemplateTypeContextMarkers =
        {
            "[ColorVision template type reference]", "模板类型", "template type reference",
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

        private static readonly string[] ScriptRuntimeMarkers =
        {
            "Python", "Python脚本", "Python 脚本", ".py", "Node.js", "NodeJS", "Node JS", "node", "npm", "npx",
            "JavaScript", "TypeScript", ".js", ".mjs", ".cjs", ".ts", "PowerShell", "pwsh", "CMD", ".cmd", ".bat",
            "python", "python script", "node.js", "nodejs", "node script", "javascript", "typescript", "powershell", "batch script",
        };

        private static readonly string[] ScriptExecutionMarkers =
        {
            "运行", "执行", "调用", "启动", "处理", "转换", "批量", "自动化",
            "run", "execute", "invoke", "start", "process", "convert", "batch", "automate",
        };

        private static readonly string[] BatchAutomationMarkers =
        {
            "批量转换", "批量处理文件", "批处理文件", "自动处理文件",
            "batch convert", "batch process", "convert all", "process all files",
        };

        private static readonly string[] BatchImageMarkers =
        {
            "CVRAW", "CVCIE", "图像", "图片", "批量执行算法",
            "cvraw", "cvcie", "image", "images", "batch image processing",
        };

        private static readonly string[] BatchImageActionMarkers =
        {
            "批量图像", "批量图片", "批量转换", "批量处理", "批量执行算法", "打开批量",
            "转换", "转成", "转为", "转换为", "导出为",
            "转TIFF", "转TIF", "转PNG", "转JPG", "转JPEG", "转BMP", "转WEBP",
            "batch image", "batch convert", "batch process", "convert images", "convert to", "export to",
        };

        private static readonly string[] BatchImageConversionMarkers =
        {
            "批量转换", "转换", "转成", "转为", "转换为", "导出为",
            "batch convert", "convert", "export",
        };

        private static readonly HashSet<string> BatchImageFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cvraw", ".cvcie", ".bmp", ".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff",
        };

        private static readonly string[] ShellExplanationMarkers =
        {
            "如何运行", "怎么运行", "怎样运行", "如何执行", "怎么执行", "怎样执行", "运行原理", "执行原理",
            "如何创建", "怎么创建", "怎样创建",
            "how to run", "how do i run", "how to execute", "how do i execute", "how to create", "how do i create", "explain how to run",
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

            if (ContainsAny(request.UserText, LocalScopeMarkers)
                || ContainsAny(request.UserText, WorkspaceEditMarkers)
                    && !ContainsAny(request.UserText, WorkspaceEditExplanationMarkers)
                || ContainsAny(request.UserText, WorkspaceCreateMarkers)
                    && !ContainsAny(request.UserText, WorkspaceCreateExplanationMarkers)
                || ContainsAny(request.UserText, WorkspaceRollbackMarkers))
            {
                return true;
            }

            return ContainsAny(request.UserText, LocalArtifactMarkers)
                && ContainsAny(request.UserText, LocalInspectionMarkers)
                && !ContainsAny(request.UserText, ConceptualQuestionMarkers);
        }

        public static bool NeedsWorkspaceEdit(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || request.Mode == CopilotAgentMode.Review
                || ExplicitlyDisallowsWriteAccess(request)
                || request.WritableLocalRootPaths.Count == 0 && request.WritableLocalFilePaths.Count == 0
                || ContainsAny(request.UserText, WorkspaceEditOptOutMarkers)
                || ContainsAny(request.UserText, WorkspaceEditExplanationMarkers))
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
                || ExplicitlyDisallowsWriteAccess(request)
                || request.WritableLocalRootPaths.Count == 0 && request.WritableLocalFilePaths.Count == 0)
            {
                return false;
            }

            return ContainsAny(request.UserText, WorkspaceRollbackMarkers);
        }

        public static bool NeedsWorkspaceCreate(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || request.Mode == CopilotAgentMode.Review
                || ExplicitlyDisallowsWriteAccess(request)
                || request.WritableLocalRootPaths.Count == 0
                || ContainsAny(request.UserText, WorkspaceEditOptOutMarkers)
                || ContainsAny(request.UserText, WorkspaceCreateExplanationMarkers))
            {
                return false;
            }

            return ContainsAny(request.UserText, WorkspaceCreateMarkers)
                || ContainsAny(request.UserText, ScriptCreateActionMarkers)
                    && (ContainsAny(request.UserText, ScriptRuntimeMarkers)
                        || request.UserText.Contains("脚本", StringComparison.OrdinalIgnoreCase));
        }

        public static bool NeedsWorkspaceValidation(CopilotAgentRequest? request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || request.Mode == CopilotAgentMode.Review
                || ExplicitlyDisallowsWriteAccess(request)
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
            if (!IsAgentRequest(request))
                return false;

            var activeRequest = request!;
            if (NeedsShellExecution(activeRequest) && !ContainsAny(activeRequest.UserText, FlowGraphMarkers))
                return false;

            if (MatchesCurrentOrContinuation(activeRequest, FlowGraphMarkers,
                "InspectFlowGraph", "SearchFlowNodeCatalog", "PreviewFlowPatch", "ApplyFlowPatch"))
            {
                return true;
            }

            if (NeedsLocalEvidence(activeRequest))
                return false;

            return HasFlowContext(activeRequest)
                && (ContainsAny(activeRequest.UserText, CurrentSurfaceReferenceMarkers)
                    || ContainsAny(activeRequest.UserText, CurrentSurfaceProblemMarkers)
                        && !ContainsAny(activeRequest.UserText, DefinitionQuestionMarkers));
        }

        public static bool NeedsSavedTemplateContext(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (HasSavedTemplateContext(request!)
                    || MatchesCurrentOrContinuation(
                        request!,
                        SavedTemplateContextMarkers,
                        "InspectSavedTemplate"));
        }

        public static bool NeedsTemplateTypeContext(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && (HasTemplateTypeContext(request!)
                    || MatchesCurrentOrContinuation(
                        request!,
                        TemplateTypeContextMarkers,
                        "InspectTemplateType"));
        }

        public static bool NeedsFlowMutation(CopilotAgentRequest? request)
        {
            return NeedsFlowGraph(request)
                && !ExplicitlyDisallowsWriteAccess(request)
                && ContainsAny(request!.UserText, FlowMutationMarkers)
                && !ContainsAny(request.UserText, MutationExplanationMarkers);
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
                && !ExplicitlyDisallowsWriteAccess(request)
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
            if (!IsAgentRequest(request)
                || ExplicitlyDisallowsWriteAccess(request)
                || ContainsAny(request!.UserText, ShellExplanationMarkers))
                return false;

            return ContainsAny(request.UserText, ShellMarkers)
                || ContainsAny(request.UserText, ScriptRuntimeMarkers)
                    && ContainsAny(request.UserText, ScriptExecutionMarkers)
                || ContainsAny(request.UserText, ScriptExecutionMarkers)
                    && MatchesCurrentOrContinuation(request, ScriptRuntimeMarkers, "RunShellCommand")
                || ContainsAny(request.UserText, BatchAutomationMarkers)
                    && !NeedsBatchImageProcessing(request);
        }

        public static bool NeedsBatchImageProcessing(CopilotAgentRequest? request)
        {
            return IsAgentRequest(request)
                && !ExplicitlyDisallowsWriteAccess(request)
                && !ContainsAny(request!.UserText, ConceptualQuestionMarkers)
                && !(ContainsAny(request.UserText, ScriptRuntimeMarkers)
                    && ContainsAny(request.UserText, ScriptExecutionMarkers))
                && ContainsAny(request.UserText, BatchImageMarkers)
                && ContainsAny(request.UserText, BatchImageActionMarkers);
        }

        public static bool NeedsBatchImageConversionExecution(CopilotAgentRequest? request)
        {
            return NeedsBatchImageProcessing(request)
                && ContainsAny(request!.UserText, BatchImageConversionMarkers);
        }

        public static bool NeedsTaskLedger(CopilotAgentRequest? request)
        {
            if (!IsAgentRequest(request)
                || request!.Mode is CopilotAgentMode.Chat or CopilotAgentMode.Explain
                || string.IsNullOrWhiteSpace(request.UserText))
            {
                return false;
            }

            if (request.Recovery != null || ContainsAny(request.UserText, ExplicitPlanningMarkers))
                return true;

            var actionCount = CountDistinctActionIntents(request);
            if (actionCount < 2)
                return false;

            var hasExplicitParts = ContainsAny(request.UserText, MultiPartTaskMarkers)
                || request.UserText.Contains('\n')
                || request.UserText.Contains("1.", StringComparison.Ordinal)
                || request.UserText.Contains("1、", StringComparison.Ordinal);
            return hasExplicitParts && request.UserText.Length >= 80
                || actionCount >= 3 && request.UserText.Length >= 180;
        }

        private static int CountDistinctActionIntents(CopilotAgentRequest request)
        {
            var actions = new[]
            {
                NeedsLocalEvidence(request),
                NeedsWorkspaceEdit(request) || NeedsWorkspaceCreate(request) || NeedsWorkspaceRollback(request),
                NeedsWorkspaceValidation(request),
                NeedsPublicWebSearch(request) || NeedsUrlFetch(request),
                NeedsFlowMutation(request),
                NeedsDatabaseRead(request) || NeedsDatabaseWrite(request),
                NeedsRecentLogs(request)
                    || NeedsWindowsSystemInspection(request)
                    || NeedsWindowsProcessInspection(request)
                    || NeedsWindowsServiceInspection(request)
                    || NeedsTcpPortInspection(request),
                NeedsShellExecution(request),
                NeedsBatchImageProcessing(request),
            };
            return actions.Count(value => value);
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

        internal static bool ExplicitlyDisallowsWriteAccess(CopilotAgentRequest? request)
        {
            return request != null && ContainsAny(request.UserText, ExplicitReadOnlyRequestMarkers);
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
            return string.Equals(tool?.Name, "ApplyWorkspacePatchEnvelope", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsWorkspaceRollbackTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "RollbackWorkspacePatchEnvelope", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsWorkspaceValidationTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "RunWorkspaceValidation", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsShellExecutionTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "RunShellCommand", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsBatchImageProcessingTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "OpenBatchImageProcessing", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsBatchImageConversionTool(ICopilotTool? tool)
        {
            return string.Equals(tool?.Name, "ConvertBatchImages", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsBatchImageFilePath(string? path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && BatchImageFileExtensions.Contains(Path.GetExtension(path));
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
                || ContainsAny(request.UserText, LocalScopeMarkers)
                || ContainsAny(request.UserText, LocalArtifactMarkers)
                    && ContainsAny(request.UserText, LocalInspectionMarkers))
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

        private static bool HasSavedTemplateContext(CopilotAgentRequest request)
        {
            return CopilotReferenceContextSupport.HasReference(
                request,
                "composer-template:",
                "[ColorVision saved template reference]");
        }

        private static bool HasTemplateTypeContext(CopilotAgentRequest request)
        {
            return CopilotReferenceContextSupport.HasReference(
                request,
                "composer-template-type:",
                "[ColorVision template type reference]");
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

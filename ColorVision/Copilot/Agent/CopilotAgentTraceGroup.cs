using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentTraceGroup
    {
        private CopilotAgentTraceGroup(string category, IReadOnlyList<CopilotAgentTraceEntry> entries)
        {
            Category = category;
            Entries = entries;
        }

        internal string Category { get; }

        public IReadOnlyList<CopilotAgentTraceEntry> Entries { get; }

        public CopilotAgentTraceEntry FirstEntry => Entries[0];

        public bool IsSingle => Entries.Count == 1;

        public bool IsMultiple => Entries.Count > 1;

        public bool IsFailure => Entries.Any(entry => entry.IsFailure);

        public string ActivityGlyph
        {
            get
            {
                if (Entries.Any(entry => entry.State is CopilotToolExecutionState.Pending or CopilotToolExecutionState.Running))
                    return "·";
                if (Entries.Any(entry => entry.State == CopilotToolExecutionState.AwaitingApproval))
                    return "?";
                if (Entries.Any(entry => entry.State is CopilotToolExecutionState.Failed or CopilotToolExecutionState.TimedOut))
                    return "!";
                if (Entries.Any(entry => entry.State is CopilotToolExecutionState.Denied or CopilotToolExecutionState.Cancelled or CopilotToolExecutionState.Interrupted))
                    return "×";
                return "✓";
            }
        }

        public string ActivityLabel
        {
            get
            {
                if (IsSingle)
                    return FirstEntry.ActivityLabel;

                var (running, completed) = Category switch
                {
                    "command" => ("正在运行多个命令", "运行了多个命令"),
                    "database-query" => ("正在执行多次数据库查询", "执行了多次数据库查询"),
                    "database-write" => ("正在执行多条数据库 SQL", "执行了多条数据库 SQL"),
                    "web" => ("正在处理多个网页请求", "处理了多个网页请求"),
                    "file-read" => ("正在读取多个文件", "读取了多个文件"),
                    "file-search" => ("正在执行多次文件搜索", "执行了多次文件搜索"),
                    "delegation" => ("正在运行多个子 Agent", "运行了多个子 Agent"),
                    "workspace" => ("正在处理多项文件修改", "处理了多项文件修改"),
                    "application" => ("正在执行多个应用操作", "执行了多个应用操作"),
                    _ => ("正在运行多个工具调用", "运行了多个工具调用"),
                };

                if (Entries.Any(entry => entry.State is CopilotToolExecutionState.Pending or CopilotToolExecutionState.Running))
                    return running;
                if (Entries.Any(entry => entry.State == CopilotToolExecutionState.AwaitingApproval))
                    return completed + " · 等待批准";

                var failureCount = Entries.Count(entry => entry.IsFailure);
                return failureCount switch
                {
                    0 => completed,
                    _ when failureCount == Entries.Count => completed + " · 失败",
                    _ => completed + " · 部分失败",
                };
            }
        }

        public string ActivityDurationLabel => IsSingle ? FirstEntry.ActivityDurationLabel : string.Empty;

        public string ActivityDescription => IsSingle
            ? FirstEntry.ActivityDescription
            : $"包含 {Entries.Count} 次调用，展开可查看每次调用的结果和诊断信息。";

        public static IReadOnlyList<CopilotAgentTraceGroup> Create(IEnumerable<CopilotAgentTraceEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            var groups = new List<CopilotAgentTraceGroup>();
            var currentEntries = new List<CopilotAgentTraceEntry>();
            var currentCategory = string.Empty;

            foreach (var entry in entries.Where(entry => entry != null && entry.IsVisibleInActivity))
            {
                var category = GetCategory(entry.ToolName);
                if (currentEntries.Count > 0 && !string.Equals(category, currentCategory, StringComparison.Ordinal))
                {
                    groups.Add(new CopilotAgentTraceGroup(currentCategory, currentEntries.ToArray()));
                    currentEntries.Clear();
                }

                currentCategory = category;
                currentEntries.Add(entry);
            }

            if (currentEntries.Count > 0)
                groups.Add(new CopilotAgentTraceGroup(currentCategory, currentEntries.ToArray()));

            return groups;
        }

        private static string GetCategory(string toolName)
        {
            return toolName switch
            {
                "FetchUrl" or "WebSearch" => "web",
                "ReadLocalFile" or "ReadAttachedFile" or "GetRecentLog" => "file-read",
                "ListDirectory" or "SearchFiles" or "GrepText" or "SearchDocs" => "file-search",
                "DelegateExplore" or "DelegateScout" => "delegation",
                _ when toolName.StartsWith("Delegate", StringComparison.Ordinal) => "delegation",
                "QueryFlowExecutionStats" or "QueryDatabaseSql" => "database-query",
                "ExecuteDatabaseSql" => "database-write",
                "InspectWindowsSystem" or "InspectWindowsProcesses" or "InspectWindowsServices" or "InspectTcpPort" or "InspectGitWorkingTree" or "InspectGitDiff" or "RunShellCommand" => "command",
                "PreviewWorkspacePatch" or "PreviewCreateWorkspaceFile" or "PreviewWorkspaceChangeSet"
                    or "ApplyWorkspacePatch" or "ApplyCreateWorkspaceFile" or "ApplyWorkspaceChangeSet"
                    or "RollbackWorkspacePatch" or "RollbackWorkspaceChangeSet"
                    or "PreviewWorkspacePatchEnvelope" or "ApplyWorkspacePatchEnvelope" or "RollbackWorkspacePatchEnvelope" => "workspace",
                "CreateFlow" or "ApplyTemplatePatch" or "TemplatePatch" or "ExecuteMenu" or "SetLanguage" or "SetTheme" => "application",
                _ => "tool:" + toolName,
            };
        }
    }
}

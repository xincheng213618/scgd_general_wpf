using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public enum CopilotLocalCommandKind
    {
        Help,
        Shortcuts,
        Recap,
        Status,
        Doctor,
        Feedback,
        Tasks,
        Queue,
        Approve,
        Usage,
        Statistics,
        Context,
        Permissions,
        InitializeProject,
        Skills,
        Mcp,
        Mention,
        Diff,
        Compact,
        Review,
        Plan,
        Goal,
        ResumeConversation,
        RenameConversation,
        RewindConversation,
        SearchPromptHistory,
        CopyResponse,
        ExportConversation,
        FindInConversation,
        SelectModel,
        SelectReasoning,
        SelectPersonality,
        NewConversation,
        ClearConversation,
        ForkConversation,
        SideQuestion,
        Skill,
        Hooks,
    }

    public sealed record CopilotLocalCommand(
        string Name,
        string Description,
        CopilotLocalCommandKind Kind,
        bool AcceptsArguments = false,
        bool AvailableWhileAgentRuns = false,
        string Usage = "",
        IReadOnlyList<CopilotLocalCommandArgument>? Arguments = null,
        bool RequiresMoreInputAfterCompletion = false)
    {
        public string CompletionText => Name + (AcceptsArguments ? " " : string.Empty);
    }

    public sealed record CopilotLocalCommandArgument(
        string Value,
        string Description,
        bool AcceptsArguments = false);

    public sealed record CopilotLocalCommandInvocation(
        CopilotLocalCommand Command,
        string Arguments);

    public static class CopilotLocalCommandCatalog
    {
        private const int BaselineMaximumSuggestions = 40;
        private const int MinimumBareSlashSkillSuggestions = 9;

        private static readonly CopilotLocalCommand[] Commands =
        [
            new("/help", "查看全部固定命令，或查询单个命令的用法", CopilotLocalCommandKind.Help, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/help [命令]"),
            new("/shortcuts", "查看按焦点作用域整理的键盘快捷键", CopilotLocalCommandKind.Shortcuts, AvailableWhileAgentRuns: true, Usage: "/shortcuts"),
            new("/recap", "回顾当前会话的目标、最近一轮与待执行状态", CopilotLocalCommandKind.Recap, AvailableWhileAgentRuns: true, Usage: "/recap"),
            new("/status", "查看模型、Agent、工作区与连接状态", CopilotLocalCommandKind.Status, AvailableWhileAgentRuns: true, Usage: "/status"),
            new("/doctor", "检查模型、会话保存、任务、MCP、Hook 与 Skill 健康度", CopilotLocalCommandKind.Doctor, AvailableWhileAgentRuns: true, Usage: "/doctor"),
            new("/feedback", "反馈当前 Copilot 会话问题，可补充问题说明", CopilotLocalCommandKind.Feedback, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/feedback [问题说明]"),
            new("/tasks", "查看正在运行、排队及等待恢复的 Agent 任务", CopilotLocalCommandKind.Tasks, AvailableWhileAgentRuns: true, Usage: "/tasks"),
            new("/queue", "查看当前会话等待执行的后续请求", CopilotLocalCommandKind.Queue, AvailableWhileAgentRuns: true, Usage: "/queue"),
            new("/approve", "查看待确认操作，或打开指定操作的原生审查窗口", CopilotLocalCommandKind.Approve, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/approve [N]"),
            new("/usage", "查看当前会话已记录的输入、输出与缓存 Token", CopilotLocalCommandKind.Usage, AvailableWhileAgentRuns: true, Usage: "/usage"),
            new("/stats", "汇总最近 7 天、30 天或全部本地会话活动", CopilotLocalCommandKind.Statistics, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/stats [7|30|all]", Arguments:
            [
                new("7", "最近 7 个本机日历日"),
                new("30", "最近 30 个本机日历日"),
                new("all", "全部本地会话历史"),
            ]),
            new("/context", "查看本地上下文、预算与注入统计", CopilotLocalCommandKind.Context, AvailableWhileAgentRuns: true, Usage: "/context"),
            new("/permissions", "选择按需确认/临时自动复核，或查看权限状态", CopilotLocalCommandKind.Permissions, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/permissions [status|ask|auto]", Arguments:
            [
                new("status", "显示当前文件范围、能力与审批策略"),
                new("ask", "恢复受保护操作逐次确认"),
                new("auto", "为下一任务或当前任务临时启用自动复核"),
            ]),
            new("/init", "为当前项目生成根级 AGENTS.md，不覆盖已有项目指令", CopilotLocalCommandKind.InitializeProject, Usage: "/init"),
            new("/hooks", "查看生效 Hook、模块来源与最近运行健康度", CopilotLocalCommandKind.Hooks, AvailableWhileAgentRuns: true, Usage: "/hooks"),
            new("/skills", "查看 Skill 使用率、连续未加载与降级状态", CopilotLocalCommandKind.Skills, AvailableWhileAgentRuns: true, Usage: "/skills"),
            new("/mcp", "查看本机与外部 MCP 状态；verbose 展开脱敏诊断", CopilotLocalCommandKind.Mcp, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/mcp [verbose]", Arguments:
            [
                new("verbose", "展开外部服务策略、工具发现与最近健康快照"),
            ]),
            new("/mention", "打开文件、模板与菜单的关联目录，可补充查询", CopilotLocalCommandKind.Mention, AcceptsArguments: true, Usage: "/mention [查询]"),
            new("/diff", "查看已暂存、未暂存补丁和未跟踪文件", CopilotLocalCommandKind.Diff, AcceptsArguments: true, Usage: "/diff [both|staged|unstaged]", Arguments:
            [
                new("both", "同时查看已暂存和未暂存变更"),
                new("staged", "只查看已暂存变更"),
                new("unstaged", "只查看未暂存变更和未跟踪文件"),
            ]),
            new("/compact", "压缩早期对话，可在命令后补充聚焦要求", CopilotLocalCommandKind.Compact, AcceptsArguments: true, Usage: "/compact [聚焦要求]"),
            new("/review", "只读审查当前工作区变更，可补充关注点", CopilotLocalCommandKind.Review, AcceptsArguments: true, Usage: "/review [关注点]"),
            new("/plan", "只读分析并生成可执行计划，可在命令后直接填写任务", CopilotLocalCommandKind.Plan, AcceptsArguments: true, Usage: "/plan [任务]"),
            new("/goal", "查看或管理当前会话的持续目标", CopilotLocalCommandKind.Goal, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/goal [目标|edit <新目标>|pause|resume|clear]", Arguments:
            [
                new("edit", "修改当前持续目标", AcceptsArguments: true),
                new("pause", "暂停当前持续目标"),
                new("resume", "恢复已暂停的持续目标"),
                new("clear", "清除当前持续目标"),
            ]),
            new("/resume", "搜索并切换已有 Copilot 会话，可补充标题或关键词", CopilotLocalCommandKind.ResumeConversation, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/resume [会话 ID|标题|关键词]"),
            new("/rename", "重命名当前 Copilot 会话；省略名称时打开输入窗口", CopilotLocalCommandKind.RenameConversation, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/rename [新名称]"),
            new("/rewind", "从历史请求创建仅会话回溯分支，并恢复原请求供修改", CopilotLocalCommandKind.RewindConversation, AcceptsArguments: true, Usage: "/rewind [N]"),
            new("/history", "搜索可见历史请求，可切换当前/全部会话并恢复到输入框", CopilotLocalCommandKind.SearchPromptHistory, Usage: "/history"),
            new("/copy", "复制最近已完成的回答；可用 /copy 2 选择倒数第二条", CopilotLocalCommandKind.CopyResponse, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/copy [N]"),
            new("/export", "复制当前会话的可见 Markdown；可补充文件名并打开保存窗口", CopilotLocalCommandKind.ExportConversation, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/export [文件名.md|文件名.txt]"),
            new("/find", "查找并定位当前会话中的可见消息", CopilotLocalCommandKind.FindInConversation, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/find [文本]"),
            new("/model", "选择当前会话使用的模型 Profile；可补充 Profile 名或模型 ID", CopilotLocalCommandKind.SelectModel, AcceptsArguments: true, Usage: "/model [Profile 名|模型 ID]"),
            new("/reasoning", "选择当前模型 Profile 的推理强度；可补充受支持级别", CopilotLocalCommandKind.SelectReasoning, AcceptsArguments: true, Usage: "/reasoning [auto|off|on|high|max]"),
            new("/effort", "同 /reasoning；调整当前模型 Profile 的推理强度", CopilotLocalCommandKind.SelectReasoning, AcceptsArguments: true, Usage: "/effort [auto|off|on|high|max]"),
            new("/personality", "设置当前会话后续回答的默认沟通风格", CopilotLocalCommandKind.SelectPersonality, AcceptsArguments: true, Usage: "/personality [friendly|pragmatic|none]", Arguments:
            [
                new("friendly", "友好协作，同时保持直接和证据优先"),
                new("pragmatic", "结果优先，简洁直接，只说明关键权衡"),
                new("none", "不附加会话级沟通风格"),
            ]),
            new("/new", "开始一个新的 Copilot 会话", CopilotLocalCommandKind.NewConversation, Usage: "/new"),
            new("/clear", "清空当前上下文并开始新会话；可先命名旧会话", CopilotLocalCommandKind.ClearConversation, AcceptsArguments: true, Usage: "/clear [旧会话名称]"),
            new("/fork", "复制当前会话到新会话分支；Agent 运行时创建可见快照", CopilotLocalCommandKind.ForkConversation, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/fork [新会话名称]"),
            new("/branch", "同 /fork；只分叉会话，不创建 Git 分支", CopilotLocalCommandKind.ForkConversation, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/branch [新会话名称]"),
            new("/btw", "从当前会话上下文回答一次旁路问题，不影响主任务", CopilotLocalCommandKind.SideQuestion, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/btw [问题]"),
        ];

        public static IReadOnlyList<CopilotLocalCommand> All => Commands;

        private static int MaximumSuggestions =>
            Math.Max(BaselineMaximumSuggestions, Commands.Length + MinimumBareSlashSkillSuggestions);

        public static CopilotLocalCommand? FindExact(string? input)
        {
            var invocation = Parse(input);
            return invocation is { Arguments.Length: 0 } ? invocation.Command : null;
        }

        public static CopilotLocalCommandInvocation? Parse(string? input)
        {
            var normalized = Normalize(input);
            if (normalized.Length == 0)
                return null;

            var separatorIndex = normalized.IndexOfAny([' ', '\t', '\r', '\n']);
            var name = separatorIndex < 0 ? normalized : normalized[..separatorIndex];
            var arguments = separatorIndex < 0 ? string.Empty : normalized[(separatorIndex + 1)..].Trim();
            var command = Commands.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (command == null || (!command.AcceptsArguments && arguments.Length > 0))
                return null;

            return new CopilotLocalCommandInvocation(command, arguments);
        }

        public static IReadOnlyList<CopilotLocalCommand> Suggest(
            string? input,
            IReadOnlyList<CopilotAgentSkillCatalogItem>? skills = null,
            IReadOnlyList<CopilotProfileConfig>? profiles = null,
            CopilotProfileConfig? selectedProfile = null,
            CopilotLocalCommandComposerContext composerContext = CopilotLocalCommandComposerContext.Idle)
        {
            var normalized = (input ?? string.Empty).TrimStart();
            if (!CopilotLocalCommandAvailabilityPolicy.CanShowSuggestions(composerContext)
                || normalized.Length == 0
                || normalized[0] is not '/' and not '$')
            {
                return Array.Empty<CopilotLocalCommand>();
            }

            var separatorIndex = normalized.IndexOfAny([' ', '\t', '\r', '\n']);
            if (separatorIndex >= 0)
                return SuggestArguments(normalized, separatorIndex, profiles, selectedProfile, composerContext);
            if (normalized.StartsWith('/') && FindExact(normalized) != null)
                return Array.Empty<CopilotLocalCommand>();

            var suggestions = normalized.StartsWith('/')
                ? Commands.Where(command =>
                    CopilotLocalCommandAvailabilityPolicy.CanSuggest(command, composerContext)
                    && command.Name.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                : Enumerable.Empty<CopilotLocalCommand>();
            var skillSuggestions = (skills ?? Array.Empty<CopilotAgentSkillCatalogItem>())
                .Select(skill => new CopilotLocalCommand(
                    normalized[0] + skill.Name,
                    "Skill · " + skill.Description,
                    CopilotLocalCommandKind.Skill,
                    AcceptsArguments: true,
                    Usage: normalized[0] + skill.Name + " [参数]"))
                .Where(command => command.Name.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(command.Name, normalized, StringComparison.OrdinalIgnoreCase));
            return suggestions
                .Concat(skillSuggestions)
                .DistinctBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaximumSuggestions)
                .ToArray();
        }

        private static CopilotLocalCommand[] SuggestArguments(
            string input,
            int separatorIndex,
            IReadOnlyList<CopilotProfileConfig>? profiles,
            CopilotProfileConfig? selectedProfile,
            CopilotLocalCommandComposerContext composerContext)
        {
            if (input[0] != '/')
                return Array.Empty<CopilotLocalCommand>();

            var name = input[..separatorIndex];
            var command = Commands.FirstOrDefault(item =>
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (command?.AcceptsArguments != true
                || !CopilotLocalCommandAvailabilityPolicy.CanSuggest(command, composerContext))
                return Array.Empty<CopilotLocalCommand>();

            var query = input[(separatorIndex + 1)..].TrimStart();
            if (command.Kind == CopilotLocalCommandKind.Help)
                query = query.TrimStart('/');
            var arguments = ResolveArguments(command, profiles, selectedProfile);
            return arguments
                .Where(argument => argument.Value.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .Where(argument => argument.AcceptsArguments
                    || !string.Equals(argument.Value, query, StringComparison.OrdinalIgnoreCase))
                .Take(MaximumSuggestions)
                .Select(argument => new CopilotLocalCommand(
                    command.Name + " " + argument.Value,
                    "参数 · " + argument.Description,
                    command.Kind,
                    AcceptsArguments: argument.AcceptsArguments,
                    AvailableWhileAgentRuns: command.AvailableWhileAgentRuns,
                    Usage: command.Usage,
                    RequiresMoreInputAfterCompletion: argument.AcceptsArguments))
                .ToArray();
        }

        private static IReadOnlyList<CopilotLocalCommandArgument> ResolveArguments(
            CopilotLocalCommand command,
            IReadOnlyList<CopilotProfileConfig>? profiles,
            CopilotProfileConfig? selectedProfile)
        {
            if (command.Kind == CopilotLocalCommandKind.Help)
            {
                return Commands.Select(item => new CopilotLocalCommandArgument(
                        item.Name[1..],
                        item.Description))
                    .ToArray();
            }

            if (command.Kind == CopilotLocalCommandKind.SelectModel)
                return BuildProfileArguments(profiles, selectedProfile);
            if (command.Kind == CopilotLocalCommandKind.SelectReasoning)
                return BuildReasoningArguments(selectedProfile);
            return command.Arguments ?? Array.Empty<CopilotLocalCommandArgument>();
        }

        private static CopilotLocalCommandArgument[] BuildProfileArguments(
            IReadOnlyList<CopilotProfileConfig>? profiles,
            CopilotProfileConfig? selectedProfile)
        {
            var candidates = (profiles ?? Array.Empty<CopilotProfileConfig>())
                .Where(profile => profile != null)
                .ToArray();
            var duplicateLabels = candidates
                .GroupBy(profile => profile.DisplayLabel, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return candidates.Select(profile =>
                {
                    var value = duplicateLabels.Contains(profile.DisplayLabel)
                        ? profile.Id
                        : profile.DisplayLabel;
                    var current = ReferenceEquals(profile, selectedProfile)
                        || string.Equals(profile.Id, selectedProfile?.Id, StringComparison.Ordinal);
                    var description = profile.SecondaryLabel;
                    if (current)
                        description += " · 当前";
                    return new CopilotLocalCommandArgument(
                        value,
                        description);
                })
                .DistinctBy(argument => argument.Value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static CopilotLocalCommandArgument[] BuildReasoningArguments(
            CopilotProfileConfig? selectedProfile)
        {
            if (!CopilotReasoningCapabilities.HasConfigurableReasoning(selectedProfile))
                return Array.Empty<CopilotLocalCommandArgument>();

            return CopilotReasoningCapabilities.GetOptions(selectedProfile)
                .Select(option => new CopilotLocalCommandArgument(
                    CopilotReasoningCapabilities.GetCommandToken(option.Mode),
                    option.Description + (option.IsSelected ? " · 当前" : string.Empty)))
                .ToArray();
        }

        private static string Normalize(string? input)
        {
            return (input ?? string.Empty).Trim();
        }
    }
}

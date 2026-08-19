using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot
{
    public enum CopilotLocalCommandKind
    {
        Help,
        Shortcuts,
        Recap,
        Status,
        EffectiveConfig,
        Doctor,
        Feedback,
        Tasks,
        BackgroundCommands,
        TaskLog,
        Queue,
        StopTask,
        Approve,
        Usage,
        Subagents,
        [Obsolete("Use Usage. This value remains reserved to preserve enum numbering.")]
        Statistics,
        Context,
        ProjectInstructions,
        Permissions,
        AdditionalDirectories,
        Settings,
        InitializeProject,
        Skills,
        Mcp,
        Mention,
        Diff,
        RollbackWorkspace,
        Compact,
        Review,
        Verify,
        Plan,
        ViewPlan,
        Goal,
        ResumeConversation,
        ArchiveConversation,
        DeleteConversation,
        UnarchiveConversation,
        RenameConversation,
        RewindConversation,
        NavigateTurn,
        SearchPromptHistory,
        PromptSuggestions,
        Transcript,
        Timestamps,
        CompactMode,
        MultilineComposer,
        FollowUpBehavior,
        RetryResponse,
        CopyResponse,
        ExportConversation,
        FindInConversation,
        SelectModel,
        SelectReasoning,
        SelectPersonality,
        [Obsolete("Use ClearConversation. This value remains reserved to preserve enum numbering.")]
        NewConversation,
        ClearConversation,
        ForkConversation,
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
        public string CompletionText => CompletionTextOverride.Length > 0
            ? CompletionTextOverride
            : Name + (AcceptsArguments ? " " : string.Empty);

        public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();

        internal string CompletionTextOverride { get; init; } = string.Empty;

        internal CopilotAgentSkillReference? AgentSkillReference { get; init; }
    }

    public sealed record CopilotLocalCommandArgument(
        string Value,
        string Description,
        bool AcceptsArguments = false);

    public sealed record CopilotLocalCommandInvocation(
        CopilotLocalCommand Command,
        string Arguments)
    {
        public string InvokedName { get; init; } = Command.Name;
    }

    public static class CopilotLocalCommandCatalog
    {
        private const int BaselineMaximumSuggestions = 40;
        private const int MinimumBareSlashSkillSuggestions = 9;

        private static readonly CopilotLocalCommand[] Commands =
        [
            new("/help", "查看全部固定命令，或查询单个命令的用法", CopilotLocalCommandKind.Help, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/help [命令]"),
            new("/shortcuts", "查看按焦点作用域整理的键盘快捷键", CopilotLocalCommandKind.Shortcuts, AvailableWhileAgentRuns: true, Usage: "/shortcuts"),
            new("/recap", "回顾当前会话的目标、最近一轮与待执行状态", CopilotLocalCommandKind.Recap, AvailableWhileAgentRuns: true, Usage: "/recap"),
            new("/status", "查看模型、Agent、工作区与连接状态", CopilotLocalCommandKind.Status, AvailableWhileAgentRuns: true, Usage: "/status") { Aliases = ["/session-info"] },
            new("/debug-config", "查看 Copilot 配置来源链、会话覆盖与脱敏后的运行时有效值", CopilotLocalCommandKind.EffectiveConfig, AvailableWhileAgentRuns: true, Usage: "/debug-config"),
            new("/doctor", "检查模型、会话保存、任务、MCP、Hook 与 Skill 健康度", CopilotLocalCommandKind.Doctor, AvailableWhileAgentRuns: true, Usage: "/doctor"),
            new("/feedback", "反馈当前 Copilot 会话问题，可补充问题说明", CopilotLocalCommandKind.Feedback, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/feedback [问题说明]"),
            new("/tasks", "查看、停止、恢复或放弃指定的 Agent 任务恢复项", CopilotLocalCommandKind.Tasks, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/tasks [stop N|resume N|dismiss N]", Arguments:
            [
                new("stop", "经原生确认停止任务列表中的第 N 项", AcceptsArguments: true),
                new("resume", "恢复“需要处理”中的第 N 项", AcceptsArguments: true),
                new("dismiss", "经原生确认放弃“需要处理”中的第 N 项", AcceptsArguments: true),
            ]),
            new("/ps", "查看当前会话由 Copilot 启动的后台命令、限长输出或停止进程树", CopilotLocalCommandKind.BackgroundCommands, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/ps [N|stop N|clear]", Arguments:
            [
                new("stop", "经原生确认停止后台命令第 N 项的进程树", AcceptsArguments: true),
                new("clear", "清除当前会话已经结束的后台命令记录"),
            ]),
            new("/task-log", "查看当前会话最近或失败的 Agent 工具、审批与停止事件", CopilotLocalCommandKind.TaskLog, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/task-log [N|errors]", Arguments:
            [
                new("errors", "只显示错误、阻塞、审批拒绝、异常停止及带失败码的事件"),
            ]),
            new("/queue", "查看或控制当前会话等待执行的后续请求", CopilotLocalCommandKind.Queue, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/queue [clear|send N|edit N|up N|down N|delete N]", Arguments:
            [
                new("clear", "经原生确认取消当前会话全部排队请求"),
                new("send", "把 #N 提升到下一项并停止当前任务", AcceptsArguments: true),
                new("edit", "取消 #N 并把请求及附件恢复到输入框", AcceptsArguments: true),
                new("up", "把 #N 向前移动一位", AcceptsArguments: true),
                new("down", "把 #N 向后移动一位", AcceptsArguments: true),
                new("delete", "取消 #N；若为自动续作，同时暂停对应持续目标", AcceptsArguments: true),
            ]),
            new("/stop", "停止当前任务；有安全 checkpoint 时优先暂停，否则取消当前轮次", CopilotLocalCommandKind.StopTask, AvailableWhileAgentRuns: true, Usage: "/stop"),
            new("/approve", "审核待确认操作，或为自动审查拒绝授权一次精确重试", CopilotLocalCommandKind.Approve, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/approve [N]"),
            new("/usage", "查看当前会话或本地每日、每周与累计 Token 活动", CopilotLocalCommandKind.Usage, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: CopilotUsageCommand.Usage, Arguments:
            [
                new("session", "当前会话 Token、Agent 时延与最新供应商限额"),
                new("daily", "最近 7 个本机日历日的逐日活动"),
                new("weekly", "最近 30 个本机日历日的本地活动"),
                new("cumulative", "全部本地会话历史累计"),
            ]) { Aliases = ["/stats"] },
            new("/agents", "按活动或结束状态查看、关闭请求级子代理，或按 run_id 引导、停止子代理；父 Agent 继续运行", CopilotLocalCommandKind.Subagents, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/agents [roles|runs [N]|active [N]|done [N]|show <run_id>|close <run_id>|steer <run_id> <message>|stop <run_id>]", Arguments:
            [
                new("roles", "只显示内置子代理角色及其只读能力"),
                new("runs", "显示当前会话 N 次可见子运行；运行中优先，同状态新到旧", AcceptsArguments: true),
                new("active", "只显示当前会话最近 N 次活动子运行", AcceptsArguments: true),
                new("done", "只显示当前会话最近 N 次已结束子运行", AcceptsArguments: true),
                new("show", "按 run_id 显示单次子运行的限长结果与审计详情", AcceptsArguments: true),
                new("close", "按 run_id 从默认列表关闭已结束子运行；结果与审计保留", AcceptsArguments: true),
                new("steer", "按 run_id 向当前会话中的运行中子代理排入新指令", AcceptsArguments: true),
                new("stop", "按 run_id 停止当前会话中的运行中子代理；父 Agent 继续", AcceptsArguments: true),
            ]) { Aliases = ["/subagents"] },
            new("/context", "查看本地上下文、预算与注入统计", CopilotLocalCommandKind.Context, AvailableWhileAgentRuns: true, Usage: "/context"),
            new("/memory", "预览工作区型 Agent 请求会加载的个人与项目指令，或按编号打开源文件", CopilotLocalCommandKind.ProjectInstructions, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/memory [open N]", Arguments:
            [
                new("open", "在内置编辑器中打开第 N 个生效指令文件", AcceptsArguments: true),
            ]) { Aliases = ["/instructions"] },
            new("/permissions", "选择按需确认/临时自动复核，或查看权限状态", CopilotLocalCommandKind.Permissions, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/permissions [status|ask|auto]", Arguments:
            [
                new("status", "显示当前文件范围、能力与审批策略"),
                new("ask", "恢复受保护操作逐次确认"),
                new("auto", "为下一任务或当前任务临时启用自动复核"),
            ]),
            new("/add-dir", "管理当前会话后续 Agent 请求可读取的附加目录", CopilotLocalCommandKind.AdditionalDirectories, AcceptsArguments: true, Usage: CopilotAdditionalDirectoryCommand.Usage, Arguments:
            [
                new("list", "列出当前会话的附加只读目录"),
                new("add", "添加一个现有绝对目录", AcceptsArguments: true),
                new("remove", "按编号移除附加目录", AcceptsArguments: true),
                new("clear", "清空全部附加目录"),
            ]),
            new("/settings", "打开模型、Agent、MCP 或后端同步设置", CopilotLocalCommandKind.Settings, AcceptsArguments: true, Usage: "/settings [models|agent|mcp|sync]", Arguments:
            [
                new("models", "模型 Profile、Endpoint 与推理设置"),
                new("agent", "Agent 默认行为与上下文预算"),
                new("mcp", "MCP 服务与控制能力"),
                new("sync", "后端配置同步"),
            ]) { Aliases = ["/config", "/preferences", "/prefs"] },
            new("/init", "为当前项目生成根级 AGENTS.md，不覆盖已有项目指令", CopilotLocalCommandKind.InitializeProject, Usage: "/init"),
            new("/hooks", "查看生效 Hook、模块来源与最近运行健康度", CopilotLocalCommandKind.Hooks, AvailableWhileAgentRuns: true, Usage: "/hooks"),
            new("/skills", "列出当前工作区 Skill、使用证据；支持按 SKILL.md 路径启停", CopilotLocalCommandKind.Skills, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: CopilotAgentSkillCommand.Usage, Arguments:
            [
                new("reload", "强制从磁盘重扫当前工作区与内置 Skill 目录"),
                new("off", "按列表编号关闭一个精确 Skill 路径", AcceptsArguments: true),
                new("enable", "按列表编号恢复一个精确 Skill 路径", AcceptsArguments: true),
            ]),
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
            new("/rollback", "查看或安全撤销当前会话仍可回滚的精确文件修改", CopilotLocalCommandKind.RollbackWorkspace, AcceptsArguments: true, Usage: "/rollback [N]"),
            new("/compact", "压缩早期对话，可在命令后补充聚焦要求", CopilotLocalCommandKind.Compact, AcceptsArguments: true, Usage: "/compact [聚焦要求]"),
            new("/review", "只读审查工作区、基线分支或指定提交，可补充关注点", CopilotLocalCommandKind.Review, AcceptsArguments: true, Usage: "/review [--current|--base <分支>|--commit <提交号>] [关注点]", Arguments:
            [
                new("--current", "审查当前已暂存和未暂存变更", AcceptsArguments: true),
                new("--base", "审查指定基线分支的合并基点到 HEAD", AcceptsArguments: true),
                new("--commit", "审查指定十六进制提交号", AcceptsArguments: true),
            ]),
            new("/verify", "只读审查改动并经确认运行一次受限构建或测试", CopilotLocalCommandKind.Verify, AcceptsArguments: true, Usage: "/verify [关注点]") { Aliases = ["/check-work", "/check"] },
            new("/plan", "只读分析并生成可执行计划，可在命令后直接填写任务", CopilotLocalCommandKind.Plan, AcceptsArguments: true, Usage: "/plan [任务]"),
            new("/view-plan", "定位当前会话最近一份已完成计划", CopilotLocalCommandKind.ViewPlan, AvailableWhileAgentRuns: true, Usage: "/view-plan"),
            new("/goal", "查看或管理当前会话的持续目标", CopilotLocalCommandKind.Goal, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/goal [目标|history|edit <新目标>|budget <Token|clear>|pause|resume|clear]", Arguments:
            [
                new("history", "查看当前持续目标最近的有界迭代记录"),
                new("edit", "修改当前持续目标", AcceptsArguments: true),
                new("budget", "设置或清除持续目标 Token 预算", AcceptsArguments: true),
                new("pause", "暂停当前持续目标"),
                new("resume", "恢复已暂停的持续目标"),
                new("clear", "清除当前持续目标"),
            ]),
            new("/resume", "搜索并切换已有 Copilot 会话，可补充标题或关键词", CopilotLocalCommandKind.ResumeConversation, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/resume [会话 ID|标题|关键词]"),
            new("/archive", "归档当前会话并从常用列表隐藏；不会删除内容", CopilotLocalCommandKind.ArchiveConversation, Usage: "/archive"),
            new("/delete", "永久删除当前会话；始终经过保留状态检查与原生二次确认", CopilotLocalCommandKind.DeleteConversation, Usage: "/delete"),
            new("/unarchive", "列出或恢复已归档会话", CopilotLocalCommandKind.UnarchiveConversation, AcceptsArguments: true, Usage: "/unarchive [会话 ID|唯一完整标题|关键词]") { Aliases = ["/archived"] },
            new("/rename", "重命名当前 Copilot 会话；省略名称时打开输入窗口", CopilotLocalCommandKind.RenameConversation, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/rename [新名称]"),
            new("/rewind", "从历史请求创建仅会话回溯分支，并恢复原请求供修改", CopilotLocalCommandKind.RewindConversation, AcceptsArguments: true, Usage: "/rewind [N]"),
            new("/turn", "定位当前会话倒数第 N 条用户请求；1 表示最近一条", CopilotLocalCommandKind.NavigateTurn, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/turn [N]"),
            new("/history", "搜索可见历史请求，可切换当前/全部会话并恢复到输入框", CopilotLocalCommandKind.SearchPromptHistory, Usage: "/history"),
            new("/suggestions", "管理当前设备上的本地历史补全", CopilotLocalCommandKind.PromptSuggestions, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/suggestions [on|off]", Arguments:
            [
                new("on", "输入普通请求时显示当前会话的历史前缀补全"),
                new("off", "隐藏本地历史前缀补全"),
            ]),
            new("/transcript", "展开或收起当前会话的推理与工具活动", CopilotLocalCommandKind.Transcript, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/transcript [expand|collapse]", Arguments:
            [
                new("expand", "展开全部已有推理与工具活动"),
                new("collapse", "收起全部已有推理与工具活动"),
            ]),
            new("/timestamps", "显示或隐藏用户与助手消息的本地时间戳", CopilotLocalCommandKind.Timestamps, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/timestamps [on|off]", Arguments:
            [
                new("on", "显示用户与助手消息时间"),
                new("off", "隐藏用户与助手消息时间"),
            ]),
            new("/compact-mode", "切换仅本地的紧凑消息间距，不压缩会话上下文", CopilotLocalCommandKind.CompactMode, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/compact-mode [on|off]", Arguments:
            [
                new("on", "减少消息列表与气泡间距"),
                new("off", "恢复标准消息间距"),
            ]),
            new("/multiline", "切换 Enter 换行、Shift+Enter 发送的多行输入模式", CopilotLocalCommandKind.MultilineComposer, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/multiline [on|off]", Arguments:
            [
                new("on", "Enter 换行，Shift+Enter 或 Ctrl+Enter 发送"),
                new("off", "Enter 发送，Shift+Enter 换行"),
            ]) { Aliases = ["/ml"] },
            new("/follow-up", "设置运行期间 Enter 默认调整当前任务或排到下一轮", CopilotLocalCommandKind.FollowUpBehavior, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/follow-up [steer|queue]", Arguments:
            [
                new("steer", "Enter 调整当前运行，Tab 排到下一轮"),
                new("queue", "Enter 排到下一轮，Tab 调整当前运行"),
            ]),
            new("/retry", "重新生成当前会话最后一轮；refresh 会重新读取附件与网页上下文", CopilotLocalCommandKind.RetryResponse, AcceptsArguments: true, Usage: "/retry [refresh]", Arguments:
            [
                new("refresh", "重新读取本轮文件、图片与网页上下文后重试"),
            ]),
            new("/copy", "复制最近已完成的回答；可用 /copy 2 选择倒数第二条", CopilotLocalCommandKind.CopyResponse, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/copy [N]"),
            new("/export", "复制当前会话的可见 Markdown；可补充文件名并打开保存窗口", CopilotLocalCommandKind.ExportConversation, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/export [文件名.md|文件名.txt]"),
            new("/find", "查找并定位当前会话中的可见消息", CopilotLocalCommandKind.FindInConversation, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/find [文本]"),
            new("/model", "选择当前会话使用的模型 Profile；可补充 Profile 名或模型 ID", CopilotLocalCommandKind.SelectModel, AcceptsArguments: true, Usage: "/model [Profile 名|模型 ID]"),
            new("/reasoning", "选择当前模型 Profile 的推理强度；可补充受支持级别", CopilotLocalCommandKind.SelectReasoning, AcceptsArguments: true, Usage: "/reasoning [auto|off|on|high|max]") { Aliases = ["/effort"] },
            new("/personality", "设置当前会话后续回答的默认沟通风格", CopilotLocalCommandKind.SelectPersonality, AcceptsArguments: true, Usage: "/personality [friendly|pragmatic|none]", Arguments:
            [
                new("friendly", "友好协作，同时保持直接和证据优先"),
                new("pragmatic", "结果优先，简洁直接，只说明关键权衡"),
                new("none", "不附加会话级沟通风格"),
            ]),
            new("/clear", "清空当前上下文并开始新会话；可先命名旧会话", CopilotLocalCommandKind.ClearConversation, AcceptsArguments: true, Usage: "/clear [旧会话名称]") { Aliases = ["/new"] },
            new("/fork", "复制当前会话到新会话分支；Agent 运行时创建可见快照", CopilotLocalCommandKind.ForkConversation, AcceptsArguments: true, AvailableWhileAgentRuns: true, Usage: "/fork [新会话名称]") { Aliases = ["/branch"] },
        ];

        private static readonly Dictionary<string, CopilotLocalCommand> CommandsByName =
            BuildCommandLookup();

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
            if (!CommandsByName.TryGetValue(name, out var command)
                || (!command.AcceptsArguments && arguments.Length > 0))
                return null;

            return new CopilotLocalCommandInvocation(command, arguments)
            {
                InvokedName = name,
            };
        }

        public static IReadOnlyList<CopilotLocalCommand> Suggest(
            string? input,
            IReadOnlyList<CopilotAgentSkillCatalogItem>? skills = null,
            IReadOnlyList<CopilotProfileConfig>? profiles = null,
            CopilotProfileConfig? selectedProfile = null,
            CopilotLocalCommandComposerContext composerContext = CopilotLocalCommandComposerContext.Idle,
            CopilotConversationRecord? conversation = null)
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
                return SuggestArguments(
                    normalized,
                    separatorIndex,
                    profiles,
                    selectedProfile,
                    composerContext,
                    conversation);
            if (normalized.StartsWith('/') && FindExact(normalized) != null)
                return Array.Empty<CopilotLocalCommand>();

            var suggestions = normalized.StartsWith('/')
                ? Commands.Where(command =>
                    CopilotLocalCommandAvailabilityPolicy.CanSuggest(command, composerContext)
                    && command.Name.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                : Enumerable.Empty<CopilotLocalCommand>();
            var availableSkills = (skills ?? Array.Empty<CopilotAgentSkillCatalogItem>()).ToArray();
            var duplicateSkillNames = availableSkills
                .GroupBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var skillSuggestions = availableSkills
                .Select(skill => new CopilotLocalCommand(
                    normalized[0] + skill.Name,
                    BuildSkillSuggestionDescription(skill, duplicateSkillNames.Contains(skill.Name)),
                    CopilotLocalCommandKind.Skill,
                    AcceptsArguments: true,
                    Usage: normalized[0] + skill.Name + " [参数]")
                {
                    CompletionTextOverride = BuildSkillCompletionText(normalized[0], skill),
                    AgentSkillReference = CopilotAgentSkillReference.FromCatalogItem(skill),
                })
                .Where(command => command.Name.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(command.Name, normalized, StringComparison.OrdinalIgnoreCase));
            return suggestions
                .Concat(skillSuggestions)
                .DistinctBy(BuildSuggestionIdentity, StringComparer.OrdinalIgnoreCase)
                .Take(MaximumSuggestions)
                .ToArray();
        }

        private static string BuildSuggestionIdentity(CopilotLocalCommand command)
        {
            return command.Kind == CopilotLocalCommandKind.Skill
                && command.AgentSkillReference?.IsStructurallyValid() == true
                    ? command.Name + "\0" + command.AgentSkillReference.SkillFilePath
                    : command.Name;
        }

        private static string BuildSkillSuggestionDescription(
            CopilotAgentSkillCatalogItem skill,
            bool includeSource)
        {
            var displayName = string.IsNullOrWhiteSpace(skill.DisplayName) ? string.Empty : skill.DisplayName + " · ";
            var dependencies = skill.Dependencies.Count == 0 ? string.Empty : $" · 依赖 {skill.Dependencies.Count}";
            var source = includeSource ? " · 来源 " + BuildSkillSourceLabel(skill) : string.Empty;
            return "Skill · " + displayName + skill.EffectiveDescription + dependencies + source;
        }

        private static string BuildSkillSourceLabel(CopilotAgentSkillCatalogItem skill)
        {
            if (skill.SourceKind == CopilotAgentSkillSourceKind.User)
                return "用户";
            if (skill.SourceKind == CopilotAgentSkillSourceKind.BuiltIn)
                return "内置";

            try
            {
                var agentsDirectory = Directory.GetParent(skill.SearchRootPath)?.FullName;
                var projectDirectory = string.IsNullOrWhiteSpace(agentsDirectory)
                    ? null
                    : Directory.GetParent(agentsDirectory)?.FullName;
                var directoryName = string.IsNullOrWhiteSpace(projectDirectory)
                    ? string.Empty
                    : Path.GetFileName(projectDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                return string.IsNullOrWhiteSpace(directoryName) ? "项目" : "项目:" + directoryName;
            }
            catch
            {
                return "项目";
            }
        }

        private static string BuildSkillCompletionText(char prefix, CopilotAgentSkillCatalogItem skill)
        {
            var invocation = prefix + skill.Name;
            if (string.IsNullOrWhiteSpace(skill.DefaultPrompt))
                return invocation + " ";
            if (ContainsSkillInvocation(skill.DefaultPrompt, skill.Name))
                return skill.DefaultPrompt;
            return invocation + " " + skill.DefaultPrompt;
        }

        private static bool ContainsSkillInvocation(string text, string skillName)
        {
            return ContainsSkillInvocation(text, '$', skillName)
                || ContainsSkillInvocation(text, '/', skillName);
        }

        private static bool ContainsSkillInvocation(string text, char prefix, string skillName)
        {
            var invocation = prefix + skillName;
            var startIndex = 0;
            while (startIndex < text.Length)
            {
                var index = text.IndexOf(invocation, startIndex, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    return false;
                var endIndex = index + invocation.Length;
                if (endIndex == text.Length || text[endIndex] is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-'))
                    return true;
                startIndex = index + 1;
            }
            return false;
        }

        private static CopilotLocalCommand[] SuggestArguments(
            string input,
            int separatorIndex,
            IReadOnlyList<CopilotProfileConfig>? profiles,
            CopilotProfileConfig? selectedProfile,
            CopilotLocalCommandComposerContext composerContext,
            CopilotConversationRecord? conversation)
        {
            if (input[0] != '/')
                return Array.Empty<CopilotLocalCommand>();

            var name = input[..separatorIndex];
            CommandsByName.TryGetValue(name, out var command);
            if (command?.AcceptsArguments != true
                || !CopilotLocalCommandAvailabilityPolicy.CanSuggest(command, composerContext))
                return Array.Empty<CopilotLocalCommand>();

            var query = input[(separatorIndex + 1)..].TrimStart();
            if (command.Kind == CopilotLocalCommandKind.Help)
                query = query.TrimStart('/');
            var arguments = ResolveArguments(
                command,
                profiles,
                selectedProfile,
                conversation,
                query);
            return arguments
                .Where(argument => argument.Value.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .Where(argument => argument.AcceptsArguments
                    || !string.Equals(argument.Value, query, StringComparison.OrdinalIgnoreCase))
                .Take(MaximumSuggestions)
                .Select(argument => new CopilotLocalCommand(
                    name + " " + argument.Value,
                    "参数 · " + argument.Description,
                    command.Kind,
                    AcceptsArguments: argument.AcceptsArguments,
                    AvailableWhileAgentRuns: command.AvailableWhileAgentRuns,
                    Usage: command.Usage,
                    RequiresMoreInputAfterCompletion: argument.AcceptsArguments))
                .ToArray();
        }

        private static Dictionary<string, CopilotLocalCommand> BuildCommandLookup()
        {
            var lookup = new Dictionary<string, CopilotLocalCommand>(StringComparer.OrdinalIgnoreCase);
            foreach (var command in Commands)
            {
                AddCommandName(lookup, command.Name, command);
                foreach (var alias in command.Aliases)
                    AddCommandName(lookup, alias, command);
            }

            return lookup;
        }

        private static void AddCommandName(
            Dictionary<string, CopilotLocalCommand> lookup,
            string name,
            CopilotLocalCommand command)
        {
            if (string.IsNullOrWhiteSpace(name) || name[0] != '/')
                throw new InvalidOperationException($"Copilot command name '{name}' must begin with '/'.");
            if (!lookup.TryAdd(name, command))
                throw new InvalidOperationException($"Duplicate Copilot command name or alias '{name}'.");
        }

        private static IReadOnlyList<CopilotLocalCommandArgument> ResolveArguments(
            CopilotLocalCommand command,
            IReadOnlyList<CopilotProfileConfig>? profiles,
            CopilotProfileConfig? selectedProfile,
            CopilotConversationRecord? conversation,
            string query)
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
            if (command.Kind == CopilotLocalCommandKind.Subagents)
            {
                var separatorIndex = query.IndexOfAny([' ', '\t', '\r', '\n']);
                if (separatorIndex >= 0)
                {
                    var action = query[..separatorIndex];
                    return CopilotSubagentDiagnostics.BuildRunArguments(conversation, action);
                }
            }
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

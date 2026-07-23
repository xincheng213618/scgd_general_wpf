# Copilot 生命周期、预算与 Skills

每次工具调用都有稳定的 `CallId`、轮次、尝试次数/上限、运行时、工具名、读写级别、风险、审批模式、幂等性、并发模式、资源指纹、排队耗时、失败分类、是否允许重试、参数摘要、开始/结束时间、总耗时、超时、结果摘要和最终状态。状态包括 `Running`、`AwaitingApproval`、`Completed`、`Failed`、`TimedOut`、`Denied`、`Cancelled` 和 `Interrupted`。

## 多工具并发

Harness 创建后会显式启用 `FunctionInvokingChatClient.AllowConcurrentInvocation`，因此模型在同一响应内发出的多个函数调用可以由 Agent Framework 并行调度。业务工具不会直接无约束并发，而是统一进入 `CopilotToolExecutionGate`：

- 独立的 `SharedRead` 最多同时运行 4 个。
- 相同资源指纹的读取互斥，避免重复访问同一状态或缓存。
- `Exclusive` 调用会阻止新的读取，等待现有读取结束后全局独占；任何写工具都会被强制提升为 `Exclusive`，即使工具声明错误。
- 等待闸门时取消会产生 `Cancelled` trace 和审计记录，但不会调用工具。
- Agent 事件出口串行化，保证并行工具不会并发修改聊天状态。

框架的并行开关及其默认串行语义见 [FunctionInvokingChatClient.AllowConcurrentInvocation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.functioninvokingchatclient.allowconcurrentinvocation)。

## 上下文压缩与请求预算

Harness 不再关闭压缩。ColorVision 使用独立于模型 Profile 的 Agent 上下文窗口，默认 1,048,576 Token；当最大输出为默认 8,192 Token 时，单次输入预算为 1,040,384 Token。`ContextWindowCompactionStrategy` 在每次模型调用前执行框架原生两阶段策略：

1. 达到输入预算的 50% 后，先把旧工具调用组折叠成简短结果，保留工具名称和结论。
2. 达到输入预算的 80% 后，再删除最旧的非系统消息组，同时保留最近对话。

1M 是 ColorVision 新 Agent 配置的统一默认值，不从模型 Profile 推导；用户可以在独立 Agent 设置页收紧它，单次请求也可以覆盖。框架会从 `MaxContextWindowTokens - MaxOutputTokens` 计算输入预算；具体策略见 [ContextWindowCompactionStrategy](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai.compaction.contextwindowcompactionstrategy?view=agent-framework-dotnet-latest)。

`CopilotAgentRunBudget` 统一管理一次 Agent 运行的上下文窗口、累计请求 Token、业务工具调用、Agent pass 和总时长。这些参数保存在独立的 `CopilotConfig.AgentDefaults`，不再属于任何模型 Profile；Profile 只保留厂商、协议、端点、模型和生成参数。有效值按“单次请求覆盖 > 全局 Agent 默认值 > 框架安全默认值”解析。新默认值是 1,048,576 Token 上下文窗口、1,048,576 累计请求 Token、128 次业务工具调用、32 个 pass 和 7,200 秒；可配置硬上限分别是 1,048,576、1,048,576、512、128 和 86,400 秒，避免误循环变成真正无界运行。业务工具硬上限由 Bridge 独立执行，Harness 的 todo、mode、approval 等框架函数和最后一次自然语言总结使用单独的有界迭代余量，因此用完最后一次业务工具后仍可返回结论；只有继续越界调用工具时才记录 `ToolBudgetExhausted`。设置窗口把这些值放在独立 `Agent` 页，集成调用方仍可通过 `CopilotAgentRunBudgetOverride` 只收紧或覆盖当前请求。

Harness 正常结束但没有产生任何 `TextContent` 时，Runtime 不再把空 Todo 账本直接判为完成。它会通过同一 Token 与传输重试中间件发起一次非流式最终总结，`ChatOptions.Tools` 固定为空，并把有界工具观察与当前任务账本作为数据交给模型；因此这个阶段不能重放业务或 Framework 工具。总结仍为空或失败时会发出固定用户提示、记录 `IncompleteOutput` 与 `provider_empty_output` blocker，保存 checkpoint，并且绝不标记 `Completed`。

`CopilotAgentRecoveryMode.Finalize` 为这种状态以及“Provider 在任务已完成后断流”的状态提供独立恢复协议。Runtime 在验证 profile、checkpoint 和最后一次 `RunStopped` 后直接进入 no-tools Provider 调用，不发现外部 MCP、不创建 Harness、不恢复 Todo、不打开审批；不匹配的 Finalize 请求会在工具发现之前拒绝，不能降级成普通 Agent 执行。再次空输出或超时时会刷新原 session 的 journal 与对话记忆并继续保留 checkpoint；成功后旧 session checkpoint 会退役，因为旁路生成的最终回答并不存在于旧 Framework session 中，后续轮次应从包含新答案的可见历史创建新 session。

`CopilotTokenBudgetChatClient` 基于官方 `DelegatingChatClient` 中间件包装真实模型客户端，累计同一 Agent 请求内所有供应商调用的 usage。当已观测用量达到有效请求预算时，下一次供应商调用会被替换为确定性的结束响应，不会再次调用模型或重放工具。供应商不返回 usage 时使用字符数近似，并在诊断中标记 `includes estimates`。这个预算是跨调用循环闸门；单个供应商响应可能使最终统计略微超过阈值。

总时长由与调用方取消令牌链接的运行级计时器约束。超时或业务工具越界都会返回结构化 `BudgetExhausted` 结果，并在可能时先完成任务账本和 Session 检查点；用户主动暂停或取消的语义优先于同时发生的超时。最终 Token、供应商调用、工具调用、pass 上限、已用时长、是否使用估算以及具体预算耗尽类型都会作为 `RuntimeDiagnostic` 和 `CopilotAgentBudgetSnapshot` 写入执行记录。

## 原生任务账本与 plan/execute

Harness 的 `TodoProvider` 和 `AgentModeProvider` 现在作为标准运行时能力直接启用，不再由 ColorVision 维护第二套计划状态：

- 模型通过框架原生 `todos_add`、`todos_complete`、`todos_remove` 和查询工具维护任务；任务保存在 `AgentSessionStateBag`，随会话检查点一起持久化。
- 新会话默认进入 `execute`；模型在确实需要用户做关键选择时可切换到 `plan`。模式同样属于 Session 状态。
- `TodoCompletionLoopEvaluator` 只在 `execute` 模式驱动后续 Agent pass。只要还有未完成任务，Harness 会把剩余清单作为反馈再次调用 Agent；pass 数、工具调用、请求 Token 和总时长都使用当前请求解析后的统一运行预算。
- 每轮结束都会生成结构化 `CopilotAgentTaskLedgerSnapshot`，并把完成数、模式和最多三个未完成标题写入 `RuntimeDiagnostic`。聊天会话因此保留可检查的任务恢复记录，同时真实状态仍以 Framework Session 为唯一数据源。
- 从检查点恢复时，会在执行记录中明确标注恢复了多少任务。未完成只读任务可继续；持久任务只代表上下文和计划，不代表执行授权。
- Todo 状态更新本身由 Framework 对同一 Session 串行化，避免并发函数调用产生重复 ID 或丢失更新。

这里直接使用 Framework 的任务提供器与完成循环，而不是实现自定义 planner。官方 Harness 说明将持久 Todo、plan/execute 模式、逐次模型调用历史和可选完成循环列为完整 Agent 脚手架的组成部分，参见 [Agent Harnesses](https://learn.microsoft.com/en-us/agent-framework/agents/harness) 与 [TodoCompletionLoopEvaluator](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai.todocompletionloopevaluator?view=agent-framework-dotnet-latest)。

## 项目指令链

非 Chat 模式按当前解决方案根到活动文档目录的顺序发现项目指令。每个目录先检查 `AGENTS.override.md`，只有它不存在、为空或无法安全读取时才回退到同目录的 `AGENTS.md`；同一目录最多注入一份，越靠近活动文档的内容越晚出现。额外搜索根只提供其自身根指令，不会扫描与活动文档无关的任意子目录。重解析点文件或目录不会进入指令链。

单份原始文件最多有界读取 32,768 个字符，注入正文最多 12,000 个字符，全部正文合计最多 24,000 个字符和 4 份文档。宿主在序列化为作用域 JSONL 后再次施加 32,768 字符硬上限，因此路径、换行和反斜杠转义不能绕过正文预算。超限的最后一份文档会在保持完整 JSON 行的前提下缩短并标记 `IsTruncated`，不会截出损坏 JSON。

为减少只面向维护者的上下文噪声，文件中代码围栏之外的 `<!-- HTML comments -->` 会在脱敏和截断前剥离；围栏内的示例原样保留。项目指令始终作为 workspace-scoped user-role 数据注入，只影响其目录范围内的行为，不能授权写入、审批、外部副作用或越界访问。这结合了 [Codex AGENTS.md](https://learn.chatgpt.com/docs/agent-configuration/agents-md) 的根到当前目录和 override 语义，以及 [Claude Code memory](https://code.claude.com/docs/en/memory) 的就近作用域与维护者注释清理原则。

## Agent Skills

Harness 的 `AgentSkillsProvider` 作为标准能力启用，采用渐进式加载：模型先看到技能的名称和说明，只有当前任务匹配时才调用 `load_skill` 读取完整 `SKILL.md`，随后按需读取该技能目录中的参考资料。这样可以让 ColorVision 保存稳定、可复用的诊断流程，而不必把所有领域说明长期塞进系统提示。

技能只从受信任的应用或工作区目录发现：

- 当前解决方案搜索根下的 `.agents/skills/<skill-name>/SKILL.md`，适合项目级扩展和覆盖。
- 应用输出目录下的 `Copilot/Skills/<skill-name>/SKILL.md`，用于随 ColorVision 发布的内置技能。
- 不默认扫描用户主目录或任意外部路径；符号链接和重解析点目录不会加入技能源。

为避免项目 Skill 持续增长后放大每次模型调用的 L1 元数据成本，运行时在 Framework 完成一次文件发现后对候选项去重，并根据当前请求与 `name`、`description` 的中英文相关性选择最多 16 个活跃 Skill。Skill 名称和说明的合计预算按独立 Agent 上下文窗口的 2% 换算（以 4 字符约等于 1 Token 做保守估算），并保留 8,000 字符硬上限；当前默认 1,048,576 Token 窗口因此使用 8,000 字符硬上限，如用户收紧窗口，Skill 预算也会同步下降。预算不足时先按剩余候选数公平缩短 description，仍然过多时才省略低排名 Skill；缩短只影响初始目录，真正加载时仍读取完整 `SKILL.md`。用户显式点名的 Skill 具有最高优先级，同一运行的发现结果会被缓存。运行诊断会显示候选数、启用数、缩短说明数、预算省略数，以及真正被 `load_skill` 或参考资料读取路径加载的 Skill 名称。这与 Codex 初始 Skills 列表的 2% / 8,000 字符规则保持一致，而不是把 8,000 当作每次都应该占满的目标。

运行结束后，宿主只把“本轮被选择”、“本轮实际加载”和“连续入选但未加载”计数写入本地有界状态 `Copilot/State/skill-usage.json`。状态最多保留 128 个名称，单文件上限 1 MiB，损坏或超限时安全重建；不保存提示正文、Skill 内容或用户问题。连续至少 20 次被选择但未加载的 Skill 会自动降级为 explicit-only，不再占用后续请求的默认 Skill 元数据预算；这也能识别“历史上曾用过一次，但后来长期不再有效”的 Skill。用户在当前问题中以 `$skill-name` 或完整 Skill 名称直接点名时仍可加载；一次真实加载会把连续未加载计数清零，使该 Skill 重新参与隐式匹配，之后若再次连续低效仍会重新降级。Schema 1 的旧统计会保守迁移：从未加载的历史保留已有降级结论，无法推断连续性的已加载历史从零重新取样。独立 Agent 设置页显示选择次数、加载次数、加载率、连续未加载次数和当前历史降级状态。

Skill 作者也可以在同目录的 `agents/openai.yaml` 中设置 `policy.allow_implicit_invocation: false`，长期保持 explicit-only；用户直接点名时仍可使用。运行时只读取 Skill 目录内、不经过重解析点且不超过 32 KiB 的该策略文件。作者策略优先于历史统计，真实加载不会把作者声明改回隐式匹配。两类 explicit-only 都只是从默认上下文中移除，宿主不会自动删除 Skill 文件、执行脚本或扩大业务工具授权，因此清理是可逆且可审计的。

用户还可以在独立 Agent 设置页为具体 Skill 配置覆盖状态。`Auto` 使用作者策略和连续未加载证据；`Name only` 只向模型公开名称，用一个不可见的单字符 description 满足 Agent Framework 的非空校验，原说明和完整正文都不会进入初始目录；`Explicit only` 只允许用户直接点名；`Off` 即使被点名也不加入模型目录。覆盖按 Skill 名称保存到 `CopilotConfig.AgentDefaults.SkillOverrides`，与模型 Profile 无关；选择 Auto 会移除持久化覆盖。Name-only 可以用极低元数据成本重新观察历史低效 Skill，作者声明的 explicit-only 仍具有更高优先级。所有状态都只改变可见性，不删除或修改 `SKILL.md`，与 [Codex 禁用但不删除 Skill](https://learn.chatgpt.com/docs/build-skills) 及 [Claude Code skillOverrides](https://code.claude.com/docs/en/slash-commands) 的原则一致。

技能脚本发现与执行当前完全关闭。`load_skill` 和 `read_skill_resource` 是只读元数据操作，由 Framework 的只读规则自动批准，不在界面生成无意义的审批；技能内容本身不构成任何业务操作授权。所有 ColorVision 工具仍经过现有 Schema、风险级别、并发闸门、审计和写操作审批。

新增技能时，为目录创建 `SKILL.md`，并在 YAML frontmatter 中提供稳定的 `name` 和明确的 `description`。正文应描述何时使用、证据顺序、停止条件和安全边界；较长的清单或领域资料放进同目录的 `references/`，让 Agent 按需读取。内置的 `colorvision-flow-diagnostics` 是流程诊断示例；`colorvision-database-operations` 复用重置、导出和清理代码中的分类，把数据库组织为服务配置表、服务设置表和结果表，先验证实时 Schema，再指导通用 SQL 查询或经原生审批的数据清理。

这种结构遵循 [OpenAI Skills](https://learn.chatgpt.com/docs/build-skills) 与 [Claude Code Skills](https://code.claude.com/docs/en/slash-commands) 的渐进式披露语义，并直接使用 [Microsoft Agent Framework Agent Skills](https://learn.microsoft.com/en-us/agent-framework/agents/skills) 实现运行时加载。

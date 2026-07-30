# Copilot Agent 执行链

```text
CopilotToolRegistry
  -> CopilotAgentTaskHost 以单活动槽执行，跨会话 Agent 请求进入最多 3 项 FIFO 队列
  -> CopilotAgentExtensionBridge 合并当前已加载业务模块的上下文与工具
  -> 按请求筛选 CanHandle
  -> 官方 MCP C# SDK 按请求发现已配置的 Streamable HTTP 工具
  -> 外部工具适配成同一 ICopilotTool 契约并合并去重
  -> Agent Framework 选择工具
  -> TodoProvider 持久化多步骤任务，AgentModeProvider 管理 plan/execute
  -> ContextWindowCompactionStrategy 在每次模型调用前压缩上下文
  -> TokenBudgetChatClient 累计本请求模型用量并限制循环
  -> Always 审批工具由 ApprovalRequiredAIFunction 形成精确调用边界
  -> 按需确认时 Pending Actions 收集批准/拒绝/过期决定；临时授权仅允许路径和哈希绑定的工作区补丁/回滚自动批准
  -> 使用同一 AgentSession 回传 ToolApprovalResponseContent
  -> CopilotToolExecutor 运行前置 Hook
  -> 进入资源感知执行闸门（最多 4 个独立只读调用）
  -> 同资源调用互斥，写调用等待全部读调用并全局独占
  -> 发布 ToolStarted
  -> 在工具级超时和取消令牌下执行
  -> 返回 failure_kind / retry_allowed / attempt
  -> 仅当模型显式重试幂等工具的瞬时失败时允许第二次尝试
  -> 用户批准、拒绝或审批过期后回写同一 CallId
  -> 写入脱敏审计并运行后置 Hook
  -> 发布 ToolResult
  -> 将 observation 交回模型进入下一轮
  -> ExecutionContractLoopEvaluator 校验显式 URL / Web 搜索是否已有成功工具证据
  -> TodoCompletionLoopEvaluator 在 execute 模式下按本轮预算继续未完成任务
  -> 完成后序列化 AgentSession 到当前会话检查点
```

`CopilotAgentRuntimeRouter` 将配置完整的 OpenAI-compatible 和 Anthropic-compatible Profile 送入 Agent Framework。运行时不会在失败后自动切换执行器，也不会重放已经产生文本或工具调用的请求，避免写操作被重复执行。模型设置不暴露运行时开关。输入框的访问状态通过同一个可变 `CopilotAgentAccessContext` 进入 `CopilotTurnRequest`、`CopilotAgentRequest` 和正在运行的 Framework Session，但不会写入会话状态。`ConfirmProtectedActions` 继续创建 Pending Action；内部兼容枚举名 `FullAccess` 表示最长 15 分钟的临时授权，必须精确绑定 conversation、task 和当前 workspace，并且只有 `AllowsTemporaryFullAccess` 的工具才可进入自动批准。目前该集合严格限制为 `ApplyWorkspacePatchEnvelope` 与 `RollbackWorkspacePatchEnvelope`；模板、Flow、批量转换、Shell、菜单和数据库仍逐项确认。Review 模式、工具 Schema、意图策略、工作区范围、执行契约、并发闸门、超时和审计不受影响。启用临时授权不会批准已经等待的 Framework Action；任务结束、失败、取消、超时、工作区变化或应用重启都会撤销授权，新会话和 conversation branch 也不会继承。

`DesktopPetCopilotBridge` 订阅同一 `CopilotAgentTaskHost` 与 `CopilotMcpConfirmationStore`，但不再用最后一次事件覆盖单个会话 ID。`DesktopPetCopilotActivityTracker` 为每个会话保留有界活动，按 `NeedsInput -> Blocked -> Ready -> Running` 排序；完成、失败和暂停状态会留到用户打开对应会话，取消的排队任务会移除而不会抢走当前活动会话。宠物单击打开最高优先级活动，右键活动菜单最多展开八项；打开终态活动后再显示下一项。该状态投影只影响桌宠呈现和会话导航，不改变任务调度、审批或恢复语义，与 [Codex Pets](https://learn.chatgpt.com/docs/pets?surface=app) 的多聊天活动优先级保持一致。

直接 URL 请求还有确定性策略：`Auto` 模式会同时暴露 `FetchUrl` 与作为回退的 `WebSearch`，先读取原 URL，失败或证据不足时再搜索公开网页。Framework 原生 `LoopEvaluator` 上的执行契约会检查真实 step record；如果模型先写出答案却没有调用匹配工具，运行时撤回这段未支持草稿，并在同一 Session 中反馈缺失证据、要求下一轮调用工具。只有成功的 URL/搜索 observation 才满足契约；直接读取失败且仍有未尝试的搜索工具时继续回退，所有匹配路径都失败或模型仍拒绝调用时以 `Blocked` 和稳定 blocker code 结束，不再把模型文字当成已访问网页的证明。

`WebSearch` 在返回标题、摘要和 URL 的同时，从显式 URL 或 `site:` 查询提取目标主机，优先选择匹配结果，并通过同一 `FetchUrl` 实现深读；没有目标主机时只深读排名第一的安全结果。深读连同已确认的同源 JSON、RSS 和 Atom 最多读取三个资源，失败不会抹掉搜索线索，模型也不应重复读取已经成功深读的结果。工具结果压缩会分别保留搜索线索和深读正文。最终回答若使用成功的内置或外部网页工具却没有引用其返回的任何 URL，运行时会追加最多三个经过 http/https 校验的真实来源；已有有效引用、失败工具、普通问答、暂停和超时运行都不会触发补写。用户明确要求不访问网络时不触发该策略。

`Auto` 模式只要拥有当前解决方案的搜索根，就稳定提供 `SearchFiles`、`GrepText`、`ReadLocalFile` 和 `ListDirectory`。它们不再按当前句子的关键词裁剪；模型依据名称、描述和 JSON Schema 自主决定是否调用，因此普通概念问答仍可不执行搜索。搜索发现的根目录内文件可以在同一 Agent 运行中继续读取，不要求文件路径必须在发送问题前已经显式出现；读取和列目录仍做规范化根边界检查，并拒绝经重解析目录越界。公开网页/最新信息与直接 URL 仍分别控制 `WebSearch` 和 `FetchUrl` 的动态暴露。数据库、日志、流程统计、系统诊断和通用 Shell 同样属于稳定内置能力。外部 MCP 中名称或描述可识别为文件搜索、网页搜索和 URL 读取的工具仍服从对应意图门槛，其他设备、状态与业务工具继续按自身运行时可用性判断。

主 Agent 通过宿主管理的 `CopilotSubagentRoleCatalog` 选择专用只读角色。`DelegateExplore` 把范围较广或预计产生大量中间证据的多文件调查交给全新的只读 Harness Session；`DelegateScout` 处理需要查找、读取并综合多个公开来源的文档或依赖研究，简单单页读取仍直接使用 `FetchUrl` / `WebSearch`。模型可在同一个响应中发出最多两个互不依赖的角色调用；Framework 函数层并发执行，角色共享的协调器再用两个可取消的槽限制子运行，第三个调用等待槽位。不同任务使用不同只读资源键，重复任务继续服从现有同资源互斥与 no-progress 保护。

子 Agent 不继承父会话历史、附件、checkpoint、todo、mode、Skills、可写根、外部 MCP、访问模式或审批状态，也不会获得 Harness 自动注入的 `todo_*`、`mode_*` 或 `load_skill` 控制函数。`Explore` 只接收自包含调查任务、最多四个搜索根、活动文档和仍处于这些根内的项目指令；存在活动文档时优先保留其所在根，工具面固定为 `SearchFiles`、`GrepText`、`ReadLocalFile` 和 `ListDirectory`。`Scout` 只接收自包含外部研究任务，不接收任何本地根、活动文档或项目指令，工具面固定为 `WebSearch` 和 `FetchUrl`；网页内容始终按不可信证据处理。两者都不能调用 Shell、数据库、写工具、MCP、审批或再次委派。`Explore` 每次最多 8 次工具调用，`Scout` 最多 6 次；两者都是 2 个 Agent pass、90 秒、16,384 个请求 Token 和 12,000 字符返回内容，更小的父运行预算会同步收紧工具次数、pass、时长和 Token。

同一父请求的全部 `Explore` / `Scout` 运行共享一个委派 Token 池：总量通常为父请求预算的一半，最低 4,096、最高 32,768 Token；单个子运行按并发公平分配并至少需要 4,096 Token。成功后按真实用量结算，异常或取消时保守消耗已预留额度，预算不足时不再启动新的供应商调用。每个子运行生成带角色前缀的独立 `explore-*` / `scout-*` ID，父工具 trace 的 `CallId` 与角色、该 ID、停止原因、排队时间、工具次数和 Token 预算一起持久化；子运行内部的逐工具噪声仍不复制到主聊天。供应商调用数、Token 与估算用量继续归集进父运行预算，父 Agent 收到结果后仍需综合证据并完成最终回答。这一角色分工保持了 [OpenCode Agents](https://opencode.ai/docs/agents/) 中“主 Agent 选择带独立提示词和权限的 Explore / Scout”的核心语义；整体仍由 [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/) 提供 Agent、Session、工具与中间件执行基础，并保留 ColorVision 自己的预算、隔离和审批模型。

稳定的内置 Agent 能力不依赖上一轮关键词，因此“现在呢”“再检查一遍”等短追问仍能看到相同 Schema，并由模型结合会话历史重新发起结构化调用。动态或意图作用域工具仍可续租最近一轮真正成功执行过、只读、幂等且无需审批的能力；写工具和通用 Shell 不通过续租获得额外授权。checkpoint 恢复后也会用当前能力目录重新规划，不把旧调用参数当成授权。

执行契约比工具暴露策略更严格地区分“可能需要最新信息”和“用户明确要求搜索”：直接 URL、`Web` 模式以及“联网搜索 / search the web”等明确动作词才强制成功的网页证据。普通概念问题不会因为当前工具面中恰好存在 `WebSearch` 就增加模型轮次或产生失败搜索；没有匹配工具可用时也不会制造无意义循环，而是保留模型正常回答或说明能力边界的空间。

明确的工作区修改请求使用同一类执行契约：只有 `ApplyWorkspacePatch` 或 `ApplyCreateWorkspaceFile` 成功完成才算执行了对应修改，模型仅输出代码或声称“已经修改”不能满足契约。现有文件修改采用 `PreviewWorkspacePatch -> ApplyWorkspacePatch` 两阶段协议；预览只允许做一次精确且唯一的 `oldText/newText` 替换，并绑定当前文件 SHA-256。新文件采用 `PreviewCreateWorkspaceFile -> ApplyCreateWorkspaceFile`，只允许在当前解决方案可写根内创建白名单文本扩展，绝不覆盖已存在路径；缺失目录树先在同一父目录构造随机暂存树，再原子移动到首个缺失目录，避免与其他进程竞争时误认目录所有权。应用阶段必须使用原预览 ID、重新核对可写范围和冲突条件，并经 Agent Framework 原生审批。成功应用的两类预览都可由 `RollbackWorkspacePatch` 在再次审批且当前文件仍匹配应用后 SHA-256 时恢复原始字节或删除 Agent 创建的文件；只有 Agent 原子创建且回滚时仍为空的目录才会清理。预览只在当前进程中保留 30 分钟，不跨应用重启构成恢复授权。

普通 Chat 与进入 Framework 的可见对话历史统一经过可信窗口：只保留规范化的 `user` / `assistant` 角色，拒绝 `system`、工具和未知角色，防止历史数据提升为运行时指令。窗口不再使用固定的 8 条 / 32,000 字符旧限制，而是从独立 Agent 上下文配置和当前最大输出计算：约 50% 的输入空间分给可见历史，其余空间保留给系统提示、项目指令、Skills、工具 Schema、附件、运行时观察和输出。在默认 1,048,576 Token 上下文、8,192 Token 输出下，历史上限为 508 条 / 2,080,768 字符，单条最多 260,096 字符；32,768 Token 的最小上下文则自动收紧为约 12 条 / 49,152 字符。最终仍有 512 条和单条 262,144 字符的结构性上限，防止状态异常导致无界枚举或单条消息垄断上下文。

窗口始终保留最初用户目标和最近一轮，字符超限时优先删除完整的旧 `user -> assistant` 轮次，避免留下失去问题来源的孤立回复。无用户消息的异常历史只做有界截断，不会构造默认空目标。Chat 附件上下文占用一个独立槽位，并计入同一个自适应字符预算；`/context` 显示本轮实际解析出的条数、总字符和单条字符上限。

该窗口只收敛发给模型的历史，不删除本地完整会话，也不为每轮额外调用模型做摘要；请求预览会显示实际保留的消息数、字符数和原始规模。这样先用确定性窗口为其他上下文组成留出稳定余量，再由 Agent Framework 的 Token 压缩处理运行时消息，避免重复摘要成本。设计取向与 [Codex `/compact`](https://learn.chatgpt.com/docs/developer-commands.md?surface=cli) 保留关键点、释放上下文，以及 [Claude Code `/compact`](https://code.claude.com/docs/en/commands) 对长对话主动压缩的原则一致。Harness 指令仍由当前 Profile 和运行时单独提供。

输入框使用一个有界的本地 Slash 命令目录。输入 `/` 会显示全部候选，继续输入会按命令名前缀过滤；Tab 补全第一项，Enter 补全后直接执行，也可以点击任意候选。固定目录复用既有能力：`/status`、`/context`、`/permissions`、`/hooks`、`/skills`、`/mcp` 提供本地状态与诊断，`/diff`、`/compact`、`/review`、`/plan`、`/goal` 提供工作区和任务控制，`/resume`、`/rename`、`/new`、`/fork`（别名 `/branch`）提供会话导航与命名，`/btw` 提供旁路提问。Skill 还可以按 `$name` 或 `/name` 出现在同一补全目录中；固定候选和动态 Skill 候选合计最多 20 项，确保当前全部固定命令在裸 `/` 查询中可见。`CopilotComposerReferenceCatalog` 为输入框提供独立的 `@` 目录；既可直接键入，也可从 `+` 菜单在当前光标或选区位置插入，已有未闭合 mention 不会重复追加。它从 `TemplateControl.ITemplateNames` 的模板类型与已保存模板名、`CopilotMenuToolSupport` 和当前 solution root 的有界文件索引生成最多 12 个候选；空查询在模板、菜单和文件之间均衡取样，输入查询后按标题、路径和代码相关性排序。模板和菜单候选先在 UI 线程立即显示；文件扫描在后台最多索引 5,000 个受支持文件，跳过 `.git`、IDE 状态、依赖和构建输出目录，同一次未闭合 `@` 查询复用结果，每次新的 `@` 查询重新取样，因此刚创建的文件不会被长期缓存隐藏。仅等待文件结果或最终没有命中时，`@` 弹层分别显示“正在索引”和“未找到”状态；索引完成前 Enter 不会误发未完成查询，Tab 也不会误触发排队。选择文件会走既有附件边界；选择模板或菜单会生成带稳定 source ID 的 `CopilotContextItem`，保存模板引用只携带身份元数据，菜单上下文明确不构成执行请求。完成后的文本使用 `@[标题]` 闭合标记，防止继续被当作未完成查询。

本地状态、诊断和会话导航命令在 Profile 校验和任务调度之前执行，不写入会话消息或模型历史，因此未配置模型时仍可使用；`/review` 与 Skill 命令会按正常 Agent 请求进入任务调度。`/diff` 不调用模型、不会修改文件；`/compact` 只在没有可结构化恢复的 Agent 任务时运行，避免为压缩历史而静默丢弃 checkpoint。诊断结果只显示在可关闭的临时面板中；`/context` 不输出提示正文、完整文件路径或凭据，只显示受信项目根名称和指令文件的短标签；`/skills` 只读取有界的本地使用统计，`/mcp` 复用已有脱敏状态，`/new` 与界面上的新会话按钮保持同一语义。`/resume` 优先按精确 conversation ID，再按唯一完整标题直接切换；空查询、关键词、重名或无匹配复用并聚焦现有侧栏搜索。搜索框的上下键只移动预览候选，Enter 才确认，`Ctrl+R` 通过同一个 `RenameConversationCommand` 重命名高亮候选但不切换；两者都会先冲刷尚未触发的 180 毫秒筛选防抖，避免操作旧结果，Esc 清除并退出。`/rename [名称]` 复用相同的规范化、长度校验、持久化和筛选刷新路径；省略名称时打开既有命名窗口。手动命名会先取消该 conversation 的非阻塞标题生成，再设置 `HasCustomTitle`，而生成结果提交前仍检查生成实例身份和自定义标题，因此手动名称不会被迟到的模型结果覆盖。Agent 运行中允许 `/resume`、`/rename` 和搜索框 `Ctrl+R`，源任务不会因浏览、切换或命名而取消。`/permissions` 复用当前请求规划器、Capability Catalog 和外部 MCP 配置策略，分别显示搜索根、受信项目根、可写根、当前可写文件以及 `Never` / `Conditional` / `Always` 审批分组；它不主动发现远端工具，不输出 MCP 地址或 token 环境变量，也不会把项目指令、Skill 或历史消息解释成授权。Review 与 Diagnose 模式继续由工具注册表强制收敛为只读面，Chat 模式不启动 Agent 工具循环；其他模式仍在真正请求时按意图、运行时可用性和路径范围过滤。该边界沿用 [Codex sandbox 与 approval 的独立约束](https://learn.chatgpt.com/docs/agent-approvals-security.md) 和 [Claude Code permissions](https://code.claude.com/docs/en/permissions) 的可观察原则，而不引入第二套授权配置。命令匹配和结果格式集中在 `CopilotLocalCommandCatalog` 与现有诊断组件中。这个小目录采用 [Codex slash commands](https://learn.chatgpt.com/docs/developer-commands.md?surface=cli) 和 [Claude Code interactive mode](https://code.claude.com/docs/en/interactive-mode) 的“输入 `/` 展示、继续输入过滤”原则，但不照搬与 ColorVision 无关的命令。

# Copilot 任务、恢复与内置工具

## 任务 UI、停止原因与运行中 steering

成功的 Agent 轮次会把任务快照和结构化 `CopilotAgentStopReason` 写入对应的 Assistant 消息。聊天面板直接显示模式、完成数、任务标题/说明和停止原因。停止原因包括正常完成、等待用户、审批未通过、请求预算耗尽和本轮任务 pass 上限；这些字段随聊天状态持久化，状态 Schema 当前为 6。

只有最新 Assistant 消息且当前 Conversation 仍持有兼容 Session 检查点时，“继续”按钮才可用。点击后会创建一个正常的可见用户轮次，要求先复核当前状态；它不会从历史任务生成写授权。

运行中的补充要求走 Harness 自带的 `MessageInjectingChatClient`：

- Runtime 在活动 `AgentSession` 上注册短生命周期 steering context，结束或异常时自动移除。
- 用户在生成过程中输入内容并按 Enter/点击 `↳` 后，只以 `ChatRole.User` 入队，不允许客户端构造 system、assistant 或工具消息。
- 注入队列按 Session 隔离，并在线程安全的 `EnqueueMessages` 中等待下一个模型调用机会；立即停止仍使用原有取消令牌和方形停止按钮。
- steering 只改变模型后续决策，所有业务工具仍通过同一 Schema、预算、并发闸门和审批边界。

具体注入语义见官方 [MessageInjectingChatClient](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai.messageinjectingchatclient?view=agent-framework-dotnet-latest)。

## AgentSession 会话检查点

Runtime 使用 Harness 的 `ChatHistoryProvider.InvokedAsync` 正式持久化边界：首次创建 Session 时先保存一个安全点，此后每次成功的真实模型调用写入历史后，都通过 `SerializeSessionAsync` 增量序列化 `AgentSession`，连同最新 todo ledger、evidence 和 journal 原子保存到对应 `CopilotConversationRecord`。包装器内部仍委托 `InMemoryChatHistoryProvider`，并使用与 Harness 相同的 context-window compaction reducer，不维护第二份聊天历史。正常结束时再写入带最终 stop reason 的检查点。应用重启或重新创建 Runtime 后，下一轮使用 `DeserializeSessionAsync` 恢复框架内部历史；运行时将 checkpoint 的有界 conversation memory 与 UI 可见历史做有序对齐，只把 checkpoint 之后尚未持久化的可见消息插入当前用户消息之前，既避免重复整段历史，也覆盖异常退出、旧状态迁移或增量保存滞后造成的上下文缺口。对话记忆与可执行 Session 分开保存：Profile、能力或请求工具变化导致 Session 重建时恢复有界语义上下文，而 todo、历史工具调用和审批继续作废。该扩展点的调用顺序见官方 [Harness 文档](https://learn.microsoft.com/en-us/agent-framework/agents/harness)。

检查点具备以下约束：

- 使用 Profile ID、协议、Base URL、模型和系统提示的不可逆指纹做兼容性校验；配置变化后自动新建 Session。
- 单个检查点上限 4,000,000 字符，超限或 JSON 损坏时不恢复。
- 发起新请求时保留上一安全点，只有新的安全点成功保存后才替换；如果应用中途退出，启动归一化会把开放 run 标记为 `Interrupted`，由用户显式继续，不自动执行。
- Chat 模式和重新生成回答不会复用 Framework 检查点。
- 最新回复存在可用结构化恢复时，消息卡的通用“重试”和 `/compact` 都不会从头执行或清空检查点；用户必须选择“继续任务”“重试只读检查”“重试最终回答”或“重新规划”。真正需要从头开始时，先在任务列表中明确放弃旧任务。
- 工具 trace、幂等限制和写操作审批仍是独立安全边界；恢复 Session 不代表恢复任何旧批准。即使恢复清单要求重复同一个写调用，也会产生新的 CallId 和 Pending Action。

## 显式有界重试

模型供应商调用使用独立的传输重试层。HTTP 408、429、5xx、无响应连接错误、超时和 I/O 中断只有在尚未收到第一个流式更新时才会退避重试；默认最多请求三次，间隔 250ms、500ms。首个文本、usage 或 FunctionCall 更新一旦出现，后续中断绝不重放已有输出；若本轮已有文本或业务工具 step，Runtime 会记录 `ProviderFailure` 与 `provider_interrupted` blocker，并在不再次调用 Provider 的情况下读取 todo、合并 evidence/journal、序列化当前 Harness Session。开放 todo 通过 `Resume` 恢复同一 Session；没有开放 todo 时通过 `Finalize` 只生成最终回答。尚无任何实质进展的 400/401/403 等永久错误、连接失败和调用方取消仍直接向上传播。重试层位于 Token 预算层外侧，因此每次真实供应商请求都计入 `ProviderCalls`；诊断只记录失败类别、尝试序号和等待时间，不保存响应正文或异常消息。

运行时不会在工具内部暗中自动重跑。首次失败会把 `failure_kind`、`retry_allowed` 和 `attempt` 交回 Agent Framework；只有模型再次发出完全相同的调用时才触发重试。重试必须同时满足：

- 工具声明 `Idempotent`。
- 上次结果是 `Transient` 且状态为 `Failed` 或 `TimedOut`。
- 同参数最多执行两次，并且未超过本请求工具轮次上限。
- 受保护写工具每次重试都生成新的审批动作；上一次批准不会被复用。

`NonIdempotent`、`Unknown`、校验错误、权限拒绝、用户取消和业务失败都不可重试。这使失败恢复是可见、可审计的 Agent 决策，而不是无法观测的执行器副作用。

`CopilotToolExecutionAuditLogger` 保存最近 200 条调用并写入 log4net。参数摘要和错误会复用 MCP 的脱敏规则，不应记录 API key、token、密码、Authorization 或 bearer secret。聊天面板显示工具开始、完成状态和耗时，便于确认 Agent 是否真正执行了动作。未获得结果的文件、文档或网页搜索属于后台证据尝试，默认不显示活动行，也不会把整段处理状态标红；完整脱敏诊断仍保留在结构化 trace 中供恢复与排障使用。

工作区修改后的真实验证由 `RunWorkspaceValidation` 提供。它不是通用命令行：只接受工作区内现有 `.sln` / `.slnx` / 项目文件，以及精确的 `dotnet build` 或 `dotnet test`、`Debug` / `Release` 和 10–600 秒超时；执行参数由宿主固定拼装，始终附带 `--no-restore`，不经过 shell，也不接收额外参数。该操作会触发原生审批，因为项目 target 本身可能执行仓库代码。stdout/stderr 分别有界保留头尾，超时会终止进程树；非零退出作为已完成的失败验证证据交回模型，不会因工具层失败而自动重复。显式“修改并验证”请求由执行契约强制按“批准修改 -> 批准验证”的顺序完成，提前验证不能满足契约。

跨文件修改使用 `PreviewWorkspaceChangeSet`、`ApplyWorkspaceChangeSet` 和 `RollbackWorkspaceChangeSet`。模型先为 2–8 个不同路径分别生成精确的单文件修改或创建预览，再把这些 `previewId` 绑定成一个变更集；绑定后的子预览不能绕过变更集单独应用。审批窗口一次展示完整文件清单、每个操作以及前后 SHA-256。应用前先验证所有路径、状态和文件指纹，确认整组仍可写后才开始落盘；Windows 文件系统不提供跨文件事务，因此中途失败或取消时会按逆序补偿已经完成的写入。已成功应用的变更集可以通过一次新的原生审批整体回滚，回滚过程中若后续文件失败，则尽力重新应用先前已回滚的文件以维持原状态。预览和变更集都继承 30 分钟有效期。显式“修改多个文件”请求的执行契约只接受完整变更集成功，单个子文件成功不能冒充任务完成。

`PreviewWorkspacePatchEnvelope` 将这条链路收敛为一次结构化预览调用：同一信封可按顺序表达 1–8 个 `add`、`update`、`delete` 操作，每个路径只能出现一次。`add` 携带完整 UTF-8 内容，`update` 仍要求唯一精确匹配的 `oldText/newText`，`delete` 只接受可写工作区根内可解码的现有文本文件；单独授权的根外文件可以更新但不能删除，避免回滚时把原路径误当成新文件授权。预览阶段不写入，内部直接生成并保留同一变更集；`ApplyWorkspacePatchEnvelope` 经一次原生审批后复用整组路径检查、前置 SHA-256 验证、原子单文件写入和逆序补偿。删除记录绑定删除前字节与哈希，应用后目标必须缺失；`RollbackWorkspacePatchEnvelope` 仅在目标仍缺失时恢复原字节，若外部进程重新创建了同名路径则整组回滚在写入前失败，绝不覆盖。旧的逐文件预览与 `PreviewWorkspaceChangeSet` 保留为兼容入口，但 Agent instructions 和执行契约优先选择统一信封。

当前 Windows 版本、显示版本、Edition、安装类型、系统构建号与 UBR、系统/进程架构和 .NET 运行时由 `InspectWindowsSystem` 提供。工具无参数，直接使用 .NET 运行时信息和只读的 `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion`，注册表不可读时退回有界的运行时信息；它不启动进程、不接受命令文本，也不需要审批。该 Schema 与其他内置诊断稳定提供给模型，Windows 版本问题应优先使用它，不能改用 SQL 或应用日志猜测机器状态。

Git 工作树由 `InspectGitWorkingTree` 提供。它接受当前请求搜索/可写根目录内的可选路径，向上查找时不会越过宿主给出的根目录，也拒绝穿过根目录下的重解析点；因此子目录不能借 Git 仓库向外扩大可见范围。宿主只从 Program Files、用户本机 Git 安装目录或 GitHub Desktop 版本目录解析 `git.exe`，清除继承的 `GIT_DIR`、`GIT_WORK_TREE`、index/object/config parameter 等仓库选择变量，固定执行 `git --no-pager --no-optional-locks`、关闭 fsmonitor/untracked cache、把 `core.worktree` 绑定到已验证根目录，再运行 `status --porcelain=v2 --branch --untracked-files=normal --no-renames --ignore-submodules=all`；模型不能提供命令文本，也不能借宿主环境把目标切换到另一个仓库。结果有界返回 repository root、branch、HEAD、upstream、ahead/behind，以及 staged、unstaged、untracked、conflict 计数和最多 100 个路径；输出被进程层截断时会明确把 `status_complete` 和 `is_clean` 置为 false，避免把不完整观察误报为干净。由于 Git 在判断文件状态时仍可能评估仓库定义的 attributes 与外部 filter，而当前宿主没有操作系统级进程沙箱，该工具按受保护读取处理并要求 Agent Framework 原生审批；Git 不可用或目标不是仓库时返回真实 NotFound，不退回 Shell 猜测。

具体改动内容由 `InspectGitDiff` 提供。Agent 只能选择 `unstaged`、`staged` 或 `both`，以及当前请求根目录内的可选现存路径；宿主使用同一套可信 `git.exe`、仓库边界、重解析点和环境变量隔离，固定关闭 external diff、textconv、rename、submodule 与颜色输出，并把 pathspec 放在 `--` 之后，模型不能注入 Git 参数。每个范围最多返回 24,000 个字符，进程层或服务层发生截断时同时标记 `output_complete=false` 与 `patch_truncated=true`，因此 Agent 只能把它描述为有界摘录。补丁始终按不可信工作区数据处理，不能把其中的文字当作新指令；该工具与工作树状态一样，经原生审批后执行，不退回任意 Shell 命令。

单个 TCP 端口的常见检查由 `InspectTcpPort` 提供。例如模型可以根据“我想要知道 6666 端口有没有被占用”发出 `{"port":6666}` 的结构化调用；宿主执行固定的只读 PowerShell 诊断，不接受模型提供的命令文本，因此不需要审批。结果是有界的结构化数据：是否占用、绑定数量、本地/远程端点、连接状态、PID 和进程名；最多返回 64 条绑定并标记截断。工具 Schema 与通用 Shell 同时可见，模型应优先选择风险更低、参数更窄的专用工具；询问“如何检查端口”之类的概念问题则可以直接回答，宿主不会按关键词强制调用。

运行进程由 `InspectWindowsProcesses` 提供。模型可以传精确 PID、精确进程名（可省略 `.exe`），或请求按最近 CPU、工作集内存、名称、PID 排序的前 1–25 项；宿主在进程内通过 .NET API 采集，CPU 使用率来自 250ms 短采样并按逻辑处理器数量归一化，不启动 PowerShell/CMD，也不接受命令文本，因此无需审批。结果有界返回 PID、进程名、CPU、工作集、私有内存、线程、Session、启动时间；只有按 PID/名称聚焦查询时才读取可执行文件路径，Windows 拒绝访问的字段用 `null` 或空串明确表示。进程名和路径只是机器数据，不能作为 Agent 指令。

已安装 Windows 服务由 `InspectWindowsServices` 提供。模型可以用服务名或显示名的大小写无关片段筛选，也可按 `running`、`stopped`、`paused`、`pending` 状态过滤，并按服务名、显示名或状态排序；每次最多返回 50 项。宿主直接使用 .NET `ServiceController` 读取服务名、显示名、状态、服务类型和可停止/暂停/关机能力，不启动 PowerShell/CMD、不接受命令文本，也不需要审批。零匹配是有效 observation，可以支持“当前没有安装/运行匹配服务”的回答；服务名和显示名只作为机器数据返回，不能驱动新的 Agent 指令。

通用 Windows 命令由 `RunShellCommand` 提供，并与专用诊断工具一起稳定注册。模型需要在结构化参数中给出完整命令、`PowerShell` / `CMD` / `Auto`、可选现有工作目录和 5–600 秒超时；仅在回复文本中展示命令不会触发宿主执行。设置窗口的 `Default shell` 可选择“自动（PowerShell）”、`PowerShell` 或 `CMD`。宿主始终以无窗口、非交互方式运行命令，关闭标准输入，并有界返回真实 exit code、stdout、stderr 和耗时。根 Shell 会进入带 `KILL_ON_JOB_CLOSE` 的独立 Windows Job Object；正常完成、取消或超时时都会收敛其后台子进程，并在读取 stdout/stderr 前关闭后代继承的管道。由于当前宿主没有类似 Codex 的系统级文件沙箱，所有通用命令即使由模型选中也必须经过 Agent Framework 原生审批，审批内容显示 Shell、工作目录和完整命令；参数审计只保存字段名。普通概念问答可以直接回答，宿主不会根据“端口”“系统”等词强制调用 Shell。

当用户明确要求使用 CMD 或提供批处理语法时，仍按通用命令处理并进入原生审批，例如：

```bat
netstat -ano | findstr :6666
```

业务数据库同时提供语义快捷能力和通用 SQL 能力。`QueryFlowExecutionStats` 只读聚合 `t_scgd_measure_batch`：接受 `today`、`yesterday` 或 `last7days`，按本机时区生成左闭右开的日历范围，返回执行尝试总数、各 `FlowStatus` 数量、完成率和平均耗时。它适合“今天执行了多少次流程”这类常见问题，不要求模型了解表结构。

`QueryDatabaseSql` 是 Agent 模式的通用只读数据库工具，作为稳定 Schema 与其他内置能力一起提供；是否需要查询、应生成哪条 SQL 由模型结合当前对话决定，宿主不会通过关键词把系统问题改写成数据库查询。该工具接受一条只读 MySQL 语句，支持 `SELECT`、`SHOW`、`DESCRIBE`、`EXPLAIN`、`TABLE` 和最终落到只读语句的 CTE。默认最多返回 100 行，可在 1–500 行内调整；列数、单元格和总输出长度都有上限，密码、token、API key 等敏感列会统一显示为 `<redacted>`。`ExecuteDatabaseSql` 接受一条数据或结构变更，支持 `INSERT`、`UPDATE`、`DELETE`、`REPLACE`、`CREATE`、`ALTER`、`DROP`、`TRUNCATE` 和 `RENAME`，每次都必须经过 Agent Framework 原生审批；无 `WHERE` 的 `UPDATE` / `DELETE`、`TRUNCATE` 和 `DROP` 会在审批说明中给出加强警告。普通 DML 在事务内提交，DDL 遵循 MySQL 的隐式提交语义。服务设置表是版本托管的只读边界，即使进入审批也会在执行前拒绝变更；更新时由版本自带 SQL 重置原生设置。服务配置表由 Service Manager 在数据库重置前导出并回写，结果表不参与保留且可通过受审批的清理流程删除。

两个通用工具都只连接 ColorVision 当前配置的 MySQL，不接受连接字符串。解析层只允许单语句并拒绝 executable comment；账号与授权管理、创建/删除数据库、全局或会话设置、事务控制、锁、动态 SQL、存储过程调用、服务器关闭/终止、插件管理、文件导入导出以及延时函数不开放。审计只保存参数名和 SQL 指纹，错误结果不回显数据库异常或连接信息。只有宿主返回的真实 observation 才能支持当前数据库事实；但是否调用数据库工具仍由模型决定，宿主不在模型回答后用关键词补做查询。

数据库浏览器还提供独立的只读动态上下文，用于回答“当前这张表”“这次查询结果有多少行”“当前字段结构是什么”一类依赖软件界面的追问。它在每次相关 Agent 请求开始时重新读取最近激活的浏览器窗口，包含数据源类型、数据库/表名、表注释、存储引擎、分页与匹配行数、是否启用搜索、排序、未保存行数和最多 60 个字段的类型/主键/可空性摘要。表切换和分页刷新开始时先把旧页标记为不可用，成功后再发布新快照；切到非表节点、加载失败或窗口关闭时清除旧对象。该上下文不包含连接字符串、用户名、密码、SQL 文本、搜索词、默认值或任何单元格值；名称与注释仍经过统一内联脱敏。需要读取真实数据时继续由模型显式调用 `QueryDatabaseSql`，需要修改时仍走 `ExecuteDatabaseSql` 的原生审批，不把浏览器快照提升为隐式数据库权限。

检测结果历史与批次详情共用 `measurement-results` 动态来源。历史页只提供当前加载条数、是否启用筛选和选中批次的内部 ID、模板、状态、时间、归档状态；详情页额外提供取图/算法结果数量、失败与未知结果计数，以及当前选中结果的类型、内部 ID、结果码、耗时、时间和引用文件是否仍存在。页面导航、窗口激活、筛选、批次选择和结果选择都会刷新来源；导航离开或关闭最后一个结果页面后注销。批次 `Name/Code` 在实际流程中可承载序列号，因此一律不进入快照；文件路径、请求参数、原始结果消息、设备代码、算法 payload 和测量值同样不注入。正在运行的批次仍由 Flow 上下文负责，结果历史不会建立第二份运行状态。Flow 上下文中的批次序列号现在按字段级规则直接显示为 `<redacted>`，批次结果只报告消息是否存在，不再透传内容。

任务调度器使用 `scheduler` 来源读取 `QuartzSchedulerManager.TaskInfos` 的实时聚合，而不是解析窗口文本。明确询问计划任务时，即使任务窗口未打开，也会返回调度器启动状态、Ready/Running/Paused 数量、总执行/成功/失败次数，以及最多 30 个任务的有界目录；目录优先展示运行中、存在失败和暂停的任务，并包含任务/分组、状态、Job 类型、模式、优先级、执行统计、最后状态和下次触发时间，超出上限时明确标记截断。任务窗口激活后额外提供当前选择、超时、重复模式和前后触发时间。执行历史窗口只提供当前页、成功/失败筛选、行数、平均耗时和选中记录的时间/状态元数据。任务配置值、Cron 原文、`JobDataMap`、结果或异常详情、payload、路径与凭据全部留在宿主；最后执行消息和历史详情只报告是否存在。窗口关闭会释放动态会话并清除旧 Live Context，管理器级会话继续提供新鲜聚合快照。

项目专用结果视图不反向依赖主程序 Copilot，也不在 Copilot 核心增加客户项目分支。公共层只提供 `CopilotProjectResultContextSnapshot` 的低敏结果形状，具体项目程序集通过 `CopilotAgentExtensionRegistry` 自行注册和映射。新的业务快照与构建器放在按领域命名的 partial 文件中，不继续扩大单一 `CopilotBusinessContextBuilder.cs`。首个接入的 ARVRPro 使用 `project-arvr-pro-results` 来源覆盖模组检测结果列表、ObjectiveTestResult 历史和测试项明细；多个窗口由同一 `CopilotDynamicContextCoordinator` 跟随最近激活实例，最后一个窗口关闭后整个项目来源注销。列表提供加载/运行/完成/通过/失败数量以及选中结果的内部 ID、流程名、状态、耗时和时间；明细只提供测试项通过/失败数量和最多 20 个失败项名称。`ObjectiveTestItemCollector` 是项目内共享的纯解析器，窗口显示和 Agent 汇总不会各自解释一遍 JSON。SN、条码、文件路径、原始消息、原始 JSON、测量值、上下限和单位始终留在项目宿主内；是否存在图片、消息或结构化 payload 只以布尔元数据表示。其他项目需要接入时复用公共快照和注册协议，在自己的程序集内完成映射，不复制 ARVRPro 模型进公共层。

同一份生命周期数据还会以版本化 `CopilotAgentTraceEntry` 写入聊天会话。工具开始和结束事件都会触发原子状态保存；待确认动作还会把 `approval action_id` 与 Agent `CallId` 关联。批准、拒绝、过期、开始执行和执行结果都会更新同一条 trace。切换会话后仍能看到当前状态；若应用在执行或等待审批时退出，加载时会把遗留的 `Pending` / `Running` / `AwaitingApproval` trace 以及开放的 Agent run 收敛为 `Interrupted`，要求用户从最近安全点继续并为受保护调用产生新的审批，不会自动重放可能产生副作用的工具。结果、错误与参数在持久化前统一脱敏并限制长度。旧会话没有结构化 trace 时继续使用原有的 `ExecutionContent`，无需迁移才能打开。

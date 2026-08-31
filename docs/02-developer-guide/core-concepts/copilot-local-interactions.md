---
knowledge_id: "copilot.interactions"
knowledge_type: "topic"
status: "current"
summary: "Copilot 命令目录、输入与引用、会话导航及消息/桌宠呈现；本地入口不等于无副作用。"
aliases: ["Copilot 快捷键", "Copilot 输入框", "Slash 命令", "@关联", "公式显示", "桌宠活动", "CopilotLocalCommandCatalog", "CopilotPermissionCommand", "/permissions", "/context", "/queue", "/tasks", "/mention", "/multiline", "/follow-up"]
code_paths: ["ColorVision/Copilot/CopilotLocalCommandCatalog.cs","ColorVision/Copilot/CopilotLocalCommandAvailabilityPolicy.cs","ColorVision/Copilot/CopilotChatViewModel.LocalCommandWorkflows.cs","ColorVision/Copilot/CopilotChatViewModel.Composer.cs","ColorVision/Copilot/CopilotChatViewModel.ComposerReferences.cs","ColorVision/Copilot/CopilotChatViewModel.AttachmentCommands.cs","ColorVision/Copilot/Agent/CopilotWebPageToolSupport.cs","ColorVision/Copilot/Capabilities/CopilotBoundedHttpContentReader.cs","ColorVision/Copilot/CopilotChatViewModel.TurnExecution.cs","ColorVision/Copilot/CopilotChatViewModel.AttachmentLifecycle.cs","ColorVision/Copilot/Context/CopilotImageAttachmentAdmission.cs","ColorVision/Copilot/CopilotChatPanel.Composer.cs","ColorVision/Copilot/CopilotChatViewModel.DiagnosticsCommands.cs","ColorVision/Copilot/CopilotChatViewModel.QueuedFollowUps.cs","ColorVision/Copilot/Presentation","ColorVision/Copilot/State/CopilotComposerStash.cs","ColorVision/Copilot/CopilotKeyboardShortcutHelp.cs","ColorVision/Copilot/CopilotMarkdownMath.cs","ColorVision/Copilot/CopilotMarkdownView.xaml.cs","ColorVision/Copilot/CopilotComposerReferences.cs","ColorVision/Copilot/CopilotMarkdownView.SpecialContent.cs","ColorVision/FloatingBall/DesktopPetCopilotBridge.cs","ColorVision/FloatingBall/DesktopPetCopilotActivityTracker.cs","ColorVision/FloatingBallWindow.xaml.cs","ColorVision/Copilot/CopilotChatViewModel.ConversationDataCommands.cs","ColorVision/Copilot/CopilotChatViewModel.Messages.cs","ColorVision/Copilot/CopilotChatViewModel.RequestAdmission.cs","ColorVision/Copilot/CopilotChatViewModel.MessageInteraction.cs","ColorVision/Copilot/Context/CopilotImageInputBudget.cs","ColorVision/Copilot/Context/CopilotImageUnderstandingService.cs","ColorVision/Copilot/CopilotChatViewModel.WorkspaceCommands.cs"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotLocalCommandAvailabilityTests.cs","Test/ColorVision.Copilot.Tests/CopilotComposerSessionTests.cs","Test/ColorVision.Copilot.Tests/CopilotComposerPagingTests.cs","Test/ColorVision.Copilot.Tests/CopilotCodexMentionsV2FeatureTests.cs","Test/ColorVision.Copilot.Tests/CopilotChatViewModelProfileIsolationTests.cs","Test/ColorVision.Copilot.Tests/DesktopPetCopilotActivityTrackerTests.cs","Test/ColorVision.Copilot.Tests/CopilotMarkdownViewTests.cs","Test/ColorVision.Copilot.Tests/CopilotImageAttachmentAdmissionTests.cs","Test/ColorVision.Copilot.Tests/CopilotAttachmentPathTests.cs","Test/ColorVision.Copilot.Tests/CopilotConversationDeletionPersistenceTests.cs","Test/ColorVision.Copilot.Tests/CopilotRequestAdmissionLifetimeTests.cs","Test/ColorVision.Copilot.Tests/CopilotMessageEditAdmissionTests.cs","Test/ColorVision.Copilot.Tests/CopilotWebPageDeadlineTests.cs","Test/ColorVision.Copilot.Tests/CopilotWebPageAttachmentAdmissionTests.cs","Test/ColorVision.Copilot.Tests/CopilotAttachmentRemovalEditLifetimeTests.cs","Test/ColorVision.Copilot.Tests/CopilotImagePayloadValidationTests.cs","Test/ColorVision.Copilot.Tests/CopilotImageInputBudgetTests.cs"]
related: ["copilot.runtime", "copilot.configuration", "copilot.view-model", "copilot.lifecycle", "copilot.session-tools", "copilot.execution", "copilot.tool-contracts"]
---

# Copilot 输入、命令与活动呈现

本页对应 `CopilotChatPanel`、ViewModel 的命令路由、`Presentation` 投影和桌宠桥接。完整命令名、别名、参数与运行中可执行标记以 `CopilotLocalCommandCatalog` 为准；输入 `/help [命令]` 查询同一目录，不另维护一份手册式全量清单。

“本地命令”只说明由宿主解析，**不保证只读、不联网或不改变任务**。诊断、草稿、任务控制、模型请求及受保护操作必须按以下边界区分。

## 命令发现、补全与执行门禁

- `/` 列出固定命令及可用 Skill，继续输入按名称匹配；`$name` 和 `/name` 可引用 Skill。候选数由 `max(40, Commands.Length + 9)` 决定，不在文档冻结命令总数。
- `/help` 直接读取固定元数据；精确命令名允许省略开头的 `/`，不区分大小写。它不运行目标命令，也不读取动态 Skill 正文。
- 完整命令名后的空白进入参数补全。静态参数来自同一目录；模型、推理级别等动态项来自当前 Profile/会话。需要后续正文的中间候选（如 `/goal edit`）只插入参数和尾随空格，不直接执行。
- ↑↓ 环绕候选，Tab/点击只补全；Enter 对可提交候选完成补全后进入正常提交。右箭头补全要求无选择且光标已在文本末尾。候选、引用、历史搜索先于普通发送/历史导航处理，不能把一次补全当作用户发送。
- `CopilotLocalCommandAvailabilityPolicy.CanSuggest` 在 Idle 和 ActiveRun 都允许显示候选；当前实现**不会隐藏所有运行中不可执行的命令**。`CanExecute` 则只允许 Idle 或带 `AvailableWhileAgentRuns` 的 ActiveRun 命令。不能立即执行的本地命令可进入下一轮队列，由 `QueuedFollowUps` 的专用宿主路径处理，不冒充模型 steering。
- 待用户回答、排队视图、消息编辑或提示历史搜索会关闭相应目录。入口展示与最终执行校验是两层；看到候选不等于当下获准执行。

## Composer 与显式引用

发送或重试在异步保存图片之后重新验证捕获的 conversation：会话已删除、已归档、ViewModel 已释放或宿主已不再准入时，不创建消息、不清除 checkpoint，也不调用模型。发送路径在自动压缩返回后再次校验。单纯切换当前选中会话不会取消仍有效的原请求；请求继续使用原会话及其捕获上下文，不改写新会话的草稿。`CopilotRequestAdmissionLifetimeTests` 使用真实图片保存与受控 UI 续体验证这些边界；失效请求新生成的内容寻址图片留给已有孤儿清理，不抢删其他并发请求可能共用的文件。

发送前自动压缩也使用准入时捕获的会话、模型 Profile 和配置快照：图片准备期间切换会话或修改模型设置，不会把另一会话的历史发给摘要模型，也不会把摘要或压缩用量记到另一会话。压缩成功后，原请求重新捕获同一会话的压缩历史继续发送。手动 `/compact` 仍作用于执行命令时选中的会话；上述回归同时核验实际摘要 HTTP 请求、用量归属及后续请求历史。

图片入库与像素分析共用 `CopilotImagePayloadLoader` / `CopilotImageInputBudget`：除文件签名、尺寸和字节预算外，还要求 Skia 首帧像素解码返回 `Success`。截断或损坏的像素数据在整个附件批次写入前拒绝，也不能到达分析 Provider；缩放不能把部分解码结果重新编码成看似有效的图片。未缩放且成功解码的图片保留原字节，GIF 仍保留原动画数据；这不是逐帧或严格文件尾校验，像素已完整解码的 PNG 即使缺少 IEND 仍可接受。`CopilotImagePayloadValidationTests` 覆盖小图、大图缩放、混合批次和受控 Provider 请求，不修改用户源文件。

`/mention [查询]` 和 `+` 菜单只打开当前光标位置的 `@` 查询，不自行选择对象、建立附件或提交请求。查询限定单行、最多80字符；候选最多12项，文件索引最多5,000项并跳过依赖/构建目录，同一未闭合 mention 复用结果，新 mention 重新取样。异步结果由会话键、版本和取消状态约束；索引中 Enter/Tab 都被引用层消费；索引结束且无候选时 Tab 仍被消费，但 Enter 会继续进入正常提交链，不能保证未闭合的查询文本不会发送。

`CopilotComposerReferenceCatalog` 与 `ComposerReferences` 决定候选和完成动作：

| 类型 | 选择后形成的输入 | 不代表什么 |
| --- | --- | --- |
| 文件/图像 | 走既有附件解析、去重与范围边界 | 不是授予整个磁盘访问权 |
| 模板/菜单 | 带稳定 source ID 的结构化上下文，正文用闭合 `@[标题]` 标记 | 引用菜单不等于执行菜单；保存模板身份不等于已取得全部参数 |
| Skill | 校验结构化 Skill 引用，写入待选引用与完成文本 | 不在补全时执行 Skill，不升级工具权限 |

`ConfiguredMentionsV2Enabled` 开启时统一目录可包含 Skill、模板、菜单和文件；关闭时保留文件候选，不应声称所有类型始终可选。模板类型/保存项的实际读取由共享能力 `InspectTemplateType` / `InspectSavedTemplate` 负责，见[工具契约](./copilot-agent-tool-contracts.md)；业务实时上下文见[模块扩展](./copilot-agent-extensions.md)。

显式附件或上下文可能含用户原始数据；发送前检查选中的内容，凭据加密和局部脱敏不等于所有附件都已净化。

网页附件与 `fetch_url` 共用 `CopilotWebPageToolSupport`。一次加载使用同一个 20 秒超时，覆盖地址解析、重定向请求和完整正文读取；收到响应头不会解除正文的超时保护，跳转也不会重新计时。调用方取消仍可提前停止，失败时释放响应与传输对象。`CopilotWebPageDeadlineTests` 使用内存 HTTP handler 和受控正文流，验证默认超时、调用方取消、成功及 HTTP 错误，不访问真实站点。

刷新同来源（未指定来源时按同标题匹配）的上下文附件时，会替换草稿中的附件快照，不原地改写已被发送请求捕获的对象。图片保存等异步准入尚未结束时，新上下文仍留在草稿，当前请求继续使用提交时的旧内容；未更新的原附件正常消费，新增来源的附件也保留。`CopilotRequestAdmissionLifetimeTests` 通过真实外部上下文入口和图片保存续体验证这些边界，不因更新附件而补发第二个请求。

同地址的网页附件刷新也替换草稿中的快照：较早开始的发送继续使用原网页内容，不能消费后来刷新的网页。编辑消息期间开始的网页读取绑定本次编辑，等待时取消编辑后，完成结果不会附加到恢复后的草稿；刷新时原网页已不在附件集合，也不重新回填。停止读取或关闭 ViewModel 后不附加迟到结果。`CopilotWebPageAttachmentAdmissionTests` 通过实际 ViewModel 的网页加载流程、可控加载结果和真实图片准入验证，不打开网页输入对话框或访问网络。

剪贴板图片在后台编码为托管 PNG 后，须在原会话仍存在且操作未取消时才加入附件。取消或关闭发生在编码完成与 UI 续体执行之间时，也会清理本次尚未附加的图片；后台尚未结束则在完成后清理，失败继续被观察。清理使用已有根内路径与重解析点检查，不删除先前附件，也不改变草稿。`CopilotChatViewModelProfileIsolationTests` 用冻结的合成图片和受控 STA 续体覆盖正常附加、取消与关闭，不读取系统剪贴板。

剪贴板保存和 `CopilotImageAttachmentAdmission` 的内容寻址保存共用 `PrepareStorageDirectory`：创建目录前后都检查目录及祖先的 reparse point，复用已有图片前也检查目标文件路径，不向符号链接或目录联接指向的位置写入附件。普通图片准入按 `Storage` 失败报告，保留原始输入供重试；读取端已有的拒绝链接路径规则保持不变。回归使用临时目录链接验证拒绝时不会在目标侧创建目录或文件，并保留普通保存与去重检查；这些路径检查不保证对检查后发生的并发文件系统替换提供原子隔离。

发送按键还取决于本地偏好：标准模式 Enter 提交、Shift+Enter 换行；`/multiline` 开启后 Enter 换行、Shift+Enter 提交。运行中 `/follow-up steer|queue` 选择默认提交是调整当前任务还是排到下一轮，Tab 使用另一种行为；Ctrl+Enter 的立即接管先登记下一轮，再请求取消当前轮，等待当前轮收尾后运行；调度失败不会先取消当前任务。补全弹层优先消费按键。任务暂停、取消和工具是否真实静止仍以[任务与恢复](./copilot-agent-session-and-tools.md)为准，不由按键返回证明。

## 草稿编辑与历史恢复

编辑最新消息后发送，会捕获本次编辑会话的身份与原 user / assistant 对象。图片保存等待期间取消编辑、重新打开同一消息、切换会话后编辑其他消息，或由较新的发送替换原轮次，都会使旧发送退出；它不能替换历史、清除 checkpoint 或结束后来开始的编辑。同一次编辑中继续输入新草稿仍允许提交已捕获的旧文本，新文本和新增附件留在草稿。`CopilotMessageEditAdmissionTests` 用真实图片保存与受控 UI 续体验证上述边界；有效编辑本身不触发自动压缩。

`Ctrl+E` 打开本机 `CopilotTextInputWindow`，不启动 `$EDITOR` 或创建第二套 composer。`CopilotComposerEditorSnapshot` 限制 UTF-16 安全文本长度并夹紧光标，不 trim 首尾空白；确认才写回，取消只恢复焦点/光标。

`Ctrl+S` 用 `CopilotComposerStash` 捕获当前会话文本、光标、附件与一次性请求状态：非空输入且没有 stash 才捕获并清空；空输入才恢复并消费；已有 stash 不被新非空输入覆盖。不触发发送，不保存临时授权。stash 随 chat-state 持久化，其附件计入引用与孤儿清理，不能按“当前消息为空”当成可丢弃内容。

`/history` 与输入框 Ctrl+R 搜索可见 user `Content`，默认当前会话；弹层内 Ctrl+S 切换全部本地会话。按时间排序、去重并限长预览，选中仍恢复完整可见请求；不读隐藏 `RequestContent`、附件正文或 trace。Enter/Tab/点击只把选中项放回草稿，Esc 恢复打开前草稿，不发送。历史搜索独占这些按键，因此弹层内 Ctrl+S 不操作 stash；侧栏搜索的 Ctrl+R 则是重命名候选，不是提示历史。

## 会话导航、回顾与出口

附件移除的失败回滚也区分编辑状态：移除发起于某次消息编辑，而该编辑已取消或重新开始时，不把原历史附件插入后来的草稿。普通草稿等待保存期间重新附加同来源（无来源时按标题匹配）的上下文后，失败不再恢复旧版本形成重复附件；不同来源仍独立保留。编辑未结束时仍回滚到原位置，普通草稿单纯切换会话仍恢复原会话附件，不改写新会话。`CopilotAttachmentRemovalEditLifetimeTests` 用实际磁盘状态、受控保存失败和命令入口验证这些边界，并检查历史图片未被删除。

永久删除仍需原生确认；确认框返回后再次检查运行、后台命令和保留状态，不能依据打开确认框之前的状态继续删除。`DeleteConfirmedConversationAsync` 先保存不含目标会话的快照，等待既有持久化屏障成功，才清理输出档案并尝试删除独占托管附件；保存失败会恢复原会话、恢复记录及原选择，不报告删除成功。草稿附件移除也先等待引用变化落盘，失败时恢复附件，避免重启后旧消息或草稿指向已删除图片。清理仍检查其他会话、stash、steering 和队列引用，并保留现有根内路径／重解析点限制。`CopilotConversationDeletionPersistenceTests` 覆盖挂起保存、失败回滚、共享引用及确认后状态变化；不代表修复旧版本已经丢失的图片，也不承诺操作系统拒绝删除的文件会立即消失。

| 入口 / 实现 | 当前边界 |
| --- | --- |
| `/recap` / `CopilotConversationRecap` | UI 线程生成当前会话的可见目标、最近一轮、任务状态和草稿/附件数量报告；最近回答只取最新 user 之后，预览有界。不读隐藏请求、reasoning、trace、附件正文或草稿正文，不调用模型 |
| `/view-plan` / `CopilotConversationPlanNavigation` | 倒序定位最近 `HasCompletedPlan` 的原消息。进行中、中断、截断、非 Completed 或非 Plan 的回答不是目标；导航不生成、批准或执行计划 |
| `/turn N` / `CopilotConversationTurnNavigation` | 最新可见 user 为1，仅滚动到同一消息；不分支、不恢复草稿、不撤销操作 |
| `/find` / `CopilotConversationFindNavigator` | 当前会话可见正文、可展开活动、附件显示元数据的本地匹配；不读取隐藏请求、附件正文或外部文件。Ctrl+F 会话内，Ctrl+G 跨会话 |
| `/resume`、`/rename` | 精确ID或唯一完整标题可直接切换，否则进入侧栏搜索；搜索预览不立即切换。手动命名取消标题生成且提交时复查身份，避免迟到结果覆盖 |
| `/clear`（`/new`） | 保留旧会话并创建干净上下文；可先命名旧会话。不删除旧记录、不继承 checkpoint 或临时授权，也不是 `/compact` |
| `/fork`（`/branch`）、`/rewind N` | 会话快照/分支，不是文件系统回滚。`rewind` 复制目标请求之前的完整历史并恢复该请求供编辑，不自动发送；不继承可执行 checkpoint、临时授权或回滚能力 |
| `/archive`、`/unarchive`、`/delete` | 归档隐藏与永久删除不同；删除经过状态检查与原生确认，不能把“本地会话命令”当作可无确认清理 |
| `/copy N`、Ctrl+Shift+C | 从最近开始选择有正文、非活动、非中断且非 display-only 的回答；部分流式回答不遮住上一条稳定回答，使用既有正文剪贴板格式；不再占用打开文件的 Ctrl+O |
| `/export [文件名]` | 无参数复制可见 Markdown，有文件名则预填保存对话框，由用户选目录及覆盖；使用同目录临时文件与原子替换 |
| `/feedback [说明]` | 打开现有 FeedbackWindow，附上有界可见会话快照；用户仍需选择诊断内容并显式 Send，打开窗口不等于上传 |

导出和反馈共用 `CopilotConversationMarkdownExporter` 的 UI 快照：只包含已完成且有可见正文/附件引用的消息，排除活动回答、隐藏请求、reasoning、execution trace、附件正文和 composer 草稿。反馈说明最多4,000字符，会话附件最多200,000字符/最近50条已完成消息，单条和附件字段仍有独立上限；反馈临时附件随窗口关闭清理；该附件仍可能含用户实际可见内容，发送前应审阅。

`/usage`（`/stats`）展示本地已记录用量与运行诊断；daily/weekly/cumulative 不是账户账单或价格计算。消息用量持久化和分支去重见[执行链](./copilot-agent-execution.md)，不因查询统计再请求模型。

## 诊断、设置与任务控制不是同一种命令

- `/status`、`/doctor`、`/debug-config`、`/context`、`/hooks`、`/mcp [verbose]` 读取已有本地状态或健康快照并脱敏展示；`/doctor` 不替用户联网测试或自动修复，`/mcp` 不等于 Refresh Discovery。
- `/settings` 打开配置；`/model`、`/reasoning`（`/effort`）选择现有 Profile/受支持级别，不另建一套配置。落盘与运行态发布失败要分开判断，见[配置契约](./copilot-configuration.md)。`/personality` 是当前会话后续回答的沟通风格，不修改工具权限。
- `/permissions` 打开同一盾牌菜单，`status` 展示范围/能力/审批策略；`ask|auto` 修改任务绑定的访问状态。`/approve` 包含原生待确认动作和自动审查拒绝后的精确重试入口，具体授权、过期和复核边界见[执行链](./copilot-agent-execution.md)。
- 裸 `/tasks` 查看活动/队列和可恢复项；`stop N`、`resume N`、`dismiss N` 分别进入停止、恢复、放弃路径。stop/dismiss 有原生确认，resume 重新评估 checkpoint/能力兼容后才提交；Ctrl+Alt+T 只是折叠同一任务列表，不做这些操作，不再占用常见的新标签 Ctrl+T。
- 裸 `/queue` 查看当前会话条目，编号是当时的全局队列位置，不是稳定ID。当前实现还有 `send|edit|up|down|delete N` 和 `clear`：send 提升下一项并请求停止当前任务；edit 取消排队并恢复输入/附件但不发送；delete 取消且可能暂停绑定目标；clear 经确认只清当前会话等待项。编号在命令执行时按当前队列重新解析，稍早看到的同一编号可能已对应同会话另一项，不能把数字当稳定身份。解析到对象后才由 Host 状态复查拒绝已开始或已离队对象；清空确认期间开始执行的项会被跳过，不把清理等待项变成停止当前任务，原子取消边界见[后续队列](./copilot-agent-session-and-tools.md#任务-ui、停止原因、运行中-steering-与后续队列)。
- `/ps` 是 Copilot 后台命令登记表入口，stop 需确认；不是系统所有进程列表。`/agents` 的只读目录与 steer/stop 等控制子命令也须区分，不可整体标成只读。
- `/init`、`/review`、`/verify`、`/plan`、`/compact` 和 Skill 可能进入真实模型/工具流程；`/rollback N` 会创建精确文件回滚审批。它们不能因为以 Slash 开头就绕过范围、预算、确认或执行证据。项目指令与 Skills 见[生命周期](./copilot-agent-lifecycle.md)，文件修改见[任务与内置工具](./copilot-agent-session-and-tools.md)。

## 消息显示与桌宠活动

输入框通过 `ApplicationCommands.Save` 接入保存路由：普通状态暂存/恢复草稿，历史搜索打开时仍切换搜索范围。主窗口内默认 Ctrl+S，跟随应用保存快捷键修改或清空；没有 ViewModel 时拒绝并终止路由，不能误保存其它文档。面板内 Ctrl+F 继续查找会话，主窗口功能搜索使用 Ctrl+Shift+P。

`/transcript expand|collapse` 只修改当前会话已有 `HasThinkingTrace` 消息的 `IsThinkingExpanded`；无参数时按是否有收起项选择全展开/全收起，非法参数不改变状态。实际变化才请求已有状态持久化，不创造或导出新的隐藏推理。`/compact-mode` 只改变消息间距，不压缩模型上下文；`/timestamps` 只控制时间展示。

公式由 `CopilotMarkdownMath` 识别、`CopilotMarkdownView.SpecialContent` 使用 WpfMath 渲染：行内支持 `$...$` 与 `\(...\)`，独立/多行块支持 `$$...$$` 与 `\[...\]`。行内代码跳过行内公式识别，解析/渲染失败回退文本；块公式经过 trim/拼接，不保证逐字节还原。`CopilotMarkdownView.xaml.cs` 在三反引号围栏内优先收集代码，不进行块公式识别；其中已闭合或未闭合的公式分隔符都保留在代码正文及复制内容中，不吞掉结束围栏后的正文。围栏外公式仍按原路径渲染。公式显示不是模型执行数学验证的证据。

桌宠启用 `EnableCopilotIntegration` 后，`DesktopPetCopilotBridge` 从 `CopilotAgentTaskHost` 和 `CopilotMcpConfirmationStore` 投影多会话活动，Tracker 最多保留16项，优先级为 `NeedsInput → Blocked → Ready → Running`。徽标显示活动数量；单击打开最高优先级，右键活动菜单展示有界列表。打开 Ready/Blocked 项后移除其待查看状态，再展示下一项；NeedsInput 不因打开页面就消除。取消排队项不会覆盖正在运行会话的导航目标。

桌宠确认卡仍经过同一 confirmation decision 和上下文复查；点击批准会先展示原生确认内容，不由活动图标授予执行权。桌宠投影不改变调度、工具审批或恢复语义。

## 验证范围

元数据中的测试覆盖命令展示/执行分离、composer 状态、分页、mentions_v2 门控、部分 ViewModel 交互及桌宠优先级。`CopilotMarkdownViewTests` 在无窗口 STA 线程检查文档树中的代码正文、复制载荷与围栏后的段落／公式，覆盖两种块公式分隔符的单行、多行及未闭合内容。测试路径存在不代表本次已运行；公式实际字体布局、系统剪贴板/保存窗口、真实焦点和桌宠确认窗口仍需要对应 WPF 验证。修改命令时同时核对 Catalog、AvailabilityPolicy、ViewModel handler 和 Panel 键盘路由，不只更新一处帮助文案。

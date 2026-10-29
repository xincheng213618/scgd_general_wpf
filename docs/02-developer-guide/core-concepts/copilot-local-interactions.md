---
knowledge_id: "copilot.interactions"
knowledge_type: "topic"
status: "current"
summary: "Copilot 命令目录、输入与引用、会话导航及消息/桌宠呈现；本地入口不等于无副作用。"
aliases: ["Copilot 快捷键", "Copilot 输入框", "Slash 命令", "@关联", "公式显示", "桌宠活动", "CopilotLocalCommandCatalog", "CopilotPermissionCommand", "/permissions", "/context", "/queue", "/tasks", "/mention", "/multiline", "/follow-up"]
code_paths: ["ColorVision/Copilot/Agent/CopilotWebPageBrowserRenderer.cs","ColorVision/Copilot/Agent/CopilotWebPageToolSupport.Rendering.cs","ColorVision/Copilot/CopilotChatPanel.xaml.cs","ColorVision/Copilot/CopilotChatViewModel.Conversations.cs","ColorVision/Copilot/CopilotLocalCommandCatalog.cs","ColorVision/Copilot/CopilotLocalCommandAvailabilityPolicy.cs","ColorVision/Copilot/CopilotChatViewModel.LocalCommandWorkflows.cs","ColorVision/Copilot/CopilotChatViewModel.Composer.cs","ColorVision/Copilot/CopilotChatViewModel.PromptHistory.cs","ColorVision/Copilot/CopilotChatViewModel.ComposerReferences.cs","ColorVision/Copilot/CopilotChatViewModel.AttachmentCommands.cs","ColorVision/Copilot/Agent/CopilotWebPageToolSupport.cs","ColorVision/Copilot/Capabilities/CopilotBoundedHttpContentReader.cs","ColorVision/Copilot/CopilotChatViewModel.TurnExecution.cs","ColorVision/Copilot/CopilotChatViewModel.AttachmentLifecycle.cs","ColorVision/Copilot/Context/CopilotImageAttachmentAdmission.cs","ColorVision/Copilot/CopilotChatPanel.Composer.cs","ColorVision/Copilot/CopilotTextInputWindow.xaml","ColorVision/Copilot/CopilotTextInputWindow.xaml.cs","ColorVision/Copilot/State/CopilotComposerEditorSnapshot.cs","ColorVision/Copilot/CopilotChatViewModel.DiagnosticsCommands.cs","ColorVision/Copilot/CopilotChatViewModel.QueuedFollowUps.cs","ColorVision/Copilot/Presentation","ColorVision/Copilot/State/CopilotComposerStash.cs","ColorVision/Copilot/CopilotKeyboardShortcutHelp.cs","ColorVision/Copilot/CopilotMarkdownMath.cs","ColorVision/Copilot/CopilotMarkdownView.xaml.cs","ColorVision/Copilot/CopilotMarkdownTable.cs","ColorVision/Copilot/CopilotComposerReferences.cs","ColorVision/Copilot/CopilotMarkdownView.SpecialContent.cs","ColorVision/FloatingBall/DesktopPetCopilotBridge.cs","ColorVision/FloatingBall/DesktopPetCopilotActivityTracker.cs","ColorVision/FloatingBallWindow.xaml.cs","ColorVision/Copilot/CopilotChatViewModel.ConversationDataCommands.cs","ColorVision/Copilot/CopilotChatViewModel.Messages.cs","ColorVision/Copilot/CopilotChatViewModel.RequestAdmission.cs","ColorVision/Copilot/CopilotChatViewModel.MessageInteraction.cs","ColorVision/Copilot/Context/CopilotImageInputBudget.cs","ColorVision/Copilot/Context/CopilotImageUnderstandingService.cs","ColorVision/Copilot/CopilotChatViewModel.WorkspaceCommands.cs","ColorVision/Copilot/CopilotLocalGitDiffService.cs","ColorVision/Copilot/CopilotChatPanel.xaml","ColorVision/Copilot/CopilotChatPanel.AttachmentsAndScroll.cs","ColorVision/Copilot/CopilotChatViewModel.ComposerAndRuntimeState.cs","ColorVision/Copilot/CopilotChatViewModel.cs"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotWebPageRenderingTests.cs","Test/ColorVision.Copilot.Tests/CopilotPromptHistorySearchBehaviorTests.cs","Test/ColorVision.Copilot.Tests/CopilotGitDiffFeedbackTests.cs","Test/ColorVision.Copilot.Tests/CopilotLocalGitDiffConsistencyTests.cs","Test/ColorVision.Copilot.Tests/CopilotBackgroundStopFeedbackTests.cs","Test/ColorVision.Copilot.Tests/CopilotLocalCommandAvailabilityTests.cs","Test/ColorVision.Copilot.Tests/CopilotComposerSessionTests.cs","Test/ColorVision.Copilot.Tests/CopilotExpandedComposerEditorTests.cs","Test/ColorVision.Copilot.Tests/CopilotComposerPagingTests.cs","Test/ColorVision.Copilot.Tests/CopilotCodexMentionsV2FeatureTests.cs","Test/ColorVision.Copilot.Tests/CopilotChatViewModelProfileIsolationTests.cs","Test/ColorVision.Copilot.Tests/CopilotMarkdownViewTests.cs","Test/ColorVision.Copilot.Tests/CopilotMarkdownTableBoundaryTests.cs","Test/ColorVision.Copilot.Tests/CopilotImageAttachmentAdmissionTests.cs","Test/ColorVision.Copilot.Tests/CopilotAttachmentPathTests.cs","Test/ColorVision.Copilot.Tests/CopilotConversationDeletionPersistenceTests.cs","Test/ColorVision.Copilot.Tests/CopilotRequestAdmissionLifetimeTests.cs","Test/ColorVision.Copilot.Tests/CopilotMessageEditAdmissionTests.cs","Test/ColorVision.Copilot.Tests/CopilotWebPageDeadlineTests.cs","Test/ColorVision.Copilot.Tests/CopilotWebPageAttachmentAdmissionTests.cs","Test/ColorVision.Copilot.Tests/CopilotAttachmentRemovalEditLifetimeTests.cs","Test/ColorVision.Copilot.Tests/CopilotImagePayloadValidationTests.cs","Test/ColorVision.Copilot.Tests/CopilotImageInputBudgetTests.cs","Test/ColorVision.Copilot.Tests/CopilotComposerMentionRangeTests.cs","Test/ColorVision.Copilot.Tests/CopilotChatPanelScrollLifetimeTests.cs","Test/ColorVision.Copilot.Tests/CopilotMarkdownRenderSchedulingTests.cs","Test/ColorVision.Copilot.Tests/CopilotPartialEvidencePresentationTests.cs","Test/ColorVision.Copilot.Tests/CopilotLocalFileLinkNavigationTests.cs", "Test/ColorVision.Copilot.Tests/CopilotConversationExportFileNameTests.cs"]
related: ["copilot.runtime", "copilot.configuration", "copilot.view-model", "copilot.lifecycle", "copilot.session-tools", "copilot.execution", "copilot.tool-contracts", "ui.desktop-pet"]
---

# Copilot 输入、命令与活动呈现

本页维护 `CopilotChatPanel`、ViewModel 命令路由、Presentation 和桌宠的交互契约。完整命令、参数及运行中可用性以 `CopilotLocalCommandCatalog` 和 `/help [命令]` 为准，不另列全量手册。

“本地命令”只表示宿主解析，不保证只读、不联网或不改变任务。设置入口在停靠标题栏；输入栏提供附件、访问模式、模型/推理、语音和发送，窄面板仍须显示权限文字。当前窗口建议只是附加上下文入口。

## 命令发现、补全与执行门禁

`/` 列固定命令与可用 Skill，`$name`/`/name` 可引用 Skill；`/help` 只查目录元数据，不执行目标或读取 Skill 正文。参数补全来自同一目录和当前 Profile，尚需正文的候选只补文本，不直接执行。

↑↓ 选候选，裸 Tab/点击补全，Enter 对可提交候选进入正常提交；带修饰键的 Tab 仍交焦点/宿主处理，右箭头仅在无选择且光标末尾补全。补全、引用和历史搜索优先于发送，不能把补全当成用户提交。

候选可在 Agent 运行时显示，不代表命令可立即执行。`CanSuggest` 与 `CanExecute` 分开；运行中不可执行命令可走专用后续队列，不冒充 steering。待回答、排队视图、编辑或历史搜索按状态关闭相应目录，最终执行仍复核资格。

## Composer 与显式引用

所有异步发送/重试/附件准备绑定原 conversation、请求/编辑身份及快照。图片保存、自动压缩等等待后须再验：源会话删除/归档、ViewModel 释放、原轮次替换或宿主失准入时，不建消息、不清 checkpoint、不调用模型。单纯切换页面不取消仍有效原请求，也不能改写新会话草稿。同草稿版本、编辑和附件快照的重复提交只准入一次，失败可原样重试，新正文/附件或主动重发仍正常。

图片准备未注册 Host 前也能由主按钮、Esc、`/stop` 取消当前选中会话准备；其他会话不受影响。取消在 UI 续体复核，保留输入/附件；排队准备继承原 Host 取消及后继规则。编辑期间的迟到失败不覆盖后来草稿/编辑，所有本地异步提示只在原会话仍选中且反馈版本未更新时发布；合法附加、导出或停止结果仍归原请求，不因页面切换伪装失败。

内容寻址图片从保存前到消息/运行取得引用之间持临时引用，保护共享图片不被孤儿清理误删；失败/取消最终释放。目录和祖先、复用目标均检查重解析点，不能向链接目标写入，不宣称可抵御检查后的并发替换。图片签名/预算之外还须首帧完整解码，拒绝部分像素后缩放“修复”；未缩放有效图片保留原字节/动画，这不是严格尾标记或逐帧校验。图片分析失败/取消仍结算已报告用量，不估造未报告费用。

自动压缩用提交时的原会话/Profile/配置，成功后重新捕获该会话历史；排队 `/compact` 保留入队上下文，执行时取最新历史，不切页面或改后来模型选择。停止摘要请求不写摘要，失败/取消保留后续队列待显式启动，确实无需压缩则正常交接；已报告费用仍归原会话。模型上下文完整性见[压缩契约](./copilot-agent-execution.md#压缩摘要不能升级完成证据)。

`/diff` 固定到状态检查返回的同一仓库，期间消失则失败，不转另一根拼补丁。原始状态截断时计数仅是下界；只是展示列表截断而原始状态完整时计数仍准确。停止后台命令也依真实返回状态报告，确认期间自然结束不能宣称被停止。

`/mention` 与 `+` 菜单只打开光标处 `@` 查询，不自行选择、附加或发送。查询仅从 `@` 到光标，补全只替换该段，保留后文；光标/选择变化使旧异步结果失效，已闭合引用不重开。索引中 Enter/Tab 都被消费；索引结束且零候选时 Enter 可进入正常发送，不能承诺未闭合 mention 永远不发送。

| 引用 | 选择后的边界 |
| --- | --- |
| 文件/图片 | 走既有附件解析、去重和范围，不授予整盘访问 |
| 模板/菜单 | 结构化 source 身份及闭合标记；引用菜单不执行，模板身份不代表参数已读全 |
| Skill | 校验引用并准备输入，不在补全时执行或扩大工具权限 |

`ConfiguredMentionsV2Enabled` 关闭时只保留文件候选，不承诺全部类型可选。实际模板读取走 `InspectTemplateType`/`InspectSavedTemplate`，业务来源见[模块扩展](./copilot-agent-extensions.md)。显式附件可能含用户原始数据，局部脱敏不等于附件已净化。

网页附件与 FetchUrl/搜索深读共用 `CopilotWebPageToolSupport`：静态 HTTP 优先，纯文本保留换行/字面标签，HTML 正文缺少或稀疏才用 WebView2 执行脚本提取可见正文。结果区分 static 与 browser-rendered，空页/失败不冒充成功；期限覆盖传输和渲染，取消释放本次控制器，少量静态内容可保留但注明不足。

浏览器用独立 InPrivate，不复用用户登录态，不提供登录、点击、提交、下载、弹窗或主机桥接。资源仅 GET 且逐请求/重定向检查公开地址、DNS 与连接，其他通道受限；资源总量和正文预算有界，完全访问不绕过。它用于动态正文读取，不证明视觉页面审计。URL 去重保留路径/query 大小写及顺序；静态链接去 fragment，浏览器链接保留 hash 路由，不能混用。

刷新同来源上下文/网页只替换草稿快照，不原地改写已被请求捕获的对象。发送继续用旧快照且不消费后来刷新内容，新增附件保留；编辑取消、原附件移除或关闭后的迟到网页结果不回填。普通重试沿原附件，`/retry refresh` 才重读网页，失败传递失败诊断，不把旧缓存当刷新证据；不顺带刷新静态 Context。剪贴板后台图片仅在原操作有效后附加，取消/关闭的未附加临时图片按根内清理，不动旧附件。

发送与运行中输入的操作规则：

| 状态 | 操作 |
| --- | --- |
| 标准输入 | Enter 提交，Shift+Enter 换行 |
| `/multiline` | Enter 换行，Shift+Enter 提交 |
| 活动 Agent 的纯文本 | `/follow-up steer` 或 `/follow-up queue` 决定默认方式，Tab 用另一方式；补全层优先消费 |
| 活动 Agent 新增附件 | 文本与附件整体进入后续队列，即使选了 steering；队满/校验失败保留全部输入，不降级成只送文本 |
| Ctrl+Enter 立即接管 | 先登记下一轮再请求取消当前轮，登记失败不先取消；等待当前轮收尾，不证明工具已停稳 |

生命周期细节以 `CopilotRequestAdmissionLifetimeTests`、`CopilotImageAttachmentAdmissionTests`、`CopilotWebPageAttachmentAdmissionTests`、`CopilotComposerMentionRangeTests` 与 `CopilotWebPageRenderingTests` 定位；真实调度/网页/桌面边界不能由受控结果替代。

## 草稿编辑与历史恢复

结构化问题预设选项只提交选项文字，保留未发草稿/附件；手工答案接受后才消费所提交正文，问题卡等待 Runtime 事件收尾，不因点击自认已回答。

编辑最新请求先退出历史搜索，使用真实草稿作替换确认。发送绑定本次编辑身份和原消息对象，取消/重开编辑、原轮次替换或其他新状态使旧发送失效，不清当前 checkpoint 或结束新编辑。同一次编辑中后来新增正文/附件保留，提交可继续使用已捕获旧快照；普通发送也不能在等待后给正在编辑的同一会话追加轮次。

`Ctrl+E` 打开扩展编辑器，保留完整正文/空白和光标，确认才写回。保存复核原会话、Composer 版本及编辑身份；等待期间草稿恢复或变化时保留窗口并展示当前完整草稿供合并，再保存只能替换已展示版本。切换会话/搜索/重新编辑后的旧窗口不能写错状态。超长恢复草稿不因打开/确认/暂存而截成合法前缀，发送仍独立校验上限。

`Ctrl+S` 在非空且无 stash 时暂存文本、光标、附件与一次性请求状态，空输入时恢复并消费；不覆盖已有 stash、不发送、不保存临时授权。stash 持久化并计附件引用，总状态超限明确失败，不靠删尾保存。历史搜索弹层内 Ctrl+S 改为切换范围，不操作 stash。

`/history`/输入框 Ctrl+R 只搜可见 user Content，默认当前会话，可切全部本地会话；不搜隐藏请求/附件正文/trace。Enter/Tab/点击只恢复完整请求到草稿，Esc 恢复打开前草稿，不发送。搜索词不是草稿，也不参与模型上下文计量；外部请求、队列移回编辑先核对真实草稿保护，再关闭搜索交接。侧栏 Ctrl+R 是重命名候选，语义不同。

搜索去重/评分/候选上限直接查 `CopilotPromptHistorySearchBehaviorTests`；正文相同但来源更新仍应刷新来源，后台刷新保留有效选择，不把输入框变成第二份历史。计量归属见[输入时的上下文计量](./copilot-view-model-architecture.md#输入时的上下文计量)。编辑回归为 `CopilotMessageEditAdmissionTests`、`CopilotExpandedComposerEditorTests`。

## 会话导航、回顾与出口

新建/切换前退出提示历史搜索，离开会话保留完整草稿、模式和 Review 目标；仅完全空白会话直接复用时重置 Auto/目标。附件移除先等引用保存，失败恢复原位置/原草稿，不能将已取消编辑的历史附件插进新草稿，也不能覆盖后来刷新过的同来源附件。普通草稿进入消息编辑时，失败回补到原草稿备份而非历史附件；该备份参与图片引用但只在当前 ViewModel 内，不承诺重启恢复。

永久删除须确认后再查运行/保留条件，并先持久化不含目标的状态，成功后才清输出/独占附件。保存失败恢复会话、恢复记录及选择，不报成功；保存等待中的标题/用量仍归原会话，成功删除后迟到结果不复活会话。清理复核其他会话、stash、steering、队列和临时引用及根内路径，不保证 OS 拒绝的文件即时消失。

以下入口容易误解，完整参数仍查 `/help`：

| 入口 | 语义与限制 |
| --- | --- |
| `/recap` | 当前可见目标/最近轮次/任务和草稿附件数量的本地报告，不读隐藏内容或调用模型 |
| `/view-plan` | 定位最近完整 Completed Plan 原消息，不生成、批准或执行计划 |
| `/turn N` | 最新可见 user 为 1，仅滚动，不分支/恢复/撤销 |
| `/find`、Ctrl+F/Ctrl+G | 会话内/跨会话可见正文与活动/附件显示元数据查找，不读附件正文或外部文件 |
| `/resume`、`/rename` | 精确 ID/唯一完整标题才直接导航，否则搜索；手动命名防迟到自动标题覆盖 |
| `/clear`（`/new`） | 保留旧会话，建干净上下文，不继承 checkpoint/临时批准，不等于 compact |
| `/fork`、`/branch`、`/rewind` | 会话快照/分支；rewind 恢复目标请求供编辑、不自动发送，不回滚文件、不继承执行授权 |
| `/archive`、`/unarchive`、`/delete` | 隐藏与永久删除分开，后者仍需确认和当前状态复核 |
| `/copy N`、Ctrl+Shift+C | 选择稳定可见回答，跳过活动、中断及 display-only 项，部分回答不遮住上条稳定结果 |
| `/export` | 无参数复制可见 Markdown，有文件名则打开用户保存流程，临时文件原子替换；Windows 设备名及变体拒绝 |
| `/feedback` | 非模态打开反馈窗口并准备有界快照，用户审阅后显式 Send 才上传 |
| `/usage`、`/stats` | 已记录本地用量/诊断，不是账户账单，不为显示额外调用模型 |

导出与反馈仅取完成且可见的消息/附件引用，不含隐藏请求、reasoning、trace、附件正文或 Composer 草稿；有界反馈仍可能含实际可见用户数据，发送前审阅。反馈临时附件随窗口清理。范围与费用见[用量观察](./copilot-agent-execution.md#用量观察与分支去重)。

查找刷新更新高亮但不重复拉回当前匹配；主动导航优先于流式滚底，向上阅读取消待滚底，旧会话/卸载/已关闭查找的回调失效。延迟排版增高只在原本靠底且无向上位移时继续跟随，初始零尺寸不证明原位置；历史阅读保持位置。滚底不用末行定位，避免超高消息被滚到行首。实现/回归定位 `CopilotChatPanelScrollLifetimeTests`，不在正文维护 Dispatcher 顺序和像素阈值。

删除/附件回退的验证入口为 `CopilotConversationDeletionPersistenceTests`、`CopilotAttachmentRemovalEditLifetimeTests`；它们不修复旧版本已丢失数据。

## 诊断、设置与任务控制不是同一种命令

- `/status`、`/doctor`、`/debug-config`、`/context`、`/hooks`、`/mcp` 只展示已有脱敏状态/健康快照；doctor 不自动联网测试或修复，mcp 不等于刷新发现。
- `/settings`、`/model`、`/reasoning` 使用现有配置与支持档位，落盘/运行态发布分别判断，见[配置契约](./copilot-configuration.md)。`/personality` 是会话后续沟通风格，不改权限。
- `/permissions ask|auto|full` 对应按需确认、临时复核和持久完全访问；`/approve` 是待审/自动拒绝精确重试入口，细节见[审批契约](./copilot-agent-tool-contracts.md#原生审批与参数快照)。
- `/tasks` 的 stop/dismiss 需确认，resume 重查恢复兼容；折叠任务列表不等于控制任务。`/queue` 的编号是执行时重新解析的全局位置，不是稳定 ID；send 提升接管，edit 移回草稿不发送，delete/clear 只取消仍等待项。确认期间已晋升的请求跳过，不能误停活动任务。
- `/ps` 仅 Copilot 后台登记表，stop 需确认，不是全系统进程；`/agents` 的只读目录与 steer/stop 控制分开。
- `/init`、`/review`、`/verify`、`/plan`、`/compact`、Skill 可能调用真实模型/工具，`/rollback` 进入精确文件回滚审批；Slash 不绕过范围、预算或执行证据。项目指令与[生命周期](./copilot-agent-lifecycle.md)、[Skills](./copilot-skills.md)及[任务工具](./copilot-agent-session-and-tools.md)分别负责其边界。

## 消息显示与桌宠活动

本地文件链接可用工作区内绝对/相对路径、file URI 及行列定位，含空格用 Markdown 尖括号路径。点击时重查当前工作区/显式受信根和存在性，普通链接不因诊断命令的额外受信根获授权；非法行列不退化成普通打开。只给同路径活动编辑器定位，独立查看器不能定位时保留旧光标并提示，不假称完成。定位具体格式见 `CopilotLocalFileLinkNavigationTests`。

输入框接应用保存路由，普通状态暂存/恢复，历史搜索时改范围；默认 Ctrl+S 随应用配置变化。无 ViewModel 拒绝并停止路由，不能误保存别的文档。会话 Ctrl+F 与主窗口 Ctrl+Shift+P 功能搜索分开。

`/transcript` 仅展开已有 trace 展示，不创造/导出新隐藏推理；`/compact-mode` 改间距，不压模型上下文；timestamps 只改时间显示。工具成功与覆盖完整独立，partial 标题/展开理由和续读方向随会话保存，真实失败优先；旧记录不凭摘要补造 partial，也不由工具成功证明任务完成。

Markdown 保留代码字面语义与复制内容：围栏优先于公式/表格，正确处理同字符闭合、缩进、空块和未闭合块；行内代码需同长度反引号配对，不解析内部公式/链接，转义竖线不当表格边界。公式支持常用行内/块分隔符，失败回退文本，显示不代表数学验证。语法细节与复制边界直接查 `CopilotMarkdownViewTests`、`CopilotMarkdownTableBoundaryTests`。

渲染合并连续更新但不因每片新文本无限推迟；宽度变化可重排，流式更新/同正文重排尽量保留可核对选区，歧义不猜。消息容器换消息清掉旧正文/代码选择与滚动，即使正文相同；同消息重载可保留。代码焦点仅在原控件仍持焦点且窗口活动时恢复，不从输入框抢焦点。渲染仍在 UI 线程，合并间隔不是繁忙 UI 延迟保证。定位 `CopilotMarkdownRenderSchedulingTests`。

桌宠启用集成后只从 TaskHost/confirmation store 投影有界多会话活动，优先级 `NeedsInput → Blocked → Ready → Running`；打开 Ready/Blocked 消除待查看，NeedsInput 不因打开而消失，取消队列不覆盖活动导航目标。批准卡先显示原生完整确认并重查上下文，图标不授予执行权；投影不改变调度/审批/恢复。启用与素材见[桌面宠物](../../04-api-reference/ui-components/desktop-pet.md)。

## 验证范围

frontmatter 映射测试保护命令发现/执行分离、输入生命周期、引用/分页、持久化与活动投影。测试引用不表示本次通过，受控 STA/磁盘/渲染检查不替代真实焦点、系统剪贴板/保存窗口、字体布局和桌宠确认交互。修改命令须同时核对 Catalog、AvailabilityPolicy、ViewModel handler 和 Panel 键盘路由，不能只改帮助文字。

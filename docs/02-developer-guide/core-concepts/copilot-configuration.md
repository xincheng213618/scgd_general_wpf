---
knowledge_id: "copilot.configuration"
knowledge_type: "topic"
status: "current"
summary: "ColorVision内置Copilot的设置草稿、配置保存与运行态发布、模型选择和联网诊断；保存失败可能已落盘，Local MCP测试核验会话握手与只读状态调用。"
aliases: ["有效配置诊断","/debug-config","/debug config","/context","/memory","Copilot配置","Copilot 设置 Save / Apply / Cancel","聊天配置","模型设置","Test Model","Apply to Chat","CopilotSettingsViewModel","CopilotConfig","推理模式保存失败","MCP连接测试","config.toml不生效"]
code_paths: ["ColorVision/Copilot/Config/CopilotEffectiveConfigDiagnostics.cs","ColorVision/Copilot/CopilotChatViewModel.DiagnosticsCommands.cs","ColorVision/Copilot/CopilotContextDiagnostics.cs","ColorVision/Copilot/CopilotProjectInstructionDiagnostics.cs","ColorVision/Copilot/Agent/CopilotProjectInstructionDiscoveryConfig.cs","ColorVision/Copilot/Agent/CopilotCodexExecPolicy.cs","ColorVision/Copilot/Config","ColorVision/Copilot/CopilotSettingsWindow.xaml","ColorVision/Copilot/CopilotSettingsWindow.xaml.cs","ColorVision/Copilot/CopilotSettingsViewModel.cs","ColorVision/Copilot/CopilotSettingsViewModel.ProfileManagement.cs","ColorVision/Copilot/CopilotSettingsViewModel.BackendSync.cs","ColorVision/Copilot/CopilotSettingsViewModel.ExternalMcp.cs","ColorVision/Copilot/CopilotSettingsViewModel.Diagnostics.cs","ColorVision/Copilot/CopilotSettingsViewModel.McpOperations.cs","ColorVision/Copilot/CopilotSettingsViewModel.WebPageNetwork.cs","ColorVision/Copilot/CopilotChatViewModel.ControlAndSettings.cs","ColorVision/Copilot/CopilotChatViewModel.Composer.cs","ColorVision/Copilot/CopilotChatViewModel.ConfigPersistence.cs","ColorVision/Copilot/CopilotChatViewModel.ConversationCommands.cs","ColorVision/Copilot/CopilotChatViewModel.Conversations.cs","ColorVision/Copilot/CopilotChatViewModel.Lifecycle.cs","ColorVision/Copilot/State/CopilotConversationSession.cs","ColorVision/Copilot/State/CopilotChatStatePersistenceCoordinator.cs","ColorVision/Copilot/CopilotModelConnectionDiagnostic.cs","ColorVision/Copilot/CopilotMcpConnectionDiagnostic.cs","ColorVision/Copilot/Mcp/CopilotMcpRequestHandler.cs","UI/ColorVision.UI/ConfigHandler.cs"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotEffectiveConfigDiagnosticsTests.cs","Test/ColorVision.Copilot.Tests/CopilotContextDiagnosticsTests.cs","Test/ColorVision.Copilot.Tests/CopilotProjectInstructionDiagnosticsTests.cs","Test/ColorVision.Copilot.Tests/CopilotConfigurationIsolationTests.cs","Test/ColorVision.Copilot.Tests/CopilotBackendSyncTransactionTests.cs","Test/ColorVision.Copilot.Tests/CopilotChatConfigPersistenceTests.cs","Test/ColorVision.Copilot.Tests/CopilotMcpClientConfigurationTests.cs","Test/ColorVision.Copilot.Tests/CopilotMcpConnectionDiagnosticTests.cs","Test/ColorVision.Copilot.Tests/CopilotLocalMcpDiagnosticLifecycleTests.cs","Test/ColorVision.Copilot.Tests/CopilotExternalMcpDiagnosticDraftTests.cs","Test/ColorVision.Copilot.Tests/CopilotModelConnectionDiagnosticLifecycleTests.cs","Test/ColorVision.Copilot.Tests/CopilotProfileConfigTests.cs","Test/ColorVision.Copilot.Tests/CopilotConfigWebPageNetworkTests.cs"]
related: ["copilot.runtime", "copilot.interactions", "copilot.lifecycle", "copilot.extensions", "copilot.mcp-server", "copilot.view-model", "ui.configuration"]
---

# Copilot 设置、持久化与连接诊断

本页负责 ColorVision 内置 Copilot 的配置来源、设置窗口和聊天面板中的配置变更。设置草稿、配置文件、运行期对象、会话中的模型选择和远端连接是不同状态；任何一个“成功”提示都不能代替其它层的完成证据。

## 配置来源与责任

运行时使用 `ConfigHandler` 提供的 `CopilotConfig`，不加载全局或项目 `config.toml` 来选择 provider、model、tools 或 approval。仍会发现有作用域的 `AGENTS.md` / `CLAUDE.md`，其顺序、预算和权限边界见[项目指令](./copilot-agent-lifecycle.md)；技能来源与调用见 [Copilot 技能](./copilot-skills.md)。源码中的 `CopilotCodex*` 兼容类型、来源标签或设置页生成的 TOML 片段，不表示内置 Copilot 会读取外部 Codex 配置。

`CopilotProjectInstructionDiscoveryConfig` 提供默认快照、预算上限和项目根标记校验；执行策略判断与 Shell 环境过滤直接消费请求快照。`CopilotConfigurationIsolationTests` 验证外部配置不会覆盖 ColorVision 设置，同时仍能发现指令文档。

| 状态 | owner 与用途 |
| --- | --- |
| `CopilotConfig.Profiles` | provider 协议、模型、地址、API Key、生成参数和模型能力声明；不是会话历史 |
| `CopilotConfig.AgentDefaults` | 全局 Agent 预算、压缩、Shell 偏好和 Skill 覆盖；不属于单个模型 Profile，技能开关与生效优先级见 [Copilot 技能](./copilot-skills.md) |
| `CopilotConfig` 的 MCP / Web / Backend 字段 | 入站 Local MCP、外部 MCP client 配置、Web Pref64 和后台同步地址；各入口的联网与落盘不同 |
| `CopilotChatState` / `CopilotConversationRecord` | 活动 Profile ID、各会话选择、回答风格、消息与恢复状态；由独立的会话状态存储负责 |

配置 JSON 路径、节合并、文件替换和重载导致的旧对象失效见[配置持久化与对象所有权](../../04-api-reference/ui-components/configuration.md)。Copilot 设置保存的是其中的 `CopilotConfig` 节，不另建一个 `config.toml` 或模型配置数据库。

## 本地配置与上下文诊断

三个命令各有明确的读取范围，均不调用模型、Agent 工具或外部 MCP，不保存配置，也不把诊断文本加入模型历史。它们预览当前设置与待提交上下文；正在运行的任务继续使用提交时冻结的 Profile、预算、指令及工具配置，不能用诊断中更新后的值反推在途任务已经切换。

| 入口 | 实际报告范围 |
| --- | --- |
| `/debug-config` | 内置默认、应用配置文件元数据、状态文件加载／恢复来源、当前 Profile、AgentDefaults、会话覆盖和临时权限；显示实际宿主 sandbox／审批／Shell 策略，以及 Local MCP、外部 MCP 数量和 Web Pref64 状态 |
| `/context` | 当前输入模式、历史与工具结果预算、压缩状态、持续目标、指令与 Skill 数量、业务扩展及当前策略；Chat 模式保留会话相关信息，省略不参与该模式的 Agent 扩展详情 |
| `/memory [open N]` | 个人与项目指令的发现目标、文件顺序、截断与预算；`open N` 打开对应源文件，发现清单不是当前任务已经注入的回执 |

这些输出不列出外部 `config.toml` 的模型或功能覆盖来源。Profile 模型与提示主体来自 ColorVision 配置，会话回答风格来自会话状态；内部策略快照仍用于说明实际权限，但不表示存在可编辑的外部 Codex 配置层。`/context` 的指令预览与 `/memory` 的发现结果，是否注入还取决于后续请求的本地证据需求、工作区补丁能力与模式。

`/debug-config` 只探测主配置 JSON 的 `CopilotConfig` 节、schema、属性存在性及 Profile ID，文件大于 16 MiB 时不解析。文件缺失、节缺失、损坏或不可读取时保留运行期值，标明“当前文件来源未证实”；更高版本 schema 会报告写入被阻止。文件元数据只说明命令执行时的文件状态，不能证明每个运行期键的启动来源。状态来源保留主文件、临时快照、备份、恢复快照和未来版本等区别。

诊断不输出 API Key、MCP token、系统提示或自动复核策略正文、Shell 环境变量值、后台／外部 MCP 地址及 Pref64 前缀。模型端点仅显示 origin，去掉用户信息、路径、查询和 fragment。设置变更后的诊断重新读取当前对象与文件元数据，不更改原配置文件；完整权限与审批执行边界见[工具契约](./copilot-agent-tool-contracts.md)。

## 设置窗口中的草稿与按钮

`/settings [models|agent|web|mcp|sync]` 由 `Config/CopilotSettingsCommand.cs` 路由到同一个 `CopilotSettingsWindow`；聊天 ViewModel 的 `OpenSettings` 在 `IsBusy` 时不打开窗口。窗口创建 `CopilotSettingsViewModel`，从配置克隆 Profiles，并将其它字段复制为草稿。选择、添加、复制、删除 Profile 或编辑字段，通常只改变这份窗口草稿。

“打开后取消”不是严格的零写入事务：构造函数先对运行期配置执行 `EnsureInitialized()`，发现需规范化时会调用 `Save<CopilotConfig>()`。这个初始化保存尝试与之后的按钮保存不同；该便捷方法不向窗口返回保存结果。初始化会处理缺省值、旧 schema、失效内置 Profile 和不受信后台 Profile。未来版本的 `SchemaVersion` 不被当前实现规范化，序列化保存会拒绝覆盖。

| 入口 | 实际结果 |
| --- | --- |
| 普通 Add / Duplicate / Delete | 更改草稿；最后一个 Profile 被删除时补一个默认草稿；需后续保存 |
| Apply | 调用 `Save()`，不关窗 |
| Save | 调用同一个 `Save()`；只有返回 `true` 才以成功结果关闭 |
| Add and Use / Apply to Chat | 最终调用同一个 `Save()`，会提交整个设置候选，不只是被选中的模型 |
| Cancel / Close / 关闭窗口 | 不调用该按钮保存，也不回滚之前 Apply、后台同步或初始化已经产生的写入；关闭时取消窗口持有的异步操作 |

保存按钮的前置有效性主要约束 MCP 端口、外部 MCP 文本和 Web Pref64 语法，不要求每个 Profile 都能连接。`CopilotProfileConfig.IsConfigured` 只检查 API Key、Base URL、Model 和端点规则；“Ready”不是网络测试结果。模型的图像输入声明也不是自动探测：改变模型、地址或协议会清除 `SupportsImageInput`，不能把旧端点能力沿用给新端点。

## 保存完成的三个层次

`CopilotSettingsViewModel.ProfileManagement.cs::Save` 的顺序是：解析草稿 → 从配置和草稿构造独立候选 → `EnsureInitialized` → `ConfigHandler.TrySaveAndPublish` 先落盘，再通过 `CommitPersistenceSnapshot` 发布运行期配置 → 属性通知、重建窗口 Profiles、应用 Local MCP 设置和更新选中 Profile。

| 结果 | 必须怎样解释 |
| --- | --- |
| `NotPersisted` | 本次候选未提交；正常保存失败路径不将候选发布到运行期，保留草稿以便修正或重试 |
| `PersistedButPublishFailed` | 文件已保存，但内存发布未完整完成；不能宣称旧文件未变，也不能靠 Cancel 回滚 |
| 发布成功后，通知或运行态刷新抛异常 | 文件仍已保存，运行期或窗口可能只完成部分刷新；`Save()` 同样返回 `false`，提示已保存但刷新失败 |
| 全部完成 | 重新从规范化后的配置克隆窗口列表，清除 `HasUnsavedSettings`，设置 `HasAppliedChanges`，返回 `true` |

因此 `Save() == false` 不等于磁盘没变。先看 `SettingsStatusText` 区分 “not saved” 与 “saved, but … could not refresh”，再核对实际配置和运行期；不要用反复点击或直接覆盖文件掩盖发布故障。规范化还可能删除失效或不受信的 Profile，成功后应以重建的列表为准，不能继续持有旧草稿对象。

从聊天面板打开设置时，`OpenSettings` 在窗口关闭后只要发现成功结果或 `HasAppliedChanges`，就会 `ReloadStateFromConfig(window.ActiveProfileId)`，重绑定 Profile、会话选择并请求会话状态保存；所以 Apply 后再 Cancel 仍会触发重载。Local MCP 的 `ApplySettings` 则在设置保存的刷新阶段执行，启停监听或变更 token 不是纯文件操作。协议和会话失效条件见[Local MCP](./colorvision-mcp.md)。

## 新建 Profile 的模型预设

`CopilotVendorCatalog` 只为新建或编辑 Profile 提供当前模型名候选，不自动改写已经保存的模型、地址、凭据或默认 Profile。当前候选为：OpenAI `gpt-6-astra`、`gpt-5.6`、`gpt-5.6-sol`、`gpt-5.6-terra`、`gpt-5.6-luna`；DeepSeek `deepseek-v4-pro`、`deepseek-v4-flash`、`deepseek-v4-flash-vision-exp`；Claude `claude-fable-5`、`claude-mythos-5`、`claude-opus-5`、`claude-sonnet-5`、`claude-haiku-4-5-20251001`；Grok `grok-4.6`、`grok-4.5`、`grok-4.20`；Gemini `gemini-3.1-pro-preview`、`gemini-3.8-flash`、`gemini-3.7-flash`、`gemini-3.6-flash`、`gemini-3.5-flash`、`gemini-3.5-flash-lite`；GLM `glm-5.2`、`glm-5-turbo`、`glm-4.7-flash`、`glm-4.5-air`；MiniMax `MiniMax-M2.7`、`MiniMax-M2.7-highspeed`、`MiniMax-M2.5`、`MiniMax-M2.5-highspeed`；MiMo `mimo-v2.5-pro`、`mimo-v2.5`；SenseNova `sensenova-6.7-flash-lite`。

目录移除已退役或已被当前系列替代的旧候选，但兼容读取手工填写或既有保存值。模型名出现在候选中不等于账号已获权限、端点兼容或模型可连接；仍应通过 Profile 的实际连接测试验证。

## `/model` 与 `/reasoning` 不是同一种保存

`/model` 选择一个已经存在的 Profile，不改写其 provider、模型地址或凭据。`SelectModelProfile` 通过 `SelectedProfile` → `CopilotConversationSession.SelectProfile` 更新运行期选择、`ActiveProfileId` 和当前会话的 `ProfileId`，再由 `PersistState()` 请求保存会话状态。选择先在内存生效，状态保存由 `CopilotChatStatePersistenceCoordinator` 异步完成；命令的“后续请求将使用”不是耐久化回执，保存故障也没有在此选择方法中回滚。会话保存通知、重试与 Flush 属于[状态所有权](./copilot-view-model-architecture.md)。

`/reasoning`（兼容 `/effort`）才会修改当前 Profile 的 `ReasoningMode`。只接受 `CopilotReasoningCapabilities` 为该 Profile 声明的级别，归一化后通过 `TryPersistConfigMutation` 克隆候选并使用上述三态提交；`NotPersisted` 保留原 Profile 并显示“推理模式未更改”，`PersistedButPublishFailed` 显示“已保存，但当前聊天界面未能刷新”。成功后重新绑定发布的 Profile，而不是原地修改旧对象；使用同一个 Profile 的后续请求会读取这个配置，不应描述成仅本会话风格。

官方 `api.openai.com` 上的 GPT-6 Astra 提供 `Default/Low/Medium/High/XHigh/Max`，不提供 `Disabled/Enabled`；历史 `Disabled` 配置或 Codex `none/minimal` 覆盖发送前收敛为 `low`，`ultra` 收敛为 `max`。枚举新增值追加在已有数值之后，避免改变旧 JSON 中 `Default/Disabled/Enabled/High/Max` 的数字含义。伪装成 OpenAI vendor 的第三方兼容端点不会因此获得官方推理档位。

`SetSelectedProfileReasoningMode` 返回是否完成设置（或已是所选值）；`SelectReasoningMode` 只有成功时才回显“已设置／保持”，否则展示同一保存失败或刷新失败说明。已是所选值时不重复保存。测试专用的无 `ConfigHandler` 构造路径只提交内存，即使内部复用 `PersistedAndPublished` 枚举也不代表写了磁盘。

`/personality` 则修改会话回答风格，不属于 Profile 或 `CopilotConfig` 的保存；交互入口见[本地交互](./copilot-local-interactions.md)。这三个命令不授权模型执行工具或更改审批策略。

## 凭据保护与两种 MCP

`CopilotConfig` 实现 `IConfigSecure`。`ConfigHandler` 序列化安全配置的克隆，`CopilotCredentialProtector` 用 Windows DPAPI `CurrentUser` 保护 Profile API Key 和入站 Local MCP 的 `McpBearerToken`，不把运行期明文对象改成密文。解密兼容旧格式；Profile 密钥无法恢复时清空并置 `CredentialNeedsReentry`，Local MCP token 无法恢复时清空，后续初始化可生成新 token。复制配置文件不能保证另一 Windows 用户能解密，也不能把文件保护误认为所有内存、网络或剪贴板数据都已脱敏。

外部 MCP client 的 bearer token 只从配置中指定的环境变量读取，配置保存的是环境变量名称，不是 token 值；其 URL、白名单、默认审批和发现生命周期由[外部 MCP](./copilot-agent-extensions.md#外部-mcp-工具发现)负责。入站 Local MCP 的复制按钮可以把 token 或含真实 token 的 PowerShell 命令写入剪贴板；`Copy Codex Config` / `Copy Token Command` 只复制，不替用户修改外部客户端配置或执行环境变量命令。Regenerate 先改变草稿，须 Apply / Save 才更新运行态，随后客户端也需更新凭据。

## 诊断、发现与同步的副作用

以下是入口契约，不是要求为核对文档而执行这些动作。涉及模型、MCP 或后台的检查需要用户明确选择对应服务；不要使用真实凭据、模型请求或生产后台做默认文档验证。

| 入口 | 读取什么、产生什么 |
| --- | --- |
| Test Model | 使用当前选中的未保存 Profile 的克隆，固定短提示要求回答 OK，覆盖系统提示、`MaxTokens=128`、`Temperature=0`，经 `CopilotChatService` 发起真实流式模型请求；可能产生供应商用量和传输重试，不保存设置、不运行 Agent 工具 |
| Local MCP / Test Connection | 用窗口当前端口和 token 向 loopback 发送 HTTP；不先替用户保存或启用 server，不能据此验证未应用草稿已在运行 |
| External MCP / Refresh Discovery | 从当前未保存的配置文本构造请求，强制实时连接与工具发现，更新进程内健康／缓存状态并释放本次 lease；不持久保存这份配置，也不调用发现到的业务工具 |
| Refresh Diagnostics、`/doctor`、`/mcp` | 读取本地配置或已有健康快照，不等于执行 Test Model 或实时远端发现；命令范围见本地交互主题 |
| Backend Sync | 显式联网下载托管 Profile，并立即尝试持久化与发布，不是等待 Apply 的预览；远端要求 HTTPS，loopback 可用 HTTP |

Test Model 的结果记录耗时、可显示字符和重试；“Connected”也可能带“没有可显示文本”或“响应提前结束”的提示。它没有验证答案必须精确等于 OK，更不能证明工具调用、图像输入、业务正确性或账户额度可用。窗口关闭会请求取消诊断，但已经发送的请求和供应商用量不能由取消撤回。

诊断期间仍可切换和编辑 Profile；这些动作会使原诊断失效并请求取消。结果只允许回写到始终未切换、未编辑的原 Profile，切换后再切回或编辑后改回原值也不能沿用旧结果。成功与失败都在 UI 续体中复查 Profile 修订、取消和窗口寿命，避免已经完成的请求在取消或关窗后覆盖状态。

Profile 未变时，模型测试仍将成功或失败写入该模型的诊断区域；全局设置提示只有仍显示本轮测试提示时才被替换。测试期间用户编辑其它设置或保存校验失败产生的较新提示会保留，避免连接成功掩盖“设置尚未保存”或端口错误。回归在 HTTP 已完成但 UI 续体尚未执行时更改设置，分别验证成功、失败、普通未保存提示与保存校验错误。

Local MCP 测试由 `CopilotMcpConnectionDiagnostic` 依次发送 `initialize`、`notifications/initialized` 和只读 `tools/call get_server_status`。初始化响应须匹配请求 ID、协议版本并返回有效 `Mcp-Session-Id`；后续请求携带该会话和协议头。只有通知得到确认、状态调用返回有效非错误结果时才显示 Connected。整条诊断共享 5 秒取消预算，JSON 响应有大小上限；HTTP、JSON-RPC 或工具错误均不报成功，远端错误正文不会直接回显，避免泄露 token 或会话 ID。此结果不验证其他工具权限、审批或业务执行。

测试绑定开始时的端口、token 和设置修订。修改启用状态、端口数值／文本或 token 会取消本轮测试；编辑后改回原值、输入无效端口或关闭窗口也不能接收旧结果。成功和异常的 UI 续体都会重查窗口、取消及修订状态，同时保留测试期间其他设置操作产生的新提示。取消不会撤回已发送请求，也不关闭已建立的服务端会话。`CopilotLocalMcpDiagnosticLifecycleTests` 用实际请求处理器和受控 HTTP／UI 续体验证这些边界，以及取消完成后按新端口重新测试，不启动真实监听。

外部 MCP 刷新绑定开始时的草稿修订；编辑、清空或编辑后还原配置都会取消并使旧刷新失效。成功和失败都在 UI 续体复查修订、取消和窗口寿命，不能用旧服务列表覆盖当前配置，也不覆盖用户后来产生的其他设置提示。已完成的网络检查可能仍更新其服务的进程缓存／健康；取消不撤销这些已发生操作。诊断会逐个核查配置服务，范围和上限见[外部 MCP 工具发现](./copilot-agent-extensions.md#外部-mcp-工具发现)。

本地 `Refresh Diagnostics`、修改 Local MCP 设置、测试连接及构造诊断复制文本也会刷新外部服务的健康展示，但始终重新解析窗口当前的 `ExternalMcpServersText`，不拿已保存配置替换草稿对应的列表。空草稿显示无服务，无效草稿保留校验错误并清空服务行；有效草稿读取其自身的现有健康快照。`CopilotExternalMcpDiagnosticDraftTests` 覆盖这些入口及健康快照更新，验证这类展示刷新不执行外部发现、HTTP 请求或配置保存。

后台同步使用 `CopilotBackendSyncTransaction` 分开构造“应持久化的列表”和“窗口显示列表”：同步同源托管 Profile，保留无关本地草稿，但不会把未保存的本地草稿一并写入配置。下载完成后、提交开始前再次检查窗口是否关闭；即使取消发生在下载任务完成与 UI 续体执行之间，也不再保存或发布结果。下载成功仍可能保存失败；已持久化后通知失败则保留已保存结果并提示刷新失败。Cancel 不能撤销已经提交的同步，普通 Save 才会提交其余窗口草稿。后台连接与同步配置不是运行时加载 `config.toml` 的另一条路径。

## 实现与验证入口

- `Config/CopilotEffectiveConfigDiagnostics.cs`、`CopilotContextDiagnostics.cs`、`CopilotProjectInstructionDiagnostics.cs`：分别生成配置来源、上下文提交预览和指令发现报告。`CopilotEffectiveConfigDiagnosticsTests` 覆盖当前设置／会话变更、在途快照边界、脱敏、文件缺失／损坏／超限和未来 schema；`CopilotContextDiagnosticsTests`、`CopilotProjectInstructionDiagnosticsTests` 覆盖模式、实际策略与指令展示边界。
- `CopilotSettingsViewModel.ProfileManagement.cs`、`CopilotSettingsWindow.xaml.cs`：按钮保存、Profile 草稿和模型诊断入口；`Config/CopilotConfig.cs`：规范化、候选发布、schema 与凭据边界。
- `CopilotChatViewModel.ConfigPersistence.cs`、`Composer.cs`、`ConversationCommands.cs`：聊天中配置变更、三态反馈及命令回显；`State/CopilotConversationSession.cs`：只选择 Profile 的会话状态变化。
- `CopilotModelConnectionDiagnostic.cs`、`CopilotMcpConnectionDiagnostic.cs`、`CopilotSettingsViewModel.ExternalMcp.cs` / `McpOperations.cs` / `BackendSync.cs`：真实联网入口；`Config/CopilotBackendSyncTransaction.cs`：托管 Profile 合并与发布。
- `CopilotConfigurationIsolationTests` 核对 ColorVision 配置不被外部 TOML 覆盖且仍发现指令；`CopilotBackendSyncTransactionTests` 覆盖锁文件保存失败、草稿隔离、未来 schema、凭据克隆、规范化列表、同步后通知失败，以及下载完成但 UI 续体尚未执行时关闭窗口的提交隔离；不代表模型或 MCP 实际连接成功。
- `CopilotChatConfigPersistenceTests` 覆盖推理配置落盘失败、成功重绑定、命令的保存／刷新失败回显及无 handler 的内存模式；`CopilotMcpClientConfigurationTests` 覆盖配置与未来 schema；`CopilotProfileConfigTests`、`CopilotConfigWebPageNetworkTests` 覆盖能力声明重置与 Web 配置路由／校验。
- `CopilotMcpConnectionDiagnosticTests` 通过受控 HTTP handler 对接实际本地 MCP request handler，覆盖握手顺序、每次诊断的会话隔离、认证与协议错误、有界响应及响应头到达后的正文读取取消，不启动真实监听。
- `CopilotModelConnectionDiagnosticLifecycleTests` 用受控 HTTP 和 UI 续体驱动真实模型诊断路径，覆盖正常结果、切换／编辑后的失效、关窗／取消后已完成结果不回写，以及在途请求取消；不使用实际供应商或凭据。

这些测试路径不是本次运行结果。设置窗口按钮的完整 WPF 交互、实际供应商连接、真实 HTTP 监听和后台同步仍需对应场景验证；不能用元数据／链接校验或某个单元测试通过替代。

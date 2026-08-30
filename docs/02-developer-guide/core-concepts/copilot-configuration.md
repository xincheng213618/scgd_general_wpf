---
knowledge_id: "copilot.configuration"
knowledge_type: "topic"
status: "current"
summary: "ColorVision内置Copilot的设置草稿、配置保存与运行态发布、模型选择和联网诊断；保存失败可能已落盘，Local MCP测试存在握手缺口。"
aliases: ["Copilot配置", "聊天配置", "模型设置", "Test Model", "Apply to Chat", "CopilotSettingsViewModel", "CopilotConfig", "推理模式保存失败", "MCP连接测试", "config.toml不生效"]
code_paths: ["ColorVision/Copilot/Config", "ColorVision/Copilot/CopilotSettingsWindow.xaml", "ColorVision/Copilot/CopilotSettingsWindow.xaml.cs", "ColorVision/Copilot/CopilotSettingsViewModel.cs", "ColorVision/Copilot/CopilotSettingsViewModel.ProfileManagement.cs", "ColorVision/Copilot/CopilotSettingsViewModel.BackendSync.cs", "ColorVision/Copilot/CopilotSettingsViewModel.ExternalMcp.cs", "ColorVision/Copilot/CopilotSettingsViewModel.McpOperations.cs", "ColorVision/Copilot/CopilotSettingsViewModel.WebPageNetwork.cs", "ColorVision/Copilot/CopilotChatViewModel.ControlAndSettings.cs", "ColorVision/Copilot/CopilotChatViewModel.Composer.cs", "ColorVision/Copilot/CopilotChatViewModel.ConfigPersistence.cs", "ColorVision/Copilot/CopilotChatViewModel.ConversationCommands.cs", "ColorVision/Copilot/CopilotChatViewModel.Conversations.cs", "ColorVision/Copilot/CopilotChatViewModel.Lifecycle.cs", "ColorVision/Copilot/State/CopilotConversationSession.cs", "ColorVision/Copilot/State/CopilotChatStatePersistenceCoordinator.cs", "ColorVision/Copilot/CopilotModelConnectionDiagnostic.cs", "ColorVision/Copilot/Mcp/CopilotMcpRequestHandler.cs", "UI/ColorVision.UI/ConfigHandler.cs"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotConfigurationIsolationTests.cs", "Test/ColorVision.Copilot.Tests/CopilotBackendSyncTransactionTests.cs", "Test/ColorVision.Copilot.Tests/CopilotChatConfigPersistenceTests.cs", "Test/ColorVision.Copilot.Tests/CopilotMcpClientConfigurationTests.cs", "Test/ColorVision.Copilot.Tests/CopilotProfileConfigTests.cs", "Test/ColorVision.Copilot.Tests/CopilotConfigWebPageNetworkTests.cs"]
related: ["copilot.runtime", "copilot.interactions", "copilot.lifecycle", "copilot.extensions", "copilot.mcp-server", "copilot.view-model", "ui.configuration"]
---

# Copilot 设置、持久化与连接诊断

本页负责 ColorVision 内置 Copilot 的配置来源、设置窗口和聊天面板中的配置变更。设置草稿、配置文件、运行期对象、会话中的模型选择和远端连接是不同状态；任何一个“成功”提示都不能代替其它层的完成证据。

## 配置来源与责任

运行时使用 `ConfigHandler` 提供的 `CopilotConfig`，不加载全局或项目 `config.toml` 来选择 provider、model、tools 或 approval。仍会发现有作用域的 `AGENTS.md` / `CLAUDE.md`，其顺序、预算和权限边界见[项目指令与 Skills](./copilot-agent-lifecycle.md)。源码中的 `CopilotCodex*` 兼容类型、来源标签或设置页生成的 TOML 片段，不表示内置 Copilot 会读取外部 Codex 配置。

| 状态 | owner 与用途 |
| --- | --- |
| `CopilotConfig.Profiles` | provider 协议、模型、地址、API Key、生成参数和模型能力声明；不是会话历史 |
| `CopilotConfig.AgentDefaults` | 全局 Agent 预算、压缩、Shell 偏好和 Skill 覆盖；不属于单个模型 Profile，生效优先级见生命周期主题 |
| `CopilotConfig` 的 MCP / Web / Backend 字段 | 入站 Local MCP、外部 MCP client 配置、Web Pref64 和后台同步地址；各入口的联网与落盘不同 |
| `CopilotChatState` / `CopilotConversationRecord` | 活动 Profile ID、各会话选择、回答风格、消息与恢复状态；由独立的会话状态存储负责 |

配置 JSON 路径、节合并、文件替换和重载导致的旧对象失效见[配置持久化与对象所有权](../../04-api-reference/ui-components/configuration.md)。Copilot 设置保存的是其中的 `CopilotConfig` 节，不另建一个 `config.toml` 或模型配置数据库。

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

## `/model` 与 `/reasoning` 不是同一种保存

`/model` 选择一个已经存在的 Profile，不改写其 provider、模型地址或凭据。`SelectModelProfile` 通过 `SelectedProfile` → `CopilotConversationSession.SelectProfile` 更新运行期选择、`ActiveProfileId` 和当前会话的 `ProfileId`，再由 `PersistState()` 请求保存会话状态。选择先在内存生效，状态保存由 `CopilotChatStatePersistenceCoordinator` 异步完成；命令的“后续请求将使用”不是耐久化回执，保存故障也没有在此选择方法中回滚。会话保存通知、重试与 Flush 属于[状态所有权](./copilot-view-model-architecture.md)。

`/reasoning`（兼容 `/effort`）才会修改当前 Profile 的 `ReasoningMode`。只接受 `CopilotReasoningCapabilities` 为该 Profile 声明的级别，归一化后通过 `TryPersistConfigMutation` 克隆候选并使用上述三态提交；`NotPersisted` 保留原 Profile 并显示“推理模式未更改”，`PersistedButPublishFailed` 显示“已保存，但当前聊天界面未能刷新”。成功后重新绑定发布的 Profile，而不是原地修改旧对象；使用同一个 Profile 的后续请求会读取这个配置，不应描述成仅本会话风格。

当前 `SelectReasoningMode` 在调用返回 `void` 的 setter 后仍无条件生成“已设置／保存到当前模型 Profile”的本地回显；它可能与 `PendingActionFeedbackText` 中的保存失败提示并存。这是调用层反馈缺口，不能把回显作为落盘证据。测试专用的无 `ConfigHandler` 构造路径只提交内存，即使内部复用 `PersistedAndPublished` 枚举也不代表写了磁盘。

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

Local MCP 测试当前有明确实现缺口：`TestMcpConnectionAsync` 直接发送 `tools/call get_server_status`，没有先 `initialize`，也不带 `Mcp-Session-Id`；`CopilotMcpRequestHandler` 对非初始化请求要求有效会话。按当前实现，即使监听和 token 正常也会遇到缺会话拒绝，不能把该失败直接诊断成端口或凭据错误。完整握手应按 Local MCP 主题核验；本页没有通过修改协议或运行产品来绕过此差异。

后台同步使用 `CopilotBackendSyncTransaction` 分开构造“应持久化的列表”和“窗口显示列表”：同步同源托管 Profile，保留无关本地草稿，但不会把未保存的本地草稿一并写入配置。下载成功仍可能保存失败；已持久化后通知失败则保留已保存结果并提示刷新失败。Cancel 不能撤销这次同步，普通 Save 才会提交其余窗口草稿。后台连接与同步配置不是运行时加载 `config.toml` 的另一条路径。

## 实现与验证入口

- `CopilotSettingsViewModel.ProfileManagement.cs`、`CopilotSettingsWindow.xaml.cs`：按钮保存、Profile 草稿和模型诊断入口；`Config/CopilotConfig.cs`：规范化、候选发布、schema 与凭据边界。
- `CopilotChatViewModel.ConfigPersistence.cs`、`Composer.cs`、`ConversationCommands.cs`：聊天中配置变更、三态反馈及命令回显；`State/CopilotConversationSession.cs`：只选择 Profile 的会话状态变化。
- `CopilotModelConnectionDiagnostic.cs`、`CopilotSettingsViewModel.ExternalMcp.cs` / `McpOperations.cs` / `BackendSync.cs`：真实联网入口；`Config/CopilotBackendSyncTransaction.cs`：托管 Profile 合并与发布。
- `CopilotConfigurationIsolationTests` 核对 ColorVision 配置不被外部 TOML 覆盖且仍发现指令；`CopilotBackendSyncTransactionTests` 覆盖锁文件保存失败、草稿隔离、未来 schema、凭据克隆、规范化列表和同步后通知失败；不代表模型或 MCP 实际连接成功。
- `CopilotChatConfigPersistenceTests` 覆盖推理配置落盘失败、成功重绑定及无 handler 的内存模式；`CopilotMcpClientConfigurationTests` 覆盖配置与未来 schema；`CopilotProfileConfigTests`、`CopilotConfigWebPageNetworkTests` 覆盖能力声明重置与 Web 配置路由／校验。

这些测试路径不是本次运行结果。设置窗口按钮的完整 WPF 交互、实际供应商连接、Local MCP 测试握手修复与真实后台同步仍需对应场景验证；不能用元数据／链接校验或某个单元测试通过替代。

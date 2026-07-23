# Copilot 扩展、MCP 与 Hook

## 业务模块 Agent 扩展

Flow、设备、模板、图像和其他业务项目不能反向引用主程序中的 `ICopilotTool`。需要把模块状态或窄业务动作交给 Agent 时，在 UI 公共契约层使用 `CopilotAgentExtensionRegistry.Shared.Register`，并持有返回的 `IDisposable`。注册项包含稳定 `SourceId`、显示名、版本、零到多个 `ICopilotContextProvider`，以及零到多个 `ICopilotModuleTool`；页面、编辑器或插件卸载时释放句柄，提供者、工具和 Capability Catalog 来源会一起撤销。注册表拒绝空扩展、重复来源、跨模块重复工具名、非法 Schema 和无界超时，桥接层还会报告与内置工具名冲突的能力并拒绝激活。

`ICopilotModuleTool` 是面向业务程序集的窄契约，不暴露模型 Profile、审批对象、会话 checkpoint 或完整 Agent 内部状态。模块只接收当前模式、用户目标、结构化参数、已采集上下文、搜索根和活动文档。只读模块工具映射成 `ReadOnly + Idempotent + SharedRead`；任何声明为 `Write` 的模块工具都由宿主强制映射成 `High risk + Always approval + NonIdempotent + Exclusive`，未收到 Agent Framework 对本次精确函数调用的原生批准时适配器直接拒绝，模块不能自行降低风险等级。

模块工具必须提供稳定名称、短说明和对象类型 JSON Schema。`SourceVersion`、工具类型、说明、安全元数据和 Schema 共同进入 Capability Catalog 指纹；模块升级、注销或语义改变会让旧 checkpoint 重新规划。动态上下文在 Agent 任务真正开始时从当前注册快照采集，适合窗口和编辑对象生命周期，不需要重建 Copilot 面板。`EditTemplateJson`、`FlowEngineManager`、`ServiceManager`、`DatabaseBrowserWindow`、检测结果页面与任务调度器已完成实际接入：模板编辑器 Loaded 时注册完整的当前模板 JSON 上下文，Unloaded 时注销；Flow 和设备服务单例各自只注册一次提供者，但在每次相关请求开始时通过 UI Dispatcher 重新读取当前对象，不复用旧快照。Flow 上下文只在 Diagnose、明确 Flow 意图或 Flow 仍是当前软件表面时采集，运行消息只保留最近 6,000 字符并统一脱敏。数据库浏览器、结果页面和调度器窗口使用公共的 `CopilotDynamicContextCoordinator<TSnapshot>` 管理会话：同一模块只注册一个来源，多个窗口或 Page 之间跟随最近激活实例，异步采集完成前若页面已经切换则丢弃旧快照。纯页面来源在最后一个页面关闭后自动注销；调度器保留一个管理器级会话，使用户不打开任务窗口也能通过明确调度意图查询最新聚合状态，窗口会话只在激活时覆盖为当前任务或历史页。

设备扩展会跟踪主显示区当前选中的设备，也会跟随服务管理窗口中的设备选择；用户点名唯一设备时优先读取该对象。单设备上下文包含心跳、MQTT 操作状态和最多 60 个简单配置字段，底层 CLR 字段名先完成 `SN`、token、license 等脱敏，再使用本地化显示名；设备名称、代码、主题、摘要和日志仍经过第二层内联脱敏。未锁定单设备时只提供最多 60 项的设备健康总览和完整在线/离线计数，不注入配置。设备重载、删除或切换显示面后旧对象不再被采集。当前阶段没有注册设备控制工具，原有 Live Context 继续负责 UI 位置摘要和用户显式附件，两者职责不同。

`/context` 本地诊断会列出当前模块扩展来源、版本、上下文提供者数量、工具已激活/声明数量以及最多八个冲突或发布问题；输出有数量和单行长度上限，不调用模型，也不写入会话历史。模块工具名与内置工具冲突时，诊断会保留来源和原因，实际工具不会进入 Agent 工具面。

Marketplace manifest 中的 `copilot_agents` 仍只用于宿主管理的受限只读子 Agent，不等同于任意模块工具授权。能够执行 `ICopilotModuleTool` 的 DLL 本来就在 ColorVision 进程内运行，应只来自与普通插件代码相同信任级别的已审核程序集；未来若要允许远程或低信任扩展，应使用 MCP 或独立 ServiceHost 能力面，不应复用进程内注册表冒充沙箱。

## 外部 MCP 工具发现

Copilot 使用官方 `ModelContextProtocol.Core` SDK 连接显式配置的 Streamable HTTP 服务。每次请求开始时发现工具，适配为 `ICopilotTool` 后再进入 Harness；外部工具不会绕过输入 Schema、调用次数、超时、并发闸门、生命周期事件、脱敏审计或审批机制。

- 当前只支持显式 Streamable HTTP 地址，不启动 stdio 命令或任意本地进程。
- 最多配置 8 个服务；单服务最多暴露 32 个工具，单请求最多合并 64 个外部工具。与内置工具重名时保留内置工具并记录诊断。
- 远程地址必须是 HTTPS；只有 loopback 允许 HTTP。URL 中不能嵌入用户名或密码。
- bearer token 只通过环境变量解析，配置文件不保存 token 值。
- 默认 `approval` 将整个服务视为高风险、可写、非幂等能力，每次精确调用都通过 `ApprovalRequiredAIFunction` 暂停并等待用户批准。
- 配置第五段可按远端工具精确名称建立白名单，并用 `tool=approval/read-only` 覆盖服务默认策略；只要存在白名单，未列出的工具就不会进入 Harness。留空或 `*` 为兼容模式，会暴露服务发现的全部工具。
- `read-only` 是操作者对对应工具做出的显式信任声明；此时工具按只读、幂等能力进入共享读取闸门。远端返回的 tool annotations 只作为提示，不用于放宽本地权限。
- 成功发现的协议 `Tool` 定义进入最多 32 个服务项、每个服务最多 512 个定义、默认 TTL 5 分钟的进程内缓存；缓存键包含服务、地址和 bearer token 的不可逆指纹，不保存 token 明文。缓存命中时使用官方 `McpClientTool(McpClient, Tool, ...)` 构造路径绑定到本轮新连接，不复用或常驻旧会话。
- 若服务通过 `ServerCapabilities.Tools.ListChanged` 声明支持工具列表变更，client lease 会注册 `notifications/tools/list_changed` 处理器；收到通知后立即使对应缓存失效，下一轮强制实时发现。处理器先于 client 释放，只在当前请求连接存续期间监听；TTL 和设置页 `Refresh Discovery` 仍是无活跃连接时的兜底。ColorVision 自带 MCP server 尚不能主动发送该通知，因此不会虚假声明此能力。
- 设置页 `Refresh Discovery` 强制绕过缓存。实时发现会比较有界能力签名、保留或递增服务能力 revision 并发出进程内变更事件；健康快照标明本轮使用 live 还是 cached discovery，并在通知已使缓存失效时显示需要实时刷新。
- MCP JSON Schema 会完整传给模型；本地先验证顶层必填字段、未知字段和参数总长度，嵌套约束仍由远端 MCP 服务权威校验。
- 工具发现会记录进程内健康快照，包括连接状态、发现数量和实际暴露数量；设置页只显示紧凑摘要，不把连接错误铺到主聊天区。
- 单个服务不可用只产生运行时诊断，不会终止 Agent 请求；普通聊天界面隐藏这类基础设施噪声。

官方 SDK 和传输说明见 [Model Context Protocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)。ColorVision 自带 MCP server 同时支持 `Content-Length` 与 HTTP chunked 请求正文，已通过官方客户端连接、工具发现和真实 `tools/call` 的端到端测试。

## Hook 扩展点

实现 `ICopilotToolExecutionHook` 可接入统一策略：

- `BeforeExecuteAsync`：权限判断、用户确认、速率限制、策略拒绝。
- `AfterExecuteAsync`：遥测、诊断记录、结果归档。

前置 Hook 拒绝或异常时工具不会运行；后置 Hook 异常只记录日志，不覆盖真实工具结果。Hook 应保持轻量，并避免保存未脱敏的参数。

## 当前边界与下一步

这是一套基础框架，不等同于 Codex、Claude Code 或 OpenCode 的完整能力。当前已采用相同的核心分工：稳定工具目录和 JSON Schema 交给模型选择，宿主执行确定性的参数校验、权限、审批、隔离、审计与结果回传；关键词策略仅保留在确实需要缩减外部/动态能力的边界，不再承担核心诊断工具路由。后续按优先级扩展：

1. 先打磨主程序中的 Agent 基础闭环，包括运行状态、暂停/恢复、失败恢复、审批一致性、上下文预算和可测试性。业务模块接入暂缓，尤其不直接改动 `Plugins/**` 中的扩展版本；只有基础契约稳定且出现经过验证的高频需求后，才重新评估模块接入。
2. `/context` 已展示模块来源、版本、上下文提供者、已激活工具和冲突原因；下一步只在出现真实启停需求后再增加设置管理页，避免让诊断信息提前膨胀成第二套插件管理器。
3. 将同一 Capability Fabric 扩展到 LAN 和 ServiceHost，保持能力白名单、身份、审批、evidence 和审计语义一致；跨进程能力不直接复用进程内模块注册表。
4. 通用 Shell 在没有系统沙箱时继续保持每次原生审批。当前 runner 已完成 Job Object 进程树归组；后续再增加受限 Windows 身份、文件系统与网络边界，形成真正的系统沙箱。

框架中间件与函数调用层的设计参考 [Microsoft Agent Framework Middleware](https://learn.microsoft.com/en-us/agent-framework/agents/middleware/)；请求预算中间件使用官方建议的 [DelegatingChatClient](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.delegatingchatclient?view=net-11.0-pp) 组合方式。

相关测试集中在 `CopilotCoreRuntimeTests`、`CopilotAgentExtensionRegistryTests`、`CopilotFlowContextProviderTests`、`CopilotDeviceContextProviderTests`、`CopilotDatabaseContextProviderTests`、`CopilotMeasurementResultContextProviderTests`、`CopilotSchedulerContextProviderTests`、`ProjectARVRCopilotContextTests`、`CopilotExploreSubagentTests`、`CopilotScoutSubagentTests`、`CopilotSubagentRoleRegistryTests` 与 `CopilotToolExecutorTests`。

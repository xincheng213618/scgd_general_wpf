---
knowledge_id: "copilot.mcp-server"
knowledge_type: "reference"
status: "current"
summary: "ColorVision 入站本地 MCP 的连接与会话、工具和资源、工作区读取、两阶段确认及菜单写入边界。"
aliases: ["ColorVision 本地 MCP", "Local MCP", "Codex 如何连接 ColorVision", "MCP 工具能否执行流程", "CopilotMcpServer", "CopilotMcpRequestHandler", "CopilotMcpClientSessionStore", "CopilotMcpToolDispatcher", "Mcp-Session-Id", "MCP-Protocol-Version", "MCP SSE", "MCP 会话容量", "-32013", "Safety boundary", "confirm_action", "arguments_digest", "get_enabled_tools", "get_agent_task_events", "agent_task_events_scope_required", "workspace_scope_changed", "no_allowed_roots", "execute_menu", "create_flow", "Copy Codex Config", "Copy Token Command"]
code_paths: ["ColorVision/Copilot/Mcp/", "ColorVision/Copilot/Capabilities/", "ColorVision/Copilot/CopilotExecutionScope.cs", "ColorVision/Copilot/Config/CopilotConfig.cs", "ColorVision/Copilot/CopilotMcpSettingsControl.xaml", "ColorVision/Copilot/CopilotSettingsViewModel.cs", "ColorVision/Copilot/CopilotSettingsViewModel.McpProperties.cs", "ColorVision/Copilot/CopilotSettingsViewModel.McpOperations.cs"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotMcpServerLifecycleTests.cs", "Test/ColorVision.Copilot.Tests/CopilotMcpRequestIdentityTests.cs", "Test/ColorVision.Copilot.Tests/CopilotMcpPathSecurityTests.cs", "Test/ColorVision.Copilot.Tests/CopilotMcpWorkspaceSnapshotCaptureTests.cs", "Test/ColorVision.Copilot.Tests/CopilotMcpConfirmationDecisionTests.cs", "Test/ColorVision.Copilot.Tests/CopilotMcpConnectionDiagnosticTests.cs", "Test/ColorVision.Copilot.Tests/CopilotSharedCapabilityInputContractTests.cs"]
related: ["copilot.runtime", "copilot.extensions", "copilot.configuration", "copilot.tool-contracts"]
---

# ColorVision 本地 MCP

ColorVision 本地 MCP 让同一台计算机上的 MCP 客户端读取应用上下文、日志、文档和允许范围内的文件，也提供面板导航、主题切换以及需确认的模板、流程和菜单操作。服务默认关闭，启用后仅监听 IPv4 loopback，并要求 Bearer token。连接授权覆盖该服务暴露的工具，并不等于只有读取权限。

本页描述 ColorVision **作为 MCP server 的入站接口**。Copilot 连接其他服务的出站配置见[外部 MCP 工具发现](./copilot-agent-extensions.md#外部-mcp-工具发现)；两者独立启用，不能互相继承审批或会话。

## 启用与连接

前提是 ColorVision 正在运行，客户端支持下述 JSON HTTP 请求和会话握手。启用服务会开始本机监听；复制配置不代表已经连接。

1. 在 Copilot 设置的本地 MCP 区域勾选 **Enable local MCP server**。确认 `Port`、`Endpoint` 和 `Bearer token`，保存设置后应用到监听器。默认端口为 `38473`，有效范围为 `1–65535`；`McpEnabled`、`McpPort`、`McpBearerToken` 保存在 `CopilotConfig`。
2. 使用 **Copy Codex Config** 复制当前草稿端点的配置片段，按客户端的配置入口保存。设置页生成的默认片段如下：

   ```toml
   [mcp_servers.colorvision]
   url = "http://127.0.0.1:38473/mcp"
   bearer_token_env_var = "COLORVISION_MCP_TOKEN"
   ```

3. 让客户端进程取得与已保存设置一致的 token。**Copy Token Command** 复制的是写入 Windows 用户环境变量的命令，不会替你执行。下面仅为占位示例，实际 token 不应写入仓库、问题报告或普通日志：

   ```powershell
   [Environment]::SetEnvironmentVariable('COLORVISION_MCP_TOKEN', '<ColorVision 设置中的 token>', 'User')
   ```

   用户环境变量的修改不会更新已运行进程的环境；重新启动客户端，并确认启动它的进程取得了新值。服务本身不读取 `COLORVISION_MCP_TOKEN`，它核对的是请求头 `Authorization: Bearer <token>` 与 `CopilotConfig` 中的值。
4. 使用 **Test Connection** 检查握手和只读 `get_server_status`，再由外部客户端建立自己的会话。该按钮使用当前草稿端口和 token，不自动保存或启用服务；具体诊断预算与保存边界见[配置与连接诊断](./copilot-configuration.md#诊断、发现与同步的副作用)。

更换端口会重启监听器；同端口更换 token 会清除现有会话。关闭服务、重启应用或会话过期后，客户端需要重新初始化。`Regenerate` 先修改设置草稿，保存后才影响正在运行的服务。

## HTTP 协议与会话

端点是 `http://127.0.0.1:<port>/mcp`。当前实现返回 JSON，使用 MCP 会话头；它不是完整的 Streamable HTTP 功能实现，不能据此假定支持 SSE、订阅或流恢复。

| 请求 | 当前行为 |
| --- | --- |
| `POST initialize` | 单独的 JSON-RPC 2.0 请求；不得附带已有 `Mcp-Session-Id`。`params` 需含非空 `protocolVersion`、对象 `capabilities` 以及非空 `clientInfo.name` / `version`；成功从响应头取得随机会话 ID |
| 后续请求 | 携带相同 Bearer token 和 `Mcp-Session-Id`，先发送 `notifications/initialized`。服务支持的协议版本是 `2025-03-26`；后续 `MCP-Protocol-Version` 头可省略，提供时必须与此值一致 |
| JSON-RPC 方法 | `ping`、`tools/list`、`tools/call`、`resources/list`、`resources/read`。初始化后的非空批量请求顺序处理；普通 `notifications/initialized` 返回 HTTP `202` 空正文 |
| `GET /mcp` | 也需要认证、有效会话和匹配的可选版本头，只返回 JSON 状态，不建立 SSE 流 |
| 其他 HTTP 方法 | 有效会话下返回 `405`；没有 `DELETE` 主动关闭会话接口 |
| 响应判定 | HTTP `200` 不等于业务成功。工具结果在 `content[].text` 中，`isError=true` 表示失败或等待确认；资源读取失败返回 JSON-RPC error |

`initialize` 返回固定 `protocolVersion=2025-03-26`，并不要求客户端传入的版本与它相同；客户端应检查协商结果。`serverInfo.version=1.0.0` 是接口声明值，不是 ColorVision 安装版本。服务接收 initialized 通知，但没有将其作为工具调用的独立状态门禁。

会话保存在进程内，最多 `256` 个，空闲 `30` 分钟失效，按请求解析时清理过期项。会话绑定创建它的网络来源；两个同为 `127.0.0.1` 的客户端仍取得不同随机 ID 和派生身份。更换会话不能继续使用旧会话的审批动作，token 相同也不能转移批准。

监听器最多同时处理 `16` 个 TCP 客户端，超额连接直接关闭。请求头上限 `64 KiB`，请求体上限 `1 MiB`，支持 Content-Length 或 chunked 请求体；同一请求同时声明两者会被拒绝。每个连接有 `30` 秒的读取、分派与写回复总预算，响应使用 `Connection: close`。这些连接限制与工具自身超时共同生效；超时、取消或断线不证明已开始的操作没有产生影响。

## 工具与资源

先通过 `tools/list` 获取当前名称与 Schema，或调用 `get_enabled_tools` 查看分类、风险标签及示例。列表表示工具已注册；活动编辑器、工作区、数据库和审批条件仍可能使单次调用不可用。`tools/call.params.arguments` 应为 JSON 对象，字段按所列 Schema 提供。

### 工具用途与效果

| 用途 | 工具 | 结果与边界 |
| --- | --- | --- |
| 服务和运行环境 | `get_server_status`、`get_enabled_tools`、`get_runtime_environment_summary` | 监听器、活动/排队运行数量、运行环境等快照，不输出 token |
| 调用诊断 | `get_audit_log`、`get_audit_summary`、`get_last_tool_error`、`get_diagnostic_bundle` | 当前 MCP 会话的调用审计；诊断包另含应用日志、运行环境、实时上下文和流程摘要，默认最多 `12000` 字符，可设 `1000–60000`，不含 Agent journal |
| 应用与模板上下文 | `get_live_context`、`get_workspace_context`、`get_active_template_context`、`get_saved_template_context`、`get_template_type_context` | 读取已发布或已加载的内存状态。已保存模板用精确 `template_code` / `template_name` 定位；类型查询只需 code，返回字段结构和已加载名称，不返回参数值，不主动查询数据库 |
| 日志与产品文档 | `get_recent_log`、`search_docs` | 前者读取应用日志；后者检索已发布的在线文档索引，可能联网，不等于检索当前源码检出版本 |
| 工作区文件 | `search_files`、`grep_text`、`read_allowed_file`、`list_allowed_directory` | 受允许根约束的搜索、文本读取和目录分页，见下文 |
| 流程诊断 | `get_flow_summary`、`diagnose_flow_failure`、`get_flow_graph`、`get_flow_node_catalog`、`preview_flow_action` | 读取当前流程、节点、图 revision 与可用类型；预览动作只返回说明，即使名称是 `select_node` / `open_node_property` 也不实际选择或打开界面 |
| 模板建议和预览 | `suggest_template_patch`、`preview_template_patch` | 建议字段或计算差异，不修改编辑器；满足活动编辑器条件的预览会在内存保存 `preview_id` |
| 流程预览和修改 | `preview_flow_patch`、`apply_flow_patch` | 单项 `add_node`、`set_property` 或 `connect`；应用需确认且复核 `expected_revision`，成功只修改当前编辑器，不自动保存或运行 |
| 模板应用 | `apply_template_patch` | 需要有效 `preview_id` 和确认；修改活动编辑器及绑定内存参数，不自动保存到数据库 |
| 创建流程 | `create_flow` | 确认后走 `TemplateFlow.Create` 创建空流程，**会写入数据库**并刷新流程列表；不是临时编辑器预览，不运行该流程。未指定名称时由应用生成名称，重名返回 `flow_name_exists` |
| 面板与主题 | `open_panel`、`set_theme` | 直接执行，不创建二次确认。主题切换会更新主题配置并调用配置保存 |
| 菜单与语言 | `execute_menu`、`set_language` | 菜单默认 dry-run，实际执行按匹配菜单的风险分类处理；语言修改需确认，可能触发现有重启提示，见下文 |
| 执行批准 | `confirm_action` | 只消费相同会话、工作区和参数绑定的已批准动作 |
| Agent 事件 | `get_agent_task_events` | 标准 HTTP 外部会话没有 Copilot conversation 绑定，返回 `agent_task_events_scope_required`；工具出现在列表中不代表可以读取进程级任务历史 |

### 只读资源

每个 URI 都使用完整的 `colorvision://` 前缀。资源返回请求时的快照，不订阅后续变化；读取本身也会形成调用审计。

| URI | 内容 |
| --- | --- |
| `colorvision://live-context/current` | 当前已发布业务上下文 |
| `colorvision://workspace/current` | 解决方案目录、活动文档和允许搜索根 |
| `colorvision://logs/recent` | 应用近期日志 |
| `colorvision://template/current` | 活动模板 JSON 编辑器上下文 |
| `colorvision://flow/current` | 活动流程与节点摘要 |
| `colorvision://mcp/audit-summary` | 当前会话审计、失败及待批准计数 |
| `colorvision://mcp/audit-log` | 当前会话近期调用记录 |
| `colorvision://copilot/capabilities` | JSON 能力目录，包含内置及已发现的 Copilot 能力元数据；不是本入站服务 `tools/list` 的同义列表，也不会自动把所有能力变成可调用 MCP 工具 |
| `colorvision://copilot/task-events` | JSON Agent journal 入口；与同名用途工具一样，标准外部会话因未绑定 conversation 而拒绝读取 |

业务上下文是已发布的结构化文本，不包含图像像素，也不代表每次读取都会重新采集设备。发布和刷新机制见[业务上下文](./copilot-agent-extensions.md#业务模块-agent-扩展)。Agent journal 的保存、过滤和有界查询见[结构化任务事件](./copilot-agent-tool-contracts.md#结构化任务事件-journal)。

## 工作区读取范围

`get_workspace_context` 显示的是 **ColorVision 当前解决方案**与活动文档，不是外部客户端自己的工作目录。默认允许根来自现有解决方案目录，以及活动文档所指目录或文件的父目录；活动文档可能使解决方案外的目录也成为允许根。没有有效根时返回 `no_allowed_roots`，不会回退到任意磁盘位置。

- 相对路径以第一个允许根解析，不会逐个根尝试；需要访问其他允许根时传其完整路径。路径规范化后必须仍位于允许范围，重解析点路径会被拒绝。
- `search_files` 查字面文件名/路径片段；`grep_text` 查不区分大小写的单行字面文本，不执行正则或 Shell。分页 `cursor` 是不透明续页值，保持原查询和范围后原样传回。
- `read_allowed_file` 必须明确给出单一 `path`，只读取支持的文本扩展名，可按 Schema 给出行列范围；不能用它读 `.stn`、图片或任意二进制。`list_allowed_directory` 不传 path 时列允许根，续页时需要明确目录。
- 文件调用会重查当前工作区。一次调用内解决方案发生切换时返回 `workspace_scope_changed`，需在新上下文重新查询。UI 线程繁忙导致默认 `2` 秒内未取得工作区快照时，可能返回空范围，而不是证明磁盘上没有文件。

这些根限制针对文件工具。应用日志、已加载模板和实时上下文各有自己的来源，不能把“受限文件读取”理解为所有返回内容都来自解决方案目录。

## 应用操作与两阶段确认

### 面板、主题和菜单

`open_panel` 只接受别名：`copilot` 打开 Copilot，`log` 打开日志，`solution` / `config` / `template` 都显示 `ProjectPanel`，`device` 显示 `AcquirePanel`。它们不是打开任意窗口或设备指令；面板未注册时返回 `panel_not_registered`。`set_theme` 直接修改并保存主题配置，不能把整个 MCP 服务描述为“不修改配置”。

`execute_menu` 的 `dry_run` 默认为 `true`，返回匹配菜单、候选、可执行状态和风险，不执行命令。`dry_run=false` 时，低风险菜单可直接调度；需要确认的菜单创建待批准动作，批准后通过 `confirm_action` 调度。无法唯一匹配、匹配不够明确或 `CanExecute` 为 false 时不会调度。

菜单来自当前可见的主窗口/全局菜单，风险按标题、路径、GUID 和类型名称中的词语分类，未知类别默认需确认。这是通用菜单调用，**没有逐项禁止设备控制、流程运行、文件写入或删除的白名单**。确认绑定的是查询参数，执行时还会重新解析当前菜单；返回 `scheduled` 仅表示安排了 UI 命令，不能当作菜单业务已成功完成。需要评估具体命令效果及当前界面结果，不能由风险标签推断绝无副作用。

专用流程诊断和 `preview_flow_action` 拒绝 start/stop/run，图补丁也不运行流程；这些限制不能推广为通用菜单绝不会触发流程。接口没有接受任意 Shell/PowerShell/Python 文本的专用命令工具。

**已知提示不一致：**`get_server_status` 的固定 `Safety boundary` 文本仍声称不修改配置、不控制设备、不执行流程等，与主题保存及通用菜单的实际执行路径不一致。该文本不是权限门禁，应依据本页的工具效果和具体处理器判断；文档核验不代表这一运行时提示已修复。

### 从预览到执行

先读状态与工作区，再取得相关上下文和预览。模板与 Flow 使用不同的预览凭据：

| 操作 | 调用顺序与条件 |
| --- | --- |
| 模板 JSON | 可先用 `suggest_template_patch`，然后 `preview_template_patch` → `apply_template_patch { preview_id }` → ColorVision 批准 → `confirm_action`。预览默认有效 `10` 分钟；只有绑定活动编辑器的预览能应用，显式传非空 `current_json` 仅供离线预览 |
| Flow 图 | `get_flow_graph` 取得 revision；按需要查询 `get_flow_node_catalog`；`preview_flow_patch` → `apply_flow_patch` → 批准 → `confirm_action`。apply 使用操作参数和 `expected_revision`，会自行重新预览，不接收模板的 `preview_id` |
| 语言、创建流程、需确认菜单 | 调用目标工具取得待批准动作 → 在 ColorVision 核对效果并批准 → `confirm_action` |

模板冲突校验、内存修改与保存边界见[模板 JSON 预览与应用](./copilot-agent-tool-contracts.md#模板-json-预览与应用)；Flow 节点类型、属性、端口和 revision 契约见[Flow 图语义与受保护编辑](./copilot-agent-tool-contracts.md#flow-图语义与受保护编辑)。预览是审查数据，不是授权，也不能跨表面复用批准。

待确认响应的错误码为 `confirmation_required`，风险值为 `confirmation-required`，并含 `action_id`、`tool_name`、`arguments_summary`、`arguments_digest` 与 `expires_at`。它表示等待用户，不是操作已失败执行或已完成。

1. 保留原始响应，在 ColorVision 的 Pending Actions 区域审阅来源、工作区、影响和参数后批准。外部客户端的自然语言“已同意”或本地客户端自己的确认不代替 ColorVision 批准。
2. 在原会话中调用 `confirm_action`，原样提交 `action_id`、`tool_name`、`arguments_digest`。摘要只用于展示，不能替代完整规范化参数的 SHA-256 绑定。
3. 核对工具结果与实际应用状态。动作默认从创建起有效 `5` 分钟，批准不延长有效期；执行前重查工具名、参数指纹、调用方、工作区、作用域和状态。执行中的或已执行动作不能重放，即使执行结果失败也不能重新消费同一动作。

会话过期、工作区切换、动作过期、模板内容变化或 Flow revision 变化后，需要重新读取、预览并取得新批准。传输中断或超时时先检查应用状态，不盲目重试写入。内置 Agent 使用自己的原生审批恢复链，详见[工具审批契约](./copilot-agent-tool-contracts.md#原生审批与参数快照)。

## 审计、输入与错误处理

外部会话的审计、审计摘要、最近工具失败和待审批计数按派生 session identity 隔离；通用审计不返回可复用 action ID 或参数指纹。应用日志和业务上下文仍是应用级数据，不因审计隔离就变成客户端专属数据。

原始 session ID 不写入普通审计；token、密码、Authorization 等采用字段和文本规则脱敏，检测到敏感确认参数时会拒绝创建动作。此类规则不能保证识别任意形式的秘密，不应主动将凭据放进查询、模板值或诊断文本。

Dispatcher 在处理器和审批创建前按 Schema 校验对象参数；非法嵌套字段、类型、必填项、范围和枚举返回 `invalid_arguments`。HTTP 包装层存在另一个边界：缺少或非对象 `arguments` 会先转为空对象，大小写冲突的重复键可能使连接因解析异常关闭。因此客户端应发送无重复字段的对象，不能依赖所有畸形 HTTP 输入都得到同一种 Schema 错误。

HTTP 工具回复只投影 `text` 与 `isError`，不单独序列化内部 `ErrorCode` / `FailureKind`；资源失败统一使用 JSON-RPC `-32002` 并附说明。下表的工具错误码用于对应源码和审计诊断，不能假定回复中必有 `errorCode` 字段。需要进一步定位时，可在同一会话读取 `get_last_tool_error` 或 `get_audit_log`。

| 现象或错误 | 检查与处理 |
| --- | --- |
| 连接拒绝、MCP disabled、端口不可用 | 确认应用仍在运行、设置已保存、端口未占用；监听启动失败时读设置页状态。关闭监听时可能直接连接失败，不一定有 HTTP 错误体 |
| `401` | 核对请求 Bearer token 与已保存 token；检查客户端是否仍持有旧环境值 |
| `400 / -32010`、`404 / -32011` | 前者缺少会话头；后者会话未知、过期或来源不符。重新单独 initialize，不要在新握手上附旧会话头 |
| `400 / -32014` | 后续版本头不受支持；检查 initialize 返回的 `2025-03-26` |
| `503 / -32013` | 会话容量已满；复用已有会话，或等待过期。频繁 Test Connection 也会创建新会话 |
| `tool_not_found`、`invalid_arguments` | 重新读取 `tools/list`，使用正式工具名、对象参数与当前 Schema |
| `no_allowed_roots`、`workspace_scope_changed`、`path_not_allowed` | 查 `get_workspace_context`，确认活动解决方案、根目录、路径和 UI 响应；不要扩大到任意路径绕过限制 |
| `agent_task_events_scope_required` | 普通外部会话未绑定 Copilot conversation；不能通过传一个 conversation 参数取得权限，应在应用内查看该任务诊断 |
| `confirmation_required`、`action_pending` | 到 ColorVision 审阅并批准，再从原会话 confirm；反复提交确认不会代替用户批准 |
| `action_source_mismatch`、`action_workspace_mismatch`、`action_scope_mismatch`、`action_arguments_mismatch` | 旧动作与当前调用不匹配，不能替换 digest 或迁移批准；在正确上下文重新提出动作 |
| `action_expired`、`action_already_executed` | 过期需重新申请；已执行先核实实际结果，避免重复副作用 |
| `template_patch_preview_required`、`template_patch_conflict`、`template_context_mismatch` | 打开目标 JSON 编辑器重新预览；离线 JSON 预览不授予编辑器写入权限 |
| `flow_execution_not_supported`、`flow_patch_failed` | 前者属于专用诊断预览的运行限制；后者按返回信息核对活动编辑器、运行状态、revision、类型/属性/端口 |
| `tool_execution_timeout`、连接在响应前关闭 | 调用超时或取消可能留下已完成、仍在进行或未知结果；检查实际状态后再决定是否重试 |

## 实现与验证入口

| 责任 | 入口 |
| --- | --- |
| 监听、HTTP、握手及会话 | `CopilotMcpServer`、`CopilotMcpRequestHandler`、`CopilotMcpClientSessionStore` |
| 工具与资源列表、Schema、调用和审计 | `CopilotMcpToolDispatcher` 的 `Descriptors`、`Resources`、`Invocation`、`Diagnostics` 分部 |
| 工作区捕获与路径授权 | `CopilotMcpToolEnvironment`、`CopilotMcpWorkspaceSnapshotCapture`、dispatcher `Search` 分部 |
| 确认和执行绑定 | `CopilotExecutionScope`、`CopilotMcpConfirmationStore`、dispatcher `Confirmation` 分部 |
| 菜单解析与真实副作用 | `CopilotMenuToolSupport`、`CopilotApplicationCapability.ExecuteMenuAsync` 与具体菜单命令 |

共享工具的定义、策略、路由、Schema 一致性和 Agent trace 由[共享能力契约](./copilot-agent-tool-contracts.md#agent-与-mcp-的共享能力定义)统一维护；新增本地工具仍需在 `CopilotMcpToolDefinition` 集合绑定 descriptor 与 handler。新增业务快照按[业务上下文扩展](./copilot-agent-extensions.md#业务模块-agent-扩展)接入，不另建 MCP 专属事实副本。

相关测试包含：`CopilotMcpRequestIdentityTests` 的认证、会话隔离和事件拒绝；`CopilotMcpPathSecurityTests` / `CopilotMcpWorkspaceSnapshotCaptureTests` 的根边界与繁忙 UI；`CopilotMcpConfirmationDecisionTests` 的作用域、完整参数绑定和审计载荷；`CopilotMcpServerLifecycleTests` 的退出取消；`CopilotMcpConnectionDiagnosticTests` 的握手及状态响应校验。共享输入测试见 `CopilotSharedCapabilityInputContractTests`。

下面是实现变更后的可选验证命令，会构建测试依赖，部分用例会启动临时 loopback 监听器、WPF Dispatcher 并创建临时文件；文档修改不要求为验证文字而启动服务：

```powershell
dotnet test .\Test\ColorVision.Copilot.Tests\ColorVision.Copilot.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~CopilotMcp|FullyQualifiedName~CopilotApprovalReview" -v minimal
```

这些测试不等于真实客户端互通、所有菜单风险分类、数据库写入或设备行为的端到端验收。网站与检索通过也只验证文档结构和可发现性。

# ColorVision 本地 MCP

ColorVision 本地 MCP 让 Codex 或其他 MCP 客户端在本机读取正在运行的 ColorVision 上下文，用于诊断、导航、日志查看、文档搜索和有限的低风险 UI 操作。它默认关闭，只绑定 loopback，并使用 bearer token 认证。本页是维护入口，源码以 `ColorVision/Copilot/Mcp/` 和 `ColorVision/Copilot/Capabilities/` 为准。
本页描述的是 ColorVision 作为 MCP server 的入站接口。Copilot 作为 MCP client 连接其他 Streamable HTTP 服务的配置、权限和执行链，参见 [外部 MCP 工具发现](./copilot-agent-extensions.md#外部-mcp-工具发现)。两者可以独立启用。

## 核心边界

| 规则 | 说明 |
| --- | --- |
| 默认关闭 | 用户需要在 Copilot 设置里启用并保存 |
| 仅本机 | 默认端点是 `http://127.0.0.1:38473/mcp` |
| 需要 token | token 存在 Copilot 设置中，外部客户端通过 `COLORVISION_MCP_TOKEN` 读取 |
| 每客户端会话 | `initialize` 后由响应头签发 `Mcp-Session-Id`，后续请求必须原样携带 |
| 先诊断后操作 | 优先读状态、上下文、日志、文档和文件 |
| 高风险不开放 | 不控制设备、不执行流程、不跑 shell、不删文件、不读任意路径 |
| 可确认操作要二次授权 | MCP 只创建待确认动作，用户必须在 ColorVision 里批准后才能 `confirm_action` |

Codex 自己可能有本地命令行能力，那不是 ColorVision MCP 暴露的工具。不要把 shell、PowerShell、Python 或任意命令执行写进本协议。
## 启用

在 ColorVision 的 Copilot 设置里启用 `Local MCP`，确认端口，默认 `38473`，保存后本地 server 会立即应用设置。外部 MCP 客户端连接时使用：

```toml
[mcp_servers.colorvision]
url = "http://127.0.0.1:38473/mcp"
bearer_token_env_var = "COLORVISION_MCP_TOKEN"
```

```powershell
[Environment]::SetEnvironmentVariable("COLORVISION_MCP_TOKEN", "<token from ColorVision settings>", "User")
```
修改 Codex 配置或环境变量后，需要重启 Codex 会话。
服务使用 Streamable HTTP 会话语义：客户端先发送单独的 `initialize` 请求，从响应头取得随机的 `Mcp-Session-Id`，随后在 `notifications/initialized`、工具和资源请求中携带该头。成熟 MCP 客户端会自动处理该握手。会话只保存在 ColorVision 进程内，绑定创建它的网络来源，空闲 30 分钟、服务停止/重启或 bearer token 变化后失效；客户端收到缺少会话的 `400` 或会话失效的 `404` 时应重新初始化。原始 session ID 不进入审批 UI、普通审计或日志。

## 调用顺序

| 步骤 | 调用 | 目的 |
| --- | --- | --- |
| 1 | `get_server_status` | 确认 MCP 是否启用、认证是否成功、server 是否运行 |
| 2 | `colorvision://live-context/current` | 获取当前 Copilot 上下文 |
| 3 | `colorvision://workspace/current` | 确认当前工作区和允许的文件根 |
| 4 | `get_recent_log` / `get_diagnostic_bundle` | 聚合日志和运行时信息 |
| 可选 | `colorvision://copilot/task-events` / `get_agent_task_events` | 仅在排查 Agent 运行、工具、审批或停止原因时读取 |
| 5 | `search_docs` / `search_files` / `grep_text` | 定位产品行为、源码或配置 |
| 6 | `diagnose_flow_failure` | 诊断流程失败，不启动、不停止、不重跑流程 |
| 7 | `open_panel` / `execute_menu` dry-run | 只做低风险导航或预览 |
| 8 | `suggest_template_patch` / `preview_template_patch` | 生成模板修改建议和预览 |
| 9 | `apply_template_patch` + 用户批准 + `confirm_action` | 只对已预览、已批准、参数匹配的模板 patch 生效 |

工具返回 `confirmation-required` 时，客户端应把 `action_id`、`tool_name`、用于展示的 `arguments_summary` 和绑定完整规范化原始参数的 `arguments_digest` 交给用户确认。用户在 ColorVision 待确认区批准后，客户端必须原样提交 `action_id`、`tool_name` 与 `arguments_digest` 调用 `confirm_action`；摘要不会作为执行授权。

## 工具和资源

| 分类 | 工具或 URI | 风险 |
| --- | --- | --- |
| 状态/审计 | `get_server_status`、`get_enabled_tools`、`get_runtime_environment_summary`、`get_audit_log`、`get_last_tool_error`、`get_agent_task_events` | 只读 |
| 上下文 | `get_live_context`、`get_workspace_context`、`get_active_template_context`、`get_flow_summary`、`diagnose_flow_failure` | 只读 |
| 搜索和文件 | `get_recent_log`、`search_docs`、`search_files`、`grep_text`、`read_allowed_file`、`list_allowed_directory` | 只读，仅限允许根 |
| 模板预览 | `suggest_template_patch`、`preview_template_patch` | 只读 |
| 应用操作 | `open_panel`、`set_theme` | 低风险 |
| 待确认操作 | `execute_menu`、`apply_template_patch`、`set_language`、`confirm_action` | 需要用户批准 |
| 资源 | `colorvision://live-context/current`、`workspace/current`、`logs/recent`、`template/current`、`flow/current`、`mcp/audit-*`、`copilot/capabilities`、`copilot/task-events` | 只读快照 |

工具列表和路由都由 `CopilotMcpToolDefinition` 集合生成；每项把 descriptor 与 handler 绑定在一起，不再维护第二份路由名称表。共享项通过 `SharedDefinition` 只声明目录项和 handler，名称、说明、Schema、分类、调用示例与风险均从目录生成。新增或删除工具时修改该定义集合和测试，再同步本页。统一调用入口在路由到 handler 之前执行 descriptor 的冻结 Schema：拒绝未知参数、缺失必填参数、类型错误、上下界/数组长度错误和非法枚举，返回可审计的 `invalid_arguments`，不会创建确认动作或触发应用状态。与内置 Agent 重叠的能力名称、两侧说明、两侧 Schema、MCP descriptor 元数据、执行路由、Agent trace 展示和执行策略必须经过 `CopilotSharedCapabilityCatalog`；具体 Agent 类、MCP descriptor 和 trace presenter 不再内联重复说明、Schema、调用提示、展示分类或风险字符串。16 项同形能力复用同一个 Schema，文件读取和菜单执行的两个安全/批处理差异则在目录中显式记录原因。目录集中持有 18 项共享能力的 Agent Descriptor，包括访问、风险、审批、幂等性、并发、超时、审计和 evidence 模式；Agent 工具运行时直接引用该对象，MCP 风险标签从同一策略派生。标记为 `ApplicationCapabilityRuntime` 的 12 项能力由 Agent wrapper 与本机 MCP 请求处理器共用进程级默认 dispatcher；自定义 environment 只能通过显式注入形成隔离实例。文档与日志读取标为 `SurfaceCapabilityAdapter`，共享业务能力但保留各自协议投影；四个文件系统读取能力标为 `WorkspaceAuthorizationAdapter`，只保留 Agent 本轮证据根与外部 MCP workspace 根的授权差异。每个共享能力必须显式声明一种非空执行路由，搜索算法和规范结果文本仍由同一 capability 生成。两个组合根启动时再次核对说明、Schema、MCP 分类/调用示例、完整 Agent 策略、执行路由与 MCP 风险标签，发现漂移就拒绝启动该工具面。`colorvision://copilot/task-events` 和 `get_agent_task_events` 只读取当前选中会话最近一次已保存的有界、脱敏 Agent journal。开始新一轮时保留上一安全点，直到新的增量 checkpoint 原子替换它；运行终态再由会话聚合根一次提交。默认 diagnostic bundle 不包含该数据。查询支持事件类型、run、工具、subject/related ID 和序号游标，但不会返回工具参数、steering 原文或可复用审批。

## 业务上下文扩展

流程、设备、图像和模板通过 `CopilotBusinessContextCoordinator` 发布同一种 `CopilotBusinessContextBundle`。新增界面上下文时优先实现 `ICopilotBusinessContextSource`，只提供结构化快照，使用 `CopilotBusinessContextBuilder` 脱敏，并让发布和发送复用同一个 bundle。
诊断入口默认使用 `CopilotPromptMode.Diagnose`。外部 MCP 模板写入保持 `suggest_template_patch -> preview_template_patch -> apply_template_patch -> 用户批准 -> confirm_action`；内置 Agent 的 `TemplatePatch` 工具复用相同预览和冲突校验，但其待确认动作在 ColorVision 用户批准后直接应用到未保存的编辑器。MCP 构造时从同一组定义生成描述和路由，并校验名称唯一性；共享 capability 还会校验 Agent 与 MCP 两侧均存在。

## 安全要求

明确不支持设备控制、流程启动/停止/重跑、任意 shell/cmd/PowerShell/batch/Python 或进程执行、文件删除、任意路径读取、配置静默修改、二进制图片通过上下文快照上传。

文件工具只允许读取规范化后的 ColorVision 工作区根内文本文件。每个已初始化客户端使用由随机 session ID 派生的不可逆 caller identity；即使两个客户端都来自 `127.0.0.1`，一个客户端也不能确认或执行另一个客户端创建的动作。审计、审计摘要和待审批计数按该会话隔离；未绑定到应用内 Copilot 对话的外部 MCP 会话不能读取进程级 Agent 任务事件。确认动作会在本地记录 `action_id`、工具名、风险、过期时间、脱敏参数摘要和完整参数的 SHA-256 指纹；执行时使用固定时间比较核对该指纹和 caller identity。通用 MCP 审计资源只标记是否为审批事件，不返回可复用的 `action_id` 或参数指纹；客户端只能从自己创建动作的原始响应取得确认载荷。token、原始 session ID、密码、API key、Authorization、bearer secret 等敏感值不会进入待确认动作。

## 排查

| 现象 | 先查 |
| --- | --- |
| MCP disabled | Copilot 设置是否启用并保存 |
| 端口不可用 | Copilot 设置里的端口是否被占用 |
| 401 或 token mismatch | `COLORVISION_MCP_TOKEN` 是否和 ColorVision 当前 token 一致 |
| 400 缺少会话 / 404 session expired | 完成或重新执行 `initialize`；检查 ColorVision 是否重启、token 是否变化或会话是否空闲超过 30 分钟 |
| Codex 看不到 server | Codex 配置是否重启生效，URL 是否和 ColorVision 端点一致 |
| 工具要求确认 | 用户是否在 ColorVision Copilot 待确认区域批准 |
| 模板 patch 无法应用 | 当前活动模板编辑器是否还是 preview 时的同一个编辑器和 JSON 快照 |
| 流程相关请求被拒绝 | MCP 只诊断和预览，不执行流程 |

## 验证

```powershell
dotnet test Test/ColorVision.Copilot.Tests/ColorVision.Copilot.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~CopilotMcp|FullyQualifiedName~CopilotApprovalReview" -v minimal
dotnet build ColorVision/ColorVision.csproj -v minimal
```

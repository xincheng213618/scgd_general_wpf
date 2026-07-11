# ColorVision 本地 MCP

ColorVision 本地 MCP 让 Codex 或其他 MCP 客户端在本机读取正在运行的 ColorVision 上下文，用于诊断、导航、日志查看、文档搜索和有限的低风险 UI 操作。它默认关闭，只绑定 loopback，并使用 bearer token 认证。

本页是维护入口，不是完整协议规范。源码以 `ColorVision/Copilot/Mcp/` 和 `ColorVision/Copilot/Capabilities/` 为准。

## 核心边界

| 规则 | 说明 |
| --- | --- |
| 默认关闭 | 用户需要在 Copilot 设置里启用并保存 |
| 仅本机 | 默认端点是 `http://127.0.0.1:38473/mcp` |
| 需要 token | token 存在 Copilot 设置中，外部客户端通过 `COLORVISION_MCP_TOKEN` 读取 |
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

## 调用顺序

| 步骤 | 调用 | 目的 |
| --- | --- | --- |
| 1 | `get_server_status` | 确认 MCP 是否启用、认证是否成功、server 是否运行 |
| 2 | `colorvision://live-context/current` | 获取当前 Copilot 上下文 |
| 3 | `colorvision://workspace/current` | 确认当前工作区和允许的文件根 |
| 4 | `get_recent_log` / `get_diagnostic_bundle` | 聚合日志和运行时信息 |
| 5 | `search_docs` / `search_files` / `grep_text` | 定位产品行为、源码或配置 |
| 6 | `diagnose_flow_failure` | 诊断流程失败，不启动、不停止、不重跑流程 |
| 7 | `open_panel` / `execute_menu` dry-run | 只做低风险导航或预览 |
| 8 | `suggest_template_patch` / `preview_template_patch` | 生成模板修改建议和预览 |
| 9 | `apply_template_patch` + 用户批准 + `confirm_action` | 只对已预览、已批准、参数匹配的模板 patch 生效 |

工具返回 `confirmation-required` 时，客户端应把 `action_id` 和 `arguments_summary` 交给用户确认；用户在 ColorVision 待确认区批准后再调用 `confirm_action`。

## 工具和资源

| 分类 | 工具或 URI | 风险 |
| --- | --- | --- |
| 状态/审计 | `get_server_status`、`get_enabled_tools`、`get_runtime_environment_summary`、`get_audit_log`、`get_last_tool_error` | 只读 |
| 上下文 | `get_live_context`、`get_workspace_context`、`get_active_template_context`、`get_flow_summary`、`diagnose_flow_failure` | 只读 |
| 搜索和文件 | `get_recent_log`、`search_docs`、`search_files`、`grep_text`、`read_allowed_file`、`list_allowed_directory` | 只读，仅限允许根 |
| 模板预览 | `suggest_template_patch`、`preview_template_patch` | 只读 |
| 应用操作 | `open_panel`、`set_theme` | 低风险 |
| 待确认操作 | `execute_menu`、`apply_template_patch`、`set_language`、`confirm_action` | 需要用户批准 |
| 资源 | `colorvision://live-context/current`、`workspace/current`、`logs/recent`、`template/current`、`flow/current`、`mcp/audit-*` | 只读快照 |

工具列表由 `CopilotMcpToolDispatcher.ListTools()` 生成。新增或删除工具时，先改源码和测试，再同步本页。

## 业务上下文扩展

流程、设备、图像和模板通过 `CopilotBusinessContextCoordinator` 发布同一种 `CopilotBusinessContextBundle`。新增界面上下文时优先实现 `ICopilotBusinessContextSource`，只提供结构化快照，使用 `CopilotBusinessContextBuilder` 脱敏，并让发布和发送复用同一个 bundle。

诊断入口默认使用 `CopilotPromptMode.Diagnose`。外部 MCP 模板写入保持 `suggest_template_patch -> preview_template_patch -> apply_template_patch -> 用户批准 -> confirm_action`；内置 Agent 的 `TemplatePatch` 工具复用相同预览和冲突校验，但其待确认动作在 ColorVision 用户批准后直接应用到未保存的编辑器。MCP 描述与处理器在构造时会做集合一致性检查。

## 安全要求

明确不支持设备控制、流程启动/停止/重跑、任意 shell/cmd/PowerShell/batch/Python 或进程执行、文件删除、任意路径读取、配置静默修改、二进制图片通过上下文快照上传。

文件工具只允许读取规范化后的 ColorVision 工作区根内文本文件。确认动作会记录 `action_id`、工具名、风险、过期时间和脱敏参数摘要；token、密码、API key、Authorization、bearer secret 等敏感值不会进入待确认动作。

## 排查

| 现象 | 先查 |
| --- | --- |
| MCP disabled | Copilot 设置是否启用并保存 |
| 端口不可用 | Copilot 设置里的端口是否被占用 |
| 401 或 token mismatch | `COLORVISION_MCP_TOKEN` 是否和 ColorVision 当前 token 一致 |
| Codex 看不到 server | Codex 配置是否重启生效，URL 是否和 ColorVision 端点一致 |
| 工具要求确认 | 用户是否在 ColorVision Copilot 待确认区域批准 |
| 模板 patch 无法应用 | 当前活动模板编辑器是否还是 preview 时的同一个编辑器和 JSON 快照 |
| 流程相关请求被拒绝 | MCP 只诊断和预览，不执行流程 |

## 验证

```powershell
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj --filter "CopilotCapabilitiesTests|CopilotMcpTests" -v minimal
dotnet build ColorVision/ColorVision.csproj -v minimal
```

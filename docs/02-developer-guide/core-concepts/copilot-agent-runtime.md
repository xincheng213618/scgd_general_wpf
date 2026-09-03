---
knowledge_id: "copilot.runtime"
knowledge_type: "index"
status: "current"
summary: "ColorVision Copilot 的 Agent Framework 执行层、宿主策略边界和按任务检索的专题路由。"
aliases: ["Copilot 架构从哪里看","Agent Framework","CopilotMicrosoftAgentFrameworkRuntime","ICopilotTurnRuntime"]
code_paths: ["ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.cs","ColorVision/Copilot/Runtime/ICopilotTurnRuntime.cs","ColorVision/Copilot/Runtime/CopilotTurnRuntime.cs"]
test_paths: ["Test/ColorVision.Copilot.Tests/ColorVision.Copilot.Tests.csproj"]
related: ["copilot.configuration","copilot.execution","copilot.lifecycle","copilot.extensions","copilot.tool-contracts","copilot.interactions","copilot.view-model","copilot.session-tools","copilot.mcp-server"]
---

# Copilot Agent Runtime

ColorVision Copilot 使用 Microsoft Agent Framework 作为唯一 Agent 执行层。模型、工具、审批、任务账本、恢复和会话状态都沿同一条运行路径处理；框架不可用时本轮明确失败，不切换到另一套规划器重放请求。

项目指令、按需 Skill、子 Agent、外部 MCP 与 Hook 分别拥有发现、上下文、执行和权限边界；不要因为界面入口相近就把它们合成同一种配置。按下表定位各执行阶段的权威主题。

## 架构边界

| 层级 | 主要职责 |
| --- | --- |
| Agent Framework | Session、Harness、工具调用、原生审批、任务与模式 |
| ColorVision Runtime | 能力筛选、预算、并发、恢复、审计和执行契约 |
| 业务模块扩展 | 动态上下文和受宿主策略约束的窄业务工具 |
| 外部 MCP | 显式配置的 Streamable HTTP 工具发现与适配 |
| 本地 MCP Server | 向本机客户端提供受限的诊断、导航和确认操作 |

## 按任务定位实现

| 问题/变更 | 知识入口 | 首查符号 |
| --- | --- | --- |
| 设置保存、模型切换、凭据或联网诊断 | [配置与发布](./copilot-configuration.md) | `CopilotSettingsViewModel`、`CopilotConfig`、`ConfigSavePublicationStatus` |
| 界面状态串会话、输入丢失、ViewModel 该怎么拆 | [状态所有权](./copilot-view-model-architecture.md) | `CopilotConversationSession`、`CopilotComposerSession`、`ICopilotTurnRuntime` |
| 工具为什么没调用、委派权限或证据不足 | [执行链](./copilot-agent-execution.md) | `CopilotToolRegistry`、`CopilotAgentExecutionContract` |
| 本地命令、回顾、查找、快捷键 | [交互入口](./copilot-local-interactions.md) | `CopilotLocalCommandCatalog` |
| Schema、审批、journal、恢复或 Flow 编辑契约 | [工具契约](./copilot-agent-tool-contracts.md) | `CopilotToolExecutionContracts`、`CopilotAgentTaskEventJournal` |
| AGENTS.md、压缩或请求预算 | [生命周期](./copilot-agent-lifecycle.md) | `CopilotAgentProjectInstructions`、`CopilotAgentTokenBudget` |
| 技能发现、调用、开关或 MCP 依赖 | [Copilot 技能](./copilot-skills.md) | `CopilotAgentSkillCatalog`、`CopilotAgentSkills` |
| 检查点、重试、任务 UI 或取消终态 | [会话与工具](./copilot-agent-session-and-tools.md) | `CopilotAgentSessionCheckpoint`、`CopilotToolExecution` |
| 模块上下文、外部 MCP client 或 Hook | [扩展边界](./copilot-agent-extensions.md) | `CopilotAgentExtensionRegistry`、`CopilotMcpToolProvider` |
| 让外部 Codex 读取本机 ColorVision | [本地 MCP server](./colorvision-mcp.md) | `CopilotMcpServer` |

## 验证

先运行受影响专题在 `test_paths` 中列出的定向测试；下面是更宽的本地验证入口，会写编译/测试产物，不上传或发布。不要为陈旧测试恢复运行时读取全局/项目 `config.toml`：ColorVision 自己管理 provider、model、tools 与 approval，保留的是 AGENTS.md / CLAUDE.md 指令发现。

```powershell
dotnet test Test/ColorVision.Copilot.Tests/ColorVision.Copilot.Tests.csproj -p:Platform=x64
dotnet build ColorVision/ColorVision.csproj -p:Platform=x64
```

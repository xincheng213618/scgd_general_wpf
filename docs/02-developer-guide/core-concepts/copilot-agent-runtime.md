# Copilot Agent Runtime

ColorVision Copilot 使用 Microsoft Agent Framework 作为唯一 Agent 执行层。模型、工具、审批、任务账本、恢复和会话状态都沿同一条运行路径处理；框架不可用时本轮明确失败，不切换到另一套规划器重放请求。

功能选择主要遵循 Codex 与 Claude Code 的共同原则：长期规则放进有作用域的项目指令，可复用流程放进按需加载的 Skill，需要隔离上下文或权限时才使用子 Agent，外部系统接入才使用 MCP，必须确定执行的生命周期策略才使用 Hook。每一种扩展都必须有明确职责、预算和验证依据；重复需求尚未出现时不提前增加常驻能力。该分工参考 [Codex AGENTS.md](https://learn.chatgpt.com/docs/agent-configuration/agents-md)、[OpenAI Skills](https://learn.chatgpt.com/docs/build-skills) 以及 [Claude Code features overview](https://code.claude.com/docs/en/features-overview)。

## 架构边界

| 层级 | 主要职责 |
| --- | --- |
| Agent Framework | Session、Harness、工具调用、原生审批、任务与模式 |
| ColorVision Runtime | 能力筛选、预算、并发、恢复、审计和执行契约 |
| 业务模块扩展 | 动态上下文和受宿主策略约束的窄业务工具 |
| 外部 MCP | 显式配置的 Streamable HTTP 工具发现与适配 |
| 本地 MCP Server | 向本机客户端提供受限的诊断、导航和确认操作 |

## 详细说明

- [执行链、工具选择与子 Agent](./copilot-agent-execution.md)
- [工具契约、任务事件、恢复和 Flow 编辑](./copilot-agent-tool-contracts.md)
- [生命周期、预算、任务账本、项目指令和 Skills](./copilot-agent-lifecycle.md)
- [任务 UI、检查点、重试和内置工具](./copilot-agent-session-and-tools.md)
- [业务模块扩展、外部 MCP 与 Hook](./copilot-agent-extensions.md)
- [ColorVision 本地 MCP Server](./colorvision-mcp.md)

## 验证

```powershell
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj --filter "FullyQualifiedName~Copilot" --no-restore -p:BuildProjectReferences=false
dotnet test Test/ProjectARVRPro.Tests/ProjectARVRPro.Tests.csproj --no-restore
dotnet build ColorVision/ColorVision.csproj --no-restore -p:BuildProjectReferences=false
```

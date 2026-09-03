---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# Copilot

> 自动生成的领域目录。修改主题 Markdown 元数据后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

Agent会话、工具契约、上下文、恢复和MCP边界。 返回[知识总入口](../index.md)。

只读与当前问题相关的主题，再核对源码和测试。`规划`、`历史`不代表当前能力。

- [Copilot Agent Runtime](../../02-developer-guide/core-concepts/copilot-agent-runtime.md) — `copilot.runtime`
  ColorVision Copilot 的 Agent Framework 执行层、宿主策略边界和按任务检索的专题路由。

- [Copilot 设置、持久化与连接诊断](../../02-developer-guide/core-concepts/copilot-configuration.md) — `copilot.configuration`
  ColorVision内置Copilot的设置草稿、配置保存与运行态发布、模型选择和联网诊断；保存失败可能已落盘，Local MCP测试核验会话握手与只读状态调用。

- [Copilot Agent 执行链](../../02-developer-guide/core-concepts/copilot-agent-execution.md) — `copilot.execution`
  Copilot 请求调度、工具筛选、审批、只读委派与执行证据闭环。

- [Copilot 扩展、MCP 与 Hook](../../02-developer-guide/core-concepts/copilot-agent-extensions.md) — `copilot.extensions`
  业务模块动态上下文、外部 MCP client 和 Hook 如何进入统一宿主权限与生命周期。

- [Copilot 输入、命令与活动呈现](../../02-developer-guide/core-concepts/copilot-local-interactions.md) — `copilot.interactions`
  Copilot 命令目录、输入与引用、会话导航及消息/桌宠呈现；本地入口不等于无副作用。

- [Copilot 生命周期、预算与项目指令](../../02-developer-guide/core-concepts/copilot-agent-lifecycle.md) — `copilot.lifecycle`
  Copilot 任务生命周期、恢复预算与项目指令发现的契约；技能发现和调用见独立技能主题。

- [Copilot 任务、恢复与内置工具](../../02-developer-guide/core-concepts/copilot-agent-session-and-tools.md) — `copilot.session-tools`
  Copilot 会话检查点、任务呈现、重试和内置工具的状态恢复与安全边界。

- [Copilot 技能：发现、调用与排障](../../02-developer-guide/core-concepts/copilot-skills.md) — `copilot.skills`
  Copilot 技能的项目/用户/内置发现路径、显式调用、同名选择、开关、按需加载、使用统计和 MCP 依赖配置。

- [Copilot 工具契约](../../02-developer-guide/core-concepts/copilot-agent-tool-contracts.md) — `copilot.tool-contracts`
  Copilot 工具结果、事件、审批恢复和 Flow 编辑必须遵守的执行契约。

- [Copilot 状态所有权与界面交接](../../02-developer-guide/core-concepts/copilot-view-model-architecture.md) — `copilot.view-model`
  Copilot 界面状态的所有权、异步输入交接、检查点提交及会话保存完成边界。

- [ColorVision 本地 MCP](../../02-developer-guide/core-concepts/colorvision-mcp.md) — `copilot.mcp-server`
  ColorVision 入站本地 MCP 的连接与会话、工具和资源、工作区读取、两阶段确认及菜单写入边界。

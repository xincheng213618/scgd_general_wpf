---
knowledge_id: "platform.extensibility"
knowledge_type: "topic"
status: "current"
summary: "菜单、插件、属性编辑器、算法模板和 Copilot 扩展的职责与源码入口。"
aliases: ["增加扩展应放在哪一层","IPlugin","IResultHandleBase","PropertyEditorType","CopilotAgentExtensionRegistry"]
code_paths: ["UI/ColorVision.Common/Interfaces/","UI/ColorVision.UI/Plugins/PluginLoader.cs","Engine/ColorVision.Engine/Templates/"]
test_paths: ["Test/ColorVision.UI.Tests/PluginLoaderTests.cs"]
related: ["plugins.index","plugins.model","engine.index","ui.index","ui.menus"]
---

# 扩展性开发

ColorVision 的扩展工作分布在运行时插件、Engine 服务、模板和 Flow 等不同模块。本页按变更任务定位所有者，不复制接口或生命周期；实现细节以对应专题页和当前源码为准。

## 按需求选择入口

| 目标 | 先读 | 源码入口 |
| --- | --- | --- |
| 接入运行时插件 | [插件装配与模块入口](../../04-api-reference/plugins/README.md)、[装载与扩展发现](../plugin-development/overview.md) | `Plugins/`、`UI/ColorVision.UI/Plugins/` |
| 新增菜单或改变菜单树、显示与执行 | [菜单契约](../../04-api-reference/ui-components/menus.md) | `UI/ColorVision.Common/Interfaces/Menus/`、`UI/ColorVision.UI/Menus/` |
| 新增设备或业务服务 | [Engine 设备装配与扩展契约](../../04-api-reference/engine-components/device-service-chain.md) | `Engine/ColorVision.Engine/Services/` |
| 新增算法参数或模板 | [模板核心契约](../../03-architecture/components/templates/design.md)、[Flow 模板与流程包](../../04-api-reference/engine-components/template-flow-chain.md) | `Engine/ColorVision.Engine/Templates/` |
| 新增或维护 Flow 节点 | [FlowEngineLib 节点扩展](../../04-api-reference/extensions/flow-node.md)、[FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md) | `Engine/FlowEngineLib/`、`Engine/ColorVision.Engine/FlowProcessing/` |
| 业务模块向 Copilot 提供上下文或窄工具 | [Copilot 模块扩展](./copilot-agent-extensions.md) | `UI/ColorVision.Common/Interfaces/Copilot/CopilotAgentExtensionRegistry.cs` |
| 本地图像算法、结果与宿主适配 | [统一图像算法平台](./image-algorithm-platform-v1.md) | `UI/ColorVision.Algorithms/`、`UI/ColorVision.ImageEditor/Algorithms/` |

如果还不能确定改动属于哪一层，先看 [Engine 组件总览](../../04-api-reference/engine-components/README.md)。当前已整理成专题的扩展点集中在 [扩展点概览](../../04-api-reference/extensions/README.md)，该页不是所有扩展机制的完整清单。

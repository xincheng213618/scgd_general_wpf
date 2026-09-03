---
knowledge_id: "platform.extensibility"
knowledge_type: "index"
status: "current"
summary: "按插件、菜单、属性面板、设备、Flow、算法和结果扩展任务定位实现与当前契约。"
aliases: ["扩展开发","扩展入口","增加扩展应放在哪一层","扩展功能应该实现哪个接口"]
code_paths: ["UI/ColorVision.Common/Interfaces/","UI/ColorVision.UI/Plugins/PluginLoader.cs","UI/ColorVision.UI/PropertyEditor/PropertyEditorTypeAttribute.cs","UI/ColorVision.UI/PropertyEditor/PropertyEditors.cs","Engine/FlowEngineLib/Base/CVBaseServerNode.cs","Engine/ColorVision.Engine/Templates/","Engine/ColorVision.Engine/Abstractions/IResultHandlers.cs","Engine/ColorVision.Engine/Services/ResultHandleRegistry.cs"]
test_paths: []
related: ["plugins.index","plugins.model","engine.index","engine.results","ui.index","ui.menus","ui.property-grid","algorithms.platform","flow.node-extension","engine.devices","engine.template-design"]
---

# 扩展性开发

ColorVision 的扩展工作分布在运行时插件、Engine 服务、模板和 Flow 等不同模块。本页按变更任务定位所有者，不复制接口或生命周期；实现细节以对应专题页和当前源码为准。

## 按需求选择入口

| 目标 | 先读 | 源码入口 |
| --- | --- | --- |
| 接入运行时插件 | [插件装配与模块入口](../../04-api-reference/plugins/README.md)、[装载与扩展发现](../plugin-development/overview.md) | `Plugins/`、`UI/ColorVision.UI/Plugins/` |
| 新增菜单或改变菜单树、显示与执行 | [菜单契约](../../04-api-reference/ui-components/menus.md) | `UI/ColorVision.Common/Interfaces/Menus/`、`UI/ColorVision.UI/Menus/` |
| 扩展属性编辑器、条件可见性或编辑事务 | [PropertyGrid 属性编辑契约](../../04-api-reference/ui-components/property-grid.md) | `UI/ColorVision.UI/PropertyEditor/`；`PropertyEditorTypeAttribute`、`IPropertyEditor` |
| 新增设备或业务服务 | [Engine 设备装配与扩展契约](../../04-api-reference/engine-components/device-service-chain.md) | `Engine/ColorVision.Engine/Services/` |
| 新增算法参数或模板 | [模板核心契约](../../03-architecture/components/templates/design.md)、[Flow 模板与流程包](../../04-api-reference/engine-components/template-flow-chain.md) | `Engine/ColorVision.Engine/Templates/` |
| 新增或维护 Flow 节点 | [FlowEngineLib 节点扩展](../../04-api-reference/extensions/flow-node.md)、[节点路由](../../04-api-reference/flow_nodes_summary.md) | `Engine/FlowEngineLib/`、`Engine/ColorVision.Engine/FlowProcessing/` |
| 业务模块向 Copilot 提供上下文或窄工具 | [Copilot 模块扩展](./copilot-agent-extensions.md) | `UI/ColorVision.Common/Interfaces/Copilot/CopilotAgentExtensionRegistry.cs` |
| 本地图像算法、结果与宿主适配 | [统一图像算法平台](./image-algorithm-platform-v1.md) | `UI/ColorVision.Algorithms/`、`UI/ColorVision.ImageEditor/Algorithms/` |
| 为 Engine 历史结果增加表格或叠图处理器 | [Engine 结果展示链路](../../04-api-reference/engine-components/result-handoff-chain.md) | `Engine/ColorVision.Engine/Abstractions/IResultHandlers.cs`、`Engine/ColorVision.Engine/Services/ResultHandleRegistry.cs` |
| 调整客户判定、导出或协议字段 | [项目业务结果边界](../../04-api-reference/engine-components/result-handoff-chain.md) | 对应 `Projects/` 模块的 Process、Recipe/Fix、exporter |

需要确认扩展如何被宿主发现时，先看 [UI 扩展发现](../../04-api-reference/ui-components/ui-runtime-handoff.md)及对应专题。设备、模板与结果的职责划分见 [Engine 组件总览](../../04-api-reference/engine-components/README.md)。

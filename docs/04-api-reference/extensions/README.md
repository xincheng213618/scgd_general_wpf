---
knowledge_id: "platform.extensions"
knowledge_type: "index"
status: "current"
summary: "按 Flow 节点、属性编辑器、模板、设备和插件问题定位可复用扩展契约。"
aliases: ["扩展功能应该实现哪个接口","IPropertyEditor","CVBaseServerNode","ITemplate"]
code_paths: ["Engine/FlowEngineLib/Base/CVBaseServerNode.cs","UI/ColorVision.UI/PropertyEditor/PropertyEditors.cs","Engine/ColorVision.Engine/Templates/ITemplate.cs"]
test_paths: []
related: ["flow.node-extension","ui.property-grid","engine.devices","engine.template-design"]
---

# 扩展任务入口

按要扩展的能力定位唯一契约页；目录位置只是现有 URL，不代表必须按插件、模板、服务的章节顺序阅读。

| 要扩展的能力 | 契约与源码入口 |
| --- | --- |
| 属性编辑器、条件可见性、事务编辑 | [PropertyGrid 契约](../ui-components/property-grid.md)：`IPropertyEditor.GenProperties`、`PropertyEditorRegistry` |
| Flow 公共节点或 Engine 本地节点 | [Flow 节点扩展](./flow-node.md)、[节点路由](../flow_nodes_summary.md)：`STNode`、`FlowNodePropertyEditorAttribute` |
| 设备类型与服务工厂 | [设备装配与扩展契约](../engine-components/device-service-chain.md)：`DeviceServiceFactoryRegistry` |
| 强类型、JSON 或 Flow 模板 | [模板核心契约](../../03-architecture/components/templates/design.md)：`ITemplate`、`ITemplateJson` 与各分支边界 |
| 中立图像算法与 overlay | [算法平台](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md)：`UI/ColorVision.Algorithms/` |
| 运行时插件、菜单和部署 | [插件能力](../plugins/README.md)、[插件开发](../../02-developer-guide/plugin-development/overview.md)、[UI 发现](../ui-components/ui-runtime-handoff.md) |

扩展前检查目标宿主的发现方式、对象生命周期和失败行为。接口存在不表示实现自动注册；专题中的测试入口只覆盖相应契约，不能替代插件加载、设备或实际窗口验证。

## 验证入口与缺口

路由页不代表所有扩展共用同一生命周期；各扩展必须使用自己的源码、测试和外部依赖边界。

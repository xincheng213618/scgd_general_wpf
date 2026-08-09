# 扩展性开发

ColorVision 的扩展工作分布在运行时插件、Engine 服务、模板和 Flow 等不同模块。本页只帮助初次参与开发的人找到入口，不再复制接口或生命周期；实现细节以对应专题页和当前源码为准。

## 按需求选择入口

| 目标 | 先读 | 源码入口 |
| --- | --- | --- |
| 开发运行时插件 | [插件开发概览](../plugin-development/overview.md) | `Plugins/` |
| 新增设备或业务服务 | [Engine 服务开发指南](../engine-development/services.md) | `Engine/ColorVision.Engine/Services/` |
| 新增算法参数或模板 | [模板系统开发指南](../engine-development/templates.md)、[Engine 模板与 Flow 链路](../../04-api-reference/engine-components/template-flow-chain.md) | `Engine/ColorVision.Engine/Templates/` |
| 新增或维护 Flow 节点 | [FlowEngineLib 节点扩展](../../04-api-reference/extensions/flow-node.md)、[FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md) | `Engine/FlowEngineLib/`、`Engine/ColorVision.Engine/FlowProcessing/` |

如果还不能确定改动属于哪一层，先看 [Engine 组件总览](../../04-api-reference/engine-components/README.md)。当前已整理成专题的扩展点集中在 [扩展点概览](../../04-api-reference/extensions/README.md)，该页不是所有扩展机制的完整清单。

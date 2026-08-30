---
knowledge_id: "algorithms.template-overview"
knowledge_type: "topic"
status: "current"
summary: "说明 Engine 模板发现、手动算法宿主、MQTT 请求和 Flow 接入链。"
aliases: ["Engine算法是在本地执行吗","DisplayAlgorithmBase","TemplateControl","MQTTAlgorithm"]
code_paths: ["Engine/ColorVision.Engine/Templates/TemplateControl.cs","Engine/ColorVision.Engine/Abstractions/IDisplayAlgorithm.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/DisplayAlgorithmManager.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/DisplayAlgorithmControl.xaml.cs"]
test_paths: []
related: ["algorithms.index","engine.template-design","flow.templates","engine.results"]
---

# 算法与模板接入概览

本页解释算法问题如何从代码落点追到执行与结果。已有中立算法平台和 Engine 模板适配层；不要把仓库全部算法写成外部 MQTT 调用，也不要把所有 `Algorithm*.cs` 当成本地计算实现。

## 代码与契约落点

| 位置 | 负责 | 主题 |
| --- | --- | --- |
| `UI/ColorVision.Algorithms/` | 中立输入、参数、执行、Result 与 overlay 数据 | [算法平台](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md) |
| `Engine/ColorVision.Engine/Templates/` | 模板定义、持久化、编辑与业务适配 | [注册与持久化](../../03-architecture/components/templates/design.md)、[编辑宿主](./templates/template-management.md) |
| `Engine/ColorVision.Engine/Services/Devices/Algorithm/` | 算法服务、通用手动配置宿主 | [模板入口](./templates/template-menu-entries.md) |
| `Engine/FlowEngineLib/`、`FlowProcessing/` | 节点执行、宿主、参数映射与最终化 | [Flow 接入](./templates/flow-engine.md) |
| `UI/ColorVision.Core/`、`Engine/cvColorVision/`、`Native/` | native 包装、设备 SDK 与部分计算 | [native 集成](../../02-developer-guide/engine-development/opencv-integration.md) |
| `ResultHandleRegistry`、`AlgorithmOverlayManager` | 历史结果与中立算法 overlay 的不同显示链 | [结果边界](../engine-components/result-handoff-chain.md) |

## Engine 模板适配链

模板发现、构造注册与数据加载是不同阶段，统一见[模板核心契约](../../03-architecture/components/templates/design.md)。模板菜单、搜索与 `DisplayAlgorithmTemplateSelection.EditCommand` 打开编辑器；算法 `Configuration` 经通用 `DisplayAlgorithmControl` 提供手动参数，按钮调用 `Execute()`。

服务型 `Algorithm*` 在 `SendCommand` 中打包 `CVTemplateParam`、文件路径和设备信息，经 `MQTTAlgorithm` 发给执行端。计算实现、路径可见性和服务版本不一定都在本仓库中；文档中的请求字段不能证明服务支持所有组合。

Flow 模板仍属于 Templates：`TemplateFlow` 保存画布与包，`FlowProcessing/Editor` 编辑，`FlowExecutionSession` / `FlowHeadlessExecutionService` 把快照交给节点执行内核。最终业务完成需要等宿主最终化，不能仅以某节点或引擎完成事件代替。

## 按问题选择证据

| 问题 | 查证入口 |
| --- | --- |
| 模板没有出现或无法保存 | `ITemplate`、`IITemplateLoad`、`TemplateControl`、数据库字典与模板载荷 |
| 手动配置选错模板 | `DisplayAlgorithmTemplateSelection`、`Config.Template`、具体算法 `Execute` |
| Flow 参数与手动入口不同 | 节点实际请求、属性编辑映射、模板名/ID |
| 结果已生成但没有显示 | 先区分历史 handler 与中立 overlay，再查匹配类型、图像源与生命周期 |
| 需要新增算法能力 | 先确定本地/远端执行位置和中立结果契约，再决定是否需要 Engine 模板适配 |

JSON 模板与强类型模板仍然并存；POI 是多个分支共享的上游依赖。只加载回答当前问题需要的主题与源码，不要求依次阅读整个模板目录。

## 验证入口与缺口

验证缺口：未登记贯穿模板发现、手动宿主、MQTT 服务与结果回放的专门自动化测试；应按具体模板保存请求样例并进行有依赖环境的联调。

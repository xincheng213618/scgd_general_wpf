---
knowledge_id: "flow.index"
knowledge_type: "index"
status: "current"
summary: "按节点用途与执行归属定位 FlowEngineLib、Engine 本地节点和属性编辑器。"
aliases: ["Flow有哪些节点在哪里定义","NodeType","STNode","FlowNodePropertyEditorAttribute"]
code_paths: ["Engine/FlowEngineLib/Node","Engine/FlowEngineLib/Base","Engine/ColorVision.Engine/FlowProcessing/Nodes","Engine/ColorVision.Engine/PropertyEditor/FlowNodePropertyEditorRegistration.cs"]
test_paths: []
related: ["flow.runtime","flow.editor","flow.node-extension","flow.templates","flow.workspace","flow.headless","flow.conversion-calibration"]
---

# Flow 节点检索入口

按节点类型、执行位置和参数编辑方式定位源码。本页是检索路由，不复制易漂移的完整类清单。

## 按问题检索

| 问题 | 源码入口 | 相关主题 |
| --- | --- | --- |
| 公共流程节点与执行参数 | `Engine/FlowEngineLib/` | [FlowEngineLib](./engine-components/FlowEngineLib.md) |
| 本地执行、内存帧和宿主扩展节点 | `Engine/ColorVision.Engine/FlowProcessing/Nodes/` | [节点扩展](./extensions/flow-node.md) |
| 通用节点参数如何选择属性编辑器 | `Engine/FlowEngineLib/PropertyEditor/`、`Engine/ColorVision.Engine/PropertyEditor/` | [PropertyGrid 契约](./ui-components/property-grid.md) |
| 复杂多模板或流程专用配置面板 | `Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/` | [模板与 Flow 链](./engine-components/template-flow-chain.md) |
| 模板存储、关联模板与导入导出 | `Templates/Flow/` | [模板与 Flow 链](./engine-components/template-flow-chain.md) |
| 当前画布、保存命令与工作区隔离 | `FlowProcessing/Editor/`、`FlowProcessing/Runtime/` | [Flow 编辑工作区](../01-user-guide/workflow/design.md) |
| 无界面运行与调度请求 | `FlowProcessing/Runtime/` | [Flow 隔离执行](./algorithms/templates/flow-engine.md) |

## 常见检索词

`Algorithm`、`Camera`、`POI`、`ROI`、`SMU`、`Sensor`、`Spectrum`、`PG`、`MQTT`、`Start`、`End`、`Loop`。同类功能可能既有外部服务节点，又有 Engine 本地节点；需沿 `Execute`/输入输出与调用方判断，不能只按名称归类。

```powershell
rg -n "class .*Node|NodeType|FlowNodePropertyEditor|PropertyEditorType|NodeConfigurator" Engine/FlowEngineLib Engine/ColorVision.Engine/FlowProcessing/Nodes Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration Engine/ColorVision.Engine/PropertyEditor
```

标注 `Obsolete` 的类型可能仍被旧流程序列化名称引用。新增节点时优先查当前替代类型和兼容要求，不因它不再出现在 UI 菜单中就删除旧类型。

## 修改时保持的边界

公共节点不直接依赖 Engine 高层 UI；依赖服务或内存图像宿主的节点留在 Engine。普通参数用元数据驱动属性编辑器，复杂专用面板才进入 `NodeConfiguration/`。节点可发现、参数可编辑和执行正确是不同验证点。

## 验证入口与缺口

此索引不保存易漂移的全量节点副本；节点是否能新建以类型注册与 Obsolete 过滤为准，行为测试从对应专题进入。

# Flow 节点摘要

本页保留 FlowEngine 节点的轻量摘要。旧的完整自动生成清单已删除；需要逐项追溯时直接查看源码。

## 数据来源

| 范围 | 源码目录 |
| --- | --- |
| 节点实现 | `Engine/FlowEngineLib/` |
| 节点配置器 | `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/` |

## 当前分类概览

| 类型 | 说明 |
| --- | --- |
| Algorithm | 算法和模板执行相关节点 |
| Camera | 相机采集、图像输入和相机控制 |
| POI/ROI | 关注点、区域和结果定位 |
| SMU/Sensor/Spectrum/PG | 设备相关节点 |
| MQTT/Device | 设备通信和服务调用 |
| Start/End/Loop/Manual | 流程控制节点 |

## 使用方式

- 想新增或修改节点：先看 [Flow 节点扩展](./extensions/flow-node.md) 和 [FlowEngineLib 架构](../03-architecture/components/engine/flow-engine.md)。
- 想理解模板和 Flow 如何绑定：看 [Engine 模板与 Flow 链路](./engine-components/template-flow-chain.md)。
- 想确认当前节点类：直接在源码中搜索 `Node`、`NodeType`、`NodeConfigurator`。

推荐命令：

```powershell
rg -n "class .*Node|NodeType|NodeConfigurator" Engine/FlowEngineLib Engine/ColorVision.Engine/Templates/Flow
```

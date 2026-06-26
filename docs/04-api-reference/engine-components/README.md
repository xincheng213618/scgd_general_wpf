# Engine 组件

`Engine/` 是 ColorVision 的业务核心，负责把设备服务、模板系统、流程引擎、MQTT 通信、数据库结果和图像展示串起来。

## 模块地图

| 模块 | 源码目录 | 主要职责 | 文档 |
| --- | --- | --- | --- |
| ColorVision.Engine | `Engine/ColorVision.Engine/` | 设备服务、模板、流程接入、MQTT、批次、结果 | [ColorVision.Engine](./ColorVision.Engine.md) |
| FlowEngineLib | `Engine/FlowEngineLib/` | 流程节点、开始/结束节点、执行控制 | [FlowEngineLib](./FlowEngineLib.md) |
| cvColorVision | `Engine/cvColorVision/` | OpenCV/native 封装、底层视觉处理 | [cvColorVision](./cvColorVision.md) |
| ColorVision.FileIO | `Engine/ColorVision.FileIO/` | CVRAW/CVCIE 等文件读写 | [ColorVision.FileIO](./ColorVision.FileIO.md) |
| ST.Library.UI | `Engine/ST.Library.UI/` | 节点编辑器 UI 控件 | [ST.Library.UI](./ST.Library.UI.md) |
| ColorVision.ShellExtension | `Engine/ColorVision.ShellExtension/` | Windows Shell 缩略图扩展 | [ColorVision.ShellExtension](./ColorVision.ShellExtension.md) |

## 常用专题

| 问题 | 入口 |
| --- | --- |
| Engine 需求、排障和发布验证该归到哪条链 | [Engine 业务链路矩阵](./business-flow-matrix.md) |
| Flow 转换、图像转换和校准节点怎么追 | [Flow 转换与校准节点](./flow-conversion-calibration-nodes.md) |

## 关键链路

```mermaid
flowchart TD
  Resource["SysResourceModel / 数据库资源"] --> Factory["DeviceServiceFactoryRegistry"]
  Factory --> Service["DeviceService 实例"]
  Service --> MQTT["MQTTDeviceService / MQTTControl"]
  Template["TemplateControl / TemplateModel"] --> FlowTemplate["TemplateFlow"]
  FlowTemplate --> FlowEngine["FlowEngineControl"]
  FlowEngine --> Node["Flow 节点"]
  Node --> Service
  Node --> Algorithm["算法/模板命令"]
  Algorithm --> Result["AlgResult / ViewResult"]
  Result --> ImageEditor["ImageEditor Overlay"]
  Result --> Dao["MySQL / SQLite / CSV"]
```

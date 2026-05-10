# Engine组件概览

本章现在只保留和当前仓库结构能直接对上的 Engine 侧模块入口，不再继续维护“版本表 + 示例代码 + 统一分层蓝图”式旧稿。

## 这一章实际覆盖什么

`Engine/` 目录下的代码并不是单一算法库，而是一组彼此配合的运行时模块：

- `ColorVision.Engine/`：主引擎层，承接服务、模板、MQTT、数据库和流程接入。
- `FlowEngineLib/`：流程节点与执行控制核心。
- `cvColorVision/`：原生能力封装与互操作桥接。
- `ColorVision.FileIO/`：图像与自定义格式文件读写。
- `ST.Library.UI/`：节点编辑器与相关 UI 基础控件。

因此阅读 Engine 章节时，不要把它理解成“只有算法实现”，它同时包含运行时编排、流程执行、底层封装和编辑器支撑层。

## 怎么读这一章

如果你第一次进入 Engine 代码，建议按下面顺序建立认知：

1. 先看 `ColorVision.Engine`，理解服务、模板和流程是怎么被主程序接起来的。
2. 再看 `FlowEngineLib`，理解节点执行、开始/结束链和流程完成事件来自哪里。
3. 然后补 `ColorVision.FileIO` 和 `cvColorVision`，区分文件读写层与原生算法/设备封装层。
4. 最后再看 `ST.Library.UI`，理解流程编辑器所依赖的节点 UI 基础设施。

## 模块地图

### 主引擎层

- [ColorVision.Engine](./ColorVision.Engine.md)：当前系统最重要的 Engine 入口，主要关注 `Services/`、`Templates/`、`MQTT/`、`Messages/` 等目录。

### 流程执行层

- [FlowEngineLib](./FlowEngineLib.md)：节点执行与流程控制核心，但它需要和 `ColorVision.Engine/Templates/Flow/` 一起看，才是完整的实际运行链。

### 底层支撑层

- [ColorVision.FileIO](./ColorVision.FileIO.md)：文件格式、导入导出和相关 I/O 处理。
- [cvColorVision](./cvColorVision.md)：原生视觉能力封装与设备/算法互操作桥接。

### 编辑器基础层

- [ST.Library.UI](./ST.Library.UI.md)：流程节点编辑器和属性面板等 UI 基础能力。

## 当前几个容易写错的边界

- `ColorVision.Engine` 不是“所有算法都在这里算完”的单体模块，它更多是把模板、设备、流程和消息链组织起来。
- `FlowEngineLib` 不是整个流程系统的全部实现；真正进入主程序时，还要经过 `Templates/Flow/` 里的模板和窗口层。
- `cvColorVision` 与 `ColorVision.FileIO` 都属于支撑层，不应和模板/UI 侧能力混写成同一层。
- `Engine/ColorVision.ShellExtension/` 当前虽然在源码树中存在，但本章还没有把它作为稳定的 API 参考入口展开。

## 建议先看的源码锚点

如果目标是理解 Engine 侧真实控制面，优先看这些代码比先翻旧文档更有效：

- `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
- `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
- `Engine/FlowEngineLib/FlowEngineControl.cs`
- `Engine/FlowEngineLib/Start/BaseStartNode.cs`
- `Engine/FlowEngineLib/End/CVEndNode.cs`

## 继续阅读

- [Templates 模块分析](../../03-architecture/components/templates/analysis.md)
- [FlowEngineLib 架构](../../03-architecture/components/engine/flow-engine.md)
- [系统运行时](../../03-architecture/overview/runtime.md)

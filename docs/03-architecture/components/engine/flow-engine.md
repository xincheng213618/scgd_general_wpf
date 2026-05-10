# FlowEngineLib 架构

本页只描述当前仓库里实际在运行的流程编辑与执行链，不再继续维护把 FlowEngineLib 写成一套独立分层框架的旧稿。

## 先看它在系统里的位置

FlowEngine 相关能力并不只存在于 `Engine/FlowEngineLib/`。当前真正的使用链跨了两层：

- `FlowEngineLib/` 提供节点编辑器里的执行控制、开始节点、结束节点和服务节点基础能力
- `Engine/ColorVision.Engine/Templates/Flow/` 负责把流程模板、编辑窗口、运行窗口和批次处理接到主程序里

因此讨论 FlowEngine 架构时，只写库本身会漏掉一半真实运行时。

## 当前最关键的对象

### `FlowEngineControl`

`FlowEngineControl` 是执行控制的中心对象。它当前负责：

- 绑定 `STNodeEditor`
- 在节点加入编辑器时识别开始节点和服务节点
- 维护 `startNodeNames` 和 `services`
- 从文件或 Base64 加载画布
- 选择开始节点并启动、停止流程
- 在开始节点完成时向外抛出 `Finished`

这意味着它并不是一个抽象调度接口，而是直接和节点编辑器实例、节点对象、服务集合绑在一起的运行时控制器。

### `BaseStartNode`

开始节点负责把一次流程运行封装为 `CVStartCFC`，并把启动、停止、完成事件沿流程图派发出去。

当前要点是：

- `Start(serialNumber)` 会创建 `CVStartCFC`
- `DoDispatch(...)` 会把动作向下游传递
- `FireFinished(...)` 才是真正发出流程结束事件的地方

因此“流程完成”不是控制器自己推断出来的，而是由开始节点最终发出的。

### `CVBaseServerNode`

大多数设备节点、算法节点都落在 `CVBaseServerNode` 体系里。它们负责：

- 发送和等待运行时动作
- 处理超时、失败和返回数据
- 把节点结果继续传给下游
- 通过 `nodeEndEvent` 报告单个节点结束状态

这里的 `nodeEndEvent` 很重要，但它只表示节点级别结束，不等于整条流程已经结束。

### `CVEndNode`

结束节点是流程完成链的最后一跳。当前实现里，结束节点会在结束处理时调用 `startAction.FireFinished()`，从而把整条流程标记为完成。

这就是为什么“某个节点执行完了”和“整条流程 finished 了”在系统里是两件不同的事。

## 流程真正是怎么跑起来的

当前主链大致是：

1. `TemplateFlow` 或 `FlowEngineToolWindow` 准备流程数据。
2. `FlowEngineToolWindow` 把 `FlowEngineLib.dll` 加载到节点编辑器。
3. `FlowEngineControl` 绑定 `STNodeEditor`，在节点加入时识别开始节点和服务节点。
4. `LoadFromBase64(...)` 或 `Load(...)` 把流程图载入画布。
5. `StartNode(...)` 选择指定开始节点，或者默认取第一个开始节点。
6. `BaseStartNode` 创建 `CVStartCFC` 并向下游节点派发。
7. 各 `CVBaseServerNode` 派生节点处理自己的运行、超时和数据传递。
8. `CVEndNode` 在结束时调用 `startAction.FireFinished()`。
9. `BaseStartNode.Finished` 被触发。
10. `FlowEngineControl.Start_Finished(...)` 再把它转成自己的 `Finished` 事件。

这个完成链比旧文档里的“某节点结束即流程结束”要严格，也更接近当前代码。

## Engine 层是怎么把它接进主程序的

### 流程模板

`TemplateFlow` 让流程图能以模板形式存在于系统里，支持：

- 模板列表管理
- 双击直接打开流程编辑器
- `.stn` / `.cvflow` 导入
- 流程包导入时处理关联模板

### 编辑窗口

`FlowEngineToolWindow` 是独立流程编辑面。它负责：

- 承载 `STNodeEditor`
- 加载 `FlowEngineLib.dll`
- 接入撤销、重做、复制、粘贴、缩放和自动对齐
- 通过 `STNodeEditorHelper` 连接属性面板和节点树

所以当前编辑体验不是 `FlowEngineLib` 自带 UI，而是 Engine 层包了一层 WPF 窗口来接它。

### 运行窗口

真正落到主程序日常使用里的，是 `DisplayFlow` 和 `FlowControl` 这条线。

`DisplayFlow` 当前负责：

- 刷新当前流程模板
- 启动前执行预处理
- 监听流程完成
- 写入运行日志、批次信息和进度
- 在流程完成后触发自定义批处理

这说明主程序里的流程运行，不只是“把图跑完”，还和批次记录、日志文本、后处理扩展绑在一起。

## 当前有哪些容易写错的边界

### `nodeEndEvent` 不是流程完成事件

`CVCommonNode` 上的 `nodeEndEvent` 只用于节点级反馈。真正的流程完成链是：

- EndNode 调用 `startAction.FireFinished()`
- `CVStartCFC.FireFinished()` 回到开始节点
- `BaseStartNode.Finished` 被触发
- `FlowEngineControl.Finished` 再向外抛出

如果把这两种事件混为一谈，就会把失败传播、进度更新和最终完成判断全写偏。

### 开始节点不是任意节点

`FlowEngineControl` 只会在节点加入时把 `BaseStartNode` 收进 `startNodeNames`。启动流程时，如果没有指定名称，默认取第一个开始节点。

所以流程是否可启动，和开始节点是否存在、是否 ready 直接相关。

### 失败传播要看节点类型

流程失败并不是统一由控制器兜底判断。很多失败、超时或取消是在节点内部生成，再沿连线继续传递。尤其是多输入节点，失败传播行为要看具体节点实现，而不是只看控制器表面状态。

## 扩展时通常落在哪

### 新节点

如果是新增流程节点，重点通常在 `FlowEngineLib` 的节点实现本身，以及它如何接入开始、结束或服务节点链。

### 新模板型流程

如果是新增一类可编辑流程模板，重点通常在 `TemplateFlow` 相邻的模板管理、导入导出和编辑窗口接入。

### 节点属性面板扩展

如果是新增流程节点配置 UI，通常会落到 `STNodeEditorHelper` 或 `NodeConfigurator` 一带，而不是只改节点类本身。

## 推荐阅读顺序

推荐按这条线读：

1. `Engine/FlowEngineLib/FlowEngineControl.cs`
2. `Engine/FlowEngineLib/Start/BaseStartNode.cs`
3. `Engine/FlowEngineLib/End/CVEndNode.cs`
4. `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
6. `Engine/ColorVision.Engine/Templates/Flow/FlowEngineToolWindow.xaml.cs`
7. `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`

这样能先建立执行主链，再回到编辑和主程序集成。

## 这页不再做什么

本页不再继续维护这些高风险内容：

- 把 FlowEngineLib 描述成与当前实现脱节的标准分层框架
- 用一组抽象设计模式覆盖所有节点行为
- 把 MQTT、日志、序列化等周边都包装成独立基础设施层承诺

如果后续要讨论重构方向，应以具体执行链和实际节点体系为起点。

## 继续阅读

- [组件交互](../../overview/component-interactions.md)
- [架构运行时](../../overview/runtime.md)
- [Templates 架构设计](../templates/design.md)
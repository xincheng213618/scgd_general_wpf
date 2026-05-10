# FlowEngineLib

本页只描述当前仓库里真实可用的 FlowEngineLib 实现，不再继续维护“类图 + 理想化数据流 + 伪 API 表”式旧稿。

## 先看这个模块现在是什么

按当前源码状态，FlowEngineLib 不是一个抽象的流程设计概念，而是一套直接建立在节点编辑器之上的运行时执行核心。它当前至少承担四类事情：

- 承载流程画布与节点对象。
- 管理开始节点、服务节点和已加载画布。
- 把节点加入 `FlowNodeManager` 的设备视图。
- 在开始节点与结束节点之间闭合整条流程的完成事件。

因此它更接近“节点执行内核”，而不是旧文档里那种独立于宿主存在的通用 DSL 平台。

## 当前最关键的文件

- `Engine/FlowEngineLib/FlowEngineControl.cs`
- `Engine/FlowEngineLib/CVFlowContainer.cs`
- `Engine/FlowEngineLib/Base/CVCommonNode.cs`
- `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`
- `Engine/FlowEngineLib/Start/BaseStartNode.cs`
- `Engine/FlowEngineLib/End/CVEndNode.cs`
- `Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs`
- `Engine/FlowEngineLib/Base/CVStartCFC.cs`

如果只是想弄清流程如何加载、启动、转发和结束，这几处代码已经覆盖主链路。

## 当前控制面怎么分层

### 流程控制器

`FlowEngineControl` 是当前最核心的运行时控制器。按实现看，它负责：

- 挂接 `STNodeEditor`
- 跟踪开始节点字典 `startNodeNames`
- 跟踪服务节点字典 `services`
- 缓存已加载画布 `loadedCanvas`
- 触发流程完成事件 `Finished`

节点进入编辑器后，`FlowEngineControl` 会在 `NodeAdded` 事件里把它们分成两类处理：

- `BaseStartNode` 进入开始节点字典，并订阅 `Finished`
- `CVBaseServerNode` 进入服务节点集合，并同步到 `FlowNodeManager`

这比旧文档里“加载图后直接执行”那种描述更贴近真实实现。

### 多流程容器

`CVFlowContainer` 是和 `FlowEngineControl` 相邻的另一条控制线。它保留了：

- 多个开始节点的映射
- `startNodesFlowMap`
- append / load / start 组合能力

这说明 FlowEngineLib 当前不只服务于单张固定画布，也考虑了按 key 追加和启动流程的场景。

## 节点体系当前实际长什么样

### `CVCommonNode`

这是所有核心节点的共同基类，当前提供：

- `NodeName`
- `NodeType`
- `DeviceCode`
- `NodeID`
- `ZIndex`
- `nodeEvent`
- `nodeRunEvent`
- `nodeEndEvent`

另外它还统一了控件创建辅助方法，并在 `OnOwnerChanged()` 时向节点编辑器注册类型颜色。

### `BaseStartNode`

开始节点当前负责：

- 创建 `OUT_START` 与多个 `OUT_LOOP` 输出
- 维护 `Ready`、`Running` 和 `startActions`
- 把 `CVStartCFC` 分发到第一批连接节点
- 在流程完成后抛出 `Finished`

所以流程“开始”不是外部控制器单独完成的，而是落实在开始节点内部。

### `CVBaseServerNode`

这是当前最常见的执行节点基类。按实现看，它负责：

- 建立 `IN` / `OUT` 等节点端口
- 维护模板 ID、模板名、图片文件名、Token 和超时配置
- 组装基础请求数据
- 接收服务端响应并沿流程继续传递

旧文档里一直出现的 `DoServerWork` 并不是当前应被强调的扩展面；现在更真实的关注点是 `OnCreate()`、请求参数构建、响应处理和重置逻辑。

### `CVEndNode`

结束节点当前做的事情非常明确：

- 接收 `CVStartCFC` 或循环下一步输入
- 调用 `startAction.DoFinishing()`
- 最后调用 `startAction.FireFinished()`

这才是整条流程 finished 的真正闭环位置。

### `AlgorithmNode`

`AlgorithmNode` 是理解服务节点的一个很典型样本。它当前会：

- 维护算子类型、模板、POI 模板、颜色和缓存长度
- 在 `OnCreate()` 中建立节点内编辑控件
- 在 `getBaseEventData(...)` 中把模板、图像、颜色和 SMU 数据打包成算法请求参数

这再次说明 FlowEngineLib 当前节点的核心工作是“构建和转发执行参数”，而不是在节点里本地跑完整算法。

## 流程完成链当前怎么闭合

`CVStartCFC` 当前是整条流程状态在节点间传递的关键对象。它会记录：

- 起止时间
- 流程状态
- 串号
- 数据字典
- 对应的开始节点

流程结束时，`CVEndNode` 调用 `DoFinishing()` 和 `FireFinished()`，再回到 `BaseStartNode` 的 `Finished` 事件，最后由 `FlowEngineControl` 对外抛出 `FlowEngineEventArgs`。

这条链如果不连起来看，就很容易把“节点结束”和“流程结束”混成同一件事。

## 当前和宿主代码的边界

FlowEngineLib 本身只负责节点执行内核。真正把它接进 ColorVision 主程序的是 `Engine/ColorVision.Engine/Templates/Flow/` 那一层，例如：

- `FlowEngineManager.cs`
- `DisplayFlow.xaml.cs`
- `TemplateFlow.cs`

那里才负责：

- 结合 MQTT RC 服务令牌刷新流程画布
- 把流程模板从 Base64 加载进控制器
- 在 UI 里选择、编辑和运行流程

因此如果只读 FlowEngineLib 而不看模板层，会知道“怎么跑”，但不知道“谁在主程序里触发它跑”。

## 当前几个最容易写错的点

### 它不是宿主级完整工作流系统的全部代码

FlowEngineLib 只实现节点执行内核。进入主程序后的模板管理、窗口交互和数据加载，仍然在 `ColorVision.Engine/Templates/Flow/` 那一层。

### “节点完成”不等于“流程完成”

当前真正让流程完成落地的是 `CVEndNode -> CVStartCFC.FireFinished() -> BaseStartNode.Finished -> FlowEngineControl.Finished` 这条链，而不是任意一个节点发出 `nodeEndEvent`。

### 服务节点扩展点不要再围绕旧稿写法理解

当前真实扩展路径更接近：

- `OnCreate()`
- 参数组装
- 响应处理
- `Reset()`

继续照旧文档去找统一的“本地执行业务函数”，会把节点模型理解偏。

### `loadedCanvas` 不是装饰缓存

`FlowEngineControl` 和 `CVFlowContainer` 都会用画布内容哈希避免重复加载。这个细节会影响你对“为什么同一份流程不会重复重建”的理解。

## 推荐阅读顺序

1. `Engine/FlowEngineLib/FlowEngineControl.cs`
2. `Engine/FlowEngineLib/Base/CVCommonNode.cs`
3. `Engine/FlowEngineLib/Start/BaseStartNode.cs`
4. `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`
5. `Engine/FlowEngineLib/End/CVEndNode.cs`
6. `Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs`
7. `Engine/FlowEngineLib/Base/CVStartCFC.cs`
8. `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`

这样能先建立内核认知，再把它和宿主侧 UI 触发链连起来。

## 继续阅读

- [docs/04-api-reference/extensions/flow-node.md](../extensions/flow-node.md)
- [docs/03-architecture/components/engine/flow-engine.md](../../03-architecture/components/engine/flow-engine.md)
- [docs/04-api-reference/engine-components/ColorVision.Engine.md](./ColorVision.Engine.md)
# FlowEngineLib 节点扩展

本页只描述当前仓库里真实可用的 Flow 节点扩展路径，不再继续维护基于示意 API 的旧版“开发指南”。

## 先看节点体系实际长什么样

从当前代码看，Flow 节点扩展主要围绕这几类基类展开：

- `CVCommonNode`：所有节点的共同基类，提供 `NodeName`、`NodeType`、`DeviceCode`、`NodeID`、`ZIndex` 以及 `nodeEvent` / `nodeRunEvent` / `nodeEndEvent` 等公共能力。
- `BaseStartNode`：流程开始节点，负责创建 `CVStartCFC`、维护运行中的 `startActions`，并在流程结束时抛出 `Finished`。
- `CVBaseServerNode`：最常见的服务/算法类节点基类，负责输入输出、MQTT 请求组装、超时处理和节点级完成回传。
- `CVEndNode`：流程结束节点，最终调用 `startAction.FireFinished()` 把整条流程标记为完成。

这意味着当前节点扩展并不是一套“实现接口即可”的轻量插件模型，而是直接建立在 `STNode` 和一组具体基类之上。

## 当前最值得先看的代码锚点

如果你要新增或理解一个节点，优先看这些文件：

- `Engine/FlowEngineLib/Base/CVCommonNode.cs`
- `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`
- `Engine/FlowEngineLib/Start/BaseStartNode.cs`
- `Engine/FlowEngineLib/End/CVEndNode.cs`
- `Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs`

其中 `AlgorithmNode` 是一个很典型的现实例子：它不是在节点内部直接算图，而是收集模板、颜色、图像路径等参数，再拼出真正发往服务端的请求数据。

## 服务节点当前通常怎么扩展

从 `CVBaseServerNode` 的实现看，当前最常见的扩展方式是：

1. 继承 `CVBaseServerNode`。
2. 在构造函数里确定标题、`NodeType`、服务名和设备代码，并设置 `operatorCode` 等节点行为字段。
3. 在 `OnCreate()` 里添加输入输出或编辑控件。
4. 通过重写 `getBaseEventData(CVStartCFC start)` 组装真正发往执行端的参数对象。
5. 需要时重写 `OnServerResponse(...)`、`Reset(...)` 或连接相关虚方法，补充响应处理和清理逻辑。

旧文档里那种“重写 `DoServerWork` 就完成节点开发”的说法，和当前 `CVBaseServerNode` 的真实实现并不一致。

## 扩展骨架

新增服务节点通常继承 `CVBaseServerNode`，构造函数里确定标题、服务名、设备代码和 `operatorCode`，`OnCreate()` 添加输入输出或编辑控件，`getBaseEventData(...)` 组装请求参数，必要时重写 `OnServerResponse(...)` 或 `Reset(...)`。节点核心是“构建请求并接入现有执行链”，不是在节点里完成整段业务计算。

## 开始节点和结束节点分别控制什么

### `BaseStartNode`

开始节点负责创建并保存 `CVStartCFC`，通过 `m_op_start` 和多个 `m_op_loop` 分发启动动作，管理 `Ready`、`Running` 和进行中的 `startActions`，并在流程真正结束后抛出 `Finished`。

### `CVEndNode`

结束节点接收 `CVStartCFC` 或循环继续动作，在 `DoNodeEnded(...)` 中调用 `startAction.DoFinishing()`，最终调用 `startAction.FireFinished()`。这是当前代码里整条流程 finished 的真正出口。

## 当前几个最容易写错的点

### `nodeEndEvent` 不等于流程完成

`CVCommonNode.nodeEndEvent` 只表示节点级别的结束反馈。整条流程完成要走到 `CVEndNode`，再由 `startAction.FireFinished()` 触发。

### 不要围绕不存在的 `DoServerWork` 设计新节点

当前 `CVBaseServerNode` 真正的扩展点更接近：

- `OnCreate()`
- `getBaseEventData(...)`
- `OnServerResponse(...)`
- `Reset(...)`

如果按旧文档去找 `DoServerWork`，会直接把扩展路径理解错。

### 节点和服务主题不是自动推断万能匹配

`CVBaseServerNode` 当前通过 `GetSendTopic()`、`GetRecvTopic()`、`operatorCode` 和 `FlowServiceManager` 配合消息链。如果这些字段和服务端约定不一致，节点会表现成超时或收不到响应。

### 分类路径没有单一固定规范

当前 `[STNode("...")]` 的路径字符串是实际树结构的一部分，但仓库内现有节点已经混用了 `/00 全局`、`/03_2 Algorithm` 等风格。扩展时更应该遵循相邻节点的现有分组，而不是照搬旧文档里那套假定分类表。

## 推荐阅读顺序

1. `CVCommonNode`：先理解公共属性、事件和控件辅助方法。
2. `CVBaseServerNode`：再看典型服务节点怎样发起请求、等待响应和处理超时。
3. `BaseStartNode`：理解流程启动、循环输出和 `Finished` 事件来源。
4. `CVEndNode`：确认流程结束链在哪里闭环。
5. `AlgorithmNode` 或其他相邻真实节点：最后照着现有节点扩展，而不是从旧教程样板出发。

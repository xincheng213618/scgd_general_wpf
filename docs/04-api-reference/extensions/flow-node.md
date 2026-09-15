---
knowledge_id: "flow.node-extension"
knowledge_type: "guide"
status: "current"
summary: "说明服务与本地节点基类、请求与响应扩展点、分支输入隔离、属性编辑和流程完成的边界。"
aliases: ["FlowLocalExecution","CreateLocalExecution","如何新增Flow节点","CVCommonNode","CVBaseServerNode","LocalFlowNodeBase","CVStartCFC输入快照","getBaseEventData","CVEndNode"]
code_paths: ["Engine/FlowEngineLib/Base/FlowLocalExecution.cs","Engine/FlowEngineLib/Base/CVCommonNode.cs","Engine/FlowEngineLib/Base/CVBaseServerNode.cs","Engine/FlowEngineLib/Start/BaseStartNode.cs","Engine/FlowEngineLib/End/CVEndNode.cs","Engine/FlowEngineLib/PropertyEditor/FlowNodePropertyEditors.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFlowNodeBase.cs"]
test_paths: ["Test/ColorVision.UI.Tests/LvCameraLocalForwardingTests.cs","Test/ColorVision.UI.Tests/ConventionalFlowNodeTests.cs","Test/ColorVision.UI.Tests/LocalFlowNodePortTests.cs","Test/ColorVision.UI.Tests/FlowRuntimeCompletionTests.cs"]
related: ["platform.extensibility","flow.index","flow.runtime","ui.property-grid"]
---

# FlowEngineLib 节点扩展

Flow 节点建立在 `STNode` 和 `FlowEngineLib` 基类上。服务节点负责构建请求、接入 MQTT 执行链并处理响应；指定节点也可由宿主接管本地执行，沿用同一命令完成链。客户业务计算由对应服务或算法承担。

## 选择基类

| 基类 | 职责 | 源码位置 |
| --- | --- | --- |
| `CVCommonNode` | 节点公共属性、控件和节点事件 | `Engine/FlowEngineLib/Base/CVCommonNode.cs` |
| `CVBaseServerNode` | 输入输出、MQTT 请求、超时及响应处理 | `Engine/FlowEngineLib/Base/CVBaseServerNode.cs` |
| `LocalFlowNodeBase` | Engine 本地异步执行、多输入汇合及分支输入快照 | `Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFlowNodeBase.cs` |
| `BaseStartNode` | 创建 `CVStartCFC`、维护运行状态及启动动作 | `Engine/FlowEngineLib/Start/BaseStartNode.cs` |
| `CVEndNode` | 完成流程并发布终态 | `Engine/FlowEngineLib/End/CVEndNode.cs` |

`CVCommonNode` 提供 `NodeName`、`NodeType`、`DeviceCode`、`NodeID`、`ZIndex`，以及 `nodeEvent`、`nodeRunEvent`、`nodeEndEvent`。参数编辑使用[PropertyGrid 契约](../ui-components/property-grid.md)，Flow 编辑器注册见 `Engine/FlowEngineLib/PropertyEditor/FlowNodePropertyEditors.cs`。

## 扩展服务节点

1. 继承 `CVBaseServerNode`，在构造函数中设置标题、`NodeType`、服务名、设备代码和 `operatorCode`。
2. 在 `OnCreate()` 中添加输入输出或编辑控件。
3. 重写 `getBaseEventData(CVStartCFC start)`，组装执行端需要的参数。
4. 按需要重写 `OnServerResponse(...)`、`Reset(...)` 或连接相关虚方法，处理响应与清理。
5. 核对 `GetSendTopic()`、`GetRecvTopic()`、`operatorCode` 和 `FlowServiceManager` 中的服务配置，并使用目标协议样例验证请求与响应。

`Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs` 是服务节点示例：它收集模板、颜色和图像路径等参数，生成发往算法服务的请求。`[STNode("...")]` 决定节点树分类，扩展时采用相邻节点的实际分组。

Engine 本地节点由 `LocalFlowNodeBase` 在输入到达时捕获 `CVStartCFC` 快照，再把快照副本交给异步执行。Start 的同一个动作可以扇出到多条并行分支，但各节点不得直接修改这份共享输入；单输入和多输入节点都通过各自的快照传递 `MasterId`、`MasterResultType` 与 `MasterValue`。运行资源仍由同一次流程共享并按其资源生命周期管理，分支结果字段则保存在独立的 `Data` 字典中。

### 宿主接管服务节点的本地执行

`CVBaseServerNode.CreateLocalExecution(CVMQTTRequest)` 默认返回 `null`，保留 MQTT 路径。指定节点可返回宿主实现的 `FlowLocalExecution`；选择阶段抛异常时按本次命令失败处理，不回退到 MQTT。当前只有普通 `LVCameraNode` 通过宿主注册的工厂使用此入口，实际设备选择和原生相机调用留在 Engine；FlowEngineLib 不引用 Engine。

- `Execute()` 在后台准备结果；此阶段不交接流程帧或写流程结果记录。
- `Complete(CVStartCFC)` 仅在响应成功领取原命令完成权后调用，负责落库、资源交接和生成普通响应数据。原消息 ID、超时、暂停响应缓存、失败策略和节点完成事件继续由基类管理。
- `Dispose()` 释放未被接受的结果，包括超时或停止后的晚到帧；已交给流程的资源由流程寿命管理。

本地分支在执行前复制 `CVStartCFC.Data`，避免结果字段串入并行分支，仍共享 `RuntimeResources`。不要通过修改下一次打开偏好改变执行中的后端。相机参数、保存和转发范围见[相机采集契约](../../01-user-guide/devices/camera.md)。

## 流程完成与节点完成

`BaseStartNode` 创建并保存 `CVStartCFC`，通过 `m_op_start` 和 `m_op_loop` 分发启动动作，管理 `Ready`、`Running` 及 `startActions`。`CVEndNode` 接收开始或循环动作；其 `DoNodeEnded(...)` 仅在 `TryDoFinishing()` 成功时调用 `FireFinished()`，避免重复发布流程完成。

`nodeEndEvent` 只表示单个节点结束。整条流程的完成需要到达结束节点并发布 `FireFinished()`；服务主题或操作码不匹配通常表现为超时或没有响应。运行会话的状态与失败语义见[FlowEngineLib](../engine-components/FlowEngineLib.md)。

## 验证

- `ConventionalFlowNodeTests.cs`：常规节点契约。
- `LocalFlowNodePortTests.cs`：本地节点端口。
- `FlowRuntimeCompletionTests.cs`：流程终态。
- `LvCameraLocalForwardingTests.cs`：服务节点由宿主本地执行时的路由、结果交接、命令终态和资源释放。

测试均位于 `Test/ColorVision.UI.Tests/`。新增服务协议需要目标节点的请求、响应和资源生命周期用例；硬件执行应在获授权的测试环境中验证。

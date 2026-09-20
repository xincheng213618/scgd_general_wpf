---
knowledge_id: "flow.node-extension"
knowledge_type: "guide"
status: "current"
summary: "说明服务与本地节点基类、请求与响应扩展点、分支输入隔离、属性编辑和流程完成的边界。"
aliases: ["FlowLocalExecution","CreateLocalExecution","如何新增Flow节点","CVCommonNode","CVBaseServerNode","LocalFlowNodeBase","CVStartCFC输入快照","getBaseEventData","CVEndNode"]
code_paths: ["Engine/FlowEngineLib/Base/FlowLocalExecution.cs","Engine/FlowEngineLib/Base/CVCommonNode.cs","Engine/FlowEngineLib/Base/CVBaseServerNode.cs","Engine/FlowEngineLib/Start/BaseStartNode.cs","Engine/FlowEngineLib/End/CVEndNode.cs","Engine/FlowEngineLib/PropertyEditor/FlowNodePropertyEditors.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFlowNodeBase.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility"]
test_paths: ["Test/ColorVision.UI.Tests/CompatibilityNodeMigrationTests.cs", "Test/ColorVision.UI.Tests/LvCameraLocalForwardingTests.cs","Test/ColorVision.UI.Tests/ConventionalFlowNodeTests.cs","Test/ColorVision.UI.Tests/LocalFlowNodePortTests.cs","Test/ColorVision.UI.Tests/FlowRuntimeCompletionTests.cs"]
related: ["platform.extensibility","flow.index","flow.runtime","ui.property-grid"]
---

# FlowEngineLib 节点扩展

Flow 节点建立在 `STNode` 和 `FlowEngineLib` 基类上。服务节点负责构建请求、接入 MQTT 执行链并处理响应；指定节点也可由宿主接管本地执行，沿用同一命令完成链。客户业务计算由对应服务或算法承担。

## 选择基类

| 基类 | 职责 | 源码位置 |
| --- | --- | --- |
| `CVCommonNode` | 节点公共属性、控件和节点事件 | `Engine/FlowEngineLib/Base/CVCommonNode.cs` |
| `CVDeviceNode` | 原有服务与控制节点的设备标识，保留其流程保存字段 | `Engine/FlowEngineLib/Base/CVDeviceNode.cs` |
| `CVBaseServerNode` | 输入输出、MQTT 请求、超时及响应处理 | `Engine/FlowEngineLib/Base/CVBaseServerNode.cs` |
| `LocalFlowNodeBase` | Engine 本地异步执行、多输入汇合及分支输入快照 | `Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFlowNodeBase.cs` |
| `LocalDeviceFlowNodeBase` | 本地相机和校正节点的资源选择 | `Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalDeviceFlowNodeBase.cs` |
| `BaseStartNode` | 创建 `CVStartCFC`、维护运行状态及启动动作 | `Engine/FlowEngineLib/Start/BaseStartNode.cs` |
| `CVEndNode` | 完成流程并发布终态 | `Engine/FlowEngineLib/End/CVEndNode.cs` |

`CVCommonNode` 提供 `NodeName`、`NodeType`、`NodeID`、`ZIndex`，以及 `nodeEvent`、`nodeRunEvent`、`nodeEndEvent`，不包含设备标识。`CVDeviceNode` 和 `LocalDeviceFlowNodeBase` 实现 `IFlowDeviceNode`，声明 `DeviceCode`；服务节点从 `CVDeviceNode` 继承。通用代码仅在节点实现该接口时读取设备标识，不再从 `CVCommonNode` 获取。参数编辑使用[PropertyGrid 契约](../ui-components/property-grid.md)，模板和量程编辑器见 `Engine/ColorVision.Engine/PropertyEditor/FlowTemplatePropertiesEditors.cs`。

迁入 Engine、仍保留旧流程名称和保存标识的节点集中在 `Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/`，再按设备或功能分组。其中需要 Engine 模板或量程编辑器的相机、校准、POI、SMU、传感器和算法节点分别放在对应子目录，保留各自原 namespace、类名和保存标识；目录名不参与流程序列化。只有普通字符串、数值或枚举属性的节点，以及通用执行基类，继续由 FlowEngineLib 提供。迁移范围以属性编辑器依赖为准，无需整体搬迁节点库。

Engine 内的模板与量程属性直接通过 `PropertyEditorType` 引用具体编辑器，声明放在属性定义上；例如 `BaseCameraNode` 声明的四个模板编辑器由 L/BV 节点继承。属性编辑不再经过类级名称映射或 Selector。校正模板依赖设备的刷新、增益联动和模板选择回写保持一致。公共基类的设备字段仍使用 `FlowDeviceNameEditor` 代理，让 FlowEngineLib 不引用 Engine 业务 UI。

本地节点中，只有相机取图和校正（含校正+实时 POI）保留“设备代码”，用于选择所需资源。十字定位、点阵畸变、发光区定位、FOV、POI、布点等纯算法节点，以及本地图片、文件景深融合节点，不声明、保存或在运行参数中携带 `DeviceCode`，也不自动选择算法设备或相机。旧流程中的多余字段按 STN 未知属性规则忽略，重新保存后不再写出；设备节点的字段和保存标识保持不变。

纯算法与文件节点的结果主表不填写设备代码，按批次、节点及主结果 ID 交接，通过 `ResultRoutes.LocalFlow` 发布进程内通知。结果仍可从流程批次或历史查询查看，不再自动推送到某个设备的结果窗口。FOV 可记录输入图像携带的相机来源；没有来源时保持空值，不猜选唯一配置的相机。此关联不参与 FOV 标量参数计算。

## 扩展服务节点

1. 继承 `CVBaseServerNode`，在构造函数中设置标题、`NodeType`、服务名、设备代码和 `operatorCode`。
2. 在 `OnCreate()` 中添加输入输出或编辑控件。
3. 重写 `getBaseEventData(CVStartCFC start)`，组装执行端需要的参数。
4. 按需要重写 `OnServerResponse(...)`、`Reset(...)` 或连接相关虚方法，处理响应与清理。
5. 核对 `GetSendTopic()`、`GetRecvTopic()`、`operatorCode` 和 `FlowServiceManager` 中的服务配置，并使用目标协议样例验证请求与响应。

`Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs` 是服务节点示例：它收集模板、颜色和图像路径等参数，生成发往算法服务的请求。`[STNode("...")]` 决定节点树分类，扩展时采用相邻节点的实际分组。

Engine 本地节点由 `LocalFlowNodeBase` 在输入到达时捕获 `CVStartCFC` 快照，再把快照副本交给异步执行。Start 的同一个动作可以扇出到多条并行分支，但各节点不得直接修改这份共享输入；单输入和多输入节点都通过各自的快照传递 `MasterId`、`MasterResultType` 与 `MasterValue`。运行资源仍由同一次流程共享并按其资源生命周期管理，分支结果字段则保存在独立的 `Data` 字典中。执行返回或抛出异常时，如果本次 `RuntimeResources` 已释放，基类丢弃迟到的完成，不再发布节点结束事件或继续传递输出；节点仍需自行取消在途计算并在文件、数据库等写入前检查有效性。

### 宿主接管服务节点的本地执行

`CVBaseServerNode.CreateLocalExecution(CVMQTTRequest)` 默认调用可选的宿主工厂 `FlowLocalExecution.CreateForNode`；未注册或返回 `null` 时保留 MQTT 路径。对应的 `CanExecuteLocally` 判断用于 `RequiresRemoteService`，使编辑器、无界面执行与断线 MQTT 开始节点按实际后端检查服务依赖。Engine 注册普通光谱/EQE 节点的本地转发，节点类型和序列化身份仍保留在 FlowEngineLib。指定节点也可重写方法返回宿主实现的 `FlowLocalExecution`；选择阶段抛异常时按本次命令失败处理，不回退到 MQTT。普通 `FlowEngineLib.LVCameraNode` 的实现在 Engine 内，直接调用本地相机执行器，返回 `null` 时继续原服务请求。它通过 `STNodeSerializationModelAttribute` 保留旧端可识别的保存标识；FlowEngineLib 只提供执行基类和宿主钩子，不引用 Engine。

- `Execute()` 在后台准备结果；此阶段不交接流程帧或写流程结果记录。
- `Complete(CVStartCFC)` 仅在响应成功领取原命令完成权后调用，负责落库、资源交接和生成普通响应数据。原消息 ID、超时、暂停响应缓存、失败策略和节点完成事件继续由基类管理。
- `Dispose()` 释放未被接受的结果，包括超时或停止后的晚到帧；已交给流程的资源由流程寿命管理。

本地分支在执行前复制 `CVStartCFC.Data`，避免结果字段串入并行分支，仍共享 `RuntimeResources`。不要通过修改下一次打开偏好改变执行中的后端。相机参数、保存和转发范围见[相机采集契约](../../01-user-guide/devices/camera.md)。

## 流程完成与节点完成

`BaseStartNode` 创建并保存 `CVStartCFC`，通过 `m_op_start` 和 `m_op_loop` 分发启动动作，管理 `Ready`、`Running` 及 `startActions`。`CVEndNode` 接收开始或循环动作；其 `DoNodeEnded(...)` 仅在 `TryDoFinishing()` 成功时调用 `FireFinished()`，避免重复发布流程完成。

`nodeEndEvent` 只表示单个节点结束。整条流程的完成需要到达结束节点并发布 `FireFinished()`；服务主题或操作码不匹配通常表现为超时或没有响应。运行会话的状态与失败语义见[FlowEngineLib](../engine-components/FlowEngineLib.md)。

## 验证

- `ConventionalFlowNodeTests.cs`：常规节点契约。
- `CompatibilityNodeMigrationTests.cs`：迁移前的 30 节点合成画布，检查持久化字段、连线、请求、菜单可见性及直接属性编辑器。
- `LocalFlowNodePortTests.cs`：本地节点端口、资源节点设备字段保存，以及纯算法节点忽略旧字段且不再写出的契约。
- `FlowRuntimeCompletionTests.cs`：流程终态。
- `LvCameraLocalForwardingTests.cs`：服务节点由宿主本地执行时的路由、结果交接、命令终态和资源释放。

测试均位于 `Test/ColorVision.UI.Tests/`。新增服务协议需要目标节点的请求、响应和资源生命周期用例；硬件执行应在获授权的测试环境中验证。

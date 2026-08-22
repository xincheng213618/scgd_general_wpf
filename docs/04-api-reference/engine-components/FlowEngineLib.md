# FlowEngineLib

`Engine/FlowEngineLib/` 是节点图执行内核，不是完整的宿主工作流系统。模板持久化、版本和搜索位于 `ColorVision.Engine/Templates/Flow/`；编辑器、交互式/无界面执行、前后处理和诊断位于 `ColorVision.Engine/FlowProcessing/`，项目结果处理仍在 `Projects/*`。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| Base64 流程加载后没有节点 | Base64 是否为空、`NodeEditor.LoadCanvas(rawData)`、节点类型是否可用 |
| 重复打开同一流程没有变化 | `loadedCanvas` 的 MD5 缓存是否直接命中 |
| 开始按钮点了但没跑 | `GetStartNodeName()`、`startNodeNames`、`BaseStartNode.Ready` |
| 服务节点没有设备 | `FlowNodeManager.UpdateDevice`、`MQTTServiceInfo`、节点 `NodeType` |
| 节点执行但流程不结束 | 是否连接 `CVEndNode`，是否走到 `CVStartCFC.FireFinished()` |
| `Finished` 重复触发 | `clear()` 是否解绑旧 `BaseStartNode.Finished` |
| 项目包收不到结果 | `FlowEngineControl.Finished` 到宿主 `FlowCompleted` 的桥接 |
| UI 选择和节点参数不一致 | `FlowProcessing/Editor/NodeConfiguration/` 是否把参数写回节点 |

## 控制面

| 对象 | 负责 |
| --- | --- |
| `FlowEngineControl` | 通过图宿主加载画布，管理开始节点和服务节点，抛出引擎级 `Finished` |
| `CVFlowContainer` | 多开始节点、按 key 追加/加载/启动流程 |
| `FlowNodeManager` / `FlowServiceManager` | 设备视图、服务节点同步和 MQTT service 绑定 |
| `FlowEngineAPI` | 启动、停止、开始节点查询的外部接口 |
| `FlowRuntimeHost` / `FlowEngineRunner` | 为无界面执行持有隔离节点图、服务快照和明确的加载/运行/停止生命周期 |

`FlowEngineControl.NodeAdded` 会把节点分成两类：`BaseStartNode` 进入 `startNodeNames` 并订阅完成事件；`CVBaseServerNode` 进入服务节点集合并同步到设备视图。

## 核心节点

| 节点/基类 | 重点 |
| --- | --- |
| `CVCommonNode` | 节点名、类型、设备码、端口事件、颜色注册 |
| `BaseStartNode` | 创建开始输出，维护 Ready/Running，分发 `CVStartCFC` |
| `CVBaseServerNode` | 模板、图片、Token、超时、请求参数和服务端响应 |
| `CVEndNode` | 调用 `DoFinishing()` 和 `FireFinished()` 闭合流程 |
| `AlgorithmNode` / `AlgorithmARVRNode` | 把模板、图像、颜色、POI、SMU 数据打包成算法请求 |

大部分节点的核心职责是构建并转发执行参数，不是在本地完成完整算法。

## 弃用节点兼容

标记 `Obsolete` 的节点类型会从 `STNodeTreeView` 的新建/右键目录和 Copilot 节点目录中排除，但仍由节点类型注册表保留，因此旧画布可以继续反序列化。例如旧 MQTT、V5 开始/结束、合规验证、ROI、第三方算法、校正和图像拼接节点都走这条兼容路径；完成存量流程迁移前不要删除这些类型。

## 完成链路

```mermaid
flowchart TD
  Start["BaseStartNode.Start(sn)"] --> CFC["CVStartCFC"]
  CFC --> Server["CVBaseServerNode / 具体节点"]
  Server --> End["CVEndNode"]
  End --> Finish["CVStartCFC.FireFinished()"]
  Finish --> StartEvent["BaseStartNode.Finished"]
  StartEvent --> EngineEvent["FlowEngineControl.Finished"]
  EngineEvent --> Host["FlowControl.FlowCompleted / EngineExecutionCompleted"]
  Host --> Finalizer["FlowRunFinalizer / PostProcess"]
  Finalizer --> Finalized["RunFinalized"]
```

“节点完成”不等于“流程图完成”。流程图必须进入 End 节点并触发 `Finished` 链；`EngineExecutionCompleted` 之后仍可能执行后处理，需要最终业务结果的调用方应等待 `RunFinalized`。

## 宿主边界

FlowEngineLib 只知道节点图和引擎执行状态。主程序里的这些工作分布在模板层和 `FlowProcessing`：

| 工作 | 入口 |
| --- | --- |
| 持久化 Base64 流程和 `.cvflow` 包 | `Templates/Flow/TemplateFlow.cs`、`Templates/Flow/FlowPackageHelper.cs` |
| 显示和编辑流程 | `FlowProcessing/Runtime/ViewFlow.xaml.cs`、`FlowProcessing/Editor/FlowEditorCanvas.xaml.cs` |
| 编排交互式执行和最终化 | `FlowProcessing/Runtime/FlowExecutionSession.cs`、`FlowRunExecutor.cs`、`FlowRunFinalizer.cs` |
| 执行已保存流程或无界面流程 | `FlowProcessing/Runtime/FlowExecutionCoordinator.cs`、`FlowHeadlessExecutionService.cs` |
| 给节点绑定设备/模板/参数 | `FlowProcessing/Editor/NodeConfiguration/` |

如果问题是模板下拉、流程保存、项目结果解析，通常不在 FlowEngineLib 里修。

## 检查

| 验收项 | 通过标准 |
| --- | --- |
| 构建和依赖 | `FlowEngineLib.csproj`、ST.Library.UI、MQTT/JSON 依赖能加载 |
| 画布加载 | Base64 或文件能加载节点，相同画布不会重复加载 |
| 节点发现 | 开始节点进入 `startNodeNames`，服务节点进入服务集合 |
| 服务绑定 | 外部 `MQTTServiceInfo` 能绑定到服务节点 |
| 启动链 | 输入 SN 后能从正确开始节点启动，运行状态正确 |
| 参数链 | 模板、图像、颜色、POI、SMU 能进入请求数据 |
| 完成链 | 结束时能抛出 SN、状态、耗时、消息和错误节点 |
| 清理 | 停止或重新加载后不叠加旧事件 |
| 宿主桥接 | `ViewFlow`/`FlowExecutionSession` 能接到模板、服务、运行按钮和最终化结果 |

## 不要这样理解

- FlowEngineLib 不是完整 DSL 平台；它是节点执行内核。
- 不要把项目判定写进节点内核。
- 不要把服务绑定问题误判成节点执行问题，先看 `FlowProcessing/Editor/NodeConfiguration/` 和服务快照；也不要忽略会影响重复加载的 `loadedCanvas` 缓存。

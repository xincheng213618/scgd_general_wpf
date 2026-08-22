# Flow 架构

当前 Flow 已从旧的模板目录内 UI 链拆成执行内核、持久模板、编辑器、运行编排和阶段扩展五个真实边界。

## 目录边界

- `Engine/FlowEngineLib/`：节点图、开始/结束节点、服务节点、隔离 RuntimeHost 和无 UI 运行器。
- `Engine/ColorVision.Engine/Templates/Flow/`：流程模板、STN 画布数据、`.cvflow` 包、版本和搜索侧车。
- `Engine/ColorVision.Engine/FlowProcessing/Editor/`：画布、属性面板、节点菜单、布局、校验和独立编辑窗口。
- `Engine/ColorVision.Engine/FlowProcessing/Runtime/`：工作区、交互式会话、执行/最终化与无界面协调。
- `FlowProcessing/{PreProcess,PostProcess,Diagnostics,Scheduling,Compilation}/`：阶段扩展、诊断恢复、Quartz 和 STND v1 投影。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 流程无法启动 | `FlowValidator`、开始节点、service snapshot、`FlowRunLifecycleGate` |
| 节点执行完但流程没结束 | `nodeEndEvent` 只是节点事件；整图结束看 `CVEndNode` 和 `FlowCompleted` |
| 共享 UI/调度链引擎结束但结果未完成 | 后处理仍可运行；等待 `RunFinalized` / `RunFlowAndWaitForFinalizationAsync()` |
| 快速切模板后画布错乱 | `FlowTemplateWorkspaceController` generation 和 latest-wins 提交 |
| 节点属性缺设备/模板选择 | `FlowNodePropertyMetadataProvider`、`Editor/NodeConfiguration/` |
| Quartz 卡住 | 区分 `FlowJob` 与 `HeadlessFlowJob`，检查最终结果等待方式 |
| 无界面执行触碰编辑器 | 应使用独立 `FlowRuntimeHost` 的 `FlowHeadlessExecutionService` |
| 失败难追踪 | `FlowExecutionJournalCoordinator`、Run/Event/Attempt 和 Incident |

## 主要所有者

| 对象 | 责任 |
| --- | --- |
| `FlowEngineControl` | 加载图、识别开始/服务节点、启动/停止并发布整图完成 |
| `IFlowGraphHost` 及两个实现 | 适配可视 `STNodeEditor` 与无界面 `CVNodeContainer`，不拥有选择/缩放/命令 UI |
| `FlowRuntimeHost` / `FlowEngineRunner` | 隔离一代非可视图及运行生命周期，不访问 WPF Application/Dispatcher/Window |
| `TemplateFlow` / `FlowPackageHelper` | 数据库模板、STN Base64 和 `.cvflow`；本地 `.stn` 由工作区打开/保存 |
| `FlowEditorCanvas` | 组合节点画布、属性面板、文档视图、编辑命令和布局 |
| `FlowTemplateWorkspaceController` | 每个 `ViewFlow` 的模板选择、加载、起点和 refresh generation |
| `FlowExecutionSession` / `FlowRunExecutor` | 批次、前处理、一次引擎运行、取消/超时和引擎完成事件 |
| `FlowRunFinalizer` / `FlowRunLifecycleGate` | 后处理、最终 outcome、持久化收尾和单 active run 门禁 |
| `FlowExecutionCoordinator` / `FlowHeadlessExecutionService` | UI 调度入口和不可变 snapshot 的隔离无界面执行 |

## 两条执行链

```text
UI: ViewFlow -> FlowExecutionSession -> snapshot/gate/batch/journal/preprocess
    -> FlowRunExecutor -> FlowControl -> FlowEngineLib -> FlowRunFinalizer -> RunFinalized
Headless: FlowExecutionCoordinator -> immutable STN/service request
    -> FlowHeadlessExecutionService -> new FlowRuntimeHost -> FlowEngineRunner -> result
```

事件含义不能混用：`nodeEndEvent` 是单节点结束；`FlowControl.FlowCompleted` / `EngineExecutionCompleted` 是图引擎结束；`RunFinalized` 才表示引擎和全部后处理已收尾。`FlowExecutionCompleted` 只是过时兼容别名，Required 后处理失败会改变最终结果，Warning 失败只记录警告。

无界面服务不读当前画布、不复用 `ViewFlow`/`FlowControl`，也不自动创建 UI 批次或执行前后处理。请求创建时复制 STN 和 service token；调用方需要批次或业务阶段时必须显式编排。

## 编辑与扩展

`FlowEngineToolWindow` 只承载 standalone `ViewFlow`，由 `ViewFlow` 组合 `FlowEditorCanvas`。Canvas 持有 `STNodeEditor`；空白右键动态发现节点，布局、导航、校验和文档展示由 Editor 内各自的真实服务负责。

- 普通字段用 `STNodePropertyAttribute`；专用控件用 `FlowNodePropertyEditorAttribute`，经 metadata provider、selector 和 registry 接入统一 PropertyEditor。
- 多模板族或随算法变化的节点选择面板放进 `Editor/NodeConfiguration/`，复用 `NodeConfiguratorRegistry` 和 `NodePanelBuilder`。
- 节点执行语义放 `FlowEngineLib`，本地节点放 `FlowProcessing/Nodes/`，不要把运行或属性 UI 塞回 `TemplateFlow`。

## 调度、诊断与边界

- `FlowJob` 走当前选中 UI 流程和完整批次/前后处理；`HeadlessFlowJob` 走已保存 STN，并防止同一 JobDetail 并发。
- journal 和 abandoned-run recovery 是 fail-open 辅助诊断，诊断库不可用不能阻止生产流程。
- `FlowCanvasCatalogBuilder` 从 STND v1 生成版本/搜索投影，不建立 live editor graph、不改源画布；codec 可能短暂实例化节点以发现 option schema。
- `FlowEngineLib` 不拥有模板数据库、WPF 工作区、批次或项目后处理；UI 会话与 RuntimeHost 不共享可变画布或节点实例。
- 部分现有 `Projects/*` 窗口仍直接持有 `FlowControl`，在 `FlowCompleted` 后执行项目自己的最终化；它们尚未接入共享 `RunFinalized`，不能按目标架构描述为已经迁移。
- 没有开始节点、service snapshot 不完整或门禁拒绝时必须明确拒绝启动；`DisplayFlow` 只是宿主壳。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 节点执行 | `Engine/FlowEngineLib/FlowEngineControl.cs`、`Engine/FlowEngineLib/Runtime/{FlowGraphHost,FlowRuntimeHost,FlowEngineRunner}.cs` |
| 模板与包 | `Engine/ColorVision.Engine/Templates/Flow/{TemplateFlow,FlowPackageHelper}.cs` |
| 编辑器与属性 | `Engine/ColorVision.Engine/FlowProcessing/Editor/FlowEditorCanvas.xaml.cs`、`FlowNodePropertyMetadataProvider.cs`、`NodeConfiguration/` |
| UI 工作区 | `Engine/ColorVision.Engine/FlowProcessing/Runtime/{ViewFlow.xaml.cs,FlowTemplateWorkspaceController.cs}` |
| UI 执行 | `Engine/ColorVision.Engine/FlowProcessing/Runtime/{FlowExecutionSession,FlowRunExecutor,FlowRunFinalizer,FlowRunLifecycleGate}.cs` |
| 无界面执行 | `Engine/ColorVision.Engine/FlowProcessing/Runtime/{FlowExecutionCoordinator,FlowHeadlessExecutionService}.cs` |
| 调度/投影/诊断 | `Engine/ColorVision.Engine/FlowProcessing/{Scheduling,Compilation,Diagnostics}/` |

存储、`.cvflow`、调度和验收契约见 [流程引擎 API](../../../04-api-reference/algorithms/templates/flow-engine.md)。

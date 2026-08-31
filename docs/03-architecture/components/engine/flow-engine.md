---
knowledge_id: "flow.architecture"
knowledge_type: "topic"
status: "current"
summary: "区分 Flow 底层画布、节点内核、模板存储、编辑工作区、共享会话与隔离执行的所有权。"
aliases: ["Flow架构","工作流程","自动化流程","节点结束是不是流程结束","FlowRuntimeHost","FlowCompleted","RunFinalized"]
code_paths: ["Engine/FlowEngineLib/FlowEngineControl.cs","Engine/FlowEngineLib/Runtime","Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/FlowEditorCanvas.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowExecutionSession.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowHeadlessExecutionService.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FlowFinalizedExecutionApiTests.cs","Test/ColorVision.UI.Tests/FlowRuntimeHostTests.cs","Test/ColorVision.UI.Tests/FlowTemplateWorkspaceControllerTests.cs"]
related: ["flow.index","flow.editor","flow.runtime","flow.templates","flow.workspace","flow.session","flow.headless"]
---

# Flow 架构与责任边界

Flow 将设备动作、模板处理和结果输出组织成可重复的节点图，但“编辑画布、保存模板、运行节点、完成业务”由不同对象负责。本页只定义跨模块所有权与调用关系，各层具体行为在所属主题维护。

## 按职责定位

| 边界 | 主要所有者 | 权威主题 |
| --- | --- | --- |
| 底层画布与序列化 | `STNodeEditor`、`STNodeOption`、`CVNodeContainer`：WPF 输入、节点/端口、STN 兼容 | [ST.Library.UI](../../../04-api-reference/engine-components/ST.Library.UI.md) |
| 节点执行内核 | `FlowEngineControl`、`IFlowGraphHost`、`FlowRuntimeHost` / `FlowEngineRunner`：图加载、服务绑定与引擎状态 | [FlowEngineLib](../../../04-api-reference/engine-components/FlowEngineLib.md) |
| 模板存储 | `TemplateFlow` / `FlowPackageHelper`：MySQL、Base64 STN、流程包、版本/搜索侧车 | [Flow 模板与包](../../../04-api-reference/engine-components/template-flow-chain.md) |
| 编辑工作区 | `ViewFlow`、`FlowEditorCanvas`、`FlowTemplateWorkspaceController`：命令、active/requested 模板、文档基线和刷新代际 | [工作区契约](../../../01-user-guide/workflow/design.md) |
| 共享业务会话 | `FlowExecutionSession` / `FlowRunExecutor` / `FlowRunFinalizer` / `FlowRunLifecycleGate`：批次、前后处理、门禁、最终 outcome 与诊断 | [执行会话](../../../01-user-guide/workflow/execution.md) |
| 隔离无界面执行 | `FlowExecutionCoordinator` / `FlowHeadlessExecutionService`：请求副本、独立 RuntimeHost、裸图终止结果 | [无界面执行](../../../04-api-reference/algorithms/templates/flow-engine.md) |

`IFlowGraphHost` 将可视 `STNodeEditor` 与非可视 `CVNodeContainer` 适配为执行所需的图，但不拥有选择、缩放或文档命令。`FlowRuntimeHost` 不访问 WPF Application/Dispatcher/Window；隔离图与编辑器不能共享可变节点实例。

`FlowProcessing/Editor/` 持有画布组合、布局、菜单、属性面板和校验；`FlowProcessing/Runtime/` 持有工作区与运行编排。`Templates/Flow/` 不应重新吸收这些 UI/运行职责。`PreProcess`、`PostProcess`、`Diagnostics`、`Scheduling`、`Compilation` 分别提供阶段扩展、诊断、调度和语义投影。

## 两条执行链

```text
UI/FlowJob → ViewFlow → FlowExecutionSession → 批次/前处理/门禁
           → FlowRunExecutor → FlowControl → FlowEngineLib
           → FlowRunFinalizer → RunFinalized

裸请求/HeadlessFlowJob → FlowExecutionCoordinator → 不可变STN/服务请求
                      → FlowHeadlessExecutionService
                      → 新FlowRuntimeHost / FlowEngineRunner → 裸图终止结果
```

第二条链不会自动获得第一条链的批次、前后处理或 `RunFinalized`。按 `FlowKey` 读取模板的协调器仍可能使用 WPF Dispatcher 访问模板集合；“隔离图执行不触碰编辑器”不等于所有入口都完全不依赖宿主。

事件层次是“单节点结束 → 整图引擎结束 → 宿主业务最终化”，不能因某层事件名称含 Completed 就跨层判断成功。完整完成判据只在[执行会话](../../../01-user-guide/workflow/execution.md)和[无界面结果契约](../../../04-api-reference/algorithms/templates/flow-engine.md)中维护。

## 扩展与不可跨越的边界

- 公共节点执行语义归 `FlowEngineLib`；Engine 本地节点归 `FlowProcessing/Nodes/`。客户判定、协议字段和导出映射仍在 `Projects/*`。
- 普通节点字段接入统一属性元数据；多模板族等专用补充面板归 `Editor/NodeConfiguration/`，具体规则见[工作区](../../../01-user-guide/workflow/design.md)，不要在模板存储类里创建属性 UI。
- `FlowEngineToolWindow` 只承载 standalone `ViewFlow`；主窗口与独立窗口共用视图，但文档目标和工作区状态不相同。
- 诊断与版本/搜索投影是辅助链，分别由执行主题和模板主题定义失败边界；它们不能替代生产流程结果或源 STN。
- 部分现有 `Projects/*` 窗口仍直接持有 `FlowControl`，在 `FlowCompleted` 后完成项目自己的最终化；未迁移前不能描述为已接入共享 `RunFinalized`。
- 运行、停止、模板保存/导入、Incident 处置和设备动作有不同副作用。架构入口不授予这些动作的执行权限。

## 症状到责任

| 症状 | 对应边界 |
| --- | --- |
| 画布加载后节点或连线缺失 | ST 类型目录、序列化兼容与节点程序集 |
| 快速切模板显示错对象，保存到了错误模板 | 工作区 active/requested 身份、generation 与文档目标 |
| 保存成功但版本或搜索缺项 | 模板层 catalog 侧车，不先重写源 STN |
| 启动被拒绝、卡住或引擎结束后仍未完成 | 共享会话的服务/门禁/后处理，或裸执行器自己的终止结果 |
| Quartz 行为与手动运行不同 | 先识别 `FlowJob` 或 `HeadlessFlowJob`，再查输入来源与完成边界 |
| 本地完成但客户输出不正确 | 对应项目的最终化与结果映射，而非 ST 库或裸运行器 |

## 验证边界

`FlowRuntimeHostTests` 覆盖隔离宿主，`FlowTemplateWorkspaceControllerTests` 覆盖工作区状态，`FlowFinalizedExecutionApiTests` 覆盖共享最终化接口。它们是不同层的证据，不代表整个架构、设备链或项目输出已经验证。改动应沿上表定位最小测试，并另行记录真实数据库、MQTT、设备和项目兼容入口的缺口。

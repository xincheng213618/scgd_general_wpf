---
knowledge_id: "flow.session"
knowledge_type: "topic"
status: "current"
summary: "FlowExecutionSession 的启动前提、停止请求与最终化判据，以及按失败阶段定位证据。"
aliases: ["流程运行","流程没结束","RunFinalized","执行调试","StopFlow","CVBaseServerNode","FlowExecutionSession","FlowJob"]
code_paths: ["Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowExecutionSession.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowRunExecutor.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowRunFinalizer.cs","Engine/ColorVision.Engine/FlowProcessing/Scheduling/FlowJob.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FlowFinalizedExecutionApiTests.cs","Test/ColorVision.UI.Tests/FlowRunFinalizerTests.cs"]
related: ["flow.architecture","flow.templates","flow.workspace","flow.headless","flow.diagnostics"]
---

# Flow 启动、停止与最终化

`FlowExecutionSession` 负责共享 Flow 执行会话：校验模板快照和起始节点、检查所需服务、执行前处理、启动节点图并最终化。本页用于判断“为什么没启动、停在哪里、什么时候才算完成”。编辑对象与当前画布见[工作区](./design.md)，数据库/包格式见[模板持久化](../../04-api-reference/engine-components/template-flow-chain.md)，不需要 UI 批次和前后处理的隔离图运行见[无界面执行](../../04-api-reference/algorithms/templates/flow-engine.md)。

运行可能控制设备、写数据库或调用外部系统；停止也会影响正在执行的任务。查询执行方法不授权实际运行或停止，复测前应确认输入、目标设备和外部输出范围。

## 启动前提

| 检查项 | `RunFlowAsync` 内部执行链的行为 |
| --- | --- |
| 有可执行的模板快照 | 等待 `WaitForExecutionSnapshotAsync()`；无法取得时记录原因并返回，不启动节点图 |
| 当前图包含 `CVBaseServerNode` | 此时才要求 `MqttRCService.IsConnect` 且 `ServiceTokens` 非空；缺 token 时请求刷新并返回 |
| 已选择有效起始节点 | 刷新起始节点选择；无效或为空时提示并返回 |
| 当前没有活动运行且模板身份仍有效 | 生命周期门禁拒绝并发启动；工作区在等待后又切换模板时拒绝使用过期快照 |
| 输入与前处理通过 | 生成或接收 SN，执行前处理；前处理拒绝时记录失败，不继续启动引擎 |

不含 `CVBaseServerNode` 的图不会被上述注册中心/token 检查拦截，但这不保证图中其它节点没有自己的文件、设备或环境依赖。

## 执行一次并确认终态

1. 确认当前模板、起始节点和节点绑定；使用已获授权的测试输入及可追踪 SN，不混入正式生产批次。
2. 从执行入口启动，记录开始时间、当前节点、进度和日志。进度与耗时只帮助定位阶段，不证明业务完成。
3. 共享链等待 `RunFinalized`，或调用 `RunFlowAndWaitForFinalizationAsync()` 获取最终结果，再检查最终业务状态与摘要。
4. 用同一 SN/批次和时间核对日志、结果图、数据库、导出文件或外部响应；只有当前流程实际负责的输出才属于本轮完成条件。
5. 需要中止且已获授权时调用停止入口。`StopFlow()` 请求取消活动生命周期并取消 token；无活动生命周期但引擎仍运行时才直接调用 `FlowControl.Stop()`。随后确认实际终态和设备状态，不能把点击停止当作硬件已立即停止的证明。

## 完成判据

`FlowControl.FlowCompleted` / `EngineExecutionCompleted` 表示节点图结束，不表示后处理完成；`FlowExecutionCompleted` 是过时的引擎完成兼容别名，不增加业务最终化语义。`FlowRunFinalizer` 在后处理后解析最终业务结果；必需后处理失败会使最终结果失败，警告级后处理失败可以保留业务成功并附带警告。

`ViewFlow`、`FlowJob` 和共享自动化应以最终化结果为准。部分现有 `Projects/*` 窗口仍直接监听 `FlowCompleted`，再执行项目自己的最终化或 `Processing`；它们的项目结果、导出及协议完成判据不能直接替换成共享链事件。

| 调用入口 | 返回边界 |
| --- | --- |
| `FlowEngineManager.RunFlowAsync()` | 兼容的引擎执行结果，不承诺后处理成功 |
| `RunFlowAndWaitForFinalizationAsync()` | `FlowRunFinalizedData`，应读取 `FinalOutcome` 与后处理结果 |
| `FlowJob.Execute()` | 经 `FlowExecutionCoordinator` 回到 WPF Dispatcher，执行单例 `FlowEngineManager.View` 主工作区选中的流程，等待最终化后写入 `context.Result`；不跟随任意独立窗口的激活状态 |

`FlowJob` 仅把 `Succeeded` / `SucceededWithWarnings` 映射为成功，并把失败后处理摘要加入任务消息。它与读取已保存快照的 `HeadlessFlowJob` 是两条入口，不可因同属 Quartz 就混用输入和完成判据。

## 复现与关联证据

| 记录项 | 需要保留的上下文 |
| --- | --- |
| 流程模板 | 名称、版本、导入来源、最后修改人，避免复测旧版本 |
| 起始条件 | 起始节点、SN/批次输入、项目窗口或外部触发方式 |
| 设备依赖 | 相机、电机、SMU、文件服务等节点绑定与设备 Code |
| 模板依赖 | 图像处理模板、校准模板、判定阈值和输入图片来源 |
| 数据去向 | 当前链实际写入的数据库表、图片目录、导出文件、Socket/MES 响应 |
| 失败证据 | 第一个失败或未推进的节点、日志时间点、错误信息、最终状态与摘要 |

## 按失败阶段定位

先区分启动前拒绝、执行中失败和最终化失败；已经启动时以第一个失败或未推进的节点为起点。

| 阶段 | 典型表现 | 第一检查点 |
| --- | --- | --- |
| 执行前 | 按运行未启动、提示刷新服务、起始节点缺失 | 模板快照、起始节点；有服务节点时再查注册中心/token |
| 前处理 | 立即返回并报告前处理失败 | 输入参数、模板合法性、项目窗口上下文 |
| 设备节点 | 等待超时、无响应、返回码异常 | 设备 Code、连接、MQTT/串口/IP 与对应设备日志 |
| 模板节点 | 节点结束但数值不对 | 模板版本、阈值、图片来源和校准数据 |
| 最终化/数据 | 引擎结束但最终失败或看不到结果 | 后处理策略与异常、最终业务状态、批次/SN、数据库与文件权限 |
| 外部系统 | 本地完成但 MES/Socket 没收到 | 项目 handler、协议与端口、响应字段及项目自己的完成判据 |

## Incident 与诊断持久化

`FlowExecutionJournalCoordinator` 关联 Run/Event/Attempt，诊断存储失败不应改变业务结果；最终化返回也不保证 journal 终态已写入。进程中断恢复只标记遗留记录失败，不续跑节点，且不能仅凭心跳过旧判死。快照、重试、owner 判定与 Incident 处置统一见[Flow 运行诊断](../../04-api-reference/engine-components/flow-diagnostics.md)。

打开默认自动加载的 Incident 窗口可能初始化/迁移本地 schema，不是严格零写入；确认/关闭另有明确的写入授权边界，也不改变流程成功失败。普通日志检索仍见[日志主题](../interface/log-viewer.md)。

## 源码与验证边界

主入口为 `Engine/ColorVision.Engine/FlowProcessing/Runtime/DisplayFlow.xaml.cs`。同目录 `ViewFlow.xaml.cs` / `FlowExecutionSession.cs` 负责交互会话，`FlowRunExecutor.cs` 负责运行等待，`FlowRunFinalizer.cs` 负责后处理与最终业务状态；无界面入口为 `FlowExecutionCoordinator.cs` 和 `FlowHeadlessExecutionService.cs`。

`FlowFinalizedExecutionApiTests.cs` 覆盖兼容/最终化 API 区别以及 `FlowJob` 读取最终结果；`FlowRunFinalizerTests.cs` 覆盖必需/警告级后处理失败，顺序用例核对后处理先于 legacy fallback 终态持久化。诊断协调、事件处置、旧库迁移与进程恢复的测试及未覆盖分支见[诊断验证边界](../../04-api-reference/engine-components/flow-diagnostics.md#验证入口与缺口)。

这些测试不证明真实设备已停止、注册中心可用或项目外部系统收到结果；环境行为须在获授权的对应环境补验。需要验证整轮时，在最终化后关联批次状态、耗时、节点尝试、Incident 和后处理结果；当前没有据此承诺单步或断点调试能力。

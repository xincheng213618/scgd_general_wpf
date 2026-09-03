---
knowledge_id: "flow.session"
knowledge_type: "topic"
status: "current"
summary: "流程启动、分阶段停止与后处理完成判据；区分当前画布、诊断快照、执行耗时和结果落库。"
aliases: ["流程运行","流程没结束","执行流程","停止流程","执行耗时","流程后处理","RunFinalized","执行调试","StopFlow","CVBaseServerNode","FlowExecutionSession","FlowJob"]
code_paths: ["Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowExecutionSession.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowRunExecutor.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowRunFinalizer.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowControl.cs","Engine/ColorVision.Engine/FlowProcessing/PostProcess/PostProcessExecution.cs","Engine/ColorVision.Engine/FlowProcessing/Scheduling/FlowJob.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FlowFinalizedExecutionApiTests.cs","Test/ColorVision.UI.Tests/FlowRunFinalizerTests.cs"]
related: ["flow.architecture","flow.templates","flow.workspace","flow.headless","flow.diagnostics"]
---

# Flow 启动、停止与最终化

从“工作流程”面板或“流程编辑器”点击“执行流程”，运行对应工作区的节点图。本页说明启动前提、停止行为，以及如何区分引擎结束、后处理完成和结果落库。共享业务链由 `FlowExecutionSession` 编排；项目窗口直接持有 `FlowControl` 的入口可能使用自己的完成规则。

编辑与保存见[工作区](./design.md)，数据库/包格式见[模板持久化](../../04-api-reference/engine-components/template-flow-chain.md)。不需要 UI 批次和前后处理的裸图运行见[无界面执行](../../04-api-reference/algorithms/templates/flow-engine.md)。

## 启动前提

| 检查项 | 当前行为 |
| --- | --- |
| 已加载有效模板，且没有正在切换的请求 | 等待工作区加载完成并取得模板身份快照；无法取得或身份已过期时不启动 |
| 当前图包含服务节点 `CVBaseServerNode` | 要求注册中心已连接且 `ServiceTokens` 非空；缺 token 时请求刷新并返回，需就绪后再次执行 |
| 有有效起始节点 | 刷新起始节点选择；无效或为空时提示并返回 |
| 当前会话没有活动运行 | 生命周期门禁拒绝并发启动，范围包含启动准备和收尾 |
| 业务批次可以创建 | 在启动引擎前向 MySQL 写入 `MeasureBatchModel`；失败会中断启动，不受诊断 SQLite 的容错保护 |
| 前处理通过，起始节点就绪 | 前处理拒绝时不启动引擎；随后最多等待5秒的起始节点准备，再尝试启动 |

不含服务节点的图可以跳过注册中心/token 检查，但共享会话仍创建业务批次；“没有设备节点”不等于“不依赖数据库”。节点还可能有自己的文件、设备或环境要求。5秒是启动准备等待，不是整图执行上限；共享会话没有向 `FlowRunExecutor` 设置整图超时，节点自身仍可报告超时。

## 执行一次并确认终态

1. 确认所在工作区、模板、起始节点和节点绑定。运行会创建业务批次，也可能控制设备或写外部系统，使用已获授权的输入和目标。
2. 需要按保存版本复现时，先确认保存成功。UI 手动运行可使用未保存的画布；`FlowJob` 使用主工作区，不跟随任意独立窗口的激活状态。完整输入差异见[当前画布与已保存版本](./design.md#当前画布与已保存版本)。
3. 点击“执行流程”，记录实际生成的 SN、批次和开始时间。进度及当前节点用于定位阶段，不作为成功凭据。
4. 等待共享链最终化，再核对最终状态、后处理结果和本流程负责的输出。API 调用方使用 `RunFlowAndWaitForFinalizationAsync()`；事件订阅方使用 `RunFinalized`。
5. 需要中止时点击“停止流程”，按下节确认后续状态；不要仅凭取消提示判断设备停稳或数据写入已停止。

## 停止后会发生什么

`StopFlow()` 请求取消活动生命周期的 token，并更新界面摘要。取消在不同阶段产生不同结果：

| 请求停止时的阶段 | 后续行为 |
| --- | --- |
| 启动准备或前处理 | 后续启动检查发现取消后不启动节点图。前处理接口没有接收该 token，已进入的前处理不会因此被强制打断；已创建的批次会尝试写入取消状态 |
| 正在等待节点图结束 | `FlowRunExecutor` 调用 `FlowControl.Stop()` 并返回取消结果，然后进入收尾。停止调用不是等待硬件停稳的接口 |
| 引擎已结束，正在收尾或后处理 | 后处理接口没有接收停止 token，仍等待已进入的处理返回。最终状态由引擎结果和后处理策略计算，不会只因点击停止就改成 `Canceled` |

引擎已启动且有批次时，后处理也会在图失败、取消或超时后执行，并非仅成功后执行。各处理器应根据批次状态决定自己的行为。尚未启动图的前处理拒绝/取消不走这条后处理链。

没有活动生命周期、但底层 `FlowControl.IsFlowRun` 仍为真时，停止入口才直接调用底层停止。刷新工作区也会先取消并等待收尾，不能用刷新绕过仍未返回的前后处理。

## 完成判据

| 信号或结果 | 能确认什么 |
| --- | --- |
| `FlowControl.FlowCompleted` | 底层图给出终止结果；结果可能是成功、失败、取消或超时 |
| `EngineExecutionCompleted` | 会话完成引擎阶段的收尾尝试，尚未执行共享后处理。`FlowExecutionCompleted` 是它的过时兼容别名 |
| `RunFinalized` / `FlowRunFinalizedData` | 已等待本链匹配的后处理返回，并计算 `FinalOutcome`；仍需分别核对持久化与项目外部输出 |

引擎成功时，必需后处理失败使 `FinalOutcome` 为 `Failed`；仅警告级失败为 `SucceededWithWarnings`；没有失败则为 `Succeeded`。引擎本身失败、取消或超时时，后处理不会将其提升为成功。后处理调度整体抛错会作为一项警告结果返回，需查看 `PostProcessResults`，不能只读取引擎状态。

共享默认后处理按流程名称匹配配置，不区分大小写，依次调用处理器；单项返回失败或抛错会被记录，并继续后续项。完成只覆盖这些处理器返回的工作，不保证它们自行启动的后台任务或外部交付已经结束。

| 调用入口 | 返回边界 |
| --- | --- |
| `FlowEngineManager.RunFlowAsync()` | 同样等待本会话执行链返回，但仅给出 `FlowControlData` 引擎结果；不能由此判定后处理成功 |
| `RunFlowAndWaitForFinalizationAsync()` | 返回最终结果，应读取 `FinalOutcome` 与 `PostProcessResults`；未进入最终化时可返回 `null`，启动异常也可能抛出 |
| `FlowJob.Execute()` | 经协调器回到 WPF Dispatcher，运行单例 `FlowEngineManager.View` 主工作区并等待最终化，再写入 `context.Result` |

`FlowJob` 仅将 `Succeeded` / `SucceededWithWarnings` 映射为成功，并将失败后处理摘要加入消息。它与读取已保存快照的 `HeadlessFlowJob` 是两条入口。部分 `Projects/*` 窗口直接监听 `FlowCompleted` 后执行自己的 `Processing`、导出或协议响应，须继续核对所属项目的完成规则。

## 耗时与落库状态

| 字段或现象 | 判读方式 |
| --- | --- |
| `FlowControlData.TotalTime` / `FlowJobResult.TotalTime` | 正常图终止时来自引擎结果；执行器合成的取消结果没有填充此值，不能把0解释为没有运行 |
| 批次 `TotalTime` / journal `ElapsedMs` | 会话计时从创建批次前开始，到收到执行器结果、进入最终化时停止；包含前处理及启动等待，不包含后处理和随后诊断收尾的耗时 |
| 后处理耗时 | 查看后处理日志、事件中的 `elapsedMs`，或各 `PostProcessExecutionResult` 的开始/结束时间；不能直接由上述总耗时推算 |
| 最终结果已返回，但批次仍是旧状态 | 会话捕获并记录 MySQL 批次完成更新失败；必需后处理失败后的批次更新也可能失败 |
| 最终结果已返回，但本地诊断未完成 | 节点记录等待是有界的，journal 终态写入也可能失败；继续查存储错误和对应记录 |

会话的节点结束等待上限为1秒，旧节点写队列 flush 等待为5秒；超时记录警告并继续，不把诊断阻塞变成无限等待。上述信号没有构成 MySQL、SQLite、文件和外部系统之间的共同事务。

## 复现与关联证据

| 记录项 | 需要保留的上下文 |
| --- | --- |
| 流程模板 | 名称、身份/版本、导入来源，以及本轮是否使用了未保存修改。诊断快照不保证等于运行画布，见[快照来源](../../04-api-reference/engine-components/flow-diagnostics.md#快照是否对应本次画布) |
| 起始条件 | 起始节点、SN/批次、项目窗口或外部触发方式 |
| 设备依赖 | 相机、电机、SMU、文件服务等节点绑定与设备 Code |
| 模板依赖 | 图像处理模板、校准模板、判定阈值和输入图片来源 |
| 数据去向 | 当前链实际写入的数据库表、图片目录、导出文件、Socket/MES 响应 |
| 失败证据 | 第一个失败或未推进的节点、日志时间点、错误信息、最终状态与后处理摘要 |

## 按失败阶段定位

| 阶段 | 典型表现 | 第一检查点 |
| --- | --- | --- |
| 执行前 | 按运行未启动、提示刷新服务、起始节点缺失 | 工作区加载、起始节点；有服务节点时再查注册中心/token |
| 批次/前处理 | 立即报错或报告前处理失败 | MySQL 批次创建、输入参数和前处理日志 |
| 启动准备 | 提示“流程 MQTT 连接尚未就绪，本次未启动” | 查看“流程启动准备”日志中的就绪结果；底层拒绝也可能来自起始节点缺失、忙碌或未就绪，不能仅凭此提示认定为 MQTT 连接故障 |
| 设备节点 | 等待超时、无响应、返回码异常 | 设备 Code、连接、MQTT/串口/IP 与对应设备日志 |
| 模板节点 | 节点结束但数值不对 | 模板版本、阈值、图片来源和校准数据 |
| 最终化/数据 | 引擎结束但最终失败或看不到结果 | 后处理策略与异常、批次/SN、数据库与文件写入结果 |
| 外部系统 | 本地完成但 MES/Socket 没收到 | 所属项目的处理器、协议、响应字段与完成判据 |

## Incident 与诊断持久化

`FlowExecutionJournalCoordinator` 关联 Run/Event/Attempt。记录失败降级、终态重试、进程中断恢复和 Incident 处置统一见[Flow 运行诊断](../../04-api-reference/engine-components/flow-diagnostics.md)。中断恢复仅标记遗留记录失败，不续跑节点。

打开自动加载的 Incident 窗口可能初始化或迁移本地库；确认/关闭会写入处置状态，需要相应授权，也不改变业务成功失败。普通日志检索见[日志主题](../interface/log-viewer.md)。

## 源码与验证边界

`DisplayFlow.xaml` 的执行/停止命令绑定到 `ViewFlow`。同目录 `FlowExecutionSession.cs` 负责启动与收尾，`FlowRunExecutor.cs` 负责等待和取消，`FlowControl.cs` 连接底层引擎；`FlowRunFinalizer.cs` 和 `PostProcess/PostProcessExecution.cs` 负责后处理及最终状态。

`FlowFinalizedExecutionApiTests.cs` 核对事件/API 类型与 `FlowJob` 结果映射，不是实际会话的时序测试。`FlowRunFinalizerTests.cs` 用替身核对后处理策略、调度异常和后处理先于 legacy fallback 持久化；不验证真实 MySQL/SQLite 写入。停止阶段、耗时范围及当前画布与诊断快照差异依据上述源码链，现有这两组测试没有覆盖完整组合。

诊断相关测试及缺口见[诊断验证边界](../../04-api-reference/engine-components/flow-diagnostics.md#验证入口与缺口)。真实设备停止、注册中心连接和项目外部输出仍需在获授权的环境验证；当前没有单步或断点调试能力的承诺。

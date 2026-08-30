---
knowledge_id: "flow.headless"
knowledge_type: "topic"
status: "current"
summary: "隔离 STN 无界面执行的不可变请求、终止结果与 HeadlessFlowJob 调度边界，不自动运行批次和前后处理。"
aliases: ["无界面运行流程","独立调度","FlowHeadlessExecutionRequest","FlowHeadlessExecutionService","RunSavedFlowHeadlessAsync","HeadlessFlowJob","FlowRuntimeHost"]
code_paths: ["Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowHeadlessExecutionService.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowExecutionCoordinator.cs","Engine/ColorVision.Engine/FlowProcessing/Scheduling/HeadlessFlowJob.cs","Engine/FlowEngineLib/Runtime/FlowRuntimeHost.cs","Engine/FlowEngineLib/Runtime/FlowEngineRunner.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FlowHeadlessExecutionServiceTests.cs","Test/ColorVision.UI.Tests/FlowRuntimeHostTests.cs","Test/ColorVision.UI.Tests/FlowHeadlessHostTests.cs"]
related: ["flow.architecture","flow.runtime","flow.templates","flow.session","ui.scheduler"]
---

# Flow 隔离无界面执行

`FlowHeadlessExecutionService` 执行脱离编辑器的 STN 快照，每次请求独占 `FlowRuntimeHost`。它不复用当前 `ViewFlow` / `FlowControl`，不自动创建 UI 批次，也不执行共享前后处理。需要业务最终化的调用方应选用[共享执行会话](../../../01-user-guide/workflow/execution.md)，或明确编排自己的批次与业务阶段。

无界面不等于无副作用：节点仍可能控制硬件、写文件或调用远程服务。创建说明或检索文档不授权实际运行；调用前确认 STN、起始节点、SN、服务快照、超时和可影响的设备/数据范围。

## 请求与隔离

`FlowHeadlessExecutionRequest` 接收非空 STN、起始节点、SN、可选 MQTT 服务列表以及 readiness / execution timeout。构造时复制 STN、服务 token 和设备信息，计算 `ContentHash`；读取请求属性或创建宿主输入时再次复制，后续编辑和调用方修改不得改变正在执行的请求。

readiness timeout 必须为正值且不接受无限等待；execution timeout 可为正值或 `Timeout.InfiniteTimeSpan`。传入非法参数会在请求构造时抛出异常，不会进入结构化运行结果。

`RunAsync` 每次创建并最终释放一个 `FlowRuntimeHost`，加载后由 `FlowEngineRunner` 等待运行结束；并发请求不共享可变节点实例。若图中有 `CVBaseServerNode` 但未提供任何服务快照，明确返回 `StartRejected`。提供了非空快照也不证明每个设备在线或绑定成功。

## 三种入口不能混用

| 入口 | 读取什么 | 完成含义 |
| --- | --- | --- |
| `RunHeadlessAsync(request)` | 调用方提供的不可变 STN/服务请求 | 本次隔离节点图的终止结果 |
| `RunSavedFlowHeadlessAsync(flowKey, ...)` | 在 `TemplateFlow.Params` 按 `FlowKey` 找到的 `DataBase64`，随后复制到请求 | 与裸执行相同，不自动增加前后处理 |
| `RunSelectedFlowAndWaitForFinalizationAsync()` | 单例 `FlowEngineManager.View` 的主工作区，不是任意激活的独立窗口 | 共享会话的最终业务结果，详见[执行会话](../../../01-user-guide/workflow/execution.md) |

“读取已保存流程”入口当前读取模板集合中的内容，不是每次直接向 MySQL 重新查询；也不读取当前尚未保存的编辑器画布。需要最新持久化内容时，先确认模板集合已经加载/刷新。创建请求后的字节副本不受后续编辑影响。

裸 `FlowHeadlessExecutionService` 不访问 WPF Application/Dispatcher/Window。按 `FlowKey` 取模板的协调器在 WPF 应用存在时可能通过 Dispatcher 读取模板集合，这与“执行节点时不触碰编辑器”并不矛盾。

## 结果与失败语义

`FlowHeadlessExecutionResult` 返回 `Started`、`Termination`、`ContentHash`、`Data` 和总耗时。`Succeeded` 只有在已经启动、终止类型为 `Completed` 且引擎状态也为 `Completed` 时才为真。

| `Termination` | 含义 |
| --- | --- |
| `Completed` | 运行器完成；仍须检查 `Data.Status`，不能只读枚举名判成功 |
| `StartRejected` | 门禁、服务快照或起始节点等条件未满足 |
| `Canceled` | 取消请求导致终止 |
| `TimedOut` | 运行超时 |
| `LoadFailed` | 加载 STN/运行图失败 |
| `Faulted` | 已进入加载后的运行阶段发生异常 |

`ToFlowControlData()` 保留起始节点、SN、状态、时间、消息和错误节点映射，不会凭空创建 `RunFinalized` 或客户报表。调用边界也有会直接抛出的错误，例如空请求、非法请求参数，以及已保存流程不存在/非合法 Base64；不要假定所有失败都以 `Termination` 返回。

`FlowHeadlessExecutionObserver` 只附着于本次隔离图，用于节点 Run/End 诊断；结束时解除订阅，不作为全局编辑器事件注册。

## Quartz 独立调度

`HeadlessFlowJob` 使用 `RunSavedFlowHeadlessAsync`，带 `DisallowConcurrentExecution`，限制同一 JobDetail 并发；不同作业与裸请求仍须自行考虑硬件共享。它将结果转换为 `FlowJobResult` 写入 `context.Result`，启动边界异常记录为 `HeadlessStartupException`。

| JobDataMap 字段 | 约束 |
| --- | --- |
| `FlowKey`、`StartNode` | 必填 |
| `SerialNumber` | 可选；缺省时由作业名与 UTC 时间生成 |
| `ReadinessTimeoutMs`、`ExecutionTimeoutMs` | 可选，设置时必须为正整数毫秒 |

这是 `HeadlessFlowJob` 的字段契约，不能套用到 `FlowJob`。后者通过 WPF Dispatcher 运行单例 `FlowEngineManager.View` 主工作区选中的流程，不跟随任意独立窗口的激活状态；它包含批次/前后处理并等待最终化结果，并非运行已保存快照的同义入口。

## 验证入口与缺口

`FlowHeadlessExecutionServiceTests` 覆盖请求副本、并发隔离、取消结果、无效 STN、观察器和无服务快照拒绝；`FlowRuntimeHostTests` / `FlowHeadlessHostTests` 覆盖非可视宿主及生命周期。裸执行测试不能证明真实 MQTT/设备可用、Quartz 配置完整或项目业务输出成功。

对应改动应核对两次并发执行不共享图、请求创建后修改原输入无效、取消/超时/加载失败/启动拒绝有明确结果。若还要求批次、后处理、MES 或客户结果，必须另外验证负责这些阶段的宿主，不能将裸图完成当作业务交付。

---
knowledge_id: "flow.session"
knowledge_type: "topic"
status: "current"
summary: "从工作流程面板或流程编辑器开始执行；流程停止请求与结果返回按启动准备、引擎和前后处理收尾分阶段判断，区分当前画布、诊断快照、执行耗时和结果落库；停止请求不保证设备停稳。"
aliases: ["工作流程","流程编辑器","流程启动","流程运行","流程卡住","流程没结束","执行流程","停止流程","已经取消执行","执行耗时","流程后处理","RunFlowCommand","StopFlowCommand","RunFinalized","执行调试","StopFlow","CVBaseServerNode","FlowExecutionSession","FlowJob"]
code_paths: ["Engine/ColorVision.Engine/FlowProcessing/Runtime/DisplayFlow.xaml","Engine/ColorVision.Engine/FlowProcessing/Runtime/ViewFlow.xaml","Engine/ColorVision.Engine/FlowProcessing/Runtime/ViewFlow.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowExecutionSession.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowRunExecutor.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowRunFinalizer.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowControl.cs","Engine/ColorVision.Engine/FlowProcessing/PostProcess/PostProcessExecution.cs","Engine/ColorVision.Engine/FlowProcessing/Scheduling/FlowJob.cs"]
test_paths: ["Test/ColorVision.UI.Tests/OfflineCameraFlowTests.cs","Test/ColorVision.UI.Tests/FlowFinalizedExecutionApiTests.cs","Test/ColorVision.UI.Tests/FlowRunFinalizerTests.cs"]
related: ["flow.architecture","flow.templates","flow.workspace","flow.headless","flow.diagnostics"]
---

# 流程执行：启动、停止与最终化

从“工作流程”面板或“流程编辑器”点击“执行流程”，运行对应工作区的节点图。本页说明启动前提、停止行为，以及如何区分引擎结束、后处理完成和结果落库。共享业务链由 `FlowExecutionSession` 编排；项目窗口直接持有 `FlowControl` 的入口可能使用自己的完成规则。

`ViewFlow.NodeExecutionCompleted` 在会话接受当前运行的一次节点结束通知后发布，携带批次 ID、SN、节点身份和状态码；旧 SN、未匹配的节点尝试及重复结束通知不会发布。它用于 IV/IVL 等执行中结果观察，不表示整图或后处理完成，也不保证诊断写入已经结束。订阅者须快速返回，将数据库查询和绘图转交后台与 Dispatcher；订阅者异常会被隔离记录，不中断流程节点收尾。

编辑与保存见[工作区](./design.md)，数据库/包格式见[模板持久化](../../04-api-reference/engine-components/template-flow-chain.md)。不需要 UI 批次和前后处理的裸图运行见[无界面执行](../../04-api-reference/algorithms/templates/flow-engine.md)。

流程编辑器右上角的“预处理”打开动作配置；点击“添加”后，选择带名称和用途说明的动作卡片，再点击“添加”，即可在右侧编辑参数。所有已启用动作在每次流程启动前按列表顺序执行，修改自动保存。旧配置的 `TemplateNames` 字段会被忽略，避免界面隐藏的旧限制影响执行。首次创建预处理配置时，默认启用一条“缓存大小清理”，超过 100 GiB 后清理到 50 GiB；已有配置中的启用状态、目录和容量设置继续保留，已删除的动作不会自动补回。清理会永久删除配置目录内匹配的旧文件，使用前应确认缓存目录；具体范围与文件保护边界见[缓存清理](../../04-api-reference/extensions/flow-node.md#节点目录与右键菜单)。

## 启动前提

| 检查项 | 当前行为 |
| --- | --- |
| 已加载有效模板，且没有正在切换的请求 | 等待工作区加载完成并取得模板身份快照；指定模板时只接受本次选择成功返回的快照，选择失败或身份已过期时不启动 |
| 当前图包含服务节点 `CVBaseServerNode` | 要求注册中心已连接且 `ServiceTokens` 非空；缺 token 时请求刷新并返回，需就绪后再次执行 |
| 有有效起始节点 | 刷新起始节点选择；无效或为空时提示并返回 |
| 当前会话没有待启动请求或活动运行 | 运行请求预约拒绝并发请求，范围从模板选择开始，覆盖启动准备、前后处理和最终收尾 |
| 业务批次可以创建 | MySQL 已连接时，在启动引擎前写入 `MeasureBatchModel`；未连接时使用仅驻留内存的批次，不向配置库保存结果 |
| 前处理通过，起始节点就绪 | 前处理拒绝时不启动引擎；随后最多等待5秒的起始节点准备，再尝试启动 |

不含服务节点的图可以跳过注册中心/token 检查；现有 MQTT/MQTT V5 开始节点即使未连接也可承担本地分发。包含服务节点时仍要求真实 MQTT 就绪。节点还可能有自己的文件、设备或环境要求。5秒是启动准备等待，不是整图执行上限；共享会话没有向 `FlowRunExecutor` 设置整图超时，节点自身仍可报告超时。

运行请求采用非等待式预约：从选择指定模板或等待当前模板快照开始，一直占用到最终收尾结束。期间的重复请求立即拒绝，返回结果的 API 返回 `null`，按钮入口不启动，也不排队。即使请求指定了另一个模板，也不会停止当前流程、替换画布或改写原执行摘要。选择失败、快照过期或启动异常时同样释放预约，后续请求可重新尝试。

进入起点启动阶段后，启动被拒绝的执行摘要会带上所选起点名称。已知存在并发运行时提示等待完成或停止后重试；其他拒绝提示检查起点后续连线、流程占用和设备/服务连接，不能仅凭启动失败认定 MQTT 断线。

会话接受启动后，当前画布立即进入只读状态，覆盖启动准备、前处理、节点运行、后处理和最后收尾。期间不能修改节点参数或连线、删除、新建、导入、撤销/重做；仍可平移、缩放、选中节点查看参数、说明和执行详情，手动分支节点的 A/B 运行选择保留。全部收尾结束后恢复原先的编辑权限，原本只读的画布不会因此解锁。

## 无 MySQL/MQTT 的本地取图

本地保存的流程可以沿用 MQTT 开始节点，连接“相机取图”（`LocalCameraNode`）和结束节点。准备好本地设备配置、相机驱动及有效 SDK 许可证后，无需运行服务即可在本机取图。也可使用本地图片节点验证图像输入链。

未连接 MySQL 时，一次运行在启动时固定 `PersistResults=false`：相机及本地图片节点保留内存帧，供当前流程后续节点使用；相机仍可预览，节点启用保存文件时仍按原有目录保存图像。它们不会创建 MySQL 图像结果主表，也不会把结果写进 `ColorVision.Local.db`。现有诊断记录使用独立的诊断库，不能作为图像结果历史查询。

此边界覆盖本地相机取图和本地图片输入，不代表所有带“本地”名称的算法已经脱离结果数据库。依赖 MySQL 模板/结果明细、校准数据、远端服务的节点，以及用户配置的前后处理，仍须满足各自依赖；不能直接把完整服务流程视为离线流程。真实 SDK、许可证和硬件采集需单独验收。

## 执行一次并确认终态

1. 确认所在工作区、模板、起始节点和节点绑定。运行会创建业务批次，也可能控制设备或写外部系统，使用已获授权的输入和目标。
2. 需要按保存版本复现时，先确认保存成功。UI 手动运行和未指定模板的运行接口可使用未保存的画布；程序化指定模板时，若当前画布有未保存修改则拒绝切换，不弹保存对话框，也不继续运行旧图。`FlowJob` 使用主工作区，不跟随任意独立窗口的激活状态。完整输入差异见[当前画布与已保存版本](./design.md#当前画布与已保存版本)。
3. 点击“执行流程”，记录实际生成的 SN、批次和开始时间。进度及当前节点用于定位阶段，不作为成功凭据。
4. 等待共享链最终化，再核对最终状态、后处理结果和本流程负责的输出。API 调用方使用 `RunFlowAndWaitForFinalizationAsync()`；事件订阅方使用 `RunFinalized`。
5. 执行过程中需要中止时点击“停止流程”，按下节确认后续状态；不要仅凭停止提示判断设备停稳或数据写入已停止。

## 停止后会发生什么

`StopFlow()` 请求取消活动生命周期的 token。取消在不同阶段产生不同结果：

会话接受启动后，“执行流程”切换为“停止流程”，覆盖启动准备、前处理、节点运行、后处理和收尾；这段时间不能再次启动。点击停止后按钮暂时禁用，并显示“正在停止，等待当前环节结束…”，直到整个会话退出才恢复执行按钮。进度条在启动准备、后处理和停止等待期间显示活动状态，不把节点图完成的 100% 当作所有工作已经结束。

| 请求停止时的阶段 | 后续行为 |
| --- | --- |
| 启动准备或前处理 | 后续启动检查发现取消后不启动节点图。前处理接口没有接收该 token，已进入的前处理不会因此被强制打断；已创建的批次会尝试写入取消状态 |
| 正在等待节点图结束 | `FlowRunExecutor` 调用 `FlowControl.Stop()` 并返回取消结果，然后进入收尾。停止调用不是等待硬件停稳的接口 |
| 引擎已结束，正在收尾或后处理 | 后处理接口没有接收停止 token，仍等待已进入的处理返回。最终状态由引擎结果和后处理策略计算，不会只因点击停止就改成 `Canceled` |

引擎已启动且有批次时，后处理也会在图失败、取消或超时后执行，并非仅成功后执行。各处理器应根据批次状态决定自己的行为。尚未启动图的前处理拒绝/取消不走这条后处理链。

没有活动生命周期、但底层 `FlowControl.IsFlowRun` 仍为真时，停止入口才直接调用底层停止。刷新工作区也会先取消并等待收尾，不能用刷新绕过仍未返回的前后处理。

停止只发出取消请求，不立即解除画布只读；前后处理或收尾仍未返回时，继续等待其结束后才能编辑。停止等待提示独立于执行摘要：后处理期间请求停止保留已有引擎结果与失败节点详情，不将已经完成的引擎结果改写为“已取消”；启动前已取消的会话在退出时才显示取消结果。空闲时调用停止不清除上次执行摘要。

## 完成判据

| 信号或结果 | 能确认什么 |
| --- | --- |
| `FlowControl.FlowCompleted` | 底层图给出终止结果；结果可能是成功、失败、取消或超时 |
| `EngineExecutionCompleted` | 会话完成引擎阶段的收尾尝试，尚未执行共享后处理。`FlowExecutionCompleted` 是它的过时兼容别名 |
| `RunFinalized` / `FlowRunFinalizedData` | 已等待本链匹配的后处理返回，并计算 `FinalOutcome`；仍需分别核对持久化与项目外部输出 |

引擎成功时，必需后处理失败使 `FinalOutcome` 为 `Failed`；仅警告级失败为 `SucceededWithWarnings`；没有失败则为 `Succeeded`。引擎本身失败、取消或超时时，后处理不会将其提升为成功。后处理调度整体抛错会作为一项警告结果返回，需查看 `PostProcessResults`，不能只读取引擎状态。

共享默认后处理按流程名称匹配配置，不区分大小写，依次调用处理器；单项返回失败或抛错会被记录，并继续后续项。完成只覆盖这些处理器返回的工作，不保证它们自行启动的后台任务或外部交付已经结束。

新增后处理项时，名称默认填入当前选中的流程模板名，切换流程时自动跟随。手动修改名称后，切换流程保留自定义名称；清空名称后再次选择流程可恢复自动填入。编辑已有项时保留原名称。

新增或编辑后处理项时，IVL 和数据导出类型在处理配置下方显示 **CSV 保存目录**，可复制完整路径或点击 **打开目录**。该目录读取共享后处理配置，默认是当前用户的桌面 `Batch` 文件夹，可在外层“后处理配置”窗口工具栏的齿轮中修改。目录尚未生成时，打开入口提示等待导出创建；保存后处理项只保存配置，CSV 在实际后处理执行时写入。

| 调用入口 | 返回边界 |
| --- | --- |
| `FlowEngineManager.RunFlowAsync()` | 同样等待本会话执行链返回，但仅给出 `FlowControlData` 引擎结果；不能由此判定后处理成功 |
| `RunFlowAndWaitForFinalizationAsync()` | 返回最终结果，应读取 `FinalOutcome` 与 `PostProcessResults`；未进入最终化时可返回 `null`，启动异常也可能抛出 |
| `FlowJob.Execute()` | 经协调器回到 WPF Dispatcher，运行单例 `FlowEngineManager.View` 主工作区并等待最终化，再写入 `context.Result` |

`FlowJob` 仅将 `Succeeded` / `SucceededWithWarnings` 映射为成功，并将失败后处理摘要加入消息。它与读取已保存快照的 `HeadlessFlowJob` 是两条入口。部分 `Projects/*` 窗口直接监听 `FlowCompleted` 后执行自己的 `Processing`、导出或协议响应，须继续核对所属项目的完成规则。

## 耗时与落库状态

“流程结果管理”读取 MySQL 批次历史。普通查询按设置中的批次序号顺序和查询数量读取，输入完整批次编号后按 Enter 或点击“搜索”应用条件；“重置”返回未指定批次的列表，高级查询保留独立条件入口。输入过程中不查询数据库，读取失败时保留原列表并显示错误。顶部概览只统计本次查询结果：完成数为 `Completed`，失败/超时数为 `Failed` 与 `OverTime`，平均耗时只取已完成批次；不代表全部历史产量或项目良率。

列表与详情将批次耗时由毫秒换算为秒，时间采用 24 小时制。选中记录后可查看完整结果信息、打开测量结果或流程执行分析；手动后处理位于独立页签，仍需选中批次与处理器后执行。查询设置只展示查询数量和排列顺序，旧配置中的 `AutoRefreshView`、`InsertAtBeginning` 字段继续兼容读取，但不控制历史查询。

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

主工作区和独立流程窗口在失败摘要能关联到当前画布节点时，显示“节点执行详情”。点击后定位该节点并打开执行分析；找不到对应节点时隐藏入口。

执行分析中的节点定位优先使用记录的稳定 `NodeId`：节点已删除或被同名新节点替代时，不会改为定位新节点。缺少 ID 的旧记录才按唯一名称匹配；同名存在歧义时不猜测。定位只改变当前选中节点与视口，不修改流程或加载历史画布。

| 阶段 | 典型表现 | 第一检查点 |
| --- | --- | --- |
| 执行前 | 按运行未启动、提示刷新服务、起始节点缺失 | 工作区加载、起始节点；有服务节点时再查注册中心/token |
| 模板选择 | 提示未保存修改或模板正在切换 | 指定模板的运行请求未取得有效快照；先处理当前画布修改，再等待目标选择完成后重试，不会回退运行旧图或其它模板 |
| 批次/前处理 | 立即报错或报告前处理失败 | MySQL 批次创建、输入参数和前处理日志 |
| 启动准备 | 摘要显示未能从所选起点启动，或已有流程正在运行 | 并发拒绝先等待完成或停止；其他拒绝核对起点、后续连线、流程占用和设备/服务就绪。“流程启动准备”日志的 `ReadinessSucceeded=false` 表示准备未通过，准备成功但 `Started=false` 表示随后启动被拒绝；不能直接归因为 MQTT 断线 |
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

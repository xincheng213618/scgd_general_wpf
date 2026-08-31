---
knowledge_id: "ui.scheduler"
knowledge_type: "topic"
status: "current"
summary: "Quartz 调度定义的启动恢复、JSON/SQLite 分工与执行统计；暂停不终止在途任务，重启恢复不是执行进度续跑。"
aliases: ["定时任务为什么不执行","任务暂停后仍然执行","重启后一次性任务又执行","ColorVision.Scheduler","QuartzSchedulerManager","InitializationTask","scheduler_tasks.json","SchedulerHistory.db","SchedulerInfo","TaskExecutionListener","TimeoutSeconds","DisallowConcurrentExecution"]
code_paths: ["UI/ColorVision.Scheduler/README.md","UI/ColorVision.Scheduler/ColorVision.Scheduler.csproj","UI/ColorVision.Scheduler/QuartzSchedulerManager.cs","UI/ColorVision.Scheduler/MenuTaskViewer.cs","UI/ColorVision.Scheduler/SchedulerStatusBarProvider.cs","UI/ColorVision.Scheduler/SchedulerInfo.cs","UI/ColorVision.Scheduler/SchedulerTriggerFactory.cs","UI/ColorVision.Scheduler/SchedulerTaskSerializer.cs","UI/ColorVision.Scheduler/TaskExecutionListener.cs","UI/ColorVision.Scheduler/Data/SchedulerDbManager.cs","UI/ColorVision.Scheduler/TaskViewerWindow.xaml.cs","UI/ColorVision.Scheduler/CreateTask.xaml.cs","UI/ColorVision.Scheduler/ExecutionHistoryWindow.xaml.cs","UI/ColorVision.UI/Environments.cs","Engine/ColorVision.Engine/Services/Devices/ScheduledDeviceJobHelper.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Job/CameraCaptureJob.cs","Engine/ColorVision.Engine/FlowProcessing/Scheduling/HeadlessFlowJob.cs"]
test_paths: ["Test/ColorVision.UI.Tests/SchedulerTriggerFactoryTests.cs","Test/ColorVision.UI.Tests/SchedulerTaskSerializationTests.cs","Test/ColorVision.UI.Tests/SchedulerHistoryQueryTests.cs","Test/ColorVision.UI.Tests/ScheduledDeviceJobHelperTests.cs"]
related: ["ui.index","ui.configuration","flow.templates","flow.headless"]
---

# Quartz 任务定义、恢复与执行历史

`UI/ColorVision.Scheduler/` 负责把 `SchedulerInfo` 定义注册到 Quartz、保存调度意图并记录已返回的执行结果。真正的设备、流程和客户操作由各 `IJob` 实现负责；任务已登记、Quartz 已触发、业务已完成和历史已落盘是不同完成条件。

首次获取管理器、打开窗口、恢复或立即触发任务都可能启动已有设备任务，不是保证只读的诊断入口。未获运行授权时，只读源码和已有文件；不要为验证文档而调用 `GetInstance()`、打开产品调度窗口或尝试恢复任务。

## 首次访问和恢复顺序

`QuartzSchedulerManager.GetInstance()` 创建进程内单例，构造函数依次执行 `Load()`、`RestoreStatsFromDb()`、注册 Copilot 上下文，然后把 `Start()` 保存为 `InitializationTask`。取得对象不等于异步初始化已完成。

- `Load()` 读取 JSON；统计恢复会取得 `SchedulerDbManager`，其首次构造创建目录并执行 `CodeFirst.InitTables<JobExecutionRecord>()`，即使当前没有任务也可能创建历史数据库。
- `Start()` 取得默认 Quartz scheduler、注册 `TaskExecutionListener`、发现 Job 类型，再逐项调用 `CreateJob` 恢复定义。它先恢复暂停意图，最后才调用 `Scheduler.Start()`，不是“先启动、延迟一段时间再恢复”。
- `TaskViewerInitializer.Order=1000` 的正常宿主入口等待 `InitializationTask`，让低 Order 的服务初始化先执行。但 `SchedulerStatusBarProvider`、`TaskViewerWindow` 和历史窗口的上下文注册也会取得单例，不能据初始化器顺序断言所有首次访问都已等待设备就绪。
- 单个任务类型丢失、定义校验失败或注册失败会汇总警告，其他可恢复任务仍会启动；启动阶段整体异常则记录日志、提示并使初始化任务失败。JSON/SQLite 加载异常有各自的捕获分支，不等于启动被统一阻断。

Job 类型来自当次 `AssemblyService.Instance.GetAssemblies()` 扫描，不来自 SQLite。发现条件是实现 `IJob` 且不是接口；最终创建还要求非抽象类。显示键取 `DisplayNameAttribute` 或类型名，同名会覆盖目录项；当前没有持续监听新程序集重新发现 Job 的机制。

## JSON 定义不是执行检查点

两种文件都位于 `Environments.DirStateScheduler`，即 `DirAppData/State/Scheduler`；默认公司名为 ColorVision 时是 `%AppData%/ColorVision/State/Scheduler`。`DirAppData` 可被宿主设置，不能继续把旧的 `%AppData%/ColorVision` 根目录当成当前固定路径。

| 文件 | 保存和恢复的责任 | 不能据此推断 |
| --- | --- | --- |
| `scheduler_tasks.json` | `SchedulerTaskSerializer` 保存整组 `SchedulerInfo`，含 Job 类型、配置、调度参数、暂停状态及当时的显示/统计字段 | 不是 Quartz 已触发次数、在途执行或完成游标的检查点 |
| `SchedulerHistory.db` | `JobExecutionRecord` 保存每次返回后的开始/结束时间、耗时、成功标记、结果与消息；启动时按 JobName/GroupName 恢复部分统计 | 不是任务定义，也不是设备实际静止或每次执行都已记录的保证 |

JSON 使用 `TypeNameHandling.All` 保留多态类型和 `IJobConfig`，加载依赖对应程序集/类型可解析；没有在此序列化器配置类型白名单，因此只应把它当作可信本机状态，不接受未经核验的外来 JSON。保存先写同目录临时文件，再用 `File.Replace` 保留 `.bak`，首次保存则移动文件；这是单文件替换，不是 JSON 与 Quartz、SQLite 的跨系统事务。`LoadFromFile` 只读取主文件，没有自动加载 `.bak` 的回退，加载失败后不要用空任务列表覆盖证据。

恢复时除了 `Paused`，其他状态都归一为 `Ready`；`SchedulerTriggerFactory.Build` 使用恢复时的当前时间重新建立起点，延迟也重新计算。监听器不会在一次性任务结束时删除 JSON 定义，也不会把已执行次数反馈给恢复触发器。因此保留的一次性定义重启后可能再执行，多次触发进度也不是从上次计数续跑。历史里存在成功记录不会阻止再次调度。

缺少定义版本的旧 `Interval + Forever` 任务会迁移为 `Paused` 并写回版本：旧实现误表现为每日一次，修正后的触发器会按配置间隔重复。重新恢复这类任务可能造成高频设备动作，必须先核对间隔与实际运行授权，不能当成无风险迁移确认。

## 触发器、任务身份和并发

`SchedulerTriggerFactory` 验证 Job 类型、任务名/分组、枚举、优先级、非负超时以及相应的间隔、延迟和 Cron，然后产生稳定的 `JobName-trigger/GroupName`：

- `Simple` 的 Once 只设置首次触发；Multiple 的 `RepeatCount` 是首次之后的追加次数；Forever 按 `Interval` 重复。
- `Interval` 使用 `DailyTimeIntervalScheduleBuilder`，要求整数秒间隔。Multiple 同样传递追加次数，Forever 不设置零重复次数；不要把它当成 Simple 触发器或“每天只执行一次”。
- `Calendar` 当前固定为一个日历日间隔，不能把界面“24 h”理解为跨夏令时始终固定秒数；Cron 直接交给 Quartz。当前构造器不显式设置时区或 misfire 策略，具体时区/错过触发行为不能由任务标题推断。
- `Priority` 传给触发器，不是抢占正在执行的 Job。立即运行直接调用 `Scheduler.TriggerJob(JobKey)`，没有在这个 UI 入口检查 `Running`/`Paused` 或去重；返回只表示触发请求完成，不是业务完成，也不是暂停状态下绝对不可运行的保证。

`_mutationGate` 串行化创建、更新、删除、暂停等管理操作，并不串行化 Job 执行。是否禁止同一任务并发由 Job 自身的 `[DisallowConcurrentExecution]` 等机制决定；该属性以 `JobKey` 为界，不能防止两个不同任务名同时操作同一设备。`CameraCaptureJob`、`SpectrumGetDataJob` 和 `HeadlessFlowJob` 声明了此属性，不能据此扩展成所有 Job 都受保护。

`TaskExecutionListener` 按 `FireInstanceId` 记录在途执行，同一 JobKey 的一个执行结束时，另一个仍在途就保持 `Running`。但 `Paused` 意图优先保留，所以列表显示 Paused 也可能仍有在途操作；不要把 `TaskInfos.Status` 当作独立的设备运行锁。

## 修改完成、回退与界面边界

`CreateTask` 提交新建/编辑时检查 `SchedulerOperationResult.Success`，失败不关闭窗口。编辑路径先克隆定义；`UpdateJob` 使用原任务名/分组定位旧任务，校验新定义和身份冲突，并拒绝更新当前正在执行的原 JobKey。

创建、更新和删除等路径用 `SchedulerStandbyLease` 暂停新的触发分派，记录/替换 Quartz 定义和内存列表后保存 JSON，再按原状态恢复 scheduler。保存失败会尝试移除新 Job 或恢复原定义、触发器和暂停意图；补偿本身可能失败，部分路径仅写日志，不能声称失败返回必然完全回滚。Standby 不等待已有 Job 结束，操作成功也只证明相应调度修改完成，不证明业务任务执行成功。

同身份编辑会在替换前合并最新运行统计，防止编辑期间完成的执行被旧副本覆盖。改任务名或分组则被视为新统计身份，列表计数归零；旧 SQLite 历史仍保留在旧键下。删除任务不删除历史。直接修改 `SchedulerInfo` 属性、调用 `SaveTasks` 或 `LoadTasks` 不能替代 `CreateJob/UpdateJob` 的 Quartz 注册流程。

`IConfigurableJob` 只定义配置类型与默认配置工厂，`CreateTask.RenderConfigurationEditor` 会实例化 Job 并生成属性编辑器；类型被发现不代表构造、配置或运行必然成功。管理器默认只向 JobDataMap 写入 `SchedulerInfo`。要求额外键的 `HeadlessFlowJob` 不能仅因出现在类型列表就视为已配置可运行；它的 FlowKey、StartNode 与独立超时契约见[无界面流程执行](../algorithms/templates/flow-engine.md)，传统 `FlowJob` 的批次/最终完成链见[流程模板与执行](../engine-components/template-flow-chain.md)。

## 暂停、取消、超时和关闭

| 调用或状态 | 实际效果与限制 |
| --- | --- |
| `StopJob` / `PauseAll` | 调用 Quartz PauseJob/PauseAll 并保存暂停意图；没有调用 Interrupt，不终止已开始的 Job |
| `ResumeJob` / `ResumeAll` | 恢复触发并保存 Ready 意图；可能重新开始设备动作，不是单纯改界面状态 |
| `TimeoutSeconds` | 通用模型和触发器只保存/校验该值，没有统一包裹每个 Job 的超时执行器；是否生效取决于具体 Job |
| `Shutdown` | 当前调用无布尔参数的 Quartz `Shutdown()`，等价于 `Shutdown(false)`，不等待在途 Job 完成；同一已关闭 scheduler 不能通过 StartCommand 重启 |
| 关闭任务/历史窗口 | 解除窗口事件与 Copilot 上下文，不关闭进程级 scheduler，也不取消 Job |

例如相机任务通过 `ScheduledDeviceJobHelper.WaitForTerminalStateAsync` 等待消息 Success/Fail/Timeout，并观察 Quartz cancellation token；超时只是使等待结束并上报异常，清理的是事件订阅，没有向硬件发送停止命令。取消 token 同样不等于相机采集已停止。0 秒在该 helper 中表示无限等待，但不能将此解释为所有 Job 的实现约定；FlowJob、HeadlessFlowJob 各有自己的执行链。

排障时要分别确认调度已暂停、Job 已返回、外部设备已停止和历史写入结果，不能为“消除 Running”未经授权结束进程或控制硬件。

## 执行结果、历史查询与清理

`JobWasExecuted` 以 Quartz 返回的 `JobExecutionException` 和 `context.Result` 判定结果：有异常即失败；没有异常时，仅反射检查结果对象的公开 `bool Success`，值为 false 才判失败。null、普通字符串或没有该属性的对象默认按成功统计。因此“工具方法返回/没有异常”要由 Job 自己正确映射业务失败，Scheduler 不会替它核实设备结果。

监听器先更新 UI 统计，再把历史写入排到串行 `_historyWriteTail` 并等待该次写入尝试结束，最后通知订阅者；订阅者异常被隔离。数据库初始化、插入和统计恢复的异常会记录日志但可能被捕获，历史没有记录不能证明任务没执行。只在返回后插入终态记录，进程崩溃时的在途任务没有持久化开始记录可供本模块恢复或补账。

启动统计按 JobName/GroupName 从数据库全量聚合；有记录才覆盖对应计数、平均/最短/最长耗时和最后结果/消息，不会恢复完整执行现场，也不覆盖所有 JSON 显示字段（如最后耗时）。清理历史或写入失败会使这些统计与旧 JSON 快照、当前内存计数不同，不能承诺永久累计计数。

`QueryExecutionHistory` 的任务/结果筛选用于分页和统计同一组数据，在读事务内完成，按 StartTime、Id 倒序稳定排序，越界页收敛到末页；窗口每页100条，统计是全部匹配历史而非当前页。查询失败明确返回 `QuerySucceeded=false`，窗口清空旧结果并提示；不要将失败后的空列表当成零次执行。旧的 `QueryRecords/QueryAllRecords/GetTaskStats` 错误回退为未区分失败的空/零结果，调用方需看日志。

历史窗口清理按钮经确认删除所有任务90天前的记录，即使窗口当前只展示某个任务或失败筛选也不会限于该视图。`CleanupOldRecords` 失败返回0，与无可删记录相同；没有内建撤销，也没有后台自动清理调用。删除历史会影响以后重启恢复的统计，不改任务定义，当前列表累计值也不会立即同步重算。执行清理或导出前需获得对应数据操作授权。

任务窗口已有 CSV、JSON 和文本报告导出，但导出的是当前 `TaskInfos` 快照，不是完整 SQLite 执行历史或原子恢复备份；手工 JSON 导出使用的设置也不同于 canonical `SchedulerTaskSerializer`。状态栏当前只显示任务数量/是否有任务，不证明 Quartz 已启动或 Job 正常。

## 验证范围与缺口

- `SchedulerTriggerFactoryTests` 检查触发器类型、追加次数、日历间隔、Cron、延迟及非法值；其中 Quartz RAM scheduler 用例只验证替换行为，不启动真实 Job，也不覆盖 `QuartzSchedulerManager` 的 JSON 失败补偿链。
- `SchedulerTaskSerializationTests` 验证旧多态 JSON、定义版本与替换备份；不验证管理器启动时的旧 Interval/Forever 暂停迁移或坏主文件恢复。
- `SchedulerHistoryQueryTests` 使用隔离临时 SQLite，验证筛选/分页统计、稳定排序与显式失败；不覆盖真实历史库初始化失败、执行监听写入或清理。
- `ScheduledDeviceJobHelperTests` 检查相机/光谱 Job 的并发属性、零/正超时转换以及等待超时/取消；不证明设备已被停止，也没有执行真实采集。

首次宿主访问、恢复后立即触发、重启重复执行、在途任务与管理操作交错、JSON/Quartz 补偿失败、shutdown 后任务静止、真实设备取消和时区/misfire 仍需隔离环境验证；涉及真实 Job 的验收必须另行取得设备和数据操作授权。

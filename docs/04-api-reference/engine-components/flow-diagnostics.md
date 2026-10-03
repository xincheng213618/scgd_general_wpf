---
knowledge_id: "flow.diagnostics"
knowledge_type: "topic"
status: "current"
summary: "Flow本地诊断SQLite快照、节点尝试与Incident事件列表的读写边界；快照不保证包含未保存画布，终态持久化与业务结果分开，中断恢复不续跑节点，心跳不是判死条件。"
aliases: ["离线节点分析","FlowAnalysisDataSource","流程运行诊断","流程 Incident 管理","复制稳定标识","定位当前画布节点","NodeExecutionFailed","NodeTimeout","PostProcessFailed","流程诊断快照","FlowTemplateSnapshotFactory","进程中断恢复","流程心跳","节点执行尝试","诊断记录未完成","Incident确认关闭","异常事件管理","FlowExecutionJournal","FlowExecutionJournalCoordinator","FlowExecutionJournalScope","FlowRunRecord","FlowNodeAttempt","FlowTemplateSnapshot","FlowIncidentService","FlowIncidentManagementWindow","FlowOwnerProcessState","RunRecovered","流程执行分析","阶段耗时","FlowNodeTiming"]
code_paths: ["Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowAnalysisDataSource.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowExecutionJournal.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/IFlowExecutionJournal.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowExecutionJournalCoordinator.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowExecutionRecovery.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowIncidentService.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowIncidentManagementWindow.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowIncidentManagementWindow.xaml","Engine/ColorVision.Engine/FlowProcessing/Runtime/ViewFlow.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowDiagnosticsSchemaMigrator.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowNodeRecordConfig.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowTemplateSnapshot.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowTemplateSnapshotFactory.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowTemplateWorkspaceController.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowRunRecord.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowExecutionEvent.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowNodeAttempt.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowIncident.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowExecutionSession.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowRunFinalizer.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowNodeAnalysisPage.xaml","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowNodeAnalysisPage.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowNodeTiming.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFlowNodeBase.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFindLuminousAreaNode.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameFileService.cs"]
test_paths: ["Test/ColorVision.UI.Tests/FlowExecutionJournalTests.cs","Test/ColorVision.UI.Tests/FlowExecutionJournalCoordinatorTests.cs","Test/ColorVision.UI.Tests/FlowExecutionRecoveryTests.cs","Test/ColorVision.UI.Tests/FlowIncidentServiceTests.cs","Test/ColorVision.UI.Tests/FlowDiagnosticsSchemaTests.cs","Test/ColorVision.UI.Tests/FlowRunFinalizerTests.cs","Test/ColorVision.UI.Tests/FlowNodeTimingTests.cs","Test/ColorVision.UI.Tests/LocalFindLuminousAreaNodeTests.cs"]
related: ["flow.session","flow.templates","flow.headless","operations.data","ui.sqlite-storage"]
---

# Flow 运行诊断、中断恢复与 Incident 处置

使用“流程 Incident 管理”查看异常事件、关联运行和节点尝试，或核对进程退出后的遗留记录。`FlowExecutionJournal` 是共享执行会话的本地诊断存储，不是业务执行器：有记录不证明设备动作或外部交付成功，没有记录也不证明流程未运行。业务完成以[共享会话最终化](../../01-user-guide/workflow/execution.md)及所属项目输出为准；[隔离无界面执行](../algorithms/templates/flow-engine.md)不会自动获得这套共享会话记录。

“中断恢复”是将确认失去拥有进程的遗留运行记录标为失败，并保留异常证据；它不续跑节点、不重放设备请求、不重做后处理，也不回滚 MySQL 批次或文件。

## 打开并查询异常事件

从流程编辑器工具栏点击“流程 Incident 管理”。窗口读取整份本地诊断库，不自动筛选为当前画布的流程，也不是全部运行记录列表。首次加载会构造 `FlowIncidentService`，可能初始化或迁移 SQLite 表、payload 字段与索引，删除旧的 TemplateId + hash 唯一索引，并设置 WAL / busy timeout；打开窗口不属于严格零写入检查。

1. 选择状态，按需填写严重级别、类型或搜索文本。
2. 点击“刷新”，或在搜索框按 Enter。筛选在查询时生效，并回到第一页；窗口每页50条，用“上一页”“下一页”翻页。
3. 选中左侧记录，在右侧查看“详情”“运行事件”“节点尝试”和“原始信息”。事件与尝试属于同一 Run 的全部记录，不仅限于该 Incident 关联的节点。
4. 需要关联其他日志或反馈问题时，点击“复制稳定标识”，并另行记录时间与 SN。

| 筛选项 | 含义 |
| --- | --- |
| 状态：待处置 | 默认值 `Active`，排除 `Resolved`；包含未确认和已确认，不是只查 `Open` |
| 状态：未确认 / 已确认 / 已关闭 / 全部 | 分别对应 `Open` / `Acknowledged` / `Resolved` / `All` |
| 严重级别 | 留空查全部，否则按字段等值匹配，如 `Error`、`Warning` |
| 类型 | 留空查全部，否则按字段等值匹配。运行链使用 `NodeExecutionFailed`、`NodeTimeout`、`PostProcessFailed`、`UnhandledRunException`、`ProcessInterrupted` 等值 |
| 搜索 | 在摘要、NodeId、DetailsJson 中包含匹配，不自动搜索流程名称、SN、RunKey 或 FlowKey |

列表按发现时间和 ID 从新到旧排列，发现时间标为 UTC。服务 API 的 `PageNumber` 最小为1，`PageSize` 限制为1–200、默认50；这些参数不是窗口里的可编辑选项。查不到记录时先清除筛选或选择“全部”，再核对诊断库位置、运行标识与记录写入错误。

### 关联运行与当前画布

| 操作 | 结果与限制 |
| --- | --- |
| 打开运行分析 | 有正数 `Run.BatchId` 时打开旧节点耗时分析，传入 BatchId、SN 和 NodeId；没有关联批次时提示并显示稳定标识 |
| 定位当前画布节点 | 当前流程与记录的 FlowKey 相同，或正 TemplateId 相同后，尝试按 NodeId 定位；不会切换模板、加载诊断快照或比较版本/hash。定位成功也不能证明画布就是运行时版本 |
| 复制稳定标识 | 复制 IncidentId、RunRecordId、RunKey、FlowKey、TemplateId、NodeId；不包含快照正文或 SN |

## 节点耗时与消息明细

在“流程执行分析”的概览中打开节点，选择本次执行的一条消息，在接收 Payload 中直接查看 `Timing` JSON。消息列表按内容收紧，较多消息可滚动；右侧历史列表用于切换执行记录。没有增加单独的阶段图表。

打开分析时优先读取并展示当前批次，最近批次与同流程历史导航随后在后台加载；导航就绪前切换按钮暂不可用，当前节点和消息仍可查看。概览先呈现节点列表与汇总，再创建时序图；直接打开节点详情不会先创建概览图表。首次确定运行与读取记录共用一次有界写入等待，刷新仍等待写入；超时且没有节点时提示稍后刷新。刷新、切换和关闭会使旧后台结果失效。`FlowAnalysisLoad` 在后台以 INFO 汇总写入等待、当前查询及页面构建耗时，`FlowAnalysisNavigation` 以 DEBUG 记录历史查询耗时；这些数值不代表屏幕完整绘制耗时，也不作为现场性能承诺。

概览节点行和节点页的“跨批次比对”集中显示同流程、同 NodeId 的执行与消息。列表包含 Batch、SN、时间、节点耗时和消息状态，可按批次、SN、事件、消息 ID 或状态消息筛选。选择消息后点击“设为比对基准”，随后选择其他批次；下方并排展示基准与当前选择的响应或发送正文和 Topic，JSON 自动格式化。没有消息的执行保留在列表中，超时不显示节点耗时；正文只读取当前选择和基准，不全量解压历史消息。

比对查询最近500条同流程运行记录并纳入当前打开运行，沿用 `FlowIdentity` 的 FlowKey、TemplateId、旧记录 FlowName 兼容规则，再按 Batch + SN 和 NodeId 收紧。旧运行缺 BatchId 时沿用完整 SN 关联，无法区分重复使用相同 SN 的旧运行；缺流程身份或节点 ID 时只显示本次执行，不用同名节点猜测跨批次关系。同一批次内节点循环执行的消息按 NodeRecordId 或旧消息时间范围关联，各次调用保留。此入口不比较模板版本或标记 JSON 字段差异，也不改变原本的运行切换与清理范围。

ARVRPro 的现场只读窗口可将数据库路径、来源标签和完整运行标识传入 `FlowExecutionAnalysisWindow`。`FlowAnalysisDataSource` 将概览、节点历史、运行切换、事件及惰性正文读取固定到该文件；缺失的可选诊断表返回空集合，不查询本机运行库。来源标签始终显示，清理入口隐藏且处理函数拒绝写入。未传离线来源的原有入口继续使用运行库。离线未结束节点以快照中最后节点时间为显示截止点，不随查看时刻累计等待时间。

`LocalFlowNodeBase` 为每次执行单独创建内存计时上下文；成功和异常完成消息均追加 `Timing`，保留已经完成或失败的阶段。相机取图、打开本地图片、校正、发光区定位、FindCross、FOV、点阵畸变的主要路径已接入。其他本地节点至少记录 `ExecuteLocal` 总体阶段，新增明细须在所属实现的实际操作处接入。L/BV 相机转发在本地成功响应中附带同结构计时；其执行异常仍沿用服务节点错误响应，不保证包含阶段明细。远端服务内部未提供的计时不能由客户端补造。

| JSON 字段 | 含义 |
| --- | --- |
| `Timing.Version` / `Unit` | 当前格式版本 1；时间单位 `ms`，小数保留至微秒量级，不代表硬件精度 |
| `Timing.TotalMs` | 通用本地节点从进入执行方法到序列化完成消息前的时间，含输入处理、算法及结果交接；不含后台任务排队、诊断 JSON 序列化及随后日志落盘。L/BV 转发从创建本地执行对象计时，跨取图与 Complete 交接，包含两者之间的等待 |
| `Stages[].Name` | 稳定操作名，如 `OpenImage`、`Algorithm`、`PersistResult` |
| `Id` / `ParentId` / `StartMs` | 阶段身份、父阶段及相对执行起点的开始偏移。父阶段耗时包含子阶段，不能把所有行直接相加 |
| `ElapsedMs` / `Status` | 阶段实际墙钟耗时，以及 `Completed`、`Failed`、`Canceled`、`Skipped`；若在阶段尚未结束时取得快照，则为 `Running`。0 ms 不等于未执行，跳过操作由 `Skipped` 表示 |
| `OmittedStages` | 每次最多记录 256 个阶段，超过上限只累计省略数；用于限制日志体积，不中断业务 |

**图像输入与算法耗时：**发光区定位、FindCross、FOV 和点阵畸变优先使用上游内存帧，此时 `OpenImage` 为 `Skipped`；没有内存帧、回退到文件时，`OpenImage` 包含文件存在性检查及实际加载。共享文件加载器进一步记录 `ReadImageHeader`、`ReadImageData`（CVRAW/CVCIE）或 `DecodeImage`（位图）及缓冲区复制。`Algorithm` 单独记录算法调用；模板更新、结果持久化和发布分别记录为 `UpdatePoiTemplate`、`PersistResult`、`PublishResult`。异常阶段会保留失败状态；后续未执行的操作不会补写为 0。

**相机与校正：**本地相机记录连接、锁等待、参数设置、自动曝光、源图尺寸查询（`GetSourceFrameInfo`）、帧分配、SDK 取图等边界。`CaptureFrame` 是整个 SDK 调用，不能进一步推断实际曝光、读出与传输。L/BV 本地响应同时保留 `Backend=Local`、设备编号、图像尺寸/位深/通道、实际曝光/增益、平均次数、RAW/CIE 缓冲区字节数及校正后端，供相同负载之间比较；这些字段不证明服务配置开关的状态。校正记录资源加载/缓存检查和算法执行；存图记录数据复制及 RAW/CIE 文件写入。`PublishPreview` / `PublishResult` 表示向界面或结果链提交的调用，不是屏幕实际绘制完成时间。

本地相机启用结果持久化时，`PersistResult` 内进一步记录 `ResolveBatch`（按 SN 查询批次）、`BuildResultModel`（组装及序列化图像记录）和 `InsertResult`（保存记录并取得 ID）。数据库阶段包含客户端调用及连接等待，不能直接当作数据库服务端 SQL 执行时间；父阶段已经包含这些子阶段，不能重复相加。相机结果插入使用公共 DAO；慢写或失败时另有 `DatabaseCommandTiming`，成功插入可按其 `Result` 对应相机 `MasterId`，进一步区分连接取得和命令执行（包含自动提交），不记录 SQL 正文或参数。登记仍先于存图完成，计时不改变结果交接顺序，也不额外写库。

原有 `TotalTime`、`CaptureTime`、`CalibrationTime`、`SaveTime` 的含义和业务结果结构保持兼容。例如发光区节点原有 `TotalTime` 仍是算法耗时，打开文件时间查看新 `Timing`；不能把两者相加。`Timing` 为完成日志保留字段，扩展本地节点时不要将其用作业务结果字段。

计时只在已激活的执行上下文中收集时间戳和有限条目，使用完成消息一次性保存，经[SQLite Payload 存储](../ui-components/sqlite-storage.md)压缩并按所选消息惰性读取。不逐阶段写文件/数据库，不在像素循环中打点，不全量读取历史 Payload；旧日志没有阶段数据时不会回填。

`FlowNodeTimingTests` 验证并发执行隔离、父子阶段、失败/取消、条目上限以及真实本地节点完成事件中的 JSON。`LocalFindLuminousAreaNodeTests` 用临时位图与替代算法/持久化服务验证文件回退、内存复用和保存失败阶段。不启动设备或读取真实历史数据库，当前套件也不生成分析页面预览。

执行日志在默认 INFO 级别保留流程开始/完成、协议消息及落库耗时、ARVR 阶段汇总、图像快照准备与导出耗时、实际清理汇总、重试和错误。已连接提示、逐节点正常转交、预处理匹配/开始、每个删除成功文件及导图准备提示使用 DEBUG，避免重复已经落库或汇总的信息。预处理动作成功且耗时至少 100 ms 时以 `PreProcessTiming` 保留动作类型、流程、SN 和耗时；失败与异常始终记录。正常 Socket 界面投影使用 DEBUG，排队至少 1000 ms 或更新至少 100 ms 时仍以 INFO 记录 `SocketMessageUiTiming`；这些是日志输出条件，不是业务超时或 PASS/FAIL 阈值。

`FlowPerformanceSampler` 在 `FlowExecutionSession` 或 `FlowControl` 收到引擎完成事件时，通过 `QueueIfDue` 尝试提交后台采样，两个入口共用进程内的 30 秒节流。完成回调只做节流和无等待提交；计数器读取、指标计算、JSON 序列化及 INFO `FlowPerformanceSample` 写入全部在线程池执行，不等待采样或日志完成，没有轮询线程或定时器。最多一个请求处于排队、读取或写日志状态，忙时跳过，避免慢日志积压任务；后台实际开始后重新计算节流起点，失败重试也受节流。

日志带进程、序列号、起始节点、请求时间 `RequestedAt`、后台实际采样开始时间 `SampledAt` 和排队耗时 `QueueDelayMs`，通过请求上下文与实际时间识别延迟样本。`FlowExecutionSession` 还提供模板名称和业务批次 ID，直接使用 `FlowControl` 的入口（包括 ARVR）没有这两个值时保留 null，通过序列号、起始节点及时间关联其他阶段记录，不伪造批次或模板名。样本包含窗口内的进程/系统 CPU 使用率、进程读写 MiB/s、当前工作集/私有内存、托管堆、分配速率、GC 次数及线程池可用线程/待处理任务。CPU、I/O 速率和 GC 次数使用两次实际读取之间的窗口；内存和线程池状态是在后台读取期间取得的快照，不代表流程结束的精确瞬间，也不是所有字段原子地同时读取。线程池状态包含采样工作本身。

进程 CPU 按 `Environment.ProcessorCount` 归一化；系统 CPU 使用 `GetSystemTimes`，超过 64 个逻辑处理器时仅代表调用线程所在处理器组。进程 I/O 包含文件、网络与设备操作，不能解释为物理磁盘带宽或磁盘队列；GC 暂停比例来自最近一次 GC 的运行时快照。第一次采样或计数器不可用时，速率/区间增量为 null，不能当作 0。`SamplingMs` 只包含计数器读取耗时，不包含指标计算、JSON 序列化和日志写入。

采样竞争或失败时跳过，不改写业务结果。线程池拥塞时样本可能延后，慢计数器或慢日志仍会占用一个后台工作线程，但完成通知不等待它。长流程尚未完成时不会产生中途样本，日志不含 CPU 频率/温度，不能直接证明降频或定位某个外部进程。`FlowPerformanceSamplerTests` 验证窗口计算、节流、后台请求上下文和时间、忙时跳过、读数或日志阻塞不阻塞提交、失败恢复、缺失数据与只读进程采样，不替代现场性能验收。

## 确认和关闭

确认与关闭是独立的处置写入，需要相应授权；它们不会改变 Run / Attempt 的结果，也不重跑流程。操作人字段默认填当前系统用户名，服务只接收字符串，不据此认证身份。

**当前窗口的线程访问限制：**确认和关闭处理函数在 `Task.Run` 中读取 `OperatorTextBox.Text`、`ActionNoteTextBox.Text`，违反 WPF 控件的线程访问要求。出现“调用线程无法访问此对象，因为另一个线程拥有该对象”时，处置方法尚未被调用，不能将下表的服务能力视为按钮已经可用。该问题位于 `FlowIncidentManagementWindow.xaml.cs`；底层服务测试不覆盖这条点击路径。

| 服务操作 | 输入与状态转换 |
| --- | --- |
| `Acknowledge`（确认） | 确认人必填、备注可选。Open → Acknowledged，记录 UTC 时间；重复确认保留首次信息。Resolved 或未知状态不能确认 |
| `Resolve`（关闭） | 关闭人、关闭备注必填。Open 或 Acknowledged → Resolved，记录 UTC 时间；重复关闭保留首次信息。未知状态不能关闭，也没有重新打开操作 |

服务成功写入后，刷新并核对状态与备注。“待处置”不显示已关闭记录，需切换到“已关闭”或“全部”查看。重复操作保留首次信息，但 `UpdateIncident` 仍执行事务与 Update；异常会抛给调用者，不受运行层的诊断降级保护。

## 记录由谁拥有

默认数据库来自 `FlowNodeRecordConfig.SqliteDbPath`，与旧 `FlowNodeRecord` / `FlowNodeMessage` 使用同一个 `FlowNodeRecords.db`，不是 Engine 业务 MySQL。默认路径与数据源入口见[数据所有者](../../01-user-guide/data-management/README.md)，压缩正文、停写维护与备份见[SQLite 存储维护](../ui-components/sqlite-storage.md)。不要把旧节点写队列、journal 事务和业务结果当成一个共同事务。

| 对象 | 关联与判读边界 |
| --- | --- |
| `FlowTemplateSnapshot` | 保存调用方提供的字节与内容 hash，不验证它是否是可运行的 STN。优先按 `FlowKey + ContentHash` 复用；未提供 FlowKey 时按 TemplateId 与 hash 查找。同 hash 但内容或长度不同会拒绝，不代表整个项目已备份 |
| `FlowRunRecord` | 本轮 `RunKey` 关联快照、SN、可选 BatchId、状态与最终结果；SN 不是 journal 的幂等键。记录另带 instance、机器、PID、进程启动时间和心跳 |
| `FlowExecutionEvent` | 同一运行内按 `EventKey` 去重，分配递增 `SequenceNo`；不是跨所有运行的全局顺序 |
| `FlowNodeAttempt` | `InvocationId` 区分调用，同一节点的不同调用递增 `AttemptNo`；重复提交同一调用不会新增一次尝试，不能把循环中的多次调用折叠成节点唯一记录 |
| `FlowIncident` | 同一运行内按 `IncidentKey` 去重，可关联节点和 attempt；处置状态与运行成功/失败是两个维度 |

journal 会校验重复键对应的内容，不是拿相同键就能覆盖旧事实。直接调用 journal 的非法参数或冲突会抛错；运行层 coordinator 才负责捕获存储异常并降级。

## 快照是否对应本次画布

当前记录链存在画布与快照不一致的限制。`ViewFlow` 的执行器连接当前节点画布，启动命令不先保存；`TryBeginExecutionJournal` 却从工作区模板身份快照中的 `FlowParam.DataBase64` 创建诊断快照，不重新读取 `STNodeEditor.GetCanvasData()`。

| 执行来源 | 诊断快照实际来源 |
| --- | --- |
| 已加载的数据库模板 | 捕获到的模板 `DataBase64`；之后未保存的画布编辑可以参与运行，但不会自动进入这份记录 |
| 以 `FlowParam` 打开的独立窗口，或成功读取包内 STN 的 `.cvflow` 文档 | 当时持有的 `FlowParam.DataBase64`，同样不保证包含后续画布修改 |
| 没有 `FlowParam` 的本地 `.stn` 文档或新建图 | 临时模板对象仅有名称；空 `DataBase64` 按空字节创建快照，不会从本地文件或画布补取 |

`FlowTemplateSnapshotFactory` 对传入字节复制并计算 SHA-256；journal 校验 hash 与内容一致，但允许空字节，也不加载 STN 来验证节点图。有效 hash、版本号或记录已创建都不证明它准确保存了本轮画布，空快照也不会仅因长度为0就触发 legacy 降级。

复现时同时确认运行入口、模板保存状态及实际文件/画布来源，不单凭快照或版本号还原本轮运行。保存目标与文档切换规则见[流程工作区](../../01-user-guide/workflow/design.md)。

## 记录失败不应改写业务结果

`FlowExecutionSession` 创建业务批次后尝试创建快照和 journal scope，再记录前处理、节点事件和最终化。快照非法或诊断库不可用时可以退回 legacy 记录路径；这个容错不承诺 MySQL 批次写入、节点动作或后处理也会被忽略。

`FlowExecutionJournalCoordinator.Shared` 默认每5秒更新 scope 心跳。首次初始化失败后，30秒内的后续访问不再重试工厂；到期后由新的访问触发重试，不是每30秒自动重新连接。成功创建 journal 时尝试一次 abandoned-run recovery；恢复查询失败会记日志，但不禁用新运行记录。

`FlowRunFinalizer` 先解析后处理和最终业务状态，再尝试 journal 终态写入。最终化结果可以已经返回，而 SQLite 仍没有成功写入终态。因此“后处理完成”“收到最终结果”“本地记录已完成”需要分别核对。

`FlowExecutionJournalScope.TryCompleteRun` 的重试规则：

- 首次请求固定 `(Status, ElapsedMs, FinalOutcome)`，停止心跳，并对相同终态最多立即写两次。
- 两次都失败时可再次请求同一结果，不能改用另一终态。`IsCompletionRequested` 表示终态已成功持久化，不是仅发起过请求。
- 完成请求设置后，scope 拒绝后续普通事件/attempt 调用；检查与实际写入不是一个原子区间，已通过检查的并发写入或直接 journal 调用不受这项门禁完整隔离。
- `Dispose()` 只停心跳和释放 timer，不补写终态，也不安排后台无限重试。

底层 `CompleteRun` 重复收到同一终态会返回原记录并保留第一次完成时间；结果三元组冲突则拒绝。不要为了让界面状态变绿，把仍待核对的记录改成成功。

## 哪些遗留运行会被恢复

`RecoverAbandonedRuns` 只考察 `FlowStatus.Runing` 且 `CompletedTimeUtc` 为空的记录；`Runing` 是现存枚举拼写。还必须同时满足：

1. owner instance、机器、正 PID 和进程启动时间完整。
2. 属于当前机器，但不是当前 instance。
3. `IFlowProcessProbe` 明确返回 `NotRunning` 或 `StartTimeMismatch`。真实进程探针以 PID 和启动时间一起区分 PID 复用，启动时间比较允许1秒精度差。

其他机器、旧记录缺 owner、当前 instance、存活进程、`Unknown` 或探针异常都不据此终结。**心跳年龄不参与判死**；即使心跳很旧，只要上述证据不足也不恢复。5秒心跳间隔不等于5秒失联就自动标失败。

每条候选在独立写事务内重新读取并核对状态、完成时间和 owner 未变，然后：

- 将运行与最终结果都设为 `Failed`，填写恢复时间/原因。
- 将未完成 attempt 标为 `Interrupted`，错误码为 `ProcessInterrupted`。
- 写入 `RunRecovered` 事件和一个 Open / Error / `ProcessInterrupted` Incident。

单条冲突或失败会回滚该条并继续其余候选，不是所有候选的整体事务；重复恢复已终结记录不会再追加一套事件。恢复耗时按原开始时间到恢复时刻计算，不是精确的进程退出时刻，也不能解释为硬件持续工作了这么久。

## 验证入口与缺口

| 测试 | 已有用例的范围 |
| --- | --- |
| `FlowExecutionJournalTests` | 快照复用、每轮事件顺序、稳定键和终态冲突 |
| `FlowExecutionJournalCoordinatorTests` | 用替身检查初始化失败、心跳与相同终态重试 |
| `FlowExecutionRecoveryTests` | 临时 SQLite、可控进程探针下的 owner 判定、单条恢复回滚与幂等；另检查本测试进程启动时间的数据库往返 |
| `FlowIncidentServiceTests` | 筛选、详情、Open 确认后关闭、输入校验；直接调用服务，不操作窗口 |
| `FlowDiagnosticsSchemaTests` | 解码字节 hash、无效 Base64、旧表迁移 |
| `FlowRunFinalizerTests` | 后处理策略，以及后处理先于 legacy fallback 持久化；不是到真实 journal 的组合测试 |

窗口查询/分页/定位、确认/关闭点击、当前画布与快照一致性，以及 Incident 直接关闭 Open、重复动作、未知状态和在途并发等边界，没有被上述用例逐项覆盖。正文分别依据窗口、服务、journal 与执行会话源码，不把服务层测试当成完整界面或跨库验证。

这些测试不连接现场业务库或设备，也不证明真实断电时刻、硬件停止、跨进程/跨数据库事务或客户交付完整。没有诊断结果时先核对记录阶段、存储初始化/写入异常和 owner 证据，不能据此授权重跑可能已产生副作用的流程。

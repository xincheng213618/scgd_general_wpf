---
knowledge_id: "engine.results"
knowledge_type: "topic"
status: "current"
summary: "算法结果交接、已加载批次主记录查询与叠加显示：区分本地中立 Geometry/Overlay、Engine 历史 handler 和客户业务导出，保留来源与文档/revision 生命周期。"
aliases: ["结果交接","算法结果管理","算法视图配置","历史结果查询","保存数据列","自动保存数据列","结果白色底图","原图缺失","结果尺寸恢复","persistent overlay","overlay 注册句柄","结果CSV","结果列表清空","ResultMessageBus","AlgorithmResultDataSaver","ViewAlgorithmConfig","AutoRefreshView","AutoSaveSideData","AlgorithmResultImageDimensions","算法有结果为什么没有叠加层","ViewResultAlg","ResultHandleRegistry","IViewResult","IResultHandleBase","CanHandle1","AlgorithmOverlayManager","本地算法结果叠加","AlgorithmOverlayRenderer","AlgorithmGeometryArtifact","AlgorithmOverlayArtifact","批次详情","已加载批次结果","InspectLoadedBatchResults","inspect_loaded_batch_results"]
code_paths: ["Engine/ColorVision.Engine/Services/Core/ViewResultAlg.cs","Engine/ColorVision.Engine/Services/ResultHandleRegistry.cs","Engine/ColorVision.Engine/Abstractions/IResultHandlers.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/Views/AlgorithmView.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/Views/AlgorithmView.xaml","Engine/ColorVision.Engine/Services/Devices/Algorithm/Views/ViewAlgorithmConfig.cs","Engine/ColorVision.Engine/Abstractions/ViewConfigBase.cs","Engine/ColorVision.Engine/Services/Results/ResultMessageBus.cs","Engine/ColorVision.Engine/Services/Results/AlgorithmResultDataSaver.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/AlgorithmResultImageDimensions.cs","Engine/ColorVision.Engine/Services/Core/ResultImagePresentation.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/DisplayAlgorithmManager.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFindCrossNode.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFindLuminousAreaNode.cs","UI/ColorVision.Algorithms/AlgorithmResults.cs","UI/ColorVision.ImageEditor/Algorithms/AlgorithmOverlayRenderer.cs","UI/ColorVision.ImageEditor/Algorithms/AlgorithmOverlayManager.cs","UI/ColorVision.ImageEditor/Contexts/ImageProcessingContext.cs","UI/ColorVision.ImageEditor/ImageView.xaml.cs","UI/ColorVision.ImageEditor/Operations/ImageOperationCoordinator.cs","Engine/ColorVision.Engine/Dao/MeasureBatchPage.xaml.cs","Engine/ColorVision.Engine/Dao/CopilotLoadedBatchResultsReader.cs","Engine/ColorVision.Engine/Dao/CopilotMeasurementResultContextProvider.cs","Engine/ColorVision.Engine/Services/Core/ViewResultImage.cs","ColorVision/Copilot/Capabilities/CopilotLoadedBatchResultsInspectionService.cs","ColorVision/Copilot/Capabilities/CopilotLoadedBatchResultsInspectionService.Cursor.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectLoadedBatchResultsTool.cs","ColorVision/Copilot/Mcp/CopilotMcpToolEnvironment.LoadedBatchResults.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.LoadedBatchResults.cs","ColorVision/Copilot/Skills/colorvision-database-operations/SKILL.md","UI/ColorVision.Common/Interfaces/Copilot/CopilotBusinessContextBuilder.cs"]
test_paths: ["Test/ColorVision.UI.Tests/AlgorithmResultOverlayTests.cs","Test/ColorVision.UI.Tests/FindCrossResultOverlayTests.cs","Test/ColorVision.UI.Tests/LocalFindCrossNodeTests.cs","Test/ColorVision.UI.Tests/LocalFindLuminousAreaNodeTests.cs","Test/ColorVision.UI.Tests/AlgorithmResultImageDimensionsTests.cs","Test/ColorVision.UI.Tests/ResultMessageBusTests.cs","Test/ColorVision.UI.Tests/AlgorithmOverlayManagerTests.cs","Test/ColorVision.UI.Tests/CopilotLoadedBatchResultsWorkflowTests.cs","Test/ColorVision.UI.Tests/CopilotLoadedBatchResultsSourceTests.cs","Test/ColorVision.Copilot.Tests/CopilotLoadedBatchResultsServiceTests.cs","Test/ColorVision.Copilot.Tests/CopilotLoadedBatchResultsToolTests.cs","Test/ColorVision.UI.Tests/CopilotLoadedBatchResultReferenceContextTests.cs"]
related: ["engine.index","engine.mqtt","engine.devices","ui.image-editor","algorithms.platform","algorithms.find-cross","algorithms.find-light-area","algorithms.arvr"]
---

# 算法结果交接、展示与导出

先区分三种结果：Engine 的数据库历史结果、统一图像算法的中立 Result/overlay、客户项目业务结果。它们可以显示在同一个 ImageEditor 中，但注册、存储和生命周期不是同一套契约。

## 按问题定位

| 问题 | 入口 |
| --- | --- |
| 历史结果有记录但没图 | `ViewResultAlg.FilePath`、`AlgorithmResultImageDimensions`、文件服务 |
| 历史结果有图但没叠图 | `ResultHandleRegistry`、`CanHandle1`、DAO 和 `Load/Handle` |
| 新增本地算法并在图上显示结果 | [算法注册与执行](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md#新增本地算法与结果展示) → Geometry/Overlay → `AlgorithmOverlayRenderer` → `AlgorithmOverlayManager` |
| 新统一算法叠图清不掉或误删新叠图 | `AlgorithmOverlayManager`、注册 token、文档 ID 与 source revision |
| 明细表为空 | `ViewResults`、handler 的 `Load` 与列绑定 |
| CSV/MES/Socket 客户字段不对 | 项目 `Process`、Recipe/Fix、exporter，不在通用 overlay 管理器修 |
| 调试按钮打开错算法窗口 | `DisplayAlgorithmManager` 和 `IDisplayAlgorithm`，不是结果注册表 |

## 接收结果与查询历史

`AlgorithmView` 关联 `DeviceAlgorithm` 时订阅该设备的 MQTT 回包和本地 `ResultMessageBus`；无设备参数的视图不会建立这两种实时订阅。

| 入口 | 接受与加载条件 |
| --- | --- |
| 远端回包 | 设备 Code 精确匹配且 `Data.MasterId` 可转换为正数，随后回查 `AlgResultMasterDao`；这段接收逻辑不以回包 Code 或 EventName 作成功门禁 |
| 本地结果消息 | ResultKind 为 `algorithm`；Route 为 `local-flow` 时不按设备 Code 过滤，其他通知仍要求 Route 为 `algorithm` 且设备 Code 精确匹配。随后按 MasterId 回查主表，不从消息直接取得像素或明细 |
| 工具栏 **查询** | 先清当前列表，直接按主表 ID 排序加载；默认倒序、最多 50 条，按 `Count` 完整加载，`Count<=0` 不限条数；不自动限定当前设备或批次 |
| **高级查询** | 打开主结果表的通用查询窗口；按用户条件和查询数量完整加载，与实时消息筛选分开，不受实时结果保留上限限制 |

回查找不到主记录时跳过并记日志，没有自动重试；加载成功后通过 Dispatcher 排队插入列表，执行前仍检查视图/设备是否已释放。视图按主记录 ID 去重，重复通知不重复创建驻留结果或自动保存数据列。通知到达、数据库可读、列表插入和选中展示不是一个事务；本地消息的 `Code=0` 是固定信封值，算法成败看主记录 `ResultCode`。

纯本地算法和文件节点不绑定设备：结果主表的设备代码为空，通知使用 `ResultRoutes.LocalFlow`（`local-flow`），保留 `SerialNumber`、`NodeId`、`ZIndex` 和主结果引用。`LocalPoiNode` 等纯本地算法完成并发布持久化结果后，已订阅的算法详情（包括默认算法）自动新增记录，沿用自动选中、去重和历史容量设置；即使详情尚未显示也能接收。本地算法通知不按设备 Code 过滤，也不修改主表设备归属；文件节点的图像通知不进入算法列表。从流程批次结果或普通历史查询加载时仍使用同一套 DAO 和 handler。相机、校正及服务节点继续采用各自的设备路由。

通过齿轮打开 **算法视图配置**，区分以下选项：

| 配置 | 默认值与作用 |
| --- | --- |
| `InsertAtBeginning` / `AutoRefreshView` | 均为 true；实时结果插到开头并选中新项，关闭自动刷新只阻止自动选中，不阻止插入 |
| `AutoSaveSideData` | false；开启后，实时新增结果在刷新步骤后调用数据列保存；刷新或 handler 抛错可能阻断后续保存 |
| `SaveSideDataDirPath` | 桌面；只给自动数据列保存提供目标目录，不代表每个 handler 都会写出有效数据 |
| `Count` / `OrderByType` | 50 / Desc；控制普通历史查询，不是实时结果列表的容量上限 |
| `MaxHistoryCount`（实时结果保留上限） | 500，至少 1 条；实时新增结果时按此上限裁剪当前列表，修改后也立即裁剪当前列表；搜索加载时不裁剪 |

手动查询直接填充列表，不走实时新增的自动选中/自动保存流程。清空按钮和删除键只修改内存列表，不删除数据库记录或原文件，之后查询仍可能看到这些结果。

搜索时按查询设置完整加载，可以超过默认 500 条实时保留上限；之后有新实时记录加入，就将同一个列表收回到上限，旧搜索记录也会被淘汰。裁剪按最小主记录 ID 淘汰，表头排序与插入方向不改变规则，晚到的旧通知不会挤掉上限内已保留的较新结果。裁剪选中行会解除选择并清理侧栏和图像，自动刷新开启时再选择新结果。右键菜单与命令按需创建，释放视图时清空驻留结果与去重索引。`ResultHistoryTests` 覆盖实时容量、排序后的淘汰、重复通知、配置兼容、完整搜索后恢复实时上限和释放边界。

## Engine 历史结果契约

| 对象 | 责任 |
| --- | --- |
| `ViewResultAlg` | 主结果 ID、批次、文件路径、模板名、`ViewResultAlgType`、明细集合 |
| `IViewResult` | POI、MTF、SFR、FOV、Ghost 等具体算法明细 |
| `ResultHandleRegistry` | 收集可实例化的 `IResultHandleBase` 派生类型 |
| `IResultHandleBase` | 类型匹配、按需加载明细、表格/叠图展示、可选侧边导出 |
| 项目结果模型 | 客户判定、字段映射和 CSV/MES/Socket 输出 |

`AlgorithmView.listView1_SelectionChanged` 从 `ResultHandles` 取第一个满足 `CanHandle1(result)` 的 handler，然后依次调用 `Load(context, result)`、准备图像坐标空间、`Handle(context, result)`。默认 `CanHandle1` 判断 `CanHandle.Contains(result.ResultType)`，V2 等 handler 可以覆写它并检查结果版本。

主结果与明细的关系是：`AlgResultMasterModel → ViewResultAlg → ResultHandleRegistry/CanHandle1 → Load/DAO → IViewResult → Handle/表格/图元`。不匹配时会清空图像，而不是随意选一个 handler。多个 handler 的匹配条件冲突会受到集合顺序影响，不能以“都能处理同一枚举”作为正确注册。选择变化先清空明细绑定和侧边文本，随后才匹配和加载；`CanHandle1`、`Load`、图像准备或 `Handle` 抛错没有此入口的逐阶段捕获/回滚。尤其 `Load` 发生在清理旧图之前，加载失败时旧底图可能仍在，不能把它当作当前记录的图像。

Copilot 的 `InspectLoadedBatchResults`／`inspect_loaded_batch_results` 只读取已打开并注册的 `MeasureBatchPage` 详情页内存主记录。自动上下文已提供完整有效 `source_ref`／`expected_source_instance_id` 对时，优先原样用于该页读取；否则省略 `source_ref` 分页发现全部已注册来源，再用目录返回的精确绑定读取。需要单条记录时再传该来源返回的 `result_ref`。这些32位十六进制对象引用不是批次 ID、结果 ID、名称或文件路径，不能从当前激活窗口、选中行或其他业务编号猜测，也不回退到另一个批次。该范围不包括未打开的历史数据库记录、设备算法视图的全部结果或客户项目判定。

`max_items` 默认40、范围1–100；非空 `cursor` 最多4,096字符，须原样续传。`source_ref` 必须同时提供 `expected_source_instance_id`，目录查询也可传该标识以绑定注册表；`result_ref` 必须属于所选来源，且不能与 cursor 并用。目录与结果行分别按 `source_ref`／`result_ref` Ordinal 升序分页，游标绑定注册表来源、查询模式、对应对象集合 revision、所选批次身份和实际交付末行，分页大小可以改变。每页最多18,000字符完整 JSON，预算不足时只移除尚未交付的尾行，再从实际末行续读。

根对象的 `source=registered_loaded_batch_details`，`mode` 区分 `directory`、`results` 与 `focus`；`source_instance_id` 是应原样传回的注册表标识，`sources_revision`／`inventory_revision` 描述对应对象集合。目录行包含 `source_ref`、`batch_id` 及已加载图像／算法数量，结果行包含 `result_ref`、`kind`、`internal_id`、`type_code`、已脱敏有界 `template_name`、`result_code`、耗时与创建时间。`sources`／`results` 仅承载本页实际交付行，按 `has_more`／`next_cursor` 继续相应流；末页不包含前页，coverage 的 `all_matched_sources_returned`／`all_matched_results_returned` 也只评价当前响应。无注册详情页可以得到真实空目录，不代表数据库没有历史结果。

精确来源或记录已不存在时为 NotFound，不回退到当前页面或相同内部 ID；cursor／注册表绑定不匹配或游标绑定集合变化为 Validation，须重新发现。采集期间来源变化也明确返回 Validation、不给出本次窗口，重试时由正式绑定检查判断原 cursor 是否仍可用。页面卸载、关闭或批次模型引用／ID 改变会撤销旧来源，只有已有成功加载／注册边界才能重新提供来源，不把旧 rows 重标成新批次。源不可读或不一致明确失败，不能当成空结果。跨页读取不承诺冻结显示 metadata，也不证明数据库或执行历史完整。

结果 metadata 保留种类、内部 ID、类型、原始可空结果码、耗时和记录时间；同一数字 ID 在图像与算法结果中不代表同一个对象。未知结果码保持未知，非零码或批次状态不替代客户 PASS／FAIL 判定。`recorded_duration_ms` 保留原始带符号耗时，`duration_ms` 仅在非负有效时提供，否则为 null；coverage 的 `unknown_result_code_rows`、`unknown_duration_rows`、`invalid_duration_rows` 只统计已交付行，不能把缺失或负耗时补成0。图像耗时直接读取数值来源，不从界面的 `mm:ss` 展示文本反向解析；`created_at` 保留原 `DateTime.Kind`，并由 `created_at_kind` 与 `unspecified_created_at_rows` 说明，不补造 UTC。`text_redacted`／`text_truncated`／`output_limited` 分别标明文本和整页预算边界。工具不交付 SN、路径、原始消息、参数、测量值或文件存在判断。

正式读取不创建页面或结果 wrapper，不调用 DAO、handler 的 `Load/Handle`、图片或硬件入口，也不改变列表选择。来源通过弱引用登记，读取在排队前固定注册集合，再用同一个 UI Dispatcher 回调采集全部页面，前后复核批次模型、集合引用和成员对象；不同 Dispatcher 的来源明确拒绝，不交付跨 UI 线程拼接的快照。采集期间注册变化使本次读取失败，但重试时仍有效的精确来源或游标可以继续使用：目录游标绑定来源集合，结果游标只绑定所选来源、原始批次 ID 和该来源的记录集合，其他页面注册变化不自动使它失效；新鲜的精确聚焦也不因其他记录增删而转向另一对象。自动测量结果上下文仍负责汇总与当前选中项，不能代替这条明确对象绑定的正式读取链。

自动测量结果上下文的固定来源 ID 保持 `measurement-results`。有效详情页快照从已有 registration token 一次取得 `LoadedBatchSourceInstanceId`／`LoadedBatchSourceRef`，正文在固定说明之后、长 metadata 之前以 `Loaded batch result source: ` 加完整 JSON 输出对应工具参数对；历史列表、legacy 快照、半对或无效引用不输出绑定。采集引用不创建／重新注册来源；卸载、关闭或模型改变后也不能用旧 rows 重绑。历史聊天附件中的绑定可能已过期，须由正式工具再次核验，失效后重新发现，不能按批次 ID 回退。

本地 Flow 节点失败仍可能产生诊断主记录：`LocalFindLuminousAreaNode` / `LocalFindCrossNode` 在检测拒绝或结果校验失败时尝试保存非零 `ResultCode` 的主记录并发布，随后节点失败；不会把这条记录写成下游有效 `MasterValue`。前置错误、数据库或发布失败可能更早中断，不能保证每次失败都出现在结果页。成功/失败明细、文件和事务规则分别见[发光区定位](../algorithms/templates/find-light-area.md)与[十字定位](../algorithms/detectors/find-cross.md)。

## 注册与手动算法发现的区别

`Services/ResultHandleRegistry.cs` 在惰性单例首次创建时遍历 `AssemblyHandler.GetAssemblies()`，实例化非抽象的 `IResultHandleBase`。因此需要确认程序集已经进入发现集合、类型可无参构造、匹配条件正确；不能假设之后加载程序集会自动重建这个注册表。`GetTypes` 和构造器调用没有逐类型捕获，一个类型加载或构造失败就可能中断整次初始化；集合没有显式优先级排序，handler 实例由注册表共享，而非每个视图独立创建。

`Services/Devices/Algorithm/DisplayAlgorithmManager.cs` 扫描的是 `IDisplayAlgorithm` 与 `DisplayAlgorithmAttribute`，负责手动算法菜单、创建算法实例和 `DisplayAlgorithmControl`。它**不负责扫描结果 handler**。`Abstractions/IDisplayAlgorithm.cs` 也不是结果注册入口。

## 图像缺失与历史回放

`Load` 返回后，`PrepareResultImageSurface` 清理画布旧图元。`File.Exists(FilePath)` 为真时交给 handler 打开文件；它不在此处验证文件能否解码。文件缺失时，`AlgorithmResultImageDimensions` 用主结果的正数 BatchId 查询测量图像记录，依次尝试：

1. `FilePath` 按分号拆分后的路径与测量记录 `FileUrl` / `RawFile` 匹配；双方都是完整路径时不只按同名文件匹配。
2. 相同 ZIndex 的记录。
3. 整个批次的记录。

第 1 步按路径先后分别尝试，不汇总所有路径；每个候选集合只有一个不同的有效尺寸时才采用，否则继续后续路径或下一层；尺寸来自 `ImgFrameInfo` 的正整数 width/height，字段名不区分大小写。批次内尺寸仍有歧义时清空旧图并记录警告，不任选一个尺寸。

尺寸可恢复时，`ResultImagePlaceholderCache` 提供白色矢量底图并保留坐标空间。它不是恢复出的原始像素，也不提供可重新计算算法的图像帧。文件缺失且尺寸恢复失败后仍会继续调用 `Handle`，所以不能从图像准备方法返回推断最终已经有图。

历史结果完成切图后，右键本地算法取得的帧必须对应当前 `ViewBitmapSource`。ImageView 在取帧时会核对缓存所属的源图对象；源图已替换而缓存仍指向上一条结果时，先使旧 revision 失效并从当前位图重建帧。

客户项目可以有自己的历史图片策略，例如保存原图/标注图的回退。不要把某个项目的策略当成 Engine 全局契约；项目页面和项目测试是该策略的权威入口。

## 保存数据列与导出图像

| 入口 | 实际输出与限制 |
| --- | --- |
| **保存数据列** / 结果行右键同名项 | `AlgorithmResultDataSaver` 对选中且有匹配 handler 的记录再次 `Load`，再调用 `SideSave`，不调用 `Handle`；文件名、字段与覆盖/追加规则归具体 handler |
| 工具栏保存图标 | 要求有选中项，但 CSV 导出的是当前 `ViewResults` 全部主记录；追加写入并再次写表头，同时尝试将当前 `ImageShow.Source` 保存为同名 PNG，不包含独立画布叠图 |
| 结果行右键 **导出** | `ViewResultAlg.Export` 只接受 CIE 文件并打开 `ExportCVCIE`；不是主表 CSV、客户 MES 或所有结果明细的通用导出 |

保存数据列的可用性只检查是否匹配 handler；基类 `SideSave` 是空实现，所以按钮可用、方法返回都不证明文件已生成。多条保存按顺序调用，没有逐项异常隔离或整批回滚。工具栏 CSV 则使用固定主字段顺序、直接拼接逗号和换行，未做字段转义；显示列经过调整时，表头与固定字段顺序也可能不一致。CSV 与 PNG 不是一个保存事务。

## 统一算法 overlay 是另一条链

`UI/ColorVision.Algorithms/AlgorithmResults.cs` 中的结果与 `AlgorithmOverlayArtifact` 是宿主中立数据，不依赖 Engine DAO 或 `IViewResult`。ImageEditor 使用 `AlgorithmOverlayRenderer` 生成图元，`ImageOperationCoordinator` 持有本视图的 `AlgorithmOverlayManager`，并通过 `ImageProcessingContext` 提供兼容访问与注册入口。Renderer 在界面线程处理结果，按几何 ID 查找引用；重复 ID 取最后一项，引用缺失的 overlay item 跳过。几何点数/种类不支持时可能没有可见图形，有 artifact 不等于画出了内容；最终注册还要求文档 ID、source revision 仍有效且视图未释放。源、显示和算法协调的所有权见[编辑器上下文](../ui-components/image-editor-context.md)。

| 生命周期条件 | 当前语义 |
| --- | --- |
| 相同名称注册新 overlay | 先移除旧项，再登记新 token 与 store entry ID；后续失败不保证恢复旧图元，不能当作完整替换事务 |
| 旧会话释放 | 只有 token 仍匹配时才能移除，不能删除同名的新替代项 |
| source revision 提交 | 清除旧 revision 的 transient 项；persistent 项继续跟随当前文档 |
| 文档替换或清理 | 清除较旧 revision 的图元/artifact，保留已在新 revision 注册的项；宿主释放清全部 |
| 注册释放 | transient 随会话 Dispose 移除；persistent 保留，需显式移除或文档清理，持久性不代表已经保存到磁盘 |
| 跨线程变更 | Manager 方法通过画布 Dispatcher 执行；兼容 `AlgorithmOverlays` store 在后台清理时，图元删除使用 BeginInvoke 排队，存储和视觉变化可暂时不同步；entry ID 防止旧回调删掉同名新项 |

注册句柄的 `Dispose` 与 `Remove` 共用一次性标志；persistent 句柄 Dispose 后再调用该句柄 Remove 不会删除保留项，需通过兼容 store 的删除/清理入口或文档变更清理。Renderer 的异常清理会 Remove 本次已注册项，但不恢复被同名替换掉的旧项；store 自身的异常回滚测试不等于画布与多项结果的事务保证。

这条链的调用仲裁见 [统一图像算法平台](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md) 和 [ImageEditor](../ui-components/ColorVision.ImageEditor.md)。不要为了显示普通本地算法结果就增加 Engine 历史 DAO，也不要让旧历史 handler 绕过自己的加载契约直接套用中立 Result。

## 新增 Engine 历史结果展示

下面步骤仅适用于接入 Engine 数据库历史结果，不是中立算法 overlay 或客户项目结果的通用实现清单。

1. 明确主结果类型、版本和历史数据兼容规则。
2. 定义实现 `IViewResult` 的明细模型，使用所属模板的 DAO。
3. 派生 `IResultHandleBase`（抽象基类），在 `CanHandle` / `CanHandle1` 中准确匹配。
4. `Load` 负责明细加载；`Handle` 使用 `ViewResultContext` 更新列表和图像。
5. 复用 `UI/ColorVision.ImageEditor/Draw/` 图元与现有坐标转换，例如 `imageView.AddVisual(...)`；不另起独立 Canvas。
6. 客户判定和导出字段保留在项目包；旧数据、缺图与坐标空间都要验证。

## 验证入口与缺口

| 测试 | 已有断言范围 |
| --- | --- |
| `AlgorithmResultOverlayTests` / `FindCrossResultOverlayTests` | 多边形笔刷，以及十字历史叠图的原始中心解析与回退 |
| `ResultImagePresentationTests` / `AlgorithmResultImageDimensionsTests` | 尺寸 JSON、占位缓存、路径/ZIndex/批次尺寸选择；不访问真实数据库 |
| `ResultMessageBusTests` | 进程内信封字段与顺序退订；不覆盖已排队 UI 回调、DAO 或实际展示 |
| `AlgorithmOverlayManagerTests` | transient/persistent、同名替换、后台 facade 清理、较旧 revision 清理与新注册保护；部分用例仅测试 store 回滚 |
| 本地 FindCross / FindLuminousArea 节点测试 | 使用替身核对诊断主记录、发布与下游引用；不等于真实 native、数据库及图像页全链验收 |

已加载批次读取的离线验证入口是 `CopilotLoadedBatchResultsWorkflowTests`：真实详情页注册后沿默认 Engine reader、原生共享调用与 MCP 读取未选中记录，核对精确对象引用、同内部 ID 替换、卸载失效、原始耗时／时间、隐私边界与不改变选择。这些测试不代表真实数据库、设备或模型验收。

本页列出的测试覆盖上述局部边界；handler 扫描失败、条件重叠、Load/Handle 中断、实时重复消息、查询范围和各导出路径仍需对应隔离验证。真实数据库、设备与项目输出需要单独验收，文档和工具校验不能替代运行结果。

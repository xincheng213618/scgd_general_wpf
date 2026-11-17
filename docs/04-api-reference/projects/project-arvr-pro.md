---
knowledge_id: "projects.arvr-pro"
knowledge_type: "reference"
status: "current"
summary: "ARVRPro 项目入口、Socket 自动化、输出与历史结果查询；流程组、实例 Recipe 和 Demura 各有对应操作主题。"
aliases: ["现场数据库离线查看", "打开现场数据", "ArvrOfflineDataSource", "ARVR 历史原图删了还能看结果吗", "保存结果图会不会重复画标记", "ProjectARVRPro", "ResultImageFileCandidates", "SavedSourceImageFileName", "SavedResultImageFileName", "结果统计", "统计日期记忆", "CycleTimeStatisticsWindow", "ARVR 项目", "算法实证", "同图算法比对"]
code_paths: ["Projects/ProjectARVRPro/Offline/", "Projects/ProjectARVRPro/ARVRWindow.xaml", "Projects/ProjectARVRPro/TestResultViewWindow.xaml", "Projects/ProjectARVRPro/ThunderbirdSerialDebugWindow.xaml", "Projects/ProjectARVRPro/ARVRWindow.xaml.cs", "Projects/ProjectARVRPro/FlowRuntimeEstimateCache.cs", "Projects/ProjectARVRPro/FlowRunningNodeTracker.cs", "Projects/ProjectARVRPro/ResultImagePresentation.cs", "Projects/ProjectARVRPro/ProjectARVRReuslt.cs", "Projects/ProjectARVRPro/ViewResultManager.cs", "Projects/ProjectARVRPro/Services/SocketControl.cs", "Projects/ProjectARVRPro/Services/SwitchGroupSocket.cs", "Projects/ProjectARVRPro/Services/RunAllSocket.cs", "Projects/ProjectARVRPro/Flow/ExternalImageSwitchNode.cs", "Projects/ProjectARVRPro/CycleTimeStatisticsWindow.xaml", "Projects/ProjectARVRPro/CycleTimeStatisticsWindow.xaml.cs", "Projects/ProjectARVRPro/ResultStatisticsTheme.xaml", "Projects/ProjectARVRPro/ResultStatistics.cs", "Projects/ProjectARVRPro/ResultTimeline.cs", "Projects/ProjectARVRPro/ProjectARVRProConfig.cs", "Projects/ProjectARVRPro/AlgorithmEvidence", "UI/ColorVision.ImageEditor/Algorithms/Evidence"]
related: ["projects.index","projects.arvr-pro-demo","projects.arvr-pro-protocol","projects.arvr-pro-processes","projects.arvr-pro-demura","projects.capabilities"]
---

# ProjectARVRPro

`Projects/ProjectARVRPro/` 是当前主力 AR/VR 专业测试项目包，运行时以 `ProjectARVRPro.dll` 加载。维护时优先看流程组、Socket 自动化、切图、Recipe 和输出格式。

排查整组取图、切图和流程耗时时，在反馈窗口选择“ARVRPro 测试与阶段耗时记录”和“ARVRPro 流程配置”。前者默认收集最近 7 天的本地结果库，后者脱敏后保留当前已保存的流程组、切图设置、相机覆盖参数与 Recipe。导出只读，结合节点、MQTT 和 Socket 记录归因。 [反馈诊断](../ui-components/ColorVision.UI.Desktop.md)

## 按任务查找

| 现场问题 | 第一检查点 |
| --- | --- |
| 项目包没出现 | `manifest.json`、`ProjectARVRPro.dll`、插件目录、主程序版本要求 |
| 配置流程、Recipe 或解析映射 | [流程组与解析配置](./project-arvr-pro-processes.md) |
| 初始化后没有下一步 | 当前 `ProcessGroup` 是否有启用的 `ProcessMeta` |
| 外部系统触发无反应 | [TCP 协议、请求字段与错误排查](./project-arvr-pro-protocol.md) |
| 切图失败 | `PictureSwitchConfig`、雷鸟串口、返回值和超时 |
| RunAll 只跑一部分 | `AllowTestFailures`、Flow 模板名、切图和预处理错误 |
| CSV 或 Socket 字段不对 | `UseLegacyARVROutput`、标准 CSV、Legacy 输出、客户 XLSX |
| AOI 流程卡住 | 主 Socket、外部切图节点的等待状态、`AOITestSwitchImageComplete` |
| Demura 烧录失败 | [PG 连接、GECS 指令及烧录诊断](./project-arvr-pro-demura.md) |
| 重启后配置丢失 | `%APPDATA%/ColorVision/Config/ProjectARVRProProcessGroups.json` 和 Recipe 配置；升级时核对旧共享文件迁移日志 |

结果工具栏的“缓存管理”打开进程级总览，并标记当前窗口的截图缓存。可释放当前窗口或全部缓存，已保存文件与在用帧受保护。结果图片按数据库路径查缓存，命中时可直接加载，无需原文件仍在磁盘。 [缓存与图像读取](../engine-components/ColorVision.FileIO.md)

## 项目边界和版本

| 入口 | 通信方式 | 流程组织 | 典型风险 |
| --- | --- | --- | --- |
| Pro JSON | JSON `EventName` | `ProcessGroup` + `ProcessMeta` | 切图回执、Legacy 输出、控制连接归属 |
| Pro LUX | 文本命令 | 活动流程组 + `SocketCode` | 文本返码、客户命令映射 |

客户项目判定逻辑应留在 `Projects/ProjectARVRPro/Process/` 和 Recipe 体系里，不要回写到 Engine 通用模板或 UI 基础库。手工维护项目 `ProjectARVRPro.csproj` 的 `VersionPrefix`；打包器从主 DLL 的文件版本生成 manifest 版本，不手工同步 `manifest.json`。最低宿主要求读取 manifest 的 `requires`。

原 LUX 客户端可直接对接 ARVRPro。将“通信 → 服务设置”的解析模式设为 `Text`，再在“流程处理配置 → 其他配置”填写设备号和 VID 参数，在流程组填写 `SocketCode`。设备号是回包前缀，旧配置由现场重新填写。Pro 客户端继续使用 `Json`。 [LUX TCP 协议](./project-lux-protocol.md)

原独立项目通过仓库 `Projects/ARCHIVED.md` 的 Git 标签回溯，后续迭代在 Pro。

`T0000` 可在窗口创建前回握手。`T0001` 和 `T0031` 通过后台实例测量，窗口已打开时显示状态与耗时，并在测量期间锁定测试和配置。 [单次 VID 测量](./project-arvr-pro-protocol.md#单次-vid-测量)

协议入口位于 `Services/LuxSocketDispatcher.cs`，设备调用位于 `Services/LuxDeviceMeasurements.cs`，单流程适配位于 `ARVRWindow.Lux.cs`。

## 主链路

外部系统发送 `ProjectARVRInit`，或用户在窗口输入 SN 后，`ARVRWindow` 选择当前 `ProcessGroup` 并找到下一个启用的 `ProcessMeta`。步骤启用 `PictureSwitchConfig` 时先切图，再运行绑定的 FlowEngine 模板。该次启动选定的处理实例通过 `IProcess.Execute(ctx)` 读取 Engine 结果并应用自身 Recipe，最后写入 `ObjectiveTestResult`，按配置保存 SQLite、CSV、Legacy CSV、客户 XLSX，并通过 Socket 返回下一步或最终结果。

上方“测试”按钮在单流程实际运行时显示“停止”；启动准备和结果收尾期间暂不可点击。点击“停止”调用现有流程停止接口，再以 `Failed`、原因“用户停止测试”进入同一单流程失败处理，执行解析器失败处理、保存失败结果和整组汇总，并按 `AllowTestFailures` 决定发送下一项 `SwitchPG` 或结束测试。停止与自然结束重叠时只收尾一次，迟到的旧流程结束事件不能影响下一轮。“一键执行”仍使用原有执行路径，上方按钮在该路径中不提供停止操作。

节点诊断由 `FlowNodeExecutionRecorder` 按运行编号收尾，旧流程的迟到事件不能关闭新一轮。新批次启动前会收尾停止流程的残留记录。诊断落库超时或失败记日志，操作员可继续启动下一轮。

宿主提供 `QueueFlowRunAsync` 时，成功流程的 SQLite 诊断记录在后台收尾，生产批次和结果仍按原顺序保存。诊断队列积累到四轮时，最多等待最早一轮 5 秒，超时后后台仍继续写入。结果页和节拍统计里的“流程执行分析”会先等待选中批次的诊断写入，超时可稍后重试。`ARVRFlowPhaseTiming` 的 `FlowDiagnosticsDeferred=True` 表示 `FlowRunRecordMs`、`NodeRecorderFlushMs` 记录入队耗时。实际写入结果见同一序列号和批次的 `ARVRFlowDiagnosticsCompletion`，节拍上等待诊断队列的时间单独记录为 `FlowDiagnosticsBackpressureMs`。

`ARVRFlowPhaseTiming.ResultSqliteWrites` 分别记录明细、整组汇总和完成时间的保存阶段。`GateWaitMs` 是本进程的数据库维护锁等待，`BeginMs` 包含 SQLite 开启事务时的等待，`RowMs` 是字段写入，`CompressionMs` 和 `PayloadMs` 分别是 GZip 压缩与 BLOB 写入。`CommitMs` 记录显式提交。完成时间使用单条语句，隐式提交计入 `RowMs`。明细的 `PublicationMs` 包含提交后的列表检查和同步界面回调。未取得耗时的阶段为空，`Succeeded` 记录保存调用的结果。

## 界面主题

“空间不足时自动清理”默认关闭。启用后按各输出盘的剩余空间下限检查，默认 100 GiB。`ResultStorageSpaceManager` 从旧到新删除已识别的导出图像、标准 CSV 和历史日汇总 XLSX，达到阈值即停止。原始采集、数据库、快捷方式目标、未知文件、当前 SN、在途输出和当天汇总受保护。只扫描配置的结果和客户报表目录。清理后 SQLite 历史仍在，历史图像需核对候选文件。

清理仅支持本地磁盘目录，跳过根目录、符号链接、联接和挂载点。阈值不小于总容量时跳过。占用、权限或容量不足等问题见清理日志，保存按原错误处理继续。CSV/XLSX 保存完成后才发送最终响应，保存期间拒绝下一轮初始化与启动。

底部“设置”进入“检测设置”，结果栏齿轮进入“结果保存”，均打开“模组检测设置”。结果目录、CSV、旧格式兼容、客户报表和编号在“结果保存”。标记图、原图和快捷方式在“图像保存”，共用输出根目录。雷鸟串口及手动切图从流程处理配置的“雷鸟连接与切图”进入。

`Ctrl+F` 搜索设置，`Esc` 清空。可用原图格式取决于源图的 BMP 支持。

“图像保存”按通道配置命名，旧配置沿用原命名。标记图默认 `{FlowName}`，原图默认 `{FlowName}_source`。兼容 ProjectLUX 可选 PNG，例如 `White51_Test.png`。模板支持固定文字、`{FlowName}`（流程模板名）和 `{SN}`（序列号）。路径与本次输入图冲突时（含目录联接或符号链接）追加通道后缀。标记图和原图重名时给原图追加 `_source`。重测可覆盖上次导出。

启用“标记图使用热力图底图”后，按输出灰度范围生成 Jet 底图并叠加标记。仍保存一张同名、同格式、同尺寸标记图，`HeatmapMs` 记录额外处理耗时。

用 `BatchId` 和 `ResultId` 关联同一次后台图像保存的日志。核对连续生产时的节拍，查看发起与结束时的在途数量，以及最后一次导出何时完成。各层的总耗时包含各自子段。

- `ARVRImageSnapshotTiming` 记录 UI 捕获的 `CaptureMs`。随后 `ImageSnapshotExportTiming` 将 `QueueMs` 中的工作槽等待和渲染线程等待分别记为 `EncodeSlotWaitMs`、`StaWaitMs`。从提交编码到编码回调启动的 `EncodeDispatchWaitMs` 已包含在 `EncodeWriteMs`。未进入的等待阶段为 0。准备成功时 `PixelAlignedRgb48Png` 表示是否使用全尺寸 RGB48 对齐路径。`AdaptivePng=True` 表示大尺寸带标记结果图通过透明度及压缩采样检查并采用了快速无损 PNG 编码，解码像素、DPI 及 ColorVision 显示记录沿用原有结果图约定。
- `ARVRImagePublicationTiming` 记录空间检查 `StorageWaitMs`、后台处理 `SnapshotPipelineMs`、正式文件替换和暂存清理 `FilePublishMs`、结果路径回写 `ResultPathPersistMs`。`TotalMs` 从 UI 捕获之后的导出提交计至任务结束。结合发布标志和 `PathUpdateStatus` 区分正式文件落盘与路径回写完成。

全尺寸和半尺寸的大幅 RGB48 标记图都可使用快速 PNG 编码，压缩样本较密集的半尺寸结果图使用 WPF 编码。`HalfPrescaledRgb48=True` 表示底图逐像素对齐且宽高可精确减半，WPF 先缩放底图再叠加原有标记。

设置逐项自动保存，文字和数字在结束输入时提交。无效值标红，保存失败恢复该项运行值并可重试。关窗时确认是否放弃未保存项。恢复默认经确认一次写入。未编辑项、雷鸟参数及旧文本路径保留，在途导出沿用启动时的设置。

执行状态栏显示当前节点与整数毫秒耗时。“详情”可复制完整错误、流程名和精确耗时，并查看历史估计。错误保留至后续执行更新。界面的“流程执行完成”只表示 Flow 完成，产品判定以 PASS/FAIL 结果为准。

“正在执行”列出尚未结束的并行节点。重叠执行的同一节点全部结束后才移除，悬停和详情可看完整列表。上一轮迟到事件不改变当前状态。

页面使用动态画刷，结果、连接和图像标记保留业务色含义。 [ColorVision.Themes](../ui-components/ColorVision.Themes.md)

## 长期运行与结果视图刷新

主窗口实时最多保留最近 1,000 条，列表淘汰不删除 SQLite 或在途导出的结果。手动历史查询仍按配置数量，恢复实时追加后重新限量。

在主窗口结果表用鼠标左键拖动列头可改变列顺序，例如将 `SN` 拖到第一列。关闭模组检测窗口时保存列顺序，重新打开时恢复。已有配置缺少列顺序时沿用默认布局，升级新增的列追加在已保存列之后。

`ARVRFlowPhaseTiming` 的待快照数和未结束导出数都不能视为实际并发编码数。排查短时卡顿优先看 `FlowPerformanceSample` 的窗口 GC 停顿，进程累计百分比会稀释短时事件。页面错误数包含软缺页，需独立证据区分磁盘换页。

结果工具栏的“视图刷新管理”汇总已接入的 `AutoRefreshView` 开关，同类设备合并显示并列出影响范围。

逐项调整或“一键关闭全部刷新”后点击“确定并保存”才生效，取消或保存失败恢复原状态。只有已加载且仍开启的项目计入运行警示。关闭自动刷新保留视图与查询配置。

## 关键目录和配置

| 目录/文件 | 作用 |
| --- | --- |
| `ARVRWindow.xaml.cs` | 主窗口、初始化、单步执行、RunAll、结果完成 |
| `ProjectARVRProConfig.cs` | 全局运行配置，例如 SN、重试、失败策略 |
| `Process/` | `IProcess`、流程组、流程步骤、各测试项解析 |
| `Recipe/` | 限值和 `y = Kx + B` 修正 |
| `Services/SocketControl.cs` | `ProjectARVRInit`、`SwitchPGCompleted` 等 JSON handler |
| `Services/RunAllSocket.cs` | Socket 触发一键执行 |
| `Services/SwitchGroupSocket.cs` | 外部切换流程组 |
| `Flow/ExternalImageSwitchNode.cs` | 向外部 Client 请求切图并等待完成确认的本地节点 |
| `ObjectiveTestResult.cs` | 聚合结果模型 |
| `ViewResultManager.cs` | 本地结果、SQLite、CSV 和输出配置 |
| `TestResultViewWindow.xaml.cs` | 结果查看和导出 |

流程字段、内置处理类型、解析映射的优先级、实例 Recipe、切图默认值及配置迁移统一见[流程与解析配置](./project-arvr-pro-processes.md)。`ProcessGroup` 决定执行顺序，独立的 `ResultParserMetas` 提供解析映射，不额外加入 RunAll。

## Socket 自动化

ARVRPro 通过 `ColorVision.SocketProtocol` 的 JSON 模式接入外部系统。常规节奏是 `ProjectARVRInit` 初始化，软件返回 `SwitchPG`，外部切图后发 `SwitchPGCompleted`，软件运行当前 Flow 和 `IProcess`，全部完成后返回 `ProjectARVRResult`。

| EventName | 作用 |
| --- | --- |
| `ProjectARVRInit` | 初始化测试并返回第一步切图信息 |
| `SwitchPGCompleted` | 外部确认切图完成，触发当前步骤 |
| `SwitchGroup` | 切换当前流程组 |
| `RunAll` | 一键执行当前组内启用步骤 |
| `AOITestSwitchImageComplete` | AOI 切图完成信号，完成当前外部切图节点的等待 |

详细请求与响应、索引约定、状态码、并发限制及 AOI 切图见 [TCP 通讯协议](./project-arvr-pro-protocol.md)，可运行的客户端示例见 [Integration Demo](./project-arvr-pro-integration-demo.md)。

## 输出和兼容

标准 CSV 为七列，文本按规则转义，数值用小数点。批量横向 CSV 用首条有指标的记录定列。`UseLegacyARVROutput` 影响 CSV 和 Socket `ProjectARVRResult.Data`，改字段前先确认客户格式。

新版嵌套畸变结果按项目结果属性名输出，光学畸变字段为 `DistortionTestResult.OpticDistortion`，新生成的测试项 `Name` 也为 `OpticDistortion`。算法原始结果中的 `Optic_Distortion` 由 Engine 在解析入口映射，项目输出不沿用该原始字段名。读取历史项目结果时仍兼容 `Optic_Distortion`，重新序列化只输出 `OpticDistortion`；两个字段同时存在且标准字段非空时以标准字段为准。

W255 和键化亮色度 `LuminanceChromaticityTestResults[配置 Key]` 保留原有 `ColorUniformity`（所有 POI 两两之间的最大 Δu′v′），并在其后输出独立的 `ColorCenterRmsToD65`。该指标对各流程已应用色度修正的有效 POI 直接计算相对 D65 的等权 RMS Δu′v′，不匹配或读取 `PoiAnalysis` 中的均匀性结果，在模板均匀性和本地均匀性两种模式下都会计算。计算值再应用自身 Recipe 的 K/B 修正与 Min/Max 限值，默认 Min/Max 均为 `0`，表示不约束；设置非零限值后按对应上下限参与该流程 PASS/FAIL 判定。亮色度 Key 为 `White` 时，该指标同步到 W255 兼容结果。旧配置没有该字段时使用此默认值，已保存的限值保持原值；没有有效 u′v′ POI 时记录警告并保留默认测试项，不增加限值判定。公式、D65 常量和 POI 样本边界见 [CVCIE POI 结果数值](../engine-components/cvcie-results.md#色彩中心与-d65-rms)。

## 历史结果图回退与持久化

历史记录的原始图像不在原路径时，不应立即判定“无法查看结果”。`Projects/ProjectARVRPro/ResultImagePresentation.cs` 中 `ResultImageFileCandidates.GetExisting` 按下列顺序收集存在且去重的路径，`OpenFirstAsync` 在解码失败、超时或未得到图像时继续尝试下一候选；取消仍中止当前请求。

| 顺序 | 字段/条件 | 显示规则 |
| --- | --- | --- |
| 1 | 原始 `FileName` | 打开原图并按历史结果重新绘制 overlay |
| 2 | `SavedSourceImageFileName` | 打开保存的原位深原图并重新绘制 overlay |
| 3 | `SavedResultImageFileName` | 直接显示已经含标记的结果图，`RequiresOverlayRendering=false`，不能重复绘制 overlay |
| 4 | 候选均不可用，但能确认结果宽高 | 使用相同尺寸的白色画布承载历史标记 |
| 5 | 候选均不可用且尺寸未知 | 清除旧底图并记录失败，不能继续显示上一条结果的图 |

`ARVRWindow.xaml.cs` 负责实际加载、请求版本仲裁和 `RenderResultImage` / `ShowSavedResultImage` 分流。两个保存路径是 `ProjectARVRReuslt` 的可空列；`ViewResultManager` 通过现有 `CodeFirst.InitTables<ProjectARVRReuslt, ObjectiveTestResultRecord>()` 为旧库补列，不能要求用户删库或手工编辑 SQLite。旧磁盘 PNG 不会被自动扫描回填；只有实际成功导出的通道才更新对应路径。

内置解析器复用当前批次已经读到的图像尺寸，需要时仍回查数据库兼容外部解析器和历史缺失值。查不到宽高仍可保存结果。实现见 `IProcessExecutionContext` 和 `ViewResultManager.Save`。

在主窗口结果工具栏、右键菜单或本机 PG 明细打开“算法实证”，需宿主算法组件与项目包配套。可分析历史查询快照或手选本地图。历史图按原图、无标记备用图顺序读取，缺图跳过。现场资料只读窗口不开放该功能。“上传当前原图”仅加入云盘队列，需在云盘点击“开始 / 继续上传”才分享。

发光区可比 RobustV2、经典兼容和服务端 AA。十字 RGB 显示逐点 R-G/B-G 及覆盖、饱和信息。点阵畸变可比本地 GridV2 和服务端旧版本，各用自己的参数。如果两版公式或点位不同，应分别读原值。

AA 与旧版畸变调用可选 x64 `CV_algorithm.dll`，需配套 `opencv_world401.dll` 和 VC++ 运行库；从主服务发现或手选库。默认标为**参考参数（非历史参数）**，可选模板或导入 JSON，不自动还原历史参数。报告记录库路径、SHA256、实际参数及原始结果。只接受 8/16 位 Gray/BGR，BGRA 去 alpha，浮点图不自动缩放；不输出调试图、不请求设备或改写 POI、历史结果。

换图或换算法只更新载入内容，点击“开始验证”才运行。失败、缺库或无效几何不叠加，迟到结果不画到新图。原生 DLL 崩溃可终止宿主，只加载可信且版本匹配的库。算法接受状态不能作为客户产品判定。

报告只导出已计算并查看的结果、来源、算法身份、参数和缺图清单，不重新采集或写库。

## 结果统计与查询记忆

### 启动准备与预计时间

“上次执行 / 预计剩余时间”按同一窗口、同名称和相同内容的已完成模板估算，不查询历史库。缓存未命中先显示实际经过时间；估计不参与稳定等待、判定、超时或保存。

圆环显示启用步骤顺序与运行进度，未知耗时使用旋转弧。完成、失败和停止使用各自状态，后续未执行的步骤仍保持原状。

双击步骤或按 Enter 编辑模板，右键“编辑配置”进入该 `ProcessMeta` 的 Recipe/Process/切图。执行、切图等待和导出期间配置入口禁用。

### 统计口径与阶段归因

“统计设置”可启用 L/R 联合统计，新增“全批次记录”并扩展首页指标。开关由 `CycleTimeStatisticsWindowConfig` 单独持久化。

首页可切换累计或每小时产量。小时产量按结束时间归入 `[整点, 下一整点)` 并包含 PASS 和 FAIL，平均 CT 只按实际完成批次计算。空小时产量为 0，CT 留空。“全部”仍按月汇总。切换统计图只重绘现有查询结果。

在结果统计选择“打开现场数据”读取反馈 ZIP 或 `ProjectARVRPro.db`，也可选数据库所在目录或其 `Database` 上级目录。`ArvrOfflineDataSource` 用独立副本打开四个已知库，文件夹导入包含已提交 WAL。默认定位到资料最新日期，原资料和本机运行库不写入。副本可从缓存管理的“现场数据副本”清理，活跃进程和仍在读取的副本受保护。原 ZIP、源目录和聚合资料不在清理范围。

同一设备的多次反馈可用 `Scripts/merge_feedback.py` 手动聚合，以扩展可查询的历史范围。需要 Python 3.11 或更新版本，仅使用标准库。从仓库根目录运行 `py .\Scripts\merge_feedback.py "D:\Feedback\<机器名>"` 只预览；需要生成或更新时手动加 `--apply`。`--feedback-id <原始 feedbackId>` 可重复使用以选择部分反馈。脚本核对 `SystemInfo.txt` / 反馈元数据中的机器名与设备目录名，以原始 `feedbackId` 识别来源，按 `serverReceivedAt`、旧 `createdAt`、目录时间依次确定快照顺序，不使用文件修改时间，也不使用反馈上传客户端的版本代替采集时主程序版本。接收反馈、启动程序和打开统计窗口均不会触发聚合。

执行后在设备目录下建立 `_aggregate`：`Sources/<feedbackId>/` 保存各次日志、配置和导出说明；`versions/<版本>/Database/` 保存结果、节点、Socket 和 MQTT 的聚合只读资料；`aggregate.json` 指向最近完整发布的版本，记录原 ZIP 路径、SHA-256、接收时间、各库导出范围及去重计数。原反馈文件不改写。重复行保留原编号，先核对运行身份，再选用较晚接收的快照；同 SN 的不同执行不按 SN 合并，缺失的新字段不编造值。每行保留来源关联，原始正文可追溯到原包。编号对应的身份不同、字段类型不兼容、导出失败或已导入原包发生变化时停止，不自动重新编号、跨机器拼接或覆盖旧聚合。较早无结果库的反馈仍保存日志，但无法补出不存在的结果记录；各库时间覆盖仍须查看清单，不能把区间并集视为无缺口的连续数据。

后续手动重复执行只导入新增反馈，未变化的已导入包不会再次计数。更新先在独立版本准备和校验数据库，再一次替换清单指针；失败留下的 `.pending_*` 仅供排查，不供统计窗口读取。旧版本保留，因此聚合占用额外磁盘。查看时用“打开资料文件夹”选择设备下的 `_aggregate`，读取器先固定一个已发布版本，再复制到独立离线目录；已打开的窗口不随后续更新切换。原有“打开现场数据”也可直接选择该版本 `Database/ProjectARVRPro.db`。

离线窗口复用整轮结果、PG 明细、时间轴与导出。选中一轮后可打开“本轮相关消息”；Socket/MQTT 按本轮前后各 1 秒筛选候选消息，必须结合 SN、MsgID 和连接地址核对，不能仅凭时间邻近认定业务归属。PG 明细右键的执行分析先在同一份节点库按 `BatchId` 查找，再核对项目 SN 与 Flow 的“SN＋启动时间”，要求唯一运行标识；无匹配或多匹配明确提示，不回退到本机 MySQL。节点历史、消息正文和流程切换仍限定同一数据源；离线窗口隐藏清理操作，禁止批次 MySQL 查询及打开现场绝对图片路径。各库是独立导出快照，缺库/缺记录不表示现场没有执行；反馈不含原图，不能承诺查看图像。

离线库兼容旧 TEXT 和 gzip。可选阶段字段缺失显示不可用，缺少必要表或关联字段则拒绝打开并说明原因。初始化只读，无需补列或建索引。

本机旧库首次打开时自动补齐索引并保留原记录，大库可能打开较慢。统计首页的单侧汇总和趋势共用一个已提交数据快照，查询可与本机 WAL 写入并行。离线资料保持只读。

在批次记录或全批次记录选中一轮，下方时间轴用相同的起止区间展示各流程，悬停阶段可查耗时。右键流程明细可打开执行分析或测试结果；离线资料中的本机文件位置和 MySQL 查询入口不可用。

联合统计只读结果库。全局记录按 ID 相邻、均已最终化、SN 主体相同且后缀依次为 `_L_HHmmss` 和 `_R_HHmmss` 的两条记录组成全批次。单侧记录均保留在“批次记录”。全批次仅在两侧均 PASS 时为 PASS，按 R 最终化时间归入所选周期。跨期时也按 R 的实际前一条记录确认配对，允许 L 在范围外完成。

全批次 CT 从 L 收到 Init 并创建记录，到 R 结果最终化，包含两侧 CT 和 L 完成到 R Init 的等待。全批次时间轴标出 L Init、L 完成、R Init、R 完成。联合视图仍保留单侧指标，趋势切换为全批次口径。

首页的“平均单侧运行时间（不含 PG）”代替本小时和今日计数：先按每条最终化批次的 SN 与前后结果 ID 边界汇总关联流程的 `RunTime`，再在所选日/周/月/全部周期内按批次取平均。“平均全批次运行时间”同样先将每个完整 L/R 配对的左右流程运行时间相加，再按全批次取平均。运行时间仅包含流程实际执行，不包含 PG 应答、切图与准备、执行后处理或 L→R 等待，与墙钟 CT 分开显示；没有关联流程记录的单侧批次、任一侧缺少流程记录的全批次均不计入运行时间平均值，无可用样本时显示 `-`。

整组 CT 是 `ObjectiveTestResult.SessionStartTime` 到结果记录最终化的墙钟时间。统计界面中的“PG→执行结束”按每步 `SwitchPG` 发送到该步流程执行结束累计，内部包含 PG 应答、启动准备和流程执行；它与单独显示的绿色执行时间是包含关系，不能再次相加。整组 CT 减去这些 PG 周期后，才是执行结束后的结果处理、保存与无法归因空档。后台结果图导出可与后续流程重叠，也不能仅凭单张导出耗时推断其占用了同等 CT。

新记录在 `ProjectARVRReuslt` 的可空阶段时间字段中保存精确边界；本机运行库通过现有 CodeFirst 初始化自动补列，离线资料只读映射可用字段：

| 时间段 | 字段边界 | 时间轴含义 |
| --- | --- | --- |
| PG 应答（PG 周期子段） | `SwitchRequestedAt` → `SwitchAcknowledgedAt` | 发出 `SwitchPG` 到 ARVR 在 UI 线程处理 `SwitchPGCompleted`；包含接收后的存储、派发和 UI 调度，不是物理 PG 单独计时；RunAll 可为空 |
| 流程准备 | `SwitchAcknowledgedAt` → `PictureSwitchStartedAt` | 确认后选择/刷新模板、捕获运行上下文和读取运行内预计时间缓存；缺任一边界时保持未归因 |
| 本地切图/稳定 | `PictureSwitchStartedAt` → `PictureSwitchCompletedAt` | `PictureSwitchService` 调用和配置的稳定等待 |
| 预处理/准备 | `PictureSwitchCompletedAt` → `PreProcessingCompletedAt` | Flow 启动前预处理；边界间没有记录的部分仍显示为未归因 |
| 流程执行 | `FlowStartedAt` → `FlowCompletedAt` | 与流程运行 stopwatch 对应的主执行段 |
| 执行后处理/保存 | `FlowCompletedAt` → `ResultProcessingCompletedAt` | 流程收尾、客户结果读取与解析、结果记录保存；旧日志不能把整段等同于纯解析耗时 |

`ARVRFlowPhaseTiming` 用 `SN + Model + BatchId` 关联耗时。`FlowFinalizeMs` 已包含运行记录、批次回写和节点刷新，不应与子段重复相加。数据库慢调用对照 `DatabaseCommandTiming` 区分打开连接和 SQL；核对阶段终点是否落盘再分析时间轴。

`ResultImageDimensionsFromProcessCache` 为 `false` 时，保存可能因兼容回退再查 MySQL，可结合 `ViewResultSaveMs` 及图像尺寸定位。

启动阶段的字段以 `ARVRWindow.xaml.cs` 中 `ARVRFlowPhaseTiming` 为准。`StartupWorkMs` 可能省略单步方法进入之前的步骤选择。`RefreshTotalMs` 已包含服务发送、解绑、图加载和事件挂接。`BatchCreateMs`、`NodeRecorderStartMs` 属于 `FlowMs`，不计入启动段。服务发送时间不能用来估算设备应答。

原始回包到业务派发应结合宿主的 [Socket 计时](../ui-components/ColorVision.SocketProtocol.md#回包派发与界面刷新计时)，不能把消息列表后台排队时间重复计入 PG 或 CT。

旧记录没有上述阶段字段时，时间轴只用 `CreateTime - RunTime` 推算流程执行段，并把剩余部分明确显示为“未归因间隔”；旧数据不能事后可靠拆分切图、预处理和结果保存。任何缺失、逆序或超出整组范围的阶段边界都不能被强行归类。

- 软件每次重新启动后，三个页面默认“按天／今天”，筛选文本和结果条件回到初始状态。同一次软件运行中关闭、重新打开统计窗口，保留当前标签、周期、日期和筛选条件。
- `ProjectARVRProConfig.ResultStatisticsWindowState` 仅作为运行内状态，标注 `[JsonIgnore]`；旧配置 JSON 中的同名字段不再恢复，关闭统计窗口也不会为查询条件写入配置。此规则只涉及查询条件，不会删除或截断历史结果数据。
- 手动切换仍支持按天、按周、按月和全部。按天使用所选日期的 `00:00` 到次日 `00:00`，按周使用周一到下周一，按月使用月初到下月初，统一为包含起点、不含终点的区间；不是滚动 24 小时或最近 7 天。
- “今天／本周／本月”返回当前周期并保留筛选，“全部”隐藏日期导航。“重置”清空筛选。SN 和流程名按字面子串匹配，`_`、`%` 作为普通字符。
- 首页与批次记录按整组结束时间统计，流程查询按流程 `CreateTime` 统计，不能把跨午夜完成的整组记录误算到开始日。首页“今日产量”和“本小时产量”是所选查询范围中落入当前日期/小时的子集；查看不包含今天的历史范围时这两个值为零。

## 验收

| 验收项 | 通过标准 |
| --- | --- |
| 项目装载 | 菜单入口出现，`ARVRWindow` 能打开 |
| 流程组 | 切换、保存、重启后步骤顺序和启用状态恢复 |
| Socket 初始化 | `ProjectARVRInit` 返回第一条启用步骤的 `SwitchPG` |
| 切图确认 | `SwitchPGCompleted` 后运行绑定 Flow 和 `IProcess` |
| RunAll | 当前组启用步骤按顺序执行，失败策略符合配置 |
| Recipe | 限值、修正、PASS/FAIL 和窗口显示一致 |
| 输出 | SQLite、CSV、Legacy、客户 XLSX、Socket 结果都符合当前配置 |
| 外部切图节点 | 请求、完成确认、超时/停止和稳定延时可追踪；验证客户端回执归属 |
| 交付包 | `.cvxp` 内含 DLL、manifest、README、CHANGELOG |

## 本地构建与测试

以下命令写入本地编译产物。项目版本读取 `ProjectARVRPro.csproj` 的 `VersionPrefix`，与主程序版本独立。

```powershell
dotnet build Projects/ProjectARVRPro/ProjectARVRPro.csproj -c Release -p:Platform=x64
```

## 打包上传（需明确发布授权）

只有明确要求发布 ProjectARVRPro 时才运行以下命令。wrapper 会重新构建、生成并上传 `.cvxp`，随后清理本地包；不支持 `--no-upload`。打包器由主 DLL 的 `FileVersion` 同步 manifest 版本，不要另行手工同步 manifest。发布完成还需核对远端元数据和可下载包，不能以本地 build 成功代替。

```powershell
.\Scripts\package_project.bat ProjectARVRPro
```

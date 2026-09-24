---
knowledge_id: "projects.arvr-pro"
knowledge_type: "reference"
status: "current"
summary: "ARVRPro 项目入口、Socket 自动化、输出与历史结果查询；流程组、实例 Recipe 和 Demura 各有对应操作主题。"
aliases: ["现场数据库离线查看","打开现场数据","ArvrOfflineDataSource","ARVR 历史原图删了还能看结果吗","保存结果图会不会重复画标记","ProjectARVRPro","ResultImageFileCandidates","SavedSourceImageFileName","SavedResultImageFileName","结果统计","统计日期记忆","CycleTimeStatisticsWindow","ARVR 项目"]
code_paths: ["Projects/ProjectARVRPro/Offline/","Projects/ProjectARVRPro/ARVRWindow.xaml","Projects/ProjectARVRPro/TestResultViewWindow.xaml","Projects/ProjectARVRPro/ThunderbirdSerialDebugWindow.xaml","Projects/ProjectARVRPro/ARVRWindow.xaml.cs","Projects/ProjectARVRPro/FlowRuntimeEstimateCache.cs","Projects/ProjectARVRPro/ResultImagePresentation.cs","Projects/ProjectARVRPro/ProjectARVRReuslt.cs","Projects/ProjectARVRPro/ViewResultManager.cs","Projects/ProjectARVRPro/Services/SocketControl.cs","Projects/ProjectARVRPro/Services/SwitchGroupSocket.cs","Projects/ProjectARVRPro/Services/RunAllSocket.cs","Projects/ProjectARVRPro/SocketRelay/","Projects/ProjectARVRPro/CycleTimeStatisticsWindow.xaml","Projects/ProjectARVRPro/CycleTimeStatisticsWindow.xaml.cs","Projects/ProjectARVRPro/ResultStatisticsTheme.xaml","Projects/ProjectARVRPro/ResultStatistics.cs","Projects/ProjectARVRPro/ResultTimeline.cs","Projects/ProjectARVRPro/ProjectARVRProConfig.cs"]
test_paths: ["Test/ProjectARVRPro.Tests/OfflineDataSourceTests.cs","Test/ProjectARVRPro.Tests/ProjectARVRPro.Tests.csproj","Test/ProjectARVRPro.Tests/ResultImagePresentationTests.cs","Test/ProjectARVRPro.Tests/ResultJsonPayloadStorageTests.cs","Test/ProjectARVRPro.Tests/ResultStatisticsTests.cs","Test/ProjectARVRPro.Tests/FlowPhaseTimingPersistenceTests.cs","Test/ProjectARVRPro.Tests/FlowRuntimeEstimateCacheTests.cs"]
related: ["projects.index","projects.arvr-pro-demo","projects.arvr-pro-protocol","projects.arvr-pro-processes","projects.arvr-pro-demura","projects.capabilities"]
---

# ProjectARVRPro

`Projects/ProjectARVRPro/` 是当前主力 AR/VR 专业测试项目包，运行时以 `ProjectARVRPro.dll` 加载。维护时优先看流程组、Socket 自动化、切图、Recipe 和输出格式。

排查整组取图、切图和流程耗时时，可从反馈窗口收集本地运行数据库。项目加载后自动提供默认选中的“ARVRPro 测试与阶段耗时记录”，按 `ViewResultManager.SqliteDbPath` 读取结果库，默认最近 7 天，保留阶段时间、关联整组结果与完整压缩 JSON；同时默认收集“ARVRPro 流程配置”，按 `ProcessManager.GroupPersistFilePath` 读取当前已保存的 `ProjectARVRProProcessGroups.json`，经脱敏后保留流程组、切图等待、相机覆盖参数与 Recipe，不受 7 天限制。与流程节点、MQTT 及 Socket 记录的打包、时间筛选和失败说明统一见[反馈诊断](../ui-components/ColorVision.UI.Desktop.md)。导出不会触发检测或修改源数据库、源配置；现场性能结论仍需基于实际记录分析。

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
| AOI 流程卡住 | 主 Socket、`SocketRelay`、`AOITestSwitchImageComplete` |
| Demura 烧录失败 | [PG 连接、GECS 指令及烧录诊断](./project-arvr-pro-demura.md) |
| 重启后配置丢失 | `%APPDATA%/ColorVision/Config/ProjectARVRProProcessGroups.json` 和 Recipe 配置；升级时核对旧共享文件迁移日志 |

结果列表工具栏的“缓存管理”打开 Engine 的进程级“本地缓存管理”，以两个 Tab 查看校正缓存与默认开启的单槽位 CVRAW 文件缓存，“释放全部”一并释放。图片仍通过 `ImageView.OpenImage` → `CVRawOpen` 打开，由 FileIO 校验路径、长度和修改时间后复用缓存，并沿用显示位图的复用逻辑。旁边的“释放截图缓存”只释放本窗口截图导出缓冲，与文件槽位用途不同。

## 项目边界和版本

| 项目 | 通信方式 | 流程组织 | 典型风险 |
| --- | --- | --- | --- |
| `ProjectARVRPro` | JSON `EventName` | `ProcessGroup` + `ProcessMeta` | 切图、Legacy 输出、SocketRelay |
| `ProjectLUX` | 文本命令 | 流程组 + `SocketCode` | 文本返码、客户命令映射 |

客户项目判定逻辑应留在 `Projects/ProjectARVRPro/Process/` 和 Recipe 体系里，不要回写到 Engine 通用模板或 UI 基础库。手工维护项目 `ProjectARVRPro.csproj` 的 `VersionPrefix`；打包器从主 DLL 的文件版本生成 manifest 版本，不手工同步 `manifest.json`。最低宿主要求读取 manifest 的 `requires`。

## 主链路

外部系统发送 `ProjectARVRInit`，或用户在窗口输入 SN 后，`ARVRWindow` 选择当前 `ProcessGroup` 并找到下一个启用的 `ProcessMeta`。步骤启用 `PictureSwitchConfig` 时先切图，再运行绑定的 FlowEngine 模板。该次启动选定的处理实例通过 `IProcess.Execute(ctx)` 读取 Engine 结果并应用自身 Recipe，最后写入 `ObjectiveTestResult`，按配置保存 SQLite、CSV、Legacy CSV、客户 XLSX，并通过 Socket 返回下一步或最终结果。

## 界面主题

主窗口测试工具栏下方使用 `ColorVision.UI.Controls.FlowExecutionStatus`，与 KB、LUX 共用紧凑执行状态栏：普通提示保持一行，运行时显示当前节点，运行中和结束后的耗时均显示整数毫秒（ms）；长错误最多占两行，窄窗口优先保留提示并隐藏辅助耗时。“详情”浮层可查看、选择和复制完整提示、流程名、节点与精确耗时，按 Esc 关闭，不挤压结果区域。上次耗时和预计剩余时间只在详情内显示，单位同为 ms；预计剩余时间仅作为历史参考。状态图标同时配中文文字，错误保留至后续执行更新，已排队的定时刷新不得覆盖最终状态。“流程执行完成”只表示 Flow 完成，不代替检测结果的 PASS/FAIL 判定。

主界面分隔线、结果明细表格、流程配置提示和串口/Socket 中转日志界面使用 [ColorVision.Themes](../ui-components/ColorVision.Themes.md) 的动态画刷。切换黑白主题时，普通背景、说明文字和按钮状态随主题更新；断开连接后的状态文字也保留动态资源引用。结果明细的隔行背景在行样式中设置，避免覆盖选中与悬停高亮。明细选中行的结果文字跟随行前景色，未选中时保留 PASS/FAIL 业务颜色；连接状态和图像标记保留各自的业务颜色。

## 长期运行与结果视图刷新

所有继承 `ViewConfigBase` 的读图与结果视图都保留 `AutoRefreshView` 开关，便于调试时自动打开最新图像或绘制结果。ARVRPro 主窗口结果列表工具栏提供“视图刷新管理”：窗口从已注册配置和当前设备控制项动态发现相机、算法、校正、光谱、SMU、第三方算法等视图，不把范围写死为三个类型；同类配置存在多个设备实例时合并为一项并列出实际影响数量和名称。

窗口中的逐项开关和“一键关闭全部刷新”只修改草稿，点击“确定并保存”后才统一应用并通过 `ConfigService.SaveConfigs` 写入配置文件，下次启动继续保持；取消不改变运行配置。当前已加载且仍开启自动刷新的项目使用橙色行背景与汇总警示，主窗口入口同时显示开启项数量；未加载的配置仍可预先关闭并持久化，但不计入运行时警示。保存失败时必须恢复进入窗口前的运行值并明确提示，不得出现本次运行已经关闭而文件未保存的半应用状态。关闭自动刷新不关闭视图、不修改查询数量，也不禁止之后重新开启调试功能。

结果区和各设备结果列表的高度仍由分隔条调整并写回配置，供下次启动恢复；这类运行时布局值不再显示在属性编辑器中，避免用户输入与界面实际布局互相覆盖。

`ProjectARVRProConfig.StepIndex` 与 `SN` 由主窗口进度条和 SN 输入框绑定并在运行流程中更新，因此也不在属性编辑器中显示；隐藏只影响设置窗口，绑定、属性通知和现有序列化兼容规则保持不变。

主界面结果工具栏不再保留仅用于定位数据库文件的旧 `SlectDb` 入口；数据库位置与维护统一从“数据清理”进入，反馈包或其它现场数据库继续通过“结果统计”的只读“打开现场数据”入口查看。

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
| `SocketRelay/` | AOI Flow 与外部 Client 的中转层 |
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
| `AOITestSwitchImageComplete` | AOI 切图完成信号，经 Relay 回给 Flow |

详细请求与响应、索引约定、状态码、并发限制及 AOI 中转见 [TCP 通讯协议](./project-arvr-pro-protocol.md)，可运行的客户端示例见 [Integration Demo](./project-arvr-pro-integration-demo.md)。

## 输出和兼容

结果输出由 `ViewResultManager.Config` 控制，覆盖 SQLite、标准 CSV、Legacy CSV、客户 XLSX 和 Socket `ProjectARVRResult.Data`。`UseLegacyARVROutput` 会影响 CSV 和 Socket `Data`，改字段前先确认客户解析程序使用新版还是旧版。

W255 保留原有 `ColorUniformity`（所有 POI 两两之间的最大 Δu′v′），并在其后输出独立的 `ColorCenterRmsToD65`。新指标对 W255 已应用色度修正的有效 POI 直接计算相对 D65 的等权 RMS Δu′v′，不匹配或读取 `PoiAnalysis` 中的均匀性结果。计算值再应用自身 Recipe 的 K/B 修正与 Min/Max 限值，默认范围为 `0–0.02`，并参与 W255 PASS/FAIL 判定；旧配置没有该字段时使用此默认值。公式、D65 常量和 POI 样本边界见 [CVCIE POI 结果数值](../engine-components/cvcie-results.md#色彩中心与-d65-rms)。

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

内置结果解析器通过单次 `IProcessExecutionContext` 复用当前 batch 已读取的 `MeasureResultImgModel` 列表，并在 `Execute` 返回、最终 `FileName` 确定后从 `ImgFrameInfo` 写入 `ImageWidth` / `ImageHeight`。新结果已有两个正数尺寸时，`ViewResultManager.Save` 不再为读取尺寸重复查询 MySQL；尺寸缺失、无效、多尺寸无法唯一确定，或外部解析器既未写入有效尺寸也未使用该上下文缓存时，仍保留原数据库查询作为兼容回退。回退失败不会阻止结果保存，尺寸继续保持可空，也不需要现场数据库迁移。

验证入口：`Test/ProjectARVRPro.Tests/ResultImagePresentationTests.cs` 覆盖候选顺序、缺失/重复路径、首图加载失败后继续、标记图不重复绘制，以及尺寸缓存、最终路径匹配和兼容回退短路契约；`ResultJsonPayloadStorageTests.cs` 覆盖结果持久化相关兼容。自动测试不等于已验证现场历史文件仍存在，现场排查还须只读核对记录路径与文件可读性。

## 结果统计与查询记忆

### 启动准备与预计时间

自动切图确认、手动单步和 RunAll 启动均不为“上次执行 / 预计剩余时间”查询本地历史数据库。`FlowRuntimeEstimateCache` 只保存本窗口已经完成且耗时为正的流程，按模板名称匹配，并核对启动时捕获的模板内容；模板内容变化、名称变化或新窗口没有命中时，先只显示已经执行时间。该缓存只影响预计时间显示，不参与 PG 稳定等待、采集、超时、PASS/FAIL 或历史结果保存。

流程图仍通过原 `Refresh` 路径加载，保留设备更新、节点事件解绑/挂接及已有图内容命中规则，不跨模板复用可变运行节点。`FlowRuntimeEstimateCacheTests` 覆盖首次无缓存、完成后更新、内容/名称/窗口隔离及无效耗时；实际 CT 收益还须使用相同方向、同一组模板的现场日志验证，不能从缓存命中推导固定节省秒数。

### 统计口径与阶段归因

`CycleTimeStatisticsWindow` 默认提供首页指标与 CT 趋势、批次记录及流程查询；顶部右侧“统计设置”可启用 L/R 联合统计，选择会保存到窗口专属的 `CycleTimeStatisticsWindowConfig` 并在重新打开应用后恢复，不写入 `ProjectARVRProConfig`。启用后增加“全批次记录”页面并在首页追加一行紧凑的全批次指标。该窗口配置同时保存主统计窗口的位置和尺寸；界面使用与启动恢复窗口相同的主题调色板、标题层级和弱边框圆角卡片，样式在项目包本地的 `ResultStatisticsTheme.xaml` 中定义，不依赖宿主 `ColorVision` 程序集的资源。筛选区在窗口变窄时换行，表格保留分页、虚拟化、右键操作和详情入口。

查看现场反馈时，在结果统计顶部选择“打开现场数据”（反馈 ZIP 或 `ProjectARVRPro.db`），也可用“打开资料文件夹”选择数据库所在目录或包含 `Database` 的上级目录。`ArvrOfflineDataSource` 在 `%LOCALAPPDATA%/ColorVision/OfflineData/<独立标识>/` 准备独立副本，新窗口标注来源和“只读”，默认显示该资料最新记录所在日期。ZIP 只提取结果库与同目录的 `FlowNodeRecords.db`、`SocketMessages.db`、`MsgRecords.db` 及导出说明；文件夹/数据库导入使用 SQLite backup 包含已提交的 WAL 内容。它不覆盖本机运行库、不改全局数据库路径、不启动写入队列，也不共用本机统计窗口的查询状态。副本保留在本地供排查，位置可从来源提示查看。

离线窗口复用整轮结果、PG 明细、时间轴与导出。选中一轮后可打开“本轮相关消息”；Socket/MQTT 按本轮前后各 1 秒筛选候选消息，必须结合 SN、MsgID 和连接地址核对，不能仅凭时间邻近认定业务归属。PG 明细右键的执行分析先在同一份节点库按 `BatchId` 查找，再核对项目 SN 与 Flow 的“SN＋启动时间”，要求唯一运行标识；无匹配或多匹配明确提示，不回退到本机 MySQL。节点历史、消息正文和流程切换仍限定同一数据源；离线窗口隐藏清理操作，禁止批次 MySQL 查询及打开现场绝对图片路径。各库是独立导出快照，缺库/缺记录不表示现场没有执行；反馈不含原图，不能承诺查看图像。

离线读取兼容旧 TEXT 与 gzip 正文，缺失的可选阶段字段显示为不可用，必要表或关联字段缺失时拒绝加载并列出原因。只读库不会执行 CodeFirst、补列或建索引。`OfflineDataSourceTests` 使用临时旧结构、同 ID 的不同来源、WAL 和压缩正文验证数据隔离及不改源库；设置 `COLORVISION_OFFLINE_FEEDBACK_ROOT` 可对指定反馈目录执行整轮到消息的验证，`COLORVISION_OFFLINE_PREVIEW_DIR` 可输出深浅主题预览。现场反馈验证不替代完整宿主安装验收。

本机运行库在 `ViewResultManager` 打开时为结果表和统计表补齐查询索引；旧库保留原记录并自动补建，包括按 `BatchId` 查找流程结果的索引。首次为较大的旧库建索引可能增加打开耗时，后续打开重复执行不会重建已有索引。离线资料仍保持只读。

批次记录优先显示 SN、整组 CT、流程运行时间、结束时间和流程数；流程数显示为纯数字，测试次数放在末列。选中批次后，右侧下方时间轴以整组开始和最终化时间为同一横轴，按流程显示 PG 应答、本地切图与稳定等待、预处理、流程执行、执行后处理与保存，以及无法归因的间隔。鼠标悬停阶段条可查看起止时间和耗时。

联合统计不新增或回写结果库字段，只在查询时解析最终后缀为 `_L_HHmmss` / `_R_HHmmss` 的 SN。只有全局结果记录中相邻、顺序为 L→R、去掉侧别与时间后主体 SN 相同且均已最终化的两条记录才组成一个全批次；插入其它记录、侧别倒序、主体不同或只存在单侧时均不配对，原记录仍完整保留在“批次记录”。全批次结果仅在 L 和 R 都 PASS 时为 PASS，完成时间按 R 最终化时间归入所选日/周/月；跨统计边界时会读取范围前紧邻的一条记录用于确认当天第一条 R 的 L 配对，但不会把范围外完成的 R 计入当前范围。

全批次 CT 从 L 收到 Init 并创建整组记录的时间开始，到 R 结果最终化结束，包含 L 单侧 CT、L 完成到 R Init 的等待以及 R 单侧 CT。全批次详情将 L、橙色的 L→R 等待和 R 映射到同一横轴，并列出 L Init、L 完成、R Init、R 完成四个里程碑；首页同时保留单侧批次指标，联合统计行显示全批次数、PASS/FAIL、成功率、平均全批次 CT、平均 L→R 等待和今日全批次，趋势在开关启用时改用全批次口径，避免把约 20–24 秒单侧基线与左右合计时间混为一谈。

整组 CT 是 `ObjectiveTestResult.SessionStartTime` 到结果记录最终化的墙钟时间。统计界面中的“PG→执行结束”按每步 `SwitchPG` 发送到该步流程执行结束累计，内部包含 PG 应答、启动准备和流程执行；它与单独显示的绿色执行时间是包含关系，不能再次相加。外部逐步切图模式会在解析成功后、保存前准备下一项 PG，因此当前结果保存可与下一项 PG 等待重叠。整组 CT 减去这些 PG 周期后，只剩未被 PG 周期覆盖的结果处理、保存与无法归因空档，不能再当作完整保存耗时。后台结果图导出可与后续流程重叠，也不能仅凭单张导出耗时推断其占用了同等 CT。

新记录在 `ProjectARVRReuslt` 的可空阶段时间字段中保存精确边界；本机运行库通过现有 CodeFirst 初始化自动补列，离线资料只读映射可用字段：

| 时间段 | 字段边界 | 时间轴含义 |
| --- | --- | --- |
| PG 应答（PG 周期子段） | `SwitchRequestedAt` → `SwitchAcknowledgedAt` | 发出 `SwitchPG` 到 ARVR 在 UI 线程处理 `SwitchPGCompleted`；包含接收后的存储、派发和 UI 调度，不是物理 PG 单独计时；RunAll 可为空 |
| 流程准备 | `SwitchAcknowledgedAt` → `PictureSwitchStartedAt` | 确认后选择/刷新模板、捕获运行上下文和读取运行内预计时间缓存；缺任一边界时保持未归因 |
| 本地切图/稳定 | `PictureSwitchStartedAt` → `PictureSwitchCompletedAt` | `PictureSwitchService` 调用和配置的稳定等待 |
| 预处理/准备 | `PictureSwitchCompletedAt` → `PreProcessingCompletedAt` | Flow 启动前预处理；边界间没有记录的部分仍显示为未归因 |
| 流程执行 | `FlowStartedAt` → `FlowCompletedAt` | 与流程运行 stopwatch 对应的主执行段 |
| 执行后处理/保存 | `FlowCompletedAt` → `ResultProcessingCompletedAt` | 流程收尾、客户结果读取与解析、结果记录保存；旧日志不能把整段等同于纯解析耗时 |

每个成功流程还会输出一条 `ARVRFlowPhaseTiming` 结构化 INFO 日志，并携带 `SN`、`Model` 和 `BatchId`。其中 `SwitchWaitMs`、`SwitchPreparationMs`、`PictureSwitchMs`、`PreProcessingMs` 和 `FlowMs` 对应启动前与执行阶段；`FlowFinalizeMs`、`BatchLookupMs`、`ProcessExecuteMs`、`ViewResultSaveMs`、`ObjectiveResultSaveMs`、`LinkSaveMs` 和 `ResultProcessingTimestampPersistMs` 用于继续拆分流程结束后的约束路径。`ResultProcessingTimestampPersisted` 用于确认阶段终点是否写回 SQLite，`ImageExportIncludedInCt` 明确后台图像导出不属于该阶段的 CT 归因。后续反馈诊断应优先按同一 `SN + Model + BatchId` 汇总这些字段，而不是从相邻日志行估算。

`ResultImageDimensionsFromProcessCache` 表示本次结果保存前已从解析阶段的 batch 图像查询中解析出有效宽高。正常内置流程该字段为 `true` 时，`ViewResultSaveMs` 不再包含第二次尺寸查询；若为 `false`，保存层可能因兼容回退仍访问 MySQL，应结合 `ViewResultSaveMs` 和尺寸数据继续诊断。

启动软件段另有以下日志字段，单位为保留三位小数的毫秒，仅增加日志、不增加结果库列：

| 字段 | 范围与相加规则 |
| --- | --- |
| `StartupWorkMs` | 进入本次单步启动方法或 RunAll 迭代，到本地切图开始；单步不含进入方法前的步骤选择，因此不强求等于 `SwitchPreparationMs` |
| `RuntimeEstimateCacheHit` / `RuntimeEstimateLookupMs` | 显示用预计耗时是否命中，以及捕获模板身份/查运行内缓存耗时；不访问历史库 |
| `RefreshServicesMs` | 服务查询请求的发送调用，不等于服务应答耗时 |
| `RefreshDetachNodesMs` | 解绑旧节点显示与诊断事件 |
| `RefreshLoadGraphMs` | 读取模板内容、加载流程图和更新设备信息 |
| `RefreshAttachNodesMs` | 枚举新节点并挂接显示、诊断事件 |
| `RefreshTotalMs` | 上述四项 Refresh 子段之和；不能再与其子段相加 |
| `StartupOtherMs` | `StartupWorkMs - RefreshTotalMs - RuntimeEstimateLookupMs` 的非负余项；包括本轮上下文准备等，不能全部解释成线程等待 |

原始回包到业务派发应结合宿主的 [Socket 计时](../ui-components/ColorVision.SocketProtocol.md#回包派发与界面刷新计时)，不能把消息列表后台排队时间重复计入 PG 或 CT。

旧记录没有上述阶段字段时，时间轴只用 `CreateTime - RunTime` 推算流程执行段，并把剩余部分明确显示为“未归因间隔”；旧数据不能事后可靠拆分切图、预处理和结果保存。任何缺失、逆序或超出整组范围的阶段边界都不能被强行归类。

- 软件每次重新启动后，三个页面默认“按天／今天”，筛选文本和结果条件回到初始状态。同一次软件运行中关闭、重新打开统计窗口，保留当前标签、周期、日期和筛选条件。
- `ProjectARVRProConfig.ResultStatisticsWindowState` 仅作为运行内状态，标注 `[JsonIgnore]`；旧配置 JSON 中的同名字段不再恢复，关闭统计窗口也不会为查询条件写入配置。此规则只涉及查询条件，不会删除或截断历史结果数据。
- 手动切换仍支持按天、按周、按月和全部。按天使用所选日期的 `00:00` 到次日 `00:00`，按周使用周一到下周一，按月使用月初到下月初，统一为包含起点、不含终点的区间；不是滚动 24 小时或最近 7 天。
- 各页的“今天／本周／本月”按钮按当前周期回到本周期并刷新查询；切到“全部”时隐藏日期导航，按钮显示“全部”。已有 SN、流程名和结果筛选不会被快捷返回清空，需要清空时使用“重置”。
- 首页与批次记录按整组结束时间统计，流程查询按流程 `CreateTime` 统计，不能把跨午夜完成的整组记录误算到开始日。首页“今日产量”和“本小时产量”是所选查询范围中落入当前日期/小时的子集；查看不包含今天的历史范围时这两个值为零。

验证入口：`ResultStatisticsTests.cs` 覆盖自然周期、日期前后切换、分页、配置 JSON 忽略与运行内状态，以及三个查询在午夜的包含/排除边界。测试使用临时 SQLite；界面布局另需检查浅色/深色、最小窗口宽度、日期弹出日历和筛选操作。只加载 XAML 的合成预览不能替代真实窗口的查询与重开验证。

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
| AOI Relay | Flow 请求、外部确认、Relay 转发三段都可追踪 |
| 交付包 | `.cvxp` 内含 DLL、manifest、README、CHANGELOG |

## 本地构建与测试

下列命令编译和运行本地测试，会写入本地构建/测试产物，不上传包。项目版本读取 `ProjectARVRPro.csproj` 的 `VersionPrefix`，与主程序版本独立。

```powershell
dotnet build Projects/ProjectARVRPro/ProjectARVRPro.csproj -c Release -p:Platform=x64
dotnet test Test/ProjectARVRPro.Tests/ProjectARVRPro.Tests.csproj -c Release -p:Platform=x64
```

## 打包上传（需明确发布授权）

只有明确要求发布 ProjectARVRPro 时才运行以下命令。wrapper 会重新构建、生成并上传 `.cvxp`，随后清理本地包；不支持 `--no-upload`。打包器由主 DLL 的 `FileVersion` 同步 manifest 版本，不要另行手工同步 manifest。发布完成还需核对远端元数据和可下载包，不能以本地 build 成功代替。

```powershell
.\Scripts\package_project.bat ProjectARVRPro
```

---
knowledge_id: "ui.storage-maintenance"
knowledge_type: "topic"
status: "current"
summary: "存储分类占用与模块扩展接口、独立文件确认清理，以及配置页的恢复点和选择性下次启动重置。"
aliases: ["存储", "配置", "存储空间", "分类占用", "扫描空间", "清理选中项", "配置恢复点", "安排重置", "取消重置", "清理日志", "运行垃圾", "工作垃圾", "清理缓存", "重置设置", "StorageMaintenanceCatalog", "StorageMaintenanceControl", "StorageMaintenanceViewModel", "MaintenanceFileCleanup", "ConfigMaintenanceResetService", "ThumbnailCacheMaintenanceSnapshot", "HasPackageMaintenanceProtection", "HasActiveUpdateForCleanup", "文件缓存管理", "内存缓存管理", "FileManagementWindow", "IFileMaintenanceProvider", "IStorageUsageProvider", "增量扫描"]
code_paths: ["ColorVision/Settings/Maintenance", "ColorVision/App.xaml.cs", "ColorVision/Update/CombinedUpdateCoordinator.cs", "UI/ColorVision.UI/Maintenance", "UI/ColorVision.UI/ConfigMaintenanceResetService.cs", "UI/ColorVision.UI/ConfigHandler.cs", "UI/ColorVision.UI/Update/ExitUpdateHandoff.cs", "UI/ColorVision.UI.Desktop/Download/Aria2cDownloadManager.cs", "UI/ColorVision.UI.Desktop/Download/Infrastructure/DownloadTaskStore.cs", "UI/ColorVision.ImageTools/MultiImageViewer/ThumbnailCacheManager.cs", "UI/ColorVision.ImageTools/MultiImageViewer/ThumbnailCacheManager.Maintenance.cs", "Engine/ColorVision.Engine/Services/Storage", "Engine/ColorVision.Engine/FlowProcessing/PreProcess"]
test_paths: ["Test/ColorVision.UI.Tests/MaintenanceFileCleanupTests.cs","Test/ColorVision.UI.Tests/StorageMaintenanceCatalogTests.cs","Test/ColorVision.UI.Tests/StorageMaintenanceTests.cs","Test/ColorVision.UI.Tests/StorageOverviewTests.cs","Test/ColorVision.UI.Tests/StorageUsageScannerTests.cs","Test/ColorVision.UI.Tests/EngineStorageUsageProviderTests.cs","Test/ColorVision.UI.Tests/ThumbnailCacheMaintenanceTests.cs"]
related: ["ui.settings", "ui.configuration", "operations.logs", "delivery.update", "engine.database-maintenance"]
---

# 存储清理与选择性设置重置

设置“存储”展示本机已登记来源的分类占用、分段比例条和来源位置，并提供文件缓存、内存缓存、历史数据与程序存档的管理入口。配置恢复点与选择性重置位于“配置”，崩溃转储设置也归入该页；Dump 文件本身仍可由存储清理来源管理。页面标题和内容使用设置宿主的同一左边界，自定义页不重复生成总标题。

`ColorVision/Settings/Maintenance/` 提供独立的“文件缓存管理”窗口，通过主窗口底部的文件夹图标或“存储”的同名入口打开。内存释放仍由原有独立窗口处理，不参与文件清理。普通清理仅处理明确归属本软件、可删除或可重建的文件；重置需要管理员确认，下次启动生效。

页面通过 `StorageMaintenanceSettingsProvider` 的 `IConfigSettingProvider` 元数据发现，展示与通用设置窗口的关系见[设置窗口契约](./settings.md)。文件类别通过 `IFileMaintenanceProvider` 的程序集发现注册。主程序 `BuiltInFileMaintenanceProvider` 接入现有五类，`StorageMaintenanceCatalog` 保持文件白名单；客户包可以提供独立类别，主程序不依赖客户包。每个类别负责扫描快照、保留期、保护提示和确认清理，准备或扫描失败仅使所属类别不可清理；重复类别标识不覆盖已有 owner。Windows 文件执行器归 `UI/ColorVision.UI/Maintenance/`；缩略图数据库仍由自己的 owner 管理，不能交给通用文件删除器处理。

分类占用由独立的 `IStorageUsageProvider` 提供：`PrepareStorageScan` 只捕获模块当前路径和配置，`GetStorageCategories` 提供类别，后台 `StorageUsageCategory.Scan` 返回文件大小、数量、文件清单和扫描问题。`StorageUsageRule` 是只读统计白名单，不赋予删除权限；统计不创建不存在的目录，也不为了发现来源而连接设备或初始化业务数据库。重复来源按规范化文件路径去重，访问失败与取消不能当成完整的零占用结果。

占用统计包含来源实际持有的文件，与清理窗口按保留期、保护状态筛出的可清理候选分别计算。顶部总量是已登记且成功扫描的来源，不是系统磁盘剩余空间或整个软件安装占用。类别可通过 `ManagementCategoryId` 指向所属模块的清理类别，通过可选 `OpenManager` 提供模块管理入口，或仅提供来源位置。`OpenManager` 的实际操作授权由所属模块在执行前检查，统计发现不会自动授予模块管理权限。受保护的相机配置、校正和授权文件可以计入占用，但不会仅因出现在比例条中就加入普通缓存清理。

## 清理范围与默认保留

| 类别 | 当前白名单 | 默认策略与保护 |
| --- | --- | --- |
| 历史日志 `logs` | 当前 log4net 仓库中所有 `FileAppender` 的实际落点；只有 `RollingFileAppender` 对应的日期/序号归档名进入清理规则 | 保留最近 30 天，默认勾选；当前活动日志始终保留，不把同目录所有文本文件当日志 |
| 运行临时文件 `temp` | `%TEMP%` 顶层 `ColorVision_Diagnostics_*.zip`、`ColorVision_Screenshot_*.png`；`ColorVisionUpdate-<N格式GUID>`、`ColorVisionPluginsUpdate-<N格式GUID>`、`ColorVisionPackageVersion-<N格式GUID>` 目录内文件 | 保留最近 7 天，默认勾选；反馈窗口打开时保留反馈附件；进行中或待执行的更新保护更新暂存文件 |
| 缩略图 `thumbnails` | `ThumbnailCacheManager.SqliteDbPath` 指向的既有缩略图数据库 | 默认勾选；只经 owner 清缓存记录和整理空间，不直接删除数据库、WAL 或 SHM 文件 |
| CIE 背景 `cie-cache` | `%LocalAppData%\ColorVision\ImageEditor\CieCache` 中三个 `CieDiagramKind` 对应、版本号有效的背景 PNG | 默认勾选；可按需重新生成，不含用户图像 |
| 安装包缓存 `packages` | `Environments.DirPackageCache` 下 `Application/Full`、`Application/Incremental`、`Plugins`、`Tools` 的顶层已知包扩展 | 保留最近 30 天，默认不勾选；保护待安装更新、下载任务和续传状态，不递归进入 `Recovery` |
| 相机资源 `Engine.CameraResources` | 已加载物理相机的 `FileBasePath/<Code>/cfg` | 只读占用和文件清单；不注册清理类别，模块管理入口仍检查 `physical-camera.manage` |
| 相机图像 `Engine.CameraImages`、算法图像 `Engine.AlgorithmImages` | 已加载对应设备的 `DataBasePath/<Code>/Data` 专用目录 | 独立业务文件清理类别，保留最近 30 天且默认不勾选；流程、相机预览/采集、算法忙碌期间保护，不将整个数据根目录纳入扫描 |
| 预处理目录 `Engine.PreProcessFiles` | 已配置 `FolderSizePreProcessor` 的 `FolderPaths`、扩展名和是否递归选择 | 独立类别，保留最近 30 天且默认不勾选；目录覆盖到相机资源时仍保护资源文件。自定义 `IStorageUsageContributor` 提供的路径只参与占用统计，不自动获得清理权限 |

加载 ProjectARVRPro 后，“现场数据副本”由该包的 `OfflineFileMaintenanceProvider` 注册，默认不勾选，保留 7 天。其范围和应用会话保护见[现场数据离线查看](../projects/project-arvr-pro.md)。未知来源的临时解压目录不会仅凭名称自动纳入清理。

“崩溃转储”由 `UI/ColorVision.UI.Desktop/Diagnostics/CrashDumpFileMaintenanceProvider.cs` 注册，读取当前程序的 WER 保存目录，只匹配该程序命名的 `.dmp`，共享目录中其他程序的转储不纳入清理。“服务日志”由 `Engine/ColorVision.Engine/Services/ServiceLogFileMaintenanceProvider.cs` 注册，读取 x64、开发、注册中心及存档服务的实际 `log` 目录，同时提供 `%ProgramData%\ColorVision\ServiceHost` 位置；保留各日志子目录最新文件、当天修改的日志及 `ColorVisionServiceHost.log` 当前记录。两类默认保留 30 天且不勾选，复用确认清单与文件身份复核，不启停服务或自动提权。反馈打包中断留下的 `ColorVision_ProcessDump_*.dmp/.txt` 由工作临时文件白名单管理，反馈窗口仍打开时保留。

有保留期的类别统一提供 7、14、30、90、180 天及“全部清理”（持久化为 0 天）。全部清理取消文件和已结束会话目录的等待期限，仍遵守归属、运行状态、占用和确认清单保护；不会扩张清理范围。修改一类的保留期会使该类旧扫描失效，必须重新扫描。保留期以文件 UTC 修改时间判断；更新 GUID 暂存目录的创建时间也必须早于所选保留期（默认 7 天），防止刚解压的文件继承旧修改时间而被误认成历史垃圾。目录不存在时不创建。

每个类别通过 `FileMaintenanceCategory.SourceDirectories` 提供“打开文件位置”；多个来源显示目录菜单，没有过期候选也能打开已存在目录。文件清单中可对选中的文件使用同名右键操作，在资源管理器中定位该文件。打开位置不执行扫描、清理或创建目录。

通用缓存白名单不包括原始检测图、结果文件、流程模板、校准文件、设备配置、授权、数据库连接、用户下载目录、Copilot 会话、程序快照和恢复区。相机、算法和预处理输出只可通过上表独立业务来源管理，不因后缀类似缓存就默认勾选。未登记的手工导出、默认桌面侧栏保存目录和其它共享目录不会整目录扫描；不能根据 `.tmp`、`.zip`、`.db` 等扩展名扩展到整个磁盘或临时目录。

`EngineStorageUsageProvider` 读取现有设备和物理相机实例的路径，不为了显示存储页初始化服务。预处理管理器已加载时读取当前 action 配置；未加载时只读取现有持久化 JSON，不构造预处理器。新的预处理保存功能可在其已配置 action 上实现 `IStorageUsageContributor.GetStorageUsageRules()`，提供明确目录、文件名模式、扩展名筛选和递归范围；模块若还需删除，应独立提供 `IFileMaintenanceProvider` 的确认清理契约，不能把统计规则直接当作删除授权。

数据库管理和程序备份是“存储”页面上的独立入口，不参与“清理选中项”。这些入口以及“配置”页的配置备份、打开恢复点目录、安排和取消选择性重置，通过 `MaintenanceAccess` 以各自的 `maintenance.*` 动作和 `application` 资源请求授权；默认策略要求应用管理员。执行前重新检查，确认重置之后也再次检查，不只依据页面或按钮打开时的权限。数据库入口还要求当前流程状态可用、未运行；后续行为仍由[数据库清理窗口](../engine-components/database-maintenance.md)负责。打开程序备份也不等于删除快照或重建默认快照，应用内授权不代替 Windows 文件权限或后续维护步骤的检查。

## 扫描、确认与执行

`FileManagementWindow` 首次加载自动扫描，再次通过入口激活也刷新；同一进程重新打开时复用上次快照。`StorageMaintenanceViewModel` 使用共享操作门禁，避免多个窗口同时执行扫描或清理。扫描在后台逐类别执行，每完成一类即更新界面，未勾选的类别也显示扫描结果。普通文件扫描仍枚举白名单目录以发现新增、修改和删除；大小、创建时间及修改时间未变的候选复用既有文件身份，变化文件重新读取身份，不把整盘扫描或文件内容哈希称为增量。保留期、保护回调每次重新评估，过期判断不依赖旧快照。勾选决定顶部合计和“清理选中项”的范围；行内“清理”只处理该行，不要求先勾选。普通文件详情显示绝对路径、字节数和 UTC 修改时间。统计表示本次合格候选，不是整个目录占用，也不保证全部字节最终可释放。

普通文件按以下边界执行：

1. `MaintenanceFileCleanup.Scan` 规范化绝对根路径、文件名模式和保留期，生成不可变文件清单。重复候选去重；扫描取消时当前类不提供可执行的完整扫描。
2. 页面确认选中类别的候选数量和大小。没有有效扫描、保留期已变化、数量为零或正在执行时，不能直接清理该类。
3. `Cleanup` 仅处理已确认清单，不重新扩张目录范围，不删除扫描后新出现的文件。
4. 每个文件执行前重新校验路径归属、文件身份、创建/修改时间、长度、保留期及保护回调。扫描后被替换、修改或开始使用的文件跳过，要求重新扫描。
5. Windows 目录句柄从路径祖先开始固定，文件与任意祖先的链接/重解析点均被拒绝；删除使用同一个独占校验句柄，不在检查后按路径重新打开删除。占用、权限和其它失败分别进入结果，不自动提升权限或结束进程。

文件删除是永久删除，不进入回收站。执行器只删文件，保留根目录及清空后的目录，不承诺清理所有空目录。成功返回的统计按已完成删除累计；跳过和失败不计为释放空间。

点击“取消”会请求停止后续工作，已经完成的删除不会撤销。设置窗口在当前维护页忙碌时阻止直接关闭并提示先取消或等待；独立文件窗口忙碌时同样阻止关闭，并允许请求取消。取消不能强制中断每个正在执行的同步操作，特别是缩略图 owner 的一次数据库事务/空间整理；界面取消不是事务回滚。

## 更新、下载与反馈保护

`CombinedUpdateCoordinator.HasPackageMaintenanceProtection` 仅读取待更新计划、正在运行的预下载任务和更新操作门禁。只要存在这些状态，安装包整类保留，避免“已下载完成”被误认成“已经安装、可以删除”。暂存文件也受同一更新状态保护。

`ExitUpdateHandoff.HasActiveUpdateForCleanup` 检查 `%LocalAppData%\ColorVision\UpdateState` 各安装实例的 `update.pending`，保护准备阶段或仍存活的外部更新进程。该入口不会清理失效标记、写重开请求或启动更新；格式异常、无法读取或无法确认进程状态时保守保护。它不同于可能清除过期 marker 的启动接管检查。完整更新机制见[自动更新](../../02-developer-guide/deployment/auto-update.md)。

`Aria2cDownloadManager.IsPathProtectedFromCleanup` 不调用 `GetInstance()`，不会为了扫描而初始化下载器、创建其任务库或启动 aria2。判断包括已有实例的并发活动集合、`.aria2` 续传文件，以及 `DownloadTaskStore` 对既有任务数据库的只读查询。查询覆盖全部 waiting/downloading/paused/failed 记录，不能用分页界面的 `Tasks` 集合代替；任务库不存在不创建，读取失败则保留候选包。

反馈附件保护查看当前 WPF 反馈窗口是否仍打开。窗口打开期间保留其类别的诊断包、临时进程转储与截图，不因上传尚未开始、上传已经读取部分附件或用户尚未点击发送而删除。这项保护检查不触发上传或日志收集。

## 缩略图必须经 owner 清理

维护页将缩略图操作委托给 `ThumbnailCacheManager.ScanCacheForMaintenance` 和 `ClearCacheForMaintenance`。扫描不调用单例初始化，不建立不存在的目录、数据库或 schema，只读取既有缓存；返回记录数、数据库/侧文件大小、缓存写版本和内容元数据签名。

确认后清理会在 owner 锁与 SQLite 事务下复核写版本、记录数、签名以及数据库/侧文件状态；若扫描后缓存变化，保留现有数据并要求重扫。成功清空缓存表后推进维护 generation，使较早启动、较晚完成的缩略图生成不能回填已清掉的扫描批次。原始图片不修改。

清空记录与 `VACUUM` 整理空间是不同阶段：删除事务提交后，空间整理失败不会恢复缓存行。释放字节数是操作前后的实际缓存文件大小差，不能用数据库扫描大小当作必定可释放值。忙碌、损坏或变化中的数据库不得通过直接删除 `.db/-wal/-shm` 文件“修复”。

## 选择性启动重置与独立备份

“配置”页只允许重置下列已注册非关键配置节：

| 页面选项 | 配置节 | 初始选择 |
| --- | --- | --- |
| 外观与语言 | `ThemeConfig`、`LanguageConfig` | 勾选 |
| 主窗口偏好 | `ColorVision.MainWindowConfig`、旧 `MainWindowConfig` | 勾选 |
| 快捷键 | `HotKeyConfig` | 未勾选 |
| 搜索 | `SearchConfig` | 未勾选 |
| 图像浏览 | `MultiImageViewerConfig` | 未勾选 |

主程序在创建 `ConfigHandler` 之前通过 `ConfigureMaintenanceResetSections(sectionNames, startupAdmission)` 注册白名单和启动准入回调，启动后冻结策略。以上每种配置都同时选择完整类型名和对应旧短类名，避免删除新键后又回退读到旧值。完整键支持命名空间分隔符 `.` 和嵌套类型分隔符 `+`，每一段仍须是非空标识符；重置不能接受路径、通配符或任意插件配置节。设备、数据库连接、授权及未选配置保持原值；这不是凭文件名全量清空配置。主窗口选项不清除 `Spectrum.MainWindowConfig`，也不清空完整停靠布局。

管理员确认后，页面先保存当前配置，再由 `Prepare`/`Schedule` 写入 `<配置路径>.maintenance-reset.json` 意图文件。当前运行中的配置实例不替换、不热重置；正常退出前的保存仍可继续，实际重置在下次启动 `ConfigHandler.Load` 读取配置、创建实例之前执行。待执行计划可以取消；取消只删除意图文件，不修改配置或已有备份。

主程序传入 `MaintenanceStartupGuard.CanApplyReset` 作为 `startupAdmission`。由于常规单实例转交发生在配置加载之后，`ApplyPending` 在共享配置文件锁内先检查是否还有同进程名的其他实例存活；有旧实例时返回 `Deferred`，该次启动的重置步骤不写备份、不改写意图文件或配置，保留待执行计划。必须等旧实例正常保存并退出后，再在后续独占启动时应用重置，避免旧实例退出保存把刚重置的配置覆盖。`Deferred` 是本次操作结果而非已应用，不会作为新的意图文件状态写入；`ConfigHandler` 记录延期原因并继续正常配置加载。

`ApplyPending` 在共享配置文件锁内先保存完整持久化字节到配置目录旁的 `MaintenanceBackups`，验证备份后才删除选中的 JSON 顶层节，并通过配置原子写入入口替换文件。未选节保留原始 JSON 值，包括未知插件节和高精度数字；不靠反序列化全部插件再生成默认配置。

意图文件的 `Scheduled/Prepared/Applied` 状态和前后 SHA-256 用于重启恢复及幂等：原子替换完成后进程中断不会让下次启动再次清除新会话写入的值；准备后配置已被其他写入改变、备份损坏或白名单不匹配时，拒绝继续套用旧计划。重置失败不代表所有文件必定未变化，应结合 `ConfigurationChanged`、状态及错误判断；备份存在也不代表发生了自动恢复。

在“配置恢复点”中点击“创建备份”，页面先调用 `SaveConfigs` 保存当前设置，再调用 `CreateBackup` 备份完整文件；保存失败则不会继续备份。单独调用 `CreateBackup` 只备份当时已落盘的完整字节，不先保存内存设置。这两个入口都不安排重置或替换运行中的配置实例。维护备份与 `BackupConfigs` 的滚动备份独立，不被普通清理和滚动备份保留策略删除；它含完整配置，应按配置本身的敏感程度保管。当前维护页提供备份和打开备份目录，不提供自动回滚按钮，也不通过删除配置主文件触发旧备份兜底。

## 验证入口与缺口

相关隔离测试位于：

- `MaintenanceFileCleanupTests.cs`：确认范围、身份变化、保留期、取消、占用和路径保护。
- `StorageMaintenanceCatalogTests.cs`：实际日志命名、白名单目录、保护状态复查、新更新目录、续传文件与只读接管标记检查。
- `StorageMaintenanceTests.cs`：扩展类别的扫描/清理与失败隔离、文件窗口加载自动扫描、ViewModel 门禁、保留期失效、取消与跨页面互斥；中英语言、深浅主题及 980/1180 宽度的隔离布局；真实设置框架中的注入分组、搜索、滚动复位与说明保留，不发现生产配置或连接生产服务。
- `StorageOverviewTests.cs`：只读占用与保护文件、跨来源清单去重、准备失败清除旧统计、无清单统计拒绝计入、重扫替换和取消后的部分结果；不初始化生产 provider 或访问真实业务目录。
- `StorageUsageScannerTests.cs` 与 `EngineStorageUsageProviderTests.cs`：统计白名单、链接/重解析点和重复来源、模块专用路径、预处理扩展来源、活动保护及确认清单清理；仅使用注入路径与隔离文件。
- `ThumbnailCacheMaintenanceTests.cs`：缺库只读扫描、变化后重扫、事务清理、原图保留和维护 generation 推进；未模拟旧生成任务的实际回写。

从仓库根目录可执行最小相关测试；仅针对测试创建的隔离数据，不运行实际维护页的删除、联网更新或业务数据清理：

```powershell
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~MaintenanceFileCleanupTests|FullyQualifiedName~StorageMaintenanceCatalogTests|FullyQualifiedName~StorageMaintenanceTests|FullyQualifiedName~ThumbnailCacheMaintenanceTests"
```

测试文件存在和知识检查通过不代表测试已运行或真实窗口已验收。日志滚动配置变体、多个应用实例、活跃更新/反馈、真机缩略图并发、设置窗口关闭/卸载及完整重启重置仍需按变更风险验证，不能拿一次模拟目录测试替代这些场景。

---
knowledge_id: "ui.storage-maintenance"
knowledge_type: "topic"
status: "current"
summary: "设置中的日志、缓存、安装包扫描与清理，以及配置恢复点和选择性启动重置；先确认白名单清单，保护活跃任务和业务数据，删除不回滚，重置先独立备份。"
aliases: ["存储与维护", "存储与重置", "扫描空间", "清理选中项", "配置恢复点", "安排重置", "取消重置", "清理日志", "运行垃圾", "工作垃圾", "清理缓存", "重置设置", "StorageMaintenanceCatalog", "StorageMaintenanceControl", "StorageMaintenanceViewModel", "MaintenanceFileCleanup", "ConfigMaintenanceResetService", "ThumbnailCacheMaintenanceSnapshot", "HasPackageMaintenanceProtection", "HasActiveUpdateForCleanup"]
code_paths: ["ColorVision/Settings/Maintenance", "ColorVision/App.xaml.cs", "ColorVision/Update/CombinedUpdateCoordinator.cs", "UI/ColorVision.UI/Maintenance", "UI/ColorVision.UI/ConfigMaintenanceResetService.cs", "UI/ColorVision.UI/ConfigHandler.cs", "UI/ColorVision.UI/Update/ExitUpdateHandoff.cs", "UI/ColorVision.UI.Desktop/Download/Aria2cDownloadManager.cs", "UI/ColorVision.UI.Desktop/Download/Infrastructure/DownloadTaskStore.cs", "UI/ColorVision.ImageTools/MultiImageViewer/ThumbnailCacheManager.cs", "UI/ColorVision.ImageTools/MultiImageViewer/ThumbnailCacheManager.Maintenance.cs"]
test_paths: ["Test/ColorVision.UI.Tests/MaintenanceFileCleanupTests.cs", "Test/ColorVision.UI.Tests/StorageMaintenanceCatalogTests.cs", "Test/ColorVision.UI.Tests/StorageMaintenanceTests.cs", "Test/ColorVision.UI.Tests/ThumbnailCacheMaintenanceTests.cs", "Test/ColorVision.UI.Tests/ConfigMaintenanceResetTests.cs"]
related: ["ui.settings", "ui.configuration", "operations.logs", "delivery.update", "engine.database-maintenance"]
---

# 存储清理与选择性设置重置

`ColorVision/Settings/Maintenance/` 在设置窗口中提供独立的“存储与维护”页。普通清理仅处理明确归属本软件、可删除或可重建的日志和缓存；设置重置是另一个需要管理员确认、下次启动生效的操作。两者都不是“删除整个 AppData”或“恢复出厂后清空业务数据”。

页面通过 `StorageMaintenanceSettingsProvider` 的 `IConfigSettingProvider` 元数据发现，展示与通用设置窗口的关系见[设置窗口契约](./settings.md)。文件白名单归主程序 `StorageMaintenanceCatalog`，Windows 文件执行器归 `UI/ColorVision.UI/Maintenance/`；缩略图数据库由自己的 owner 管理，不能交给通用文件删除器处理。

## 清理范围与默认保留

| 类别 | 当前白名单 | 默认策略与保护 |
| --- | --- | --- |
| 历史日志 `logs` | 当前 log4net 仓库中所有 `FileAppender` 的实际落点；只有 `RollingFileAppender` 对应的日期/序号归档名进入清理规则 | 保留最近 30 天，默认勾选；当前活动日志始终保留，不把同目录所有文本文件当日志 |
| 运行临时文件 `temp` | `%TEMP%` 顶层 `ColorVision_Diagnostics_*.zip`、`ColorVision_Screenshot_*.png`；`ColorVisionUpdate-<N格式GUID>`、`ColorVisionPluginsUpdate-<N格式GUID>`、`ColorVisionPackageVersion-<N格式GUID>` 目录内文件 | 保留最近 7 天，默认勾选；反馈窗口打开时保留反馈附件；进行中或待执行的更新保护更新暂存文件 |
| 缩略图 `thumbnails` | `ThumbnailCacheManager.SqliteDbPath` 指向的既有缩略图数据库 | 默认勾选；只经 owner 清缓存记录和整理空间，不直接删除数据库、WAL 或 SHM 文件 |
| CIE 背景 `cie-cache` | `%LocalAppData%\ColorVision\ImageEditor\CieCache` 中三个 `CieDiagramKind` 对应、版本号有效的背景 PNG | 默认勾选；可按需重新生成，不含用户图像 |
| 安装包缓存 `packages` | `Environments.DirPackageCache` 下 `Application/Full`、`Application/Incremental`、`Plugins`、`Tools` 的顶层已知包扩展 | 保留最近 30 天，默认不勾选；保护待安装更新、下载任务和续传状态，不递归进入 `Recovery` |

日志、临时文件、安装包保留期可选 7、14、30、90、180 天。修改一类的保留期会使该类旧扫描失效，必须重新扫描。保留期以文件 UTC 修改时间判断；更新 GUID 暂存目录的创建时间也必须早于所选保留期（默认 7 天），防止刚解压的文件继承旧修改时间而被误认成历史垃圾。目录不存在时不创建。

不在普通清理范围内的内容包括：原始检测图、结果文件、流程模板、校准文件、设备配置、授权、数据库连接、用户下载目录、Copilot 会话、程序快照和恢复区。不能根据 `.tmp`、`.zip`、`.db` 等扩展名扫描整个磁盘或临时目录。

数据库管理和程序备份是页面上的独立入口，不参与“清理选中项”。数据库入口要求管理员且当前流程状态可用、未运行；后续行为仍由[数据库清理窗口](../engine-components/database-maintenance.md)负责。打开程序备份也不等于删除快照或重建默认快照。

## 扫描、确认与执行

`StorageMaintenanceViewModel` 使用进程内共享操作门禁，避免多个维护页同时执行扫描或清理。点击“扫描空间”会在后台检查全部五类，未勾选的安装包也会显示扫描结果。勾选决定顶部合计和“清理选中项”的范围；行内“清理”只处理该行，不要求先勾选。普通文件详情显示绝对路径、字节数和 UTC 修改时间。统计表示本次合格候选，不是整个目录占用，也不保证全部字节最终可释放。

普通文件按以下边界执行：

1. `MaintenanceFileCleanup.Scan` 规范化绝对根路径、文件名模式和保留期，生成不可变文件清单。重复候选去重；扫描取消时当前类不提供可执行的完整扫描。
2. 页面确认选中类别的候选数量和大小。没有有效扫描、保留期已变化、数量为零或正在执行时，不能直接清理该类。
3. `Cleanup` 仅处理已确认清单，不重新扩张目录范围，不删除扫描后新出现的文件。
4. 每个文件执行前重新校验路径归属、文件身份、创建/修改时间、长度、保留期及保护回调。扫描后被替换、修改或开始使用的文件跳过，要求重新扫描。
5. Windows 目录句柄从路径祖先开始固定，文件与任意祖先的链接/重解析点均被拒绝；删除使用同一个独占校验句柄，不在检查后按路径重新打开删除。占用、权限和其它失败分别进入结果，不自动提升权限或结束进程。

文件删除是永久删除，不进入回收站。执行器只删文件，保留根目录及清空后的目录，不承诺清理所有空目录。成功返回的统计按已完成删除累计；跳过和失败不计为释放空间。

点击“取消”会请求停止后续工作，已经完成的删除不会撤销。设置窗口在当前维护页忙碌时阻止直接关闭并提示先取消或等待；页面卸载也会请求取消。取消不能强制中断每个正在执行的同步操作，特别是缩略图 owner 的一次数据库事务/空间整理；界面取消不是事务回滚。

## 更新、下载与反馈保护

`CombinedUpdateCoordinator.HasPackageMaintenanceProtection` 仅读取待更新计划、正在运行的预下载任务和更新操作门禁。只要存在这些状态，安装包整类保留，避免“已下载完成”被误认成“已经安装、可以删除”。暂存文件也受同一更新状态保护。

`ExitUpdateHandoff.HasActiveUpdateForCleanup` 检查 `%LocalAppData%\ColorVision\UpdateState` 各安装实例的 `update.pending`，保护准备阶段或仍存活的外部更新进程。该入口不会清理失效标记、写重开请求或启动更新；格式异常、无法读取或无法确认进程状态时保守保护。它不同于可能清除过期 marker 的启动接管检查。完整更新机制见[自动更新](../../02-developer-guide/deployment/auto-update.md)。

`Aria2cDownloadManager.IsPathProtectedFromCleanup` 不调用 `GetInstance()`，不会为了扫描而初始化下载器、创建其任务库或启动 aria2。判断包括已有实例的并发活动集合、`.aria2` 续传文件，以及 `DownloadTaskStore` 对既有任务数据库的只读查询。查询覆盖全部 waiting/downloading/paused/failed 记录，不能用分页界面的 `Tasks` 集合代替；任务库不存在不创建，读取失败则保留候选包。

反馈附件保护查看当前 WPF 反馈窗口是否仍打开。窗口打开期间保留其类别的诊断包与截图，不因上传尚未开始、上传已经读取部分附件或用户尚未点击发送而删除。这里没有上传操作，也不会清理原始日志收集源。

## 缩略图必须经 owner 清理

维护页将缩略图操作委托给 `ThumbnailCacheManager.ScanCacheForMaintenance` 和 `ClearCacheForMaintenance`。扫描不调用单例初始化，不建立不存在的目录、数据库或 schema，只读取既有缓存；返回记录数、数据库/侧文件大小、缓存写版本和内容元数据签名。

确认后清理会在 owner 锁与 SQLite 事务下复核写版本、记录数、签名以及数据库/侧文件状态；若扫描后缓存变化，保留现有数据并要求重扫。成功清空缓存表后推进维护 generation，使较早启动、较晚完成的缩略图生成不能回填已清掉的扫描批次。原始图片不修改。

清空记录与 `VACUUM` 整理空间是不同阶段：删除事务提交后，空间整理失败不会恢复缓存行。释放字节数是操作前后的实际缓存文件大小差，不能用数据库扫描大小当作必定可释放值。忙碌、损坏或变化中的数据库不得通过直接删除 `.db/-wal/-shm` 文件“修复”。

## 选择性启动重置与独立备份

页面只允许重置下列已注册非关键配置节：

| 页面选项 | 配置节 | 初始选择 |
| --- | --- | --- |
| 外观与语言 | `ThemeConfig`、`LanguageConfig` | 勾选 |
| 主窗口偏好 | `MainWindowConfig` | 勾选 |
| 快捷键 | `HotKeyConfig` | 未勾选 |
| 搜索 | `SearchConfig` | 未勾选 |
| 图像浏览 | `MultiImageViewerConfig` | 未勾选 |

主程序在创建 `ConfigHandler` 之前通过 `ConfigureMaintenanceResetSections(sectionNames, startupAdmission)` 注册白名单和启动准入回调，启动后冻结策略。重置不能接受目录、通配符或任意插件配置节。设备、数据库连接、授权及未选配置保持原值；这不是凭文件名全量清空配置。主窗口选项只重置 `MainWindowConfig` 偏好，不是清空完整停靠布局。

管理员确认后，页面先保存当前配置，再由 `Prepare`/`Schedule` 写入 `<配置路径>.maintenance-reset.json` 意图文件。当前运行中的配置实例不替换、不热重置；正常退出前的保存仍可继续，实际重置在下次启动 `ConfigHandler.Load` 读取配置、创建实例之前执行。待执行计划可以取消；取消只删除意图文件，不修改配置或已有备份。

主程序传入 `MaintenanceStartupGuard.CanApplyReset` 作为 `startupAdmission`。由于常规单实例转交发生在配置加载之后，`ApplyPending` 在共享配置文件锁内先检查是否还有同进程名的其他实例存活；有旧实例时返回 `Deferred`，该次启动的重置步骤不写备份、不改写意图文件或配置，保留待执行计划。必须等旧实例正常保存并退出后，再在后续独占启动时应用重置，避免旧实例退出保存把刚重置的配置覆盖。`Deferred` 是本次操作结果而非已应用，不会作为新的意图文件状态写入；`ConfigHandler` 记录延期原因并继续正常配置加载。

`ApplyPending` 在共享配置文件锁内先保存完整持久化字节到配置目录旁的 `MaintenanceBackups`，验证备份后才删除选中的 JSON 顶层节，并通过配置原子写入入口替换文件。未选节保留原始 JSON 值，包括未知插件节和高精度数字；不靠反序列化全部插件再生成默认配置。

意图文件的 `Scheduled/Prepared/Applied` 状态和前后 SHA-256 用于重启恢复及幂等：原子替换完成后进程中断不会让下次启动再次清除新会话写入的值；准备后配置已被其他写入改变、备份损坏或白名单不匹配时，拒绝继续套用旧计划。重置失败不代表所有文件必定未变化，应结合 `ConfigurationChanged`、状态及错误判断；备份存在也不代表发生了自动恢复。

在“配置恢复点”中点击“创建备份”，页面先调用 `SaveConfigs` 保存当前设置，再调用 `CreateBackup` 备份完整文件；保存失败则不会继续备份。单独调用 `CreateBackup` 只备份当时已落盘的完整字节，不先保存内存设置。这两个入口都不安排重置或替换运行中的配置实例。维护备份与 `BackupConfigs` 的滚动备份独立，不被普通清理和滚动备份保留策略删除；它含完整配置，应按配置本身的敏感程度保管。当前维护页提供备份和打开备份目录，不提供自动回滚按钮，也不通过删除配置主文件触发旧备份兜底。

## 验证入口与缺口

相关隔离测试位于：

- `MaintenanceFileCleanupTests.cs`：确认范围、身份变化、保留期、取消、占用和路径保护。
- `StorageMaintenanceCatalogTests.cs`：实际日志命名、白名单目录、保护状态复查、新更新目录、续传文件与只读接管标记检查。
- `StorageMaintenanceTests.cs`：ViewModel 扫描/清理门禁、保留期失效、取消与跨页面互斥；中英语言、深浅主题及 980/1180 宽度的隔离布局；真实设置框架中的注入分组、搜索、滚动复位与说明保留，不发现生产配置或连接生产服务。
- `ThumbnailCacheMaintenanceTests.cs`：缺库只读扫描、变化后重扫、事务清理、原图保留和维护 generation 推进；未模拟旧生成任务的实际回写。
- `ConfigMaintenanceResetTests.cs`：选择性删除、完整备份、启动幂等、取消和失败边界。

从仓库根目录可执行最小相关测试；仅针对测试创建的隔离数据，不运行实际维护页的删除、联网更新或业务数据清理：

```powershell
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~MaintenanceFileCleanupTests|FullyQualifiedName~StorageMaintenanceCatalogTests|FullyQualifiedName~StorageMaintenanceTests|FullyQualifiedName~ThumbnailCacheMaintenanceTests|FullyQualifiedName~ConfigMaintenanceResetTests"
```

测试文件存在和知识检查通过不代表测试已运行或真实窗口已验收。日志滚动配置变体、多个应用实例、活跃更新/反馈、真机缩略图并发、设置窗口关闭/卸载及完整重启重置仍需按变更风险验证，不能拿一次模拟目录测试替代这些场景。

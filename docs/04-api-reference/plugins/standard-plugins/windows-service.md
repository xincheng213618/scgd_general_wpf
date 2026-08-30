---
knowledge_id: "plugins.windows-service"
knowledge_type: "topic"
status: "current"
summary: "WindowsServicePlugin的在线选包与缓存、本机完整安装、数据库版本切换和恢复边界；下载、日志完成、备份与实际服务状态不能互相替代。"
aliases: ["Windows 服务管理器", "CVWindowsService 本机安装", "服务包在线下载", "WindowsServicePlugin", "ServiceInstallViewModel", "ServiceManagerAppProvider", "ServiceManagerConfig", "ServicePackageVersionResolver", "ServiceDatabaseVersionMap", "ServiceHostWindowsServiceController", "InstallServiceManager", "InstallTool", "CVWinSMS", "AutoUpdateDatabase", "BackupBeforeInstall", "BackupServiceBeforeInstall", "UpdateServerUrl", "DownloadLocation", "IsFullServicePackageZip", "FindCachedCvWindowsServicePackage", "GetLatestCvWindowsServicePackageAsync"]
code_paths: ["Plugins/WindowsServicePlugin/WindowsServicePlugin.csproj", "Plugins/WindowsServicePlugin/manifest.json", "Plugins/WindowsServicePlugin/App.xaml.cs", "Plugins/Directory.Build.props", "Plugins/WindowsServicePlugin/ServiceManager/MenuServiceManager.cs", "Plugins/WindowsServicePlugin/ServiceManager/InstallServiceManager.cs", "Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerWizardInitializer.cs", "Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallViewModel.cs", "Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallViewModel.Install.cs", "Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallViewModel.Backup.cs", "Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallWindow.xaml", "Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallWindow.xaml.cs", "Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerConfig.cs", "Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerViewModel.Config.cs", "Plugins/WindowsServicePlugin/ServiceManager/ServiceHostWindowsServiceController.cs", "Plugins/WindowsServicePlugin/ServiceManager/ServiceDatabaseVersionMap.cs", "Plugins/WindowsServicePlugin/ServiceManager/Mysql/MySqlServiceManager.cs", "Plugins/WindowsServicePlugin/ServiceManager/MySqlServiceHelper.cs", "Plugins/WindowsServicePlugin/ServiceManager/Mqtt/MqttServiceManager.cs", "Plugins/WindowsServicePlugin/CVWinSMS/InstallTool.cs", "Plugins/WindowsServicePlugin/CVWinSMS/CVWinSMSConfig.cs", "Plugins/WindowsServicePlugin/Menus", "UI/ColorVision.UI/Menus/MenuManager.cs", "UI/ColorVision.UI/Marketplace/MarketplaceConfig.cs", "ColorVision/MainWindow.xaml.cs", "Engine/ColorVision.Engine/Mysql/MySqlDatabaseMaintenanceService.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ServiceDatabaseVersionMapTests.cs", "Test/ColorVision.UI.Tests/InstallToolAsyncCommandTests.cs", "Test/ColorVision.UI.Tests/MySqlBackupRestoreSafetyTests.cs", "Test/ColorVision.UI.Tests/ThirdPartyAppInfoTests.cs"]
related: ["plugins.index", "plugins.getting-started", "delivery.cvwindowsservice", "ui.menus", "ui.wizards"]
---

# WindowsServicePlugin：选包、本机安装与恢复

本主题负责客户端如何选取 `CVWindowsService` 包，以及本机安装、数据库切换、配置和恢复；Backend 发布、版本/包选择与下载响应由 [CVWindowsService 发布与下载](../../../02-developer-guide/backend/cvwindowsservice.md)维护。服务端成功返回不代表客户端校验、安装或启动成功。

必须区分三个制品：`WindowsServicePlugin.dll` / `.cvxp` 是插件；`CVWindowsService[版本]...zip` 是业务服务包；`ColorVisionServiceHost` 是许多安装/启停操作调用的后台权限代理。发布插件不等于发布服务ZIP，也不等于授权安装或变更数据库。

## 入口、依赖与权限

项目和 `Plugins/Directory.Build.props` 定义 Windows WPF、x64、`net10.0-windows` 及 Engine/UI 依赖。manifest身份为 `WindowsServicePlugin`，入口 `WindowsServicePlugin.dll`；插件发布版本取编译DLL的FileVersion。[插件产物与交付](../../../02-developer-guide/plugin-development/getting-started.md)负责HostCopy、manifest和.cvxp规则。

- `ServiceManagerAppProvider` 在“应用与工具 > 内部工具”提供非模态窗口，要求应用内 `PermissionMode.Administrator`；这不是Windows提权、目录ACL或后台代理授权已通过的证明。
- `InstallServiceManager` 是向导步骤，打开模态窗口；`ConfigurationStatus` 仅检查BaseLocation非空且目录存在，不检查数据库、服务安装或健康。首次向导另有 `ServiceManagerWizardInitializer` 的导入/手动/跳过选择。
- `ServiceHostWindowsServiceController` 将安装/卸载/启停交给 `ColorVisionServiceHostClient`，返回bool并记录代理缺失、旧版本或失败。执行仍需兼容的代理及服务、文件、数据库权限。
- 项目虽为WinExe，当前 `App.Application_Startup` 初始化配置/日志/主题、加载Engine后即Shutdown，不是另一套独立服务管理器交付入口。

安装、SQL、备份恢复、外部工具启动和进程关闭均须明确目标与授权。查看主题或下载元数据不授权这些动作；配置和日志可能含敏感内容，不为排障打印实际密码或完整配置。

## 在线选包、回退与缓存

当前安装窗口有 `DownloadServicePackageCommand` / `OnlineDownloadCommand`，不是只有本地选ZIP。`ServiceManagerConfig.UpdateServerUrl` 默认取Marketplace默认地址；`DownloadLocation` 默认位于 `Environments.DirToolPackageCache/CVWindowsService`。

`GetLatestCvWindowsServicePackageAsync` 的客户端顺序是：

1. 依次尝试配置源与 `MarketplaceConfig.DefaultServiceBaseUrl`，按authority去重；`BuildServerRoot` 只保留authority，丢弃配置URL的路径、query和fragment，不是任意子路径API前缀配置。
2. 每源先请求 `/api/tool/cvwindowsservice/releases`，读取latestVersion，在同版本packages中按数值suffix降序选择；没有可用downloadUrl仍可构造该版本download路径。
3. releases获取/解析未成功才读 `/latest-version`，接受版本文本或含version/latestVersion的JSON，构造 `/download/<version>`；该源两路失败才转下一源。
4. 绝对downloadUrl原样使用，相对URL按源authority解析；这一解析层没有同源或强制HTTPS门禁。元数据HttpClient超时15秒，失败转下一分支，不在这里发送下载凭据。

取得候选即结束元数据源遍历，后面的实际下载或包校验失败不会重新尝试另一个源。`FindCachedCvWindowsServicePackage` 在下载目录顶层查 `CVWindowsService[版本]*.zip`，只取本地修改时间最新的一个，再通过下述目录识别；无效时进入下载，不逐个尝试更旧缓存。不与远端suffix、大小或摘要比对。下载委托 `IDownloadService`，该调用authorization参数为null，并等待回调路径。`CvwsPackageInfo` 只保留Version/DownloadUrl，未消费服务端hash/size；不能宣称此消费者已做发布签名或SHA-256核验。

单独下载成功仅设置 `ServicePackagePath` 并勾选服务安装，不自动安装。在线下载并行取得服务包与当前可见的MySQL/MQTT/VC++ 2013组件，等待全部任务后应用路径；单项失败不回滚已下载文件。安装是独立命令；关闭安装窗口只释放日志绑定，没有取消这些后台下载/安装任务的逻辑。

## “完整包”识别不是完整性认证

`IsFullServicePackageZip` 只打开ZIP并检查顶层名称包含RegWindowsService，以及CVMainWindowsService_x64 / CVMainWindowsService_dev至少一个；不要求目录内exe、cfg、SQL或CommonDll齐全，不验证签名/摘要。名称符合不等于包可信、可运行；该工作流仍以完整包而非增量包为交付契约。

`ServicePackageVersionResolver` 优先读取包中服务exe的FileVersion，失败回退文件名中的 `CVWindowsService[版本]`；读取版本会临时提取文件，但不执行它。无法得到版本则终止安装。解压后若能读版本就比较一致性，读不到时只记日志并继续使用包版本，不是每个成功路径都完成二次版本核验。

安装清理枚举包内实际顶层名称，经名称和路径范围检查后删除BaseLocation下对应文件/目录，再解压覆盖；不限于三个服务目录。因此要确认包来源及每个实际顶层目标，“仅包内顶层”不是业务文件白名单。

## 安装、数据库与配置顺序

`ExecuteInstallAsync` 先解析现有/目标服务版本和数据库，再依选择备份、安装组件、替换文件、注册服务、执行数据库步骤、同步配置、启动服务；不是跨文件、服务注册和数据库的原子事务。

| 阶段 | 当前约束与失败边界 |
| --- | --- |
| 版本与数据库 | `ServiceDatabaseVersionMap` 以主版本4为分界：低于4使用color_vision，4及以上使用color_vision_4xx；版本已知时优先于配置库名，未知时才沿用配置或默认4xx库 |
| 数据库升级选择 | AutoUpdateDatabase初始为true。已有MySQL且跨库名边界时，关闭该选项会在替换文件前拒绝 |
| 备份 | BackupBeforeInstall和BackupServiceBeforeInstall初始均false；即使勾选，方法也可能跳过、只记异常或忽略返回false而继续，不是备份验证成功才允许覆盖 |
| 停止与替换 | 尝试停止受管理服务并关闭旧CVWinSMS进程；普通停止失败不一定阻止清理，归档服务卸载失败则阻止包更新 |
| CommonDll与注册 | CommonDll存在时复制到已存在的三个服务目录再删源目录；不存在或复制异常只记日志。普通服务缺exe会跳过，后台安装返回false只记录，不汇总为整体失败 |
| SQL与业务账号 | 新装MySQL走初始化；其它勾选升级时由插件MySqlServiceManager委托Engine的MySqlDatabaseMaintenanceService.ResetDatabaseFromSqlFileAsync。安装先要求找到color_vision_all.sql，数据库步骤成功后更新业务账号授权；明确失败中止后续配置/启动 |
| 配置与启动 | 数据库步骤后才ApplyDatabaseName、ApplyConfigAndRefreshAfterInstall。有服务/MySQL/MQTT安装工作便自动调用启动，不存在独立可选启动开关；启动返回false或部分异常不统一转安装失败 |

MySQL ZIP安装位置与服务根同级，默认业务用户cv。`MySqlServiceHelper` 将带UTF-8 BOM的SQL按UTF-8读取，其余先严格UTF-8解码、失败回退GB18030，再向mysql.exe传UTF-8。Engine重置链先备份源库中实际存在的 `MigrationBackupTableNames` 资源表，执行安装SQL后确认目标库可连接，再把保留数据回写目标库；不是整库或全部结果无损迁移。安装器要求SQL存在，而直接调用部分MySQL helper找不到SQL时会记录跳过并返回true，二者成功判据不同。

配置来自ServiceManagerConfig、MySqlServiceConfig、MqttServiceConfig和RC设置，写到实际服务目录cfg/MySql.config、MQTT.config、WinService.config等；旧App.config仍兼容同步。已存在的受管理配置写异常可传播到安装编排、阻止后续启动，但缺配置文件会跳过，`SyncLegacyAppConfig` 异常只记日志。不能沿用“所有同步失败都阻止带旧配置启动”的保证。

`ServiceManagerConfig.BaseLocation` 是安装根，`MySqlPort` 默认3306；安装窗口的 `InstallServiceChecked/InstallMySqlChecked/InstallMqttChecked` 直接代理同名配置字段，初始缺省分别true/false/false。`AutoUpdateDatabase`、两个备份开关和所选包路径则是安装ViewModel自己的状态，不能把勾选项一律当作已保存配置或已完成动作。

窗口“安装完成”只表示编排到达末尾；还须分别核对服务状态、版本、配置及数据库结果。日志、progress=100、某次ServiceHost成功均不替代整条安装验收。

## 备份和恢复不等于自动回滚

数据库备份默认写入“我的文档/ColorVision/Backup”；服务文件备份写入“我的文档/ColorVision/ServiceBackup”，优先RAR，工具不可用/失败时回退ZIP。文件存在或完成日志不是恢复演练证明；ZIP备份逐文件异常也可能只记日志后继续。

`TryRestoreArchiveServiceAfterFailedPackageUpdate` 仅尝试恢复原有归档服务注册/启动状态，可能选择当前解压后仍存在的exe；不还原全部旧文件、数据库和配置，不是全安装回滚。

手动 `DoRestoreServiceBackup` 会尝试停服务、删除整个BaseLocation，再解压ZIP/RAR，比安装时按包顶层清理范围更大。RAR工具缺失/解压失败可能发生在删除之后，finally仍尝试启动服务，没有自动恢复刚删除目录的补偿。必须先确认目标绝对路径、独立可用备份、工具及恢复授权；数据库恢复是另一项操作，不因文件恢复完成自动完成。

## 旧CVWinSMS / InstallTool仍有实际入口

当前源码与旧README“旧在线下载、外部工具、日志入口已经移除”的断言冲突：项目仍编译 `CVWinSMS/InstallTool.cs` 和Menus，未排除或标Obsolete。`InstallTool : MenuItemBase, IMainWindowInitialized` 的OwnerGuid为ServiceLog，ServiceLog挂在Help下，通用MenuManager可发现这些类型；实际可见性仍受[菜单规则与配置](../../ui-components/menus.md)影响，不保证所有环境必然显示。

`MainWindow.LoadIMainWindowInitialized` 会调用已加载实现的Initialize；InstallTool在已有配置工具文件时读版本并查询 `UpdatePath/LATEST_RELEASE`，发现更新后询问。其ZIP URL仍硬编码旧9999站点的 `Tool/InstallTool/InstallTool[版本].zip`，不走 `/api/tool/cvwindowsservice`。确认下载/更新后可能关闭或杀掉CVWinSMS进程、覆盖/迁移目录与旧配置，并用runas启动外部工具；不能写成惰性的配置兼容读取，也不能以发现类为由执行它。

服务日志菜单与localhost REST日志导出类也仍在Menus；这是另一本机入口，不表示Backend CVWS API提供这些功能。未经核实的历史License/增量包等能力不因旧类存在就恢复为当前承诺。

## 构建、发布与验证

插件构建、.cvxp上传和CVWindowsService服务ZIP发布是不同动作。以下从仓库根目录运行。本地构建写构建/HostCopy产物，不执行安装器：

```powershell
dotnet build .\Plugins\WindowsServicePlugin\WindowsServicePlugin.csproj -c Release -p:Platform=x64
```

仅在明确授权发布插件时执行下面的wrapper；它会构建、上传并清理本地.cvxp，不是普通验证命令：

```powershell
.\Scripts\package_plugin.bat WindowsServicePlugin
```

- `ServiceDatabaseVersionMapTests` 覆盖主版本数据库映射、包名解析、嵌入exe版本优先及配置回退，不执行真库升级或证明远端包可信。
- `InstallToolAsyncCommandTests` 验证旧菜单异步失败可观察、Download返回Task，不验证旧站点、下载内容或工具运行。
- `MySqlBackupRestoreSafetyTests` 中插件相关用例检查恢复/重置委托Engine的源码边界，另有临时配置文件测试；不是安装、真库迁移或故障回滚验收。
- `ThirdPartyAppInfoTests` 包含服务管理器提供器元数据检查，不证明应用角色、Windows权限及后台代理全链可用。

这些是现存测试范围，不是执行通过声明。客户端源回退/包排序/缓存、完整包识别、下载中断、部分备份/安装/启动失败、真实SQL迁移和恢复尚不能由这些测试认证；需在获授权的隔离环境逐层验证，不由文档构建替代。

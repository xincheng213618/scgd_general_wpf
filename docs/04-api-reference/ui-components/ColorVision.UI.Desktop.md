---
knowledge_id: "ui.desktop"
knowledge_type: "reference"
status: "current"
summary: "桌面辅助壳层而非产品主入口：定位设置、市场下载、第三方工具、反馈和特权崩溃诊断。"
aliases: ["设置窗口和插件市场在哪里","ColorVision.UI.Desktop","SettingWindow","MarketplacePackageDownloadService","下载管理器","断点续传","Aria2cDownloadManager"]
code_paths: ["UI/ColorVision.UI.Desktop/ColorVision.UI.Desktop.csproj","UI/ColorVision.UI.Desktop/App.xaml","UI/ColorVision.UI.Desktop/App.xaml.cs","UI/ColorVision.UI.Desktop/MainWindow.xaml","UI/ColorVision.UI.Desktop/MainWindow.xaml.cs","UI/ColorVision.UI.Desktop/Settings/SettingWindow.xaml.cs","UI/ColorVision.UI.Desktop/Marketplace","UI/ColorVision.UI.Desktop/Download","UI/ColorVision.UI.Desktop/Wizards","UI/ColorVision.UI.Desktop/ThirdPartyApps","UI/ColorVision.UI.Desktop/Diagnostics","UI/ColorVision.UI.Desktop/Feedback","UI/ColorVision.UI.Desktop/README.md"]
test_paths: ["Test/ColorVision.UI.Tests/MarketplacePackageDownloadServiceTests.cs","Test/ColorVision.UI.Tests/DownloadManagerLifecycleTests.cs","Test/ColorVision.UI.Tests/DownloadInfrastructureTests.cs","Test/ColorVision.UI.Tests/DownloadAria2IntegrationTests.cs","Test/ColorVision.UI.Tests/FeedbackLogCollectorTests.cs","Test/ColorVision.UI.Tests/NetworkAdapterPriorityServiceTests.cs"]
related: ["ui.index","ui.framework","ui.settings","ui.wizards","ui.menus","ui.configuration","ui.database","plugins.getting-started","platform.runtime"]
---

# ColorVision.UI.Desktop

`UI/ColorVision.UI.Desktop/` 是桌面侧辅助壳层功能集合，包含设置、向导、菜单管理、插件市场、下载、第三方应用入口、反馈和崩溃诊断。它不是整个产品主入口；真正主程序在 `ColorVision/`。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 设置页为空或少项 | [设置发现与缓存](./settings.md)：Provider/特性、程序集视图、类型缓存和当前搜索范围 |
| 自定义设置 View 不显示 | [页面生命周期](./settings.md)：`ViewType`、无参构造、失败内容缓存与绑定 |
| 向导步骤不出现 | [向导发现](./wizards.md)：`IWizardStep`、程序集视图、反射/构造失败和排序 |
| 插件市场 README/CHANGELOG 空白 | WebView2 初始化、Markdown CSS、内容是否为空 |
| 下载失败或卡住 | `Assets/Tool/aria2c.exe`、当前 RPC 端口、任务错误及目标目录权限；端口被占用时选择其他端口，只停止本管理器启动的进程 |
| DLL 版本窗口缺少条目 | 目标程序集是否已加载到当前进程 |
| 第三方应用打不开 | `SystemAppProvider` / 自定义应用路径、权限和系统工具是否存在 |
| Dump 设置失败 | `ColorVisionServiceHost` 是否已安装且为当前版本、`Diagnostics/CrashDumpConfiguration.cs` 的 HKLM 目标项和保存目录 |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 设置窗口 | `Settings/SettingWindow.xaml.cs` | [侧栏、搜索、活对象编辑与关窗保存](./settings.md)，不是 Tab 或取消事务 |
| 向导流程 | `WizardManager`、`WizardWindow`、`WizardWindowConfig` | [步骤发现、应用、初始化与完成标记](./wizards.md)，完成不等于所有组件健康 |
| 菜单项管理 | `MenuItemManagerConfig`、`MenuItemManagerWindow` | [显示覆盖、编辑草稿与应用/保存边界](./menus.md)；不编辑快捷键 |
| 插件市场 | `MarketplaceWindow`、`MarketplaceClient`、`MarketplacePackageDownloadService` | 展示市场内容、Markdown、下载和安装入口 |
| 下载管理 | `Aria2cDownloadManager`、`DownloadWindow` | 使用内置 `aria2c.exe` 管理下载 |
| 第三方应用 | `SystemAppProvider`、`CustomAppProvider`、`ThirdPartyAppsWindow` | 系统工具、自定义应用和磁盘 Treemap 入口 |
| 主程序工具贡献 | `ColorVision/ToolPlugins/ThirdPartyApps/` | 通过 `IThirdPartyAppProvider` 向第三方应用窗口补充主程序工具；“上网网卡选择”可读取 IPv4、DNS、网关和 Metric，修改所选接口的 Metric，或将 DNS 设为 `114.114.114.114` 后刷新缓存 |
| 崩溃诊断 | `Diagnostics/CrashDumpSettingsControl`、`CrashDumpConfiguration` | 通过通用属性反射生成 WER LocalDumps 设置，由后台特权服务写入 HKLM；支持手动保存当前进程 Dump 和反馈包收集 |
| 反馈诊断 | `Feedback/`、`Feedback/Collectors/WindowsEventLogCollector`、`ColorVision.UI/LogImp/Collectors/ConfigurationSnapshotCollector` | 打包应用日志、系统信息、脱敏配置快照、Dump 和 Windows Application/System 警告或错误；可从反馈窗口清理旧的应用、更新、服务日志和 ColorVision Dump |
| 诊断窗口 | `ViewDllVersionsWindow` | 查看已加载程序集版本、产品版本和路径 |

应用与工具窗口使用与更新、恢复窗口一致的主题资源，顶部提供搜索、添加应用、添加快捷脚本和刷新入口；分类以可换行的标签展示，应用以图标与名称组成的紧凑卡片展示。双击启动、右键操作、分类过滤与权限过滤沿用原入口；深浅主题同时覆盖窗口背景、文字和选择状态。

## 下载任务与文件保护

`Aria2cDownloadManager` 按任务串行处理启动、暂停、恢复和删除。取消或删除会立即使旧操作失效；即使 `addUri` 响应晚到，也会清除已提交的 GID，避免记录删除后后台下载继续运行。暂停任务保持同一模型和 GID，分页不会丢失恢复状态；重启只自动恢复等待或下载中的记录，明确暂停的记录由用户恢复。GID 已失效时重新提交并保留断点文件，RPC 错误作为错误处理，不能把文件打开失败当作缓存损坏而删除文件。

新建普通下载写入任务独有的 `.cvdownload-<GUID>.part`，完成后检查实际路径、大小和 SHA-256，再移动到预留的最终路径；已有最终文件不会被覆盖。旧记录仍沿用原路径及 `.aria2` 断点文件。取消和失败保留下载文件；清空记录只删除操作开始时的记录集合并停止对应任务，保留文件。删除记录时仅在用户选择删除文件后清理相应普通文件及断点文件，目录不会递归删除。完成回调异常单独记录，不触发重新下载或删除已成功的文件。

`AddDownload` 保持原有调用契约；调用方可通过 `AddVerifiedDownload` 提供预期 SHA-256，市场下载会传入包元数据的哈希并继续执行市场自己的包校验。复用本地文件必须与调用方提供的哈希或服务器 `Digest` / `Content-Digest` 中的 SHA-256 匹配，复制过程也校验内容；文件大小相同、同 URL 历史完成记录或 ETag 本身不足以证明内容可复用。缺少可信摘要时正常联网下载。

同名任务在提交前预留不同路径；URL 和磁力链接的文件名被规范化为单一文件名。磁力任务使用独立目录，跟随 aria2 的 `followedBy` 子任务，并在完成后保存实际文件或目录路径。下载认证以 Windows 当前用户的 DPAPI 保护，旧的 Base64／明文记录在初始化时迁移；迁移保留任务 ID、路径与状态。受保护凭据不能直接由另一 Windows 用户解密，迁移数据时需要重新提供认证。

RPC 只连接回环地址，后台进程使用随机的实例密钥；启动等待 RPC 就绪，不会为了回收端口而终止其他 aria2 进程。活动任务每批最多 64 个 GID 查询状态，历史完成记录不参与轮询；本地校验／复制受 `MaxConcurrentTasks` 限制。分页查询和文件检查在后台执行，取消旧分页请求可避免过期结果覆盖当前页；这些限制减少请求和 UI 阻塞，不构成吞吐量指标承诺。

## 运行链路

帮助菜单的插件市场与反馈入口支持菜单访问键，见[帮助菜单键盘入口](./menus.md#帮助菜单的键盘入口)。插件市场进入后聚焦列表，Esc 优先收起详情并回到列表，再次按下关闭窗口。反馈窗口初始聚焦正文输入框，保留多行 Enter 换行及原有默认发送、Esc 取消行为；Tab / Shift+Tab 在窗口内循环。

| 链路 | 关键路径 |
| --- | --- |
| 设置链 | [MenuOptions → SettingWindow/controller → 元数据与编辑器 → 菜单返回后保存](./settings.md) |
| 向导链 | [App 分流 → WizardManager 发现 → WizardWindow 的 Refresh/Apply/initializer → 标记与保存](./wizards.md) |
| 市场链 | `MarketplaceWindow` -> `MarketplaceClient` -> Markdown/WebView2 -> 下载/安装服务 |
| 下载链 | `DownloadWindow` -> `Aria2cDownloadManager` -> `aria2c.exe` / RPC daemon |
| 崩溃诊断链 | `SettingWindow` -> `CrashDumpSettingsProvider` -> 通用属性编辑器 -> `ColorVisionServiceHost` / WER LocalDumps / `DumpHelper` |
| 反馈收集链 | `FeedbackWindow` -> `IFeedbackLogCollector` -> 应用日志、系统信息、脱敏配置快照、Dump、Windows 事件日志 -> 匿名 `/api/feedback` |
| 菜单管理链 | [MenuItemManagerWindow → 草稿 → CommitEditingSnapshot → 运行时覆盖/重建 → 尝试保存](./menus.md) |
| DLL 诊断链 | `ViewDllVersionsWindow` |

反馈主窗口固定显示诊断项摘要、日志范围、打开来源目录、清理历史文件和打包入口，附件区滚动不会带走这些操作。“选择项目”打开独立的紧凑列表，支持按名称或说明搜索、全部选中／取消和恢复默认选择；这些批量操作作用于全部项目，不受搜索过滤影响，关闭或按 Esc 保留选择。范围统一应用于实现 `IFeedbackLogTimeRangeCollector` 的日志和数据库来源，目录菜单也仅列出该接口提供的目录，不支持的来源保持自己的收集规则。清理与打包勾选独立，仅交给实现 `IFeedbackDiagnosticCleanupSource` 的来源；无对应能力时禁用入口。打包期间禁止修改诊断选项或启动清理，避免修改正在使用的收集器。

反馈窗口从帮助菜单、启动恢复或 Copilot `/feedback` 打开时均使用非模态 `Show()`，保留 Owner 与居中定位。打包和上传期间可最小化反馈窗口、切回其他窗口继续操作；打包仍在后台任务中执行，HTTP 上传仍异步等待。Copilot 附带的临时会话文件保留到反馈窗口关闭，不能在 `Show()` 返回时提前清理。

点击“发送反馈”后匿名提交。本地 RBAC、Windows 用户名和机器名仅用于诊断信息，不构成 Web 账号归属。客户端同时提交结构化 `machineName`、`clientSubmittedAt`，日志包实际完成时才提交 `diagnosticsCollectedAt`；后续查看和下载见[反馈归属、查询与诊断附件下载](../../02-developer-guide/backend/feedback.md)。

默认选中的配置收集器把当前 `ConfigHandler.ConfigFilePath` 读取为 JSON，在反馈 ZIP 中写为 `Config/ColorVisionConfig.json`。“流程前后处理配置”同时收集 `PreProcessConfig.json`、`PostProcessConfig.json`；加载 ProjectARVRPro 后，“ARVRPro 流程配置”按项目实际配置目录收集 `ProjectARVRProProcessGroups.json`，保留流程组、切图等待、相机覆盖参数与 Recipe。配置始终采集当前已保存文件，不受日志天数或文件修改时间限制，也不扫描历史备份、其他项目、认证文件或整个配置目录。

配置收集统一使用 `FeedbackConfigurationSnapshot`，支持对象、数组和 `ConfigJson` 等嵌套 JSON 字符串；只解析 JSON 数据，不实例化 `$type` 指定的类型。它保留诊断字段，但递归遮盖名称表示密码、Token、Secret、API Key、连接字符串、凭据或 Cookie 的值；嵌套 JSON 字段无法解析时遮盖该字段，整份文件无效时仅附不含原始内容的 `.collection-error.txt`，其他文件继续收集。可选文件不存在时跳过，原始文件不直接进入反馈包。

本地运行数据库通过同一 `IFeedbackLogCollector` 发现链加入诊断项，默认勾选、最近 7 天，在主窗口统一选择 1／3／7／14／30 天。“流程与节点耗时记录”导出 `FlowNodeRecords.db`，“Socket 通信记录”导出 `SocketMessages.db`，“MQTT 服务通信记录”导出 `MsgRecords.db` 中按发送、接收、创建或更新时间命中的请求、响应和超时状态，保留完整正文；加载 ProjectARVRPro 后还会出现“ARVRPro 测试与阶段耗时记录”，导出 `ProjectARVRPro.db`。路径来自各模块当前配置，不依赖固定安装目录；收集器不会初始化业务管理器或迁移源数据库。

数据库按记录自身的本地时间／UTC 字段筛选，保留关联运行、节点、事件、异常、模板快照和结果，因此部分关联记录可以早于所选起始时间。输出是反馈 ZIP 的 `Database/` 下可直接查询的独立 SQLite 文件，保留原字段、ID、索引及完整压缩正文，不跟随结果中的图片路径收集图片。源库以只读模式在单个读取事务中访问，包含读取快照时 WAL 中已提交的数据；不同数据库之间不提供统一事务快照，尚未落库的写入队列不属于导出范围。

每个数据库附带同名 `.export.json`，记录所选时间范围（含时区）、各表条数、缺失表和失败原因。旧库缺少新诊断表时仍导出可用表并标为 `partial`；无法识别时间字段或数据库不可读时只附错误说明，不回退为全库收集。导出逻辑与模块筛选分别位于 `UI/ColorVision.Database/SqliteFeedbackCollector.cs` 和各模块的反馈收集器；回归入口为 `Test/ColorVision.UI.Tests/SqliteFeedbackCollectorTests.cs`、`Test/ProjectARVRPro.Tests/ProjectARVRProFeedbackCollectorTests.cs`。打包与发送沿用反馈窗口入口，附件上传按流读取；离线时仍可选中压缩包并打开所在目录。

“清理历史文件”先要求用户确认，再调用实现 `IFeedbackDiagnosticCleanupSource` 的诊断来源。应用和服务日志保留各活动目录中最新的文件，其他来源只返回点击清理前已经存在的历史文件；删除失败的占用或无权限文件会计入跳过数量。清理仅删除来源明确声明的文件，不删除目录、配置、数据库、反馈附件或已经打包的 ZIP；共享的 WER Dump 目录中也只匹配当前 ColorVision 进程命名的 `.dmp`。

## 新增功能检查

窗口能打开只证明入口可用，不等于[配置已持久化和发布](./configuration.md)、[安装替换成功](../../02-developer-guide/plugin-development/getting-started.md)或插件已加载。观察下载日志、目标路径和包版本是诊断；下载替换、安装更新、修改配置和系统状态需要相应授权，不因为“验证”而自动执行。

| 要做什么 | 检查点 |
| --- | --- |
| 新增设置页 | 按[设置契约](./settings.md)检查发现缓存、搜索、绑定、页面生命周期与保存调用者 |
| 新增向导步骤 | 按[向导契约](./wizards.md)检查排序、Refresh/Apply 副作用、initializer 时序与完成条件 |
| 新增市场/下载能力 | 核对任务结果、目标文件及服务提供的完整性/版本校验；下载成功不等于宿主已加载插件，装载问题继续查 PluginLoader 与 manifest。WebView2、Markdown CSS、`aria2c.exe`、目录权限和错误提示分别验证 |
| 新增第三方应用入口 | 路径、权限、图标、分组、右键入口和不存在时的提示都验证 |
| 修改崩溃诊断 | 普通用户通过 `ColorVisionServiceHost` 写入/清除 HKLM；手动保存不提权；反馈包只收集大小和时间范围内的文件 |
| 修改反馈文件清理 | 每个来源只枚举自己拥有的普通文件，保留活动日志并限制 Dump 的进程名前缀；验证去重、释放空间统计、占用/权限跳过和最小窗口布局 |

## 发布验收

| 验收项 | 要查什么 |
| --- | --- |
| 目标框架 | `ColorVision.UI.Desktop.csproj` 的 `net10.0-windows7.0`、`OutputType=WinExe` |
| 包内 README | `PackageReadmeFile`、包根目录 |
| 项目依赖 | `ColorVision.UI`、`ColorVision.Database` 等基础壳层依赖 |
| WebView/Markdown | `Microsoft.Web.WebView2`、`Markdig.Signed`、`Assets/css/github-markdown.css` |
| 下载工具 | `Assets/Tool/aria2c.exe` 能进入输出目录并可启动 |
| 设置窗口 | 设置分组、搜索、懒加载 View、保存和重启恢复正常 |
| 向导窗口 | `IWizardStep` 能发现，排序和完成状态正常 |
| 诊断窗口 | 能列出程序集版本、文件版本、产品版本和路径 |
| 崩溃诊断 | 设置页可发现；Mini/Full/Custom 保存成功；旧 `EventVWR` 插件即使残留也不会再加载 |

## 边界

- 本项目的 `App.xaml.cs` 为空实现，`App.xaml` 未设置 `StartupUri`；`MainWindow.xaml` 仅有空 `Grid`，构造器只调用 `InitializeComponent()`。声明 `WinExe` 不代表包含完整产品启动、单实例、首次向导或 AvalonDock 主窗口；真正的[宿主启动链](../../03-architecture/overview/runtime.md)在 `ColorVision/`。
- Windows 事件查看器直接由“第三方应用”启动 `eventvwr.msc`。
- 普通用户模式下，写入或清除 `HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps` 由 `ColorVisionServiceHost` 执行；管理员模式可直接写入，手动保存 Dump 不修改系统配置。
- 特权服务的 `registry-set-values` / `registry-delete-key` 是通用 HKLM 写入接口，不限制到 WER 路径，并支持显式选择 32/64 位注册表视图；所有调用仍须通过调用方身份校验、单次 Broker Ticket，并写入不含值数据的审计日志。
- 这里是窗口和管理工具集合，不是所有菜单、插件或配置运行时的唯一中心。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 设置窗口 | `Settings/SettingWindow.xaml.cs`、`Settings/SettingWindowController.cs` |
| 向导流程 | `Wizards/WizardWindow.xaml.cs`、`Wizards/WizardWindowConfig.cs` |
| 菜单管理 | `MenuItemManager/MenuItemManagerConfig.cs`、`MenuItemManagerWindow.xaml.cs` |
| 插件市场和下载 | `Marketplace/`、`Download/`、`WebViewService.cs` |
| 第三方应用 | `ThirdPartyApps/SystemAppProvider.cs`、`ThirdPartyAppsWindow.xaml.cs` |
| 主程序网卡工具 | `ColorVision/ToolPlugins/ThirdPartyApps/InternalAppProvider.cs`、`NetworkAdapterPriorityService.cs`、`NetworkAdapterPriorityWindow.xaml.cs` |
| 崩溃与反馈诊断 | `Diagnostics/`、`Feedback/`、`ColorVision.Common/NativeMethods/DumpHelper.cs` |

## 验证入口与缺口


下载生命周期测试使用临时 SQLite 与模拟 RPC，覆盖迟到响应、暂停恢复、同名任务、文件保护、内容校验、分页和磁力子任务。`DownloadAria2IntegrationTests` 使用随包的真实 aria2 与临时本地 HTTP 服务，验证下载、暂停续传及完成文件提升；它不访问外部站点，不能替代真实 P2P、代理环境、大文件吞吐量或窗口交互验收。其他自动化测试只覆盖各自受测服务；外部联网下载、HKLM 写入、DNS 修改和反馈上传都需明确授权，不能作为默认文档验证步骤。

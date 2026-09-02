---
knowledge_id: "ui.desktop"
knowledge_type: "reference"
status: "current"
summary: "桌面辅助壳层而非产品主入口：定位设置、市场下载、第三方工具、反馈和特权崩溃诊断。"
aliases: ["设置窗口和插件市场在哪里","ColorVision.UI.Desktop","SettingWindow","MarketplacePackageDownloadService"]
code_paths: ["UI/ColorVision.UI.Desktop/ColorVision.UI.Desktop.csproj","UI/ColorVision.UI.Desktop/App.xaml","UI/ColorVision.UI.Desktop/App.xaml.cs","UI/ColorVision.UI.Desktop/MainWindow.xaml","UI/ColorVision.UI.Desktop/MainWindow.xaml.cs","UI/ColorVision.UI.Desktop/Settings/SettingWindow.xaml.cs","UI/ColorVision.UI.Desktop/Marketplace","UI/ColorVision.UI.Desktop/Download","UI/ColorVision.UI.Desktop/Wizards","UI/ColorVision.UI.Desktop/ThirdPartyApps","UI/ColorVision.UI.Desktop/Diagnostics","UI/ColorVision.UI.Desktop/Feedback","UI/ColorVision.UI.Desktop/README.md"]
test_paths: ["Test/ColorVision.UI.Tests/MarketplacePackageDownloadServiceTests.cs","Test/ColorVision.UI.Tests/FeedbackWindowLayoutTests.cs","Test/ColorVision.UI.Tests/NetworkAdapterPriorityServiceTests.cs"]
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
| 下载失败或卡住 | `Assets/Tool/aria2c.exe`、RPC 端口、旧 aria2c 进程 |
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
| 反馈诊断 | `Feedback/`、`Feedback/Collectors/WindowsEventLogCollector` | 打包应用日志、系统信息、Dump 和 Windows Application/System 警告或错误 |
| 诊断窗口 | `ViewDllVersionsWindow` | 查看已加载程序集版本、产品版本和路径 |

## 运行链路

| 链路 | 关键路径 |
| --- | --- |
| 设置链 | [MenuOptions → SettingWindow/controller → 元数据与编辑器 → 菜单返回后保存](./settings.md) |
| 向导链 | [App 分流 → WizardManager 发现 → WizardWindow 的 Refresh/Apply/initializer → 标记与保存](./wizards.md) |
| 市场链 | `MarketplaceWindow` -> `MarketplaceClient` -> Markdown/WebView2 -> 下载/安装服务 |
| 下载链 | `DownloadWindow` -> `Aria2cDownloadManager` -> `aria2c.exe` / RPC daemon |
| 崩溃诊断链 | `SettingWindow` -> `CrashDumpSettingsProvider` -> 通用属性编辑器 -> `ColorVisionServiceHost` / WER LocalDumps / `DumpHelper` |
| 反馈收集链 | `FeedbackWindow` -> `IFeedbackLogCollector` -> 应用日志、系统信息、Dump、Windows 事件日志 |
| 菜单管理链 | [MenuItemManagerWindow → 草稿 → CommitEditingSnapshot → 运行时覆盖/重建 → 尝试保存](./menus.md) |
| DLL 诊断链 | `ViewDllVersionsWindow` |

## 新增功能检查

窗口能打开只证明入口可用，不等于[配置已持久化和发布](./configuration.md)、[安装替换成功](../../02-developer-guide/plugin-development/getting-started.md)或插件已加载。下表也用于回答运行问题，不再另维护一份 UI 使用手册。观察下载日志、目标路径和包版本是诊断；下载替换、安装更新、修改配置和系统状态需要相应授权，不因为“验证”而自动执行。

| 要做什么 | 检查点 |
| --- | --- |
| 新增设置页 | 按[设置契约](./settings.md)检查发现缓存、搜索、绑定、页面生命周期与保存调用者 |
| 新增向导步骤 | 按[向导契约](./wizards.md)检查排序、Refresh/Apply 副作用、initializer 时序与完成条件 |
| 新增市场/下载能力 | 核对任务结果、目标文件及服务提供的完整性/版本校验；下载成功不等于宿主已加载插件，装载问题继续查 PluginLoader 与 manifest。WebView2、Markdown CSS、`aria2c.exe`、目录权限和错误提示分别验证 |
| 新增第三方应用入口 | 路径、权限、图标、分组、右键入口和不存在时的提示都验证 |
| 修改崩溃诊断 | 普通用户通过 `ColorVisionServiceHost` 写入/清除 HKLM；手动保存不提权；反馈包只收集大小和时间范围内的文件 |

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
- 旧文档里的 `SystemInitializer` 不在当前 `UI/ColorVision.UI.Desktop` 目录中。
- Windows 事件查看器直接由“第三方应用”启动 `eventvwr.msc`；不再维护 `EventWindow` 内嵌控件。
- “应用与工具”不再内置 `CVRaw To CSV` 和 `DAT File Reader`；通用 CVRAW/CVCIE 文件读取仍由 Engine/FileIO 与图像编辑器负责。
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

关联测试：`Test/ColorVision.UI.Tests/MarketplacePackageDownloadServiceTests.cs`、`Test/ColorVision.UI.Tests/FeedbackWindowLayoutTests.cs`、`Test/ColorVision.UI.Tests/NetworkAdapterPriorityServiceTests.cs`。

自动化测试只覆盖各自受测服务；联网下载、HKLM 写入、DNS 修改和反馈上传都需明确授权，不能作为默认文档验证步骤。

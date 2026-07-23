# ColorVision.UI.Desktop

`UI/ColorVision.UI.Desktop/` 是桌面侧辅助壳层功能集合，包含设置、向导、菜单管理、配置管理、插件市场、下载、第三方应用入口、反馈和崩溃诊断。它不是整个产品主入口；真正主程序在 `ColorVision/`。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 设置页为空或少项 | `ConfigSettingManager` 是否收集到 Provider，Provider 所在程序集是否加载 |
| 自定义设置 View 不显示 | `SettingWindow` 懒加载、`ViewType`、构造函数和资源字典 |
| 向导步骤不出现 | 是否实现 `IWizardStep`，程序集是否加载，`Order` 是否异常 |
| 插件市场 README/CHANGELOG 空白 | WebView2 初始化、Markdown CSS、内容是否为空 |
| 下载失败或卡住 | `Assets/Tool/aria2c.exe`、RPC 端口、旧 aria2c 进程 |
| DLL 版本窗口缺少条目 | 目标程序集是否已加载到当前进程 |
| 第三方应用打不开 | `SystemAppProvider` / 自定义应用路径、权限和系统工具是否存在 |
| Dump 设置失败 | `ColorVisionServiceHost` 是否已安装且为当前版本、`Diagnostics/CrashDumpConfiguration.cs` 的 HKLM 目标项和保存目录 |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 设置窗口 | `Settings/SettingWindow.xaml.cs` | 从 `ConfigSettingManager` 取设置项，按分组生成 Tab，支持自定义 View 懒加载 |
| 配置管理 | `ConfigManagerWindow.xaml(.cs)` | 桌面侧集中配置管理窗口 |
| 向导流程 | `WizardManager`、`WizardWindow`、`WizardWindowConfig` | 扫描 `IWizardStep`，按 `Order` 排序并驱动步骤切换 |
| 菜单项管理 | `MenuItemManagerConfig`、`MenuItemManagerWindow` | 管理菜单项设置与持久化 |
| 插件市场 | `MarketplaceWindow`、`MarketplaceClient`、`MarketplacePackageDownloadService` | 展示市场内容、Markdown、下载和安装入口 |
| 下载管理 | `Aria2cDownloadManager`、`DownloadWindow` | 使用内置 `aria2c.exe` 管理下载 |
| 第三方应用 | `SystemAppProvider`、`CustomAppProvider`、`ThirdPartyAppsWindow` | 系统工具、自定义应用和磁盘 Treemap 入口 |
| 崩溃诊断 | `Diagnostics/CrashDumpSettingsControl`、`CrashDumpConfiguration` | 通过通用属性反射生成 WER LocalDumps 设置，由后台特权服务写入 HKLM；支持手动保存当前进程 Dump 和反馈包收集 |
| 反馈诊断 | `Feedback/`、`Feedback/Collectors/WindowsEventLogCollector` | 打包应用日志、系统信息、Dump 和 Windows Application/System 警告或错误 |
| 诊断窗口 | `ViewDllVersionsWindow` | 查看已加载程序集版本、产品版本和路径 |

## 运行链路

| 链路 | 关键路径 |
| --- | --- |
| 设置链 | `SettingWindow` -> `ConfigSettingManager` -> 配置页/属性页懒加载 |
| 向导链 | `WizardManager` -> `IWizardStep` 发现 -> `WizardWindow` 切换与完成 |
| 市场链 | `MarketplaceWindow` -> `MarketplaceClient` -> Markdown/WebView2 -> 下载/安装服务 |
| 下载链 | `DownloadWindow` -> `Aria2cDownloadManager` -> `aria2c.exe` / RPC daemon |
| 崩溃诊断链 | `SettingWindow` -> `CrashDumpSettingsProvider` -> 通用属性编辑器 -> `ColorVisionServiceHost` / WER LocalDumps / `DumpHelper` |
| 反馈收集链 | `FeedbackWindow` -> `IFeedbackLogCollector` -> 应用日志、系统信息、Dump、Windows 事件日志 |
| 管理链 | `MenuItemManagerWindow`、`ConfigManagerWindow`、`ViewDllVersionsWindow` |

## 新增功能检查

| 要做什么 | 检查点 |
| --- | --- |
| 新增设置页 | Provider 能被收集；自定义 View 可构造；保存和重启恢复正常 |
| 新增向导步骤 | 实现 `IWizardStep`；程序集被加载；排序、前后切换、完成条件正常 |
| 新增市场/下载能力 | WebView2、Markdown CSS、`aria2c.exe`、下载目录和错误提示都能闭环 |
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

- `App.xaml.cs` 和 `MainWindow.xaml.cs` 很轻，不要把本项目写成主程序启动中心。
- 旧文档里的 `SystemInitializer` 不在当前 `UI/ColorVision.UI.Desktop` 目录中。
- Windows 事件查看器直接由“第三方应用”启动 `eventvwr.msc`；不再维护 `EventWindow` 内嵌控件。
- 普通用户模式下，写入或清除 `HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps` 由 `ColorVisionServiceHost` 执行；管理员模式可直接写入，手动保存 Dump 不修改系统配置。
- 特权服务的 `registry-set-values` / `registry-delete-key` 是通用 HKLM 写入接口，不限制到 WER 路径，并支持显式选择 32/64 位注册表视图；所有调用仍须通过调用方身份校验、单次 Broker Ticket，并写入不含值数据的审计日志。
- 这里是窗口和管理工具集合，不是所有菜单、插件或配置运行时的唯一中心。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 设置和配置窗口 | `Settings/SettingWindow.xaml.cs`、`ConfigManagerWindow.xaml.cs` |
| 向导流程 | `Wizards/WizardWindow.xaml.cs`、`Wizards/WizardWindowConfig.cs` |
| 菜单管理 | `MenuItemManager/MenuItemManagerConfig.cs`、`MenuItemManagerWindow.xaml.cs` |
| 插件市场和下载 | `Marketplace/`、`Download/`、`WebViewService.cs` |
| 第三方应用 | `ThirdPartyApps/SystemAppProvider.cs`、`ThirdPartyAppsWindow.xaml.cs` |
| 崩溃与反馈诊断 | `Diagnostics/`、`Feedback/`、`ColorVision.Common/NativeMethods/DumpHelper.cs` |

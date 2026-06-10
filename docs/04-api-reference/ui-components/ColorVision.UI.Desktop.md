# ColorVision.UI.Desktop

本页只描述 UI/ColorVision.UI.Desktop 当前已经落地的桌面端窗口和配套功能，不再延续旧文档里那种“整个系统主程序入口”的写法。

## 模块定位

`ColorVision.UI.Desktop` 当前更接近桌面侧辅助壳层功能集合，主要提供：

- 设置窗口
- 配置向导
- 菜单项管理窗口
- 配置管理窗口
- 第三方应用接入
- DLL 信息查看等辅助窗口

它不是整个仓库的主应用入口。当前真正的主程序项目在 `ColorVision/`，而这里的 `App.xaml.cs` 和 `MainWindow.xaml.cs` 都非常轻。

## 当前最关键的目录

从项目目录看，最值得优先阅读的是：

- `Settings/`：统一设置窗口
- `Wizards/`：向导窗口、步骤发现、窗口配置
- `MenuItemManager/`：菜单项管理与持久化
- `ThirdPartyApps/`：系统工具和第三方应用入口
- `Marketplace/`：DLL 版本查看等辅助窗口
- `ConfigManagerWindow.xaml(.cs)`：配置管理窗口
- `Feedback/`、`Download/`、`TimedButtons/`、`WebViewService.cs`：其他桌面辅助能力

## 关键入口类型

### App 与 MainWindow

当前 `App.xaml.cs` 只是一个很轻的 `Application` partial，`MainWindow.xaml.cs` 也只保留基础构造逻辑。

这说明：

- 这个项目里确实有 `App` 和 `MainWindow`
- 但它们并不是旧文档描述的那种承载完整启动流程和系统初始化逻辑的中心文件

阅读这个项目时，真正更值得先看的是各个功能窗口和管理器，而不是把注意力放在空壳入口上。

### SettingWindow

`Settings/SettingWindow.xaml.cs` 是当前设置系统的主要桌面入口。它负责：

- 读取 `ConfigSettingManager.GetInstance().GetAllSettings()`
- 按分组创建 Tab
- 根据 `ConfigSettingType` 决定生成 Tab 页、整类属性页或单属性控件
- 对 `ViewType` 做懒加载，避免窗口初始化时把所有视图一次性实例化

因此这页旧文档里“统一设置窗口”这个方向是对的，但实现细节应当落到 `ConfigSettingManager` + 惰性加载上。

### WizardManager / WizardWindow / WizardWindowConfig

当前向导链是这组类型：

- `WizardManager`：反射扫描 `IWizardStep`
- `WizardWindow`：多步骤窗口与完成逻辑
- `WizardWindowConfig`：窗口配置和完成状态

`WizardManager` 会遍历程序集并实例化 `IWizardStep`，然后按 `Order` 排序；`WizardWindow` 会驱动进度条、前后步骤切换和完成验证。

这部分是当前项目里最明确的一条“桌面辅助流程链”。

### MenuItemManagerConfig 与 MenuItemManagerWindow

`MenuItemManagerConfig` 当前负责菜单项设置的持久化，而 `MenuItemManagerWindow` 则提供可视化管理界面。它们属于 UI 壳层配置工具，而不是全局菜单运行时本身。

### ConfigManagerWindow

`ConfigManagerWindow` 是另一个桌面侧管理窗口，用来从更集中视角管理配置项。它和 `SettingWindow` 不完全重合，属于桌面工具层而不是基础接口层。

### ViewDllVersionsWindow

`Marketplace/ViewDllVersionsWindow.xaml.cs` 当前会遍历已加载程序集，收集：

- 名称
- 程序集版本
- 文件版本
- 产品版本
- 公司信息
- 路径

它更像一个运行时诊断和排查窗口，而不是插件更新核心流程本身。

### SystemAppProvider 与 WebViewService

- `ThirdPartyApps/SystemAppProvider.cs` 负责系统工具和第三方应用入口。
- `WebViewService.cs` 则表明这个项目还承载了一部分桌面 WebView 相关能力。

## 当前运行时主链

这个项目当前没有单一主链，而是几条桌面辅助链并存。更值得关注的是：

1. 设置链：`SettingWindow` -> `ConfigSettingManager` -> 配置页/属性页懒加载。
2. 向导链：`WizardManager` -> `IWizardStep` 发现 -> `WizardWindow` 切换与完成。
3. 管理链：`MenuItemManagerWindow` / `ConfigManagerWindow` / `ViewDllVersionsWindow` 提供不同侧面的桌面管理窗口。

## 作为 DLL/桌面工具包使用时

### 应该引用它的场景

- 需要统一设置窗口或配置导入导出。
- 需要首次配置向导、安装向导或多步骤引导流程。
- 需要插件市场、DLL 版本查看、下载管理、第三方应用入口。
- 需要桌面侧反馈窗口、WebView Markdown 展示或定时按钮操作统计。

### 新增设置页

1. 在业务配置类或 Provider 中暴露设置元数据。
2. 确认 `ConfigSettingManager` 能收集到设置项。
3. 如果是自定义 View，确认 `SettingWindow` 的懒加载能实例化该 View。
4. 打开设置窗口验证分组、保存和重启后的恢复。

### 新增向导步骤

1. 实现 `IWizardStep`。
2. 设置 `Order` 和显示信息。
3. 让步骤所在程序集被加载。
4. 打开 `WizardWindow` 验证排序、前后切换、完成条件和完成状态。

### 发布注意

`ColorVision.UI.Desktop` 当前会打包 `Assets/css/github-markdown.css` 和 `Assets/Tool/aria2c.exe`。市场、下载、Markdown 展示相关问题要同时检查这些资源是否复制到输出目录。

### DLL/工具包发布验收表

| 验收项 | 要查什么 | 通过标准 |
| --- | --- | --- |
| 目标框架产物 | `net10.0-windows7.0`、`OutputType=WinExe` | 能生成桌面工具程序集、`.nupkg`、`.snupkg` |
| 包内 README | `PackageReadmeFile`、包根目录 | `README.md` 随包进入根目录 |
| 项目依赖 | `ColorVision.UI`、`ColorVision.Database` | 设置窗口、数据库配置和基础壳层依赖能解析 |
| WebView2/Markdown | `Microsoft.Web.WebView2`、`Markdig.Signed`、`Assets/css/github-markdown.css` | 插件市场或 Markdown 预览能初始化并使用 CSS |
| 下载工具 | `Assets/Tool/aria2c.exe`、`Aria2cDownloadManager` | 输出目录能找到 `aria2c.exe`，下载窗口能启动或复用 RPC daemon |
| 设置窗口 | `SettingWindow`、`ConfigSettingManager` | 设置分组、搜索、懒加载 View、保存和重启恢复正常 |
| 向导窗口 | `WizardManager`、`WizardWindow` | `IWizardStep` 能被发现，排序、前后切换、完成状态正常 |
| 诊断窗口 | `ViewDllVersionsWindow` | 能列出已加载程序集、文件版本、产品版本和路径 |

### 现场故障首查

| 现象 | 第一检查点 |
| --- | --- |
| 设置页为空或少项 | 检查 `ConfigSettingManager` 是否收集到 Provider，以及 Provider 所在程序集是否加载 |
| 向导步骤不出现 | 检查步骤是否实现 `IWizardStep`，程序集是否被加载，`Order` 是否异常 |
| 插件市场 README/CHANGELOG 空白 | 先查 WebView2 初始化，再查 Markdown CSS 是否复制到 `Assets/css/` |
| 下载失败或卡住 | 检查 `Assets/Tool/aria2c.exe`、RPC 端口和是否有旧的 aria2c 进程占用 |
| DLL 版本窗口缺少条目 | 检查目标程序集是否真的加载到当前 AppDomain |
| 第三方应用入口打不开 | 检查 `SystemAppProvider` 生成的路径、权限和系统工具是否存在 |

## 当前实现有哪些边界

### 不是整个系统主入口

这是这页最容易写错的地方。当前项目里的 `App` 和 `MainWindow` 都很轻，不能继续把 `ColorVision.UI.Desktop` 讲成整个产品唯一的启动中心。

### 不是所有功能都围绕 MainWindow

这个项目更像一组窗口和管理工具集合。很多价值来自独立窗口，而不是一个庞大的主窗口编排层。

### 旧文档提到的 SystemInitializer 在这个项目里并不存在

当前 `UI/ColorVision.UI.Desktop` 目录里并没有实际的 `SystemInitializer` 实现。旧文档把它列为现有组件，会直接误导读者去找一个不存在的控制点。

## 当前更适合怎样读这个模块

### 想看设置和配置窗口

先看：

- `Settings/SettingWindow.xaml.cs`
- `ConfigManagerWindow.xaml.cs`

### 想看向导和首次配置流程

先看：

- `Wizards/WizardWindow.xaml.cs`
- `Wizards/WizardWindowConfig.cs`

### 想看菜单管理和桌面辅助窗口

先看：

- `MenuItemManager/MenuItemManagerConfig.cs`
- `MenuItemManager/MenuItemManagerWindow.xaml.cs`
- `Marketplace/ViewDllVersionsWindow.xaml.cs`
- `ThirdPartyApps/SystemAppProvider.cs`

## 这页不再做什么

本页不再继续维护这些高风险内容：

- 把本项目写成整个系统主程序入口
- 不存在组件的说明，例如 `SystemInitializer`
- 大段版本号和伪 API 列表
- 把轻量 `App` / `MainWindow` 扩写成完整启动流程中心

## 继续阅读

- [UI组件概览](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Solution](./ColorVision.Solution.md)

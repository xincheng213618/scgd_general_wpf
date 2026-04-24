# ColorVision.UI.Desktop

> 版本: 1.5.5.1 | 目标框架: .NET 10.0 Windows | 输出类型: WinExe

## 功能定位

桌面应用程序入口模块，提供主窗口、设置管理、配置向导、菜单定制和第三方应用集成。

## 主要功能

### 应用入口 (App.xaml.cs)
- 全局异常捕获、单实例检查、启动参数处理
- 首次运行显示配置向导

### 主窗口 (MainWindow.xaml)
- 基于 AvalonDock 的停靠面板布局
- 文档视图 + 面板视图管理
- 动态菜单加载（MenuManager）

### 设置管理 (Settings/)
- **SettingWindow** — 统一设置窗口，自动发现 IConfigSettingProvider
- 支持 TabItem / Class / Property 三种设置类型

### 配置向导 (Wizards/)
- **WizardWindow** — 分步骤初始化向导
- **WizardManager** — 自动发现 IWizardStep 实现
- **WizardWindowConfig** — 向导完成状态持久化

### 菜单管理 (MenuItemManager/)
- **MenuItemManagerWindow** — 可视化菜单结构编辑
- **MenuItemManagerConfig** — 菜单配置持久化

### 第三方应用 (ThirdPartyApps/)
- **SystemAppProvider** — Windows 系统工具集合

### 其他
- **ShortcutCreator** — 创建 .lnk 快捷方式
- **SystemInitializer** — CUDA 初始化 + 系统信息记录

## 文件清单

| 文件 | 说明 |
|------|------|
| `App.xaml.cs` | 应用入口 |
| `MainWindow.xaml.cs` | 主窗口 |
| `ShortcutCreator.cs` | 快捷方式创建 |
| `WizardWindow.xaml.cs` | 配置向导窗口 |
| `WizardWindowConfig.cs` | 向导配置 |
| `SystemAppProvider.cs` | 系统应用提供者 |
| `MenuItemManagerConfig.cs` | 菜单管理配置 |

## 依赖关系

- **引用**: ColorVision.UI, ColorVision.Database, Markdig.Signed, Microsoft.Web.WebView2
- **被引用**: ColorVision.Solution

## 构建

```bash
dotnet build UI/ColorVision.UI.Desktop/ColorVision.UI.Desktop.csproj
dotnet run --project UI/ColorVision.UI.Desktop/ColorVision.UI.Desktop.csproj
```

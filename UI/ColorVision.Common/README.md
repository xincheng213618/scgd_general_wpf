# ColorVision.Common

> 版本: 1.5.5.1 | 目标框架: .NET 8.0 / .NET 10.0 Windows

## 功能定位

ColorVision 系统的通用基础框架库，提供 MVVM 架构、接口定义、命令系统、工具类和基础服务。作为最底层库，被所有上层模块依赖。

## 主要功能

### MVVM 架构
- **ViewModelBase** — INotifyPropertyChanged 基类，支持 `SetProperty` / `OnPropertyChanged`
- **RelayCommand / RelayCommand\<T\>** — 带参数的命令实现
- **ActionCommand** — 支持 Action 命令 + Undo/Redo

### 接口定义
| 接口 | 用途 |
|------|------|
| `IMenuItem` / `IMenuItemProvider` | 菜单系统 |
| `IPlugin` / `IPluginBase` | 插件系统 |
| `IConfig` / `IConfigSettingProvider` | 配置管理 |
| `IView` / `IViewManager` | 视图管理 |
| `IStatusBarProvider` / `IStatusBarProviderUpdatable` | 状态栏 |
| `ISearch` / `ISearchProvider` | 搜索功能 |
| `IInitializer` / `InitializerBase` | 初始化系统 |
| `IWizardStep` | 向导步骤 |
| `IFileProcessor` | 文件处理 |
| `IThumbnailProvider` | 缩略图 |
| `IDockPanelProvider` | 停靠面板 |
| `IFeatureLauncher` | 功能启动器 |
| `IFeedbackLogCollector` | 反馈日志 |
| `IThirdPartyAppProvider` | 第三方应用 |

### 权限管理
- **AccessControl** — 访问控制
- **RequiresPermissionAttribute** — `[RequiresPermission(PermissionMode.Administrator)]`
- **PermissionMode** — None / User / Administrator

### 工具类
| 工具 | 用途 |
|------|------|
| `FileUtils` | 文件操作（EnsureDirectory、SafeDelete、GetFileSizeText） |
| `ImageUtils` | 图像处理 |
| `Cryptography` | MD5/SHA256/AES 加密 |
| `RegexUtils` / `RegUtils` | 正则/注册表 |
| `WindowHelpers` / `WindowUtils` | 窗口操作 |
| `CollectionUtils` / `DictionaryUtils` / `StringUtils` / `EnumUtils` | 集合/字典/字符串/枚举 |
| `DebounceTimer` / `TaskConflator` | 异步工具（防抖、任务合并） |
| `PlatformHelper` | 平台检测 |
| `CsvWriter` | CSV 写入 |
| `MemorySize` | 内存大小计算 |

### 原生方法
- **User32** / **Dwmapi** / **Shlwapi** — Win32 API 封装
- **Clipboard** / **Keyboard** — 剪贴板/键盘操作
- **ShellFileOperations** — Shell 文件操作
- **Win32DeviceMgmt** — 设备管理

### 配置服务
- **ConfigService** — 配置实例管理（`GetRequiredService<T>()`）
- **WindowConfig** — 窗口配置基类
- **ConfigSettingType** — Property / Class / TabItem

### 其他
- **MenuItemBase** — 菜单项基类（`GlobalMenuBase` 继承自它）
- **ViewGridManager** — 视图网格管理
- **View** — 视图基类
- **StatusBarMeta** / **StatusBarAlignment** / **StatusBarType** — 状态栏元数据
- **SearchBase** / **SearchMeta** / **SearchType** — 搜索基类
- **ThirdPartyAppManager** — 第三方应用管理
- **DebounceTimer** — 防抖定时器
- **TaskConflator** — 任务合并器

## 依赖关系

- **无外部依赖**，仅依赖 .NET 基础库
- **被引用**: ColorVision.UI、ColorVision.Themes、ColorVision.ImageEditor、ColorVision.Scheduler 等所有 UI 模块

## 构建

```bash
dotnet build UI/ColorVision.Common/ColorVision.Common.csproj
```

# ColorVision.Common

本页只描述 UI/ColorVision.Common 当前已经承担的共享基础能力，不再延续旧文档里那种“大而全公共 SDK 接口大全”的写法。

## 模块定位

ColorVision.Common 当前是 UI 层的共享基础库，主要提供这些内容：

- MVVM 基础类型
- 命令封装
- 通用接口和元数据模型
- 粗粒度权限控制
- Windows 原生方法封装
- 常用工具类

它更像一组跨模块复用的基础积木，而不是一个独立运行的业务模块。

## 当前最关键的目录

从项目目录看，最值得先认识的是：

- MVVM/：`ViewModelBase`、`RelayCommand` 等基础类型
- Interfaces/：配置、菜单、状态栏、初始化器、视图等共享接口
- Authorizations/：`Authorization`、`AccessControl`、`PermissionMode`
- NativeMethods/：Windows API 包装
- Utilities/：文件、集合、窗口等常用工具
- Input/：输入相关能力
- ThirdPartyApps/：第三方应用接入相关定义

## 关键入口类型

### ViewModelBase

`ViewModelBase` 是当前最基础的可绑定对象基类，实现了 `INotifyPropertyChanged`，供大量配置类、管理器和视图模型继承。

### RelayCommand 与 Commands

当前命令层主要有两种常用入口：

- `RelayCommand` / `RelayCommand<T>`：通用命令封装
- `Commands`：少量全局 `RoutedUICommand`

旧文档里把命令系统写成一整套独立框架，但从当前代码看，真正高频使用的还是 `RelayCommand`。

### Interfaces/

`Interfaces/` 承担的是共享边界定义，而不是完整业务实现。当前常见的几组接口有：

- `IConfig`、`IConfigSettingProvider`
- `IInitializer`、`InitializerBase`
- `IMenuItemProvider`
- `IStatusBarProvider`、`IStatusBarProviderUpdatable`
- 视图相关类型，如 `View` 和 `IViewManager`

这些类型大多只定义最小契约，真正的注册、发现和执行逻辑通常在上层模块里。

### StatusBarMeta

`StatusBarMeta` 不是旧文档里那种只有图标和命令的简化模型。当前它已经承载了：

- 唯一标识和名称
- 描述文本
- 左右对齐和排序
- `Command` 或 `Popup` 两类动作
- 绑定源对象
- 图标资源或直接图标内容
- 目标窗口范围和默认可见性

所以它已经是 UI 状态栏系统的核心元数据，而不只是一个轻量 DTO。

### Authorization / AccessControl / PermissionMode

`Authorizations/` 下提供的是当前通用的粗粒度权限控制：

- `Authorization.Instance.PermissionMode`
- `AccessControl.Check(...)`
- `RequiresPermissionAttribute`

这里要特别注意边界：

- Common 层只提供全局粗粒度权限模式
- 细粒度本地 RBAC 在 `UI/ColorVision.Solution/Rbac`

不要把 Common 的权限系统写成整个项目唯一的完整 RBAC。

## 当前实现更像什么

ColorVision.Common 当前更接近“共享协议层 + 基础工具层”，而不是一个面向外部用户发布的稳定公共框架。很多接口虽然名字通用，但它们的真实作用是给仓库内的 UI 模块提供统一契约。

例如：

- `IConfig` 本身只是标记接口
- `InitializerBase` 只提供默认名字、顺序和依赖结构
- `View` 是一个带索引、标题和图标的共享 ViewModel，而不是完整视图框架

## 作为 DLL 使用时

### 应该引用它的场景

- 新增 UI 类库，需要 `ViewModelBase`、`RelayCommand` 或共享工具类。
- 新增菜单、状态栏、配置、初始化器等扩展点的契约类型。
- 新增需要跨 UI 模块复用的轻量接口或 Attribute。
- 需要调用 Win32/DWM/文件关联/剪贴板等底层 Windows 包装，但不想让业务项目直接写 P/Invoke。

### 不应该放进这里的内容

- 具体窗口、项目业务、设备业务、Engine 模板逻辑。
- 需要引用 `ColorVision.UI`、`ImageEditor`、`Solution` 才能运行的高层功能。
- 客户项目专用字段、Recipe、Fix、Socket/MES 协议。

### 新增共享接口的检查点

| 检查项 | 说明 |
| --- | --- |
| 是否足够底层 | 接口应能被多个 UI 模块复用，而不是只服务一个窗口 |
| 是否引入反向依赖 | `Common` 不应引用高层 UI DLL |
| 是否有明确实现方 | 只有契约没有任何实现方时，优先放到使用模块内部 |
| 是否需要配置持久化 | 配置服务实现不在 `Common`，只放契约或最小模型 |

### 发布注意

`ColorVision.Common` 是很多 UI 包的根依赖。改动它的 public 类型、命名空间或序列化模型时，要同时检查 `ColorVision.UI`、`ImageEditor`、`Solution`、插件和项目包的编译兼容性。

### DLL 发布验收表

| 验收项 | 要查什么 | 通过标准 |
| --- | --- | --- |
| 目标框架 | `ColorVision.Common.csproj` 的 `net8.0-windows7.0;net10.0-windows7.0` | 两个目标框架都能打出 DLL 和符号包 |
| NuGet 元数据 | `GeneratePackageOnBuild`、`PackageReadmeFile`、`README.md` | 包内包含 README，版本号和主程序依赖一致 |
| 根依赖边界 | 是否新增对 `ColorVision.UI`、`ImageEditor`、`Solution` 等高层项目的引用 | `Common` 仍保持底层共享库角色，没有反向引用 |
| MVVM 基础 | `ViewModelBase`、`RelayCommand`、`Commands` | 属性通知、命令可用性和绑定刷新不回退 |
| 扩展契约 | `IConfig`、菜单、状态栏、初始化器接口 | 上层模块仍能扫描实现类，菜单和状态栏入口不丢失 |
| 权限粗边界 | `AccessControl`、`PermissionMode` | 权限模式变更不会绕过 Solution 本地 RBAC |
| 原生辅助 | `NativeMethods/`、剪贴板、文件关联、游标资源 | Windows API 调用在 x64 和当前系统版本下可用 |

### 现场故障首查

| 现象 | 先查哪里 | 判断要点 |
| --- | --- | --- |
| 升级后多个 UI DLL 同时报 `MissingMethodException` 或 `FileLoadException` | `ColorVision.Common.dll` 版本、public 类型改动、调用方编译时间 | 根依赖被替换后，上层 DLL 可能仍按旧签名编译 |
| 菜单、状态栏或初始化器实现没有出现 | `Interfaces/Menus/`、`Interfaces/StatusBar/`、`Interfaces/IInitializer/` | 先确认接口命名空间和实现程序集是否仍一致 |
| 绑定值变化但界面不刷新 | `ViewModelBase`、属性 setter、`OnPropertyChanged` | 这通常是 MVVM 基类或调用方属性通知问题 |
| 按钮命令状态不更新 | `RelayCommand`、命令 CanExecute 触发点 | 确认调用方是否触发可执行状态刷新 |
| 权限行为和预期不一致 | `AccessControl`、`PermissionMode`、Solution RBAC | Common 只处理粗粒度模式，细权限要回到 Solution |
| 原生调用崩溃或只在某台机器失败 | `NativeMethods/`、平台目标、Windows 版本 | 先排查 x86/x64、DWM/Win32 API 可用性和资源路径 |

## 当前更适合怎样读这个模块

### 想看共享 MVVM 和命令基础

先看：

- `MVVM/ViewModelBase.cs`
- `MVVM/RelayCommand.cs`
- `Commands.cs`

### 想看配置、菜单、状态栏这些公共契约

先看：

- `Interfaces/Config/` 或 `Interfaces/ConfigSetting/`
- `Interfaces/Menus/`
- `Interfaces/StatusBar/`
- `Interfaces/IInitializer/`

### 想看权限边界

先看：

- `Authorizations/AccessControl.cs`
- `Authorizations/PermissionMode.cs`

### 想看原生方法和工具类

先看：

- `NativeMethods/`
- `Utilities/`

## 当前实现的边界

### 不是完整插件平台文档

虽然 Common 里定义了不少扩展接口，但真正的插件发现、菜单注册、配置聚合、状态栏刷新都分散在上层模块实现里。这里只是共享契约，不应被写成统一运行时中心。

### 不是完整权限中心

Common 里的权限检查适合做全局模式开关或粗粒度限制，但并不等于 Solution 侧的本地 RBAC。

### 很多接口是“最小形状”而不是“最终抽象”

像 `IConfig`、`IInitializer` 这类接口很轻，后续阅读时应优先顺着实现方去看真实控制链，而不是停留在接口定义本身。

## 这页不再做什么

本页不再继续维护这些高风险内容：

- 大段版本号和包发布信息
- 理想化的公共 SDK 清单
- 把所有接口都扩写成完整框架能力
- 把 Common 权限系统误写成全局唯一 RBAC

## 继续阅读

- [UI组件概览](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Solution](./ColorVision.Solution.md)

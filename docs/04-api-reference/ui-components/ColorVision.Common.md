# ColorVision.Common

`UI/ColorVision.Common/` 是 UI 层共享基础库，提供 MVVM、命令、扩展契约、粗粒度权限、Win32 包装和常用工具。它不是业务模块，也不是面向外部承诺长期稳定的公共 SDK。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 升级后多个 UI DLL 同时报 `MissingMethodException` / `FileLoadException` | `ColorVision.Common.dll` 版本、public 类型签名、调用方是否按旧版本编译 |
| 菜单、状态栏或初始化器没有出现 | `Interfaces/Menus/`、`Interfaces/StatusBar/`、`Interfaces/IInitializer/` 和实现程序集 |
| 绑定值变化但界面不刷新 | `ViewModelBase`、属性 setter、`OnPropertyChanged` |
| 按钮命令状态不更新 | `RelayCommand`、`CanExecute` 刷新触发点 |
| 权限表现和预期不一致 | `AccessControl`、`PermissionMode`，再回到 `UI/ColorVision.Solution/Rbac` |
| 原生调用只在某台机器失败 | `NativeMethods/`、x64 平台、Windows API 可用性和资源路径 |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| MVVM 基础 | `MVVM/ViewModelBase.cs` | 最基础的 `INotifyPropertyChanged` 基类 |
| 命令封装 | `RelayCommand`、`RelayCommand<T>`、`Commands` | 通用命令和少量全局 `RoutedUICommand` |
| 配置契约 | `IConfig`、`IConfigSettingProvider` | 只放共享契约，具体配置服务在上层 |
| 初始化契约 | `IInitializer`、`InitializerBase` | 提供名称、顺序和依赖结构 |
| 菜单/状态栏契约 | `IMenuItemProvider`、`IStatusBarProvider`、`StatusBarMeta` | 发现、注册和刷新逻辑在上层模块 |
| 视图契约 | `View`、`IViewManager` | 共享视图元数据，不是完整视图框架 |
| 粗粒度权限 | `Authorization`、`AccessControl`、`PermissionMode`、`RequiresPermissionAttribute` | 全局模式开关和基础检查 |
| 原生/工具 | `NativeMethods/`、`Utilities/`、`Input/`、`ThirdPartyApps/` | Win32 包装、文件/窗口/输入/第三方应用辅助 |

## 权限边界

| 层级 | 负责什么 |
| --- | --- |
| `ColorVision.Common` | `Authorization.Instance.PermissionMode`、`AccessControl.Check(...)`、基础 Attribute |
| `ColorVision.Solution` | 本地用户、角色、权限点、策略、管理界面 |

Common 只适合放粗粒度通用检查。需要精细到页面、按钮、角色或用户的权限，回到 `UI/ColorVision.Solution/Rbac`。

## 什么该放这里

| 适合放入 Common | 不适合放入 Common |
| --- | --- |
| 多个 UI 模块共用的基础接口 | 具体窗口、页面、业务流程 |
| `ViewModelBase`、命令、轻量 Attribute | 设备、Engine 模板、项目业务 |
| Win32/DWM/剪贴板/文件关联等低层包装 | 需要依赖 `ColorVision.UI`、`ImageEditor`、`Solution` 才能运行的功能 |
| 菜单、状态栏、初始化器等共享契约 | 客户项目字段、Recipe、Fix、Socket/MES 协议 |

新增内容前先问两个问题：是否至少有两个上层模块会复用？是否会让 Common 反向引用高层 UI DLL？任一答案不对，就先放回使用模块。

## 新增共享接口检查

| 检查项 | 通过标准 |
| --- | --- |
| 足够底层 | 不绑定某个窗口、插件或客户项目 |
| 无反向依赖 | `Common` 不引用 `ColorVision.UI`、`ImageEditor`、`Solution` 等高层项目 |
| 有实现方 | 不是只有接口、没有真实调用链的预留抽象 |
| 序列化谨慎 | public 类型、命名空间、配置模型变化不会破坏上层 DLL |
| 编译兼容 | `ColorVision.UI`、`ImageEditor`、`Solution`、插件和项目包能一起编译 |

## DLL 发布验收

| 验收项 | 要查什么 |
| --- | --- |
| 目标框架 | `ColorVision.Common.csproj` 的 `net8.0-windows7.0;net10.0-windows7.0` |
| 包元数据 | `GeneratePackageOnBuild`、`PackageReadmeFile`、`README.md` |
| 根依赖边界 | 是否新增高层项目引用 |
| MVVM 基础 | 属性通知、命令执行和可执行状态不回退 |
| 扩展契约 | 菜单、状态栏、初始化器实现仍能被上层发现 |
| 权限边界 | Common 粗权限不会绕过 Solution 本地 RBAC |
| 原生辅助 | x64 和当前 Windows 版本下 Win32 调用可用 |

## 关键文件

| 任务 | 先看 |
| --- | --- |
| MVVM 和命令 | `MVVM/ViewModelBase.cs`、`MVVM/RelayCommand.cs`、`Commands.cs` |
| 配置、菜单、状态栏契约 | `Interfaces/Config/`、`Interfaces/Menus/`、`Interfaces/StatusBar/`、`Interfaces/IInitializer/` |
| 权限边界 | `Authorizations/AccessControl.cs`、`Authorizations/PermissionMode.cs` |
| 原生方法和工具类 | `NativeMethods/`、`Utilities/` |

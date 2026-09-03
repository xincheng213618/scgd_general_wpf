---
knowledge_id: "ui.common"
knowledge_type: "topic"
status: "current"
summary: "共享接口的宿主接入、属性通知与命令的同步执行限制、粗粒度权限判据，以及第三方工具发现和启动边界。"
aliases: ["共享接口应该放在哪里", "属性相同仍然通知", "按钮禁用仍被调用", "方法权限特性与自动鉴权", "ColorVision.Common", "ViewModelBase", "SetProperty", "RelayCommand", "CanExecute", "RaiseCanExecuteChanged", "ActionCommand", "IConfig", "IAssemblyService", "ModuleCatalog", "Authorization", "AccessControl", "ExecuteWithPermissionCheck", "PermissionMode", "RequiresPermissionAttribute", "ThirdPartyAppManager", "ThirdPartyAppInfo", "IThirdPartyAppProvider"]
code_paths: ["UI/ColorVision.Common/ColorVision.Common.csproj", "UI/ColorVision.Common/README.md", "UI/ColorVision.Common/MVVM/ViewModelBase.cs", "UI/ColorVision.Common/MVVM/RelayCommand.cs", "UI/ColorVision.Common/MVVM/ActionCommand.cs", "UI/ColorVision.Common/Interfaces", "UI/ColorVision.Common/Authorizations", "UI/ColorVision.Common/ThirdPartyApps", "UI/ColorVision.Common/NativeMethods", "UI/ColorVision.Common/Utilities", "UI/ColorVision.UI/AssemblyHandler.cs", "UI/ColorVision.UI/ConfigHandler.cs", "UI/ColorVision.Rbac/RbacManager.cs", "UI/ColorVision.Rbac/Services/PermissionChecker.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ThirdPartyAppInfoTests.cs", "Test/ColorVision.UI.Tests/ModuleCatalogTests.cs", "Test/ColorVision.UI.Tests/ConfigHandlerPersistenceTests.cs"]
related: ["ui.index", "ui.framework", "ui.configuration", "ui.menus", "platform.extensibility", "plugins.model", "platform.security"]
---

# 共享接口、属性通知与粗粒度权限

`UI/ColorVision.Common/` 提供跨 UI 模块共享的类型和低层实现，包括 MVVM、扩展接口、服务访问入口、模块登记、粗权限以及第三方工具模型。部分类型的命名空间是 `ColorVision.UI`，但实现仍在 Common 程序集中；不要仅按命名空间判断项目依赖方向。

它不是只有接口的程序集，也不是承诺所有上层 DLL 长期二进制兼容的完整 SDK。公共类型或签名变化需要核对实际消费者；源码中存在某个 API，不等于该 API 在全部目标框架、Windows 环境或调用方式下均已验证。

## 属性通知与命令不会替调用方管理线程

`MVVM/ViewModelBase.cs` 的 `SetProperty` **无条件赋值、调用 `OnPropertyChanged` 并返回 true**，没有相等比较。因此返回值不能用作“值确实改变”的判据；重复赋相同值仍会通知，互相写回的订阅需要自行避免循环。`OnPropertyChanged` 在调用线程同步调用事件，没有 dispatcher 切换、异常隔离或自动推导关联属性；需要刷新关联属性时必须显式通知。继承此基类也不会让普通自动属性自动通知。

`MVVM/RelayCommand.cs` 的两个命令类型都同步调用委托，不调度后台任务、不等待 Task，也不提供运行中锁、取消、超时或异常处理。传入 async lambda 时不会因此获得异步命令的完成语义；是否占用 UI 线程、如何观察异步失败由委托与调用方负责。

| 入口 | 当前判据与非显然限制 |
| --- | --- |
| `RelayCommand.CanExecute` | 调用提供的 predicate；没有 predicate 时为 true |
| `RelayCommand.Execute` / `RaiseExecute` | 直接调用 action，**不先检查 CanExecute**；同步调用抛出的异常不在命令内捕获 |
| `CanExecuteChanged` | 两个类型均转接 `CommandManager.RequerySuggested`，不是 ViewModel 属性通知的自动映射 |
| `RelayCommand.RaiseCanExecuteChanged` | 请求 `CommandManager.InvalidateRequerySuggested()`；不能将这次调用当成按钮已经同步刷新 |
| `RelayCommand<T>` 的 `ICommand` 入口 | 只接受可匹配 T 的参数；null 仅在 `default(T) is null` 时接受。类型不匹配时 CanExecute 为 false、Execute 静默不执行，不做字符串/数值转换 |
| `RelayCommand<T>.Execute(T)` | 直接调用 action，同样不检查 predicate；此泛型类型当前没有非泛型类的 `RaiseCanExecuteChanged` 方法 |

因此“按钮灰了”不能证明底层操作不可执行。会写文件、改配置、控制设备或启动外部程序的 action，不能只把必要检查放在 `CanExecute`。`MenuItemBase` 默认把 `AccessControl.Check(Execute)` 放在 predicate 中，并未在 `RelayCommand.Execute` 内加入权限拦截；具体菜单、懒调用和搜索入口见[菜单执行契约](./menus.md)。

`ActionCommand` 只是 `Header`、`UndoAction`、`RedoAction` 的载体，不实现 `ICommand`，不持有撤销栈，也不自动执行 Undo/Redo；历史记录和调用顺序属于使用它的上层模块。

## 共享契约不等于已经注册、初始化或显示

Common 中的 `AssemblyService`、`ConfigService`、`MenuService` 是可由宿主设置的静态服务入口，不是自动创建实现的 DI 容器。对应实现分别由上层 `AssemblyHandler`、`ConfigHandler`、`MenuManager` 注入。宿主尚未装配时 `Instance` 可能为空；单独引用 Common 不会替调用方完成初始化。

| 共享边界 | Common 提供什么 | 继续核对的权威主题 |
| --- | --- | --- |
| 配置 | `IConfig` 标记、`IConfigService` 与 `IConfigReloadNotifier`；静态服务入口不实现落盘 | [配置持久化与对象所有权](./configuration.md) |
| 菜单、搜索、状态栏、视图 | 元数据、接口及部分基类；具体宿主负责发现、筛选、实例寿命和 UI 更新 | [UI 壳层责任](./ColorVision.UI.md)、[扩展入口](../../02-developer-guide/core-concepts/extensibility.md) |
| 初始化 | `IInitializer` 只有 Name、Order、InitializeAsync；`InitializerBase` 给出默认名称和顺序，没有依赖图字段 | [UI 壳层责任](./ColorVision.UI.md) |
| 插件 | `IPlugin` 只有 Header、Description、Execute；`IPluginBase` 是基础实现，不是通用加载/卸载状态机 | [插件装载与发现](../../02-developer-guide/plugin-development/overview.md) |
| 模块登记 | `ModuleCatalog` 记录主动贡献功能的程序集，并转交 `IAssemblyService.RegisterAssembly` | [插件装载与发现](../../02-developer-guide/plugin-development/overview.md) |

`ModuleCatalog` 按不区分大小写的 `Kind:Id` 去重：同一键、同一程序集重复登记不再转发；同一键换成另一个程序集会抛异常。`Seal()` 后拒绝继续登记，不能据此宣称支持运行时热插拔或卸载。登记本身也没有构造或初始化所有 provider。

`AssemblyHandler.LoadImplementations<T>` 是上层发现实现：缓存类型而不是共享所有实例，每次调用仍尝试构造；构造失败可能被记录并跳过。其程序集过滤、构造门禁和宿主二级缓存已归[插件发现契约](../../02-developer-guide/plugin-development/overview.md#loaded-不等于-provider-可见)，不要在 Common 另写一套“实现接口即可自动生效”的规则。

新增共享类型应保持依赖方向：客户字段、设备操作和具体业务窗口留在各自模块。需要上层能力时扩展现有共享接口并核对真实装配点，不在 Common 引用高层项目来闭合调用链。

## 粗粒度权限的判据不是统一授权拦截

`Authorizations/AccessControl.cs` 中的 `Authorization` 是 `IConfig`，`Instance` 为可设置的全局引用；实例默认模式为 Administrator，但引用本身没有自动初始化。`ConfigHandler.Load()` 将配置实例赋给它。设置 `PermissionMode` 会同步发送 `PermissionModeChanged`，相同值也通知，不自动触发所有控件的命令重查询。

当前枚举按权限由高到低为 `SuperAdministrator=-1`、`Administrator=0`、`PowerUser=1`、`User=2`、`Guest=3`。

| 入口 | 实际检查 |
| --- | --- |
| `AccessControl.Check(PermissionMode)` | `Authorization.Instance.PermissionMode <= required`，数值越小权限越高；Instance 未装配时会失败，不会自动创建 guest |
| `AccessControl.Check(Action)` | 只反射 `action.Method` 的 `RequiresPermissionAttribute`；无该方法特性则返回 true，有则采用上述层级比较 |
| `ExecuteWithPermissionCheck(Action, currentPermission)` | 使用**调用方传入的值与 required 精确相等**，不是 `<=`；无方法特性则直接执行，不相等弹消息但没有 bool 完成结果 |

后二者不能互换：要求 Administrator 的方法可通过 SuperAdministrator 的 `Check`，却不能通过传入 SuperAdministrator 的 `ExecuteWithPermissionCheck`。后者不自动取全局模式，也不捕获 action 自身异常。

`RequiresPermissionAttribute` 允许标在类或方法上，但上述 helper 只读取委托的方法，没有额外查询声明类；lambda 包装也不会自动继承被包装方法的特性。特性本身不拦截调用，直接调用方法不会自动执行权限检查。

RBAC 的用户/角色/权限码校验在 `UI/ColorVision.Rbac/Services/PermissionChecker.cs`；它与全局模式并存，登录等具体路径会同步模式。`RbacManager` 无有效登录态时仍可把全局模式设为 Administrator，因此 **Administrator 模式不证明已登录或拥有某个 RBAC 权限码**。完整账户、会话和接入边界见[RBAC 登录与会话](../../03-architecture/security/rbac.md)，不要把 Common 的 helper 当成全产品统一安全边界。

## 第三方工具：发现、可用状态与实际启动分开

`ThirdPartyAppManager.GetInstance()` 本身不枚举工具。`LoadApps` / `LoadAppsAsync` 经 `AssemblyService.Instance.LoadImplementations<IThirdPartyAppProvider>()` 收集定义，调用 `RefreshStatus`，按路径/参数等身份去重并排序；具体入口和窗口属于上层 Desktop。

- `IsLoaded` 为 true 后普通 Load 会复用现有列表；重新收集用 `forceReload`，通知支持缓存的 provider 重新探测另用 `forceProviderRefresh`，两者不是同一个缓存开关。`Refresh()` 对已加载列表只更新每项状态，不重新发现 provider。
- `LoadAppsAsync` 把收集放到 `Task.Run`，完成后通过可用的应用 dispatcher 替换集合。取消在 provider/条目之间检查，不能强制终止 provider 内部操作。单个 provider 的普通异常被吞掉，可能留下部分条目；最终 `IsLoaded=true` 不证明所有 provider 成功。
- `ThirdPartyAppInfo.RefreshStatus` 可能读取文件、注册表和图标；它不启动应用。配置了 `LaunchAction` 或 `LaunchPath` 会直接视为已安装，不证明命令能启动；其它探测得到文件路径也不等于可执行文件可信或功能验收通过。
- `DoubleClickCommand` 的 predicate 和实际 `OnDoubleClick` 都检查 `IsAuthorized`；`RunInstaller` 与打开目录也有执行路径检查。该模型在全局 Authorization 为空时拒绝授权，和直接使用 `AccessControl.Check(PermissionMode)` 的行为不同。
- `ThirdPartyAppContextAction` 只依赖自身 Execute/CanExecute，没有自动继承所属工具的 RequiredPermission；新增上下文操作仍需维护执行路径的授权边界。

实际启动优先调用 `LaunchAction`，其次用 `LaunchPath` 和参数，再尝试已探测的 exe，最后可能转为安装入口。`RunInstaller` 可调用自定义 `InstallAction` 或通过 shell 启动安装文件；这些调用可能打开窗口、启动外部进程或改变系统安装状态，必须有当前任务的相应授权，不能为验证工具列表而执行。异常一般弹消息；方法返回不代表子进程退出成功、安装完成或业务动作完成，也没有统一取消/回滚。

`NativeMethods/`、`Utilities/` 还包含文件、窗口、剪贴板、注册表及设备等低层操作，不能因为位于 Common 就视为无副作用。具体调用前核对对应实现、Windows 约束与任务权限，本页不作为每个工具函数的使用手册。

## 包与验证边界

目标框架、WPF/WinForms、NuGet README 和生成包设置以 `ColorVision.Common.csproj` 为准。当前项目文件没有高层 `ProjectReference`；这不等于跨平台、所有工具无外部环境依赖，或上层程序集无需匹配版本。构建入口保留在源码旁 README，包生成不等于上传发布。

- `ThirdPartyAppInfoTests.cs` 检查默认分类/权限、工具权限层级、直接执行命令仍检查权限及若干 provider 元数据；其中执行的是测试委托，不是实际安装或外部进程。它不覆盖 `AccessControl` 两套判据一致性或全部 Win32 行为。
- `ModuleCatalogTests.cs` 检查重复登记、Seal、RBAC 模块进入现有发现快照及类型快照复用；不证明所有插件、构造失败路径或上层 UI 都已验证。
- `ConfigHandlerPersistenceTests.cs` 中的重载测试检查配置实例替换与 `Authorization.Instance` 重绑定；不是账户鉴权、权限码或跨线程通知测试。
- 当前关联测试未证明 `ViewModelBase` 的所有通知线程/重入场景、泛型命令参数与重查询时序、第三方 provider 失败/取消/重载、真实外部工具、完整 SDK/ABI 兼容性。公共契约变更仍需有针对性的测试和实际消费者构建。

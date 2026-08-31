---
knowledge_id: "ui.status-bar"
knowledge_type: "topic"
status: "current"
summary: "状态栏的插件发现、活动文档通知、绑定更新、控件重建和关闭生命周期；刷新不保证发现新provider，隐藏不等于保存偏好或停止采样。"
aliases: ["状态栏", "状态栏刷新", "状态栏排序", "隐藏状态栏", "活动文档状态", "状态栏控件", "StatusBarManager", "StatusBarControl", "StatusBarMeta", "IStatusBarProvider", "IStatusBarProviderUpdatable", "IActiveDocumentStatusProvider", "StatusBarItemsChanged", "StatusBarAlignment", "StatusBarActionType", "StatusBarType"]
code_paths: ["UI/ColorVision.UI/StatusBar", "UI/ColorVision.Common/Interfaces/StatusBar", "UI/ColorVision.UI/AssemblyHandler.cs", "ColorVision/MainWindow.xaml", "ColorVision/MainWindow.xaml.cs", "ColorVision/MainWindowConfig.cs", "UI/ColorVision.Solution/SolutionStatusBarProvider.cs", "UI/ColorVision.ImageEditor/ImageView.xaml.cs", "UI/ColorVision.Scheduler/SchedulerStatusBarProvider.cs", "Plugins/SystemMonitor/SystemMonitorIStatusBarProvider.cs"]
test_paths: ["Test/ColorVision.UI.Tests/SystemMonitorLifecycleTests.cs"]
related: ["ui.framework", "ui.discovery", "ui.common", "ui.solution", "ui.image-editor", "ui.scheduler", "plugins.system-monitor", "operations.main-window"]
---

# 状态栏：发现、刷新与宿主生命周期

`UI/ColorVision.UI/StatusBar/` 把共享 `StatusBarMeta` 呈现到 WPF 宿主；接口位于 `UI/ColorVision.Common/Interfaces/StatusBar/`。它负责发现、组合和显示，不负责证明设备、调度或后台业务已经完成。菜单、搜索和状态栏虽然都能执行命令，但各自的检查和生命周期不同。

主窗口首次渲染后通过后台优先级 dispatcher 调用 `StatusBarManager.Init(StatusBarGrid, MainWindowTarget)`；停靠管理器的 ActiveContentChanged 则把当前 ActiveContent 原对象交给 `OnActiveDocumentChanged`。窗口已经出现不代表状态栏已装配完成。

## provider 与宿主的发现、复用

`Init` 先清空传入 Grid，创建新的 StatusBarControl，以 targetName 写入 `_controls`，再装配并刷新。相同 targetName 再次 Init 会覆盖字典引用，不会自动注销或清理另一个旧宿主；这不是同目标多窗口的独立状态服务。

`LoadFromAssemblies` 只有在 `_providers.Count == 0` 时才调用 `AssemblyHandler.LoadImplementations<IStatusBarProvider>()`。候选需为可构造的具体 class，具有公共无参构造；构造失败由 AssemblyHandler 逐类型记录后跳过。随后保留成功的 provider 实例，对 Updatable 实例订阅 StatusBarItemsChanged。

缓存一旦非空，`RefreshAll` 只重新读取这些实例的 metadata，不重新发现后来加载的 provider。清上游程序集缓存和重建状态栏是不同动作；不能因为插件 DLL 已加载、点击过刷新，就承诺新状态项出现。GetStatusBarIconMetadata 的获取/枚举异常另行按 provider 记录，正常其它 provider 可以继续。

元数据按 TargetName 精确匹配或 Global 进入已注册宿主；Global 不是自动给所有窗口添加状态栏。manager 的路由使用注册时的字典 key，直接改控件的 TargetName 属性不会自动重建这份路由。

provider 构造不保证无副作用。例如 `SchedulerStatusBarProvider` 构造时取得 QuartzSchedulerManager；后者读取配置/统计并创建 InitializationTask，详见[调度契约](./ColorVision.Scheduler.md)。仅为核对文档而启动主窗口或枚举全部 provider，不能视为纯只读检查。

## 三条更新路径

| 变化 | 当前处理 | 不应推导的保证 |
| --- | --- | --- |
| 已有项的 Source 属性变化 | 文本控件通过 OneWay 绑定读取 BindingName；是否更新取决于源对象的通知/绑定能力 | 不要求每次数值变化都重建 metadata，也不能只改普通 metadata 属性就期待控件通知 |
| 全局 provider 的项目集合变化 | `IStatusBarProviderUpdatable.StatusBarItemsChanged` 触发重取该 provider 的 metadata，删除旧项并添加新项 | 基础 IStatusBarProvider 没有集合刷新事件；已有实例被复用，不等于项目控件被复用 |
| 活动文档切换或其内容变化 | 切换先退订旧文档事件并删除旧上下文项；直接实现 IActiveDocumentStatusProvider 的新对象被订阅并投影 | 不自动在容器的子控件或 DataContext 中寻找接口，不为每个窗口保存独立活动文档 |

新 ActiveContent 不实现接口时，只清除上一份文档项目；实现接口时，项目送到所有 TargetName 匹配的宿主。manager 仅有一个 `_currentDocumentProvider`，不是每个宿主一个。当前文档事件要求 sender 为当前 provider；它重新取 metadata，不代表业务已执行完成。

具体例子：`SolutionStatusBarProvider` 只实现基础 provider，以 Source + BindingName 显示工作区名称和打开状态；`SystemMonitorIStatusBarProvider` 用配置变化事件增删项目，但数值仍从 monitor 绑定读取。`ImageView.GetActiveStatusBarItems` 则从 Config 生成尺寸/格式等 Description 快照，在 SetImageSource 赋值图像源并完成相关同步步骤后发事件，不代表渲染完成，也不应把任意 Config 修改都当成自动刷新。

公共 Init、AddItem、RemoveItem、RefreshAll、OnActiveDocumentChanged 不自动切到 UI 线程；两类项目变化事件内部才使用 `Application.Current?.Dispatcher.Invoke` 同步调度。没有统一节流、超时或取消协议，调用方要区分事件调度和直接方法调用；活动文档的 metadata 获取/添加也没有全局 provider 那样的异常隔离。

## 全量、增量和失败边界

`RefreshAll` 清空 `_globalItems`，重取已缓存 provider 的 metadata，再让每个控件 LoadItems。LoadItems 清空左右面板、容器映射和右键菜单，重新创建显示容器。因此“刷新复用 provider”不等于“刷新保留全部控件状态”。

单个全局 provider 事件先按旧 ID 移除项目，再获取并添加新项。获取或控件创建失败可留下空缺/部分更新，没有恢复旧显示的事务；旧项删除发生在获取异常捕获之前。活动文档切换同样先去掉旧项，也没有失败后恢复旧文档显示的协议。

`AddItem` 的手工全局项不是永久注册：下一次全量重新装配会清掉它。`RemoveItem` 只删当前集合/显示，不修改 provider 定义；随后刷新可重新出现。不要把这些方法当成持久禁用开关。

## 标识、顺序和显示字段

左右两侧分别按 Order 升序。全量装载对同 Order 保留输入顺序，增量插入放在当前同 Order 项之后，没有额外的 ID 排序。右侧也是该面板内的升序，不是把数值倒排。

控件使用 `Id ?? Name ?? meta.GetHashCode().ToString()` 作为容器 key；manager 的删除只使用 `Id ?? Name`。贡献方应提供稳定、非空且跨来源不冲突的 ID，当前没有唯一性校验：

- 重复 ID 会覆盖字典指向，但此前添加的可视容器可能仍留下；移除不是自动合并或完整去重。
- manager 按 ID 删除可影响其它 provider 或活动文档的同 ID 项，不按 provider/TargetName 隔离身份。
- Id、Name 都为 null 时，控件的 hash 兜底不能让 manager 的增量/文档切换删除走到同一个 key。

Name 用作右键菜单标题，Description 用作 Tooltip，Source 放入容器 DataContext。主要内容规则是：

| Type | 实际显示 |
| --- | --- |
| Text | BindingName 非空则绑定文本；否则使用非空 Description |
| Icon | 先 IconContent，再 IconResourceKey；均无则走 Text 回退 |
| IconText | 图标先资源键、再 IconContent；文字用 TextBindingName ?? BindingName，不自动用 Description 补正文 |

`StatusBarMeta` 的 BindingName 注释仍提到 Icon 的 IsChecked，但当前 Icon 呈现没有这种绑定。Source、IconContent 都沿用对象引用；IconContent 若为 UIElement，没有自动为多个匹配宿主克隆的机制。不要把给两个 ContentControl 传同一视觉对象描述成受支持的多宿主复用保证。

## 隐藏与关闭不是资源释放

右键项目勾选只改该容器 Visibility，不回写 meta.IsVisible，也不调用配置保存。IsVisible 是创建容器时的初始值；刷新/重建可能使手工隐藏项重新出现，Id 注释中的“持久化可见性偏好”不是当前实现。

“Hide Status Bar”只折叠 StatusBarControl 自身；主窗口的 IsOpenStatusBar 绑定到外层 StatusBarGrid，是另一层状态。切换外层可见性不保证重置内层折叠，LoadItems 也不主动把已折叠控件恢复为可见。

这些显示操作不改 provider 配置，也不注销事件或停止采样。SystemMonitor 的“全部状态项关闭时不创建 monitor”特指其 IsShow* 配置全为 false，不能套用到右键隐藏或宿主 Grid 折叠；采样决策归[监控插件](../plugins/standard-plugins/system-monitor.md)。

manager 没有统一的宿主 Unregister/Dispose，也没有在 Window.Closed/控件 Unloaded 时清空 `_controls` 或退订全部全局 provider。旧活动文档事件只在下一次 OnActiveDocumentChanged 时退订；关文档可能间接产生该通知，但关窗不是该层完成所有清理的证明。这里讨论进程仍存活时的所有权，不是说进程结束后还能运行回调。

## 点击、弹层与定时器

普通命令分支在 MouseLeftButtonDown 中先检查 `Command.CanExecute(e)`，再 `Execute(e)`，参数是鼠标事件而非 metadata/Source；没有统一捕获业务异常或等待异步任务。这与[搜索入口](./search.md)直接 Execute 的行为不同。

ActionType=Popup 且 factory 非空时走另一条分支：悬停两秒或点击可调用 PopupContentFactory，不检查 Command.CanExecute。factory 返回 null 则不显示，抛异常没有统一隔离。弹层可以带业务按钮，例如 Solution 的取消打开或打开 Explorer；不能把悬停工厂或点击视为纯状态读取。

控件有悬停/离开定时器和弹层关闭处理，但没有在移除条目、全量清空或 Unloaded 时统一停止它们。悬停弹层的 anchor 与 popup 还各自持有关闭计时状态，不能仅凭“进入弹层取消计时”的注释承诺所有移动路径都不会提前关闭。上述生命周期限制来自源码核对，未运行输入复现，也未在本次文档工作中修复。

## 验证入口与缺口

主要实现是 `StatusBarManager.cs`、`StatusBarControl.xaml.cs` / `.xaml`；接口、metadata 与实际 provider 路径列在元数据中。修改某个 provider 时，同时核对该业务模块，而不是仅验证图标出现。

`SystemMonitorLifecycleTests` 覆盖配置关闭时不创建 monitor、可见项目的 metadata、monitor 复用及配置重载等局部行为；不覆盖 StatusBarManager/Control。未找到本层发现缓存、多宿主、重复 ID、排序、增量失败、活动文档切换、隐藏持久化和关闭清理的专项测试。

文档检索、路径和网站校验不能填补 WPF 运行时缺口。后续需用获授权的隔离宿主、合成 provider 与无害命令验证；不为文档验证连接 Socket、启动实际调度或运行设备操作。本次未运行上述产品行为。

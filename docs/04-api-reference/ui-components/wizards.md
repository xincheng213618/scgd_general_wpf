---
knowledge_id: "ui.wizards"
knowledge_type: "topic"
status: "current"
summary: "配置向导的步骤发现、初始化时序、前进应用和完成标记；关闭不回滚，完成标记不证明组件健康或重启成功。"
aliases: ["设置向导", "首次启动向导", "向导步骤", "向导初始化", "向导完成", "WizardWindow", "WizardManager", "WizardWindowConfig", "WizardCompletionKey", "IWizardStep", "WizardStepBase", "IWizardInitializer", "RunsBeforeInitializers", "RequestSkipWizard"]
code_paths: ["UI/ColorVision.UI.Desktop/Wizards/WizardWindow.xaml", "UI/ColorVision.UI.Desktop/Wizards/WizardWindow.xaml.cs", "UI/ColorVision.UI.Desktop/Wizards/WizardWindowConfig.cs", "UI/ColorVision.Common/Interfaces/IWizardStep.cs", "UI/ColorVision.Common/Interfaces/Window/WindowConfig.cs", "UI/ColorVision.UI/AssemblyHandler.cs", "ColorVision/App.xaml.cs", "ColorVision/Wizards/RecommendedSoftwareWizardStep.cs"]
test_paths: ["Test/ColorVision.UI.Tests/RecommendedSoftwareWizardStepTests.cs","Test/ColorVision.UI.Tests/StartupMaintenanceLifecycleTests.cs","Test/ColorVision.UI.Tests/WizardWindowRuntimeTests.cs"]
related: ["ui.desktop", "operations.first-run", "platform.runtime", "ui.configuration", "ui.discovery"]
---

# 配置向导：步骤、应用与完成边界

`UI/ColorVision.UI.Desktop/Wizards/` 负责通用向导窗口；`IWizardStep` 和 `IWizardInitializer` 定义在 `ColorVision.Common`，具体步骤由主程序或插件提供。向导是有副作用的配置流程，不是只读说明页；它也不是[设置窗口](./settings.md)的另一个外观。

`ColorVision/App.xaml.cs` 在一次性维护参数要求进入向导、恢复选择要求重新配置，或 `WizardCompletionKey` 为 false 时显示非模态 `WizardWindow`，否则进入 `StartWindow`。完整启动和恢复分流见[主程序运行时](../../03-architecture/overview/runtime.md)与[首次启动](../../00-getting-started/first-steps.md)。

运行中可通过[应用搜索](./search.md)查找“初始化向导／向导／setup”。这是搜索专用高级维护入口，不恢复普通菜单，也没有默认快捷键。检查管理员权限后直接以主窗口为 Owner 打开 `new WizardWindow(runInitializers: false)` 对话框，不确认或重启；入口不把 `WizardCompletionKey` 改为 false，也不清空现有配置。运行期维护动作与启动交接见[运行时维护入口](../../03-architecture/overview/runtime.md#搜索中的维护入口)。

## 发现、排序与初始化

`WizardManager` 和 `WizardWindow` 实际都在 `Wizards/WizardWindow.xaml.cs`，没有独立的 `WizardManager.cs`。每次窗口初始化都会调用 manager 的 `Initialized()`，清空列表并从 `AssemblyHandler.GetAssemblies()` 的缓存/过滤视图重新扫描非抽象类型，分别无参构造 `IWizardStep` 和 `IWizardInitializer`，各自按 `Order` 升序排序。

该扫描不去重，也没有逐程序集/逐类型的异常隔离；反射或构造失败可能中断发现，并且发生在窗口延迟初始化的异常处理之外。实现两个接口的同一类型会分别构造，不保证共享同一实例。DLL 在磁盘上或已被任意方式加载，都不足以证明它进入了该扫描视图。

`IWizardStep` **没有 `IsVisible` 或统一的隐藏/跳过属性**，manager 也不按可见性筛选。不要把其它菜单或属性编辑器的可见性规则套到向导，或推断“不可见步骤会连同启动初始化一起跳过”。

原无参构造保持启动行为；显式 `runInitializers: false` 仍发现并构造步骤和 initializer 对象、刷新和应用步骤，但不执行 `IWizardInitializer.Initialize` 链。它不是“只读查看”模式，步骤自己仍可能修改配置或安装软件。

默认构造下，`IWizardInitializer` 是独立的同步链，不是 `StartWindow` 执行的 `IInitializer` 链：

| 条件 | 窗口何时执行向导 initializer |
| --- | --- |
| 没有任何步骤声明 `RunsBeforeInitializers` | 延迟初始化时先运行 initializer，再显示第一步 |
| 存在先行步骤 | 前进到一个非先行步骤前，检查此前所有先行步骤的 `ConfigurationStatus`；都为 true 时运行 |
| 到完成入口仍未运行 | 当前页 Apply 成功后运行 |

`RunsBeforeInitializers` 默认等于 `IsRequired`，**不重排 `Order`**。前进门禁只检查目标页之前的先行步骤，不保证排在后面的先行步骤已经完成；实现步骤时必须一并核对实际排序。

窗口在调用前就设置 `_initializersRun=true`，没有统一的失败重试/补偿协议。每个 initializer 得到含 Owner、IsFirstRun 的 context；`RequestSkipWizard()` 会停止后续 initializer，并直接进入“置完成标记 → 保存 → 关闭或发起重启”的流程，不是仅跳过当前页面。

## 导航不是无副作用的翻页

| 动作 | 当前执行契约 |
| --- | --- |
| 显示一步 | 设置当前 DataContext、列表选择和进度，调用 `RefreshAsync()`；刷新异常只记录警告，不自动把配置状态改成失败 |
| 下一步 | 检查转换状态、索引及 `CanContinue`，先调用当前步 `ApplyAsync()`；返回 false 或抛异常时停留，否则按初始化时序前进 |
| 上一步 | 检查转换状态和索引，切到前一步并 Refresh；不 Apply 当前页，也不撤销此前操作 |
| 配置按钮 | 调用该步骤的 `Command`，实际行为由步骤实现，不能由通用窗口推断 |

默认 `CanContinue` 为 `!IsBusy && (!IsRequired || ConfigurationStatus)`，`HasError` 本身不是独立阻塞条件。`WizardStepBase.ConfigurationStatus` 初始为 true，默认 `ApplyAsync` 也返回 true；这些默认值不是配置检查或外部服务健康证据。窗口转换中的 `_isTransitioning` 与步骤自己的 `IsBusy` 也不是同一个状态。

例如，`ColorVision/Wizards/RecommendedSoftwareWizardStep.ApplyAsync` 会安装选中的推荐软件；全部取消选择也允许该步骤完成。AI 不能为了查看后一页而默认执行“下一步”，更不能将前进或后退当作安装事务的提交/撤销。

## 完成标记、文件保存和重启是三个结果

`ConfigurationComplete_Click` 按以下顺序处理：

1. 寻找任何未完成的必需步骤；找到则跳回该步并 Refresh，结束本次完成操作。
2. 检查当前步 `CanContinue`，调用当前步 `ApplyAsync`；失败则不继续。
3. 如尚未运行，执行向导 initializer；initializer 可请求跳过整个向导。
4. 计算所有步骤的 `ConfigurationStatus`，包括非必需步骤；先写入 `WizardCompletionKey`，再直接调用 `ConfigHandler.GetInstance().SaveConfigs()`。
5. 若结果为 false，询问是否跳过未完成项；选“不”时先前的 false 和配置已经走过保存，选“是”则置 true 并再次保存。
6. 调用 `CompleteWizard()`，关闭向导或发起应用重启。

必需步骤检查发生在本次 Apply 之前，不是保存前的末端统一再验收。普通未完成项可经确认跳过，initializer 也能直接置完成标记；因此 `WizardCompletionKey=true` 表示向导流程的放行状态，不表示所有软件已安装、数据库/设备连接健康或所有步骤都完成。

向导先改内存标记，再保存文件；保存异常没有向导级回滚或补偿。点击“完成”本身不保证设置已经保存到磁盘，必须确认 `SaveConfigs` 正常返回；底层写入契约见[配置持久化](./configuration.md)，不能从一个按钮点击推导整个系统事务成功。这里直接使用 `ConfigHandler`，与选项菜单经 `ConfigService` 保存的入口不同。

`CompleteWizard` 在自身为 `Application.Current.MainWindow` 时，先用 `Process.Start` 发起新应用进程，再 `Application.Current.Shutdown()`；否则只 `Close()`。没有等待新进程健康的交接协议。`App` 退出时清理恢复记录的有关分支依据“本次显示过向导且当前标记为 true”，也不是重启已经验证成功。

## 关闭不等于取消或恢复原状

当前 XAML 没有专用“取消”按钮；如果问题中的“取消”指关窗，应按关闭路径判断。窗口没有取消 token 或回滚处理，调用 `RefreshAsync` / `ApplyAsync` 时也未传入可取消 token。关窗不能证明在途操作已停止，已安装软件或改过的配置更不会因此撤销。

`WindowConfig` 挂接的 `Closing` 只更新内存中的窗口位置和尺寸，不是取消配置；进程退出还可能通过 `ConfigHandler` 的退出保存路径写入配置。关闭本身不置 `WizardCompletionKey=true`：若完成标记仍为 false，下次正常启动仍会显示向导，异常退出还可能先进入恢复分流；恢复选择也能在标记为 true 时要求重新配置。

## 源码与验证边界

| 责任 | 源码 |
| --- | --- |
| 发现、Refresh/Apply、initializer、完成和关闭 | `UI/ColorVision.UI.Desktop/Wizards/WizardWindow.xaml.cs` |
| 按钮、选择列表和状态显示 | `UI/ColorVision.UI.Desktop/Wizards/WizardWindow.xaml` |
| 步骤默认实现与跳过上下文 | `UI/ColorVision.Common/Interfaces/IWizardStep.cs` |
| 完成标记与窗口几何 | `UI/ColorVision.UI.Desktop/Wizards/WizardWindowConfig.cs`、`UI/ColorVision.Common/Interfaces/Window/WindowConfig.cs` |
| 启动分流与退出恢复处理 | `ColorVision/App.xaml.cs` |
| 有副作用的步骤示例 | `ColorVision/Wizards/RecommendedSoftwareWizardStep.cs` |

`RecommendedSoftwareWizardStepTests` 只有三个假服务用例：缺失软件默认选中、全部取消选择时不安装也可完成、按 Everything/WinRAR 顺序调用安装服务。它们没有真实安装，也不覆盖通用向导导航、保存或重启。

`WizardWindowRuntimeTests` 使用隔离发现、假步骤与临时配置，覆盖原构造的 initializer 时序、运行期跳过 initializer、Refresh/Apply、Apply 失败不前进、普通关闭不改完成标记，以及非主窗口完成只关窗。发现失败、真实保存失败和启动主窗口重启交接仍未覆盖；底层配置测试和文档校验不能替代这些集成检查。需要实际安装、配置写入或服务操作时仍须单独确认授权。

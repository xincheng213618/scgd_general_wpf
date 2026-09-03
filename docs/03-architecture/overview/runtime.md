---
knowledge_id: "platform.runtime"
knowledge_type: "topic"
status: "current"
summary: "启动顺序与故障恢复：初始化进度和ready不代表全部成功，运行期维护区分浏览、禁用、文档准备与重启，一次性插件跳过不绕过真实故障。"
aliases: ["启动链路", "App.xaml.cs", "启动恢复", "故障恢复", "初始化向导", "安全启动", "启动进度", "初始化失败", "StartupRegistryChecker", "StartupMaintenanceController", "StartupMaintenanceSearchProvider", "StartupRecoveryWindow", "StartupRecoveryPluginScanner", "StartupInitializersCompleted", "MainWindowInitializer", "MainWindowFactory", "CompactMainWindow", "UseCompactMainWindow", "LoadingPlugin", "startup-maintenance", "startup-skip-plugins", "wait-for-process", "safe-start", "skip-plugins", "验证并回退"]
code_paths: ["ColorVision/EntryClass.cs", "ColorVision/App.xaml.cs", "ColorVision/BuiltInModules.cs", "ColorVision/ProgramTimer.cs", "ColorVision/StartWindow.xaml.cs", "ColorVision/MainWindow.xaml.cs", "ColorVision/MainWindowFactory.cs", "ColorVision/CompactMainWindow.cs", "ColorVision/MainWindowConfig.cs", "ColorVision/SingleInstanceStartupPolicy.cs", "ColorVision/StartupFileOpenPolicy.cs", "ColorVision/OperationsApplicationRestartController.cs", "ColorVision/App.StartupMaintenance.cs", "ColorVision/Recovery", "UI/ColorVision.UI/Plugins/PluginLoader.cs", "UI/ColorVision.UI/Plugins/PluginRecoveryBackupService.cs"]
test_paths: ["Test/ColorVision.UI.Tests/SingleInstanceStartupTests.cs", "Test/ColorVision.UI.Tests/StartupRecoveryPluginScannerTests.cs", "Test/ColorVision.UI.Tests/StartupMaintenanceLifecycleTests.cs", "Test/ColorVision.UI.Tests/StartupMaintenanceWindowTests.cs", "Test/ColorVision.UI.Tests/WizardWindowRuntimeTests.cs", "Test/ColorVision.UI.Tests/StartupRecoveryWindowRuntimeTests.cs", "Test/ColorVision.Copilot.Tests/CopilotBackgroundShellMaintenanceGuardTests.cs", "Test/ColorVision.UI.Tests/StartupFileOpenPolicyTests.cs", "Test/ColorVision.UI.Tests/StartupRegistryCheckerTests.cs"]
related: ["platform.architecture", "platform.startup-integrity", "delivery.update", "plugins.model", "ui.discovery", "ui.wizards", "ui.localization", "ui.search", "operations.main-window", "engine.rc-registration", "flow.architecture"]
---

# 启动、初始化与故障恢复

本页说明桌面主程序如何选择启动路径、执行初始化和进入恢复窗口，以及运行期间如何打开维护入口。启动进度完成、主窗口出现和设备可用是不同状态；具体功能是否就绪仍需查看它的初始化与业务结果。

## 启动路径与顺序

启动从 `ColorVision/EntryClass.cs` 的 `App.Main` 进入，WPF 的 `Application_Startup` 位于 `App.xaml.cs`。下表按正常路径排列；提前返回的分支不会继续执行后续步骤。

| 阶段 | 行为与分支 |
| --- | --- |
| 进程入口 | 消费故障重启参数及 `--wait-for-process`，等待原进程退出后才创建 App、读取应用配置或获取单实例锁；随后建立计时、参数和早期日志 |
| 更新交接 | App 先检查同安装目录的活动更新；已有交接可推迟本次启动。解析维护参数后，将当前工作目录设为应用基础目录，再处理 `input` 中的更新包 |
| 本次启动记录 | `StartupRegistryChecker.CheckAndSet()` 收集未完成尝试并创建当前记录；此时还没有加载主配置和内置模块 |
| 基础装配 | 配置维护重置规则，创建 `ModuleCatalog`，由 `BuiltInModules.Register` 显式登记内置模块；随后加载主配置、暂关自动保存，应用日志、主题和界面语言 |
| 独立文件路径 | `export` 处理后结束常规启动路径；`input` 的 `.cvraw` / `.cvcie` 被独立打开路由接管时也提前返回。普通 PNG、JPEG、TIFF 继续常规启动，详见[文件启动与验证](../../00-getting-started/first-steps.md) |
| 单实例与宿主入口 | 根据调试器和多实例配置决定是否替换较早实例；完成交接后处理配置重载/自动保存，并按配置应用 MCP 与 LAN 运维入口 |
| 恢复选择 | 上次尝试未完成，或显式请求 `recovery` 时，在外部插件装载前显示恢复窗口；退出则不继续，继续时采用用户选定的插件跳过和向导策略 |
| 外部扩展与窗口 | 装载允许的插件，或记录 `PluginsSkipped`；封存模块目录，初始化 WinForms 视觉样式，再显示 `WizardWindow` 或 `StartWindow` |

单实例判断使用 `Debugger.IsAttached` 和 `APPConfig.IsMute`（“允许多实例”）。未附加调试器且不允许多实例时，会尝试关闭同会话、同安装路径的较早实例，不限于异常进程；`--debug` 不参与这个判断。旧实例退出后重载最终配置失败，会保留自动保存关闭并记录错误。真实启动可能更改配置、替换旧实例并按设置启动功能，文档核验不需要执行它。

普通启动在 `WizardCompletionKey=false` 时进入向导；维护请求或恢复选择也可要求显示向导。向导步骤、保存与重启见[配置向导](../../04-api-reference/ui-components/wizards.md)。启动语言读取及设置中更换语言的区别见[界面语言](../../04-api-reference/ui-components/localization.md)。

## 初始化完成不等于所有功能就绪

| 路径 | 执行与完成含义 |
| --- | --- |
| `StartWindow` 的 `IInitializer` | 首次渲染后在后台发现并构造实例；按 `--skip` 中逗号分隔的精确 Name 排除，再按 Order、Name 排序执行。单项 InitializeAsync 异常记录后继续，循环结束记录 `StartupInitializersCompleted`；这不表示每项成功 |
| 功能启动器 | `--feature` 先按 Header、再按类型名匹配 `IFeatureLauncher`；匹配后执行并清理启动记录，未找到则交给主窗口工厂。Execute 返回不证明该功能后续的异步业务完成 |
| 主窗口选择 | 未指定 `--feature` 或未匹配功能时，`MainWindowFactory.Create` 按 `MainWindowConfig.UseCompactMainWindow` 创建 `CompactMainWindow` 或普通 `MainWindow`；新开关默认开启，只在本次创建时选择，不原地切换现有窗口；旧 `UseCompactTitleBar` 字段不读取或迁移，新字段已保存的 false/true 则保留 |
| 主窗口初始化 | 主窗口通过 Dispatcher 调用 `IMainWindowInitialized`，按 Order 执行并记录单项异常；该异步链和首次渲染各有完成入口 |
| 启动健康标记 | 主窗口首次 `ContentRendered`、主窗口初始化链结束、功能启动器返回等路径均可调用 `StartupRegistryChecker.Clear()`。首次呈现可以先于某些异步初始化完成，因此 ready 不是设备、数据库或插件业务逐项验收 |

`IInitializer` 的实例构造发生在 `--skip` 过滤之前，跳过其 InitializeAsync 不保证没有构造副作用。程序集过滤、provider 构造及各消费者缓存见[扩展发现与排查](../../04-api-reference/ui-components/ui-runtime-handoff.md)；插件的清单、条件依赖预检和装载失败规则只在[插件装载](../../02-developer-guide/plugin-development/overview.md)中维护。

普通 `MainWindow` 保留原生外观；`CompactMainWindow : MainWindow` 继承同一份工作区 XAML 和初始化链，只增加标题栏适配，不第二次构造工作区。新建配置或缺少 `UseCompactMainWindow` 的升级配置默认选择紧凑主窗口，包括仅有旧字段的配置；在设置中关闭新开关并重启仍可选择旧 `MainWindow`。紧凑外观仅在满足 Windows 11 等门禁后附加，不支持或初始化失败时在该实例回退原生外观，不再创建第二个普通主窗口；这样不会重复改写全局工作区、文档宿主和快捷键注册。设置入口、重启与恢复方式、全屏订阅顺序及验证边界见[主窗口与入口装配](../../01-user-guide/interface/main-window.md)。已经匹配的功能启动器、独立文件路由、单实例交接和恢复门禁不由这个开关改变。

启动进度依据步骤和历史耗时估算，不是健康检查结果。定位“进度结束但功能不可用”时，先查具体 initializer 的日志，再查该能力的前提，不能仅依据 `PluginsLoaded`、`StartupInitializersCompleted` 或 ready 排除故障。

## 启动记录与恢复判断

`StartupRegistryChecker` 定义在 `ColorVision/ProgramTimer.cs`，按安装目录和启动尝试保存记录，包含进程身份、版本、阶段和当前组件。仍在运行的其它尝试不会作为已退出故障读取；未完成的历史尝试转入本次恢复来源，多开进程不共用一个可相互覆盖的标志。

| 记录处理 | 含义 |
| --- | --- |
| `MarkStage` | 更新当前阶段/组件并替换旧恢复来源；如 `LoadingPlugin`、`StartupInitializer`、`MainWindowInitializer` |
| `Clear` | 清理当前记录并向启动 guard 标记 ready；业务健康边界见上一节 |
| `CompleteForRecoveryRestart` | 清理当前尝试，但不据此认证新进程已成功启动；独立文件处理、更新或实例接管等路径使用它 |
| 恢复窗口取消或准备失败 | 通常保留故障来源；健康启动中显式请求 `recovery` 后取消，只清理健康的本次尝试 |

退出清理还会考虑系统关机/注销、更新交接、实例替换和向导状态。向导分支只判断“本次显示过向导且内存中的 `WizardCompletionKey=true`”，不能将记录已清除当作保存或重启成功证据。早期依赖异常的 Release guard、ServiceHost 有限观察和重复告警抑制见[启动失败与缺依赖告警](../components/startup-integrity.md)。

## 打开故障恢复与初始化向导

发生真实启动故障时，恢复窗口在插件装载前打开。主程序已运行时，可以在[应用搜索](../../04-api-reference/ui-components/search.md)中查找 **故障恢复** 或 **初始化向导** 并执行；入口来自 `StartupMaintenanceSearchProvider`，不依赖菜单注册。

搜索目录只提供名称、说明、别名和命令。执行时要求当前主窗口已加载且可见、具有应用管理员权限；随后以主窗口为 Owner 居中打开对话框。打开时不要求结束运行任务，也不先重启或关闭主窗口。运行期向导使用 `runInitializers: false`，不重复首次初始化器，但步骤本身的刷新、配置或安装行为仍然保留。

### 插件列表如何产生

恢复页读取 `Plugins/` 顶层插件目录的清单、文件时间、持久禁用状态和当前安装的备份候选，不读取 `.deps.json` 或加载插件程序集。无清单和清单损坏的目录仍可显示；目录已经缺失但有备份时，显示“仅备份可恢复”，这种条目不能选来跳过或禁用。

“疑似”优先匹配上次记录中的组件键；没有匹配项、且故障阶段含 plugin 时，回退到最近修改的已启用项。它是排查线索，不是插件故障鉴定。列表中的 **验证并回退** 表示已找到备份元数据；完整 payload 校验在执行还原时进行，按钮可用不表示文件已验证或回退一定成功。

### 根据动作选择结果

| 动作 | 启动阶段 | 程序运行期间 |
| --- | --- | --- |
| 正常启动 / 返回应用 | 按常规插件策略继续 | 只关闭恢复对话框 |
| 本次跳过所选 / 安全启动 | 在依赖读取和 DLL 装载前跳过所选，或不进入插件加载器；配置不变 | 确认重启后把一次性选择交给新进程，不能卸载当前已加载的插件 |
| 永久禁用所选 | 保存禁用状态并继续启动；旧式插件也可按目录识别 | 复核任务状态后保存，留在窗口并提示下次启动生效，不关闭文档 |
| 初始化向导 | 继续启动时显示向导 | 直接打开运行期向导 |
| 主程序更新 | 检查主程序更新计划，有计划时执行更新交接 | 先完成下节的任务检查与文档准备，再启动更新 |
| 完整安装包修复 | 确认后下载当前版本的完整安装程序；无新版或检查失败时也保留修复入口 | 同样先检查并准备文档；入口可用不代表网络/安装包可用 |
| 验证并回退 | 核对当前安装及备份内容，准备外部目录替换和重启 | 先检查并准备文档，再验证和执行回退 |
| 程序备份、日志 | 打开对应窗口/目录，不清除故障来源 | 浏览本身不做文档收尾；执行快照还原时才检查与准备 |
| 退出 / 关闭窗口 | 终止本次常规启动 | 只关闭恢复对话框 |

恢复窗口只检查主程序版本，不执行普通“检查更新”窗口的主程序/插件合并检查。修复、插件备份与快照的制品和还原规则见[更新与程序备份](../../02-developer-guide/deployment/auto-update.md)。健康状态下主动进入的页面使用维护说明，不宣称上次启动失败。

### 运行期检查、保存与重启

执行永久禁用、临时跳过重启、更新、完整修复、插件回退或快照还原时，会复核宿主和权限，并检查 Flow 状态、Copilot 运行/排队任务及后台命令、忙碌的维护窗口和更新保护。Flow 状态不可用也会阻止动作；仅打开窗口不执行这些门禁。

更新、修复、插件回退及快照还原还需先让主工作区处理未保存文档，再按子窗口到父窗口的顺序正常关闭其它独立窗口，保存配置并复核状态。主窗口与当前操作对话框的 Owner 链保持打开。取消或保存失败会阻止业务开始；已经接受关闭的其它窗口不会因此自动恢复。

临时插件跳过的重启流程是：

1. 复核任务/更新状态，显示默认选择“否”的确认框，再复核一次。
2. 让每个窗口经过自己的 Closing 和保存/取消逻辑，子窗口先于主窗口关闭。
3. 所有窗口接受关闭后才创建子进程；子进程先等待旧进程退出，再读取配置并处理单实例。
4. 若子进程创建失败且所有窗口已关，提示手动重开并结束旧实例，避免遗留无窗口进程。取消后仍有窗口时保留应用，不影响以后普通退出的更新行为。

已提交的维护退出不附带安装预取更新，即使创建新进程失败也如此。正常退出时的更新交接仍按更新主题处理。

### 一次性维护参数

参数由维护流程生成，无需用户在重启后重复选择；它们不修改向导完成标记、多实例设置，也不伪造启动失败记录。

| 参数值 | 行为 |
| --- | --- |
| `--startup-maintenance safe-start` | 本次跳过全部外部插件 |
| `--startup-maintenance skip-plugins` 与 `--startup-skip-plugins` | 后者为一个 JSON 字符串数组参数，保留精确插件键；解析失败或空列表改走恢复页，不静默加载全部插件 |
| `--startup-maintenance setup` / `recovery` | 兼容的显式向导 / 恢复启动分支 |
| `--wait-for-process` | 在入口消费并等待指定旧进程，最多 30 秒；超时退出，后续常规启动不继续 |

真实的未完成启动记录仍会触发恢复页，safe-start / skip-plugins 不绕过它；非空跳过键按插件加载器的匹配规则处理。参数只是本机启动意图，不是远程维护授权或新进程健康凭据。

## 进入业务后的排查

| 现象 | 应核对的责任与前提 |
| --- | --- |
| 插件已装载但入口不见 | [扩展发现](../../04-api-reference/ui-components/ui-runtime-handoff.md)中的程序集过滤、provider 构造与消费者缓存；再看目标窗口和可见性 |
| 服务树或设备控件为空 | `ServiceManager` 在数据库连接可用时加载 TypeServices、TerminalServices、DeviceServices 和 GroupResources；按[设备服务链](../../04-api-reference/engine-components/device-service-chain.md)核对数据库与对象装配 |
| 数据库模板不可见 | `TemplateControl` 连接可用时发现 IITemplateLoad 并调用 Load；程序集发现、数据库和具体加载器均需核对，见[模板注册与持久化](../components/templates/design.md) |
| 设备在线但不能执行 | 按[RC 注册与就绪](../../04-api-reference/engine-components/rc-registration.md)区分连接、令牌、服务快照和设备状态 |
| 图已结束但业务未完成 | 先识别[Flow 执行路径](../components/engine/flow-engine.md)：共享会话还有前后处理与最终化；无界面裸请求及直接持有 FlowControl 的项目有各自完成契约 |

## 验证入口与缺口

- 启动分支：`SingleInstanceStartupTests` 检查决策、替换响应及安装范围锁名；`StartupFileOpenPolicyTests` 检查独立文件路由；`StartupRegistryCheckerTests` 使用临时注册表项检查未完成尝试回收。
- 恢复列表：`StartupRecoveryPluginScannerTests` 使用临时清单和无效 DLL 字节，检查记录匹配、旧式和损坏清单目录；不构成真实插件装载或完整备份恢复测试。
- 维护交接：`StartupMaintenanceLifecycleTests` 检查参数、确认、关窗取消、保存失败和失败收尾；`StartupMaintenanceWindowTests`、`WizardWindowRuntimeTests`、`StartupRecoveryWindowRuntimeTests` 用隔离窗口与替身检查 Owner、动作、向导初始化和准备顺序。
- 后台任务门禁：`CopilotBackgroundShellMaintenanceGuardTests` 使用延迟进程替身检查预留、运行和完成状态，不读取输出，不启动真实维护进程。

测试引用不表示本轮已执行，也不证明安装环境中成功重启、还原或设备健康。源码入口已随各节列明；文档检查只验证结构、路径与检索，实际恢复验收需使用获授权的隔离安装和备份。

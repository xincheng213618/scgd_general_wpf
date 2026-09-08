---
knowledge_id: "platform.runtime"
knowledge_type: "topic"
status: "current"
summary: "启动顺序与故障恢复：初始化进度和ready不代表全部成功，运行期维护区分浏览、禁用、文档准备与重启，一次性插件跳过不绕过真实故障。"
aliases: ["启动链路", "App.xaml.cs", "启动界面", "启动动画", "StartWindow", "StartupScene", "StartupUiTrace", "COLORVISION_STARTUP_TRACE", "StopAndReport", "首帧耗时", "首次使用耗时", "启动恢复", "故障恢复", "初始化向导", "安全启动", "启动进度", "初始化失败", "StartupRegistryChecker", "StartupMaintenanceController", "StartupMaintenanceSearchProvider", "StartupRecoveryWindow", "StartupRecoveryPluginScanner", "StartupInitializersCompleted", "MainWindowInitializer", "MainWindowFactory", "CompactMainWindow", "UseCompactMainWindow", "LoadingPlugin", "startup-maintenance", "startup-skip-plugins", "wait-for-process", "safe-start", "skip-plugins", "验证并回退"]
code_paths: ["ColorVision/EntryClass.cs", "ColorVision/App.xaml.cs", "ColorVision/BuiltInModules.cs", "ColorVision/ProgramTimer.cs", "ColorVision/StartWindow.xaml", "ColorVision/StartWindow.xaml.cs", "ColorVision/StartWindow.Presentation.cs", "ColorVision/StartupScene.cs", "ColorVision/StartupUiTrace.cs", "ColorVision/Startup/StartupText.cs", "ColorVision/Startup/StartupResources.resx", "ColorVision/Startup/StartupResources.en.resx", "ColorVision/Startup/StartupResources.zh-Hant.resx", "ColorVision/MainWindow.xaml.cs", "ColorVision/MainWindowFactory.cs", "ColorVision/CompactMainWindow.cs", "ColorVision/MainWindowConfig.cs", "ColorVision/SingleInstanceStartupPolicy.cs", "ColorVision/SingleInstanceStartupCoordinator.cs", "ColorVision/SingleInstanceStartupWindow.xaml", "ColorVision/SingleInstanceStartupWindow.xaml.cs", "UI/ColorVision.UI/Update/ApplicationUpdateProcessCoordinator.Startup.cs", "ColorVision/StartupFileOpenPolicy.cs", "ColorVision/OperationsApplicationRestartController.cs", "ColorVision/App.StartupMaintenance.cs", "ColorVision/Recovery", "UI/ColorVision.UI/Plugins/PluginLoader.cs", "UI/ColorVision.UI/Plugins/PluginRecoveryBackupService.cs"]
test_paths: ["Test/ColorVision.UI.Tests/StartupInitializerSequenceTests.cs", "Test/ColorVision.UI.Tests/StartupUiTraceTests.cs", "Test/ColorVision.UI.Tests/StartupThemeBootstrapTests.cs", "Test/ColorVision.UI.Tests/SingleInstanceStartupTests.cs", "Test/ColorVision.UI.Tests/SingleInstanceStartupCoordinatorTests.cs", "Test/ColorVision.UI.Tests/SingleInstanceStartupWindowTests.cs", "Test/ColorVision.UI.Tests/StartWindowThemeLifecycleTests.cs", "Test/ColorVision.UI.Tests/StartupPresentationTests.cs", "Test/ColorVision.UI.Tests/StartupSceneLifecycleTests.cs", "Test/ColorVision.UI.Tests/StartupRecoveryPluginScannerTests.cs", "Test/ColorVision.UI.Tests/StartupMaintenanceLifecycleTests.cs", "Test/ColorVision.UI.Tests/StartupMaintenanceWindowTests.cs", "Test/ColorVision.UI.Tests/WizardWindowRuntimeTests.cs", "Test/ColorVision.UI.Tests/StartupRecoveryWindowRuntimeTests.cs", "Test/ColorVision.Copilot.Tests/CopilotBackgroundShellMaintenanceGuardTests.cs", "Test/ColorVision.UI.Tests/StartupFileOpenPolicyTests.cs", "Test/ColorVision.UI.Tests/StartupRegistryCheckerTests.cs"]
related: ["platform.architecture", "platform.startup-integrity", "delivery.update", "plugins.model", "ui.discovery", "ui.wizards", "ui.localization", "ui.themes", "ui.search", "operations.main-window", "engine.devices", "engine.rc-registration", "flow.architecture"]
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

单实例判断使用 `Debugger.IsAttached` 和 `APPConfig.IsMute`（“允许多实例”）。未附加调试器且不允许多实例时，新启动会直接强制结束同会话、同安装路径的较早实例，不限于异常进程；`--debug` 不参与这个判断。`SingleInstanceStartupWindow` 展示退出进度，`SingleInstanceStartupCoordinator` 在失败后等待用户重试、仅本次直接多开或取消；[单实例启动恢复](../../00-getting-started/first-steps.md#旧进程未退出时重新启动)维护完整操作契约。取消或直接打开会先取消并等待当前结束操作收尾，避免新实例启动后继续结束其它旧进程。窗口期间临时使用显式关闭模式，返回时恢复原有 MainWindow 与 ShutdownMode，防止辅助窗口关闭导致整个应用退出。确认旧实例退出并取得单实例锁后才重载磁盘配置；重载失败保留自动保存关闭并记录错误。直接打开分支不要求取得单实例锁，也不改写多实例设置。旧版命名管道关闭协议继续保留兼容入口，普通窗口关闭仍使用既有流程。真实启动可能更改配置、替换旧实例并按设置启动功能，文档核验不需要执行它。

普通启动在 `WizardCompletionKey=false` 时进入向导；维护请求或恢复选择也可要求显示向导。向导步骤、保存与重启见[配置向导](../../04-api-reference/ui-components/wizards.md)。启动语言读取及设置中更换语言的区别见[界面语言](../../04-api-reference/ui-components/localization.md)。

单实例直接结束遇到访问拒绝时，调用 `IColorVisionServiceHostClient.TerminateProcessAsync` 复用权限服务；服务检查目标程序白名单及 PID、启动时间和实际路径，旧实例范围与多开决策仍由启动调用方承担。白名单包括 `ColorVision.exe`，不固定安装目录。服务接入、旧版自更新和请求期限见[权限代理契约](../components/service-host.md#通用进程终止)。

## 启动界面与动画边界

`StartWindow.xaml` 以 `820 × 460` WPF 逻辑单位为设计尺寸居中显示，以光线、光谱曲面、轨道与扫描线组成全景光场，不显示品牌大图或滚动日志。窗口采用不透明背景和 `WindowChrome`，整个窗口区域可拖动，不允许手动调整大小，不单独显示任务栏按钮。`MaxWidth`、`MaxHeight` 按 `SystemParameters.WorkArea` 限制窗口尺寸，外层 `Uniform` Viewbox 同时等比缩放背景、文字与进度条，以适应高 DPI 或较小工作区。

`StartWindow.Presentation.cs` 在构造时捕获 `ThemeConfig.StartupTheme`，默认固定深色；只有选择 `FollowApplication` 才读取 App 在前序阶段已应用的 `ThemeManager.Current.CurrentUITheme` 并跟随后续实际配色变化。固定深浅忽略应用主题变更，设置中的新选择留到下次启动读取。配置默认值、旧配置兼容和设置入口见[启动页主题](../../04-api-reference/ui-components/ColorVision.Themes.md#启动页主题)。这只决定启动窗口呈现，不改变 App 应用主题或初始化流程。

深色采用 `#080E19`，浅色采用 `#F4F5F7`；文字、边界、进度条及光场场景同步配色。窗口没有主题切换按钮，不重新加载或写入配置。现有 `SystemThemeChanged` 仅维护启动图标，不决定窗口背景；跟随软件通过实际 UI 主题事件更新，不直接跟随 Windows 配色。呈现层同时监听高对比度变更，使用系统背景、文字和进度色并隐藏装饰光场；关闭时从最初订阅的主题发布者解绑，并移除系统设置订阅。

界面保留版本、单行状态和细进度条；默认状态为“正在准备工作空间”，初始化循环结束时显示“正在打开工作空间”。运行中按当前初始化器显示连接基础服务、载入工作空间、加载检测模板、装配设备面板、准备计算资源或加载扩展组件六类状态。状态复用 `UpdateStartupProgress` 的 Dispatcher 调度，仅在文字变化时更新同一个元素，不追加日志、不增加独立计时器。阶段更新与 `CompleteStartupProgressAsync` 的最终状态、进度完成操作都按 `Normal` 优先级投递，保持先入先出的顺序，防止此前排队的阶段状态覆盖“正在打开工作空间”。

标题、主句、能力说明、阶段状态和进度辅助功能名称由 `StartupText` 与独立的 `StartupResources.resx`、`.en.resx`、`.zh-Hant.resx` 提供。界面文案在构造时通过 `x:Static` 读取 `CultureInfo.CurrentUICulture`，运行阶段在原有状态更新入口读取对应属性；`GetStage` 只按初始化器类型名选择文本，未知类型仍显示加载扩展组件。中文文化使用简繁父文化回退，其他文化仅在启动组件内回退英文，不修改全局语言、格式文化或语言配置，也不添加即时语言切换事件。资源读取不改变初始化器排序、执行或进度完成语义。

`StartupScene.cs` 是纯 WPF 绘制控件，不依赖视频或外部位图素材。普通深浅模式建立五个 `DrawingLayer`，各层的 `DrawingGroup`、`StreamGeometry`、画笔和画刷冻结后复用，并分别使用 `BitmapCache` 缓存绘图。光场摆动、轨道旋转和扫描平移三个动画变换置于各层缓存外，通过 `RenderTransform` 移动已缓存的内容，目标帧率为 `30`，避免因为层内元素移动而逐帧重建整个场景缓存。没有逐帧回调、逐帧几何重建或布局动画。`IsDark` 或高对比度变化时停止动画、移除旧层与缓存，再重建配色并按当前状态恢复动画；高对比度仅保留一个系统背景层。系统关闭客户端区域动画或 WPF 渲染等级为 Tier 0 时显示静态画面；隐藏或 `Unloaded` 时停止动画，卸载时同时解除系统设置与渲染等级事件订阅。

这些约束减少启动界面本身的分配、布局与日志文本刷新开销，不改变 initializer 的工作量。初始化器仍可通过 UI Dispatcher 同步构造控件，例如 `ServiceInitializer` 中的设备显示面板装配；这段工作会占用 UI 线程，因此目标帧率不保证启动全程流畅，也不代表实际启动耗时已经缩短。视觉预览应只承载 `StartupScene` 或隔离的窗口视觉，不触发真实 `StartWindow.ContentRendered` 初始化链。

启动进度继续按步骤和历史耗时权重估算，具体完成语义见下节。窗口初始化时摘除并关闭 `ProgramTimer.InitAppender` 的早期内存缓冲，不附加 UI 日志 appender；文件日志沿用既有 log4net 配置，定位方式见[日志来源与读取](../../01-user-guide/interface/log-viewer.md)。单项初始化失败仍记录后继续，启动链或主窗口创建的外层异常记录完整错误并保留错误弹窗；界面不显示日志不等于没有诊断记录。

## 初始化完成不等于所有功能就绪

| 路径 | 执行与完成含义 |
| --- | --- |
| `StartWindow` 的 `IInitializer` | 首次渲染后在后台发现并构造实例；按 `--skip` 中逗号分隔的精确 Name 排除，再按 Order、Name 排序执行。单项 InitializeAsync 异常记录后继续，循环结束记录 `StartupInitializersCompleted`；这不表示每项成功 |
| 功能启动器 | `--feature` 先按 Header、再按类型名匹配 `IFeatureLauncher`；匹配后执行并清理启动记录，未找到则交给主窗口工厂。Execute 返回不证明该功能后续的异步业务完成 |
| 主窗口选择 | 未指定 `--feature` 或未匹配功能时，`MainWindowFactory.Create` 先检查 Windows build 22000 门禁：低版本无论 `MainWindowConfig.UseCompactMainWindow` 的值为何都创建普通 `MainWindow`；门禁通过后才按该配置创建 `CompactMainWindow` 或普通 `MainWindow`。新开关默认开启，只在本次创建时选择，不原地切换现有窗口；旧 `UseCompactTitleBar` 字段不读取或迁移，新字段已保存的 false/true 则保留 |
| 主窗口初始化 | 主窗口通过 Dispatcher 调用 `IMainWindowInitialized`，按 Order 执行并记录单项异常；该异步链和首次渲染各有完成入口 |
| 启动健康标记 | 主窗口首次 `ContentRendered`、主窗口初始化链结束、功能启动器返回等路径均可调用 `StartupRegistryChecker.Clear()`。首次呈现可以先于某些异步初始化完成，因此 ready 不是设备、数据库或插件业务逐项验收 |

`IInitializer` 的实例构造发生在 `--skip` 过滤之前，跳过其 InitializeAsync 不保证没有构造副作用。程序集过滤、provider 构造及各消费者缓存见[扩展发现与排查](../../04-api-reference/ui-components/ui-runtime-handoff.md)；插件的清单、条件依赖预检和装载失败规则只在[插件装载](../../02-developer-guide/plugin-development/overview.md)中维护。

初始化器发现完成后，以及循环中满足 `180 ms` 让出间隔时，启动链仍等待一次 `Background` 优先级的 Dispatcher 回调。这不是固定时长的休眠，也不表示工作区恢复、服务响应等其他异步任务全部完成。真正依赖 UI 的初始化继续由各初始化器显式调度。

普通 `MainWindow` 保留原生外观；`CompactMainWindow : MainWindow` 继承同一份工作区 XAML 和初始化链，只增加标题栏适配，不第二次构造工作区。在 Windows build 22000 或更高版本，新建配置或缺少 `UseCompactMainWindow` 的升级配置默认选择紧凑主窗口，包括仅有旧字段的配置；在设置中关闭新开关并重启仍可选择旧 `MainWindow`。低于 build 22000 时不显示该设置，工厂直接创建普通 `MainWindow`，不会先构造紧凑类型再回退。门禁通过后，`CompactTitleBarChrome.TryAttach` 仍重复检查系统版本，并检查 DWM、窗口样式等运行条件；除版本外的条件不满足或初始化失败时，才在已有紧凑实例恢复原生外观，不再创建第二个普通主窗口。这样不会重复改写全局工作区、文档宿主和快捷键注册。设置入口、重启与恢复方式、全屏订阅顺序及验证边界见[主窗口与入口装配](../../01-user-guide/interface/main-window.md)。已经匹配的功能启动器、独立文件路由、单实例交接和恢复门禁不由这个开关改变。

启动进度依据步骤和历史耗时估算，不是健康检查结果。定位“进度结束但功能不可用”时，先查具体 initializer 的日志，再查该能力的前提，不能仅依据 `PluginsLoaded`、`StartupInitializersCompleted` 或 ready 排除故障。

### 启动耗时与首次使用计时

设备详情内容采用[设备详情视图按需初始化](../../04-api-reference/engine-components/device-service-chain.md#设备详情视图按需初始化)契约。分析性能时分别记录主窗口首帧、设备卡片装配和详情首次使用，不能把延后执行的工作算作已消除。

| 日志计时 | 覆盖范围与限制 |
| --- | --- |
| `StopAndReport` | 正常主程序路径从 `ProgramTimer.Start` 计时，到主窗口首次 `ContentRendered` 停止；包含启动窗口之前的基础装配、实例交接和插件阶段，也包含 UI 调度与首帧前执行的工作，不代表所有异步主窗口初始化器或隐藏详情已经完成 |
| `Initializer … took … ms` | 当前初始化器的等待与执行时间；不覆盖初始化器发现、构造及所有步骤之间的工作，不能直接相加替代总首帧时间 |
| `Device display controls generated` | `Creation` 是当前显示卡片生成调用的时间，`PanelBuild` 是替换显示集合的时间；未在该调用内初始化的详情内容不计入这两个值 |
| `Device view initialized. View=…, Duration=…ms.` | 一种详情实例首次成功创建 XAML 和初始化内容的时间；可能发生在首帧前，也可能由首次切换详情或收到结果触发，应同时核对它与 `StopAndReport` 的先后 |
| `Startup … took … ms` | 分别记录 App 构造与应用资源加载、基础配置、主题、语言、单实例协调、运维宿主、插件、WinForms、启动窗口构造与 Show；只记录边界内工作，启动恢复对话框及进程进入托管入口前的工作需另行区分 |
| `Startup splash ContentRendered` / `Startup splash ApplicationIdle handoff` | 前者标记启动窗口的首次呈现事件；后者记录事件处理器等待 `ApplicationIdle` 后恢复执行的时间。它们不是主窗口首帧或初始化器执行时间 |
| `Startup worker queue` | 从提交 `Task.Run` 到后台委托开始的等待，不包含随后发现、构造或执行初始化器的时间。此处及单项初始化开始日志中的 `UI` 表示当前入口是否位于 UI 线程，不表示初始化器内部没有 Dispatcher 调用 |
| `Startup UI checkpoint '…' queue` | 从投递 `Background` 回调到该 UI 回调进入时的等待；不包含回调完成后后台 continuation 恢复的时间，也不证明队列外的异步任务完成 |
| `Startup main window queue` | 从投递 `ContextIdle` 回调到该 UI 回调进入时的等待，不包含随后主窗口的构造、Show 和首次呈现 |
| 主窗口构造分段 | `Main window XAML construction` 包含 Initialized 事件；后者另有停靠登记、项目树/设备挂载、布局、主题、视图、菜单等子段。`saved geometry setup` 只记录窗口几何事件登记，实际恢复发生在 Show 内的 SourceInitialized；`remaining constructor setup` 定位构造尾部。父子段不可重复相加 |
| `Main window Show` / `Compact title bar …` | Show 内包含创建 HWND、SourceInitialized 和紧凑标题栏附加；返回不代表 ContentRendered 已发生。标题栏日志细分原生附加与完整处理，不能把此前所有等待归给标题栏 |
| `Main window initializers completed … Count=…, Failures=…` | 异步主窗口初始化链的实际结束与捕获失败数，包含发现和串行等待时间；可早于或晚于首帧。0 次捕获失败不证明外部设备或服务业务健康 |

`ContentRendered` 是 WPF 派发的事件入口，通常以 `Input` 优先级运行；它是统一比较启动的代理指标，不是显示器或 GPU 实际呈现的时间戳。提高回调优先级、改变排队顺序或把工作移到回调之后，不能单独作为启动提速的证据。

需要细分主窗口构造至该事件入口时，可为测试进程设置 `COLORVISION_STARTUP_TRACE=1`。`StartupUiTrace` 最多保留 2048 个 Dispatcher 操作，记录工厂返回、Show 返回、Loaded、事件入口，以及队列和执行耗时。事件入口先冻结记录并卸除 Hooks，`StopAndReport` 后才序列化写文件；默认不创建跟踪对象或订阅 Hooks。`COLORVISION_STARTUP_TRACE_FILE` 指定输出 JSON，未指定时写入临时目录的 `ColorVisionStartupTrace-{进程ID}.json`。创建窗口失败或窗口提前关闭会清理订阅，文件写入失败不阻断启动。

诊断报告中的执行区间可能因嵌套 Dispatcher 重叠；尾段覆盖率使用区间并集，未覆盖时间不能直接归因为空闲或 GPU。Hooks 不覆盖 `OperationCompleted` 之后的任务 continuation、原生消息或订阅前开始的操作；晚到的 Posted 事件不生成负排队时间，超限丢弃量单独报告。跟踪开启时也细分紧凑标题栏设置步骤。Hooks、锁和诊断日志都有观察开销，因此诊断样本与关闭跟踪后的正式性能对照分别记录。

比较启动版本应使用相同配置、资源和启动入口，观察多次运行，并同时检查具体阶段及首次打开详情的成本。队列等待已经包含在覆盖其区间的总耗时内；总计时、父阶段和子阶段不能重复相加，分段相加应先选择互不重叠的边界。日志相邻时间戳的间隙只定位两条记录之间的区间；例如插件末条日志到启动窗口 Initialized 之间还包含插件收尾和窗口构造，没有更细计时就不能全归因于 UI、网络、文件读取或 JIT。构建成功、静态视觉预览或动画目标帧率也不证明实际启动提速。

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

- UI 调度诊断：`StartupUiTraceTests` 使用实际 Dispatcher 与屏幕外合成窗口，检查默认关闭、真实 `ContentRendered` 的 Input 操作关联、合法 JSON 输出、停止后 Hooks 与窗口订阅可被回收，以及 Abort、提前关窗和输出失败的清理。受控跨线程投递验证 Started 之后才到达的 Posted 不产生负队列耗时；不覆盖记录上限、所有嵌套 Dispatcher 情形、真实主窗口完整启动性能或 GPU 呈现。
- 启动窗口生命周期：`StartWindowThemeLifecycleTests` 在 STA WPF 宿主中构造并关闭窗口，检查早期日志缓冲释放、其他 appender 保留、`SystemThemeChanged` 与 `CurrentUIThemeChanged` 订阅恢复及窗口可被 GC；不显示窗口、不执行真实初始化。
- 启动界面呈现：`StartupPresentationTests` 在显示前移除真实 `ContentRendered` 初始化处理器，再将产品窗口显示到屏幕外；覆盖默认深色、固定浅色独立于应用，以及跟随软件策略下的简英繁与明确深浅主题组合、英文 UseSystem 的两种解析结果。固定策略案例验证配置修改不改变当前窗口，关闭重开后读取新策略。检查后续应用主题变化、英文文字与较长阶段状态布局、辅助名称、无切换按钮、调色不改进度和关闭后主题订阅恢复。阶段文本测试检查类型名映射及未知扩展回退；不执行真实初始化、设备连接或启动流程。配置兼容及设置行验证由[启动页主题测试](../../04-api-reference/ui-components/ColorVision.Themes.md#包入口与验证范围)维护。
- 动画生命周期：`StartupSceneLifecycleTests` 将场景承载到真实的屏幕外 WPF 窗口，经历浅色、深色、再浅色及隐藏重显，检查层数、上下边缘像素、旧层脱离，以及关闭后场景和被替换层可被 GC。高对比度断言依据测试机器当前设置，不主动切换系统高对比度；不覆盖实际帧率、整体画面效果或全部静态降级组合。
- 启动分支：`SingleInstanceStartupTests` 检查决策、替换响应及安装范围锁名；`StartupFileOpenPolicyTests` 检查独立文件路由；`StartupRegistryCheckerTests` 使用临时注册表项检查未完成尝试回收。
- 恢复列表：`StartupRecoveryPluginScannerTests` 使用临时清单和无效 DLL 字节，检查记录匹配、旧式和损坏清单目录；不构成真实插件装载或完整备份恢复测试。
- 维护交接：`StartupMaintenanceLifecycleTests` 检查参数、确认、关窗取消、保存失败和失败收尾；`StartupMaintenanceWindowTests`、`WizardWindowRuntimeTests`、`StartupRecoveryWindowRuntimeTests` 用隔离窗口与替身检查 Owner、动作、向导初始化和准备顺序。
- 后台任务门禁：`CopilotBackgroundShellMaintenanceGuardTests` 使用延迟进程替身检查预留、运行和完成状态，不读取输出，不启动真实维护进程。

测试引用不表示本轮已执行，也不证明安装环境中成功重启、还原或设备健康。源码入口已随各节列明；文档检查只验证结构、路径与检索，实际恢复验收需使用获授权的隔离安装和备份。

---
knowledge_id: "operations.main-window"
knowledge_type: "topic"
status: "current"
summary: "主窗口菜单、搜索、状态栏与工作区装配；紧凑主窗口默认启用并保留旧窗口开关，Windows 11 兼容门禁与实际交互边界仍适用。"
aliases: ["主窗口","菜单不见了","搜索框消失","工作区","MainWindow","CompactMainWindow","MainWindowFactory","紧凑主窗口","紧凑标题栏","恢复旧主窗口","标题栏合并菜单","标题栏更多","标题栏按钮位置","标题栏图标颜色","MainWindowActionButtonStyle","TitleBarActionForeground","TitleBarActionInactiveForeground","EnableWindowResizeDiagnostics","MainWindowResizeDiagnostics","window-resize-diagnostics.mode","最大化闪烁","还原闪烁","UseCompactMainWindow","CompactTitleBarChrome","CompactTitleBarLayout","CompactTitleBarActions","AvalonDock","VS2026","停靠标题","文档标签","浮动窗口主题","工具面板三段色","标题右键菜单","单工具面板空白","ToolTabStrip"]
code_paths: ["ColorVision/MainWindow.xaml","ColorVision/MainWindow.xaml.cs","ColorVision/MainWindow.Hotkeys.cs","ColorVision/CompactMainWindow.cs","ColorVision/MainWindowFactory.cs","ColorVision/StartWindow.xaml.cs","ColorVision/MainWindowConfig.cs","ColorVision/Windowing/MainWindowResizeDiagnostics.cs","ColorVision/Windowing/CompactTitleBarChrome.cs","ColorVision/Windowing/CompactTitleBarVisibilityGuard.cs","ColorVision/Windowing/CompactTitleBarLayout.cs","ColorVision/Windowing/CompactTitleBarActions.cs","ColorVision/Themes/AvalonDockTheme.cs","ColorVision/Themes/AvalonDockModernLight.xaml","ColorVision/Themes/AvalonDockModernDark.xaml","ColorVision/Themes/AvalonDockModernTemplates.xaml","ColorVision/Themes/AvalonDockGripTemplates.xaml","ColorVision/Themes/DockingSurfaceBorder.cs","ColorVision/Themes/DockingTabBorder.cs","UI/ColorVision.Themes/Themes/White.xaml","UI/ColorVision.Themes/Themes/Dark.xaml","UI/ColorVision.UI/Menus","UI/ColorVision.UI/Serach/ContextualFindRouter.cs","UI/ColorVision.UI/Serach/SearchWindow.xaml","UI/ColorVision.UI/Serach/SearchWindow.xaml.cs","UI/ColorVision.UI/Serach/SearchWindowHotkeyBridge.cs","UI/ColorVision.Solution/Workspace"]
test_paths: ["Test/ColorVision.UI.Tests/StartupFileOpenPolicyTests.cs","Test/ColorVision.UI.Tests/AvalonDockThemeBindingTests.cs","Test/ColorVision.UI.Tests/MainWindowSearchShellTests.cs","Test/ColorVision.UI.Tests/WindowResizeDiagnosticsContractTests.cs","Test/ColorVision.UI.Tests/CompactTitleBarChromeTests.cs","Test/ColorVision.UI.Tests/CompactTitleBarIntegrationContractTests.cs","Test/ColorVision.UI.Tests/ContextualFindRouterTests.cs","Test/ColorVision.UI.Tests/SearchWindowHotkeyBridgeTests.cs","Test/ColorVision.UI.Tests/SearchWindowHostTests.cs"]
related: ["ui.discovery","ui.menus","ui.hotkeys","ui.search","ui.status-bar","ui.solution","ui.documents","ui.themes","platform.runtime","operations.index","ui.desktop-pet"]
---

# 主窗口与入口装配

主窗口是菜单、搜索、状态栏和停靠工作区的宿主，不是所有业务功能的实现位置。找不到窗口或命令时，先区分宿主显示、扩展发现和功能自身失败，不要求先熟悉整套界面。

## 可见现象与实现入口

| 现象或行为 | 当前实现与检查点 |
| --- | --- |
| 主窗口布局 | `ColorVision/MainWindow.xaml` 定义菜单区、停靠区和状态栏；工作区内容由具体编辑器和扩展提供 |
| 将菜单合并到标题栏 | `MainWindowConfig.UseCompactMainWindow` 默认开启、重启生效；启动工厂选择 `CompactMainWindow` 或保留的普通 `MainWindow`，两种类型复用同一套工作区实现，紧凑外观仍受 Windows 11 兼容门禁约束 |
| 桌面宠物的显示与素材 | `MainWindowConfig.OpenFloatingBall` 控制独立窗口，启用、选择和创建入口见[桌面宠物](../../04-api-reference/ui-components/desktop-pet.md) |
| 查找功能或当前内容 | Ctrl+Shift+P 打开应用搜索；Ctrl+F 按当前内容分流到局部查找或应用搜索 |
| 搜索存在但找不到候选或执行不符合预期 | `MainWindow.Hotkeys.cs` 负责承载与聚焦；Ctrl+F 是场景查找，先交给当前内容，没有局部查找的普通页面才打开应用搜索。候选来源、排序、类型开关和执行检查归[产品搜索](../../04-api-reference/ui-components/search.md)，不是宿主布局问题 |
| 菜单提示了组合键但按键无响应 | `LoadHotKeyFromAssembly()` 独立接入[快捷键注册](../../04-api-reference/ui-components/hotkeys.md)；提示文字不创建注册，先核对具体宿主与模式 |
| 菜单不出现 | `MenuManager.LoadMenuForWindow(MenuItemConstants.MainWindowTarget, Menu1)` 为宿主装配菜单；按[菜单契约](../../04-api-reference/ui-components/menus.md)核对类型缓存、目标窗口、父子可达性和显示过滤，命令检查另行判断 |
| 文档或面板位置不对 | 主窗口给 `WorkspaceManager` 设置布局对象，再挂接 `DockViewManagerHost`；文档分发和布局持久化属于 Solution 工作区 |
| 关闭标签不应清空图像 | `MenuClose.CloseDocumentCommand` 只在活动 LayoutDocument 允许关闭时调用其 Close，沿用未保存确认；不把 ApplicationCommands.Close 的图像清空语义当作关标签 |
| 状态栏缺项或显示旧状态 | 首次渲染后后台优先级调用 `StatusBarManager.Init`，活动文档变化转给 `OnActiveDocumentChanged`；按[状态栏契约](../../04-api-reference/ui-components/status-bar.md)分开查实例缓存、绑定值和文档快照，不把显示状态当成设备完成证明 |
| 窗口已显示但某个模块未就绪 | `LoadIMainWindowInitialized` 按 `Order` 调用扩展初始化并记录启动阶段；主窗口出现不等于所有扩展完成初始化 |

## 紧凑主窗口与标题栏

紧凑主窗口保留系统按钮和原生外边框，把主窗口菜单和右侧入口放进原标题栏的高度范围，为工作区留出更多垂直空间。新配置或缺少 `UseCompactMainWindow` 字段的配置默认启用。旧字段 `UseCompactTitleBar` 不读取、不迁移：升级配置即使保留该旧字段的 false，也使用新开关的默认 true；新字段明确保存的 false 或 true 则继续保留，不在启动时强制覆盖。

打开 **工具 → 选项**，搜索 **紧凑主窗口**，切换 **紧凑主窗口（重启生效）**；关闭设置窗口保存后，重新启动 ColorVision 生效。需要旧窗口或遇到按钮、边框、拖动及兼容性问题时，关闭这个新开关并重启，下次启动回到普通 `MainWindow` 的标准标题栏和独立菜单行。改变设置不会在当前已打开的窗口上即时换框或改变窗口类型。默认启用不等于保证所有显示环境下完全消除最大化、还原时的瞬时帧闪。

`StartWindow` 在无 `--feature` 或未找到匹配功能时调用 `MainWindowFactory.Create(MainWindowConfig.Instance.UseCompactMainWindow)`：false 创建原 `MainWindow`，true 创建独立类型 `CompactMainWindow : MainWindow`；已经匹配的 `IFeatureLauncher` 路径不变。普通 `MainWindow` 的公开构造函数始终走原生外观，不读取此开关。派生窗口调用受保护的基类构造函数复用 `MainWindow.xaml` 和初始化代码，再附加紧凑外观；没有第二份工作区 XAML，也不再次调用 `InitializeComponent`。菜单、状态栏、快捷键、文档和布局服务仍沿用同一套实现，不同时创建两个主窗口。

`CompactMainWindow` 在 `SourceInitialized` 已取得 HWND 后调用 `CompactTitleBarChrome.TryAttach`。该控制器仅接受 Windows 11（系统内部版本 22000 或更高）、DWM 合成已启用的普通 `SingleBorderWindow`，不接管已有 `WindowChrome`、`AllowsTransparency=true` 或无边框窗口。条件不满足或初始化抛出异常时，在同一个 `CompactMainWindow` 实例恢复原生外观并写入日志，不重新构造 `MainWindow`，避免重复初始化全局工作区和快捷键。开关值为 true、窗口类型为 `CompactMainWindow` 都不等于本次已经附加成功；DWM 属性请求的返回码不作为逐项视觉生效证明，仍需检查实际外观。

紧凑外观复用 `Menu1`、`IRightMenuItemProvider` 入口及菜单命令，不改造 `BaseWindow`、关于窗口、搜索窗口或 AvalonDock 浮动窗口。标题栏只把菜单、更新入口、右侧按钮和“更多”标为客户区命中；空白区域交给 `WindowChrome` 的非客户区处理。附加成功后显示宽度为 120 DIP 的 `CompactDragRegion`，以 `GlobalBackground` 和更高绘制层级保留顶层不透明留白，阻止菜单或通知越界占用拖动命中区；保留 WPF 命中并将 `WindowChrome.IsHitTestVisibleInChrome` 设为 false，明确交给原生标题栏处理，而非把命中穿透给底下控件。菜单容器裁切越界绘制，系统按钮还有独立透明占位。最小化、最大化/还原、关闭按钮保留 DWM 绘制与命中，不以 WPF 按钮仿制；按钮占位按窗口 DPI 与可取得的 DWM 边界计算。实际 Snap 菜单、双击、拖动及键盘系统菜单仍需真机验收。

右侧下载、第三方应用、账户等快捷入口共用 `MainWindowActionButtonStyle`：普通 `RightMenuItemPanel` 的隐式按钮样式和 `CompactTitleBarActionButtonStyle` 均基于它。默认图标使用 `TitleBarActionForeground`（浅色 `#303030`、深色 `#D8D8D8`），所属窗口非活动时使用 `TitleBarActionInactiveForeground`（`#808080`）；悬停、按下或获得键盘焦点时恢复 `GlobalTextBrush`，同时保留轻量状态背景、键盘焦点边框和完整点击区域。画刷由 `White.xaml` / `Dark.xaml` 动态提供；`NormalizeRightMenuIcon` 让文本字形绑定祖先按钮的 `Foreground`，使普通与紧凑窗口使用同一套状态配色。图片与 `Viewbox` 只统一尺寸，不将位图内容当作可换色字形。

快捷入口字形尺寸为 15 DIP；普通 `MainWindow` 的按钮保留 20 DIP 宽度，紧凑外观使用 32×28 DIP 的平面按钮。共同的 `CreateRightMenuButton` 将 provider 的 `Header` 用作 ToolTip 和辅助功能名称，不因紧凑样式更换业务命令。紧凑外观下，可用宽度足够时，快捷入口和待更新文字全部显示；宽度不足时，先把快捷入口收进 `CompactActionsOverflowButton`（“更多”），保留可容纳的更新文字；更窄时更新文字也收进“更多”，并用 `CompactUpdateBadge` 蓝点、包含更新提示的 ToolTip 和辅助功能名称提醒。窗口变宽后恢复相应入口。右侧快捷入口与更新有溢出菜单，左侧主菜单仍没有溢出导航，不承诺极窄窗口仍能完整显示全部主菜单。

内部辅助类 `CompactTitleBarActions` 为“更多”生成菜单：快捷项绑定原按钮的 `Command`、`CommandParameter`、`CommandTarget` 和 `IsEnabled`；命令目标为空时以原按钮作为目标。有待更新内容时，菜单用原更新按钮文字生成更新项，复用该按钮的 `Click` 管线，不另造下载或更新逻辑。更新可用性取自 `CombinedUpdateCoordinator.HasPendingStartupUpdate`，不以更新文字是否因布局折叠作为业务状态。

快捷入口组紧靠右侧系统按钮占位，空间不足时由同位置的“更多”替代。120 DIP 拖动留白位于快捷入口组左边，不隔在快捷入口与最小化、最大化/还原、关闭按钮之间。DockPanel 先分配“更多”和快捷入口，再分配拖动区；各自有独立绘制层级，避免极窄布局中较长菜单覆盖右侧按钮。

紧凑标题栏附加成功后，`CompactMainWindow.SetCompactMenuAlignment` 将 `Menu1` 在标题行内部垂直居中，并在原上外边距基础上增加 4 DIP，使可见中心相对居中位置下移约 2 DIP；不通过修改工作区外边距补偿菜单位置。它保存原菜单的外边距与垂直对齐，失败回退和全屏模式恢复原值，退出全屏后再接回紧凑对齐。普通 `MainWindow` 不应用该调整。

窄栏的宽度判断集中在内部辅助类 `CompactTitleBarLayout`，由 `CompactMainWindow` 和隔离的真实 XAML 布局测试共用。它测量菜单、图标、拖动区、快捷入口、待更新文字和“更多”的自然期望宽度；已折叠元素在测量期间暂用 Hidden，随后恢复，不依赖其零宽度或当前分配宽度判断能否显示。待更新状态独立传入，所以窗口变宽即可恢复被布局隐藏的提示，无需等待下一次更新事件；异步提示变化也使用同一套规则，不另加高频 Render 循环。

标题区延伸采用仅顶部的 `WindowChrome.GlassFrameThickness`，不启用全窗模糊、Acrylic 或透明分层窗口。附加期间 `Window.Background` 临时为 Transparent，但 `AllowsTransparency` 保持 false；`Root`、顶栏容器和系统按钮占位不覆盖 DWM 按钮，菜单区域、停靠管理器和状态栏分别用 `GlobalBackground` 保持内容不透明。紧凑路径把 `DockingManager1.Margin` 从普通窗口的 `-2,-3,-2,-2` 改为 0，隔开工作区与玻璃标题区，防止负外边距使内容绘制覆盖系统按钮；原生回退和进入全屏时恢复普通外边距。

颜色由捕获的 `ThemeManager.CurrentUIThemeChanged` 驱动并切回 UI 线程；紧凑路径不并行运行普通 `ApplyCaption`，避免后者重置其标题色。图标通过公共只读辅助方法 `ThemeManagerExtensions.TryLoadPackageIcon` 读取并缓存，优先保留包图标；没有包图标时才按实际主题选用默认深浅图标，详见[窗口主题与图标](../../04-api-reference/ui-components/ColorVision.Themes.md#applycaption-与-basewindow-是不同生命周期)。外边框使用 DWM 默认颜色，不把窗口是否激活绑定到内部文档的选中或活动状态。控制器复用同一个 `WindowChrome`，普通最大化/还原时同步调整客户区内缩，不在下一轮 Loaded 后再改内容边距；标题高度、DPI 或系统设置变化仍合并刷新相关尺寸，不在普通位置变化时重建窗口或模板。窗口状态变化不重复写入 DWM 主题属性；这些实现约束不等于已测得生产工作区性能无退步。

在相同 DPI 与标题内容高度下，最大化/还原共用稳定的 `GlassFrameThickness.Top` 和 `CaptionHeight` 上界。改变这两个属性会让 WPF 重算非客户区并触发 `SWP_FRAMECHANGED`，因此不能随着普通/最大化的客户区内缩反复变化。普通窗口中，上界超出真实标题下沿的窄条属于工作区：控制器仅对此窄条的 `WM_NCHITTEST` 返回 `HTCLIENT`，并排除左右缩放边缘；真正标题留白、系统按钮和其余缩放行为继续交给 `WindowChrome`/DWM。全屏暂停时不做窄条修正，恢复 chrome 后重新建立 hook 顺序。边框重复刷新、内容布局次数和 GPU 实际黑帧是不同指标，消息计数下降不等于所有设备上的动画或黑帧已经验收通过。

`WindowChromeWorker` 更新系统菜单时会临时清除、恢复 `WS_VISIBLE`，包括最大化的 `WM_SIZE` 与状态改变的 `WM_WINDOWPOSCHANGED`；这与窗口尺寸消息次数是不同路径，可能干扰 DWM 过渡画面。`CompactTitleBarVisibilityGuard` 用当前 HWND、当前 UI 线程的 subclass 回调，仅在预期系统菜单更新的直接调用帧内，阻止一次只清除 `WS_VISIBLE` 的样式修改；不吞掉尺寸消息、系统命令或菜单更新。普通同状态缩放没有拦截额度；真实 `WM_SHOWWINDOW(false)` / `SWP_HIDEWINDOW` 撤销在途额度，WPF `Hide()` 与原生 `ShowWindow(SW_HIDE)` 仍可执行。它不处理改标题/图标的显隐路径，不改变 DWM 动画设置或 `WM_NCCALCSIZE` 返回值。该规避针对 .NET 10 `WindowChromeWorker` 的消息顺序，升级 WPF 时需重跑显隐、菜单状态与生命周期测试；不能当作所有第三方原生 hook 都兼容的保证。

全屏继续由原 `SetWindowFull` 路径控制窗口样式与状态。紧凑窗口先注册自己的配置处理器，再注册该共享全屏处理器：进入前暂停 chrome 和显隐保护，恢复普通标题行尺寸、按钮占位与工作区外边距，并隐藏紧凑图标和拖动区；退出并恢复原样式后，通过 Dispatcher 在 Loaded 优先级接回同一 chrome 实例，并同步显隐保护的菜单状态。关闭取消时不提前拆除 chrome；真正 `Closed` 后解除所捕获配置对象和主题管理器上的订阅，并释放窗口 hook 与 subclass；原生 `WM_NCDESTROY` 也清理 subclass。显隐保护附加失败时回退普通外观。窗口位置保存与恢复仍沿用 `WindowConfig`，没有另建一套 DPI 或屏幕布局持久化规则。

共享全屏辅助方法仍通过 `WindowStyle=None` 与 `WindowState=Maximized` 切换；从已最大化状态进入时可能沿用任务栏工作区，这一边界在普通和紧凑窗口对照中均可复现。紧凑标题栏接入未修改该共享方法，不承诺此状态下一定覆盖完整显示器。

## 最大化与还原的诊断构建

`EnableWindowResizeDiagnostics=true` 是仅供开发排查的编译开关，不是用户设置，也不是动画修复。它同时为主程序和 `ST.Library.UI` 定义 `COLORVISION_WINDOW_RESIZE_DIAGNOSTICS`；普通构建不包含诊断类型、窗口 hook、模式文件读取或流程图计时。不要只替换其中一个程序集，也不要将诊断构建作为正常发布包。

关闭准备替换的应用后，在仓库根目录构建；此命令不发布或上传：

```powershell
dotnet build .\ColorVision\ColorVision.csproj -c Debug -p:Platform=x64 -p:EnableWindowResizeDiagnostics=true
```

诊断版可读取自身 EXE 目录中的 `window-resize-diagnostics.mode`：文件内容去掉首尾空白后，`native` 为原生窗口、`compact` 为当前紧凑窗口；仅接受这两个小写值，缺失或其它内容使用原设置。它不写回 `UseCompactMainWindow`。以相同诊断构建、已加载内容、主题、窗口初始尺寸和 DPI 分别测试，避免把轻量样窗与真实工作区当作单变量对照。主程序仍走真实启动初始化，只有在允许启动业务应用的环境中运行；诊断本身不执行流程或设备命令。

`MainWindowFactory` 在窗口创建后、显示前注册只读诊断，附加时晚于紧凑 chrome 初始化。最大化/还原命令和相应尺寸变化开启约一秒的数值采样：记录原生消息前后时序、客户区与 WPF 布局尺寸、标题区高度及 chrome 参数；关联流程图记录真实 `OnRender` 的目标重建、GDI 绘制和像素复制阶段，均使用同一 Stopwatch 时基。固定容量满后累计丢弃数；绘制期间不写文件或强制布局，不订阅 `CompositionTarget.Rendering` 来制造持续帧回调。

在过渡结束后按 **F12** 导出本地 JSON，关闭窗口也尝试导出。文件位于该 EXE 目录的 `window-resize-traces` 下，使用唯一名称；包含配置选择、本次模式覆盖、实际 chrome 附加状态、运行时及数值采样，不包含文档标题、节点文字或配置正文。检查丢弃/诊断错误字段后再解释数据；读取模式文件或导出失败不应阻止正常启动或关闭。窗口关闭时解除事件与 subclass，不保留旧文档编辑器的强引用。

编辑器通过启动时的有界可视树扫描及 Loaded 事件发现；F12 显式导出前也重扫当前窗口，兼容延迟加入的停靠内容，不在 resize/绘制热路径扫描。`EditorDiscovery` 区分已跟踪、仍存活、已有快照及 Loaded/扫描的匹配、错误和上限计数。`Editors` 为空不能解读为流程图没有重绘；F12 才发现的编辑器需要再最大化/还原后重新导出，诊断不会补造此前的绘制记录。

`OnRender` 结束只说明相应调用完成，不是 GPU/DWM Present 完成；消息尺寸和绘制耗时也不能单独证明肉眼闪烁已消失。排查结束后用 `-p:EnableWindowResizeDiagnostics=false` 重新构建普通版本，再进行正式的视觉与交互验收。

## 独立搜索窗口与焦点

`SearchWindow` 是承载 `SearchControl` 的独立 WPF `Window`，不是主窗口内浮层。初始大小为 720×560，最小 420×320；使用 `SingleBorderWindow` 标准标题栏、`CanResize` 和 `CenterOwner`，可直接拖动、调整大小。主窗口设置 `Owner=this`，以非模态 `Show()` 打开，`ShowInTaskbar=false`，不阻止继续操作主窗口。构造或显示窗口本身不查询候选，宿主随后显式调用 `Open` 开始会话。

两项搜索快捷键复用同一个尚未关闭的窗口，重复打开激活并聚焦原输入框，保留查询与原命令目标；已最小化时先恢复。关闭后清除宿主引用，下次才创建新窗口。入口保存当前内容焦点和原活动文档，焦点位于菜单时读取主窗口焦点域记住的内容，不能改为任意活动文档。

点击外部、搜索窗口失焦或主窗口移动/缩放不会关闭搜索。Esc、标准标题栏关闭按钮或提交结果结束搜索会话；主窗口关闭通过 WPF Owner 关系一并关闭搜索窗口。`Closed` 取消当前查询、释放键位桥接。仅在主窗口仍活动、原活动文档与宿主记住的焦点均未改变，且原目标仍在宿主中可用时恢复焦点；用户切到文档 B 后不抢回 A。原内容隐藏或文档已切换时，旧 RoutedCommand 结果被拒绝，不改发给新文档；普通应用命令不受这项文档检查限制，详见[搜索执行契约](../../04-api-reference/ui-components/search.md)。

搜索窗口活动时主窗口通常不活动，`SearchWindowHotkeyBridge` 因此检查搜索窗口自身活动状态，只将当前已注册、属于主窗口的两项应用内搜索动作组合映射为“聚焦已有窗口”。它不执行任意其他业务回调，忽略 IME 占位键、消费重复按键，并沿用快捷键录入/尾键门禁；当前配置清空、改绑、未注册或属于其他宿主时不会用硬编码组合补回。两窗均不活动时入口不主动激活应用，即使动作被配置为系统全局键也一样。搜索仍打开但焦点回到主窗口内容时，Ctrl+F 仍优先局部查找；标准命令、Copilot 适配及原生 Ctrl+F 保留规则归[快捷键](../../04-api-reference/ui-components/hotkeys.md)。

## 停靠外观与主题边界

工作区使用 AvalonDock 4.74.1，停靠标题、文档标签和工具面板边框采用参考 VS2026 的深浅色外观：中性背景、圆角标签与面板、紫色活动边框。文档标题继承标签前景色，长标题省略；普通未选中标签为关闭按钮保留位置，悬停不改变标签宽度。工具面板只剩一个标签时不显示底部标签栏；多个标签时，选中标签沿面板轮廓向下延伸，而不是在标签下画独立下划线。`MainWindow.xaml` 的停靠管理器默认使用外边距 `-2,-3,-2,-2`，仅成功附加紧凑标题栏时由 `CompactMainWindow` 设为 0；背景动态引用 `GlobalBackground`。停靠外观模板本身不改变模型、布局持久化、启动配置或业务命令。

单工具页通过将 `ToolTabStrip.Height` 设为 0 隐藏标签栏的占位，保留该容器的 Visible 状态和 `IsItemsHost` 面板参与布局，仍让 WPF 生成 `TabItem` 并建立选择绑定。不能将包含 ItemsHost 的外层设为 Collapsed：首次布局尚未生成标签容器时，模型虽已选中，控件的 `SelectedContent` 和标题仍可能为空，延迟内容也无法进入 Loaded。恢复多个工具页后标签栏重新取得正常高度。面板内容在关闭重开和布局恢复之间的实例所有权另见[停靠注册、布局恢复和重置](../../04-api-reference/ui-components/editor-document-lifecycle.md#停靠注册、布局恢复和重置)。

停靠主题为管理器、文档标签栏、工具面板模板底板、工具标题、选中底部标签和工具浮窗的标题/主体提供动态引用应用 `GlobalBackground` 的默认背景。`MainWindow.xaml` 中的停靠管理器、状态栏和设备控制 `ScrollViewerDisplay` 外层也显式引用该资源，不依赖窗口背景透出；启用紧凑标题栏时窗口背景暂为透明，业务内容仍有自己的不透明底色。颜色来源是 `UI/ColorVision.Themes/Themes/White.xaml` 和 `Dark.xaml` 的全局资源，不在停靠主题中复制一份全局配色。该统一只针对停靠外壳：选中文档标签及文档内容面板仍使用自身停靠配色，流程网格、图像画布、编辑器和设备卡片等内容继续保留各自的背景资源，不批量改写内容背景。

文档标签最小高度为 26 DIP，内部原生标签布局最小高度为 25 DIP，标签圆角为 3 DIP；工具标签圆角仍为 4 DIP。`IsSelected` 只决定选中页的轮廓与背景，只有 `IsActive` 才给文档标签、标签栏主线和内容面板紫色边框，并令标题文字使用 `SemiBold`。焦点转到工具面板后，文档仍可保持选中及 `IsLastFocusedDocument`，但恢复中性边框和普通字重；多个文档分组也不同时强调各自选中页。这里只读取 AvalonDock 状态改变外观，不重新激活文档或抢回键盘焦点。标题字重仅设置在 `LayoutDocumentTabItem` 模板的 `DocumentHeader` 内容呈现器上，不设置整个 `TabItem` 的字重，避免右键菜单、菜单项及其他子控件继承粗体；原生菜单和命令继续复用。

`DockingTabBorder` 为顶部文档标签与底部工具标签绘制同一轮廓的上下镜像：远离面板的一端为凸圆角，连接面板的两侧为凹肩，选中标签与面板接缝处不重复描边。未选中标签绘制时，在连接面板的一侧留出 1 DIP 的主线区域，启用布局舍入时按当前 DPI 舍入该厚度，防止悬停底色覆盖主线；这只裁切装饰绘制，不缩小原生标签布局或命中区，也不改变选中标签的凹肩。标签贴靠标签栏首/尾边缘时，对应一侧不再向外绘制凹肩。关闭前方相邻标签或重排可能只移动标签而不改变其尺寸，因此仅已加载且选中的真实 `TabItem` 跟踪布局事件，比对贴边状态变化后才调用 `InvalidateVisual`，不在每次布局时重绘。凹肩可以绘制到布局矩形之外，但 `HitTestCore` 将命中范围限制在自身布局矩形内，不抢相邻标签的点击。

WPF `Border.CornerRadius` 只约束边框自身绘制，不会自动裁切子内容。`DockingSurfaceBorder` 在布局时按边框厚度、内边距、DPI 与圆角计算内轮廓，只裁切模板拥有的 `ContentPresenter` 或 `Grid`，避免方形内容覆盖面板圆角；不直接改写实际编辑器的 `Clip`。尺寸或圆角变化后重新计算该裁切。标题和浮窗按钮使用 Windows 系统字体 `Segoe Fluent Icons`，以 `Segoe MDL2 Assets` 为回退，图标字号为 12 DIP；按钮样式默认提供 24×24 DIP 命中区，透明背景且无边框，悬停/按下时才显示状态底色，文档标签关闭按钮单独使用 22×22 DIP。

`AvalonDockTheme` 按“VS2013 基础字典 → `AvalonDockModernLight.xaml` / `AvalonDockModernDark.xaml` 调色板 → `AvalonDockModernTemplates.xaml` 模板”合并资源。VS2013 基础仍提供停靠菜单、命令和图标，不是更换停靠引擎。现代模板内合并保留原文件名的 `AvalonDockGripTemplates.xaml`；该文件现在承载无点状握柄的工具浮动窗口模板，并先合并上游 `Generic.xaml`。不要在现代模板之后再次合并上游通用字典，否则同名浮动窗口样式可能覆盖自定义模板。

上游 `DockingManager` 样式用 `StaticResource` 固定了面板样式，仅添加隐式 `TabItem` 或面板样式不能保证生效。现代管理器样式因此显式重新设置 `DocumentPaneControlStyle`、`AnchorablePaneControlStyle` 和标题模板。`AvalonDockGripTemplates.xaml` 先以唯一键 `ColorVisionBaseDockingManagerStyle` 捕获上游管理器样式，现代样式再基于此键保留整套上游菜单及其余默认 Setter，避免依赖同键查找顺序。颜色通过动态主题资源引用；模板覆盖放在 Theme 字典内，工具背景从应用全局字典取色，使独立加载主题的浮动窗口也能取得同一资源，不依靠主窗口局部资源、Loaded 后遍历修补或关闭绑定诊断。

模板保留 AvalonDock 的真实标题控件、内容宿主、菜单数据上下文和关闭、隐藏、自动隐藏命令；不可关闭但可隐藏的工具文档仍走隐藏命令。浮动工具窗口保留 `WindowChrome` 标题命中区、缩放边框以及最大化、还原和关闭/隐藏命令，去掉装饰握柄不等于取消拖动。文档浮动窗口、自动隐藏标签等未替换的上游模板仅通过调色板协调颜色，不宣称所有上游界面都已重绘。覆盖模板来源和许可证保留在 `ColorVision/Themes/`；升级 AvalonDock 时应复核模板部件、命令和资源解析顺序。

标题与标签的右键菜单和拖动入口使用 AvalonDock 原生 `DropDownControlArea` 与真实标题/标签控件，命中区域覆盖文字及其周围空白，而不是只让文字可点。圆角和凹肩等装饰层不参与命中，标签内边距放在真实原生标签控件内部，避免空白区域由装饰边框截获，导致右键菜单或拖动收不到事件。标题按钮仍保留各自的命令和命中区域；菜单继续使用上游菜单资源及正确的 `LayoutItem` 数据上下文，不另造一套停靠命令或菜单模型。

## 故障定位顺序

1. 记录功能名、窗口宽度、当前文档和本次启动时间；确认是入口缺失，还是入口存在但命令失败。
2. 入口缺失按[UI 发现链](../../04-api-reference/ui-components/ui-runtime-handoff.md)核对程序集和扩展；程序集在磁盘上存在不等于已加载。
3. 文件树与工作区切换查[资源路由](../../04-api-reference/ui-components/ColorVision.Solution.md)，文档保存/关闭和布局查[文档生命周期](../../04-api-reference/ui-components/editor-document-lifecycle.md)；不要把设备控制逻辑写入主窗口来解决显示问题。
4. 命令执行后的业务失败转入对应 Engine、插件或项目主题，结合[日志](./log-viewer.md)定位首个失败阶段。

## 验证范围

`CompactTitleBarIntegrationContractTests` 检查新开关默认开启、缺少新字段及仅有旧字段时使用默认值、新字段明确 false/true 的保留、配置元数据及本地化提示、共享的普通 Window 标记与单份工作区、客户区命中标记、原生按钮透明占位、内容背景接线，以及包图标读取辅助方法的源码契约；还从真实主窗口 XAML 提取隔离标题布局，调用生产共用的 `CompactTitleBarLayout`，检查窄宽往返、异步更新提示变化、自动高度下的布局稳定性、快捷入口与更新文字的分级收纳、“更多”和拖动区的命中，以及仅变宽即可恢复仍有待更新内容的提示，不只断言静态摆放。`CompactTitleBarChromeTests` 使用隔离合成窗口和 HWND，检查附加前提、既有 chrome 不被替换、原生窗口 style 能力、非分层窗口、主题间实例复用、同主题资源刷新但无主题事件时不遮挡系统按钮、全屏暂停/恢复、关闭取消和 Dispose 后释放；不加载生产配置、工作区或设备。系统不满足门禁时，只检查保留普通窗口的分支，不代表紧凑路径已覆盖。测试引用不表示本次已经执行。

`CompactTitleBarIntegrationContractTests` 还用 mock 命令检查原按钮与“更多”菜单的命令、参数、命令目标和禁用状态动态一致、RoutedCommand 仍经过原按钮路由、更新项只进入原更新按钮的 Click 管线，并加载真实按钮模板核对完整点击区域。普通与紧凑按钮使用真实 WPF 控件检查字形继承同一 `Foreground` 画刷、默认配色随浅深主题刷新，以及离屏窗口实际 `IsActive=false` 时的非活动前景与资源刷新；悬停、按下、键盘焦点的前景恢复和覆盖非活动状态的顺序属于模板触发器契约检查。深浅主题下的实际悬停、键盘焦点、蓝点和辅助功能体验仍需视觉与真实输入验收；不应实际执行下载、登录或更新来证明布局正确。

显隐回归由 `CompactTitleBarChromeTests` 向自有 HWND 发出系统最大化/还原命令，记录实际 `WM_STYLECHANGED`，验证切换中不清除可见位，且原生系统菜单的最大化/还原可用状态正确；覆盖全屏暂停后恢复、WPF 与原生隐藏/显示、最小化，以及业务在普通缩放或最大化的 `SizeChanged` 回调中主动隐藏。只检查最终 `IsVisible=true` 不足以证明没有中途显隐。

启用前后的交互与性能验收应使用相同机器、显示器缩放、主题、窗口尺寸和已加载内容，分别比较普通移动、实时缩放、菜单打开、流程编辑与图像操作，而不是用空白窗口推断生产工作区。性能对照应排除 Visual Studio 调试器与 XAML Hot Reload 注入的额外工作，不能只折叠应用内调试工具栏；这并不表示所有闪烁都由调试器导致。至少检查深浅色 × 活动/非活动、最小化与恢复、最大化与还原、关闭被未保存确认取消、标题空白拖动/双击、Snap、系统菜单、窄窗口、全屏往返、多屏混合 DPI 与位置恢复、WebView2 文档，以及 AvalonDock 浮动/重新停靠。自动合约或 HWND 测试不证明这些真实输入、视觉和性能条件已经不退步；发现回归时可关闭“紧凑主窗口”并重启，回到保留的旧主窗口。

关联的 `StartupFileOpenPolicyTests` 覆盖启动文件打开策略；`AvalonDockThemeBindingTests` 在隔离合成工作区中检查深浅色资源、现代面板与标题模板的实际应用、活动/选中状态、主题替换、命令绑定及绘图绑定诊断。像素级检查包括方形内容的圆角裁切、尺寸/圆角变化后裁切更新、上下标签凸角与凹肩、底部选中标签接缝和外绘凹肩的点击边界；布局验证还需覆盖首/尾贴边、关闭前方相邻标签、重排和窗口缩放，不能只检查 `CornerRadius` 属性值。合成渲染用于核对停靠外观，不启动生产主窗口或设备，不表示真机交互已通过。

单工具页回归使用真实主题与离屏 WPF 窗口，从首次布局就只有一个已选工具项开始，检查标签容器、`SelectedContent`、标题、延迟宿主 Loaded 和工厂仅创建一次；再覆盖同一管理器的 1→2→1 工具页变化与主题替换。不直接调用 `Materialize`、强设 UI 的 `SelectedIndex` 或改写测试中的标签栏属性来绕过容器生成问题。

背景与命中路由的验证应加载应用实际深浅色字典，而不是只给合成内容填入与停靠模板相同的测试颜色；分别核对停靠管理器、文档标签栏、工具面板底板、工具标题、选中底部标签及浮窗背景均解析为 `GlobalBackground`。文档选择验证应区分 `IsSelected`、`IsActive` 与 `IsLastFocusedDocument`，覆盖文档→工具→另一文档和多文档分组切换：检查真实标题文字的字重、标签/主线/面板边框及未被修改的选择状态，不通过重新激活文档满足外观断言。悬停像素验证应在不同 DPI 和选中项切换后检查主线仍连续，同时保留原有凹肩与命中边界检查。右键路由用例应从标题和标签的文字区、内边距空白区分别发起，检查命中原生控件、打开正确菜单并带有正确 `LayoutItem`；菜单打开后检查菜单及菜单项仍为普通字重，同时检查装饰层不截获事件。这些资源与路由测试不能替代真实鼠标拖出、浮动、重新停靠或设备内容交互验收。

这些测试不覆盖所有菜单、状态栏、窗口布局或插件初始化，也不证明真实拖拽和浮动窗口最大化已通过。对宿主交互的修改仍需在获准启动应用的环境中检查目标入口、窄窗口、文档切换、自动隐藏、浮动/重新停靠和日志；只读文档核对不能记为这些交互已通过。

`MainWindowSearchShellTests` 检查独立窗口标记与入口接线；`ContextualFindRouterTests` 用隔离内容检查局部查找、禁用状态、菜单焦点和跨面板边界。`SearchWindowHotkeyBridgeTests` 检查当前组合、搜索窗口活动状态、重复按键和捕获门禁；`SearchWindowHostTests` 使用隔离测试窗口，检查原生 Owner 关系、可缩放非模态窗口且无独立任务栏项、移动宿主不关闭、单独关闭后重开与 Owner 关闭联动；会话测试注入合成查询，检查关闭取消和窗口关闭后才执行结果，不运行真实 provider 或生产 MainWindow。列出这些用例不表示它们已经执行；真实输入法、多屏 DPI、拖动和业务文档交互仍需对应运行时验收。

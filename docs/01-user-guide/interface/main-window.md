---
knowledge_id: "operations.main-window"
knowledge_type: "topic"
status: "current"
summary: "主窗口菜单、搜索、状态栏与工作区装配；Windows 11 使用紧凑主窗口，Windows 10 保持原生窗口。"
aliases: ["主窗口","菜单不见了","搜索框消失","工作区","MainWindow","CompactMainWindow","MainWindowFactory","紧凑主窗口","紧凑标题栏","标题栏合并菜单","标题栏更多","标题栏按钮位置","标题栏图标颜色","Windows build 22000","MainWindowActionButtonStyle","TitleBarActionForeground","TitleBarActionInactiveForeground","最大化闪烁","还原闪烁","CompactTitleBarChrome","CompactTitleBarLayout","CompactTitleBarActions","AvalonDock","VS2026","停靠标题","文档标签","固定选项卡","固定标签","图钉","DocumentTabPinManager","浮动窗口主题","浮窗最大化","停靠导航器","停靠预览","工具面板三段色","标题右键菜单","菜单整行点击","标签栏空白背景","文档标签溢出","当前标签不可见","StableDocumentPaneTabPanel","设备控制滚动条","单工具面板空白","ToolTabStrip","工具标题命令","IDockPanelTitleActionProvider","DockPanelTitleAction","新手引导","界面导览","产品导览","首次欢迎","NewUserGuideOverlay","HasShownNewUserGuide"]
code_paths: ["ColorVision/MainWindow.FullScreen.cs","ColorVision/MainWindow.xaml","ColorVision/MainWindow.xaml.cs","ColorVision/MainWindow.NewUserGuide.cs","ColorVision/Guidance","ColorVision/Notifications","ColorVision/MainWindow.Hotkeys.cs","ColorVision/CompactMainWindow.cs","ColorVision/MainWindowFactory.cs","ColorVision/StartWindow.xaml.cs","ColorVision/MainWindowConfig.cs","ColorVision/Windowing/CompactTitleBarChrome.cs","ColorVision/Windowing/CompactTitleBarVisibilityGuard.cs","ColorVision/Windowing/CompactTitleBarLayout.cs","ColorVision/Windowing/CompactTitleBarActions.cs","ColorVision/Themes/AvalonDockTheme.cs","UI/ColorVision.Solution/Themes/AvalonDockTheme.cs","UI/ColorVision.Solution/Themes/AvalonDockModernLight.xaml","UI/ColorVision.Solution/Themes/AvalonDockModernDark.xaml","UI/ColorVision.Solution/Themes/AvalonDockModernTemplates.xaml","UI/ColorVision.Solution/Themes/AvalonDockOverlayTemplates.xaml","UI/ColorVision.Solution/Themes/DockingGuideCanvas.cs","UI/ColorVision.Solution/Themes/DocumentTabCaption.cs","UI/ColorVision.Solution/Themes/AvalonDockGripTemplates.xaml","UI/ColorVision.Solution/Themes/FloatingWindowFrame.cs","UI/ColorVision.Solution/Themes/FloatingDocumentCaption.cs","UI/ColorVision.Solution/Themes/AutoHideTabOpenConverter.cs","UI/ColorVision.Solution/Themes/DockingSurfaceBorder.cs","UI/ColorVision.Solution/Themes/DockingTabBorder.cs","UI/ColorVision.Solution/Themes/StableDocumentPaneTabPanel.cs","UI/ColorVision.Themes/Themes/White.xaml","UI/ColorVision.Themes/Themes/Dark.xaml","UI/ColorVision.Themes/Themes/Controls/Menu.xaml","UI/ColorVision.UI/Menus","UI/ColorVision.UI/Search/ContextualFindRouter.cs","UI/ColorVision.UI/Search/SearchWindow.xaml","UI/ColorVision.UI/Search/SearchWindow.xaml.cs","UI/ColorVision.UI/Search/SearchWindowHotkeyBridge.cs","UI/ColorVision.UI/Docking/DockPanelTitleAction.cs","UI/ColorVision.UI/Display/DisPlayControlPanel.cs","UI/ColorVision.Solution/Workspace","UI/ColorVision.Solution/Workspace/DocumentTabPinManager.cs"]
related: ["ui.discovery","ui.menus","ui.hotkeys","ui.search","ui.status-bar","ui.solution","ui.documents","ui.themes","platform.runtime","operations.index","ui.desktop-pet"]
---

# 主窗口与入口装配

主窗口是菜单、搜索、状态栏和停靠工作区的宿主，不是所有业务功能的实现位置。找不到窗口或命令时，先区分宿主显示、扩展发现和功能自身失败，不要求先熟悉整套界面。

主窗口首次启动的默认尺寸为 `1920 × 1280` WPF 逻辑单位，固定最小尺寸为 `800 × 600`，普通窗口与紧凑主窗口共用，不提供最小尺寸配置项。启用“启动时恢复窗口布局”时优先恢复已保存的尺寸、位置和窗口状态；恢复尺寸受相同最小值约束，没有有效保存尺寸时使用默认值，并适配到屏幕工作区域。

默认停靠布局左侧显示设备控制，中间显示工作流程和已注册的设备视图。可从“视图”打开解决方案资源管理器、日志和 Copilot，或选择“重置窗口布局”恢复默认布局。

底部状态栏随当前文档显示操作和信息。工作流程页显示当前已加载的流程名称和“执行流程”，运行时显示与流程页一致的进度。相机页显示当前图像的尺寸、通道和位深，选中其他文档或清除图像后更新状态栏。

点击主窗口状态栏最右侧的铃铛，或选择“视图 → 通知”，可打开通知面板。首次打开时面板停靠在右侧。可用标题栏图钉在固定和自动隐藏之间切换，自动隐藏时从右侧竖排“通知”标签展开，关闭后也可从原入口重新打开。当前显示“总数为 0”和空状态，“全部消除”保持禁用，尚未接入通知来源。

## 可见现象与实现入口

| 现象或行为 | 当前实现与检查点 |
| --- | --- |
| 主窗口布局 | `ColorVision/MainWindow.xaml` 定义菜单区、停靠区和状态栏。编辑器和扩展提供工作区内容 |
| 首次认识界面 | 新配置在主窗口首次渲染并完成基础装配后显示一次欢迎卡片；开始后依次聚焦顶部菜单、左侧设备控制、活动工作区和底部状态栏。左侧步骤说明可从“视图”菜单打开解决方案资源管理器。导览只介绍界面位置和用途 |
| 桌面宠物的显示与素材 | `MainWindowConfig.OpenFloatingBall` 控制独立窗口，启用、选择和创建入口见[桌面宠物](../../04-api-reference/ui-components/desktop-pet.md) |
| 查找功能或当前内容 | Ctrl+Shift+P 打开应用搜索；Ctrl+F 按当前内容分流到局部查找或应用搜索 |
| 搜索存在但找不到候选或执行不符合预期 | `MainWindow.Hotkeys.cs` 负责承载与聚焦；Ctrl+F 是场景查找，先交给当前内容，没有局部查找的普通页面才打开应用搜索。候选来源、排序、类型开关和执行检查归[产品搜索](../../04-api-reference/ui-components/search.md)，不是宿主布局问题 |
| 菜单提示了组合键但按键无响应 | `LoadHotKeyFromAssembly()` 独立接入[快捷键注册](../../04-api-reference/ui-components/hotkeys.md)；提示文字不创建注册，先核对具体宿主与模式 |
| 菜单不出现 | `MenuManager.LoadMenuForWindow(MenuItemConstants.MainWindowTarget, Menu1)` 为宿主装配菜单；按[菜单契约](../../04-api-reference/ui-components/menus.md)核对类型缓存、目标窗口、父子可达性和显示过滤，命令检查另行判断 |
| 文档或面板位置不对 | 主窗口给 `WorkspaceManager` 设置布局对象，再挂接 `DockViewManagerHost`；文档分发和布局持久化属于 Solution 工作区 |
| 固定文档标签 | [图钉、右键菜单和固定状态](#停靠外观与主题边界) |
| 关闭标签不应清空图像 | `MenuClose.CloseDocumentCommand` 只在活动 LayoutDocument 允许关闭时调用其 Close，沿用未保存确认；不把 ApplicationCommands.Close 的图像清空语义当作关标签 |
| 状态栏缺项或显示旧状态 | 首次渲染后后台优先级调用 `StatusBarManager.Init`，活动文档变化转给 `OnActiveDocumentChanged`；按[状态栏契约](../../04-api-reference/ui-components/status-bar.md)分开查实例缓存、绑定值和文档快照，不把显示状态当成设备完成证明 |
| 窗口已显示但某个模块未就绪 | `LoadIMainWindowInitialized` 按 `Order` 调用扩展初始化并记录启动阶段；主窗口出现不等于所有扩展完成初始化 |

## 界面导览

主窗口使用 `NewUserGuideOverlay` 提供轻量界面导览。自动导览由 `MainWindowConfig.HasShownNewUserGuide` 布尔值控制：程序在首帧后显示欢迎卡片之前先把它设为 `true`，确认保存成功后才显示，并记录配置路径和节名。主程序保存到 `ColorVision.MainWindowConfig`，Spectrum 保存到 `Spectrum.MainWindowConfig`；读取旧短键的兼容规则见[配置持久化](../../04-api-reference/ui-components/configuration.md)。重置主窗口偏好会清除新旧配置节，并在下次启动时重新展示欢迎卡片。

导览步骤只提供“上一步/下一步”和界面说明，不把点击高亮区域当作完成条件。遮罩由目标四周的独立区域组成，高亮孔内仍可点击原控件；目标因布局或可见性无法解析时，退化为全屏遮罩和居中说明，不阻止退出。当前步骤仅定位主窗口视觉树中的稳定区域，不跨 AvalonDock 浮动窗口、右键菜单、下拉弹层或其它独立窗口。

欢迎页和步骤文字随主程序的简体中文、繁体中文和英文资源切换。常规与紧凑主窗口复用同一层导览 XAML；引导不会改变用户已保存的停靠布局。

## 全屏与最大化

主窗口全屏与系统最大化按钮分开：最大化保留系统窗口行为和任务栏工作区；全屏隐藏原生标题栏、系统窗口按钮和边框，并覆盖当前显示器的完整区域。顶部菜单、快捷入口、工作区、文档和状态栏继续保留；全屏时撤去普通窗口用于菜单接缝的负外边距，避免裁掉文档标签边缘。图像预览未处理 F11 时，由主窗口进入全屏。再次按 F11、按 Esc 或点击顶部退出按钮，恢复进入前的位置、大小和普通/最大化状态；不强制置顶，不主动隐藏系统任务栏。

进入全屏会短暂显示退出提示。提示消失后，将鼠标移到屏幕顶边可唤出圆形退出按钮。切换到其他应用时退出浮层随宿主隐藏，避免覆盖其他窗口；全屏的位置和尺寸不会写成下次启动的普通窗口位置。

`SetWindowFull` 把 `IFullScreenState.IsFull` 与 `WindowFullScreenSession` 连接起来；一个宿主只注册一次，重复状态通知不覆盖原窗口快照，关闭后解绑。紧凑 chrome 同时监听共享全屏状态，在更改窗口样式前暂停，恢复窗口后重挂。图像预览有键盘焦点或鼠标位于图像预览内时，F11 优先进入图像全屏；主窗口在按键冒泡阶段处理剩余的 F11。全屏期间的退出键由当前最内层会话统一处理，详见[图像全屏与恢复](../../04-api-reference/ui-components/ColorVision.ImageEditor.md#图像全屏与恢复)。

## 紧凑主窗口与标题栏

主窗口按系统版本选择：Windows 11（build 22000 或更高版本）使用紧凑主窗口，Windows 10 使用普通原生窗口。紧凑主窗口保留系统按钮和原生外边框，把主窗口菜单和右侧入口放进原标题栏的高度范围，为工作区留出更多垂直空间。

启动从 `MainWindowFactory` 选择普通窗口或紧凑窗口。专用 `IFeatureLauncher` 保留自己的入口。紧凑窗口复用原主窗口的工作区、菜单、状态栏与文档服务，不创建第二个工作区。

`CompactTitleBarChrome` 在取得 HWND 后检查 Windows 版本、DWM 和原生边框条件。系统版本过低直接使用普通窗口，已构造的紧凑窗口附加失败时在同一实例恢复原生外观并记录日志。窗口类型和 DWM 属性返回码不足以判断实际附加效果，应查看日志及窗口外观。

紧凑标题区保留系统绘制的最小化、最大化、关闭按钮，空白留作系统拖动区。标题栏配色跟随实际主题和激活状态。窗口与任务栏优先使用客户包图标，默认使用 `ColorVision1.ico`。菜单或右侧入口仍调用原命令。 [窗口主题与图标](../../04-api-reference/ui-components/ColorVision.Themes.md#窗口外观与生命周期)

变窄时，右侧快捷入口先收入“更多”，随后折叠更新文字并以蓝点、提示与辅助功能名称保留更新提醒。恢复宽度后重新显示。更新状态由 `CombinedUpdateCoordinator` 提供，布局折叠不改变该状态。左侧主菜单没有对应的溢出导航。

标题区透明层和工作区不透明背景分别维护，避免遮住 DWM 按钮。紧凑路径在全屏时暂停 chrome，退出后恢复。普通窗口与回退路径保留原工作区边距、主题和窗口位置策略。

为减少最大化和还原时的闪烁，紧凑窗口保持原生标题尺寸稳定，仅修正超出真实标题下沿的狭窄命中区。 `CompactTitleBarVisibilityGuard` 只在预期系统菜单更新期间保护短暂的可见位变化，用户或业务主动隐藏窗口仍生效。该规避依赖当前 WPF 消息顺序，升级 WPF 时复核系统菜单、全屏和显隐行为。真正关闭后清理订阅与原生 hook。

## 独立搜索窗口与焦点

`MainWindow.Hotkeys.cs` 为应用搜索提供独立的 `SearchWindow`。重复打开会复用窗口和查询，Esc 结束会话。候选、命令目标、焦点恢复与快捷键转接见[产品搜索](../../04-api-reference/ui-components/search.md)。

## 停靠外观与主题边界

点击文档标签的图钉，或右键选择“固定选项卡”。`DocumentTabPinManager` 将固定标签排在同一窗格前方，阻止拖动或浮动，关闭仍走文档确认。取消固定后恢复原拖动能力。固定状态只跟随当前打开的标签，重开文档或重启后须重新固定。

单工具页隐藏底部标签栏。文档窗格可通过 `ShowHeader` 隐藏标题行，两种情况下标签容器都要参与首次布局才能显示选中内容。长文档名在标签中间省略，保留首尾及扩展名。标签放不下时，右端箭头上方出现短线；菜单显示完整标题并勾选当前页。切换标签保留文档顺序。实例和布局恢复见[文档生命周期](../../04-api-reference/ui-components/editor-document-lifecycle.md#停靠注册、布局恢复和重置)。

文档标签栏的空白处右键作用于当前选中文档。拖动浮窗时，命中的停靠格从灰色变为蓝色，半透明蓝色区域显示松手后的布局。独立文档窗口显示宿主图标与当前文件名，宽度足够时加上宿主名称。同窗分屏时，标题跟随本窗最近激活的文档。浮窗和分离窗口最大化时，标题与内容位于当前显示器的工作区内。

自动隐藏栏用色条标出当前展开的工具页。工具标题可通过 `IDockPanelTitleActionProvider.TitleActions` 提供面板命令，例如 `DisPlayControlPanel` 的设备配置和控制管理。

主程序与 Spectrum 共用 `UI/ColorVision.Solution/Themes/AvalonDockTheme.cs`。颜色取 `GlobalBackground`、`DockingChromeBackground` 和 `DockingSurfaceBackground`，边框和标签实现见 `DockingSurfaceBorder`、`DockingTabBorder`。主程序继续提供 `ColorVision.AvalonDockTheme`、`/ColorVision;component/Themes/` 入口及公开控件类型转发。

主题按 VS2013 基础字典、深浅调色板、窗格模板、停靠指示器模板的顺序合并。`AvalonDockGripTemplates.xaml` 用 `ColorVisionBaseDockingManagerStyle` 引用上游基础样式。在现代模板之后重新合并上游通用字典可能覆盖浮窗样式。

## 故障定位顺序

1. 记录功能名、窗口宽度、当前文档和本次启动时间；确认是入口缺失，还是入口存在但命令失败。
2. 入口缺失按[UI 发现链](../../04-api-reference/ui-components/ui-runtime-handoff.md)核对程序集和扩展；程序集在磁盘上存在不等于已加载。
3. 文件树与工作区切换查[资源路由](../../04-api-reference/ui-components/ColorVision.Solution.md)，文档保存/关闭和布局查[文档生命周期](../../04-api-reference/ui-components/editor-document-lifecycle.md)；不要把设备控制逻辑写入主窗口来解决显示问题。
4. 命令执行后的业务失败转入对应 Engine、插件或项目主题，结合[日志](./log-viewer.md)定位首个失败阶段。

## 验证范围

用目标 Windows 版本及显示器核对紧凑标题栏、原生按钮和主题刷新。系统不支持紧凑标题栏时走普通窗口分支。

在深浅主题和活动或非活动状态下，用真实输入核对悬停、键盘焦点、蓝点和辅助功能。布局验收使用可见反馈，无需实际下载、登录或更新。

诊断最大化和还原时的闪烁，可查看 `WM_STYLECHANGED` 中间状态和系统菜单可用项。只看切换结束时的可见状态会漏掉过程中的显隐。

用同一机器、显示器缩放、主题、窗口尺寸和业务内容对照启用前后的交互。排除 Visual Studio 调试器与 XAML Hot Reload 的额外开销。检查活动状态、系统菜单、Snap、窄窗口、全屏往返、多屏 DPI、位置恢复、WebView2 与浮动停靠。出现回归可关闭“紧凑主窗口”并重启。

用实际工作区核对启动文件、主题切换、固定标签、背景与选中态、标题栏右键菜单以及浮窗外观。实际输入法、鼠标拖出与重新停靠、业务文档和设备内容须现场验证。

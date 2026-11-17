---
knowledge_id: "operations.main-window"
knowledge_type: "topic"
status: "current"
summary: "主窗口菜单、搜索、状态栏与工作区装配；Windows 11 使用紧凑主窗口，Windows 10 保持原生窗口。"
aliases: ["主窗口","菜单不见了","搜索框消失","工作区","MainWindow","CompactMainWindow","MainWindowFactory","紧凑主窗口","紧凑标题栏","标题栏合并菜单","标题栏更多","标题栏按钮位置","标题栏图标颜色","Windows build 22000","MainWindowActionButtonStyle","TitleBarActionForeground","TitleBarActionInactiveForeground","最大化闪烁","还原闪烁","CompactTitleBarChrome","CompactTitleBarLayout","CompactTitleBarActions","AvalonDock","VS2026","停靠标题","文档标签","固定选项卡","固定标签","图钉","DocumentTabPinManager","浮动窗口主题","浮窗最大化","停靠导航器","停靠预览","工具面板三段色","标题右键菜单","菜单整行点击","标签栏空白背景","文档标签溢出","当前标签不可见","StableDocumentPaneTabPanel","设备控制滚动条","单工具面板空白","ToolTabStrip","工具标题命令","IDockPanelTitleActionProvider","DockPanelTitleAction","新手引导","界面导览","产品导览","首次欢迎","NewUserGuideOverlay","HasShownNewUserGuide"]
code_paths: ["ColorVision/MainWindow.FullScreen.cs","ColorVision/MainWindow.xaml","ColorVision/MainWindow.xaml.cs","ColorVision/MainWindow.NewUserGuide.cs","ColorVision/Guidance","ColorVision/Notifications","ColorVision/MainWindow.Hotkeys.cs","ColorVision/CompactMainWindow.cs","ColorVision/MainWindowFactory.cs","ColorVision/StartWindow.xaml.cs","ColorVision/MainWindowConfig.cs","ColorVision/Windowing/CompactTitleBarChrome.cs","ColorVision/Windowing/CompactTitleBarVisibilityGuard.cs","ColorVision/Windowing/CompactTitleBarLayout.cs","ColorVision/Windowing/CompactTitleBarActions.cs","ColorVision/Themes/AvalonDockTheme.cs","UI/ColorVision.Solution/Themes/AvalonDockTheme.cs","UI/ColorVision.Solution/Themes/AvalonDockModernLight.xaml","UI/ColorVision.Solution/Themes/AvalonDockModernDark.xaml","UI/ColorVision.Solution/Themes/AvalonDockModernTemplates.xaml","UI/ColorVision.Solution/Themes/AvalonDockGripTemplates.xaml","UI/ColorVision.Solution/Themes/DockingSurfaceBorder.cs","UI/ColorVision.Solution/Themes/DockingTabBorder.cs","UI/ColorVision.Solution/Themes/StableDocumentPaneTabPanel.cs","UI/ColorVision.Themes/Themes/White.xaml","UI/ColorVision.Themes/Themes/Dark.xaml","UI/ColorVision.Themes/Themes/Controls/Menu.xaml","UI/ColorVision.UI/Menus","UI/ColorVision.UI/Search/ContextualFindRouter.cs","UI/ColorVision.UI/Search/SearchWindow.xaml","UI/ColorVision.UI/Search/SearchWindow.xaml.cs","UI/ColorVision.UI/Search/SearchWindowHotkeyBridge.cs","UI/ColorVision.UI/Docking/DockPanelTitleAction.cs","UI/ColorVision.UI/Display/DisPlayControlPanel.cs","UI/ColorVision.Solution/Workspace","UI/ColorVision.Solution/Workspace/DocumentTabPinManager.cs"]
related: ["ui.discovery","ui.menus","ui.hotkeys","ui.search","ui.status-bar","ui.solution","ui.documents","ui.themes","platform.runtime","operations.index","ui.desktop-pet"]
---

# 主窗口与入口装配

主窗口是菜单、搜索、状态栏和停靠工作区的宿主，不是所有业务功能的实现位置。找不到窗口或命令时，先区分宿主显示、扩展发现和功能自身失败，不要求先熟悉整套界面。

主窗口首次启动的默认尺寸为 `1920 × 1280` WPF 逻辑单位，固定最小尺寸为 `800 × 600`，普通窗口与紧凑主窗口共用，不提供最小尺寸配置项。启用“启动时恢复窗口布局”时优先恢复已保存的尺寸、位置和窗口状态；恢复尺寸受相同最小值约束，没有有效保存尺寸时使用默认值，并适配到屏幕工作区域。

默认停靠布局左侧显示设备控制，中间显示工作流程和已注册的设备视图。解决方案资源管理器、日志和 Copilot 可以从“视图”菜单打开。已经保存的停靠布局仍会正常恢复，现有用户可通过“视图 → 重置窗口布局”应用新默认布局。

底部状态栏随当前文档显示操作和信息。工作流程页显示当前已加载的流程名称和“执行流程”，运行时显示与流程页一致的进度。相机页显示当前图像的尺寸、通道和位深，选中其他文档或清除图像后更新状态栏。

点击主窗口状态栏最右侧的铃铛，或选择“视图 → 通知”，可打开通知面板。首次打开时面板停靠在右侧。可用标题栏图钉在固定和自动隐藏之间切换，自动隐藏时从右侧竖排“通知”标签展开，关闭后也可从原入口重新打开。当前显示“总数为 0”和空状态，“全部消除”保持禁用，尚未接入通知来源。

## 可见现象与实现入口

| 现象或行为 | 当前实现与检查点 |
| --- | --- |
| 主窗口布局 | `ColorVision/MainWindow.xaml` 定义菜单区、停靠区和状态栏；工作区内容由具体编辑器和扩展提供。设备控制列表宿主不另加顶部外边距，项目间距由显示控件装配层统一放在每项下方；“新建分组”与停靠窗格顶部的原生命令对齐，不再占用滚动内容底部或底部工具标签 |
| 首次认识界面 | 新配置在主窗口首次渲染并完成基础装配后显示一次欢迎卡片；开始后依次聚焦顶部菜单、左侧设备控制、活动工作区和底部状态栏。左侧步骤说明可从“视图”菜单打开解决方案资源管理器。导览只介绍界面位置和用途 |
| 桌面宠物的显示与素材 | `MainWindowConfig.OpenFloatingBall` 控制独立窗口，启用、选择和创建入口见[桌面宠物](../../04-api-reference/ui-components/desktop-pet.md) |
| 查找功能或当前内容 | Ctrl+Shift+P 打开应用搜索；Ctrl+F 按当前内容分流到局部查找或应用搜索 |
| 搜索存在但找不到候选或执行不符合预期 | `MainWindow.Hotkeys.cs` 负责承载与聚焦；Ctrl+F 是场景查找，先交给当前内容，没有局部查找的普通页面才打开应用搜索。候选来源、排序、类型开关和执行检查归[产品搜索](../../04-api-reference/ui-components/search.md)，不是宿主布局问题 |
| 菜单提示了组合键但按键无响应 | `LoadHotKeyFromAssembly()` 独立接入[快捷键注册](../../04-api-reference/ui-components/hotkeys.md)；提示文字不创建注册，先核对具体宿主与模式 |
| 菜单不出现 | `MenuManager.LoadMenuForWindow(MenuItemConstants.MainWindowTarget, Menu1)` 为宿主装配菜单；按[菜单契约](../../04-api-reference/ui-components/menus.md)核对类型缓存、目标窗口、父子可达性和显示过滤，命令检查另行判断 |
| 文档或面板位置不对 | 主窗口给 `WorkspaceManager` 设置布局对象，再挂接 `DockViewManagerHost`；文档分发和布局持久化属于 Solution 工作区 |
| 固定文档标签 | 选中或悬停标签后点击图钉，或右键选择“固定选项卡”；标签进入当前文档窗格最前方的固定组，实心图钉常驻且继续保留关闭按钮。取消固定后移到固定组末尾之后 |
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

真实浮层点击、混合 DPI、多屏切换和生产工作区还需交互验证。

## 紧凑主窗口与标题栏

主窗口按系统版本选择：Windows 11（build 22000 或更高版本）使用紧凑主窗口，Windows 10 使用普通原生窗口。紧凑主窗口保留系统按钮和原生外边框，把主窗口菜单和右侧入口放进原标题栏的高度范围，为工作区留出更多垂直空间。

启动从 `MainWindowFactory` 选择普通窗口或紧凑窗口。专用 `IFeatureLauncher` 保留自己的入口。紧凑窗口复用原主窗口的工作区、菜单、状态栏与文档服务，不创建第二个工作区。

`CompactTitleBarChrome` 在取得 HWND 后检查 Windows 版本、DWM 和原生边框条件。系统版本过低直接使用普通窗口，已构造的紧凑窗口附加失败时在同一实例恢复原生外观并记录日志。窗口类型和 DWM 属性返回码不足以判断实际附加效果，应查看日志及窗口外观。

紧凑标题区保留系统绘制的最小化、最大化、关闭按钮，空白留作系统拖动区。标题栏配色跟随实际主题和激活状态。窗口与任务栏优先使用客户包图标，默认使用 `ColorVision1.ico`。菜单或右侧入口仍调用原命令。 [窗口主题与图标](../../04-api-reference/ui-components/ColorVision.Themes.md#窗口外观与生命周期)

变窄时，右侧快捷入口先收入“更多”，随后折叠更新文字并以蓝点、提示与辅助功能名称保留更新提醒。恢复宽度后重新显示。更新状态由 `CombinedUpdateCoordinator` 提供，布局折叠不改变该状态。左侧主菜单没有对应的溢出导航。

标题区透明层和工作区不透明背景分别维护，避免遮住 DWM 按钮。紧凑路径在全屏时暂停 chrome，退出后恢复。普通窗口与回退路径保留原工作区边距、主题和窗口位置策略。

为减少最大化和还原时的闪烁，紧凑窗口保持原生标题尺寸稳定，仅修正超出真实标题下沿的狭窄命中区。 `CompactTitleBarVisibilityGuard` 只在预期系统菜单更新期间保护短暂的可见位变化，用户或业务主动隐藏窗口仍生效。该规避依赖当前 WPF 消息顺序，升级 WPF 时复核系统菜单、全屏和显隐行为。真正关闭后清理订阅与原生 hook。

共享全屏会话通过无边框普通状态与完整显示器像素矩形定位，避免沿用最大化的任务栏工作区；从最大化进入后，退出仍恢复最大化。菜单属于应用内容，保留可见与点击能力，不随原生标题栏一起隐藏。

## 独立搜索窗口与焦点

`SearchWindow` 是承载 `SearchControl` 的独立 WPF `Window`，不是主窗口内浮层。初始大小为 720×560，最小 420×320；使用 `SingleBorderWindow` 标准标题栏、`CanResize` 和 `CenterOwner`，可直接拖动、调整大小。主窗口设置 `Owner=this`，以非模态 `Show()` 打开，`ShowInTaskbar=false`，不阻止继续操作主窗口。构造或显示窗口本身不查询候选，宿主随后显式调用 `Open` 开始会话。

两项搜索快捷键复用同一个尚未关闭的窗口，重复打开激活并聚焦原输入框，保留查询与原命令目标；已最小化时先恢复。关闭后清除宿主引用，下次才创建新窗口。入口保存当前内容焦点和原活动文档，焦点位于菜单时读取主窗口焦点域记住的内容，不能改为任意活动文档。

点击外部、搜索窗口失焦或主窗口移动/缩放不会关闭搜索。Esc、标准标题栏关闭按钮或提交结果结束搜索会话；主窗口关闭通过 WPF Owner 关系一并关闭搜索窗口。`Closed` 取消当前查询、释放键位桥接。仅在主窗口仍活动、原活动文档与宿主记住的焦点均未改变，且原目标仍在宿主中可用时恢复焦点；用户切到文档 B 后不抢回 A。原内容隐藏或文档已切换时，旧 RoutedCommand 结果被拒绝，不改发给新文档；普通应用命令不受这项文档检查限制，详见[搜索执行契约](../../04-api-reference/ui-components/search.md)。

搜索窗口活动时主窗口通常不活动，`SearchWindowHotkeyBridge` 因此检查搜索窗口自身活动状态，只将当前已注册、属于主窗口的两项应用内搜索动作组合映射为“聚焦已有窗口”。它不执行任意其他业务回调，忽略 IME 占位键、消费重复按键，并沿用快捷键录入/尾键门禁；当前配置清空、改绑、未注册或属于其他宿主时不会用硬编码组合补回。两窗均不活动时入口不主动激活应用，即使动作被配置为系统全局键也一样。搜索仍打开但焦点回到主窗口内容时，Ctrl+F 仍优先局部查找；标准命令、Copilot 适配及原生 Ctrl+F 保留规则归[快捷键](../../04-api-reference/ui-components/hotkeys.md)。

## 停靠外观与主题边界

工作区使用 AvalonDock 4.74.1，停靠标题、文档标签和工具面板边框采用参考 VS2026 的深浅色外观：中性背景、圆角标签与面板、紫色活动边框。文档标题继承标签前景色，长标题省略；普通未选中标签为图钉和关闭按钮保留位置，悬停不改变标签宽度。工具面板只剩一个标签时不显示底部标签栏；多个标签时，选中标签沿面板轮廓向下延伸，而不是在标签下画独立下划线。`MainWindow.xaml` 的停靠管理器默认使用外边距 `-2,-3,-2,-2`，仅成功附加紧凑标题栏时由 `CompactMainWindow` 设为 0；背景动态引用 `GlobalBackground`。除固定动作外，停靠视觉状态本身不改变模型、布局持久化、启动配置或业务命令。

活动、最后聚焦或鼠标悬停的文档标签显示图钉；点击图钉或在标签右键菜单选择“固定选项卡”，`DocumentTabPinManager` 将文档移到同一 `LayoutDocumentPane` 固定组的末尾，该组始终位于普通标签之前。固定后图钉改为实心并常驻，关闭按钮仍按原 `CanClose` 规则显示和执行；文档的 `CanMove` 暂时设为 false，避免拖出或浮动。取消固定会恢复固定前的 `CanMove` 值，并把标签放到其余固定标签之后。多组文档窗格分别维护自己的固定前缀，不跨窗格重排。

固定状态只跟随当前打开的 `LayoutDocument` 实例，不写入 `MainWindowDockLayout.xml`，关闭后重新创建标签或重启应用不会自动恢复固定状态。固定也不绕过关闭确认：图钉旁的关闭按钮、Ctrl+W 和右键关闭仍走原文档关闭管线。

单工具页通过将 `ToolTabStrip.Height` 设为 0 隐藏标签栏的占位，保留该容器的 Visible 状态和 `IsItemsHost` 面板参与布局，仍让 WPF 生成 `TabItem` 并建立选择绑定。不能将包含 ItemsHost 的外层设为 Collapsed：首次布局尚未生成标签容器时，模型虽已选中，控件的 `SelectedContent` 和标题仍可能为空，延迟内容也无法进入 Loaded。恢复多个工具页后标签栏重新取得正常高度。面板内容在关闭重开和布局恢复之间的实例所有权另见[停靠注册、布局恢复和重置](../../04-api-reference/ui-components/editor-document-lifecycle.md#停靠注册、布局恢复和重置)。

停靠内容控件可实现 `IDockPanelTitleActionProvider`，通过 `TitleActions` 返回零个或多个 `DockPanelTitleAction`；每项包含命令、命令参数、提示和图标。`AnchorablePaneTitle` 从当前 `LayoutAnchorable.Content` 读取该接口，把动作按返回顺序放在上方标题栏的下拉、固定、关闭按钮之前，并复用原生 24×24 DIP 按钮样式。未实现接口的内容得到空动作集合，不显示额外按钮，也不会产生缺失属性绑定错误。面板有实际配置时，在这里提供设置齿轮和明确的用途提示；没有配置时不显示占位入口，选中对象的“属性”仍属于工具栏或右键菜单。当前设备控制由 `DisPlayControlPanel` 提供 `DisPlayManager.ConfigureDevicesCommand`（`E710`，添加/配置设备）和 `DisPlayManager.ManageControlsCommand`（`E713`，管理设备控制）。命令行为及持久化由相应模块负责，主窗口仅放置内容控件；各个 `IDisPlayControl` 仍只参与内部列表装配。

停靠主题为管理器、工具面板模板底板、工具标题、选中底部标签和工具浮窗的标题/主体提供动态引用应用 `GlobalBackground` 的默认背景。文档标签栏单独使用 `DockingChromeBackground` 形成一整条浅灰/深灰底板，选中文档标签及内容面板继续使用 `DockingSurfaceBackground`，因此标签后的空白区不会直接透出内容或窗口背景。`MainWindow.xaml` 中的停靠管理器、状态栏和设备控制 `ScrollViewerDisplay` 外层也显式引用 `GlobalBackground`；启用紧凑标题栏时窗口背景暂为透明，业务内容仍有自己的不透明底色。颜色来源是 `UI/ColorVision.Themes/Themes/Palettes/Light.xaml` 和 `Dark.xaml` 的全局资源（旧 White/Dark 入口保留兼容），不在停靠主题中复制一份全局配色。该统一只针对停靠外壳：流程网格、图像画布、编辑器和设备卡片等内容继续保留各自的背景资源，不批量改写内容背景。

文档标签最小高度为 26 DIP，内部原生标签布局最小高度为 25 DIP，标签圆角为 3 DIP；工具标签圆角仍为 4 DIP。`IsSelected` 只决定选中页的轮廓与背景，只有 `IsActive` 才给文档标签、标签栏主线和内容面板紫色边框，并令标题文字使用 `SemiBold`。焦点转到工具面板后，文档仍可保持选中及 `IsLastFocusedDocument`，但恢复中性边框和普通字重；多个文档分组也不同时强调各自选中页。这里只读取 AvalonDock 状态改变外观，不重新激活文档或抢回键盘焦点。标题字重仅设置在 `LayoutDocumentTabItem` 模板的 `DocumentHeader` 内容呈现器上，不设置整个 `TabItem` 的字重，避免右键菜单、菜单项及其他子控件继承粗体；原生菜单和命令继续复用。

`DockingTabBorder` 为顶部文档标签与底部工具标签绘制同一轮廓的上下镜像：远离面板的一端为凸圆角，连接面板的两侧为凹肩，选中标签与面板接缝处不重复描边。未选中标签绘制时，在连接面板的一侧留出 1 DIP 的主线区域，启用布局舍入时按当前 DPI 舍入该厚度，防止悬停底色覆盖主线；这只裁切装饰绘制，不缩小原生标签布局或命中区，也不改变选中标签的凹肩。标签贴靠标签栏首/尾边缘时，对应一侧不再向外绘制凹肩。关闭前方相邻标签或重排可能只移动标签而不改变其尺寸，因此仅已加载且选中的真实 `TabItem` 跟踪布局事件，比对贴边状态变化后才调用 `InvalidateVisual`，不在每次布局时重绘。凹肩可以绘制到布局矩形之外，但 `HitTestCore` 将命中范围限制在自身布局矩形内，不抢相邻标签的点击。

文档内容外框的顶部两角默认使用 4 DIP 圆角，标签栏主线由同一外框绘制，避免直线覆盖圆角。`StableDocumentPaneTabPanel.IsSelectedTabAtStart` 根据实际可见标签的位置更新：选中标签贴靠左边时，只取消内容外框的左上圆角，让标签与外框连续衔接；选中其他位置的标签时，露出的左上角恢复圆角。标签溢出、关闭或重排后仍按可见位置判断，不依赖文档在模型中的索引。

WPF `Border.CornerRadius` 只约束边框自身绘制，不会自动裁切子内容。`DockingSurfaceBorder` 在布局时按边框厚度、内边距、DPI 与圆角计算内轮廓，只裁切模板拥有的 `ContentPresenter` 或 `Grid`，避免方形内容覆盖面板圆角；不直接改写实际编辑器的 `Clip`。尺寸或圆角变化后重新计算该裁切。标题和浮窗按钮使用 Windows 系统字体 `Segoe Fluent Icons`，以 `Segoe MDL2 Assets` 为回退，图标字号为 12 DIP；停靠标题按钮提供 24×24 DIP 命中区，工具浮窗标题高度为 32 DIP、标题按钮为 28×28 DIP，均使用透明默认背景并只在悬停/按下时显示状态底色，文档标签关闭按钮单独使用 22×22 DIP。

`ColorVision.Solution.Themes.AvalonDockTheme` 是主程序与 Spectrum 共用的主题入口，实际字典和三个标签/边框控件位于 `UI/ColorVision.Solution/Themes/`。主程序保留 `ColorVision.AvalonDockTheme` 包装、旧 `/ColorVision;component/Themes/` 字典入口及公开控件的类型转发，既有调用无需改成依赖 Spectrum；Spectrum 直接引用 Solution 公共库，不依赖主程序 EXE。该入口按“VS2013 基础字典 → `AvalonDockModernLight.xaml` / `AvalonDockModernDark.xaml` 调色板 → `AvalonDockModernTemplates.xaml` 模板”合并资源。VS2013 基础仍提供停靠菜单、命令和图标，不是更换停靠引擎。现代模板内合并保留原文件名的 `AvalonDockGripTemplates.xaml`；该文件现在承载无点状握柄的工具浮动窗口模板，并先合并上游 `Generic.xaml`。不要在现代模板之后再次合并上游通用字典，否则同名浮动窗口样式可能覆盖自定义模板。

上游 `DockingManager` 样式用 `StaticResource` 固定了面板样式，仅添加隐式 `TabItem` 或面板样式不能保证生效。现代管理器样式因此显式重新设置 `DocumentPaneControlStyle`、`AnchorablePaneControlStyle` 和标题模板。`AvalonDockGripTemplates.xaml` 先以唯一键 `ColorVisionBaseDockingManagerStyle` 捕获上游管理器样式，现代样式再基于此键保留整套上游菜单及其余默认 Setter，避免依赖同键查找顺序。颜色通过动态主题资源引用；模板覆盖放在 Theme 字典内，工具背景从应用全局字典取色，使独立加载主题的浮动窗口也能取得同一资源，不依靠主窗口局部资源、Loaded 后遍历修补或关闭绑定诊断。

模板保留 AvalonDock 的真实标题控件、内容宿主、菜单数据上下文和关闭、隐藏、自动隐藏命令；文档图钉与标签右键菜单复用同一固定命令，不可关闭但可隐藏的工具文档仍走隐藏命令。普通工具浮窗由外层窗口绘制唯一的 1 DIP 活动边框，直接承载的工具面板不再叠加第二层轮廓，主体只保留 1 DIP 内缩；最大化后取消外边框、圆角、缩放装饰和主体内缩，使内容贴合可用区域，最大化/还原/关闭命令与底部工具标签栏仍保留。`WindowChrome` 继续提供标题命中区和 5 DIP 缩放命中范围，去掉可见缩放留白不等于取消拖动或缩放。文档浮动窗口保持原模板，自动隐藏标签等未替换的上游模板仅通过调色板协调颜色。覆盖模板来源和许可证保留在 `ColorVision/Themes/`；升级 AvalonDock 时应复核模板部件、命令和资源解析顺序。

工具面板从菜单分离到独立窗口时，标题行同时承载面板名称、面板操作、停靠菜单、最大化或还原以及关闭。面板操作由当前内容提供，例如终端的新增会话和设置。

拖动文档或工具浮窗时继续使用 AvalonDock 的真实停靠目标和放置命令，只调整导航器及目标预览的主题资源。浅色导航器使用不透出底层细节的浅灰底、蓝色方向图形和深色箭头；深色导航器使用深灰底、中性灰图形和浅色箭头，避免沿用浅色蓝白外观。命中落点后两种主题都显示低透明度蓝色区域预览和清晰边线；颜色变化不改变可停靠位置、命中范围或布局结果。

标题与标签的右键菜单和拖动入口使用 AvalonDock 原生 `DropDownControlArea` 与真实标题/标签控件，命中区域覆盖文字及其周围空白，而不是只让文字可点。文档标签栏在标签面板下方另放同一原生菜单承载控件：具体标签位于其上方并由标签自身处理右键，标签后的空白区则作用于当前选中文档，因此整行都有一致的菜单入口。圆角和凹肩等装饰层不参与命中，标签内边距放在真实原生标签控件内部，避免空白区域由装饰边框截获。菜单和菜单项显式使用正常字重与横向拉伸，每一项的整行空白都可点击，活动标签只加粗自己的标题文字；标题按钮仍保留各自的命令和命中区域，菜单继续使用上游资源及正确的 `LayoutItem` 数据上下文，不另造一套停靠命令或菜单模型。文档数量超过标签栏宽度时，`StableDocumentPaneTabPanel` 移动可见窗口以露出当前选中标签，但不调用布局模型的子项重排，因此切换文件不会改变固定分组或文档的逻辑顺序。

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

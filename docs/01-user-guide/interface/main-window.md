---
knowledge_id: "operations.main-window"
knowledge_type: "topic"
status: "current"
summary: "主窗口如何挂接菜单、搜索、状态栏和工作区，以及现代停靠外观的主题覆盖与交互边界。"
aliases: ["主窗口","菜单不见了","搜索框消失","工作区","MainWindow","AvalonDock","VS2026","停靠标题","文档标签","浮动窗口主题","工具面板三段色","标题右键菜单","单工具面板空白","ToolTabStrip"]
code_paths: ["ColorVision/MainWindow.xaml","ColorVision/MainWindow.xaml.cs","ColorVision/MainWindow.Hotkeys.cs","ColorVision/Themes/AvalonDockTheme.cs","ColorVision/Themes/AvalonDockModernLight.xaml","ColorVision/Themes/AvalonDockModernDark.xaml","ColorVision/Themes/AvalonDockModernTemplates.xaml","ColorVision/Themes/AvalonDockGripTemplates.xaml","ColorVision/Themes/DockingSurfaceBorder.cs","ColorVision/Themes/DockingTabBorder.cs","UI/ColorVision.Themes/Themes/White.xaml","UI/ColorVision.Themes/Themes/Dark.xaml","UI/ColorVision.UI/Menus","UI/ColorVision.UI/Serach/ContextualFindRouter.cs","UI/ColorVision.UI/Serach/SearchWindow.xaml","UI/ColorVision.UI/Serach/SearchWindow.xaml.cs","UI/ColorVision.UI/Serach/SearchWindowHotkeyBridge.cs","UI/ColorVision.Solution/Workspace"]
test_paths: ["Test/ColorVision.UI.Tests/StartupFileOpenPolicyTests.cs","Test/ColorVision.UI.Tests/AvalonDockThemeBindingTests.cs","Test/ColorVision.UI.Tests/MainWindowSearchShellTests.cs","Test/ColorVision.UI.Tests/ContextualFindRouterTests.cs","Test/ColorVision.UI.Tests/SearchWindowHotkeyBridgeTests.cs","Test/ColorVision.UI.Tests/SearchWindowHostTests.cs"]
related: ["ui.discovery","ui.menus","ui.hotkeys","ui.search","ui.status-bar","ui.solution","ui.documents","platform.runtime","operations.index"]
---

# 主窗口与入口装配

主窗口是菜单、搜索、状态栏和停靠工作区的宿主，不是所有业务功能的实现位置。找不到窗口或命令时，先区分宿主显示、扩展发现和功能自身失败，不要求先熟悉整套界面。

## 可见现象与实现入口

| 现象或行为 | 当前实现与检查点 |
| --- | --- |
| 主窗口布局 | `ColorVision/MainWindow.xaml` 定义菜单区、停靠区和状态栏；工作区内容由具体编辑器和扩展提供 |
| 顶部没有搜索框 | 顶部不再放置搜索按钮或按键提示；通过可配置快捷键（默认 Ctrl+Shift+P）打开独立搜索窗口，不受主窗口宽度影响。工具与编辑菜单不再提供搜索入口 |
| 搜索存在但找不到候选或执行不符合预期 | `MainWindow.Hotkeys.cs` 负责承载与聚焦；Ctrl+F 是场景查找，先交给当前内容，没有局部查找的普通页面才打开应用搜索。候选来源、排序、类型开关和执行检查归[产品搜索](../../04-api-reference/ui-components/search.md)，不是宿主布局问题 |
| 菜单提示了组合键但按键无响应 | `LoadHotKeyFromAssembly()` 独立接入[快捷键注册](../../04-api-reference/ui-components/hotkeys.md)；提示文字不创建注册，先核对具体宿主与模式 |
| 菜单不出现 | `MenuManager.LoadMenuForWindow(MenuItemConstants.MainWindowTarget, Menu1)` 为宿主装配菜单；按[菜单契约](../../04-api-reference/ui-components/menus.md)核对类型缓存、目标窗口、父子可达性和显示过滤，命令检查另行判断 |
| 文档或面板位置不对 | 主窗口给 `WorkspaceManager` 设置布局对象，再挂接 `DockViewManagerHost`；文档分发和布局持久化属于 Solution 工作区 |
| 关闭标签不应清空图像 | `MenuClose.CloseDocumentCommand` 只在活动 LayoutDocument 允许关闭时调用其 Close，沿用未保存确认；不把 ApplicationCommands.Close 的图像清空语义当作关标签 |
| 状态栏缺项或显示旧状态 | 首次渲染后后台优先级调用 `StatusBarManager.Init`，活动文档变化转给 `OnActiveDocumentChanged`；按[状态栏契约](../../04-api-reference/ui-components/status-bar.md)分开查实例缓存、绑定值和文档快照，不把显示状态当成设备完成证明 |
| 窗口已显示但某个模块未就绪 | `LoadIMainWindowInitialized` 按 `Order` 调用扩展初始化并记录启动阶段；主窗口出现不等于所有扩展完成初始化 |

## 独立搜索窗口与焦点

`SearchWindow` 是承载 `SearchControl` 的独立 WPF `Window`，不是主窗口内浮层。初始大小为 720×560，最小 420×320；使用 `SingleBorderWindow` 标准标题栏、`CanResize` 和 `CenterOwner`，可直接拖动、调整大小。主窗口设置 `Owner=this`，以非模态 `Show()` 打开，`ShowInTaskbar=false`，不阻止继续操作主窗口。构造或显示窗口本身不查询候选，宿主随后显式调用 `Open` 开始会话。

两项搜索快捷键复用同一个尚未关闭的窗口，重复打开激活并聚焦原输入框，保留查询与原命令目标；已最小化时先恢复。关闭后清除宿主引用，下次才创建新窗口。入口保存当前内容焦点和原活动文档，焦点位于菜单时读取主窗口焦点域记住的内容，不能改为任意活动文档。

点击外部、搜索窗口失焦或主窗口移动/缩放不会关闭搜索。Esc、标准标题栏关闭按钮或提交结果结束搜索会话；主窗口关闭通过 WPF Owner 关系一并关闭搜索窗口。`Closed` 取消当前查询、释放键位桥接。仅在主窗口仍活动、原活动文档与宿主记住的焦点均未改变，且原目标仍在宿主中可用时恢复焦点；用户切到文档 B 后不抢回 A。原内容隐藏或文档已切换时，旧 RoutedCommand 结果被拒绝，不改发给新文档；普通应用命令不受这项文档检查限制，详见[搜索执行契约](../../04-api-reference/ui-components/search.md)。

搜索窗口活动时主窗口通常不活动，`SearchWindowHotkeyBridge` 因此检查搜索窗口自身活动状态，只将当前已注册、属于主窗口的两项应用内搜索动作组合映射为“聚焦已有窗口”。它不执行任意其他业务回调，忽略 IME 占位键、消费重复按键，并沿用快捷键录入/尾键门禁；当前配置清空、改绑、未注册或属于其他宿主时不会用硬编码组合补回。两窗均不活动时入口不主动激活应用，即使动作被配置为系统全局键也一样。搜索仍打开但焦点回到主窗口内容时，Ctrl+F 仍优先局部查找；标准命令、Copilot 适配及原生 Ctrl+F 保留规则归[快捷键](../../04-api-reference/ui-components/hotkeys.md)。

## 停靠外观与主题边界

工作区使用 AvalonDock 4.74.1，停靠标题、文档标签和工具面板边框采用参考 VS2026 的深浅色外观：中性背景、圆角标签与面板、紫色活动边框，不再绘制标题中的点状握柄。文档标题继承标签前景色，长标题省略；普通未选中标签为关闭按钮保留位置，悬停不改变标签宽度。工具面板只剩一个标签时不显示底部标签栏；多个标签时，选中标签沿面板轮廓向下延伸，而不是在标签下画独立下划线。`MainWindow.xaml` 的停靠管理器沿用紧凑外边距 `-2,-3,-2,-2` 和透明背景；该宿主布局设置与单工具页的内容生成修复分开维护，不把非负外边距当作模板生效的前提。外观模板不改变停靠模型、布局持久化、启动配置或业务命令。

单工具页通过将 `ToolTabStrip.Height` 设为 0 隐藏标签栏的占位，保留该容器的 Visible 状态和 `IsItemsHost` 面板参与布局，仍让 WPF 生成 `TabItem` 并建立选择绑定。不能将包含 ItemsHost 的外层设为 Collapsed：首次布局尚未生成标签容器时，模型虽已选中，控件的 `SelectedContent` 和标题仍可能为空，延迟内容也无法进入 Loaded。恢复多个工具页后标签栏重新取得正常高度。面板内容在关闭重开和布局恢复之间的实例所有权另见[停靠注册、布局恢复和重置](../../04-api-reference/ui-components/editor-document-lifecycle.md#停靠注册、布局恢复和重置)。

停靠主题为管理器、文档标签栏、工具面板模板底板、工具标题、选中底部标签和工具浮窗的标题/主体提供动态引用应用 `GlobalBackground` 的默认背景；主窗口管理器的局部透明背景则透出窗口底色。`MainWindow.xaml` 中设备控制的 `ScrollViewerDisplay` 外层也引用该资源，不再使用 `GlobalBorderBrush1`，避免标题、内容空白、底部标签分别出现三种底色。颜色来源是 `UI/ColorVision.Themes/Themes/White.xaml` 和 `Dark.xaml` 的全局资源，不在停靠主题中复制一份全局配色。该统一只针对停靠外壳：选中文档标签及文档内容面板仍使用自身停靠配色，流程网格、图像画布、编辑器和设备卡片等内容继续保留各自的背景资源，不批量改写内容背景。

文档标签最小高度为 26 DIP，内部原生标签布局最小高度为 25 DIP，标签圆角为 3 DIP；工具标签圆角仍为 4 DIP。选中文档标签始终使用 `SemiBold`；焦点转到工具面板后，仍被选中且为 `IsLastFocusedDocument` 的文档标签保留紫色边框，文档面板同步读取选中项的该状态。这里只根据 AvalonDock 状态改变外观，不重新激活文档或抢回键盘焦点。

`DockingTabBorder` 为顶部文档标签与底部工具标签绘制同一轮廓的上下镜像：远离面板的一端为凸圆角，连接面板的两侧为凹肩，选中标签与面板接缝处不重复描边。未选中标签绘制时，在连接面板的一侧留出 1 DIP 的主线区域，启用布局舍入时按当前 DPI 舍入该厚度，防止悬停底色覆盖主线；这只裁切装饰绘制，不缩小原生标签布局或命中区，也不改变选中标签的凹肩。标签贴靠标签栏首/尾边缘时，对应一侧不再向外绘制凹肩。关闭前方相邻标签或重排可能只移动标签而不改变其尺寸，因此仅已加载且选中的真实 `TabItem` 跟踪布局事件，比对贴边状态变化后才调用 `InvalidateVisual`，不在每次布局时重绘。凹肩可以绘制到布局矩形之外，但 `HitTestCore` 将命中范围限制在自身布局矩形内，不抢相邻标签的点击。

WPF `Border.CornerRadius` 只约束边框自身绘制，不会自动裁切子内容。`DockingSurfaceBorder` 在布局时按边框厚度、内边距、DPI 与圆角计算内轮廓，只裁切模板拥有的 `ContentPresenter` 或 `Grid`，避免方形内容覆盖面板圆角；不直接改写实际编辑器的 `Clip`。尺寸或圆角变化后重新计算该裁切。标题和浮窗按钮使用 Windows 系统字体 `Segoe Fluent Icons`，以 `Segoe MDL2 Assets` 为回退，图标字号为 12 DIP；按钮样式默认提供 24×24 DIP 命中区，透明背景且无边框，悬停/按下时才显示状态底色，文档标签关闭按钮单独使用 22×22 DIP。

`AvalonDockTheme` 按“VS2013 基础字典 → `AvalonDockModernLight.xaml` / `AvalonDockModernDark.xaml` 调色板 → `AvalonDockModernTemplates.xaml` 模板”合并资源。VS2013 基础仍提供停靠菜单、命令和图标，不是更换停靠引擎。现代模板内合并保留原文件名的 `AvalonDockGripTemplates.xaml`；该文件现在承载无点状握柄的工具浮动窗口模板，并先合并上游 `Generic.xaml`。不要在现代模板之后再次合并上游通用字典，否则同名浮动窗口样式可能覆盖自定义模板。

上游 `DockingManager` 样式用 `StaticResource` 固定了面板样式，仅添加隐式 `TabItem` 或面板样式不能保证生效。现代管理器样式因此显式重新设置 `DocumentPaneControlStyle`、`AnchorablePaneControlStyle` 和标题模板。在本项目编译后的嵌套资源组合中，直接对同一隐式类型键使用 `BasedOn` 曾未能建立继承链，导致默认菜单为空；这不代表所有同键 `BasedOn` 都失效。`AvalonDockGripTemplates.xaml` 先以唯一键 `ColorVisionBaseDockingManagerStyle` 捕获上游管理器样式，现代样式再基于此键保留整套上游菜单及其余默认 Setter，避免依赖同键查找顺序。颜色通过动态主题资源引用；模板覆盖放在 Theme 字典内，工具背景从应用全局字典取色，使独立加载主题的浮动窗口也能取得同一资源，不依靠主窗口局部资源、Loaded 后遍历修补或关闭绑定诊断。移除点状绘图同时去掉了旧模板中依赖隐藏矩形 `Fill` 的 `GeometryDrawing.Brush` / `ElementName` 绑定，避免其缺少 WPF 继承上下文时的绑定失败。

模板保留 AvalonDock 的真实标题控件、内容宿主、菜单数据上下文和关闭、隐藏、自动隐藏命令；不可关闭但可隐藏的工具文档仍走隐藏命令。浮动工具窗口保留 `WindowChrome` 标题命中区、缩放边框以及最大化、还原和关闭/隐藏命令，去掉装饰握柄不等于取消拖动。文档浮动窗口、自动隐藏标签等未替换的上游模板仅通过调色板协调颜色，不宣称所有上游界面都已重绘。覆盖模板来源和许可证保留在 `ColorVision/Themes/`；升级 AvalonDock 时应复核模板部件、命令和资源解析顺序。

标题与标签的右键菜单和拖动入口使用 AvalonDock 原生 `DropDownControlArea` 与真实标题/标签控件，命中区域覆盖文字及其周围空白，而不是只让文字可点。圆角和凹肩等装饰层不参与命中，标签内边距放在真实原生标签控件内部，避免空白区域由装饰边框截获，导致右键菜单或拖动收不到事件。标题按钮仍保留各自的命令和命中区域；菜单继续使用上游菜单资源及正确的 `LayoutItem` 数据上下文，不另造一套停靠命令或菜单模型。

## 故障定位顺序

1. 记录功能名、窗口宽度、当前文档和本次启动时间；确认是入口缺失，还是入口存在但命令失败。
2. 入口缺失按[UI 发现链](../../04-api-reference/ui-components/ui-runtime-handoff.md)核对程序集和扩展；程序集在磁盘上存在不等于已加载。
3. 文件树与工作区切换查[资源路由](../../04-api-reference/ui-components/ColorVision.Solution.md)，文档保存/关闭和布局查[文档生命周期](../../04-api-reference/ui-components/editor-document-lifecycle.md)；不要把设备控制逻辑写入主窗口来解决显示问题。
4. 命令执行后的业务失败转入对应 Engine、插件或项目主题，结合[日志](./log-viewer.md)定位首个失败阶段。

## 验证范围

关联的 `StartupFileOpenPolicyTests` 覆盖启动文件打开策略；`AvalonDockThemeBindingTests` 在隔离合成工作区中检查深浅色资源、现代面板与标题模板的实际应用、活动/选中状态、主题替换、命令绑定及绘图绑定诊断。像素级检查包括方形内容的圆角裁切、尺寸/圆角变化后裁切更新、上下标签凸角与凹肩、底部选中标签接缝和外绘凹肩的点击边界；布局验证还需覆盖首/尾贴边、关闭前方相邻标签、重排和窗口缩放，不能只检查 `CornerRadius` 属性值。合成渲染用于核对停靠外观，不启动生产主窗口或设备，不表示真机交互已通过。

单工具页回归使用真实主题与离屏 WPF 窗口，从首次布局就只有一个已选工具项开始，检查标签容器、`SelectedContent`、标题、延迟宿主 Loaded 和工厂仅创建一次；再覆盖同一管理器的 1→2→1 工具页变化与主题替换。不直接调用 `Materialize`、强设 UI 的 `SelectedIndex` 或改写测试中的标签栏属性来绕过容器生成问题。

背景与命中路由的验证应加载应用实际深浅色字典，而不是只给合成内容填入与停靠模板相同的测试颜色；分别核对停靠管理器、文档标签栏、工具面板底板、工具标题、选中底部标签及浮窗背景均解析为 `GlobalBackground`。文档选择验证应区分 `IsSelected`、`IsActive` 与 `IsLastFocusedDocument`，检查焦点转入工具面板后的字重和标签/面板边框，不通过重新激活文档满足外观断言。悬停像素验证应在不同 DPI 和选中项切换后检查主线仍连续，同时保留原有凹肩与命中边界检查。右键路由用例应从标题和标签的文字区、内边距空白区分别发起，检查命中原生控件、打开正确菜单并带有正确 `LayoutItem`，同时检查装饰层不截获事件。这些资源与路由测试不能替代真实鼠标拖出、浮动、重新停靠或设备内容交互验收。

这些测试不覆盖所有菜单、状态栏、窗口布局或插件初始化，也不证明真实拖拽和浮动窗口最大化已通过。对宿主交互的修改仍需在获准启动应用的环境中检查目标入口、窄窗口、文档切换、自动隐藏、浮动/重新停靠和日志；只读文档核对不能记为这些交互已通过。

`MainWindowSearchShellTests` 检查独立窗口标记与入口接线；`ContextualFindRouterTests` 用隔离内容检查局部查找、禁用状态、菜单焦点和跨面板边界。`SearchWindowHotkeyBridgeTests` 检查当前组合、搜索窗口活动状态、重复按键和捕获门禁；`SearchWindowHostTests` 使用隔离测试窗口，检查原生 Owner 关系、可缩放非模态窗口且无独立任务栏项、移动宿主不关闭、单独关闭后重开与 Owner 关闭联动；会话测试注入合成查询，检查关闭取消和窗口关闭后才执行结果，不运行真实 provider 或生产 MainWindow。列出这些用例不表示它们已经执行；真实输入法、多屏 DPI、拖动和业务文档交互仍需对应运行时验收。

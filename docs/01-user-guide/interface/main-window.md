---
knowledge_id: "operations.main-window"
knowledge_type: "topic"
status: "current"
summary: "主窗口如何挂接菜单、搜索、状态栏和工作区，以及入口缺失时应核对的代码边界。"
aliases: ["主窗口","菜单不见了","搜索框消失","工作区","MainWindow"]
code_paths: ["ColorVision/MainWindow.xaml","ColorVision/MainWindow.xaml.cs","ColorVision/MainWindow.Hotkeys.cs","ColorVision/Themes/AvalonDockTheme.cs","ColorVision/Themes/AvalonDockGripTemplates.xaml","UI/ColorVision.UI/Menus","UI/ColorVision.UI/Serach/ContextualFindRouter.cs","UI/ColorVision.UI/Serach/SearchPopupHotkeyBridge.cs","UI/ColorVision.Solution/Workspace"]
test_paths: ["Test/ColorVision.UI.Tests/StartupFileOpenPolicyTests.cs","Test/ColorVision.UI.Tests/AvalonDockThemeBindingTests.cs","Test/ColorVision.UI.Tests/MainWindowSearchShellTests.cs","Test/ColorVision.UI.Tests/ContextualFindRouterTests.cs","Test/ColorVision.UI.Tests/SearchPopupHotkeyBridgeTests.cs","Test/ColorVision.UI.Tests/SearchPopupAirspaceTests.cs"]
related: ["ui.discovery","ui.menus","ui.hotkeys","ui.search","ui.status-bar","ui.solution","platform.runtime","operations.index"]
---

# 主窗口与入口装配

主窗口是菜单、搜索、状态栏和停靠工作区的宿主，不是所有业务功能的实现位置。找不到窗口或命令时，先区分宿主显示、扩展发现和功能自身失败，不要求先熟悉整套界面。

## 可见现象与实现入口

| 现象或行为 | 当前实现与检查点 |
| --- | --- |
| 主窗口布局 | `ColorVision/MainWindow.xaml` 定义菜单/搜索区域、停靠区和状态栏；工作区内容由具体编辑器和扩展提供 |
| 搜索入口随窗口变窄变简或消失 | 顶部 `SearchEntryButton` 在窗口宽度小于 900 时缩为 150 并隐藏按键标签，否则宽 260；宽度不足 `Menu1.ActualWidth + 180` 时隐藏按钮。可配置的“搜索命令与功能”（默认 Ctrl+Shift+P）仍打开同一个浮层，不再创建第二个窄窗搜索框 |
| 搜索存在但找不到候选或执行不符合预期 | `MainWindow.Hotkeys.cs` 负责承载与聚焦；Ctrl+F 是场景查找，先交给当前内容，没有局部查找的普通页面才打开应用搜索。候选来源、排序、类型开关和执行检查归[产品搜索](../../04-api-reference/ui-components/search.md)，不是宿主布局问题 |
| 菜单提示了组合键但按键无响应 | `LoadHotKeyFromAssembly()` 独立接入[快捷键注册](../../04-api-reference/ui-components/hotkeys.md)；提示文字不创建注册，先核对具体宿主与模式 |
| 菜单不出现 | `MenuManager.LoadMenuForWindow(MenuItemConstants.MainWindowTarget, Menu1)` 为宿主装配菜单；按[菜单契约](../../04-api-reference/ui-components/menus.md)核对类型缓存、目标窗口、父子可达性和显示过滤，命令检查另行判断 |
| 文档或面板位置不对 | 主窗口给 `WorkspaceManager` 设置布局对象，再挂接 `DockViewManagerHost`；文档分发和布局持久化属于 Solution 工作区 |
| 关闭标签不应清空图像 | `MenuClose.CloseDocumentCommand` 只在活动 LayoutDocument 允许关闭时调用其 Close，沿用未保存确认；不把 ApplicationCommands.Close 的图像清空语义当作关标签 |
| 状态栏缺项或显示旧状态 | 首次渲染后后台优先级调用 `StatusBarManager.Init`，活动文档变化转给 `OnActiveDocumentChanged`；按[状态栏契约](../../04-api-reference/ui-components/status-bar.md)分开查实例缓存、绑定值和文档快照，不把显示状态当成设备完成证明 |
| 窗口已显示但某个模块未就绪 | `LoadIMainWindowInitialized` 按 `Order` 调用扩展初始化并记录启动阶段；主窗口出现不等于所有扩展完成初始化 |

## 搜索浮层与焦点

`CommandSearchPopup` 是附着于主窗口 `Root` 的 WPF `Popup`，里面只有一个 `CommandSearchControl`。它使用独立的原生绘制表面越过 WebView2 / `HwndHost` 的 airspace 限制，但不是独立业务 `Window` 或任务栏窗口；不隐藏原编辑器来模拟遮挡。Popup 的[独立视觉树和屏幕尺寸限制](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/popup)是采用面板大小、而非整窗透明遮罩的原因。

打开时将面板宽设为 `min(720, Root.ActualWidth - 32)`，以 Root 为基准水平居中、顶部偏移 48；高度不超过 Root 高度减 64。顶部入口、菜单和快捷键复用同一实例及打开会话，重复打开仅聚焦，不替换原命令目标。按钮的鼠标按下在取得焦点前保存内容焦点；菜单入口则读取主窗口焦点域记住的内容，不能改为任意活动文档。

`StaysOpen=false` 负责点击外部关闭；Esc 由搜索控件处理。宿主位置、大小、窗口状态变化或失活也关闭。正常关闭恢复原目标焦点，原目标已经移除时仅回到停靠区；这不是授权将原命令改发给其他文档。失活、隐藏和关闭过程中不恢复键盘焦点，不主动激活窗口。因此即使搜索动作被配置为全局热键，也只在主窗口已经可见且活动时展开。

Popup 的键盘事件可能不经过主窗口后端，`SearchPopupHotkeyBridge` 仅将当前已注册的两项搜索动作组合映射为“聚焦已有面板”。它不执行任意其他业务回调，忽略 IME 占位键、消费重复按键，并沿用快捷键录入/尾键门禁；当前配置清空、改绑、未注册或属于其他宿主时不会用硬编码组合补回。场景查找的标准命令、Copilot 适配及原生 Ctrl+F 保留规则归[快捷键](../../04-api-reference/ui-components/hotkeys.md)。

## 故障定位顺序

主窗口通过 `AvalonDockTheme` 应用 VS2013 深/浅色字典，并为停靠标题、浮动窗口标题覆盖两份握柄模板。AvalonDock 4.74.1 原模板把 `GeometryDrawing.Brush` 绑定到隐藏矩形的 `Fill`，绘图对象在缺少 WPF 继承上下文时无法解析 `ElementName`，会产生 XAML 绑定失败。覆盖模板直接给握柄矩形应用主题画刷，以不含绑定的平铺几何作为透明度蒙版；原有激活/未激活配色、拖拽、标题按钮和菜单仍保留。修正在 Theme 字典内，使独立加载主题的浮动窗口也使用同一版本，而不是在 Loaded 后修补或关闭绑定诊断。

覆盖模板来源和许可证在 `ColorVision/Themes/` 中保留。升级 AvalonDock 时，应与新版本的这两份模板核对，避免遗漏上游交互变化。

1. 记录功能名、窗口宽度、当前文档和本次启动时间；确认是入口缺失，还是入口存在但命令失败。
2. 入口缺失按[UI 发现链](../../04-api-reference/ui-components/ui-runtime-handoff.md)核对程序集和扩展；程序集在磁盘上存在不等于已加载。
3. 文件树与工作区切换查[资源路由](../../04-api-reference/ui-components/ColorVision.Solution.md)，文档保存/关闭和布局查[文档生命周期](../../04-api-reference/ui-components/editor-document-lifecycle.md)；不要把设备控制逻辑写入主窗口来解决显示问题。
4. 命令执行后的业务失败转入对应 Engine、插件或项目主题，结合[日志](./log-viewer.md)定位首个失败阶段。

## 验证范围

关联的 `StartupFileOpenPolicyTests` 覆盖启动文件打开策略；`AvalonDockThemeBindingTests` 检查停靠/浮动标题的深浅色、激活状态、主题替换和绘图绑定诊断。这些测试不覆盖所有菜单、状态栏、窗口布局或插件初始化，也不证明真实拖拽和浮动窗口最大化已通过。对宿主交互的修改仍需在获准启动应用的环境中检查目标入口、窄窗口、文档切换和日志；只读文档核对不能记为这些交互已通过。

`MainWindowSearchShellTests` 检查 Popup 装配和定位计算；`ContextualFindRouterTests` 用隔离内容检查局部查找、禁用状态、菜单焦点和跨面板边界。`SearchPopupHotkeyBridgeTests` 检查当前组合、重复按键和捕获门禁；`SearchPopupAirspaceTests` 仅创建不可见、非激活测试宿主与惰性的 STATIC 子 HWND，检查 Popup 原生表面独立于子窗口且没有任务栏样式，不运行 WebView、设备或生产 MainWindow。列出这些用例不表示它们已经执行；真实输入法、多屏 DPI、浏览器与外部点击的最终交互仍需对应运行时验收。

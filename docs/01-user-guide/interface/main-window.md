---
knowledge_id: "operations.main-window"
knowledge_type: "topic"
status: "current"
summary: "主窗口如何挂接菜单、搜索、状态栏和工作区，以及入口缺失时应核对的代码边界。"
aliases: ["主窗口","菜单不见了","搜索框消失","工作区","MainWindow"]
code_paths: ["ColorVision/MainWindow.xaml","ColorVision/MainWindow.xaml.cs","UI/ColorVision.UI/Menus","UI/ColorVision.Solution/Workspace"]
test_paths: ["Test/ColorVision.UI.Tests/StartupFileOpenPolicyTests.cs"]
related: ["ui.discovery","ui.menus","ui.hotkeys","ui.search","ui.status-bar","ui.solution","platform.runtime","operations.index"]
---

# 主窗口与入口装配

主窗口是菜单、搜索、状态栏和停靠工作区的宿主，不是所有业务功能的实现位置。找不到窗口或命令时，先区分宿主显示、扩展发现和功能自身失败，不要求先熟悉整套界面。

## 可见现象与实现入口

| 现象或行为 | 当前实现与检查点 |
| --- | --- |
| 主窗口布局 | `ColorVision/MainWindow.xaml` 定义菜单/搜索区域、停靠区和状态栏；工作区内容由具体编辑器和扩展提供 |
| 搜索框随窗口变窄消失 | `MainWindow.Window_Initialized` 中的大小变化处理在 `ActualWidth < 700` 时折叠 `SearchControl1`；先检查宽度，不要直接判断搜索模块未加载 |
| 搜索框存在但找不到候选或执行不符合预期 | `Ctrl+F` 通过 WPF CommandBinding 聚焦；候选来源、缓存、类型开关和执行检查归[产品搜索](../../04-api-reference/ui-components/search.md)，不是宿主布局问题 |
| 菜单提示了组合键但按键无响应 | `LoadHotKeyFromAssembly()` 独立接入[快捷键注册](../../04-api-reference/ui-components/hotkeys.md)；提示文字不创建注册，先核对具体宿主与模式 |
| 菜单不出现 | `MenuManager.LoadMenuForWindow(MenuItemConstants.MainWindowTarget, Menu1)` 为宿主装配菜单；按[菜单契约](../../04-api-reference/ui-components/menus.md)核对类型缓存、目标窗口、父子可达性和显示过滤，命令检查另行判断 |
| 文档或面板位置不对 | 主窗口给 `WorkspaceManager` 设置布局对象，再挂接 `DockViewManagerHost`；文档分发和布局持久化属于 Solution 工作区 |
| 状态栏缺项或显示旧状态 | 首次渲染后后台优先级调用 `StatusBarManager.Init`，活动文档变化转给 `OnActiveDocumentChanged`；按[状态栏契约](../../04-api-reference/ui-components/status-bar.md)分开查实例缓存、绑定值和文档快照，不把显示状态当成设备完成证明 |
| 窗口已显示但某个模块未就绪 | `LoadIMainWindowInitialized` 按 `Order` 调用扩展初始化并记录启动阶段；主窗口出现不等于所有扩展完成初始化 |

## 故障定位顺序

1. 记录功能名、窗口宽度、当前文档和本次启动时间；确认是入口缺失，还是入口存在但命令失败。
2. 入口缺失按[UI 发现链](../../04-api-reference/ui-components/ui-runtime-handoff.md)核对程序集和扩展；程序集在磁盘上存在不等于已加载。
3. 文件树与工作区切换查[资源路由](../../04-api-reference/ui-components/ColorVision.Solution.md)，文档保存/关闭和布局查[文档生命周期](../../04-api-reference/ui-components/editor-document-lifecycle.md)；不要把设备控制逻辑写入主窗口来解决显示问题。
4. 命令执行后的业务失败转入对应 Engine、插件或项目主题，结合[日志](./log-viewer.md)定位首个失败阶段。

## 验证范围

关联的 `StartupFileOpenPolicyTests` 覆盖启动文件打开策略，不覆盖所有菜单、状态栏、窗口布局或插件初始化。对宿主交互的修改仍需在获准启动应用的环境中检查目标入口、窄窗口、文档切换和日志；只读文档核对不能记为这些交互已通过。

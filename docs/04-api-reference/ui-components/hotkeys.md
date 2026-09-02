---
knowledge_id: "ui.hotkeys"
knowledge_type: "topic"
status: "current"
summary: "快捷键的发现、多组绑定、窗口/全局注册与搜索编辑；同一操作共享作用域，未分配操作保留展示，确认后立即保存，注册或持久化失败按结果补偿。"
aliases: ["快捷键", "热键", "组合键", "全局热键", "窗口热键", "快捷键冲突", "快捷键保存", "快捷键搜索", "热键注销", "HotkeyService", "HotKeyConfig", "HotKeysSetting", "HotkeyEditWindow", "HotkeySettingsViewModel", "HotkeyPresentation", "HotkeyApplyResult", "HotkeyCaptureLease", "IHotkeyProvider", "IHotKey", "HotkeyDefinition", "HotKeys", "WindowHotKeyManager", "GlobalHotKeyManager", "IHotkeyRegistration", "HoyKeyControl"]
code_paths: ["UI/ColorVision.UI/HotKey", "UI/ColorVision.UI/AssemblyHandler.cs", "UI/ColorVision.UI/FileProcessorFactory.cs", "UI/ColorVision.UI/Menus/Base/File", "UI/ColorVision.UI.Desktop/Settings/MenuOptions.cs", "UI/ColorVision.UI/LogImp/Menus/MenuLog.cs", "UI/ColorVision.Solution/OpenSolutionWindow.xaml.cs", "UI/ColorVision.Solution/CommandInitializer.cs", "UI/ColorVision.Solution/SolutionMenuItems.cs", "UI/ColorVision.Solution/Workspace/LayoutMenuItems.cs", "ColorVision/MainWindow.xaml.cs", "ColorVision/MainWindow.Hotkeys.cs", "ColorVision/MainWindowConfig.cs", "ColorVision/Update/MenuCheckAndUpdateV1.cs", "ColorVision/AboutMsg.xaml.cs"]
test_paths: ["Test/ColorVision.UI.Tests/HotkeyServiceTests.cs", "Test/ColorVision.UI.Tests/HotkeySettingsTests.cs", "Test/ColorVision.UI.Tests/HotkeyBackendTests.cs", "Test/ColorVision.UI.Tests/HotkeyMenuBindingTests.cs", "Test/ColorVision.UI.Tests/HotkeyMultipleBindingTests.cs", "Test/ColorVision.UI.Tests/HotkeyMultiBindingServiceTests.cs", "Test/ColorVision.UI.Tests/BuiltInShortcutDefaultsTests.cs", "Test/ColorVision.UI.Tests/BuiltInShortcutUpgradeTests.cs", "Test/ColorVision.UI.Tests/FileHotkeyDefaultsTests.cs", "Test/ColorVision.UI.Tests/RoutedCommandHotkeyGuardTests.cs", "Test/ColorVision.UI.Tests/ApplicationHotkeyIntegrationTests.cs", "Test/ColorVision.UI.Tests/ContextualFindRouterTests.cs", "Test/ColorVision.UI.Tests/SearchWindowHotkeyBridgeTests.cs"]
related: ["ui.framework", "ui.menus", "ui.settings", "ui.configuration", "ui.common"]
---

# 快捷键：发现、注册、编辑与释放

`UI/ColorVision.UI/HotKey/` 负责应用的快捷键定义、配置和注册。`HotkeyService` 协调运行时条目，`WindowHotKey` 处理控件输入事件，`GlobalHotKey` 对接 Win32 热键消息。主窗口通过 `LoadHotKeyFromAssembly()` 接入；实现 `IHotKey` 的菜单按稳定 ID 同步运行时键位提示。`InputGestureText` 本身不注册热键，菜单隐藏覆盖也不会自动禁用快捷键，见[菜单契约](./menus.md)。

快捷键配置值、注册句柄和业务回调成功是三个不同结果。这里没有统一的权限检查、异步完成、取消或业务回滚协议，回调自己的责任不能由“热键已注册”代替。

## 发现、身份与宿主

`LoadFromAssemblies(hostWindow)` 先从 `AssemblyHandler.GetAssemblies()` 的缓存视图发现 `IHotkeyProvider` / `IHotKey`，完整收集定义和配置覆盖，再注销当前服务管理的注册、替换 `_hostWindow`、清空定义，应用覆盖后逐项注册。它不是增量合并：此前显式添加、此次未重新发现的定义不会保留。

每次完整加载都会重新构造 provider；若同时实现两个接口，优先 `IHotkeyProvider`。构造、方法、属性读取或枚举异常会记录并跳过整个故障 provider，包括它在抛错前已经产出的部分定义，其它 provider 继续发现。类型来源整体或配置读取失败发生在释放旧注册之前，旧集合保持不变；真正开始注销和替换后仍不是完整集合事务，不承诺所有新注册必然成功。

| 来源或动作 | ID 与替换规则 |
| --- | --- |
| `IHotkeyProvider.GetHotkeyDefinitions` | 非空 ID 才收录；发现阶段按不区分大小写的 ID 去重，先出现的定义保留，后面的不会覆盖回调 |
| 旧 `IHotKey.HotKeys` | 需要回调；未填 ID 时用 provider 类型 FullName，默认键为空时回退当前键 |
| `AddHotKeys` / `RegisterHotkey` | 未填 ID 时从回调声明类型和方法名生成；已有相同 ID 时先保存原状态并注销，再替换定义/宿主及注册，失败则补偿原状态，不同于发现阶段的 first-wins |
| 已保存配置载入匹配 | 优先按 ID（不区分大小写）；ID 缺失或未命中时，仍可按非空 LegacyName 精确匹配最后一个同名运行时条目；均未命中则跳过 |

同一个方法绑定到不同对象或窗口，不会因此自动获得不同的回调 ID。`HotkeyService` 只有一个主要 `_hostWindow`，不是按窗口分别保存一套完整定义的服务；需要独立条目时应明确稳定 ID 与宿主，不依赖显示名区分。

`AddHotKeys(Control, ...)` 拒绝直接注册 Global，可将条目注册到指定控件。设置应用、恢复和批量注册保留条目已有的 `Control`，仅缺少宿主时回退服务的 `_hostWindow`；全局注册从该控件解析所属 Window。不能把一个主宿主理解为多窗口定义生命周期管理器。

## 展示元数据不改变持久化身份

`HotkeyDefinition` 与 `HotKeys` 可提供 `DisplayName`、`Description`、`Category`、`Source` 四个可选展示字段，均为 `JsonIgnore`。`HotkeyPresentation.Enrich` 在发现或显式注册时补齐缺失字段；`For(HotKeys)` 返回用于界面的 `HotkeyPresentationInfo`，不修改 ID、原始 Name、回调或键位，也不重新发现 provider。编辑副本会保留展示字段，但不带 Control、回调或注册句柄。

- 名称优先使用明确的 DisplayName 和非技术身份的原始 Name，再参考回调/单动作 provider 的 `DisplayNameAttribute` 与已有菜单 Header。显示时可去掉菜单访问键后缀，如 `(_O)`；原始 Name 保留给旧配置匹配。
- 说明和分类优先使用动作明确提供的值，再读取相应 `DescriptionAttribute` / `CategoryAttribute`；标准菜单所属 File/Edit/View/Tool/Help 可提供分类。多动作 `IHotkeyProvider` 的类说明不会套给它的每个动作。
- 特性值可通过贡献程序集已有 `Properties.Resources` 解析；Source 缺省使用实际贡献程序集名称。未知动作使用“自定义操作”“此操作尚未提供详细说明”等明确的空缺提示，不按类型名编造业务用途。

内置操作提供中英文说明，具体描述来自对应 provider；展示层不依赖 Engine，也不调用业务动作。仅有 `HotKeys` 属性而没有实现发现接口的菜单，不会因此新增注册。

## 内置默认键与上下文

默认键按常用桌面操作语义安排，不以旧分配为兼容目标；已有明确保存的个人覆盖仍保留，可通过设置中的“全部重置为默认值”采用新默认，不在升级时改写用户配置。

| 操作 | 默认组合 | 边界 |
| --- | --- | --- |
| 打开文件 | Ctrl+O | `MenuFileOpen` 选择文件并走统一资源打开路由，不再打开工作区列表 |
| 打开文件夹工作区 | Ctrl+Shift+O | 复用文件夹选择与工作区切换的保存/取消流程 |
| 打开工作区列表 | Ctrl+Alt+O | 先显示最近工作区列表，不直接切换；与文件和文件夹入口区分 |
| 保存当前文档 | Ctrl+S | 沿焦点路由 `ApplicationCommands.Save`，先检查 CanExecute；Copilot 输入框承接其草稿操作 |
| 另存为 | Ctrl+Shift+S | 仅执行当前编辑器支持的 SaveAs；图像/3D 是渲染图或截图，不是通用原文件另存 |
| 关闭当前标签页 | Ctrl+W、Ctrl+F4 | 主窗口独立关闭文档命令，保留未保存确认，不调用图像清空 |
| 设置 | Ctrl+, | 打开选项，不占用文本斜体的 Ctrl+I |
| 搜索命令与功能 | Ctrl+Shift+P | 保留可配置快捷键，工具菜单入口已移除；打开或激活单一独立 SearchWindow，不查找文档正文 |
| 查找当前内容或应用功能 | Ctrl+F | 保留可配置快捷键，编辑菜单入口已移除；优先当前编辑器、会话、日志的局部查找；没有局部查找的普通页面打开应用搜索 |
| 日志 | Ctrl+Alt+L | 打开托管日志窗口，L 对应 Log；窗口内 Ctrl+F 仍用于日志查找 |
| 重置窗口布局 | Ctrl+Alt+Shift+R | R 对应 Reset；菜单和快捷键均先询问，默认选择“否”，请先保存文档 |

检查更新、状态栏和关于仍可配置但默认未分配。关于不是帮助文档，因此不拿 F1 代替帮助；布局重置可能移除文件标签，确认只是显式授权，不自动保存或预审脏文档。启动恢复直接调用布局管理器，不弹出这项用户操作确认。没有为设备运行、数据库清理、消息重发等风险操作新增默认键。

新分配的默认键不覆盖旧配置：有自定义组合或明确保存为空的操作仍保留原值；可在该行执行“恢复默认”采用新默认，而无需重置其它快捷键。这里的组合是产品内窗口级默认，不宣称独立编辑器、客户插件或外部程序中均无占用。

剪切/复制/粘贴、撤销、全选、树内 F2、图像 F11 等仍由对应控件处理，未全部迁入全应用配置。正文查找保留局部命令，但应用的场景查找也可以调用它。Copilot 原来占用 Ctrl+O 的复制回答改为面板内 Ctrl+Shift+C，任务面板改为 Ctrl+Alt+T；其余上下文见 [Copilot 交互](../../02-developer-guide/core-concepts/copilot-local-interactions.md)。

`ContextualFindRouter` 限制命令目标位于当前宿主内，优先执行可用的 `ApplicationCommands.Find`，其次执行焦点祖先上明确挂接的 `LocalFindCommand`。已有局部 Find 暂时不可用时仍由当前内容拥有，不改为搜索其他文档；没有公开 WPF Find 的普通文字/密码/native 输入区域也保守保留局部语义。Copilot 的现有会话查找在主窗口装配层适配，不合成按键；日志控制器提供标准 Find 并在 Detach 移除绑定。

`RoutedCommandHotkeyGuard` 只附着主窗口，记录被接管的原生命令键和已发现操作的默认组。清空/改键后，它阻止这些默认键继续落到原生命令或编辑器硬编码处理；若组合被任何当前有效应用内动作复用，则放行给热键后端。不依赖事件订阅先后；不清空 WPF 全局 InputGestures，因此独立编辑器窗口保留原生行为。捕获关闭后尚未释放的键也会被拦截，防止尾部重复执行；录入期间不抢录制控件的输入。

主窗口将 Ctrl+F 声明为 `independentNativeGestures`：清空或改绑“场景查找”后，原编辑器 Ctrl+F 仍按局部语义工作；这不会让普通页面继续用旧键打开应用搜索。若用户明确把 Ctrl+F 分给另一个有效应用动作，则后端优先执行新动作，不再同时执行本地 Find。此例外不绕过捕获尾键保护。独立搜索窗口内的组合与焦点桥接归[主窗口搜索宿主](../../01-user-guide/interface/main-window.md)。

这些选择遵循 [Windows 快捷键指南](https://learn.microsoft.com/en-us/windows/apps/develop/input/keyboard-accelerators)中常用操作优先、菜单可发现和控件范围明确的原则；具体新增组合是产品设计决定，不是全部由系统强制规定。

## 窗口级与全局级是两种机制

| 模式 | 实际注册和触发 | 失败与限制 |
| --- | --- | --- |
| Windows | 按 Control 建立 scope，首次 `PreviewKeyDown` 命中后置 `e.Handled` 并调用回调；重复按下只消费，KeyUp 仅维护录入门禁 | 同一 scope 的相同组合返回 null；底层本身不提供 WPF ICommand 的 CanExecute 门禁，文件动作回调显式检查命令 |
| Global | 取得窗口 HWND/HwndSource，安装消息 hook，调用带 `MOD_NOREPEAT` 的 Win32 `RegisterHotKey`，在 `WM_HOTKEY` 中按注册 ID 调回调 | Key.None、找不到 HwndSource、ID 冲突或 Win32 注册失败都可能失败；系统拒绝注册时返回 Win32 错误，不自动换键或回退 Windows 模式 |

窗口级处理会识别 Alt 的 SystemKey 和 Win 修饰键；忽略纯修饰键及无修饰的 Delete/Back/Escape。设置应用会检查父子控件冲突，但直接使用底层注册并不自动经过该校验。两种底层回调都同步调用委托，没有在此处捕获业务异常或等待异步任务。

作用域冲突同时检查视觉祖先和逻辑祖先。内容控件尚未生成模板视觉树时，已经建立的逻辑父子关系仍参与校验；独立控件可以使用相同组合。

服务将句柄状态复制到 `HotKeys.IsRegistered`，单次 `AddHotKeys` 返回该次注册状态，空键返回 false 不表示定义添加失败。同 ID 显式替换失败时尝试恢复原定义、回调、宿主、键位及原先的注册状态，原错误与恢复错误通过 `LastApplyResult` 区分；它不保存配置。`RegisterAll()` 跳过仍已注册的条目；`ApplySettings` / `ReloadSettings` 通过应用流程修改运行时，并将结果保存在 `LastApplyResult`，同样不写配置文件。发现加载和底层 manager 不能套用设置保存事务的补偿语义。

`IsRegistered=false` 也可能是空键、无回调、无宿主等情形，不唯一表示冲突。设置页显示 `HotkeyApplyResult` 的具体应用/恢复错误，不从这个布尔值猜测原因。旧 `HoyKeyControl` 仍是独立的行编辑控件，不是新设置页的录制弹窗。

## 设置页：多组绑定、搜索与未分配

`HotKeyConfigProvider` 将 `HotKeysSetting` 放入[设置窗口](./settings.md)。页面由 `HotkeySettingsViewModel` 包装 `CreateEditableHotKeys(false)` 的运行时副本，不遵循普通属性行直接编辑活配置的规则：

1. 每行左侧展示动作名称和只读用途说明，右侧按顺序列出全部键位；每组都有独立编辑、删除入口，键位标签也可点击编辑。“添加快捷键”追加组合，不覆盖已有组合。分类、来源和稳定 ID 放在详情提示中，全局和已修改状态另有标记。
2. 没有绑定的已加载操作仍显示在列表中，以可点击的“未分配”文字进入添加，不显示不存在的删除按钮。没有默认键位也不影响操作被发现或搜索；删掉最后一组后保留操作。没有已加载操作和搜索无结果使用不同空态。
3. 页内搜索匹配名称、说明、全部绑定、分类、来源、ID 和状态文字，不区分大小写；忽略组合键中的空白与 `+`，多个词分别匹配。可筛选全部/未分配/已修改，显示结果数量，支持清空搜索和清除筛选。它是文本搜索，不是录制搜索，也不重新发现插件。
4. 编辑按钮打开 `HotkeyEditWindow`。按键候选与全局开关只保存在弹窗草稿；确认后提交该操作的完整绑定列表到 `ApplyAndSaveSettings`，成功即应用并保存。编辑或删除一组不影响其它组合；取消、Esc 或关闭弹窗不提交。新增时必须录入有效组合，不能提交空候选。
5. 单项恢复同时还原该操作的全部默认键位与作用域；全部恢复需二次确认，作用于全部已加载操作，不受当前搜索/筛选范围影响，不修改其它设置。搜索条件保留；恢复后结果随实际状态更新。

每个操作支持多组 `Key + Modifiers`，每组都能触发相同动作，可使用 Ctrl/Alt/Shift/Win 组合。同一操作共享一个 `Kinds`（应用内/全局），编辑弹窗明确说明全局开关影响其全部绑定；不支持逐组不同作用域、连续按键序列或鼠标绑定。页面没有独立帮助弹窗；“注释”是提供者声明的操作说明，不是用户可编辑备注。

`HotKeys` / `HotkeySetting` 的 `Hotkey` 保留第一组，`AdditionalHotkeys` 保存后续组；调用 `GetBindings()` / `SetBindings()` 取得或替换有序完整列表。默认值由 `DefaultHotkey` / `DefaultAdditionalHotkeys` 及相应 Get/Set 方法提供，`HotkeyDefinition.AdditionalDefaultHotkeys` 声明附加默认组。集合为空代表明确未分配；首次没有配置覆盖时使用提供者默认值，已保存的空绑定载入后不会擅自恢复默认。本次不新增历史配置迁移流程。

已经成功应用的修改不能靠关闭设置窗口撤销。外层 `MenuOptions` 关闭后仍执行其普通配置保存流程，文件结果按[配置持久化](./configuration.md)核验。直接调用旧 `HotkeyService.SaveSettings()` 仍只更新配置内存，不能用方法名推断已经落盘。

## 校验、保存和失败补偿

编辑器与服务共同使用 `HotkeyInput.IsValid`：接受 F1–F24、方向键等非字符按键及有效组合；纯修饰键、输入法占位键、无修饰 Enter/Space/Tab/Delete/Back/Escape，以及无修饰或仅 Shift 的普通字符键不能作为新录入值。Tab/Shift+Tab 仍用于焦点导航，Esc 取消，清空通过明确的清除按钮完成。录入框关闭 IME 文本输入；无效候选仍展示并保持禁止保存，切换作用域不会偷偷恢复旧候选。校验异常显示为错误，不向 WPF 事件冒泡。页面对其它已加载动作的重复键位给出动作名称提示，不自动清掉对方绑定。

`ApplyAndSaveSettings` 在动注册句柄之前校验 ID、重复 ID、作用域、每组主键/修饰键、同一操作内的重复组合和运行时组合冲突；附加列表中的空组无效，不静默丢弃或去重。公开的 `ValidateSettings` 可单独执行相同的只读校验，包括录入期间。Global 与同组合冲突；Windows 绑定按相同或祖先/子控件的作用域检测，任意附加组也参与检查。通过后只注销并替换有变更或需要重新注册的操作，未修改且有效的注册保留。组合有效不保证 Win32 注册成功，外部占用仍需在实际注册时检查。

| 结果 | 当前补偿边界 |
| --- | --- |
| 校验失败 | 不注销旧句柄、不写配置 |
| 新组合注册失败 | 释放本次新注册，恢复本次变更操作的完整原绑定列表、作用域与原先已注册的句柄状态；恢复失败单列 `RestoreErrors` |
| 配置未落盘 `NotPersisted` | 恢复本次运行时变更；原文件与配置内存未发布新值 |
| 落盘且发布成功 `PersistedAndPublished` | 返回成功，页面从实际运行时刷新 |
| 已落盘但发布失败 `PersistedButPublishFailed` | 先尝试将旧配置写回；旧文件恢复后才恢复旧运行时。若补偿未落盘，保留新运行时以匹配已写的新文件，尝试发布新内存，并报告恢复错误 |

保存使用配置服务的 `TrySaveAndPublish`，不支持该能力的适配器返回失败，不悄悄降级为仅改内存。`HotkeyApplyResult.Errors` 与 `RestoreErrors` 分开表达原操作失败和补偿失败；页面不把恢复失败显示为保存成功。该流程不回滚快捷键触发过的业务操作。

配置合并保留当前未发现插件的原有条目，只替换本次提交的已加载 ID。旧 JSON 的 `Name` / `IsGlobal` 仍用于兼容读取；正常有稳定 ID 的条目不再输出它们，但没有 ID 且仍有 LegacyName 的未迁移条目继续保存 `Name`，避免丢失下次匹配身份。

每个 `HotkeySetting` 持有独立组合对象，JSON 读取采用替换对象而非填充共享的 `Hotkey.None`；`FromHotKeys` 也复制组合值。定义默认值、运行时当前值与默认值分别复制组合，修改 provider 提供的对象不会改变已建条目。这样多个配置条目在序列化往返后不会互相覆盖，空键标记也不会被读入值污染。

## 录入期间暂停派发

弹窗打开时申请 `BeginCapture()`，重复 Loaded 不重复申请；关闭或保存前释放 `HotkeyCaptureLease`。首个 lease 开启共用派发门禁，并暂时注销服务当前已注册的 Global 热键；Windows 注册句柄保留，但两种后端的回调派发均受门禁控制。嵌套录入只在最后一个 lease 释放时恢复原有 Global 注册，避免某个弹窗提前重新启用快捷键。

保存必须先结束录入；恢复失败会在弹窗与页面显示，当前候选不继续保存。结束录入时记录仍按住的按键，两种后端继续抑制这次按键的尾部事件，直到释放，避免弹窗关闭后的 KeyUp 或重复消息触发旧操作。暂停期拒绝设置应用/新增注册；门禁不是操作系统键盘独占，也不能暂停未经过这两个后端的业务输入处理。Win32 全局键被释放后可能被其它进程占用，恢复仍可能失败，不能把 lease 释放当成所有键位必然恢复的证明。

## 关闭、注销与所有权限制

使用 `WindowHotKeyManager` 且宿主确为 Window 时，manager 在 Closed 释放当前记录的句柄；`GlobalHotKeyManager` 也挂接所属窗口 Closed。Hide 不触发这些注销路径：窗口热键能否收到事件仍取决于输入路由，全局注册则不会仅因隐藏而取消。

普通 Control 的 window-manager 没有 Unloaded 或父窗口关闭清理，静态 Instances 也仍可能保留控件引用。直接调用底层 `WindowHotKey.Register` / `GlobalHotKey.Register` 不自动获得 manager 的 Closed 挂接，调用方必须管理返回句柄。不能把“控件已经移出界面”当成订阅和注册已释放的证明。

manager 的 Closed 路径释放所记录句柄，清空条目的 `Registration` / `IsRegistered`，并解除 Closed 订阅。全局释放检查 `UnregisterHotKey` 的结果：已销毁 HWND 或已不存在的注册可视为已释放，其它系统错误抛回调用方；关闭窗口路径只记录释放异常，不保证操作系统级释放验收。

首次全局注册失败会回收空 scope 和 hook，不注销未成功注册的 ID。manager 只有在有效组合和回调都未变化时才复用现有句柄；组合或回调变化时先释放旧句柄再注册，不直接覆盖遗留句柄。窗口级注册也持有独立键位快照，不受调用方修改原始 Hotkey 对象影响。manager 的替换不等于设置事务，直接调用失败后不会自动执行配置与旧键位补偿，应按需要使用服务的应用接口。

多组注册由一个 `HotkeyRegistrationGroup` 归属该操作，按完整有序列表及回调判断复用。任意一组失败会回收本次已注册组，不以部分成功上报成功；释放时尝试全部子句柄，失败保留所有权供重试。未完成注册或已开始释放的组不再派发业务回调。窗口关闭、设置恢复和捕获暂停都处理完整组，而不是只处理第一项。

## 源码定位与验证

- 定义发现、ID 匹配、宿主与应用补偿：`HotkeyService.cs`、`HotkeyDefinition.cs`、`HotkeySetting.cs`、`HotkeyApplyResult.cs`。
- 动作说明、分类、来源与友好缺省：`HotkeyPresentation.cs`、`HotkeyPresentationResources.resx` / `.en.resx`。
- 列表、搜索、弹窗与录入限制：`HotKeysSetting.xaml` / `.xaml.cs`、`HotkeySettingsViewModel.cs`、`HotkeyEditWindow.xaml` / `.xaml.cs`、`HotkeyInput.cs`。
- 窗口输入、重复组合与句柄释放：`WindowHotKey/WindowHotKey.cs`、`WindowHotKey/WindowHotKeyManager.cs`。
- Win32 注册、hook 和关闭清理：`GlobalHotKey/GlobalHotKey.cs`、`GlobalHotKey/GlobalHotKeyManager.cs`。

上述相对路径均位于 `UI/ColorVision.UI/HotKey/`。`Test/ColorVision.UI.Tests/HotkeyServiceTests.cs` 通过隔离注册与持久化委托验证事前校验、最小句柄替换、注册/保存/补偿失败、未知插件配置保留、控件宿主与嵌套录入门禁；注入类型来源验证故障 provider 隔离和显式同 ID 替换的恢复。

`Test/ColorVision.UI.Tests/HotkeySettingsTests.cs` 覆盖文本搜索、单项清除/恢复、失败后显示实际状态、未显示的编辑弹窗关闭不提交、输入限制，以及展示元数据不改持久化身份；另在注入独立元数据的真实设置框架中检查中英文、深浅主题、不同宽度、搜索结果和空状态。测试不加载生产配置，不执行真实业务回调。

`Test/ColorVision.UI.Tests/HotkeyBackendTests.cs` 在测试进程自己的不可见 HWND 上真实注册临时 `Ctrl+Alt+Shift+F23/F24`，使用无害计数回调验证占用、释放重申、捕获恢复及恢复冲突，结束时释放所有注册。只向自有隐藏 HWND 发送 `WM_HOTKEY`，不注入桌面键盘事件；窗口路由使用独立控件，尾键与真实 DispatcherTimer 检查通过隔离的内部读键委托模拟按住/释放。初始组合已被外部占用时明确失败，不抢占它。

`Test/ColorVision.UI.Tests/HotkeyMenuBindingTests.cs` 验证稳定 ID 匹配、后加载、编辑/清除/恢复、条目替换与弱订阅回收，不注册热键或执行菜单业务。这些测试入口不等于已运行通过，也不替代真实物理键盘、各输入法/布局及真实业务操作的人工验收。

`HotkeyMultipleBindingTests` 覆盖模型值隔离、JSON 往返、多组注册/失败回收与关闭释放，包括自有隐藏窗口上的真实后端测试。`HotkeyMultiBindingServiceTests` 覆盖完整列表的增删改、无配置默认值、无默认操作、明确清空后重新加载、逐组冲突与失败补偿；`HotkeySettingsTests` 补充多组编辑弹窗、删除最后一组、筛选/搜索、重置完整默认列表和未分配行的 UI 状态。

`BuiltInShortcutDefaultsTests` / `FileHotkeyDefaultsTests` 检查默认值、说明、命令边界和菜单联动；`RoutedCommandHotkeyGuardTests` 用自有隐藏宿主验证原生命令不穿透、改键/恢复、窗口隔离及捕获尾键，也覆盖场景 Find 的原生例外和键位复用。`ApplicationHotkeyIntegrationTests` 检查功能搜索、场景查找与 Copilot 命令接线，不启动生产主窗口或设备；`ContextualFindRouterTests` / `SearchWindowHotkeyBridgeTests` 分别检查局部命令与独立搜索窗口键位桥接的隔离边界。

按键可能执行文件、配置或设备操作，运行时验证应使用获授权的隔离宿主和无害回调。文档检索与站点检查不证明真实 provider 完整发现、操作系统注册成功或业务回调完成。

`BuiltInShortcutUpgradeTests` 复制三项新默认的安全声明，使用假注册和内存 JSON 验证无覆盖时采用默认、稳定 ID/旧名称匹配、自定义及明确空绑定保留，以及单项恢复不改变其它操作。测试不读取或改写用户配置，也不调用日志、工作区或布局业务动作。

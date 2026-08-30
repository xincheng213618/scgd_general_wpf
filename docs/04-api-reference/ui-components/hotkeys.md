---
knowledge_id: "ui.hotkeys"
knowledge_type: "topic"
status: "current"
summary: "快捷键的发现、展示、窗口/全局注册与搜索编辑；单个操作只保留一组键位，确认后立即应用并保存，注册或持久化失败按结果执行补偿。"
aliases: ["快捷键", "热键", "组合键", "全局热键", "窗口热键", "快捷键冲突", "快捷键保存", "快捷键搜索", "热键注销", "HotkeyService", "HotKeyConfig", "HotKeysSetting", "HotkeyEditWindow", "HotkeySettingsViewModel", "HotkeyPresentation", "HotkeyApplyResult", "HotkeyCaptureLease", "IHotkeyProvider", "IHotKey", "HotkeyDefinition", "HotKeys", "WindowHotKeyManager", "GlobalHotKeyManager", "IHotkeyRegistration", "HoyKeyControl"]
code_paths: ["UI/ColorVision.UI/HotKey", "UI/ColorVision.UI/AssemblyHandler.cs", "UI/ColorVision.UI.Desktop/Settings/MenuOptions.cs", "UI/ColorVision.UI/LogImp/Menus/MenuLog.cs", "ColorVision/MainWindow.xaml.cs", "ColorVision/MainWindowConfig.cs", "ColorVision/Update/MenuCheckAndUpdateV1.cs"]
test_paths: ["Test/ColorVision.UI.Tests/HotkeyServiceTests.cs", "Test/ColorVision.UI.Tests/HotkeySettingsTests.cs", "Test/ColorVision.UI.Tests/HotkeyBackendTests.cs", "Test/ColorVision.UI.Tests/HotkeyMenuBindingTests.cs"]
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

当前为四个实际 `IHotKey` 动作补充中英文说明：选项 `Ctrl+I`、日志 `Ctrl+L`、检查更新 `Ctrl+U`、显示/隐藏状态栏 `Ctrl+Shift+B`。具体描述来自对应 provider；展示层不依赖 Engine，也不调用这些业务动作。仅有 `HotKeys` 属性而没有实现发现接口的菜单，不会因此新增注册。

## 窗口级与全局级是两种机制

| 模式 | 实际注册和触发 | 失败与限制 |
| --- | --- | --- |
| Windows | 按 Control 建立 scope，订阅 `PreviewKeyUp`；按松键时的 Key/Modifiers 查找，命中置 `e.Handled` 后调用回调 | 同一 scope 的相同组合返回 null；不是 KeyDown，也不是 WPF ICommand 的 CanExecute 门禁 |
| Global | 取得窗口 HWND/HwndSource，安装消息 hook，调用带 `MOD_NOREPEAT` 的 Win32 `RegisterHotKey`，在 `WM_HOTKEY` 中按注册 ID 调回调 | Key.None、找不到 HwndSource、ID 冲突或 Win32 注册失败都可能失败；系统拒绝注册时返回 Win32 错误，不自动换键或回退 Windows 模式 |

窗口级处理会识别 Alt 的 SystemKey 和 Win 修饰键；忽略纯修饰键及无修饰的 Delete/Back/Escape。设置应用会检查父子控件冲突，但直接使用底层注册并不自动经过该校验。两种底层回调都同步调用委托，没有在此处捕获业务异常或等待异步任务。

服务将句柄状态复制到 `HotKeys.IsRegistered`，单次 `AddHotKeys` 返回该次注册状态，空键返回 false 不表示定义添加失败。同 ID 显式替换失败时尝试恢复原定义、回调、宿主、键位及原先的注册状态，原错误与恢复错误通过 `LastApplyResult` 区分；它不保存配置。`RegisterAll()` 跳过仍已注册的条目；`ApplySettings` / `ReloadSettings` 通过应用流程修改运行时，并将结果保存在 `LastApplyResult`，同样不写配置文件。发现加载和底层 manager 不能套用设置保存事务的补偿语义。

`IsRegistered=false` 也可能是空键、无回调、无宿主等情形，不唯一表示冲突。设置页显示 `HotkeyApplyResult` 的具体应用/恢复错误，不从这个布尔值猜测原因。旧 `HoyKeyControl` 仍是独立的行编辑控件，不是新设置页的录制弹窗。

## 设置页：搜索列表与单项编辑

`HotKeyConfigProvider` 将 `HotKeysSetting` 放入[设置窗口](./settings.md)。页面由 `HotkeySettingsViewModel` 包装 `CreateEditableHotKeys(false)` 的运行时副本，不遵循普通属性行直接编辑活配置的规则：

1. 每行展示动作名称、说明、当前键位，以及编辑/清除按钮；偏离默认键位或作用域时显示单项恢复按钮。分类、来源和稳定 ID 放在详情提示中，全局绑定另有标记。
2. 页内搜索匹配名称、说明、键位文字、分类、来源与 ID，忽略空白和 `+`，不区分大小写。它是文本搜索，不是“按下一个键即可筛选”的录制搜索，也不重新发现插件。
3. 编辑按钮打开 `HotkeyEditWindow`。按键候选与全局开关只保存在弹窗草稿；确认后调用 `ApplyAndSaveSettings`，成功即应用并保存，不再有整页“加载/保存”步骤。取消、Esc 或关闭弹窗不提交候选值。
4. 清除将该操作设为 `Hotkey.None`，保留当前作用域；单项恢复同时还原该操作的默认键位与作用域。全部恢复需二次确认，只提交当前已加载动作，不修改其它设置。

当前每个操作只有一组 `Key + Modifiers`，可使用 Ctrl/Alt/Shift/Win 组合，但不支持同一动作的多组备用绑定、连续按键序列或鼠标绑定。页面没有独立帮助弹窗；具体操作说明直接显示在列表和编辑弹窗中。

已经成功应用的修改不能靠关闭设置窗口撤销。外层 `MenuOptions` 关闭后仍执行其普通配置保存流程，文件结果按[配置持久化](./configuration.md)核验。直接调用旧 `HotkeyService.SaveSettings()` 仍只更新配置内存，不能用方法名推断已经落盘。

## 校验、保存和失败补偿

编辑器与服务共同使用 `HotkeyInput.IsValid`：接受 F1–F24、方向键等非字符按键及有效组合；纯修饰键、输入法占位键、无修饰 Enter/Space/Tab/Delete/Back/Escape，以及无修饰或仅 Shift 的普通字符键不能作为新录入值。Tab/Shift+Tab 仍用于焦点导航，Esc 取消，清空通过明确的清除按钮完成。录入框关闭 IME 文本输入；无效候选仍展示并保持禁止保存，切换作用域不会偷偷恢复旧候选。校验异常显示为错误，不向 WPF 事件冒泡。页面对其它已加载动作的重复键位给出动作名称提示，不自动清掉对方绑定。

`ApplyAndSaveSettings` 在动注册句柄之前校验 ID、重复 ID、作用域、主键/修饰键和运行时组合冲突；公开的 `ValidateSettings` 可单独执行相同的只读校验，包括录入期间。Global 与同组合冲突；Windows 绑定按相同或祖先/子控件的作用域检测。通过后只注销并替换有变更或需要重新注册的条目，未修改且有效的注册保留。组合有效不保证 Win32 注册成功，外部占用仍需在实际注册时检查。

| 结果 | 当前补偿边界 |
| --- | --- |
| 校验失败 | 不注销旧句柄、不写配置 |
| 新组合注册失败 | 释放本次新注册，恢复本次变更条目的原键位、作用域与原先已注册的句柄状态；恢复失败单列 `RestoreErrors` |
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

## 源码定位与验证

- 定义发现、ID 匹配、宿主与应用补偿：`HotkeyService.cs`、`HotkeyDefinition.cs`、`HotkeySetting.cs`、`HotkeyApplyResult.cs`。
- 动作说明、分类、来源与友好缺省：`HotkeyPresentation.cs`、`HotkeyPresentationResources.resx` / `.en.resx`。
- 列表、搜索、弹窗与录入限制：`HotKeysSetting.xaml` / `.xaml.cs`、`HotkeySettingsViewModel.cs`、`HotkeyEditWindow.xaml` / `.xaml.cs`、`HotkeyInput.cs`。
- 窗口输入、重复组合与句柄释放：`WindowHotKey/WindowHotKey.cs`、`WindowHotKey/WindowHotKeyManager.cs`。
- Win32 注册、hook 和关闭清理：`GlobalHotKey/GlobalHotKey.cs`、`GlobalHotKey/GlobalHotKeyManager.cs`。

上述相对路径均位于 `UI/ColorVision.UI/HotKey/`。`Test/ColorVision.UI.Tests/HotkeyServiceTests.cs` 通过隔离注册与持久化委托验证事前校验、最小句柄替换、注册/保存/补偿失败、未知插件配置保留、控件宿主与嵌套录入门禁；注入类型来源验证故障 provider 隔离和显式同 ID 替换的恢复。

`Test/ColorVision.UI.Tests/HotkeySettingsTests.cs` 覆盖文本搜索、单项清除/恢复、失败后显示实际状态、未显示的编辑弹窗关闭不提交、输入限制，以及展示元数据不改持久化身份；另在注入独立元数据的真实设置框架中检查中英文、深浅主题、不同宽度、搜索结果和空状态。测试不加载生产配置，不执行真实业务回调。设置 `COLORVISION_HOTKEY_PREVIEW_DIRECTORY` 可将这些隔离页面渲染为本地 PNG，属于显式的本地文件输出。

`Test/ColorVision.UI.Tests/HotkeyBackendTests.cs` 在测试进程自己的不可见 HWND 上真实注册临时 `Ctrl+Alt+Shift+F23/F24`，使用无害计数回调验证占用、释放重申、捕获恢复及恢复冲突，结束时释放所有注册。只向自有隐藏 HWND 发送 `WM_HOTKEY`，不注入桌面键盘事件；窗口路由使用独立控件，尾键与真实 DispatcherTimer 检查通过隔离的内部读键委托模拟按住/释放。初始组合已被外部占用时明确失败，不抢占它。

`Test/ColorVision.UI.Tests/HotkeyMenuBindingTests.cs` 验证稳定 ID 匹配、后加载、编辑/清除/恢复、条目替换与弱订阅回收，不注册热键或执行菜单业务。这些测试入口不等于已运行通过，也不替代真实物理键盘、各输入法/布局及真实业务操作的人工验收。

按键可能执行文件、配置或设备操作，运行时验证应使用获授权的隔离宿主和无害回调。文档检索与站点检查不证明真实 provider 完整发现、操作系统注册成功或业务回调完成。

---
knowledge_id: "ui.hotkeys"
knowledge_type: "topic"
status: "current"
summary: "快捷键的发现、身份、窗口/全局注册和设置草稿；页面保存先重注册并更新配置内存，不直接落盘，注册失败不自动回滚。"
aliases: ["快捷键", "热键", "组合键", "全局热键", "窗口热键", "快捷键冲突", "快捷键保存", "热键注销", "HotkeyService", "HotKeyConfig", "HotKeysSetting", "IHotkeyProvider", "IHotKey", "HotkeyDefinition", "HotKeys", "WindowHotKeyManager", "GlobalHotKeyManager", "IHotkeyRegistration", "HoyKeyControl"]
code_paths: ["UI/ColorVision.UI/HotKey", "UI/ColorVision.UI/AssemblyHandler.cs", "UI/ColorVision.UI.Desktop/Settings/MenuOptions.cs", "ColorVision/MainWindow.xaml.cs"]
test_paths: []
related: ["ui.framework", "ui.menus", "ui.settings", "ui.configuration", "ui.common"]
---

# 快捷键：发现、注册、编辑与释放

`UI/ColorVision.UI/HotKey/` 负责应用的快捷键定义、配置和注册。`HotkeyService` 协调运行时条目，`WindowHotKey` 处理控件输入事件，`GlobalHotKey` 对接 Win32 热键消息。主窗口通过 `LoadHotKeyFromAssembly()` 接入；菜单上的 `InputGestureText` 只是文字，菜单隐藏覆盖也不会自动禁用快捷键，见[菜单契约](./menus.md)。

快捷键配置值、注册句柄和业务回调成功是三个不同结果。这里没有统一的权限检查、异步完成、取消或业务回滚协议，回调自己的责任不能由“热键已注册”代替。

## 发现、身份与宿主

`LoadFromAssemblies(hostWindow)` 先注销当前服务管理的注册，替换 `_hostWindow`，清空定义，再从 `AssemblyHandler.GetAssemblies()` 的缓存视图发现 `IHotkeyProvider` / `IHotKey`，应用 `HotKeyConfig.Instance.Hotkeys` 后逐项注册。它不是增量合并：此前显式添加、此次未重新发现的定义不会保留。

每次完整加载都会重新构造 provider；若同时实现两个接口，优先 `IHotkeyProvider`。provider 构造失败被记录并跳过，但其枚举/属性读取的异常没有同等的逐项隔离，不能承诺某个 provider 失败后其它热键仍完整加载。清空和注销已先发生，也没有恢复旧完整定义集合的事务。

| 来源或动作 | ID 与替换规则 |
| --- | --- |
| `IHotkeyProvider.GetHotkeyDefinitions` | 非空 ID 才收录；发现阶段按不区分大小写的 ID 去重，先出现的定义保留，后面的不会覆盖回调 |
| 旧 `IHotKey.HotKeys` | 需要回调；未填 ID 时用 provider 类型 FullName，默认键为空时回退当前键 |
| `AddHotKeys` / `RegisterHotkey` | 未填 ID 时从回调声明类型和方法名生成；已有相同 ID 时更新既有运行时条目，再注销/重新注册，不同于发现阶段的 first-wins |
| 保存配置匹配 | 优先按 ID（不区分大小写）；匹配不到时可按 LegacyName 精确匹配最后一个同名运行时条目；仍找不到则跳过 |

同一个方法绑定到不同对象或窗口，不会因此自动获得不同的回调 ID。`HotkeyService` 只有一个主要 `_hostWindow`，不是按窗口分别保存一套完整定义的服务；需要独立条目时应明确稳定 ID 与宿主，不依赖显示名区分。

`AddHotKeys(Control, ...)` 拒绝 Global，可将条目先注册到指定控件。但 `ReloadSettings`、`ApplySettings` 和批量 `RegisterAll` 最终使用服务的 `_hostWindow` 逐项注册，会改写条目的 Control；有主宿主时，原控件级条目也可能被重绑到该窗口，没有主宿主时批量 RegisterAll 直接返回。不能承诺一次控件注册的局部范围在所有重载后保持不变。

## 窗口级与全局级是两种机制

| 模式 | 实际注册和触发 | 失败与限制 |
| --- | --- | --- |
| Windows | 按 Control 建立 scope，订阅 `PreviewKeyUp`；按松键时的 Key/Modifiers 查找并直接调用回调 | 同一 scope 的相同组合返回 null；不同控件的路由可同时存在。不是 KeyDown，也不是 WPF ICommand 的 CanExecute 门禁 |
| Global | 取得窗口 HWND/HwndSource，安装消息 hook，调用 Win32 `RegisterHotKey`，在 `WM_HOTKEY` 中按注册 ID 调回调 | Key.None、找不到 HwndSource、ID 冲突或 Win32 注册失败都可能返回 null；外部程序占用也是需要核验的失败原因，不自动换键或回退 Windows 模式 |

窗口级处理会识别 Alt 的 SystemKey 和 Win 修饰键；忽略纯修饰键及无修饰的 Delete/Back/Escape。命中后不设置 `e.Handled`，父子 scope 也没有全局冲突协调，不能把“本 scope 注册成功”视为整个输入路由只执行一次。两种底层回调都同步调用委托，没有在此处捕获业务异常或等待异步任务。

服务将句柄状态复制到 `HotKeys.IsRegistered`，单次 `AddHotKeys` 返回该次注册状态；批量 Apply/Reload/RegisterAll 不返回完整成功清单。Apply/Reload 先注销旧键，再尝试新键，失败不恢复旧组合；直接调用公开的 `RegisterAll()` 则只逐项注册，不先注销或负责替换旧句柄。因此配置已改变和当前可用是不同状态，RegisterAll 也不是安全替换旧注册的等价入口。

`IsRegistered=false` 也可能是空键、无回调、无宿主等情形，不唯一表示冲突。当前 `HoyKeyControl.xaml` 只绑定组合键和 GlobalMode，没有显示 IsRegistered；存在 `BoolToStringConverer` 资源也不意味着界面真的显示冲突诊断。

## 设置页的草稿、加载和保存

`HotKeyConfigProvider` 将 `HotKeysSetting` 放入[设置窗口](./settings.md)。它是自定义 UserControl，没有独立的 Cancel 按钮，也不遵循普通属性行直接编辑活配置的规则：

1. 打开页面时由 `CreateEditableHotKeys(false)` 复制当前运行时键位。副本不带 Control、回调或注册句柄；IsRegistered 只是复制时的状态。
2. 录入组合键、切换 GlobalMode 只修改副本。“恢复默认”使用 `CreateDefaultEditableHotKeys`，同样只重建草稿。服务的 `SetDefault()` 则直接改运行时并重新注册，不能与页面按钮混同。
3. 页面“加载”读取当前内存 `HotKeyConfig` 并覆盖草稿，不调用配置文件重载，也不会重新发现插件。
4. 页面“保存”调用 `ApplySettings` 重新注册，再 `SaveSettings` 重建 `HotKeyConfig.Hotkeys`，最后从运行时重建页面副本；这几个方法没有在此处写配置文件。

因此，若“取消”指未保存就关闭设置窗口，未提交的键位草稿不会应用；已经点过页面保存则不能靠关窗撤销。由“选项”菜单打开时，外层 `MenuOptions` 在窗口关闭后另调用 `ConfigService.SaveConfigs()`，文件结果按[配置持久化](./configuration.md)核验。直接调用 `HotkeyService.SaveSettings()` 只改内存，方法名不代表已落盘。

ID 与兼容名称都无法匹配的旧配置项在应用时被跳过；`SaveSettings` 按当前运行时条目重建整个 Hotkeys 集合，不自动保留当前未发现插件的旧键位。旧 JSON 的 Name/IsGlobal 字段仍可用于兼容读取，新的 `HotkeySetting` 序列化不再输出这两项。

`HoyKeyControl` 在 `PreviewKeyDown` 录入并置 Handled；无修饰 Delete/Back/Escape 清空，单独修饰键、无修饰 Enter/Space/Tab，以及无修饰或仅 Shift 的普通字符键不被设为组合。录入页没有临时注销现有热键：窗口热键走 KeyUp，全局热键走消息 hook，不能承诺“正在录入新键时绝不会触发旧操作”。这是源码边界，未在真实输入环境中复现。

## 关闭、注销与所有权限制

使用 `WindowHotKeyManager` 且宿主确为 Window 时，manager 在 Closed 释放当前记录的句柄；`GlobalHotKeyManager` 也挂接所属窗口 Closed。Hide 不触发这些注销路径：窗口热键能否收到事件仍取决于输入路由，全局注册则不会仅因隐藏而取消。

普通 Control 的 window-manager 没有 Unloaded 或父窗口关闭清理，静态 Instances 也仍可能保留控件引用。直接调用底层 `WindowHotKey.Register` / `GlobalHotKey.Register` 不自动获得 manager 的 Closed 挂接，调用方必须管理返回句柄。不能把“控件已经移出界面”当成订阅和注册已释放的证明。

manager 的 Closed 清理主要 Dispose 句柄，不逐项回写运行时 `HotKeys.IsRegistered`，所以该复制字段可能陈旧。全局释放会调用 `UnregisterHotKey`，但忽略其返回值；部分 manager 的 UnRegister 即使没找到记录也返回成功，不应作为操作系统级注销验收。

另有两处现存资源边界：全局 scope 在注册前安装 hook，首次注册失败不会自动移除这个空 scope；直接反复调用 manager.Register 时，字典可能覆盖同一条目/回调的旧句柄而不先释放。正常修改应核对上层服务或 ModifiedHotkey 的先注销路径，仍不能据此宣称全部注册/失败路径都有完整资源补偿。这些限制没有在本次文档工作中修复。

## 源码定位与验证缺口

- 定义发现、ID 匹配、宿主替换、批量应用：`HotkeyService.cs`、`HotkeyDefinition.cs`、`HotkeySetting.cs`。
- 草稿和录入限制：`HotKeysSetting.xaml.cs`、`HoyKeyControl.xaml.cs` / `.xaml`。
- 窗口输入、重复组合与句柄释放：`WindowHotKey/WindowHotKey.cs`、`WindowHotKeyManager.cs`。
- Win32 注册、hook 和关闭清理：`GlobalHotKey/GlobalHotKey.cs`、`GlobalHotKeyManager.cs`。

上述相对路径均位于 `UI/ColorVision.UI/HotKey/`。当前未找到快捷键发现、编辑、注册冲突、作用域重载、注销或配置兼容的专项测试，故 `test_paths` 为空。文档检索与站点检查不替代真实输入/Win32 验收；按键可能执行文件、配置或设备操作，运行时测试应在明确授权、隔离的宿主和无害回调下进行。

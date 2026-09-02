---
knowledge_id: "ui.themes"
knowledge_type: "topic"
status: "current"
summary: "在外观与语言中切换主题；ThemeManager 的资源应用、系统跟随、窗口外观和公共控件样式，以及即时预览与保存的区别。"
aliases: ["切换深色主题","跟随系统","外观与语言","主题切换为什么不生效","跟随系统但标题栏没变","强制主题重复资源字典","主题预览会自动保存吗","主题系统事件订阅释放","XAML绑定失败","ComboBoxItem","GridViewColumnHeader","ColorVision.Themes","ThemeManager","ThemeManager.Current","Theme","ApplyTheme","ForceApplyTheme","ApplyThemeChanged","CurrentTheme","CurrentUITheme","CurrentThemeChanged","CurrentUIThemeChanged","ApplyCaption","ThemeConfig","ThemePropertiesEditor","AppsUseLightTheme"]
code_paths: ["UI/ColorVision.Themes/README.md","UI/ColorVision.Themes/Theme.cs","UI/ColorVision.Themes/ThemeManager.cs","UI/ColorVision.Themes/ThemeManagerExtensions.cs","UI/ColorVision.Themes/Themes","UI/ColorVision.Themes/ColorVision.Themes.csproj","UI/ColorVision.UI/Themes/ThemeConfig.cs","UI/ColorVision.UI/Themes/ThemePropertiesEditor.cs","UI/ColorVision.UI/ConfigSetting/ConfigSettingManager.cs","UI/ColorVision.UI.Desktop/Settings/MenuOptions.cs","UI/ColorVision.UI.Desktop/Settings/SettingSearchProvider.cs","UI/ColorVision.UI/Extension/IIconExtension.cs","UI/ColorVision.UI/DisPlayManager.cs","ColorVision/App.xaml","ColorVision/App.xaml.cs","ColorVision/StartWindow.xaml.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ThemeSettingsTests.cs","Test/ColorVision.UI.Tests/ThemeSubscriptionLifecycleTests.cs","Test/ColorVision.UI.Tests/StartWindowThemeLifecycleTests.cs","Test/ColorVision.UI.Tests/GridViewColumnHeaderBindingTests.cs","Test/ColorVision.UI.Tests/ComboBoxItemBindingTests.cs"]
related: ["ui.index","ui.settings","ui.property-grid","ui.configuration"]
---

# 主题选择、资源应用与窗口外观

`ColorVision.Themes` 负责 WPF 主题资源与窗口外观；`ColorVision.UI` 中的 `ThemeConfig` / `ThemePropertiesEditor` 负责配置对象和选项编辑。主题选择、资源应用、标题栏更新、配置落盘是不同完成条件，不能用一次 `ApplyTheme` 返回统一代表。

## 切换应用主题

1. 打开 **工具 → 选项**（默认快捷键 **Ctrl+,**）。
2. 在 **外观与语言 → 主题** 中选择 **跟随系统**、**浅色** 或 **深色**。
3. 选择不同卡片后立即预览外观；关闭由上述入口打开的设置窗口时，设置入口会调用配置保存。关闭窗口不会撤销预览。

默认选择“跟随系统”，使用 Windows 的应用配色。三个选择对应 `UseSystem`、`Light`、`Dark`；`UseSystem` 是选择策略，实际资源仍为浅色或深色。即时变色与配置写盘是两个步骤，设置窗口的保存边界见 [设置入口与配置编辑](./settings.md)。

以下说明面向主题接入与排障。独立宿主可以调整公开资源列表，但仍须遵循资源加载和事件约束。

## 选择状态与实际资源

`ThemeManager.Current` 是可替换的全局实例。`NormalizeTheme` 把不属于三种枚举的值归一为 `UseSystem`。

| 状态/入口 | 当前行为 |
| --- | --- |
| `CurrentTheme` | 选择值，初始为 `Light`；字段先改变，再同步发出 `CurrentThemeChanged` |
| `CurrentUITheme` | 资源应用状态，初始为 `Light`；字段改变才发出 `CurrentUIThemeChanged` |
| `Application.ApplyTheme(theme)` | 归一化后，若选择未变立即返回；否则先设置选择/通知，再把 `UseSystem` 解析为缓存的 `AppsTheme`；实际主题未变时不加载字典 |
| `Application.ForceApplyTheme(theme)` | 直接调用 `ApplyThemeChanged`，不改变 `CurrentTheme`，也不检查实际主题是否相同 |
| `ApplyThemeChanged(UseSystem)` | 不读取系统、不加载任何字典，只把 `CurrentUITheme` 设置为 `UseSystem`；不是强制刷新系统配色的等价入口 |

因此不能无条件把 `CurrentUITheme` 当成只有 Light/Dark 的枚举；强制入口可以写入 `UseSystem`。强制浅/深色应用后，原选择仍可能是 `UseSystem`，下次应用主题事件可再次覆盖它。

`ApplyThemeChanged(Light/Dark)` 按列表顺序加载对应 White/Dark 字典，再加载 Base 列表，逐项加入 `app.Resources.MergedDictionaries`。它不移除旧字典、不按 URI 去重；切换实际配色或重复强制应用会继续追加。相同主题的强制应用即使追加了字典，也不会再次触发 `CurrentUIThemeChanged`。资源生效还取决于控件的资源查找/绑定方式，不保证刷新所有已缓存的 Brush 或图像。

管理器初始字段为 Light，不代表已注入资源：空白 WPF 宿主第一次 `ApplyTheme(Light)` 会直接返回；`ApplyTheme(UseSystem)` 解析为 Light 时也可能跳过注入。ColorVision 主程序由 `ColorVision/App.xaml` 预载浅色字典，`App.xaml.cs` 再应用配置选择。独立包宿主应先建立初始资源，例如在 UI 线程上一次性 `ForceApplyTheme(Light)` 后再 `ApplyTheme(UseSystem)`；不要把强制应用放进重复刷新循环。

## 失败与通知不是事务

选择事件发生在资源加载前；资源全部追加后才设置 `CurrentUITheme`。这些调用没有回滚或逐订阅者异常隔离：

- `CurrentThemeChanged` 订阅者抛错时，选择字段已变，但本次资源应用可能尚未开始；再次用相同选择调用 `ApplyTheme` 会被短路。
- 字典加载中途失败会保留此前已追加的字典及已改变的选择，实际主题字段可能仍是旧值。
- `CurrentUIThemeChanged` 订阅者抛错时，字典和字段已经更新，后续订阅者可能未收到通知。

方法没有统一的 UI Dispatcher 调度、成功结果对象或资源状态恢复。调用方应在合适的 WPF 线程处理异常；“选择已改变”“资源已注入”和“全部消费者已刷新”需要分别核对，不能因异常就宣称已回滚。

## UseSystem 读取与订阅边界

构造管理器时，`AppsTheme` / `SystemTheme` 分别读取当前用户注册表 `Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` 下的 `AppsUseLightTheme` / `SystemUsesLightTheme`。值缺失按 Light，存在时直接转为整数并判断是否大于零；这里没有统一捕获读取异常或非整数值错误。

构造后约 10 秒才订阅 `SystemEvents.UserPreferenceChanged` 和 `SystemParameters.StaticPropertyChanged`。任一回调都会重新读两值，没有按事件类别过滤；延迟结束时仅建立订阅，不主动重新采样，所以等待期间错过的变化不保证立即补齐。

`AppsThemeChanged` 在选择为 `UseSystem` 时把事件参数交给实际主题应用；`SystemThemeChanged` 本身不驱动应用资源，例如启动窗口用它更新自己的图标。`AppsTheme` / `SystemTheme` setter 都是先发事件再更新缓存字段，订阅者应使用事件参数；订阅者抛错可能阻止缓存赋值。

系统回调没有显式切换 UI 线程。管理器也没有 `Dispose` 或解除这两个静态事件的路径；反复创建/替换 `ThemeManager.Current` 不能当作安全重置。以上是当前实现边界，不是已证明无遗漏、无线程风险的系统主题同步服务。

## ApplyCaption 与 BaseWindow 是不同生命周期

`window.ApplyCaption(Icon: true)` 在下一次 `Loaded` 执行时取得 HWND，应用初始 `CurrentUITheme`，并建立后续订阅；正常完成后移除这次 Loaded 处理器，在 `Closed` 解除主题处理器。应在窗口首次加载前调用一次；它没有重复调用保护，也不会在已经 Loaded 时立即补执行。反复调用会建立多份处理器。

图标优先读取窗口类型所在程序集目录的 `PackageIcon.png`，用 OnLoad 解码并冻结；读取失败且 `Icon=true` 时回退到主题库的默认图标。找到包图标时，即使 `Icon=false` 仍会赋值；这个参数只控制默认图标分支。默认图标为 `Assets/Image/ColorVision.ico`（非 Dark）或 `ColorVision1.ico`（Dark），不是任意窗口原图标的保留策略。

关键限制：后续订阅是 `CurrentThemeChanged`，不是 `CurrentUIThemeChanged`。切换到 UseSystem 时它直接收到 UseSystem，按非深色处理；随后仅 Windows 应用主题变化，或仅强制应用资源，不会通知此处理器。因此当前不能承诺“跟随系统后标题栏/默认图标始终同步实际配色”。

`SetWindowTitleBarColor` 先把 caption/border 色恢复为 DWM 默认，再分别调用旧/新沉浸式暗色属性：Dark 为 1，其余为 0。DWM 返回码被丢弃，没有返回实际生效状态或兼容性重试保证。包图标读取的 catch 不覆盖整个窗口初始化；资源加载、事件或原生调用异常不具备统一降级事务。

`BaseWindow` 不自动调用 `ApplyCaption`：它提供默认样式、窗口命令和 HWND hook。启用 `IsBlurEnabled` 时在首次 Loaded 初始化背景效果，订阅的是 `CurrentUIThemeChanged`；关闭时解除主题订阅和 HWND hook。默认未启用模糊，属性在 Loaded 后改变也没有自动初始化回调。不要把继承 BaseWindow 当成标题栏跟随的替代保证。

`ApplyCaption` 和 BaseWindow 解除主题订阅时都重新读取 `ThemeManager.Current`，未保存最初的发布者；窗口存活期间替换全局实例可能使旧订阅留下。这与启动窗口显式保存 `_subscribedThemeManager` 的实现不同。

## ThemeConfig、即时预览与落盘

`ThemeConfig` 位于 `UI/ColorVision.UI/Themes/`，虽然命名空间是 `ColorVision.Themes`，它不属于独立 Themes 包。`Instance` 从 `ConfigService` 取得对象；`Theme` 默认 UseSystem，setter 只做归一化、赋值和 PropertyChanged，不自己应用资源或写文件。`TransparentWindow` 也只是配置值，效果由具体窗口消费者决定。

`ThemePropertiesEditor` 通过 [属性编辑器契约](./property-grid.md)提供三个预览卡。卡片是固定配色的示意图，UseSystem 为浅/深两半，不是当前窗口或系统状态的截图。选择不同卡片时先写入传入对象属性，再调用 `Application.Current?.ApplyTheme`；应用不存在时仍可能已改对象，资源应用失败也不撤销属性值。

它没有候选副本、保存/取消事务，也不调用配置保存接口；预览即运行期外观修改，不保证关闭编辑页面会恢复。外部属性变化会同步选中状态，但属性 setter 本身不是主题应用入口。编辑器在 Unloaded 解除对象通知，未在再次 Loaded 时重新订阅。设置发现缓存还可能持有旧配置对象；配置重载后需按 [配置持久化与对象所有权](./configuration.md)重新取得/重建绑定。

保存时机、退出自动保存、保存失败和重载对象替换均归配置服务，不由 Themes 库承诺。“预览已变色”不能证明设置已写盘，文件重载也不自动等于所有主题消费者已重新应用。

## 更新与管理窗口的共享外观

`Themes/UpdateDialogTheme.xaml` 提供 `UpdateDialog.*` 动态画刷以及主按钮、次按钮、文字按钮和卡片样式。配色来自 Dark / White 字典，次要文字使用 0.72 不透明度区分信息层级。更新、恢复、服务主机、应用与工具以及 RBAC 用户窗口共享这套资源；主程序的 `Update/UpdateDialogTheme.xaml` 是合并该字典的兼容入口。共享字典只定义外观，不引入更新或权限业务依赖。

用户中心、用户管理和服务主机管理窗口通过 `ApplyCaption` 接入原生标题栏外观，具体系统跟随与订阅限制仍适用前述窗口外观契约。

## 公共控件样式

`Themes/Base.xaml` 在现有主题模板上设置以下默认值：

| 控件 | 对齐或尺寸规则 |
| --- | --- |
| `ComboBoxItem` | 默认水平 Left、垂直 Center；仅在 `IsVisible=true` 时绑定祖先 `ItemsControl` 的对齐属性，可见项实时跟随父控件，隐藏或脱离树时回到默认值 |
| 隐式 `GridViewColumnHeader` | 继承 HandyControl 列头样式，设置 `MinHeight=0`；实际高度由内容和 Padding 决定，保留字号继承、模板和调整列宽的 `PART_HeaderGripper` |

下拉项规则用于标准/HandyControl 默认 ComboBox、`ComboBox.Small`、`ComboBoxExtend.Small`、`ComboBoxPlus.Small` 和项目 `ComboBoxBaseStyle`，保留各自模板及紧凑尺寸。自定义 `ItemContainerStyle` 的消费者负责自己的绑定；需要固定列头高度时，明确设置列头样式的 `Height` / `MinHeight`。

排查 XAML 绑定失败时，按目标控件、目标属性和绑定来源定位共享样式。未挂载、隐藏或回收的容器可能没有可用祖先；Visual Studio 会按控件实例累计错误次数，同一种样式问题可能产生多条记录。

`ComboBoxItemBindingTests` 使用真实主题检查未挂载项、弹出层对齐的动态继承和关闭/刷新后的解绑。`GridViewColumnHeaderBindingTests` 检查未挂载列头，以及默认/`GridViewColumnHeaderBase` 列头的绑定诊断、字号继承与调整列宽模板；用户窗口中的鼠标拖动、排序和主题切换仍需单独验证。

## 包入口与验证范围

`UI/ColorVision.Themes/ColorVision.Themes.csproj` 当前面向 `net8.0-windows7.0;net10.0-windows7.0`，引用 HandyControl，启用 NuGet/符号包生成并打包 README。目标框架后缀不是每个 DWM 属性在该 Windows 版本可用的保证。包使用/本地构建入口保留在源码旁 README；发布规则见 [NuGet 包发布](./publishing.md)。

| 已有测试 | 实际断言范围 |
| --- | --- |
| `ThemeSettingsTests` | 仅支持 UseSystem/Light/Dark 的列表；历史枚举值 3、4 被 ThemeConfig 归一为 UseSystem |
| `ThemeSubscriptionLifecycleTests` | `IIconExtension.SetIconResource`、`DisPlayManagerExtension.ApplyChangedSelectedColor` 的弱引用订阅不阻止目标 GC；不是 ApplyCaption 或全体窗口生命周期测试 |
| `StartWindowThemeLifecycleTests` | 启动窗口关闭恢复 SystemTheme 订阅数，以及先解除启动日志 appender 后关闭的窗口可被 GC；不是全局系统事件解绑测试 |

测试引用不代表本次执行。当前这些测试不能证明资源追加/失败恢复、选择/实际事件顺序、UseSystem 延迟与线程、预览持久化、ApplyCaption 重复/已加载调用和 DWM 真机表现；修改这些契约需补相应针对性验证，不应把“需要验收”改写成“已经支持并验证”。

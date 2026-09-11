---
knowledge_id: "ui.themes"
knowledge_type: "topic"
status: "current"
summary: "应用主题即时预览，启动页独立选择深色、浅色或跟随软件且下次启动生效；ThemeManager 的资源、系统跟随、窗口外观和保存边界。"
aliases: ["切换深色主题","跟随系统","外观与语言","启动页主题","启动页默认深色","跟随软件主题","StartupTheme","ThemeConfig.StartupTheme","FollowApplication","主题切换为什么不生效","跟随系统但标题栏没变","强制主题重复资源字典","主题预览会自动保存吗","主题系统事件订阅释放","XAML绑定失败","ComboBoxItem","GridViewColumnHeader","圆角菜单","右键菜单","MenuPopupCornerRadius","MenuItemSecondaryForeground","ColorVision.Themes","ThemeManager","ThemeManager.Current","Theme","ApplyTheme","ForceApplyTheme","ApplyThemeChanged","CurrentTheme","CurrentUITheme","CurrentThemeChanged","CurrentUIThemeChanged","ApplyCaption","TryLoadPackageIcon","PackageIcon.png","ThemeConfig","ThemePropertiesEditor","AppsUseLightTheme"]
code_paths: ["UI/ColorVision.Themes/README.md","UI/ColorVision.Themes/Theme.cs","UI/ColorVision.Themes/ThemeManager.cs","UI/ColorVision.Themes/ThemeManagerExtensions.cs","UI/ColorVision.Themes/Behaviors","UI/ColorVision.Themes/Windowing","UI/ColorVision.Themes/ThemeResourceDictionary.cs","UI/ColorVision.Themes/HandyControlStyleResources.cs","UI/ColorVision.Themes/Themes","UI/ColorVision.Themes/ColorVision.Themes.csproj","UI/ColorVision.UI/Themes/ThemeConfig.cs","UI/ColorVision.UI/Themes/StartupTheme.cs","UI/ColorVision.UI/Themes/ThemePropertiesEditor.cs","UI/ColorVision.UI/Properties/Resources.resx","UI/ColorVision.UI/Properties/Resources.en.resx","UI/ColorVision.UI/Properties/Resources.zh-Hant.resx","UI/ColorVision.UI/ConfigSetting/ConfigSettingManager.cs","UI/ColorVision.UI.Desktop/Settings/MenuOptions.cs","UI/ColorVision.UI.Desktop/Settings/SettingSearchProvider.cs","UI/ColorVision.UI/Extension/IIconExtension.cs","UI/ColorVision.UI/DisPlayManager.cs","ColorVision/App.xaml","ColorVision/App.xaml.cs","ColorVision/StartWindow.xaml.cs","ColorVision/StartWindow.Presentation.cs","ColorVision/CompactMainWindow.cs"]
test_paths: ["Test/ColorVision.UI.Tests/StartupThemeBootstrapTests.cs","Test/ColorVision.Themes.Tests/ThemeResourceTests.cs","Test/ColorVision.UI.Tests/HelpKeyboardNavigationTests.cs","Test/ColorVision.UI.Tests/ThemeSettingsTests.cs","Test/ColorVision.UI.Tests/StartupThemeSettingsTests.cs","Test/ColorVision.UI.Tests/ThemeSubscriptionLifecycleTests.cs","Test/ColorVision.UI.Tests/StartWindowThemeLifecycleTests.cs","Test/ColorVision.UI.Tests/StartupPresentationTests.cs","Test/ColorVision.UI.Tests/GridViewColumnHeaderBindingTests.cs","Test/ColorVision.UI.Tests/ComboBoxItemBindingTests.cs","Test/ColorVision.UI.Tests/MenuThemeTests.cs"]
related: ["ui.index","ui.settings","ui.property-grid","ui.configuration","platform.runtime","operations.main-window"]
---

# 主题选择、资源应用与窗口外观

`ColorVision.Themes` 负责 WPF 主题资源与窗口外观；`ColorVision.UI` 中的 `ThemeConfig` / `ThemePropertiesEditor` 负责配置对象和选项编辑。主题选择、资源应用、标题栏更新、配置落盘是不同完成条件，不能用一次 `ApplyTheme` 返回统一代表。

文本编辑器的正文、代码地图、悬停预览、缩进参考线与搜索标记共用 `Themes/Integrations/Editor.Light.xaml` / `Editor.Dark.xaml` 中的语义颜色。字体和文字字重由编辑器设置控制，交互与订阅生命周期见[文本编辑器](./text-editor.md)。

## 切换应用主题

1. 打开 **工具 → 选项**（默认快捷键 **Ctrl+,**）。
2. 在 **外观与语言 → 主题** 中选择 **跟随系统**、**浅色** 或 **深色**。
3. 选择不同卡片后立即预览外观；关闭由上述入口打开的设置窗口时，设置入口会调用配置保存。关闭窗口不会撤销预览。

默认选择“跟随系统”，使用 Windows 的应用配色。三个选择对应 `UseSystem`、`Light`、`Dark`；`UseSystem` 是选择策略，实际资源仍为浅色或深色。即时变色与配置写盘是两个步骤，设置窗口的保存边界见 [设置入口与配置编辑](./settings.md)。

主题选项使用紧凑预览卡片，示意图等比缩放，文字标签保留正常字号；键盘焦点与当前选择都有边框提示。卡片的尺寸与绘制由 `Themes/Components/ThemePreview.xaml` 统一维护，不改变主题值或即时预览的提交规则。

以下说明面向主题接入与排障。独立宿主可以调整公开资源列表，但仍须遵循资源加载和事件约束。

## 资源入口与职责

主程序的 `App.xaml` 保留空资源字典，在 `Application_Startup` 读取配置后调用 `Application.ApplyTheme`，直接加载用户选择的主题；不先加载并丢弃一套默认浅色资源。主题应用仍位于恢复、单实例提示、独立文件、向导和启动窗口之前，配置加载及早期原生错误提示不依赖 WPF 主题。

`StartupThemeBootstrapTests` 在空资源下检查缺失或损坏配置的关键默认对象、待应用主题重置及备份，再应用主题并构造启动、单实例与恢复窗口；不执行这些窗口的真实启动和恢复动作。

独立宿主仍可在 `App.xaml` 合并 `/ColorVision.Themes;component/Themes/Theme.xaml` 获得默认浅色资源；该公共入口的行为不变。空白宿主也可以直接调用 `ApplyTheme`，包括首次选择 Light，无须先强制注入。

| 位置 | 职责 |
| --- | --- |
| `ThemeResourceDictionary`、`Themes/Theme.xaml` | 组装一个完整主题资源组；入口保持浅色默认值 |
| `Themes/Palettes/Light.xaml`、`Dark.xaml` | 相同资源键、相同类型的浅深配色；包含保留的旧颜色名 |
| `Themes/Foundations` | 语义画刷、基础尺寸和转换器资源 |
| `Themes/Controls` | 按控件组织的公共样式、模板 |
| `Themes/Components` | 对话框、面板、状态指示、BaseWindow 等组合样式 |
| `Themes/Icons` | 绘图画刷与图像资源；状态按钮模板属于 Components |
| `Themes/Integrations` | HandyControl 基础样式适配、独立的编辑器及终端色板 |
| `Themes/Compatibility` | 旧对话框资源名与未进入默认主题的历史 ListView 资源 |
| `Behaviors`、`Windowing` | 键盘行为，以及窗口、DWM 和模糊效果代码；公开命名空间保持兼容 |

旧 `White.xaml`、`Dark.xaml`、`Base.xaml`、`Menu.xaml`、`GroupBox.xaml`、`Icons.xaml`、`ToggleSwitch.xaml`、`UpdateDialogTheme.xaml`、`Window/BaseWindow.xaml` 保留为合并入口。`Listview.xaml` 仍可显式加载，但不加入默认主题；仓库无调用不能证明外部包没有使用它，因此保留实现和旧 URI。

通用资源采用用途命名：`CV.Color.*` 是 Color，`CV.Text.*`、`CV.Surface.*`、`CV.Border.*`、`CV.Accent.*`、`CV.Action.Foreground` 是 Brush。业务控件通过 `DynamicResource` 使用语义画刷。浅深主题遵循相同信息层级；编辑器语法色和终端色保留独立用途，不机械反转颜色。

旧 Color 资源继续保留明确的 Color 类型和默认值，与对应语义颜色的相等关系由测试检查；不使用可能改变 BAML 值类型的 `StaticResource` 元素别名。`PrimaryBrush` 依赖通用强调色，不再依赖更新窗口命名的颜色。旧资源名仍可解析，但新功能应使用语义资源；对旧名的局部覆盖不会自动成为所有新语义资源的覆盖。

控件字典显式声明需要的基础样式。`HandyControlStyleResources` 仅导出 `CV.HandyControl.*` 基础样式，不把整套供应商隐式样式再次暴露到控件覆盖层，避免后加载的列头资源覆盖前面的 Button/TextBox。资源组仍按供应商资源、应用色板、基础控件顺序组装，整组替换保留现有静态模板解析方式。

## 选择状态、资源应用与失败边界

`ThemeManager.Current` 是可替换的全局实例。`NormalizeTheme` 把不属于三种枚举的值归一为 `UseSystem`。公开的 `ResourceDictionaryBase` / `White` / `Dark` 列表继续允许独立宿主配置，修改列表后可通过强制应用重新加载。

| 状态/入口 | 行为 |
| --- | --- |
| `CurrentTheme` | 用户选择的策略，初始 Light；普通应用成功后更新 |
| `CurrentUITheme` | 实际 Light/Dark 配色，初始 Light；字段初值不代表已有资源 |
| `ApplyTheme(theme)` | 解析 UseSystem；缺少本管理器资源组时初始化；同一已应用配色可复用资源，仅在选择变化时通知选择事件 |
| `ForceApplyTheme(theme)` / `ApplyThemeChanged` | 重新准备和替换资源组；UseSystem 解析为缓存的应用配色，不改变用户选择策略 |

所有应用操作切回目标 Application 的 Dispatcher。准备新资源组成功后，在原主题位置替换；已知的旧平铺初始化字典会被接管。没有旧主题时插在资源合并列表开头，让已有宿主覆盖保留优先级。只识别库的资源组、统一入口及已知旧 URI，不清空应用资源，也不移除无关插件字典。合并字典中后面的同名键优先，Application 自有键仍优先于合并字典。

切换或重复强制应用不会不断追加主题组。实现整体重载色板和模板；不承诺只重新创建画刷。静态持有的图像、冻结资源或第三方缓存不保证随切换刷新：需要变色的界面应动态引用资源，或在实际主题事件中重新取值；现有 `IIconExtension` 等集成保持自己的刷新职责。

源字典加载失败时，旧资源组、选择字段和实际主题字段保持不变，可以修复资源后重试。WPF 延迟创建的资源和模板仍可能在首次使用时失败，加载入口成功不能代替控件实例化验证。资源替换及两个状态字段写入完成后，先通知选择变化，再通知实际主题变化；同配色强制刷新不重复通知实际主题。订阅者异常不回滚资源与字段，也没有逐订阅者异常隔离，调用方应区分资源加载失败与通知失败。

## 系统跟随与管理器生命周期

实例初始化时读取当前用户注册表 `Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` 下的 `AppsUseLightTheme` / `SystemUsesLightTheme`。缺失或非整数按 Light；整数大于零为 Light。注册表键通过 using 释放，访问异常由调用方处理。

第一次成功应用后延迟约 10 秒建立 `SystemEvents.UserPreferenceChanged`、`SystemParameters.StaticPropertyChanged` 订阅，并重新采样一次，补齐延迟期间的变化。回调通过应用 Dispatcher 刷新缓存；AppsTheme 变化且选择为 UseSystem 时应用实际配色。SystemTheme 用于任务栏相关外观和启动图标，不独立驱动应用色板。

`AppsTheme` / `SystemTheme` 先更新缓存再通知。ThemeManager 实现 `IDisposable`，释放时取消待建立的监听、解绑已建立的系统事件及 Application.Exit；退出也触发释放。替换 `ThemeManager.Current` 不自动处置调用方持有的旧实例，所有者负责 Dispose；窗口必须从最初订阅的发布者解绑。

## 窗口外观与生命周期

`window.ApplyCaption(Icon: true)` 按窗口幂等接入：尚未加载时等待 Loaded，已加载时立即取得 HWND；后续订阅 `CurrentUIThemeChanged`，并切回窗口 Dispatcher。关闭时向保存的发布者解绑，包括全局管理器被替换的情况。它只处理原生标题栏与图标，不接管 BaseWindow 的 WPF 标题按钮或紧凑主窗口布局。

`ThemeManagerExtensions.TryLoadPackageIcon(Window)` 从窗口类型所在程序集目录读取 `PackageIcon.png`，用 OnLoad 解码并冻结后返回。路径、文件或解码不可用时返回 null；方法自身不赋值图标、不订阅主题、不调用 DWM。ApplyCaption 找到包图标时优先采用它，包括 Icon=false；该参数仅禁用默认图标回退。默认图标是 `Assets/Image/ColorVision.ico` / `ColorVision1.ico`。

`SetWindowTitleBarColor` 恢复 DWM 默认 caption/border 色，再尝试旧/新沉浸式暗色属性；返回码仍不作为生效保证。Windows 版本与原生属性支持范围需要实际窗口验证。

BaseWindow 拥有自己的 WindowChrome、窗口命令及 WPF 标题按钮。默认样式缺失时从兼容入口局部加载，不在类型初始化期间追加 Application 字典。启用 `IsBlurEnabled` 后首次 Loaded 初始化背景效果，订阅实际主题，并在关闭时向同一个管理器解绑、移除 HWND hook。Loaded 后改变该属性仍不自动初始化模糊。

普通主窗口使用 ApplyCaption。紧凑主窗口由 `CompactTitleBarChrome` 单独拥有原生按钮显隐、透明背景保护及主题订阅；Windows build 22000 门禁、回退路径、包图标优先级及独立的 BaseWindow 对话框边界见[主窗口与紧凑标题栏](../../01-user-guide/interface/main-window.md)。关于窗口继续保持固定尺寸、仅关闭按钮的对话框形态。

## ThemeConfig、即时预览与落盘

`ThemeConfig` 位于 `UI/ColorVision.UI/Themes/`，虽然命名空间是 `ColorVision.Themes`，它不属于独立 Themes 包。`Instance` 从 `ConfigService` 取得对象；`Theme` 默认 UseSystem，setter 只做归一化、赋值和 PropertyChanged，不自己应用资源或写文件。`TransparentWindow` 也只是配置值，效果由具体窗口消费者决定。

`ThemePropertiesEditor` 通过 [属性编辑器契约](./property-grid.md)提供三个预览卡。卡片是固定配色的示意图，UseSystem 为浅/深两半，不是当前窗口或系统状态的截图。选择不同卡片时先写入传入对象属性，再调用 `Application.Current?.ApplyTheme`；应用不存在时仍可能已改对象，资源应用失败也不撤销属性值。

它没有候选副本、保存/取消事务，也不调用配置保存接口；预览即运行期外观修改，不保证关闭编辑页面会恢复。外部属性变化会同步选中状态，但属性 setter 本身不是主题应用入口。编辑器在 Unloaded 解除对象通知，未在再次 Loaded 时重新订阅。设置发现缓存还可能持有旧配置对象；配置重载后需按 [配置持久化与对象所有权](./configuration.md)重新取得/重建绑定。

保存时机、退出自动保存、保存失败和重载对象替换均归配置服务，不由 Themes 库承诺。“预览已变色”不能证明设置已写盘，文件重载也不自动等于所有主题消费者已重新应用。

### 启动页主题

**外观与语言 → 启动页主题** 使用普通下拉框，位于应用“主题”和“语言”之间。对应配置为 `ThemeConfig.StartupTheme`，枚举定义在 `UI/ColorVision.UI/Themes/StartupTheme.cs`，与应用的 `Theme` 独立。

| 选项 | 配置值 | 启动页配色 |
| --- | --- | --- |
| 深色（默认） | `Dark = 0` | 固定深色 |
| 浅色 | `Light = 1` | 固定浅色 |
| 跟随软件主题 | `FollowApplication = 2` | 使用 `ThemeManager.CurrentUITheme` 已解析的实际应用配色 |

新建配置、缺少此字段的旧配置和枚举范围外的值都使用 `Dark`。跟随软件读取实际 Light/Dark；应用选择 `UseSystem` 时由应用主题管理器解析系统应用配色，启动页不直接根据 Windows 设置另作选择。高对比度仍优先使用系统可访问性配色。

设置项采用 `SectionAppearance`、`Order = -35`，排在应用主题的 `-40` 与语言的 `-30` 之间；`EnumPropertiesEditor` 提供标准 ComboBox，标题、说明和三个选项使用公共 UI 的简英繁资源。setter 只归一化、赋值和通知属性变化，保存沿用选项窗口的正常边界，不调用应用主题预览。

修改在**下次启动**生效。`StartWindow.Presentation.cs` 在窗口构造时捕获一次策略；固定深浅忽略随后应用主题变化，只有捕获的 `FollowApplication` 响应 `CurrentUIThemeChanged`。已打开的启动页不会因配置对象再次改值而改变策略。启动页本身没有主题按钮，也不写入配置；窗口呈现、语言、动画和关闭时退订的完整边界见[启动界面与动画](../../03-architecture/overview/runtime.md#启动界面与动画边界)。

## 对话框外观与键盘行为

`Themes/Components/Dialog.xaml` 提供通用的 `CV.Button.Primary`、`CV.Button.Secondary`、`CV.Button.Text`、`CV.Tag.Border` 和 `CV.Card`。文字操作使用 `CV.Action.Foreground`，辅助说明使用不透明度 0.72 的 `CV.Text.Secondary`。悬停、按下、禁用等反馈仍由共享模板负责。

检查更新窗口直接接入这套资源，“变更日志”“程序备份”“重新安装”保持主要操作文字层级。恢复、服务主机、应用与工具、RBAC 等现有消费者仍可通过 `Themes/UpdateDialogTheme.xaml` 使用旧 `UpdateDialog.*` 资源；旧文字按钮保留次要文字默认值。主程序的 `Update/UpdateDialogTheme.xaml` 继续是兼容入口。共享资源不引入更新、服务或权限业务依赖。

`WindowKeyboardNavigation.Attach` 在首次呈现时设置指定焦点，Tab 在窗口内循环；未被子控件处理的无修饰 Esc 关闭窗口或调用自定义返回动作。搜索、下拉和上下文菜单可优先处理 Esc；窗口是否忙碌由调用方决定。

`InputKeyboardNavigation.EnterMovesFocus` 与 `NumberKeysOnly` 是从视觉字典分离的输入行为。现有 TextBox 和数字滑块模板保留原接入及键盘规则；数字规则是按键过滤，仍允许剪贴板快捷键，不替代粘贴内容、输入法或数值范围验证。需要独立交互的编辑器应显式配置行为。`BaseEvent` 及其公开 `NumberValidationTextBox` 方法保留兼容。

## 公共控件样式

`Themes/Base.xaml` 聚合 `Controls/TextBox.xaml`、`ComboBox.xaml`、`ListView.xaml` 等资源，在现有主题模板上设置以下默认值：

| 控件 | 对齐或尺寸规则 |
| --- | --- |
| `ComboBoxItem` | 默认水平 Left、垂直 Center；仅在 `IsVisible=true` 时绑定祖先 `ItemsControl` 的对齐属性，可见项实时跟随父控件，隐藏或脱离树时回到默认值 |
| 隐式 `GridViewColumnHeader` | 继承 HandyControl 列头样式，设置 `MinHeight=0`；实际高度由内容和 Padding 决定，保留字号继承、模板和调整列宽的 `PART_HeaderGripper` |

下拉项规则用于标准/HandyControl 默认 ComboBox、`ComboBox.Small`、`ComboBoxExtend.Small`、`ComboBoxPlus.Small` 和项目 `ComboBoxBaseStyle`，保留各自模板及紧凑尺寸。自定义 `ItemContainerStyle` 的消费者负责自己的绑定；需要固定列头高度时，明确设置列头样式的 `Height` / `MinHeight`。

排查 XAML 绑定失败时，按目标控件、目标属性和绑定来源定位共享样式。未挂载、隐藏或回收的容器可能没有可用祖先；Visual Studio 会按控件实例累计错误次数，同一种样式问题可能产生多条记录。

`ComboBoxItemBindingTests` 使用真实主题检查弹出层对齐的动态继承和关闭/刷新后的解绑。`GridViewColumnHeaderBindingTests` 检查未挂载列头，以及默认/`GridViewColumnHeaderBase` 列头的绑定诊断、字号继承与调整列宽模板；用户窗口中的鼠标拖动、排序和主题切换仍需单独验证。

## 菜单的共享外观

`Themes/Menu.xaml` 转发到 `Controls/Menu.xaml`，统一顶层下拉、级联子菜单和右键菜单的浮层外观：8 DIP 圆角、1 DIP 淡描边和 4 DIP 内边距；菜单项最小高度为 26 DIP，上下内边距为 2 DIP，悬停高亮使用 5 DIP 圆角。分隔线上下各留白 3 DIP。顶层菜单标题保留紧凑尺寸，不套用下拉项最小高度。

同级菜单项按实际内容共享列宽：存在图标或勾选项时才保留图标列，普通 16 DIP 图标加右侧 6 DIP 留白、18 DIP 勾选框加右侧 4 DIP 留白均占 22 DIP；未勾选的可勾选项仍占位。存在快捷键时才保留标题与快捷键之间的 12 DIP 间距；存在子菜单时才保留箭头列，由 6 DIP 箭头及左侧 6 DIP 留白撑开。纯文字菜单自动收起这些空列，同级混合项目仍保持标题、快捷键和箭头对齐；自定义宽图标按实际测量扩展图标列。

| 共享资源 | 用途与默认值 |
| --- | --- |
| `MenuPopupCornerRadius` | 浮层圆角，默认 8 |
| `MenuItemCornerRadius` | 菜单项高亮圆角，默认 5 |
| `MenuPopupPadding` | 浮层内部留白，默认 4 |
| `MenuItemMinHeight` | 下拉与右键菜单项最小高度，默认 26 |
| `MenuPopupShadowMargin`、`MenuPopupShadowEffect` | 阴影预留空间与效果；阴影由独立背景层绘制，不对文字与菜单内容整体施加效果 |
| `MenuItemSecondaryForeground` | 快捷键等次要文字画刷，由浅色、深色字典分别提供 |

面板背景、边框、悬停、分隔线与次要文字通过 `DynamicResource` 读取配色。浅色和深色使用各自的低对比分隔线；面板内部没有单独着色的图标侧栏。修改共享外观时应调整这些资源或公共模板；定义专用 `MenuItem` / `ContextMenu` 模板的消费者仍由自己的模板控制外观。

实现保留 WPF 原生 `MenuItem`、`PART_Popup`、命令绑定、访问键、勾选与禁用状态；浮层保留透明背景以及承载长菜单的 `ScrollViewer`。圆角与阴影属于视觉模板，不能替代键盘导航、子菜单鼠标穿越或滚动交互的验证。验收应覆盖浅色/深色、顶层/多级/右键菜单、长菜单滚动与高 DPI 边缘；模板或构建检查不能证明真实桌面上的这些交互已验收。

## 包入口与验证范围

`UI/ColorVision.Themes/ColorVision.Themes.csproj` 当前面向 `net8.0-windows7.0;net10.0-windows7.0`，引用 HandyControl，启用 NuGet/符号包生成并打包 README。目标框架后缀不是每个 DWM 属性在该 Windows 版本可用的保证。包使用/本地构建入口保留在源码旁 README；发布规则见 [NuGet 包发布](./publishing.md)。

| 已有测试 | 实际断言范围 |
| --- | --- |
| `ThemeResourceTests` | 独立 WPF 宿主中的首次初始化、主题组替换、宿主覆盖、加载失败重试、浅深键/类型兼容、实际模板、动态图标和窗口订阅 |
| `ThemeSettingsTests` | 仅支持 UseSystem/Light/Dark 的列表；历史枚举值 3、4 被 ThemeConfig 归一为 UseSystem |
| `StartupThemeSettingsTests` | 新建及旧配置默认深色、三策略 JSON 往返、非法数值归 Dark；注入真实元数据构造离屏设置行，检查简英繁标准下拉框、顺序和搜索投影，选择只写配置且不变更应用资源；不执行生产配置发现、真实菜单保存或重启 |
| `ThemeSubscriptionLifecycleTests` | `IIconExtension.SetIconResource`、`DisPlayManagerExtension.ApplyChangedSelectedColor` 的弱引用订阅不阻止目标 GC；不是 ApplyCaption 或全体窗口生命周期测试 |
| `StartWindowThemeLifecycleTests` | 启动窗口构造时释放早期日志缓冲并保留其他 appender，关闭后恢复 `SystemThemeChanged` 和 `CurrentUIThemeChanged` 订阅数且窗口可被 GC；不显示窗口，不覆盖启动动画或全部系统事件时序 |
| `StartupPresentationTests` | 移除真实启动处理器后显示产品窗口，检查默认深色、固定浅色独立于应用，以及跟随软件时的简英繁文案、明确深浅与 UseSystem 解析；固定策略案例另验证配置修改仅在重开后生效。覆盖后续应用变色、英文布局和辅助名称、无调色按钮、进度不受调色影响及关闭后 UI 主题订阅恢复；不执行初始化或设备链。启动呈现和动画约束见[运行时启动界面](../../03-architecture/overview/runtime.md#启动界面与动画边界) |
| `CompactTitleBarChromeTests` | 紧凑窗口原生样式、标题区域与透明背景保护等行为；不替代 DWM 原生按钮视觉验收 |
| `MenuThemeTests` | 在真实离屏 WPF 窗口中加载浅/深色菜单，检查四种菜单角色、圆角与独立阴影、UI Automation 命令/勾选/禁用行为、快捷键列对齐及文本更新、纯文字空列收起、内容增减后的同级列对齐与宽度恢复、勾选切换的宽度稳定、长菜单滚动到末项，以及替换主题资源后既有菜单的背景色；不覆盖真实桌面鼠标穿越、键盘操作或系统高 DPI 视觉验收 |

测试引用不代表本次执行。独立主题测试覆盖受控替换、源加载失败恢复及窗口订阅；系统设置通知的真实时序、预览配置持久化、第三方缓存刷新和 DWM 真机表现仍需分别验证，不能由模板编译或控件截图推定。

主题库独立回归：`dotnet test Test/ColorVision.Themes.Tests/ColorVision.Themes.Tests.csproj -c Release -p:Platform=x64`。设置 `COLORVISION_THEME_PREVIEW` 为本地输出目录后，`CommonControlsLoadAndRenderWithRealTemplates` 会导出浅深控件预览 PNG，供结构迁移前后比较。该预览使用真实模板，属于开发验证，不加入产品菜单；截图不替代不同 DPI、原生 DWM 或现场设备窗口验收。

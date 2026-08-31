---
knowledge_id: "ui.search"
knowledge_type: "topic"
status: "current"
summary: "主窗口搜索框的关键词匹配、候选来源、缓存刷新与安全执行；重新打开读取插件候选列表，重复输入不扫描磁盘文件列表，旧动态来源仍同步。"
aliases: ["搜索框", "命令面板", "应用搜索", "独立搜索窗口", "关键词匹配", "搜索缓存", "候选目录刷新", "搜索候选", "搜索结果", "搜索刷新", "动态搜索", "网页搜索", "浏览器搜索", "Everything", "Ctrl+F", "Ctrl+Shift+P", "SearchWindow", "SearchControl", "SearchManager", "SearchQuery", "SearchResultItem", "SearchPaletteViewModel", "SearchCommandExecutor", "ContextualFindRouter", "SearchWindowHotkeyBridge", "SearchConfig", "SearchSettingsWindow", "ISearch", "ISearchMetadata", "ISearchProvider", "IDynamicSearchProvider", "IAsyncSearchProvider", "SearchMeta", "SearchType", "MenuSearchProvider", "SettingSearchProvider", "TemplateSearchProvider", "ThirdPartyAppSearchProvider", "FlowNodeDynamicSearchProvider", "EnableTemplateIndex", "EnableBrowserSearch"]
code_paths: ["UI/ColorVision.UI/Serach", "UI/ColorVision.Common/Interfaces/Serach", "UI/ColorVision.UI/AssemblyHandler.cs", "UI/ColorVision.UI/ConfigHandler.cs", "UI/ColorVision.UI.Desktop/Settings/SettingSearchProvider.cs", "UI/ColorVision.UI.Desktop/Settings/SettingEntryCatalog.cs", "UI/ColorVision.UI.Desktop/Settings/SettingWindow.xaml.cs", "UI/ColorVision.UI.Desktop/ThirdPartyApps/ThirdPartyAppSearchProvider.cs", "Engine/ColorVision.Engine/Templates/TemplateSearchProvider.cs", "Engine/ColorVision.Engine/Templates/Flow/Search/FlowNodeDynamicSearchProvider.cs", "Engine/ColorVision.Engine/Templates/Flow/Search/SqliteFlowNodeSearchIndex.cs", "Engine/ColorVision.Engine/Templates/Flow/Versioning/FlowCatalogService.cs", "ColorVision/MainWindow.xaml", "ColorVision/MainWindow.Hotkeys.cs"]
test_paths: ["Test/ColorVision.UI.Tests/SearchQueryTests.cs", "Test/ColorVision.UI.Tests/SearchManagerTests.cs", "Test/ColorVision.UI.Tests/SearchPaletteTests.cs", "Test/ColorVision.UI.Tests/ContextualFindRouterTests.cs", "Test/ColorVision.UI.Tests/MainWindowSearchShellTests.cs", "Test/ColorVision.UI.Tests/SearchWindowHotkeyBridgeTests.cs", "Test/ColorVision.UI.Tests/SearchWindowHostTests.cs", "Test/ColorVision.UI.Tests/SettingSearchProviderTests.cs", "Test/ColorVision.UI.Tests/TemplateSearchProviderTests.cs", "Test/ColorVision.UI.Tests/WpfResourceEmbeddingTests.cs", "Test/ColorVision.UI.Tests/FlowSafeSearchSidecarTests.cs", "Test/ColorVision.UI.Tests/FlowCatalogServiceTests.cs"]
related: ["ui.framework", "ui.discovery", "ui.menus", "ui.hotkeys", "ui.settings", "ui.common", "ui.configuration", "operations.main-window", "flow.templates", "governance.knowledge"]
---

# 应用搜索：入口、候选与执行

`UI/ColorVision.UI/Serach/` 是实际目录拼写。它提供 ColorVision 主窗口中的功能快速入口，可找菜单命令、设置、模板、工具和流程节点；不是仓库[知识检索](../../README.md)，也不遍历磁盘文件正文。工作区文件搜索、日志全文与历史检测结果尚未统一接入这个面板。

## 入口与局部查找

顶部不再放置搜索按钮；通过工具菜单“搜索命令与功能”或触发 `MenuCommandSearch` 的默认 `Ctrl+Shift+P`，打开承载 `SearchControl` 的独立 WPF `SearchWindow`。窗口首次相对主窗口居中，使用标准标题栏，可拖动、缩放；通过 `Owner` 关联主窗口，以非模态 `Show()` 显示且 `ShowInTaskbar=false`。点击外部、失焦或移动/缩放主窗口不关闭搜索；关闭主窗口则一并关闭。具体装配见[主窗口](../../01-user-guide/interface/main-window.md)。

`MenuContextualFind` 的默认 `Ctrl+F` 按当前焦点分流：

| 焦点场景 | 行为 |
| --- | --- |
| 支持 `ApplicationCommands.Find` 的正文编辑器 | 对原焦点执行局部查找 |
| 附有 `ContextualFindRouter.LocalFindCommand` 的内容 | 执行该局部命令；已有局部命令暂不可用时不回退全应用搜索 |
| Copilot 聊天 | 宿主适配现有会话查找命令，不模拟键盘事件、不加载其它会话 |
| 普通文本/密码输入、原生 `HwndHost` | 保留局部输入语义，不强占为应用搜索；不保证每种控件自身有查找 UI |
| 没有局部查找归属的主界面 | 打开应用搜索 |
| 搜索窗口自身活动 | 聚焦现有输入框，不新增窗口；搜索仍打开但焦点回到主窗口内容时，继续按上述局部查找规则处理 |

两项动作均通过[可配置快捷键](./hotkeys.md)注册，菜单提示读取当前运行时组合。`SearchWindowHotkeyBridge` 只在搜索窗口自身活动时转接这两项当前已注册、属于主宿主的应用内组合；遵守捕获门禁、改键和清空，不注册第二套快捷键。主窗口和搜索窗口均不活动时，搜索入口不激活窗口，即使用户把该动作配置为系统全局键也不会把搜索框盖到别的应用上。

从菜单或快捷键进入时，宿主保存内容区域焦点和原活动文档，而不是将后续保存、关闭等文档命令路由到搜索框。重复打开复用现有窗口及查询，只重新聚焦，不替换原命令目标。关闭时只有主窗口仍活动、原文档和宿主记住的焦点均未改变，且目标仍可用才恢复原焦点；用户切到文档 B 后不抢回 A，切到其它应用后也不强制抢回焦点。

## 展示、匹配与限额

输入框最多接受 256 个字符。结果显示名称、说明、分类和已有的快捷键，标题内直接高亮匹配文字，不把提供者文字解释为标记。没有快捷键的功能仍能搜索。分类使用稳定键 `Commands`、`Settings`、`Templates`、`FlowNodes`、`Tools`、`External`，显示名通过资源本地化。

- 上下键选择、Enter 执行，鼠标单击结果也可执行；重复 Enter 不重复提交。Esc 或标准标题栏关闭按钮关闭搜索窗口，点击窗口外部不会关闭。类型下拉框与底部按钮保留原生键盘操作。
- 空输入返回已有静态目录中的常用/最近入口，不查询动态来源、不添加外部启动项。会话最近只记成功返回的动作 ID，最多 10 项，不存查询文字、不持久化；不会把失效或不匹配项重新补进结果。
- 非空输入默认 120 ms 防抖。开始新查询立即清空旧结果及选择接纳状态；查询版本与取消令牌共同阻止旧请求晚返回覆盖新结果，关闭也会失效所有待接纳结果。
- 加载、无结果、部分来源失败和查询失败有不同状态。不可执行项可展示为弱化状态，但不允许提交；选择默认落在第一项可执行结果。

`SearchQuery.MatchAndRank` 按任意空白拆词，静态候选须覆盖全部词，但不同词可以命中不同字段。字段包括标题、说明、别名、分类、快捷键文本和原 `GuidId`；不区分大小写。完整标题优先于标题前缀、标题包含、别名、说明/分类/ID，近期使用只加小权重。尚无拼音生成、中文分词、编辑距离纠错或向量搜索；别名由提供者明确贡献。

动态 provider 已按自己索引匹配的条目不会因可见标题不含查询词而再次被排除，但仍参与统一相关度排序。外部启动项始终排在本地结果后；相同稳定身份只保留排序优先的一项，再按来源配额与总限额截取。

`QueryAsync` 默认最多展示 60 项，调用上限为 200；每个来源最多展示 20 项。静态目录每个来源最多物化 5000 项；动态调用要求至多 21 项，用额外一项判断截断，再参与来源限额。`IsTruncated` 表示存在目录、来源或总限额，不是精确的完整匹配总数。分类是结果过滤，不是保证相应其它 provider 不运行的调度隔离。

## 来源契约、身份与刷新

旧 `ISearch`、`ISearchProvider` 与 `IDynamicSearchProvider.Search(query, limit)` 保持兼容。可选 `ISearchMetadata` 提供 `Description`、`CategoryKey`、本地化 `Category`、`Aliases` 与 `ActionId`；`SearchMeta` 实现该接口。搜索、菜单和快捷键使用同一真实动作的 `ActionId` 关联说明及当前键位，不通过显示名称猜测身份。

`SearchResultItem` 是显示快照，仍保留原始 `ISearch Source` 供执行；它不创建自己的业务命令。身份优先为 `action:{ActionId}`，其次为 `{SearchType}:{GuidId}`，最后为提供者 ID、类型和标题的组合。**`SearchMeta.GuidId` 的默认值已由随机 GUID 改为 null**：这是现有属性默认行为的变化，没有删除接口成员。扩展应明确设置稳定 ID；依赖构造即得到随机 GUID 的代码需要自己赋值。无 ID 的后备身份随标题/语言变化，不适合跨语言持久引用。

`SearchManager` 根据 `AssemblyHandler.GetAssemblies()` 返回的实际程序集对象序列判断发现缓存是否失效，不再只比较数量。程序集类型加载、单个提供者构造/枚举失败按来源记录后继续；可恢复的 `ReflectionTypeLoadException` 保留成功加载的类型，枚举中途失败的提供者不发布半份目录。动态 provider 异常同样隔离，返回 `FailedSources`。provider 自己吞掉的错误无法由管理器凭空识别。

开始新的搜索窗口会话或关闭搜索设置后调用 `InvalidateCatalog()`，下次取候选重新枚举静态数据，但程序集未变化时不重复扫描类型。重复聚焦尚未关闭的窗口不刷新目录或重置查询。查询期间只过滤缓存目录，快捷键标签仍从当前运行时条目读取。旧 `GetISearches()` 保留显式刷新目录语义；`GetStaticResults(refresh: true)` 也能刷新。上游程序集视图本身是否刷新、新菜单类型是否进入菜单缓存，仍受各自[插件发现](../../02-developer-guide/plugin-development/overview.md)和[菜单](./menus.md)生命周期约束。

| 来源 | 当前范围与执行 |
| --- | --- |
| `MenuSearchProvider` | 取菜单 ID 过滤后的主窗口/Global 可见、非顶层条目；读取热键展示元数据，通常保留原 ICommand，包括 RoutedCommand。`MenuClose` 明确改用已有 `CloseDocumentCommand`，不依赖当前活动搜索窗口。隐藏过滤不是业务权限保证 |
| `SettingSearchProvider` | 从 `SettingEntryCatalog` 的页/行元数据构建目录，不读取配置属性值或构造自定义页面；选中后打开/激活设置窗口并定位稳定设置 ID，不直接修改设置 |
| `TemplateSearchProvider` | 枚举已注册模板的名称，身份包含注册键与名称；执行时重新解析当前注册并检查名称仍存在，然后打开模板入口 |
| `ThirdPartyAppSearchProvider` | 刷新工具目录，取已授权、已安装、名称非空的工具；使用已有 `DoubleClickCommand`，安装与业务权限仍由工具模块负责 |
| `FlowNodeDynamicSearchProvider` | 查询本地版本侧车并复核当前流程版本；执行只打开流程编辑器并定位节点，不启动流程 |
| Everything / 浏览器 | 显式外部启动入口，不将外部检索结果返回或混入本地索引 |

设置结果通过 `SettingNavigation` 定位已有窗口，或创建设置窗口并在关闭后调用通用配置保存；定位细节和页面内查找范围归[设置框架](./settings.md)。新增设置搜索不赋予更改某项设置的权限，也不保证所有自定义页面内部控件都可逐项定位。

## 异步、线程归属与尚存限制

新来源可实现 `IAsyncSearchProvider.SearchAsync(query, limit, cancellationToken)`；同时实现同步/异步接口时，`QueryAsync` 优先异步版本。调用从 UI 上下文开始，等待保留上下文，并使用 `WaitAsync(token)` 让不配合取消的异步任务不能继续占住当前查询的等待。取消并不强行终止 provider 已经开始的 I/O 或副作用；提供者仍负责合作取消和资源生命周期。

旧静态/动态 provider 可能读取 WPF 控件、绑定集合或业务单例，管理器**不会用 `Task.Run` 把它们整体搬到后台线程**。旧动态 `Search` 仍同步执行，慢查询可阻塞 UI；UI 的防抖/版本门禁不等于已解决所有来源的响应时间。`SearchDynamic` 旧 API 只调用同步提供者，新的界面走 `QueryAsync`。当前没有统一超时、provider Dispose 或后台索引构建协议。

配置按结果 `SearchType` 过滤：Menu 受 `EnableMenuIndex` 控制，包含新的设置条目；File 受 `EnableTemplateIndex` 控制，包含模板和流程节点；ThirdPartyApp 受 `EnableThirdPartyAppIndex` 控制。其它类型不经该类型开关。过滤发生在来源运行后，不保证关闭某个类型就禁止其构造或查询副作用。

Flow 首次查询仍可能延迟初始化 `FlowCatalogProvider.Shared`，在应用数据目录以 ReadWriteCreate 创建 `Config/FlowCatalog.db` 并建表。SQLite 按转义后的整段查询匹配安全投影，不是任意脚本/payload 全文搜索；获取每个流程最新已索引 revision 后还会复核当前模板版本，过滤后不继续补足。因此空结果不是数据库健康或所有模板已索引的证明，见[Flow 模板和版本侧车](../engine-components/template-flow-chain.md)。

## 提交、焦点与外部副作用

`SearchControl.Open(commandTarget, commandOwner, isCommandContextCurrent)` 接收原内容目标、明确的业务宿主与可选上下文检查；独立窗口将自己的 `Owner` 作为业务宿主传入，不把搜索窗口误当成命令所属窗口。`SubmitSelection` 仅接纳当前已完成查询中的选中项，输入法组合期间不提交。所有命令执行前检查原宿主仍有效；RoutedCommand 还检查原目标未卸载、仍在原窗口、原先可见的内容未隐藏且 DataContext 未替换，并通过宿主回调确认活动文档未改变。文档切换只拒绝旧 RoutedCommand，不禁用无关的普通应用命令。不能据此检测一切业务对象内部变化，各 provider 仍需复核自己的有效性。

仅 `CloseDocumentCommand` 在没有原内容焦点时允许使用明确的业务宿主作为后备路由，仍检查原宿主和活动文档上下文，并在关闭搜索窗口后再次复核。其它 RoutedCommand 没有内容目标时保持不可执行，不退回新焦点或任意活动窗口。

执行前检查 `CanExecute`，关闭搜索窗口后再次检查可执行性和对应目标有效性，防止关闭事件改变命令上下文；RoutedCommand 显式使用原目标执行，焦点恢复须满足上述条件。同步异常由搜索入口记录并提示，失败不记为最近使用。ICommand 仍是同步协议：某个命令内部启动异步工作或静默拒绝时，正常返回不能证明业务完成；详见[Common 命令契约](./ColorVision.Common.md)。搜索不绕过业务已有确认，也没有为缺少业务鉴权的命令自动补齐授权体系。

外部入口只在非空查询、对应开关启用时生成，查询过程不会因此自动打开外部应用：

- Everything 还要求配置路径存在。候选捕获当时的路径和查询；执行前复核启用、路径未改变且文件仍存在。`ProcessStartInfo.ArgumentList` 将 `-s` 与整个查询作为两个独立参数，不再拼接不带转义的命令字符串。路径存在不证明程序身份可信。
- 浏览器候选捕获引擎和查询；执行前复核当前开关与引擎相同。Google/Baidu/Bing URL 对查询执行 `Uri.EscapeDataString`，未知枚举值回退 Google。选中后才交给 ShellExecute，可能联网；不采集外部搜索结果。

进程启动返回不证明搜索完成或后续网络成功。外部启动失败走统一同步异常提示。其它候选的副作用由自身 Command 决定，不能把外部入口“不自动启动”推广为所有 provider 查询都无副作用。

“搜索设置”按钮现在是显式入口，不再要求双击搜索图标。其独立 `SearchSettingsWindow` 仍直接绑定活 `SearchConfig`；关闭只收起窗口，不回滚，也没有在这个调用点直接保存配置。调用方只使目录失效，后续持久化仍按[配置服务](./configuration.md)的保存流程判断；不要把关闭按钮的 IsCancel 当作撤销修改。

## 验证入口与边界

- `SearchQueryTests`：字段匹配、排序、分类、跨来源去重、配额、最近权重与稳定后备身份。
- `SearchManagerTests`：缓存及同数量程序集替换、构造/枚举故障隔离、部分类型加载、空查询、异步优先/取消、旧来源线程归属、开关/限额、菜单元数据、关闭文档的明确路由与外部参数；使用隔离来源与无害/不可执行业务替身。
- `SearchPaletteTests`：旧选择失效、晚响应、关闭/重开、错误状态、命令门禁、业务宿主与原焦点、切换/隐藏原内容、IME 组合保护、标题高亮，以及中英文深浅色窄宽布局。显式设置 `COLORVISION_SEARCH_PREVIEW_DIRECTORY` 可输出隔离预览 PNG，不启动生产设备。
- `ContextualFindRouterTests`、`MainWindowSearchShellTests`：局部 Find 归属、编辑器/聊天适配、菜单焦点、独立窗口标记及宿主接线；不是生产主窗口的硬件验收。
- `SearchWindowHotkeyBridgeTests`、`SearchWindowHostTests`：独立搜索窗口的当前组合转接、活动状态和捕获门禁，以及标准可缩放非模态 Owner 窗口、移动宿主不关闭、单独关闭后重开和 Owner 关闭联动。会话测试注入合成查询，检查关闭取消和窗口关闭后才执行结果，不运行真实查询来源或生产主窗口。
- `SettingSearchProviderTests`、`TemplateSearchProviderTests`：元数据建索引不读配置值/构造页面、稳定设置身份与定位，以及模板同名去重边界、移除后不执行旧目标。
- `FlowSafeSearchSidecarTests`、`FlowCatalogServiceTests`：侧车安全投影和版本索引；不等同于真实流程窗口定位验收。`WpfResourceEmbeddingTests` 只补充 BAML 嵌入检查。

上述测试文件是可运行验证入口，不是本页声称已通过的结果。物理键盘、各输入法和键盘布局、窗口拖动、多显示器缩放与真实业务操作仍需在明确授权的隔离环境验收；不通过设备运行、删除数据或启动浏览器来顺带测试文档。

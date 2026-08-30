---
knowledge_id: "ui.search"
knowledge_type: "topic"
status: "current"
summary: "主窗口搜索框的关键词匹配、候选来源、刷新缓存、结果顺序和执行；关闭搜索设置不回滚也不直接保存，回车不统一检查命令权限。"
aliases: ["搜索框", "搜索候选", "搜索结果", "搜索刷新", "动态搜索", "网页搜索", "浏览器搜索", "Everything", "SearchControl", "SearchManager", "SearchConfig", "SearchSettingsWindow", "ISearch", "ISearchProvider", "IDynamicSearchProvider", "SearchMeta", "SearchType", "MenuSearchProvider", "TemplateSearchProvider", "ThirdPartyAppSearchProvider", "FlowNodeDynamicSearchProvider", "EnableTemplateIndex", "EnableBrowserSearch"]
code_paths: ["UI/ColorVision.UI/Serach", "UI/ColorVision.Common/Interfaces/Serach", "UI/ColorVision.UI/AssemblyHandler.cs", "UI/ColorVision.UI/ConfigHandler.cs", "UI/ColorVision.UI.Desktop/ThirdPartyApps/ThirdPartyAppSearchProvider.cs", "Engine/ColorVision.Engine/Templates/TemplateSearchProvider.cs", "Engine/ColorVision.Engine/Templates/Flow/Search/FlowNodeDynamicSearchProvider.cs", "Engine/ColorVision.Engine/Templates/Flow/Search/SqliteFlowNodeSearchIndex.cs", "Engine/ColorVision.Engine/Templates/Flow/Versioning/FlowCatalogService.cs", "ColorVision/MainWindow.xaml", "ColorVision/MainWindow.xaml.cs"]
test_paths: ["Test/ColorVision.UI.Tests/WpfResourceEmbeddingTests.cs", "Test/ColorVision.UI.Tests/FlowSafeSearchSidecarTests.cs", "Test/ColorVision.UI.Tests/FlowCatalogServiceTests.cs"]
related: ["ui.framework", "ui.discovery", "ui.menus", "ui.common", "ui.configuration", "operations.main-window", "flow.templates", "governance.knowledge"]
---

# 主窗口搜索：候选、刷新与执行

`UI/ColorVision.UI/Serach/` 是实际目录拼写，负责 ColorVision 产品中的搜索框，不是本仓库供 Codex 使用的[知识检索](../../README.md)，也不是遍历仓库或磁盘文件内容的全文引擎。共享接口位于 `UI/ColorVision.Common/Interfaces/Serach/`，具体菜单、模板和工具由各模块贡献。

`MainWindow.Hotkeys.cs` 的 `MenuCommandSearch` 通过可配置动作将默认 `Ctrl+Shift+P` 接到功能搜索，`Ctrl+F` 留给当前编辑器的正文查找；窄窗口使用菜单下方的搜索行，折叠规则归[宿主装配](../../01-user-guide/interface/main-window.md)。聚焦、查询候选和执行结果是不同阶段；找到入口不证明其业务操作已获授权或成功。

## 候选从哪里来、何时刷新

`SearchManager.GetISearches()` 从 `AssemblyHandler.GetAssemblies()` 的缓存/过滤视图扫描 `ISearch` 和 `ISearchProvider` 实现，每次调用重新构造和枚举。两类分别扫描，不是菜单那种互斥发现路径；不按 ID 统一去重。静态发现没有逐程序集、类型或 provider 的异常隔离，类型读取、构造或枚举失败可中断这一轮。

`SearchControl.Searches` 是控件持有的静态候选集合，通常在搜索框 GotFocus 时重建；UpdateResults 发现集合为空也会重建，且此步骤在空关键词检查之前。通过搜索图标打开设置并关闭后，调用方还会重建集合和结果。普通非空集合上的每次文字变化只过滤已有静态集合，不等于重新加载插件或文件列表；获焦点本身也只重建 Searches，不立即重算已显示的 FilteredResults。

| 候选来源 | 当前内容与责任 |
| --- | --- |
| `MenuSearchProvider` | 从 `GetAllMenuItemsFiltered` 复制菜单 Header/GuidId/Command；ID 隐藏过滤不等于当前窗口可见或可执行，见[菜单契约](./menus.md) |
| `TemplateSearchProvider` | 枚举 `TemplateControl.ITemplateNames` 已注册模板名并去重；选择后打开对应模板入口，不扫描任意磁盘文件 |
| `ThirdPartyAppSearchProvider` | 调用工具 manager.Refresh，取已授权、已安装且名称非空的工具，按 Order/Name 排序；执行使用该工具的 DoubleClickCommand |
| `IDynamicSearchProvider` | 每次非空 UpdateResults 调用同步 Search；当前 Flow 节点 provider 查询本地版本侧车，内容及执行复核见下文 |
| Everything / 浏览器 | 末尾追加外部启动命令，不把外部引擎结果取回并混入本地候选 |

动态 provider 实例有另一层缓存：仅比较程序集视图的**数量**是否变化，数量相同就复用原实例；同数量的程序集替换不能使它自动失效。重建 provider 时没有统一 Dispose 旧实例，类型发现/构造异常也不在逐 provider 查询的 try/catch 内。SearchManager 不缓存查询结果，但各 provider 可以有自己的状态，不能据此保证相同关键词总会读取最新业务数据。

查询调用链没有统一异步、节流、取消或超时调度；provider 构造和查询在调用线程执行，慢 provider 可能阻塞搜索框。传播到 SearchManager 的动态查询异常按 provider 记录后继续，静态发现不具备相同降级；provider 内部自行吞掉的异常则未必留下日志，“没有结果”不唯一表示没有对应对象。

## 匹配、过滤和结果顺序

静态匹配只按 ASCII 空格拆词，各词必须在同一候选的 Header 或 GuidId 中命中，使用 OrdinalIgnoreCase 包含判断。不是中文分词、模糊匹配或所有字段全文搜索；Tab 不作为分隔符。动态 provider 接收原始整段查询，自行定义匹配规则。

结果依次拼接为：静态匹配项 → 动态结果 → Everything → 浏览器。没有全局相关度排序、跨来源去重或总结果数上限。来源内部的排序不代表整个搜索框按该规则排序。

`SearchDynamic` 默认最多收集 **30 个动态结果**，不是整个候选列表最多 30 个。provider 按发现顺序查询，前面的可用结果可占满限额；每个 provider 收到原始 limit，manager 在过滤后按剩余额度截取。框体 XAML 的 `MaxLength=15` 限制输入长度，Popup 的最大高度只是显示/滚动限制，都不是候选数限制。

`SearchConfig.IsIndexedTypeEnabled` 按结果的 Type 筛选：

| Type | 开关 | 容易混淆的边界 |
| --- | --- | --- |
| Menu | EnableMenuIndex | 不能据此推导命令权限 |
| File | EnableTemplateIndex | 所有标为 File 的候选都受此开关影响，包括 Flow 节点，不只模板列表 |
| ThirdPartyApp | EnableThirdPartyAppIndex | 工具 provider 自己还有授权/安装筛选 |
| Link 和其它值 | 此方法返回 true | 外部入口另由 EnableEverythingSearch / EnableBrowserSearch 决定是否添加 |

这些索引开关、两个外部搜索开关默认均为 true；浏览器默认 Google。类型过滤发生在 provider 构造/查询之后，不是阻止 provider 运行或保证无副作用的开关；静态集合已建立后，直接改配置也不会主动通知它重建。manager 要求 Header 非空白，但不统一要求 Command 非空，条目出现后仍可能没有可执行动作。

## Flow 节点搜索不是执行流程

`Engine/ColorVision.Engine/Templates/Flow/Search/FlowNodeDynamicSearchProvider.cs` 调用 `FlowCatalogProvider.Shared.SearchLatest`，取每个 Flow **最新已索引 revision** 的匹配项，再核对当前 `TemplateFlow.Params` 中 FlowKey 与 revision。当前模板不匹配的条目被丢弃，不继续补查下一批，所以返回数可以小于 limit；最新已索引不意味着每个已保存模板都有有效投影。

SQLite 实现对侧车 SearchText 作经过 LIKE 特殊字符转义的整段包含匹配，按 revision 降序、FlowKey、NodePath 排序，不能套用静态候选的拆词规则。索引只存安全投影，不是任意节点 payload/脚本的全文副本；保存、投影与失败边界归[Flow 模板和版本侧车](../engine-components/template-flow-chain.md)。

执行结果前，provider 再核对当前版本；不匹配时提示重新搜索。通过后打开 `FlowEngineToolWindow` 并在 Loaded 尝试定位 SourceNodeGuid，目标不存在时给提示。这是打开编辑器和定位节点，不是启动流程执行，也不保证所有历史候选仍可定位。

首次查询可能触发 `FlowCatalogProvider.Shared` 的延迟初始化，在应用数据目录创建 Config、以 ReadWriteCreate 打开 `FlowCatalog.db` 并建表。因此输入搜索不保证纯内存、无文件写入，即使最终 File 类型被过滤。provider 对侧车查询异常返回空，不能将空结果当成数据库健康证明。本次文档核对未启动这条数据库路径。

## 设置关闭、结果执行与外部副作用

双击搜索图标打开 `SearchSettingsWindow`。其 DataContext 直接使用活 `SearchConfig`，没有工作副本；Close 只关闭，按钮的 IsCancel 不提供回滚。调用方返回后仅刷新候选和结果，**没有调用配置保存**。后续落盘取决于通用[配置持久化](./configuration.md)，包括退出时的保存尝试；关闭设置不是保存成功或取消修改的信号。

Enter 和结果双击处理只检查 `SelectedIndex > -1`，清空输入后直接调用候选 `Command?.Execute(this)`，不先检查 CanExecute，没有统一业务异常捕获、异步等待或权限检查。只在菜单 Command 的 predicate 里做检查，不能保证搜索入口会阻止同一操作；命令执行本身仍须承担相应业务边界，见[Common 命令契约](./ColorVision.Common.md)。

空白输入只关闭 Popup，不清空 FilteredResults、ItemsSource 或选中索引；执行入口也不检查当前文本或 Popup 状态。因此源码不能保证清空文字或收起候选后旧选中项就不可执行。这是现存输入接纳条件的缺口，未在 WPF 环境复现，也未在本次文档工作中修复。

勾选网页搜索并输入关键词，仅增加一个候选，不自动打开浏览器或向该搜索引擎发请求。执行相应候选后才发生外部启动：

- Everything：生成候选时还要求 `File.Exists(EverythingPath)`；执行时使用 ShellExecute 启动配置路径，工作目录为当前进程目录，Arguments 为 `-s {searchtext}`。这条参数字符串没有为查询单独加引号或结构化转义，不能保证任意输入都按一个纯文本参数解释；路径存在也不证明该程序身份可信。
- 浏览器：Google/Baidu/Bing URL 对查询使用 `Uri.EscapeDataString`，交给系统 ShellExecute 打开；未知枚举值回退 Google。实际访问可能联网，执行前仍需符合当前任务授权。

启用开关和路径存在只在生成候选时检查，执行时不重新核对。命令捕获当时的配置对象和查询文字，到执行时再读该对象的 EverythingPath / SearchEngine；因此实际目标可能与生成候选时不同，配置重载后该对象也不保证仍是最新实例。

两种外部启动捕获同步异常并显示消息，但进程启动返回不证明检索完成；也没有外部搜索结果、退出状态或后续网络失败的完成协议。普通候选的副作用由其 provider/Command 决定，不能把外部入口“不自动启动”推广成整个搜索框无副作用。

## 源码定位与验证缺口

- 控件刷新、拼接和执行：`UI/ColorVision.UI/Serach/SearchControl.xaml.cs`；输入上限和列表呈现：同目录 `.xaml`。
- provider 发现/缓存和限额：同目录 `SearchManager.cs`；类型开关与设置关闭：`SearchConfig.cs`、`SearchSettingsWindow.xaml.cs`。
- 接口和候选数据：`UI/ColorVision.Common/Interfaces/Serach/`；菜单、模板、工具与 Flow provider 的实际路径列在元数据中。

`WpfResourceEmbeddingTests.SearchControl_CompiledXamlIsEmbedded` 只检查 BAML 嵌入，不构造或交互搜索框。`FlowSafeSearchSidecarTests` 覆盖安全投影、深链和内存/SQLite 查询；`FlowCatalogServiceTests` 覆盖保存版本及索引投影，不覆盖动态 provider 到真实窗口定位。列出这些测试不代表本次已运行。

目前未找到 SearchManager 缓存、结果顺序、CanExecute/旧候选执行、设置关窗或外部启动的专项交互测试。文档检索与网站通过不能代替产品验收；后续应在获授权的隔离配置和无害命令/provider 下测试，不通过实际设备操作或浏览器联网来顺带验证文档。

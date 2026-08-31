---
knowledge_id: "ui.settings"
knowledge_type: "topic"
status: "current"
summary: "设置窗口的元数据发现、全局搜索定位、侧栏筛选与活对象编辑；普通选项关窗不撤销，启动检查更新仍是聚合开关。"
aliases: ["设置窗口", "选项", "设置搜索", "定位设置项", "自定义设置页", "启动检查更新", "SettingWindow", "SettingWindowController", "SettingRowFactory", "SettingMetadataResolver", "SettingEntryCatalog", "SettingSearchProvider", "SettingNavigation", "NavigateToSetting", "ConfigSettingManager", "IConfigSettingProvider", "ConfigSettingMetadata", "AggregatedBoolSetting", "MenuOptions"]
code_paths: ["UI/ColorVision.UI.Desktop/Settings", "UI/ColorVision.UI/ConfigSetting/ConfigSettingManager.cs", "UI/ColorVision.Common/Interfaces/ConfigSetting", "UI/ColorVision.UI/AssemblyHandler.cs"]
test_paths: ["Test/ColorVision.UI.Tests/PropertyEditorContractTests.cs", "Test/ColorVision.UI.Tests/ConfigServiceAdaptersTests.cs", "Test/ColorVision.UI.Tests/StorageMaintenanceTests.cs", "Test/ColorVision.UI.Tests/HotkeySettingsTests.cs", "Test/ColorVision.UI.Tests/SettingSearchProviderTests.cs"]
related: ["ui.desktop", "ui.configuration", "ui.property-grid", "ui.discovery", "ui.hotkeys", "ui.search", "ui.localization", "operations.exports", "delivery.update", "ui.storage-maintenance"]
---

# 设置窗口：发现、编辑与关闭契约

`UI/ColorVision.UI.Desktop/Settings/` 负责把配置元数据呈现为侧栏分组和内容卡片；`UI/ColorVision.UI/ConfigSetting/ConfigSettingManager.cs` 负责发现和缓存元数据。窗口不是配置事务，也不是所有业务配置的唯一入口。配置文件的写入、重载和对象替换归[配置持久化](./configuration.md)，具体编辑器的绑定方式归[属性编辑](./property-grid.md)。

## 从打开到保存

工具菜单“选项”及默认 `Ctrl+,` 进入 `MenuOptions.Execute`，与全局搜索中的设置结果共用 `SettingNavigation.Show`。已有可见的 `SettingWindow` 会被激活复用；没有可见窗口时新建并 `ShowDialog()`，返回后无条件调用 `ConfigService.Instance.SaveConfigs()`，不检查 `DialogResult`。因此，从这个入口关闭窗口不能解释为取消修改或不保存；复用已有窗口不会额外启动一次保存流程。

`SettingWindow` 装配 controller，转发搜索和分组选择，并提供按稳定 ID 定位条目的 `NavigateToSetting`；通用窗口没有“保存/取消”事务或回滚处理。`SettingRowFactory` 将原始 `metadata.Source` 交给 `PropertyEditorHelper.GenProperties`，没有先建立工作副本。编辑何时写回、是否触发额外行为取决于具体编辑器和属性 setter；自定义页面也可以有自己的保存、下载或系统操作。

需要区分三个结果：控件显示新值、运行中的配置对象已修改、文件保存正常返回。关窗后的保存可能失败，窗口层没有撤销前面修改的补偿；直接构造 `SettingWindow` 的其它调用者也不会自动获得菜单的保存步骤。可替换 `IConfigService` 是否支持保存仍需查[适配器契约](./configuration.md)，能解析配置对象不代表支持 `SaveConfigs`。

设置导入是另一条有写入副作用的链路，见[导入导出](../../01-user-guide/data-management/export-import.md)；不要把进入设置窗口当成只读检查的保证。

自定义页不能套用普通属性行的提交规则。例如[快捷键页](./hotkeys.md)使用可搜索的动作列表和单项编辑弹窗：弹窗候选是副本，确认后立即应用并保存；清除、单项恢复和确认全部恢复也各自提交。取消弹窗不应用候选，已成功应用的键位不能靠关闭设置窗口撤销；外层关窗保存不会自动提交未确认的快捷键草稿。

[语言编辑器](./localization.md)又是不同边界：选择后立即请求确认，确认便保存配置并重启应用，不等普通选项窗口关闭；取消只处理该编辑器的文化属性，不是撤销所有选项。

## 发现来源与两层缓存

`ConfigSettingManager.GetAllSettings` 使用 `AssemblyHandler.GetAssemblies()` 的缓存/过滤程序集视图，不是扫描磁盘上所有 DLL。首次建立类型缓存时收集非抽象的 `IConfigSettingProvider` 与 `IConfig` 实现：

| 来源 | 生成规则与失败边界 |
| --- | --- |
| `IConfigSettingProvider` | 无参构造 provider，追加其返回的元数据；构造或获取失败按类型记录日志并继续 |
| `IConfig` + `[ConfigSetting]` | 只从带该特性的公共实例属性创建项；需要时通过 `ConfigService` 获取源对象，解析失败跳过该类型 |
| 程序集类型读取 | `ReflectionTypeLoadException` 保留仍可取得的类型；其它程序集读取错误跳过该程序集 |

两条来源不统一去重；同一个设置同时由 provider 和特性贡献，可能产生重复项。manager 返回已缓存的元数据列表，元数据中的 `Source` 是对象引用，不是每次渲染重新解析。

`InvalidateCache()` **只清设置项缓存，不清 provider/config 类型缓存**。因此不能仅靠它发现后来加载插件的新设置类型；即使再次打开窗口，也仍要考虑 manager 的类型缓存和 `AssemblyHandler` 的上游缓存。它也不通知已打开的 controller 重新加载。当前窗口的搜索、切组只是使用已有 entries，不重新调用 manager。

配置重载可能替换源实例，已有元数据、编辑器和自定义页面仍可能持有旧引用。失效设置项缓存与重建控件是不同动作；具体替换和订阅责任继续查[配置重载](./configuration.md)。

## 侧栏、搜索与自定义页面

常规页面按元数据分成带标题的卡片：主题与语言使用 `Appearance`，聚合启动更新、更新前快照和代理设置使用 `Updates`，日志级别使用 `Diagnostics`；其它设置继续沿用原分区。侧栏与属性编辑器能力保留，切换导航组或更新搜索会将内容滚动回顶部。通用行保留可换行说明，非布尔编辑器采用较紧凑的右侧宽度。

“存储与维护”通过独立 `IConfigSettingProvider` 页面接入，不在普通设置行上即时执行删除。扫描、确认、单项/选中项清理、数据与备份入口、选择性下次启动重置见[存储清理与设置重置](./storage-maintenance.md)。该页忙碌时的关闭拦截与卸载取消是页面自己的契约，不表示所有自定义设置页自动拥有相同协议。

“快捷键”页内部另有动作搜索，匹配名称、说明、ID、分类、来源和键位文字；它与设置窗口侧栏搜索独立。侧栏不会自动递归查询快捷键列表的每个动作，详见[快捷键编辑](./hotkeys.md)。

`SettingWindowController` 在加载时通过 `SettingEntryCatalog` 把元数据变成 `SettingEntry`；缺少属性源、绑定名或对应属性的条目不能成为普通属性行。`SettingMetadataResolver` 负责稳定条目 ID、标题、说明、导航分组、内容分区和搜索文本。窗口与全局搜索共用这一目录投影，包括下述启动更新聚合规则。

| 行为 | 当前规则 |
| --- | --- |
| 导航 | `ListBox` 侧栏，不是 Tab；普通 Property 项按 `metadata.Group` 分组，缺省为 `Universal`。非 Property 项在有标题时用解析后的标题作为导航组名 |
| 排序 | `Universal` 组在前，其它组按最小条目 Order、显示名；组内按分区顺序、元数据 Order。普通属性卡片先于非 Property 页面 |
| 搜索 | 去掉查询首尾空白后，在预先建立的 `SearchText` 上做不区分大小写的整串包含匹配；不是分词、模糊匹配或全文搜索 |
| 搜索范围 | 条目标题、说明、组/分区、绑定名、源类型、View 类型和属性显示元数据；不遍历自定义 View 内的控件文字，也不将 Class 展开的每个子属性作为独立导航搜索项 |
| 选中组 | 筛选后优先保留原选中组；该组消失时选第一个可见组，内容随之重建 |

`ConfigSettingType.TabItem` 的枚举名和 `ConfigSettingAttribute.Group` 注释仍保留旧 Tab 命名；当前呈现以 `SettingWindow.xaml` 与 controller 的侧栏实现为准，不能从旧名称推导实际控件结构。

自定义内容由 `SettingRowFactory` 按条目懒创建。`ViewType` 必须可无参实例化为 `FrameworkElement`；factory 不自动把 `metadata.Source` 设为该 View 的 `DataContext`，自定义页自行负责绑定。

`Class` 类型且 `Source` 为 `ViewModelBase` 时会展开 helper 认定可编辑的属性，可再附加 `ViewType`；不要求每个子属性都有 `[ConfigSetting]`。不要把该展开规则与 manager 的特性发现规则混为一谈。

每个窗口条目缓存自己的自定义内容。切组或搜索时，普通属性行重新生成，自定义 View 从旧父容器分离后复用；创建失败、类型不符或缺少页面产生的提示内容也会缓存，切走再切回不会自动重试构造。controller 没有统一的页面销毁/退订协议，页面应自行处理资源与事件生命周期，不能把一次 `Unloaded` 当成最终释放。

## 全局搜索定位设置

`SettingSearchProvider` 从 `ConfigSettingManager.GetAllSettings()` 获取元数据，交给共享目录解析，不为索引构造设置窗口、属性编辑器或自定义 View。目录投影本身不读取/修改设置属性值；上游 provider 构造与 `GetConfigSettings()` 仍保留各自行为，不能由此保证所有配置发现无副作用。

搜索结果使用解析后的标题、说明和分组/分区文本，`Aliases` 包含目录已有的 `SearchText`。`CategoryKey=Settings` 与命令结果分开展示；兼容旧开关时仍使用 `SearchType.Menu`，所以 `EnableMenuIndex` 也控制这些设置候选。匹配排序、异步调度和结果执行归[产品搜索契约](./search.md)，不改变设置窗口内部仍按整串匹配的侧栏筛选规则。

- Property 项 ID 根据源类型的程序集名、完整类型名与 BindingName 建立，不随显示标题或界面语言变化；非 Property 项使用来源/View 类型和 Group 等标识，缺少来源及 View 时才以名称补足。它不是持久化配置键，也不能保证未提供稳定来源的任意插件项跨版本不变。
- 启动更新始终使用 `setting:startup-check-updates`，搜索只出现实际渲染的聚合项，不分别暴露两个已经收起的原始属性。搜索 provider 按 ID 保留首项去重，不改变设置 manager 或窗口可能含重复元数据的原有规则。
- 选择结果仅请求导航。`NavigateToSetting` 在当前窗口目录找到 ID 后清除旧筛选、选择其分组，并在布局可见后请求将对应行/页面带入视口；未找到返回 false，不修改设置值，也不扫描自定义页面内部控件。返回 true 只表示找到渲染目标，不证明受动态可见性限制的行已经显示，或自定义页面初始化成功。

## “启动检查更新”是聚合开关

`SettingEntryCatalog` 特判 Property 项的 `IsAutoUpdate`：源对象的运行时类型简单名称为 `AutoUpdateConfig` 或 `MarketplaceWindowConfig` 时，收起原始行并组合为“启动检查更新”。每类保留遇到的最后一项；有效目标只有一个时也能生成聚合行，不应假定两个配置一定都被发现。

`AggregatedBoolSetting.IsChecked` 的 getter 使用 `Any`：**打勾只说明至少一个目标为 true，不证明主程序和插件自动更新都开启**。setter 依次将同一个值写入可写目标；不是事务，后面的写入异常不回滚前面的修改。它不订阅目标各自的变化，外部改值也不保证当前勾选立即刷新。

聚合器不保存文件、不联网检查也不直接安装更新。实际启动检查、缓存和安装流程归[更新机制](../../02-developer-guide/deployment/auto-update.md)；界面勾选不能替代更新流程完成证据。

## 源码定位与验证缺口

| 要确认的问题 | 主要源码 |
| --- | --- |
| 选项/搜索入口复用与关闭后的保存 | `Settings/MenuOptions.cs`、`SettingSearchProvider.cs` 中的 `SettingNavigation` |
| 窗口组成和事件转发 | `Settings/SettingWindow.xaml`、`SettingWindow.xaml.cs` |
| 元数据目录、分组与聚合开关 | `Settings/SettingEntryCatalog.cs`、`SettingMetadataResolver.cs`、`SettingWindowController.cs`、`AggregatedBoolSetting.cs` |
| 全局设置候选与精确行定位 | `Settings/SettingSearchProvider.cs`、`SettingWindow.NavigateToSetting` |
| 活对象编辑、自定义页面复用 | `Settings/SettingRowFactory.cs`、`SettingEntry.cs` |
| 设置发现与失效范围 | `UI/ColorVision.UI/ConfigSetting/ConfigSettingManager.cs` |

上表省略前缀的 `Settings/` 路径均相对于 `UI/ColorVision.UI.Desktop/`。`PropertyEditorContractTests` 覆盖通用 helper 的绑定、只读属性、失败降级和实例复用，不覆盖整个设置窗口。`ConfigServiceAdaptersTests` 中名称含 `ConfigSettingManager_WorksWith...` 的用例只模拟对象解析，未构造 manager，不能作为设置发现或窗口集成测试。

`StorageMaintenanceTests` 通过注入独立元数据构造真实设置窗口，覆盖分区标题、搜索、切组回顶和说明文本，并检查中英文、深浅主题与窄窗口下的维护控件布局。该入口不调用生产设置发现，也不操作真实配置或缓存；它不替代真实 provider 发现、关窗保存、配置重载重绑定或更新安装验证。文档检索与网站校验不填补这些运行时缺口；实际修改设置、导入和更新检查应在获授权的隔离环境中单独验证。

`HotkeySettingsTests` 同样通过独立元数据把快捷键页装进真实设置框架，检查中英文、深浅主题和不同宽度下的列表布局、页内文本搜索与空状态；应用委托及键位数据是隔离替身，不调用生产配置或业务操作，具体覆盖与 Win32 验证边界见[快捷键契约](./hotkeys.md)。

`SettingSearchProviderTests` 通过注入元数据和导航回调，覆盖目录投影不读取属性值/构造自定义页面、稳定 ID 与去重、聚合项、仅按 ID 导航，以及隔离真实设置窗口中的清除筛选、分组选择和目标行定位。它不调用生产配置发现、真实菜单保存或自定义页面业务，也不证明真实窗口滚动、动态属性可见性或所有插件设置均可定位。列出测试不表示已运行通过。

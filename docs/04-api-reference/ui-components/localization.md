---
knowledge_id: "ui.localization"
knowledge_type: "topic"
status: "current"
summary: "界面语言的资源发现、系统语言回退、设置绑定和重启切换；语言下拉框不证明插件翻译完整，修改配置值不等于刷新窗口。"
aliases: ["多语言", "界面语言", "语言切换", "语言下拉框", "系统语言", "语言资源", "翻译", "日语", "简体中文", "繁体中文", "英文", "LanguageManager", "LanguageConfig", "LanguagePropertiesEditor", "UICulture", "CurrentUICulture", "LanguageChange", "zh-Hans", "zh-Hant", "添加界面语言", "卫星资源"]
code_paths: ["UI/ColorVision.UI/Languages", "UI/ColorVision.UI/Properties/Resources.resx", "UI/ColorVision.UI/Properties/Resources.en.resx", "UI/ColorVision.UI/Properties/Resources.zh-Hant.resx", "UI/ColorVision.UI/Properties/Resources.Designer.cs", "UI/ColorVision.UI/PropertyEditor/PropertyEditorHelper.cs", "UI/ColorVision.UI/Serach/SearchSettingsWindow.xaml.cs", "UI/ColorVision.UI.Desktop/Settings/SettingWindow.xaml", "ColorVision/App.xaml.cs", "ColorVision/Copilot/Capabilities/CopilotApplicationControlSupport.cs", "ColorVision/Copilot/Capabilities/CopilotAgentCapabilityServices.cs"]
test_paths: ["Test/ColorVision.UI.Tests/EngineUiLocalizationTests.cs", "Test/ColorVision.UI.Tests/FlowLocalizationTests.cs"]
related: ["ui.framework", "ui.settings", "ui.configuration", "ui.property-grid", "platform.runtime", "copilot.tool-contracts", "governance.maintenance"]
---

# 界面语言：资源发现、配置与重启

`UI/ColorVision.UI/Languages/` 管理 ColorVision 产品的界面文化选择。正常设置入口走确认、保存配置、启动新进程并关闭当前应用；它不是给全部已打开窗口广播翻译的实时切换服务。应用的语言资源与[仓库知识语言策略](../../knowledge/maintenance.md)相互独立：保留英文 `AGENTS.md` 或取消文档语言镜像，都不决定产品支持哪些界面文化。

## 资源、可选列表与系统语言

本模块提供以下资源：

| 界面文化 | 资源文件 |
| --- | --- |
| 简体中文 `zh-Hans` | 中性资源 `Resources.resx` |
| 英文 `en` | `Resources.en.resx` |
| 繁体中文 `zh-Hant` | `Resources.zh-Hant.resx` |

资源文件不是可选语言的固定白名单。下拉框还受实际输出目录中的卫星资源、当前线程文化和系统文化影响；某语言能被选择，不代表所有模块都有对应翻译。

两种目录扫描的用途不同：

| 入口 | 当前行为 |
| --- | --- |
| `GetLanguages(defaultProcessDllName)` | 从当前线程 CurrentUICulture 开始，在执行 LanguageManager 的程序集目录下逐个子目录查指定资源 DLL（仅该子目录顶层），再确保加入 zh-Hans 和 InstalledUICulture；同时清空重建静态显示名字典 keyValuePairs |
| `GetDefaultLanguages(defaultProcessDllName)` | 在同一根目录的每个子目录内递归查找资源 DLL，收集子目录名并确保加入 zh-Hans；用于配置 getter 的可用性判断，不主动加入当前/系统文化 |

未传 DLL 名时，两者使用 `AppDomain.CurrentDomain.FriendlyName + ".resources.dll"`，不是枚举每个插件的所有本地化资源，也不检查每个字符串是否有翻译。目录名未在此处统一验证成有效 CultureInfo；目录枚举异常没有局部隔离。扫描结果只是当前环境的候选证据，不是完整翻译清单。

`LanguageManager.Current.Languages` 在 manager 实例初始化时取得列表，不因资源文件或系统设置变化自动刷新。Copilot 的 `GetAvailableLanguages()` 会另行调用 GetLanguages 并替换该列表，但也不负责刷新所有已创建的语言编辑器。

显示为“跟随系统”的项实际使用 `CultureInfo.InstalledUICulture.Name` 这个文化字符串，不是一个独立的 UseSystem 持久化标记或持续监听系统变化的订阅。某文化因当前线程/系统项出现在下拉框中，不代表主程序及每个插件都有该文化的卫星资源。

## 配置值与启动应用

`LanguageConfig.UICulture` 的 setter 仅保存私有字段；不更改线程文化、不广播变化、不写文件也不重启。getter 每次按 GetDefaultLanguages 判断该字段是否在当前资源候选中，匹配不到就返回 InstalledUICulture.Name，**不回写或清除原字段**。

因此，旧配置里写了 ja，但当前资源扫描没有 ja 时，读取会得到系统文化；若系统文化本来就是 ja，返回值仍可能是 ja。残留卫星 DLL 也可能使扫描继续接受旧文化。不能将回退说成必定切换到简体中文，或声称旧配置已被迁移改写。

`ColorVision/App.xaml.cs` 在配置、日志和主题初始化之后，将读取到的 UICulture 设到启动线程的 CurrentUICulture；替换旧实例后重载配置的分支会再次应用。这个赋值没有设置 CurrentCulture 或 DefaultThreadCurrentUICulture，也不是逐个设置各模块的 `Properties.Resources.Culture`。日期、数值格式及其它线程/显式资源文化应分别查实际调用点。

## 设置选择与重启副作用

`LanguagePropertiesEditor` 为 UICulture 建立 TwoWay、PropertyChanged 更新的 ComboBox 绑定。SelectionChanged 再调用 `LanguageManager.Current.LanguageChange(str)`，不是在普通设置关窗时才决定是否切换。

| 阶段 | 实际结果与限制 |
| --- | --- |
| 与当前线程文化字符串相同 | LanguageChange 返回 false，不弹提示或重启；false 不唯一代表用户取消 |
| 选择不同语言但取消确认 | LanguageChange 返回 false；编辑器将目标属性写回创建编辑器时记录的线程文化，不回滚其它设置，也不能撤销之前已保存的修改 |
| 确认 | 先创建 CultureInfo 并改当前线程文化，再写 LanguageConfig，调用 `ConfigService.SaveConfigs()`；保存的是配置服务管理的配置集合，不仅语言字段 |
| 保存返回之后 | `Process.Start(Application.ResourceAssembly.Location.Replace(".dll", ".exe"), "-r")`，然后 `Application.Current.Shutdown()`，最后返回 true |

这个入口会写配置、启动进程和关闭当前应用，不能作为只读验证或无害预览。它没有等待新进程启动完成、校验新窗口语言或恢复旧进程的握手。任何步骤抛异常时，LanguageChange 本身没有捕获或回滚已经发生的线程/内存/文件变化；true 也不证明新进程已就绪。文件保存的准确失败语义归[配置持久化](./configuration.md)。

LanguageChange 本身不要求 lang 先存在于下拉框，只直接构造 CultureInfo；直接调用者不能把它当成资源完整性校验。也不要用直接赋值 UICulture 绕过确认后再宣称已完成语言切换。

## 为什么旧窗口不会统一更新

当前没有 LanguageChanged 广播或统一窗口重建协议。不同显示路径在各自取值时读取资源：

- `SettingWindow.xaml` 等 XAML 使用 `x:Static Properties.Resources.*`；其现有文本不是绑定到 LanguageConfig 的实时翻译表达式。
- `SearchSettingsWindow` 在构造时调用 ApplyLocalization，直接给标题和控件 Content 赋字符串；此处没有订阅语言配置变更。
- `PropertyEditorHelper.CreateLabel` 在生成标签时写入 Text/ToolTip；字符串缓存按 ResourceManager、文化名和 key 分开，新文化下再次查询与已有 TextBlock 自动更新是两件事。
- 强类型 `Resources.Designer.cs` 的 Culture 是该资源类独立的静态覆盖值；LanguageManager 不统一清空这些覆盖，也不重写硬编码字符串。

因此，新增翻译应跟随拥有该文字的模块资源和实际消费入口，保留资源键/代码符号；不能只改 LanguageConfig 就承诺已打开窗口、插件内容和所有后台格式同步切换。

## 添加或完善界面语言

1. 在拥有该文字的模块中维护 `Properties/Resources.<culture>.resx`，沿用中性资源的键与格式占位符；语言显示名称由 `ColorVision.UI` 资源中的文化名键提供。只补显示名称不会生成其它模块的翻译。
2. 核对主程序与目标模块构建后的卫星资源及部署目录。默认语言发现查主程序名对应的资源 DLL，仅有某个插件的翻译文件不保证它进入可选列表。
3. 在隔离配置中启动新的应用实例，再检查目标窗口、属性标签和格式化文本；确认“跟随系统”、资源缺项及显式资源 Culture 的回退结果。语言切换会保存配置并关闭当前应用，先处理未保存工作。

翻译只改变显示文本，不改枚举值、序列化字段、协议名称或资源键。历史翻译可从 Git 查询后按当前键集合核对；不要直接用旧资源覆盖当前文件。

## Copilot 入口与验证缺口

`CopilotAgentCapabilityServices.SetLanguageAsync` 解析当前可用语言，在 UI dispatcher 上调用同一 LanguageChange，仍需用户确认和重启；已是目标文化时返回无需修改，未确认时返回未完成。工具审批、取消与恢复说明归[工具契约](../../02-developer-guide/core-concepts/copilot-agent-tool-contracts.md)，不能因为通过 AI 调用而略过应用自身确认或把工具返回当作重启验收。

`EngineUiLocalizationTests` 和 `FlowLocalizationTests` 先设置英语文化，再查询资源或创建控件，覆盖部分标签/格式化文本及显示翻译不改变枚举、序列化值的约束。它们不是先打开旧窗口再切文化的热更新测试，列出测试路径也不表示本次已运行。

目前未找到语言目录发现、getter 回退、取消恢复或实际重启的专项测试；Copilot 输入/审批测试同样不等于语言已切换。后续验收需在获授权、无未保存工作且使用隔离配置的环境下检查资源部署、取消、保存失败及新进程界面。

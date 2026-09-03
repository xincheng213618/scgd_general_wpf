---
knowledge_id: "algorithms.template-menus"
knowledge_type: "topic"
status: "current"
summary: "从模板菜单、算法面板或应用搜索打开模板；说明选择索引、流程设计器直达和菜单发现的边界。"
aliases: ["模板编辑入口在哪里","MenuItemTemplateBase","DisplayAlgorithmTemplateSelection","ShowTemplateWindow","模板搜索入口","模板菜单没有算法","搜索模板打开流程设计器"]
code_paths: ["Engine/ColorVision.Engine/Templates/Menus/MenuTemplate.cs","Engine/ColorVision.Engine/Templates/Menus/MenuItemTemplateBase.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/DisplayAlgorithmConfiguration.cs","UI/ColorVision.UI/Menus/MenuManager.cs","Engine/ColorVision.Engine/Templates/POI/MenuItemPoiParam.cs","Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs","Engine/ColorVision.Engine/Templates/TemplateSearchProvider.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/DisplayAlgorithmManager.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/DisplayAlgorithmControl.xaml.cs","ColorVision/MainWindow.Hotkeys.cs"]
test_paths: ["Test/ColorVision.UI.Tests/MenuDiscoveryExclusionTests.cs","Test/ColorVision.UI.Tests/TemplateSearchProviderTests.cs"]
related: ["algorithms.index","algorithms.template-management","ui.discovery","ui.menus","ui.search"]
---

# 模板编辑入口与菜单契约

模板能被 `TemplateControl` 发现，不代表每个模板都有独立菜单类。当前存在主菜单、通用算法配置内的编辑命令和模板搜索三类入口，排查时先辨认宿主。

## 如何打开模板

按当前工作选择入口；下面是中文界面的默认名称，菜单可见性还受菜单设置与宿主装配影响。

| 要做的事 | 操作 |
| --- | --- |
| 管理 POI 或流程模板 | 打开 **模板 → POI模板** 或 **模板 → 流程**，在模板窗口选择条目 |
| 编辑当前手动算法使用的模板 | 进入算法设备的手动面板，选择算法及模板，再使用该模板旁的编辑命令 |
| 按已知名称找模板 | 默认按 **Ctrl+Shift+P** 打开[应用搜索](../../ui-components/search.md)，输入模板名称并执行匹配项；自定义快捷键以当前配置为准 |

主菜单列出具体注册的菜单项；算法模板也可通过后两种入口访问。打开编辑器之后的创建、保存、导入导出与关闭语义见[模板编辑宿主](./template-management.md)。

## 入口路由

| 入口 | 当前源码 | 行为 |
| --- | --- | --- |
| 模板主菜单 | `Templates/Menus/MenuTemplate.cs` | `Header = Resources.MenuTemplate`，`Order = 2` |
| 通用模板菜单基类 | `Templates/Menus/MenuItemTemplateBase.cs` | 默认 `OwnerGuid = nameof(MenuTemplate)`；`Execute` 调用 `ShowTemplateWindow` |
| POI 菜单 | `Templates/POI/MenuItemPoiParam.cs` | `MenuItemPoiParam : MenuItemTemplateBase`，提供 `TemplatePoi` |
| Flow 菜单 | `Templates/Flow/TemplateFlow.cs` 中 `MenuTemplateFlow` | 挂在 `MenuTemplate`，以 `ShowDialog` 打开 `TemplateEditorWindow(new TemplateFlow())` |
| 算法配置中的模板编辑 | `Services/Devices/Algorithm/DisplayAlgorithmConfiguration.cs` | `DisplayAlgorithmTemplateSelection.EditCommand` |
| 按名称找模板 | `Templates/TemplateSearchProvider.cs` | 重新定位当前模板及条目，打开编辑器或模板定义的专用入口 |

以上源码路径相对 `Engine/ColorVision.Engine/`。`Templates/Menus/` 提供主菜单与通用基类；具体模板的入口可位于其自身目录或通用算法配置中。

## 菜单契约

`MenuItemTemplateBase.Template` 是由具体菜单提供的 `ITemplate`。`ShowTemplateWindow` 默认创建 `TemplateEditorWindow(Template)`，设置当前活动窗口为 Owner、居中并调用非模态 `Show()`。Flow 入口显式采用 `ShowDialog()`，不要把所有模板窗口统称为模态。

菜单发现与层级组装由 `UI/ColorVision.UI/Menus/MenuManager.cs` 处理；按[菜单契约](../../ui-components/menus.md)检查类型缓存、目标窗口、父子树与显示过滤。接口或模板类存在本身不保证菜单可见。

## 通用手动算法宿主

`DisplayAlgorithmManager` 查找带 `DisplayAlgorithmAttribute` 的 `IDisplayAlgorithm` 实现；`DisplayAlgorithmControl` 根据算法 `Configuration` 生成界面，执行按钮调用 `Execute()`；请求后的结果处理见[结果链](../../engine-components/result-handoff-chain.md)。

`DisplayAlgorithmTemplateSelection` 保存模板与条目来源，提供 `SelectedIndex`、`SelectedValue`、`SelectedName`、`IsSelectionValid`、`TryGetValue<T>`。它的 `EditCommand` 用 `SelectedIndex + editorIndexOffset` 打开 `TemplateEditorWindow`；空项或特殊条目来源需检查索引偏移，不能照搬另一个算法的选中索引。

新增手动算法的模板编辑能力时优先复用这个选择对象和配置宿主；只有确有独立菜单需求时才新增具体菜单类。参数属性编辑契约见 [PropertyGrid](../../ui-components/property-grid.md)。

## 模板搜索的打开行为

`TemplateSearchProvider` 从 `TemplateControl.ITemplateNames` 枚举已注册模板及非空条目名称。标题是条目名，说明取模板的 `Title`（空时用 `Name`），别名包含注册键、模板编码和类型名；同一模板内的名称忽略大小写去重，不同注册下同名条目的身份仍不同。

执行时按注册键重新解析当前模板，检查名称仍存在并重新取得索引：

- `IsSideHide = false`：打开定位到该索引的 `TemplateEditorWindow`。
- `IsSideHide = true`：调用模板的 `PreviewMouseDoubleClick(index)`。例如 `TemplateFlow` 直接打开 `FlowEngineToolWindow` 流程设计器。

模板注册或名称已经失效时，旧结果不可执行；它不会重新创建被移除的模板。搜索目录刷新与“模板”类型开关由[应用搜索](../../ui-components/search.md)管理。

## 排查与验证

| 现象 | 检查 |
| --- | --- |
| 模板存在但菜单没有条目 | 是否确实定义了独立菜单；是否应从算法配置或搜索进入 |
| 菜单层级或顺序异常 | `OwnerGuid`、同级 `Order`、菜单发现过滤 |
| 编辑按钮打开错条目 | 实际 `ItemsSource`、`SelectedIndex`、`editorIndexOffset` |
| 搜索打开流程设计器而非模板列表 | `IsSideHide` 及对应 `PreviewMouseDoubleClick`，这是流程模板的专用入口 |
| 搜索结果缺失或旧结果无法执行 | 当前模板注册与名称、搜索类型开关，以及目录是否已刷新 |
| 窗口能打开但不能保存 | 继续检查目标 `ITemplate` 的保存/导入导出，不归菜单层 |

`MenuDiscoveryExclusionTests` 检查指定菜单类型不存在、两个保留类型通过候选谓词，以及 MySQL 工具的 Owner/Order；没有实际装配模板菜单树。`TemplateSearchProviderTests` 用内存模板验证同名条目身份稳定、替换注册后使用新实例、移除注册后旧命令失效。

这些测试不构造真实模板编辑窗口，也不验证数据库保存。窗口能打开、选择正确和保存成功应分别核对；实际保存行为属于目标 `ITemplate`。

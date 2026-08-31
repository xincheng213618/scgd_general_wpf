---
knowledge_id: "algorithms.template-menus"
knowledge_type: "topic"
status: "current"
summary: "区分现存模板主菜单、专用入口与通用算法配置中的模板编辑命令。"
aliases: ["模板编辑入口在哪里","MenuItemTemplateBase","DisplayAlgorithmTemplateSelection","ShowTemplateWindow"]
code_paths: ["Engine/ColorVision.Engine/Templates/Menus/MenuTemplate.cs","Engine/ColorVision.Engine/Templates/Menus/MenuItemTemplateBase.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/DisplayAlgorithmConfiguration.cs","UI/ColorVision.UI/Menus/MenuManager.cs","Engine/ColorVision.Engine/Templates/POI/MenuItemPoiParam.cs","Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs"]
test_paths: ["Test/ColorVision.UI.Tests/MenuDiscoveryExclusionTests.cs"]
related: ["algorithms.index","algorithms.template-management","ui.discovery","ui.menus"]
---

# 模板编辑入口与菜单契约

模板能被 `TemplateControl` 发现，不代表每个模板都有独立菜单类。当前存在主菜单、通用算法配置内的编辑命令和模板搜索三类入口，排查时先辨认宿主。

## 入口路由

| 入口 | 当前源码 | 行为 |
| --- | --- | --- |
| 模板主菜单 | `Templates/Menus/MenuTemplate.cs` | `Header = Resources.MenuTemplate`，`Order = 2` |
| 通用模板菜单基类 | `Templates/Menus/MenuItemTemplateBase.cs` | 默认 `OwnerGuid = nameof(MenuTemplate)`；`Execute` 调用 `ShowTemplateWindow` |
| POI 菜单 | `Templates/POI/MenuItemPoiParam.cs` | `MenuItemPoiParam : MenuItemTemplateBase`，提供 `TemplatePoi` |
| Flow 菜单 | `Templates/Flow/TemplateFlow.cs` 中 `MenuTemplateFlow` | 挂在 `MenuTemplate`，以 `ShowDialog` 打开 `TemplateEditorWindow(new TemplateFlow())` |
| 算法配置中的模板编辑 | `Services/Devices/Algorithm/DisplayAlgorithmConfiguration.cs` | `DisplayAlgorithmTemplateSelection.EditCommand` |
| 按名称找模板 | `TemplateSearchProvider` | 定位模板及条目，进入模板编辑器 |

以上路径相对 `Engine/ColorVision.Engine/`。`Templates/Menus/` 当前只有主菜单与通用基类；旧 `MenuITemplateAlgorithm`、`MenuITemplateAlgorithmBase`、`ExportFocusPoints`、`ExportRoi`、`ExportMenuItemMatching` 不再是可新增依赖的类型。

## 菜单契约

`MenuItemTemplateBase.Template` 是由具体菜单提供的 `ITemplate`。`ShowTemplateWindow` 默认创建 `TemplateEditorWindow(Template)`，设置当前活动窗口为 Owner、居中并调用非模态 `Show()`。Flow 入口显式采用 `ShowDialog()`，不要把所有模板窗口统称为模态。

菜单发现与层级组装由 `UI/ColorVision.UI/Menus/MenuManager.cs` 处理；按[菜单契约](../../ui-components/menus.md)检查类型缓存、目标窗口、父子树与显示过滤。接口或模板类存在本身不保证菜单可见。

## 通用手动算法宿主

`DisplayAlgorithmManager` 查找带 `DisplayAlgorithmAttribute` 的 `IDisplayAlgorithm` 实现；`DisplayAlgorithmControl` 根据算法 `Configuration` 生成界面，执行按钮调用 `Execute()`。它不是 [历史结果 handler 注册器](../../engine-components/result-handoff-chain.md)。

`DisplayAlgorithmTemplateSelection` 保存模板与条目来源，提供 `SelectedIndex`、`SelectedValue`、`SelectedName`、`IsSelectionValid`、`TryGetValue<T>`。它的 `EditCommand` 用 `SelectedIndex + editorIndexOffset` 打开 `TemplateEditorWindow`；空项或特殊条目来源需检查索引偏移，不能照搬另一个算法的选中索引。

新增手动算法的模板编辑能力时优先复用这个选择对象和配置宿主；只有确有独立菜单需求时才新增具体菜单类。参数属性编辑契约见 [PropertyGrid](../../ui-components/property-grid.md)。

## 排查与验证

| 现象 | 检查 |
| --- | --- |
| 模板存在但菜单没有条目 | 是否确实定义了独立菜单；是否应从算法配置或搜索进入 |
| 菜单层级或顺序异常 | `OwnerGuid`、同级 `Order`、菜单发现过滤 |
| 编辑按钮打开错条目 | 实际 `ItemsSource`、`SelectedIndex`、`editorIndexOffset` |
| 窗口能打开但不能保存 | 继续检查目标 `ITemplate` 的保存/导入导出，不归菜单层 |

验证窗口可构造、选择与保存是不同检查；发现测试通过不能证明具体模板的持久化成功。

## 验证入口与缺口

关联测试：`Test/ColorVision.UI.Tests/MenuDiscoveryExclusionTests.cs`。

菜单排除测试只断言指定旧类型不存在、两个保留类型通过候选谓词，以及 MySQL 工具的 Owner/Order；没有实际装配菜单树。具体模板入口仍需验证 OwnerGuid、窗口可构造以及相应模板的保存能力。

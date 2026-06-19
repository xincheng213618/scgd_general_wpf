# 模板菜单入口

`Templates/Menus/` 不是业务算法模板，而是模板功能在主菜单里的分组骨架。它决定“模板”菜单下哪些子菜单存在、算法模板菜单挂在哪里、点击菜单时打开哪个模板编辑窗口。

## 先看结论

- 顶层菜单：`MenuTemplate`
- 算法模板分组：`MenuITemplateAlgorithm`
- 通用菜单基类：`MenuItemTemplateBase`
- 算法模板菜单基类：`MenuITemplateAlgorithmBase`
- 默认行为：点击菜单后打开 `TemplateEditorWindow(Template)`

如果新增模板后“模板管理里能加载，但菜单里看不到”，先查这个菜单体系的 `OwnerGuid`、`Order`、`Header` 和 concrete menu class 是否被装配。

## 源码入口

| 文件 | 作用 |
| --- | --- |
| `Engine/ColorVision.Engine/Templates/Menus/MenuTemplate.cs` | 顶层“模板”菜单，`Order = 2` |
| `Engine/ColorVision.Engine/Templates/Menus/MenuItemTemplateBase.cs` | 普通模板菜单项基类，默认打开模板编辑窗口 |
| `Engine/ColorVision.Engine/Templates/Menus/MenuITemplateAlgorithm.cs` | “算法”模板分组和算法模板菜单基类 |
| `Engine/ColorVision.Engine/Templates/FocusPoints/ExportFocusPoints.cs` | 具体菜单示例，打开 `TemplateFocusPoints` |
| `Engine/ColorVision.Engine/Templates/FindLightArea/ExportRoi.cs` | 具体菜单示例，打开 ROI 模板 |
| `Engine/ColorVision.Engine/Templates/Matching/ExportMenuItemMatching.cs` | 具体菜单示例，打开 Matching 模板 |
| `Engine/ColorVision.Engine/Templates/SysDictionary/MenuDefalutDicAlg.cs` | 具体菜单示例，打开系统字典模板 |

## 菜单层级

当前层级可以理解为：

```text
MenuTemplate
  -> MenuITemplateAlgorithm
       -> MenuITemplateAlgorithmBase 派生菜单
```

`MenuTemplate` 继承 `MenuItemBase`：

| 属性 | 当前值 |
| --- | --- |
| `Header` | `Resources.MenuTemplate` |
| `Order` | `2` |

`MenuITemplateAlgorithm` 也继承 `MenuItemBase`：

| 属性 | 当前值 |
| --- | --- |
| `OwnerGuid` | `nameof(MenuTemplate)` |
| `Header` | `Properties.Resources.MenuAlgorithm` |
| `Order` | `3` |

`MenuITemplateAlgorithmBase` 再把 `OwnerGuid` 改成 `nameof(MenuITemplateAlgorithm)`，让具体算法模板挂到“模板 -> 算法”下面。

## 通用菜单行为

`MenuItemTemplateBase` 是最重要的基类：

| 成员 | 行为 |
| --- | --- |
| `OwnerGuid` | 默认挂在 `MenuTemplate` 下 |
| `Execute()` | 调用 `ShowTemplateWindow()` |
| `Template` | 抽象属性，由具体菜单返回 `new TemplateXxx()` |
| `ShowTemplateWindow()` | 默认 `new TemplateEditorWindow(Template).Show()` |

这意味着大多数模板菜单不需要自己写打开窗口逻辑，只需要：

```csharp
public override string Header => "MyTemplate";
public override ITemplate Template => new TemplateMyTemplate();
```

如果模板需要特殊窗口、模态打开、前置校验或先选项目，才应该覆盖 `ShowTemplateWindow()`。

## 具体菜单示例

| 菜单类 | 父级 | Header | Template |
| --- | --- | --- | --- |
| `ExportFocusPoints` | `MenuITemplateAlgorithm` | `FocusPoints` | `TemplateFocusPoints` |
| `ExportRoi` | `MenuITemplateAlgorithm` | ROI 相关资源/文本 | `TemplateRoi` |
| `ExportMenuItemMatching` | `MenuITemplateAlgorithm` | `TemplateMatching` 资源 | `TemplateMatch` |
| `MenuDefalutDicAlg` | `MenuITemplateAlgorithm` | `EditDefaultAlgorithmDictionary` 资源 | `TemplateModParam` |
| `MenuGhost2` | `MenuITemplateAlgorithm` | Ghost2 相关菜单 | `TemplateGhostQK` |
| `MenuLEDStripDetectionV2` | `MenuITemplateAlgorithm` | LEDStripDetectionV2 相关菜单 | `TemplateLEDStripDetectionV2` |

这些菜单项的业务逻辑仍在各自模板目录里，`Menus/` 只提供分组和打开方式。

## 新增模板菜单时怎么做

1. 确认模板类已经实现 `ITemplate<T>` 或对应 JSON 模板接口。
2. 如果是算法模板，新增菜单类继承 `MenuITemplateAlgorithmBase`。
3. 设置稳定的 `Order`，避免菜单位置随机变化。
4. `Header` 优先使用资源文本，临时英文/源码名要在交接文档里写清。
5. `Template` 返回新的模板实例，不要复用带状态的单例。
6. 运行应用验证菜单可见，点击后能打开 `TemplateEditorWindow`。
7. 创建、保存、复制、导入导出至少验证一个核心动作。

## 常见问题

| 现象 | 优先检查 |
| --- | --- |
| 菜单完全看不到 | `OwnerGuid` 是否指向正确父级，菜单类是否被程序集扫描 |
| 菜单位置不对 | `Order` 是否和同级菜单冲突 |
| 点击没反应 | `Template` 是否返回非空实例，`Execute()` 是否被覆盖后没有调用窗口 |
| 打开了错误模板 | concrete menu class 的 `Template` 属性 |
| 语言显示不对 | `Header` 是否硬编码，资源 key 是否存在 |

## 交接重点

- `Menus/` 是入口组织层，不是算法执行层。
- 修改 `OwnerGuid` 会直接改变菜单可见路径，可能让现场人员找不到模板入口。
- `MenuItemTemplateBase.ShowTemplateWindow()` 默认用非模态 `Show()`，个别模板若需要 `ShowDialog()` 要单独覆盖。
- 具体模板是否能保存、导入、导出，仍由 `ITemplate` 实现决定，不由菜单类决定。
- 源码里有历史拼写，例如 `MenuDefalutDicAlg`，文档和排查时要按现有类名搜索。

## 继续阅读

- [模板管理](./template-management.md)
- [Templates API 参考](./api-reference.md)
- [插件开发手册](../../../02-developer-guide/plugin-development/README.md)
- [扩展点概览](../../extensions/README.md)
- [当前算法模板覆盖清单](../current-algorithm-template-coverage.md)

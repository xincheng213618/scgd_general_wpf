# 模板菜单入口

`Templates/Menus/` 不是业务算法模板，而是模板功能在主菜单里的分组骨架。它决定“模板”菜单下哪些子菜单存在、算法模板菜单挂在哪里、点击菜单时打开哪个模板编辑窗口。

## 速查

| 项 | 值 |
| --- | --- |
| 顶层菜单 | `MenuTemplate` |
| 算法模板分组 | `MenuITemplateAlgorithm` |
| 通用菜单基类 | `MenuItemTemplateBase` |
| 算法模板菜单基类 | `MenuITemplateAlgorithmBase` |
| 默认行为 | 点击菜单后打开 `TemplateEditorWindow(Template)` |

新增模板后“模板管理里能加载，但菜单里看不到”时，先查 `OwnerGuid`、`Order`、`Header` 和 concrete menu class 是否被装配。

## 源码和层级

| 文件 | 作用 |
| --- | --- |
| `MenuTemplate.cs` | 顶层“模板”菜单，`Order = 2` |
| `MenuItemTemplateBase.cs` | 普通模板菜单项基类，默认打开模板编辑窗口 |
| `MenuITemplateAlgorithm.cs` | “算法”模板分组和算法模板菜单基类 |
| `ExportFocusPoints.cs`、`ExportRoi.cs`、`ExportMenuItemMatching.cs`、`MenuDefalutDicAlg.cs` | 具体菜单示例 |

当前层级：

```text
MenuTemplate
  -> MenuITemplateAlgorithm
       -> MenuITemplateAlgorithmBase 派生菜单
```

| 类 | 关键属性 |
| --- | --- |
| `MenuTemplate` | `Header = Resources.MenuTemplate`，`Order = 2` |
| `MenuITemplateAlgorithm` | `OwnerGuid = nameof(MenuTemplate)`，`Header = Properties.Resources.MenuAlgorithm`，`Order = 3` |
| `MenuITemplateAlgorithmBase` | `OwnerGuid = nameof(MenuITemplateAlgorithm)`，具体算法模板挂到“模板 -> 算法”下 |

## 通用行为

| 成员 | 行为 |
| --- | --- |
| `OwnerGuid` | 默认挂在 `MenuTemplate` 下 |
| `Execute()` | 调用 `ShowTemplateWindow()` |
| `Template` | 抽象属性，由具体菜单返回 `new TemplateXxx()` |
| `ShowTemplateWindow()` | 默认 `new TemplateEditorWindow(Template).Show()` |

大多数模板菜单只需要提供 `Header` 和新的 `Template` 实例。只有需要特殊窗口、模态打开、前置校验或先选项目时，才覆盖 `ShowTemplateWindow()`。

## 具体菜单示例

| 菜单类 | 父级 | Template |
| --- | --- | --- |
| `ExportFocusPoints` | `MenuITemplateAlgorithm` | `TemplateFocusPoints` |
| `ExportRoi` | `MenuITemplateAlgorithm` | `TemplateRoi` |
| `ExportMenuItemMatching` | `MenuITemplateAlgorithm` | `TemplateMatch` |
| `MenuDefalutDicAlg` | `MenuITemplateAlgorithm` | `TemplateModParam` |
| `MenuGhost2` | `MenuITemplateAlgorithm` | `TemplateGhostQK` |
| `MenuLEDStripDetectionV2` | `MenuITemplateAlgorithm` | `TemplateLEDStripDetectionV2` |

业务逻辑仍在各自模板目录里，`Menus/` 只提供分组和打开方式。

## 新增和排查

| 场景 | 要点 |
| --- | --- |
| 新增菜单 | 确认模板实现 `ITemplate<T>` 或 JSON 模板接口；算法模板继承 `MenuITemplateAlgorithmBase`；设置稳定 `Order`；`Header` 优先用资源文本；`Template` 返回新实例 |
| 验收 | 菜单可见，点击能打开 `TemplateEditorWindow`，创建/保存/复制/导入导出至少验证一个核心动作 |
| 菜单看不到 | `OwnerGuid` 是否正确，菜单类是否被程序集扫描 |
| 菜单位置不对 | `Order` 是否和同级菜单冲突 |
| 点击没反应 | `Template` 是否非空，`Execute()` 是否被覆盖后没调用窗口 |
| 打开错误模板 | concrete menu class 的 `Template` 属性 |
| 语言显示不对 | `Header` 是否硬编码，资源 key 是否存在 |

## 维护重点

- `Menus/` 是入口组织层，不是算法执行层。
- 修改 `OwnerGuid` 会改变菜单可见路径。
- `ShowTemplateWindow()` 默认用非模态 `Show()`；需要 `ShowDialog()` 时单独覆盖。
- 具体模板是否能保存、导入、导出，仍由 `ITemplate` 实现决定。
- 源码里有历史拼写，例如 `MenuDefalutDicAlg`，排查时按现有类名搜索。

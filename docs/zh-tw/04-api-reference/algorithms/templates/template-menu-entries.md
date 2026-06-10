# 模板選單入口

`Templates/Menus/` 是模板功能在主選單中的分組骨架，不是演算法執行模組。它決定模板選單下有哪些分組、點擊後開啟哪個 `TemplateEditorWindow`。

## 快速定位

| 項目 | 類別 |
| --- | --- |
| 頂層選單 | `MenuTemplate` |
| 演算法模板分組 | `MenuITemplateAlgorithm` |
| 通用模板選單基類 | `MenuItemTemplateBase` |
| 演算法模板選單基類 | `MenuITemplateAlgorithmBase` |
| 預設動作 | `new TemplateEditorWindow(Template).Show()` |

## 層級

```text
MenuTemplate
  -> MenuITemplateAlgorithm
       -> MenuITemplateAlgorithmBase 派生選單
```

`MenuItemTemplateBase` 預設掛在 `MenuTemplate` 下，`Execute()` 會呼叫 `ShowTemplateWindow()`，而 `Template` 由具體選單類返回。

## 交接重點

- `Menus/` 只組織入口，不執行演算法。
- 修改 `OwnerGuid` 會改變選單位置，也可能讓模板入口消失。
- 預設視窗是非模態 `Show()`；需要特殊流程時才覆寫 `ShowTemplateWindow()`。
- 保存、導入、導出能力仍由具體 `ITemplate` 實現決定。
- 搜尋時要使用現有類名，例如 `MenuDefalutDicAlg`。

## 相關頁面

- [模板管理](./template-management.md)
- [Templates API 參考](./api-reference.md)
- [外掛開發手冊](../../../02-developer-guide/plugin-development/README.md)
- [擴充套件點概覽](../../extensions/README.md)

# SysDictionary 系統字典模板

本頁說明 `Engine/ColorVision.Engine/Templates/SysDictionary/` 的職責。它維護演算法模板預設字典，核心資料在 `t_scgd_sys_dictionary_mod_master` 和 `t_scgd_sys_dictionary_mod_item`，目前 `TemplateModParam` 只載入 `mod_type = 7`。

## 適用範圍

| 事項 | 目前實作 |
| --- | --- |
| 模板類別 | `TemplateModParam : ITemplate<DicModParam>` |
| 參數類別 | `DicModParam : ParamModBase` |
| 編輯控制項 | `EditDictionaryMode.xaml(.cs)` |
| 建立主檔視窗 | `CreateDicTemplate.xaml(.cs)` |
| 建立明細視窗 | `CreateDicModeDetail.xaml(.cs)` |
| 選單入口 | `MenuDefalutDicAlg` |
| 主表 | `t_scgd_sys_dictionary_mod_master` |
| 明細表 | `t_scgd_sys_dictionary_mod_item` |
| 目前範圍 | `tenant_id = 0`、`mod_type = 7` |

## 資料模型

`SysDictionaryModModel` 儲存字典主檔，包括 `code`、`name`、`pid`、`p_type`、`mod_type`、`cfg_json`、`version`、`is_enable`、`is_delete` 和 `tenant_id`。

`SysDictionaryModDetaiModel` 儲存字典明細，包括 `pid`、`address_code`、`symbol`、`name`、`default_val`、`val_type`、`is_enable` 和 `is_delete`。`val_type` 可為 `Integer`、`Float`、`Bool`、`String`、`Enum`。

## 生命週期

1. `MenuDefalutDicAlg` 開啟 `TemplateEditorWindow(new TemplateModParam())`。
2. `TemplateModParam.Load()` 讀取 `tenant_id = 0`、`mod_type = 7` 的主檔。
3. 明細透過 `SysDictionaryModDetailDao.GetAllByPid(model.Id)` 載入。
4. `EditDictionaryMode` 顯示明細並可編輯預設值和啟用狀態。
5. `CreateDicTemplate` 建立 `ModType = 7` 的主檔。
6. `CreateDicModeDetail` 建立明細，預設 `ValueType = String`、`IsEnable = true`。
7. `Save()` 儲存明細。

目前 `Save()` 只儲存明細，不儲存主檔欄位。刪除路徑目前呼叫 `SysResourceModel`；若刪除沒有作用，先核對 DAO 和資料表。

## 關係

| 模組 | 關係 |
| --- | --- |
| 強型別模板 | 透過 `TemplateDicId` 讀取預設字典明細。 |
| JSON 模板 | 多數 JSON 模板主檔也是 `mod_type = 7`，內容常在 `cfg_json`。 |
| Flow 模板 | Flow 會讀取字典明細來構造節點或模板參數。 |
| Validate | Validate 使用 `mod_type = 110/111/120`，不要與此處混淆。 |

## 常見排查

| 現象 | 優先排查 |
| --- | --- |
| 模板欄位不顯示 | `TemplateDicId` 和字典主檔是否存在。 |
| 新增欄位未生效 | 明細 `pid`、`symbol`、`address_code`、`is_enable`。 |
| 預設值不生效 | `default_val` 型別是否與 `val_type` 匹配。 |
| 選單沒有入口 | `MenuDefalutDicAlg` 掃描與權限。 |
| 刪除後仍可見 | 刪除表、快取和選單/模板刷新。 |

## 延伸閱讀

- [模板管理](./template-management.md)
- [Templates API 參考](./api-reference.md)
- [Validate 判定規則模板](./validate-rules.md)
- [Engine 模板與 Flow 鏈路](../../engine-components/template-flow-chain.md)
- [目前演算法模板覆蓋清單](../current-algorithm-template-coverage.md)

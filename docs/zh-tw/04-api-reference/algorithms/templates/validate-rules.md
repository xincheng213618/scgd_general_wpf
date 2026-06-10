# Validate 判定規則模板

本頁說明 `Engine/ColorVision.Engine/Templates/Validate/` 的兩層規則體系。Validate 不是單一模板，而是包含「預設合規字典」與「實際判定模板」兩層資料。BuzProduct、Compliance 和部分專案包都會依賴這套資料。

## 適用範圍

| 事項 | 目前實作 |
| --- | --- |
| 預設字典模板 | `TemplateDicComply : ITemplate<DicComplyParam>` |
| 實際判定模板 | `TemplateComplyParam : ITemplate<ValidateParam>` |
| 字典編輯控制項 | `DicEditComply.xaml(.cs)` |
| 規則編輯控制項 | `ValidateControl.xaml(.cs)` |
| 選單入口 | `ExportComply.cs`、`ExportDicComply.cs` |
| 判定模板主表 | `t_scgd_rule_validate_template_master` |
| 判定模板明細表 | `t_scgd_rule_validate_template_detail` |
| 執行期快取 | `TemplateComplyParam.CIEParams`、`TemplateComplyParam.JNDParams` |

## 兩層模型

`TemplateDicComply` 維護預設字典和預設規則項，資料來自 `SysDictionaryModMasterDao` 和 `SysDictionaryModItemValidateDao`。

| 字典 `mod_type` | 目前用途 |
| --- | --- |
| `110` | 點位類 CIE/合規判定選單。 |
| `111` | 點位列表類合規判定選單。 |
| `120` | JND 類合規判定選單。 |

`TemplateComplyParam(code, type)` 按字典 `Code` 載入實際判定模板。它讀取 `t_scgd_rule_validate_template_master`，再讀取 `t_scgd_rule_validate_template_detail`。

| 表 | 關鍵欄位 | 說明 |
| --- | --- | --- |
| `t_scgd_rule_validate_template_master` | `dic_pid`、`code`、`name`、`is_enable`、`is_delete`、`tenant_id` | 某個字典代碼下的一套判定模板。 |
| `t_scgd_rule_validate_template_detail` | `dic_pid`、`pid`、`code`、`val_max`、`val_min`、`val_equal`、`val_radix`、`val_type` | 具體判定項與閾值。 |

## 動態選單

| 來源 | 選單行為 |
| --- | --- |
| `mod_type = 110` | 開啟 `TemplateComplyParam(item.Code)`，作為點位類規則入口。 |
| `mod_type = 111` | 開啟 `TemplateComplyParam(item.Code)`，作為點位列表類規則入口。 |
| `mod_type = 120` | 開啟 `TemplateComplyParam(item.Code, 1)`，作為 JND 規則入口。 |
| `ExportDicComply` | 開啟 `TemplateDicComply`，維護預設合規字典。 |

## 建立與儲存

`TemplateDicComply.Create(templateCode, templateName)` 會建立 `SysDictionaryModModel`，目前預設 `ModType = 111`。

`TemplateComplyParam.Create(templateName)` 會建立主表資料，依 `Code` 找到字典，複製已啟用的字典驗證項，並將 `ValMax`、`ValMin`、`ValEqual`、`ValRadix`、`ValType` 寫入明細。

`TemplateComplyParam.Save()` 儲存實際模板名稱和明細規則。`TemplateDicComply.Save()` 儲存預設字典主檔和預設規則明細。

## 執行期快取

| 快取 | 說明 |
| --- | --- |
| `CIEParams` | CIE/一般合規判定模板集合，BuzProduct 會讀取它。 |
| `JNDParams` | JND 判定模板集合。 |

目前建構函式在 `type == 1` 時會加入 `JNDParams`，之後也會加入 `CIEParams`。交接時要按現有行為說明，不要假設 JND 模板只在 `JNDParams` 中。

## 匯入限制

`TemplateComplyParam.Import()` 目前提示不支援匯入。現場遷移時，應同時遷移字典表和 `t_scgd_rule_validate_template_*` 資料，或另行補充匯入流程。

## 與其他模組的關係

| 模組 | 依賴方式 |
| --- | --- |
| [BuzProduct 產品業務參數模板](./buz-product-template.md) | 明細 `val_rule_temp_id` 指向 Validate 模板。 |
| [Compliance 結果交接](./compliance-results.md) | 讀取 `ValidateResult`，按 `ValidateRuleResultType.M` 判斷。 |
| [JND 模板](./jnd-template.md) | JND 規則來自 `mod_type = 120`。 |
| 專案包 | 可能使用 Validate/Compliance 資料產生報表和 OK/NG。 |

## 常見排查

| 現象 | 優先排查 |
| --- | --- |
| 選單缺少規則入口 | `SysDictionaryModMaster` 的 `mod_type` 和 `is_delete = false`。 |
| 新建模板沒有明細 | 字典下已啟用的驗證項是否存在。 |
| BuzProduct 沒有規則可選 | `TemplateComplyParam.CIEParams` 是否已載入對應 `Code`。 |
| JND 規則出現在 CIE 列表 | 這是目前建構函式行為。 |
| 匯入不可用 | 實際判定模板目前不支援匯入。 |

## 交接清單

- 分開說明預設字典層和實際判定模板層。
- 新增判定欄位時，同步更新字典、明細、驗收樣例和結果說明。
- 修改閾值語義時，同步檢查服務寫回的 `ValidateResult`。
- 遷移時同時遷移 `SysDictionaryMod*` 和 `t_scgd_rule_validate_template_*`。
- 修改選單時驗證 `mod_type = 110/111/120` 三條路徑。

## 延伸閱讀

- [BuzProduct 產品業務參數模板](./buz-product-template.md)
- [Compliance 結果交接](./compliance-results.md)
- [模板管理](./template-management.md)
- [Engine 結果展示與專案交接鏈路](../../engine-components/result-handoff-chain.md)
- [目前演算法模板覆蓋清單](../current-algorithm-template-coverage.md)

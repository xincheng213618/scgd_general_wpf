# Compliance 結果交接

本頁說明 `Engine/ColorVision.Engine/Templates/Compliance/` 的結果模型與展示鏈路。這個目錄不是規則編輯器；它負責讀取合規結果、展示結果，並解讀 `ValidateResult`。

## 適用範圍

| 事項 | 目前實作 |
| --- | --- |
| Y 結果 | `ComplianceYModel`、`ComplianceYDao`、`ViewHandleComplianceY` |
| XYZ 結果 | `ComplianceXYZModel`、`ComplianceXYZDao`、`ViewHandleComplianceXYZ` |
| JND 結果 | `ComplianceJNDModel`、`ComplianceJNDDao`、`ViewHandleComplianceJND` |
| 判定來源 | `ValidateResult` JSON |
| 反序列化型別 | `ObservableCollection<ValidateRuleResult>` |
| 通過條件 | 每條規則 `Result == ValidateRuleResultType.M` |
| 執行入口 | `IResultHandleBase` handler |

## 結果類型映射

| Handler | 可處理結果類型 | 資料表 |
| --- | --- | --- |
| `ViewHandleComplianceY` | `Compliance_Contrast`、`Compliance_Math`、`Compliance_Contrast_CIE_Y`、`Compliance_Math_CIE_Y` | `t_scgd_algorithm_result_detail_compliance_y` |
| `ViewHandleComplianceXYZ` | `Compliance_Contrast_CIE_XYZ`、`Compliance_Math_CIE_XYZ` | `t_scgd_algorithm_result_detail_compliance_xyz` |
| `ViewHandleComplianceJND` | `Compliance_Math_JND` | `t_scgd_algorithm_result_detail_compliance_jnd` |

## 資料模型

`ComplianceYModel` 適合單值亮度或對比結果，欄位包括 `pid`、`name`、`data_type`、`data_value` 和 `validate_result`。

`ComplianceXYZModel` 儲存色彩與光學分量，例如 `data_value_x/y/z`、`data_value_u/v`、`data_value_cct`、`data_value_wave` 和 `validate_result`。

`ComplianceJNDModel` 儲存 `data_val_h`、`data_val_v` 和 `validate_result`。

## 判定邏輯

三類模型的 `Validate` 邏輯一致：

1. `ValidateResult` 為空時，結果為 `false`。
2. 非空 JSON 會反序列化為 `ObservableCollection<ValidateRuleResult>`。
3. 只有所有規則結果都等於 `ValidateRuleResultType.M` 才通過。
4. 任意規則不是 `M`，整體結果就是失敗。

Compliance 頁不重新計算閾值，而是解讀演算法服務或上游流程寫回的判定 JSON。

## 展示鏈路

1. 結果頁依 `ViewResultAlgType` 選擇 `ViewHandleCompliance*`。
2. 若 `ResultImagFile` 存在，handler 會先開啟影像。
3. handler 依主結果 `id` 查詢明細表。
4. 明細轉成 `IViewResult` 後綁定到 ListView。
5. 表格顯示名稱、數值、判定狀態和判定 JSON。

目前 `ViewHandleComplianceXYZ` 綁定了 `DataValue` 欄位，但模型主要暴露 `DataValuex/y/z/u/v/...` 分量欄位。若 XYZ 頁面數值為空，先檢查綁定和模型欄位是否對齊。

## 與其他模組的關係

| 模組 | 在判定鏈中的角色 |
| --- | --- |
| [Validate 判定規則模板](./validate-rules.md) | 定義字段、閾值和比較方式。 |
| [BuzProduct 產品業務參數模板](./buz-product-template.md) | 透過 `val_rule_temp_id` 選擇規則模板。 |
| Compliance 結果 | 讀取 `ValidateResult` 並展示通過/失敗。 |
| 專案包 | 可能再彙總或匯出 Compliance/JND/POI 結果。 |

## 常見排查

| 現象 | 優先排查 |
| --- | --- |
| 沒有明細 | 結果類型是否命中 handler，明細表是否有同 `pid` 資料。 |
| 影像未開啟 | `ResultImagFile` 是否存在，歸檔或遷移後路徑是否失效。 |
| `Validate` 失敗 | `validate_result` 是否為空，或是否存在非 `M` 規則。 |
| XYZ 數值為空 | ListView 綁定名稱與 `ComplianceXYZModel` 是否一致。 |
| 專案報表不一致 | 專案包是否又做了篩選、排序或彙總。 |

## 交接清單

- 說明 Compliance 是結果展示與解讀層，不是規則建立層。
- 新增結果類型時同步補 handler、DAO、資料表和文檔。
- 修改 `ValidateResult` JSON 時驗證 Y、XYZ、JND 三類模型。
- 現場驗收保留主結果、明細表、原圖路徑、Validate 模板和專案匯出檔。

## 延伸閱讀

- [Validate 判定規則模板](./validate-rules.md)
- [BuzProduct 產品業務參數模板](./buz-product-template.md)
- [JND 模板](./jnd-template.md)
- [Engine 結果展示與專案交接鏈路](../../engine-components/result-handoff-chain.md)
- [目前演算法模板覆蓋清單](../current-algorithm-template-coverage.md)

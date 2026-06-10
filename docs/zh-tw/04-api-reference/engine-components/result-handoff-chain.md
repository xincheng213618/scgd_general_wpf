# Engine 結果展示與專案交接鏈路

這一頁區分兩件事：Engine 如何把演算法結果顯示出來，Projects 如何把結果變成客戶要的判定和交付格式。

## 主鏈路

```text
AlgResultMasterModel / 明細 DAO
  -> ViewResultAlg / IViewResult
  -> ViewHandleXxx / IResultHandleBase / IDisplayAlgorithm
  -> UI/ColorVision.ImageEditor/Draw 覆蓋層
  -> Projects/<Project>/Process / Recipe / Fix
  -> ObjectiveTestResult / 匯出 / Socket / MES
```

## Engine 顯示責任

Engine 要保證結果可追溯、可查詢、可視化。通常要有主結果、明細結果、影像路徑、ROI 或座標資訊、結果類型和展示處理器。

常見程式碼位置：

- `Templates/**/ViewHandle*.cs`
- `Abstractions/IResultHandlers.cs`
- `ViewResultAlg`
- `AlgResultMasterModel`
- `UI/ColorVision.ImageEditor/Draw/**`

相關專題：

- [Compliance 結果交接](../algorithms/templates/compliance-results.md)：`ViewHandleComplianceY/XYZ/JND` 和 Y/XYZ/JND 判定結果。
- [Validate 判定規則模板](../algorithms/templates/validate-rules.md)：判定規則來源和 `ValidateResult` 解讀。
- [BuzProduct 產品業務參數模板](../algorithms/templates/buz-product-template.md)：產品明細如何透過 `val_rule_temp_id` 綁定規則。
- [Matching 模板匹配](../algorithms/templates/matching-template.md)：`ViewHandleMatching`、AOI 明細和四點 overlay。
- [ImageCropping 圖像裁剪模板](../algorithms/templates/image-cropping-template.md)：`ViewHandleImageCropping` 和裁剪文件明細。

## Projects 交付責任

專案包讀取 Engine 結果後，負責客戶規則：

- Recipe 參數。
- Fix 或補償規則。
- 結果欄位映射。
- `ObjectiveTestResult`。
- CSV、資料庫、Socket、MES 等輸出。

不要在圖像覆蓋層裡寫客戶判定，也不要在專案包裡臨時偽造 Engine 應該產生的結果。

## 排查結果缺失

| 現象 | 檢查順序 |
| --- | --- |
| UI 無覆蓋層 | DAO -> `ViewResultAlg` -> `CanHandle` -> 影像路徑 -> Draw 物件 |
| 覆蓋層位置錯 | 座標系、ROI、縮放、原圖尺寸 |
| 專案結果為空 | Engine 結果 key、`Process` 讀取邏輯、Recipe/Fix |
| Socket 回傳錯 | `ObjectiveTestResult`、協議欄位、錯誤碼映射 |
| 匯出缺欄位 | exporter、欄位映射、批次號、結果 ID |

## 新增結果類型清單

1. 定義結果模型和 DAO。
2. 保證演算法執行後落主結果和明細結果。
3. 新增 `IViewResult` 或展示模型。
4. 新增 `ViewHandleXxx` 並實作 `CanHandle`。
5. 補 ImageEditor Draw 覆蓋物件。
6. 若專案需要，補 `Projects/<Project>/Process` 和 `ObjectiveTestResult` 映射。
7. 更新本頁與對應專案文檔。

## 延伸閱讀

- [Compliance 結果交接](../algorithms/templates/compliance-results.md)
- [Validate 判定規則模板](../algorithms/templates/validate-rules.md)
- [BuzProduct 產品業務參數模板](../algorithms/templates/buz-product-template.md)

# ImageCropping 圖像裁剪模板

`ImageCropping/` 負責舊強型別圖像裁剪模板、手動演算法頁、Flow 節點和裁剪結果展示。它不同於 `Jsons/ImageROI`。

## 快速定位

| 項目 | 值 |
| --- | --- |
| 模板類 | `TemplateImageCropping` |
| 參數類 | `ImageCroppingParam` |
| `TemplateDicId` | `32` |
| Code | `ImageCropping` |
| 手動演算法 | `AlgorithmImageCropping` |
| MQTT 事件 | `Event_Image_Cropping` |
| Flow 算子 | `OLED.GetRIAand` |
| 結果類型 | `ViewResultAlgType.Image_Cropping` |
| 結果 handler | `ViewHandleImageCropping` |

## 參數與 ROI

`ImageCroppingParam` 目前只有兩個持久欄位：

| 欄位 | 含義 |
| --- | --- |
| `UnEgde` | 邊緣相關裁剪參數，拼寫沿用原始碼 |
| `O_Index` | 輸出順序/索引參數，恢復 SQL 預設為 `[0,1,2,3]` |

`Point1` 到 `Point4` 是 `AlgorithmImageCropping` 的執行期 ROI 點，手動執行時作為 `ROI` 陣列送出，不會保存進模板欄位。

## Flow 與結果

手動執行會送出 `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateParam` 和 `ROI`，事件為 `Event_Image_Cropping`。

Flow 有兩條相關路徑：

- 通用 `AlgorithmNode`：`AlgorithmType.圖像裁剪` 對應 `operatorCode = "OLED.GetRIAand"`。
- `OLEDImageCroppingNode`：`圖像裁剪2` 有 `IN_IMG` 和 `IN_ROI`，會把上游 ROI 主結果寫入 `ROI_MasterId`。

`ViewHandleImageCropping` 處理 `ViewResultAlgType.Image_Cropping`，透過 `AlgResultImageDao` 讀明細，表格欄位為 `file_name`、`order_index`、`FileInfo`。

## 交接重點

- 本頁描述強型別 `ImageCropping`，不是 JSON `ImageROI`。
- 手動四點 ROI 是執行期輸入，不是模板持久欄位。
- Flow 的雙輸入節點依賴上游 ROI 結果。
- `SideSave(...)` 目前同時把 `selectedPath` 當 CSV 路徑和圖片目錄使用，導出需現場驗證。

## 相關頁面

- [結果交接鏈路](../../engine-components/result-handoff-chain.md)
- [模板與 Flow 鏈路](../../engine-components/template-flow-chain.md)
- [ROI 原語](../primitives/roi.md)
- [JSON 模板](./json-templates.md)

# FocusPoints 關注點模板

`FocusPoints/` 是舊發光區/關注點檢測鏈路的參數模板。它保存二值化、濾波、形態學、面積/矩形過濾與 ROI 邊界，再交給手動演算法頁或 Flow 節點使用。

## 快速定位

| 項目 | 值 |
| --- | --- |
| 模板類 | `TemplateFocusPoints` |
| 參數類 | `FocusPointsParam` |
| `TemplateDicId` | `15` |
| Code | `focusPoints` |
| 手動演算法 | `AlgorithmFocusPoints` |
| MQTT 事件 | `Event_LightArea_GetData` |
| Flow 算子 | `FocusPoints` |
| 選單入口 | `ExportFocusPoints` |

## 參數分組

| 分組 | 欄位 | 交接含義 |
| --- | --- | --- |
| `Binarize` | `Binarize`、`BinarizeThresh` | 是否二值化與閾值 |
| `Blur` | `Blur`、`BlurSize` | 均值濾波開關與尺寸 |
| `Erode` | `Erode`、`ErodeSize` | 腐蝕開關與尺寸 |
| `Dilate` | `Dilate`、`DilateSize` | 膨脹開關與尺寸 |
| `Param` | `FilterRect`、`Width`、`Height` | 矩形過濾與寬高限制 |
| `FilterArea` | `FilterArea`、`MaxArea`、`MinArea` | 面積過濾與上下限 |
| `Roi` | `Roi`、`Left`、`Right`、`Top`、`Bottom` | ROI 邊界 |

這裡的 ROI 是模板輸入，不是結果 overlay 座標。結果點位與 POI 復用請看 [ROI 原語](../primitives/roi.md) 和 [POI 原語](../primitives/poi.md)。

## 執行鏈路

手動頁 `DisplayFocusPoints` 選擇模板、影像來源和批號後，由 `AlgorithmFocusPoints.SendCommand(...)` 發送 `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateParam`。事件名是 `MQTTAlgorithmEventEnum.Event_LightArea_GetData`。

Flow 中 `AlgorithmType.發光區檢測` 對應 `operatorCode = "FocusPoints"`。同一組發光區節點也可能暴露 ROI、AA 找點和保存 POI 模板，所以不要只看 `FocusPoints/` 目錄判斷完整能力。

## 交接重點

- `TemplateDicId = 15` 與 `Code = "focusPoints"` 是載入和導入導出的關鍵。
- 此模板保存前處理閾值，不是專案判定規則。
- 手動執行使用 `Event_LightArea_GetData`，Flow 使用 `FocusPoints` 算子碼。
- `FocusPoints/` 目前沒有專屬 `ViewHandle*.cs`，結果展示通常追發光區、ROI 或 POI 鏈路。

## 相關頁面

- [FindLightArea 發光區定位模板](./find-light-area.md)
- [POI 模板](./poi-template.md)
- [模板與 Flow 鏈路](../../engine-components/template-flow-chain.md)
- [目前演算法模板覆蓋清單](../current-algorithm-template-coverage.md)

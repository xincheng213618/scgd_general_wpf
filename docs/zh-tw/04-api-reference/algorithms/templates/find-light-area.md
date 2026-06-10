# FindLightArea 發光區定位模板

本頁說明 `Engine/ColorVision.Engine/Templates/FindLightArea/` 的真實交接鏈路。它不是通用 ROI SDK，而是「模板參數 -> 圖像輸入 -> MQTT 演算法請求 -> 發光區點位結果 -> 圖像凸包覆蓋層」的業務模板。

## 適用範圍

| 事項 | 當前實現 |
| --- | --- |
| 模板代碼 | `FindLightArea` |
| 模板類 | `TemplateRoi : ITemplate<RoiParam>, IITemplateLoad` |
| 參數類 | `RoiParam` |
| 執行入口 | `AlgorithmRoi`，顯示名「發光區定位1」 |
| UI 面板 | `DisplayRoi.xaml(.cs)` |
| MQTT 事件 | `MQTTAlgorithmEventEnum.Event_LightArea2_GetData` |
| 結果處理 | `ViewHandleFindLightArea` |
| 結果表 | `t_scgd_algorithm_result_detail_light_area` |

## 源碼入口

| 檔案 | 交接用途 |
| --- | --- |
| `TemplateRoi.cs` | 註冊 `FindLightArea` 模板，設定 `TemplateDicId = 31`，並透過 `MysqlRoi` 恢復模板字典。 |
| `ROIParam.cs` | 保存 `Threshold`、`Times`、`SmoothSize`。 |
| `AlgorithmRoi.cs` | 組裝演算法請求並發布 MQTT 命令。 |
| `DisplayRoi.xaml.cs` | 提供模板選擇、圖像來源、批次號/Raw/本地檔案輸入和執行。 |
| `AlgResultLightAreaDao.cs` | 定義結果模型、結果載入、圖像覆蓋層和列表展示。 |
| `MysqlRoi.cs` | 恢復 MySQL 字典和預設模板項。 |

## 執行鏈路

1. `TemplateRoi` 被模板系統掃描後進入 `TemplateControl`。
2. 使用者在 `TemplateRoi.Params` 中選擇一個 `RoiParam`。
3. `DisplayRoi` 支援批次號、演算法服務 Raw/CIE 檔案或本地圖像。
4. 檔案副檔名會映射為 `Raw`、`CIE`、`Tif` 或 `Src`；`HistoryFilePath` 可把歷史檔名替換為完整路徑。
5. `AlgorithmRoi.SendCommand(...)` 發送 `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType` 和 `TemplateParam`。
6. 命令發布到 `Event_LightArea2_GetData`。
7. `ViewHandleFindLightArea` 接管 `LightArea` / `FindLightArea` 結果。

## 參數說明

| 參數 | 預設值 | 交接說明 |
| --- | --- | --- |
| `Threshold` | `1` | 發光區閾值；調整時要記錄圖像類型和曝光條件。 |
| `Times` | `1` | 演算法側處理次數，由演算法服務解釋。 |
| `SmoothSize` | `1` | 平滑尺寸；驗收時要看凸包輸出，不只看點表。 |

## 結果展示

`AlgResultLightAreaModel` 只保存 `PosX`、`PosY` 和 `Pid`。展示時會使用 `GrahamScan.ComputeConvexHull(...)` 建立凸包，並用藍色透明 `DVPolygon` 畫在圖像上。

注意兩個邊界：

- 點位列表和凸包不是同一件事；凸包異常時先查圖像和 ROI 參數。
- 當前 `SideSave(...)` 會建立導出檔，但沒有寫入點位行，不應當作穩定 CSV 匯出能力。

## 常見排查

| 現象 | 優先排查 |
| --- | --- |
| 模板下拉為空 | 程式集是否裝載、`IITemplateLoad` 是否執行、`TemplateDicId = 31` 是否恢復。 |
| 演算法服務讀不到圖像 | `ImgFileName`、`FileType`、歷史路徑替換和設備 `Code/Type`。 |
| 結果頁無點位 | 結果類型是否為 `LightArea` / `FindLightArea`，結果表是否有對應 `Pid`。 |
| 覆蓋層形狀異常 | `Threshold`、`Times`、`SmoothSize`、輸入圖像和凸包輸入點。 |

## 交接清單

- 修改參數時，同步更新 `ROIParam.cs`、`MysqlRoi.cs` 和現場推薦值。
- 修改執行事件時，同步更新 `AlgorithmRoi.SendCommand(...)`、Flow 節點說明和本頁。
- 修改結果結構時，同步更新結果表、展示列和導出邏輯。
- 專案包使用發光區結果時，必須說明讀取的是點位、凸包還是圖像區域。

## 繼續閱讀

- [ROI 原語](../primitives/roi.md)
- [OpenCV 整合](../../../02-developer-guide/engine-development/opencv-integration.md)
- [結果交接鏈路](../../engine-components/result-handoff-chain.md)
- [目前演算法模板覆蓋清單](../current-algorithm-template-coverage.md)

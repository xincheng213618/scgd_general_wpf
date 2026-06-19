# Matching 模板匹配

本頁說明 `Engine/ColorVision.Engine/Templates/Matching/` 的模板、手動演算法頁、Flow 節點和 AOI 結果展示。Matching 會向演算法服務送出 `MatchTemplate`，結果以 AOI 明細回寫並繪製四點多邊形。

## 適用範圍

| 事項 | 目前實作 |
| --- | --- |
| 模板類別 | `TemplateMatch : ITemplate<MatchParam>, IITemplateLoad` |
| 參數類別 | `MatchParam : ParamModBase` |
| 模板代碼 | `MatchTemplate` |
| 字典 ID | `TemplateDicId = 34` |
| 手動入口 | `AlgorithmMatching` |
| UI | `DisplayMatching.xaml(.cs)` |
| MQTT 事件 | `MQTTAlgorithmEventEnum.Event_MatchTemplate` |
| Flow 節點 | `AlgorithmTMNode` |
| 結果類型 | `ViewResultAlgType.AOI` |
| 結果表 | `t_scgd_algorithm_result_detail_aoi` |
| 結果 handler | `ViewHandleMatching` |

## 參數

| 參數 | 預設值 | 說明 |
| --- | --- | --- |
| `MinReducedArea` | `256` | 取樣細緻度，描述範圍 `64 ~ 2048`。 |
| `ToleranceAngle` | `0` | 誤差角度，描述範圍 `0-180`。 |
| `Similarity` | `0.7` | 相似度閾值，描述範圍 `0-1`。 |
| `MaxOverlapRatio` | `0` | 最大交疊比例，描述範圍 `0-0.8`。 |
| `TargetNumber` | `70` | 目標數量。 |

`TemplateFile` 不是 `MatchParam` 欄位，而是 `AlgorithmMatching` 和 `AlgorithmTMNode` 的執行期參數。交接時要分開記錄參數模板和模板文件。

## 執行鏈路

手動頁 `DisplayMatching` 可選擇參數模板、`TemplateFile`、本地影像、服務端 Raw/CIE 或批次號，最後呼叫 `AlgorithmMatching.SendCommand(...)`。請求包含 `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateFile` 和 `TemplateParam`。

Flow 走 `AlgorithmTMNode`，其 `operatorCode` 固定為 `MatchTemplate`，透過 `TMParam` 帶入 `TemplateFile` 和影像參數。

目前 XAML 中模板 ComboBox 的 `SelectedIndex` 綁到 `TemplatePoiSelectedIndex`，但 `SendCommand` 讀的是 `TemplateSelectedIndex`。若 UI 選了模板但送出的仍是第一筆，先檢查此綁定。

## 結果展示

`ViewHandleMatching` 處理 `ViewResultAlgType.AOI`，從 `AlgResultAoiDao.GetAllByPid(result.Id)` 讀取明細，開啟原圖，使用四角座標產生凸包，並用藍色 `DVPolygon` 繪製 overlay。表格會顯示分數、角度、中心點和四角座標。

目前 `Load(...)` 只有在 `result.ViewResults != null` 時才重新讀 DAO。若歷史結果沒有 AOI 明細，需確認呼叫端是否初始化 `ViewResults`，以及這個判斷是否需要調整。

## 常見排查

| 現象 | 優先排查 |
| --- | --- |
| 服務未執行 | `DeviceCode`、`DeviceType`、`Event_MatchTemplate` 和服務狀態。 |
| 模板文件無效 | `TemplateFile` 是否存在，服務端是否能訪問。 |
| 參數模板未生效 | ComboBox 綁定、`TemplateSelectedIndex` 和 `TemplateMatch.Params`。 |
| 結果列表為空 | 主結果類型是否為 `AOI`，明細表是否有同 `pid`。 |
| overlay 位置錯 | 四角座標、原圖、縮放和座標系。 |

## 延伸閱讀

- [Engine 結果展示與專案交接鏈路](../../engine-components/result-handoff-chain.md)
- [Engine 模板與 Flow 鏈路](../../engine-components/template-flow-chain.md)
- [ROI 原語](../primitives/roi.md)
- [目前演算法模板覆蓋清單](../current-algorithm-template-coverage.md)

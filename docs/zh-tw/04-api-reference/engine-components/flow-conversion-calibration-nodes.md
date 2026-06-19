# Flow 轉換與校準節點

本頁補齊 Flow 中的資料轉換、影像轉換、校準、校準 ROI 和舊色差校正鏈路。當前源碼並沒有 `Engine/ColorVision.Engine/Templates/FileConvert/`、`ImageTransform/`、`Calibration/` 這三個同名模板目錄；相關能力主要在 `FlowEngineLib` 節點、`ColorVision.Engine/Templates/Flow/NodeConfigurator/` 和 Calibration 裝置服務中。

交接時請按 Flow 節點、`operatorCode`、服務和參數物件追蹤，不要按同名模板目錄追蹤。

## 真實入口

| 能力 | 節點/物件 | 源碼入口 | 交接用途 |
| --- | --- | --- | --- |
| 資料轉換 | `AlgDataConvertNode` | `Engine/FlowEngineLib/Node/Algorithm/AlgDataConvertNode.cs` | 把上一步資料和模板資訊發給 Algorithm 服務。 |
| 資料轉換參數 | `DataConvertData` | `Engine/FlowEngineLib/Node/Algorithm/DataConvertData.cs` | 承載 `MethodType`、`InType`、`OutType`、`TemplateParam`。 |
| 影像轉換 | `AlgorithmImageConvertNode` | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmImageConvertNode.cs` | 把影像、通道和目標格式發給 Algorithm 服務。 |
| 影像轉換參數 | `AlgorithmImageConvertParam` | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmImageConvertParam.cs` | 承載 `ResultImageFormat`、`ResultDataFileName`、`Channel`。 |
| 單輸入校準 | `CalibrationNode` | `Engine/FlowEngineLib/Algorithm/CalibrationNode.cs` | 使用校準裝置執行曝光模板、影像和可選 POI 參數。 |
| 雙輸入校準 | `Calibration2InNode` | `Engine/FlowEngineLib/Node/OLED/Calibration2InNode.cs` | 第二輸入提供 POI 上游結果，寫入 `POI_MasterId`。 |
| 校準 ROI | `CalibrationROINode` | `Engine/FlowEngineLib/Node/Camera/CalibrationROINode.cs` | 向校準裝置發送 `SetROI`。 |
| 舊色差校正 | `AlgorithmCaliNode` | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmCaliNode.cs` | 兼容 `CaliAngleShift` JSON 模板和結果展示。 |
| 校準模板綁定 | `CalibrationNodeConfigurator` | `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/DeviceNodeConfigurators.cs` | 選擇 `DeviceCalibration`，按 `PhyCamera` 補校準模板列表。 |

## 節點矩陣

| 節點 | 分組 | `operatorCode` | 服務/裝置 | 參數物件 | 交接重點 |
| --- | --- | --- | --- | --- | --- |
| `AlgDataConvertNode` | Algorithm | `Math.DataConvert` | `SVR.Algorithm.Default` / `DEV.Algorithm.Default` | `DataConvertData` | 不是通用檔案轉換器，目前只覆蓋既有 enum 和上游結果轉換。 |
| `AlgorithmImageConvertNode` | `/03_3 Image` | `Image.Convert` | `SVR.Algorithm.Default` / `DEV.Algorithm.Default` | `AlgorithmImageConvertParam` | 目標格式為 `CSV`、`TIF`，預設通道為 `GREEN`。 |
| `CalibrationNode` | `/03_3 校正` | `Calibration` | `SVR.Calibration.Default` / `DEV.Calibration.Default` | `CalibrationData` | 單輸入校準，參數來自上一步、曝光模板、影像和可選 POI 模板。 |
| `Calibration2InNode` | `/03_3 校正` | `Calibration` | `SVR.Calibration.Default` / `DEV.Calibration.Default` | `CalibrationData` | `IN_POI` 的 `MasterId` 會成為 `POI_MasterId`。 |
| `CalibrationROINode` | `/11 ROI` | `SetROI` | `SVR.Calibration.Default` / `DEV.Calibration.Default` | `CalibrationSetROIParam` | 只設定 ROI，不執行完整校準。 |
| `AlgorithmCaliNode` | `/03_3 校正` | `CaliAngleShift` | `SVR.Algorithm.Default` / `DEV.Algorithm.Default` | `AlgorithmCaliParam` | 舊色差校正鏈路，模板來自 `TemplateCaliAngleShift`。 |

## 資料和影像轉換邊界

`AlgDataConvertNode` 會建立 `DataConvertData`，用 `getPreStepParam(start, dataConvertData)` 讀取上一步結果，再用 `BuildTemp()` 寫入 `TemplateParam`。目前 `CVDataConvertMethodType` 只有 `Camera_Motor_VID`，輸入/輸出類型都只有 `None = -1`，因此不要寫成任意檔案格式互轉。

`AlgorithmImageConvertNode` 會建立 `AlgorithmImageConvertParam(_OutputFileName, _ImageFormat, (int)_Channel)`，再呼叫 `BuildImageParam(...)` 和 `getPreStepParam(...)`。目前格式為 `CSV`、`TIF`，通道為 `BLUE`、`GREEN`、`RED`、`ALL`，預設 `GREEN`。`ResultDataFileName` 來自 `_OutputFileName`，目前沒有獨立可見的輸出檔名屬性。

## 校準鏈路

`CalibrationNode` 是單輸入完整校準：讀取上一步 `AlgorithmPreStepParam`，建立 `CalibrationData(_ExpTempName, param, _IsSaveCIE)`，透過 `BuildImageParam(calibrationData)` 寫入影像和模板資訊，並在有 `POITempName` 時補 `POIParam`。

`Calibration2InNode` 是雙輸入校準：`IN_IMG` 提供影像或上游結果，`IN_POI` 提供 POI 結果。第二輸入的 `MasterId` 會寫入 `calibrationData.POI_MasterId`，所以排查時要先確認 POI 上游是否有有效 `MasterId`。

`CalibrationROINode` 只送 `CalibrationSetROIParam(ROI_X, ROI_Y, ROI_Width, ROI_Height)` 到 Calibration 服務，負責設定 ROI，不負責完整校準或結果保存。

## 配置器責任

| 配置器 | 補充內容 |
| --- | --- |
| `CalibrationNodeConfigurator` | 補 `DeviceCalibration` 選擇器、影像路徑，且在校準裝置有 `PhyCamera` 時補 `TemplateCalibrationParam(result.PhyCamera)`。 |
| Camera node configurators | 在 `CVAOICameraNode`、`AOILocatePixelsCameraNode`、`AOILocAndRegPixelsCameraNode`、`CVAOI2CameraNode`、`CommCameraNode`、`CVCameraNode`、`LVCameraNode` 等相機節點補校準模板。 |
| `AlgorithmCaliNodeConfigurator` | 補 Algorithm 裝置、影像路徑和舊 `TemplateCaliAngleShift` JSON 模板。 |

如果節點介面看不到校準模板，先確認選中的 `DeviceCalibration` 或 `DeviceCamera` 是否有 `PhyCamera`。

## 驗收建議

| 場景 | 驗收方法 |
| --- | --- |
| 資料轉換 | 確認 `Math.DataConvert` 請求包含上一步資料、`MethodType` 和 `TemplateParam`。 |
| 影像轉換 | 用已知影像結果執行 `Image.Convert`，覆蓋 `CSV`、`TIF` 和不同 `Channel`。 |
| 單輸入校準 | 確認請求包含 `CalibrationData`、`ExpTemplateParam`、`IsSaveCIE` 和可選 `POIParam`。 |
| 雙輸入校準 | `IN_POI` 連到有效 POI 結果時，`POI_MasterId` 不應為 `-1`。 |
| 校準 ROI | 觸發 `SetROI` 後確認校準裝置 ROI 更新。 |
| 舊色差校正 | 確認 `TemplateCaliAngleShift` 可載入，`CaliAngleShift` 結果能被展示。 |

## 繼續閱讀

- [模板與 Flow 鏈路](./template-flow-chain.md)
- [裝置服務鏈路](./device-service-chain.md)
- [結果展示與專案交接鏈路](./result-handoff-chain.md)
- [POI 模板](../algorithms/templates/poi-template.md)
- [JSON 模板](../algorithms/templates/json-templates.md)
- [校準服務使用說明](../../01-user-guide/devices/calibration.md)

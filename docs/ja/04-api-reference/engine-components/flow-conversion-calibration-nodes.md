# Flow 変換と校正ノード

このページは Flow のデータ変換、画像変換、校正、校正 ROI、旧色差校正チェーンを説明します。現在のソースには `Engine/ColorVision.Engine/Templates/FileConvert/`、`ImageTransform/`、`Calibration/` という同名テンプレートフォルダはありません。関連機能は `FlowEngineLib` ノード、`ColorVision.Engine/Templates/Flow/NodeConfigurator/`、Calibration デバイスサービスにあります。

同名フォルダではなく、Flow node、`operatorCode`、service、parameter object から追跡してください。

## 実際の入口

| Capability | Node/object | Source entry | Handoff use |
| --- | --- | --- | --- |
| Data conversion | `AlgDataConvertNode` | `Engine/FlowEngineLib/Node/Algorithm/AlgDataConvertNode.cs` | 前段結果とテンプレート情報を Algorithm service に送ります。 |
| Data conversion param | `DataConvertData` | `Engine/FlowEngineLib/Node/Algorithm/DataConvertData.cs` | `MethodType`、`InType`、`OutType`、`TemplateParam` を保持します。 |
| Image conversion | `AlgorithmImageConvertNode` | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmImageConvertNode.cs` | 画像、チャンネル、出力形式を Algorithm service に送ります。 |
| Image conversion param | `AlgorithmImageConvertParam` | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmImageConvertParam.cs` | `ResultImageFormat`、`ResultDataFileName`、`Channel` を保持します。 |
| Single-input calibration | `CalibrationNode` | `Engine/FlowEngineLib/Algorithm/CalibrationNode.cs` | 露光テンプレート、画像、任意の POI パラメータで校正を実行します。 |
| Two-input calibration | `Calibration2InNode` | `Engine/FlowEngineLib/Node/OLED/Calibration2InNode.cs` | 2 番目の入力結果を `POI_MasterId` として使います。 |
| Calibration ROI | `CalibrationROINode` | `Engine/FlowEngineLib/Node/Camera/CalibrationROINode.cs` | Calibration service に `SetROI` を送ります。 |
| Legacy color correction | `AlgorithmCaliNode` | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmCaliNode.cs` | `CaliAngleShift` JSON テンプレートと結果の互換チェーンです。 |

## ノードマトリクス

| Node | Group | `operatorCode` | Service/device | Parameter object | Focus |
| --- | --- | --- | --- | --- | --- |
| `AlgDataConvertNode` | Algorithm | `Math.DataConvert` | `SVR.Algorithm.Default` / `DEV.Algorithm.Default` | `DataConvertData` | 汎用ファイル変換ではなく、既存 enum と前段結果に限定されます。 |
| `AlgorithmImageConvertNode` | `/03_3 Image` | `Image.Convert` | `SVR.Algorithm.Default` / `DEV.Algorithm.Default` | `AlgorithmImageConvertParam` | 出力形式は `CSV`、`TIF`、デフォルト channel は `GREEN` です。 |
| `CalibrationNode` | `/03_3 校正` | `Calibration` | `SVR.Calibration.Default` / `DEV.Calibration.Default` | `CalibrationData` | 単入力校正。前段、露光テンプレート、画像、任意 POI を使います。 |
| `Calibration2InNode` | `/03_3 校正` | `Calibration` | `SVR.Calibration.Default` / `DEV.Calibration.Default` | `CalibrationData` | `IN_POI` の `MasterId` が `POI_MasterId` になります。 |
| `CalibrationROINode` | `/11 ROI` | `SetROI` | `SVR.Calibration.Default` / `DEV.Calibration.Default` | `CalibrationSetROIParam` | ROI 設定のみで、完全な校正は実行しません。 |
| `AlgorithmCaliNode` | `/03_3 校正` | `CaliAngleShift` | `SVR.Algorithm.Default` / `DEV.Algorithm.Default` | `AlgorithmCaliParam` | 旧 `TemplateCaliAngleShift` に基づく色差校正です。 |

## 変換の境界

`AlgDataConvertNode` は `DataConvertData` を作り、`getPreStepParam(start, dataConvertData)` で前段結果を読み、`BuildTemp()` で `TemplateParam` を設定します。現状 `CVDataConvertMethodType` は `Camera_Motor_VID` のみ、input/output enum は `None = -1` のみなので、汎用ファイル変換として説明しないでください。

`AlgorithmImageConvertNode` は `AlgorithmImageConvertParam(_OutputFileName, _ImageFormat, (int)_Channel)` を作り、`BuildImageParam(...)` と `getPreStepParam(...)` を呼びます。形式は `CSV`、`TIF`、channel は `BLUE`、`GREEN`、`RED`、`ALL` です。`ResultDataFileName` は `_OutputFileName` 由来で、現在は独立した表示プロパティがありません。

## 校正チェーン

`CalibrationNode` は単入力校正です。前段 `AlgorithmPreStepParam` を読み、`CalibrationData(_ExpTempName, param, _IsSaveCIE)` を作成し、`BuildImageParam(calibrationData)` で画像/テンプレート情報を設定します。`POITempName` がある場合は `POIParam` も設定します。

`Calibration2InNode` は `IN_IMG` と `IN_POI` を持つ二入力ノードです。`IN_POI` の `MasterId` が `calibrationData.POI_MasterId` になるため、失敗時は POI 入力が有効な `MasterId` を返しているか確認します。

`CalibrationROINode` は `CalibrationSetROIParam(ROI_X, ROI_Y, ROI_Width, ROI_Height)` を送るだけで、完全な校正や結果保存は行いません。

## Configurator の責任

| Configurator | Adds |
| --- | --- |
| `CalibrationNodeConfigurator` | `DeviceCalibration` selector、image path selector、選択デバイスに `PhyCamera` がある場合の `TemplateCalibrationParam(result.PhyCamera)`。 |
| Camera node configurators | `CVAOICameraNode`、`AOILocatePixelsCameraNode`、`AOILocAndRegPixelsCameraNode`、`CVAOI2CameraNode`、`CommCameraNode`、`CVCameraNode`、`LVCameraNode` などに校正テンプレート selector を追加します。 |
| `AlgorithmCaliNodeConfigurator` | Algorithm device、image path、旧 `TemplateCaliAngleShift` JSON template を追加します。 |

Flow 画面に校正テンプレートが出ない場合は、選択中の `DeviceCalibration` または `DeviceCamera` に `PhyCamera` があるかを最初に確認します。

## 受け入れ確認

| Scenario | Verify |
| --- | --- |
| Data conversion | `Math.DataConvert` request に前段データ、`MethodType`、`TemplateParam` が入ること。 |
| Image conversion | `Image.Convert` が `CSV`/`TIF` と選択 channel で動くこと。 |
| Single-input calibration | `CalibrationData`、`ExpTemplateParam`、`IsSaveCIE`、任意 `POIParam` が入ること。 |
| Two-input calibration | 有効な POI 結果を接続したとき `POI_MasterId` が `-1` でないこと。 |
| Calibration ROI | `SetROI` 後にデバイス ROI が更新されること。 |
| Legacy color correction | `TemplateCaliAngleShift` が読み込め、`CaliAngleShift` 結果が表示されること。 |

## 続きを読む

- [テンプレートと Flow チェーン](./template-flow-chain.md)
- [デバイスサービスチェーン](./device-service-chain.md)
- [結果表示とプロジェクト引き継ぎチェーン](./result-handoff-chain.md)
- [POI テンプレート](../algorithms/templates/poi-template.md)
- [JSON テンプレート](../algorithms/templates/json-templates.md)
- [校正サービス利用手順](../../01-user-guide/devices/calibration.md)

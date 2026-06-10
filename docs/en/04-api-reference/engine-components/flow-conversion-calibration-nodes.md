# Flow Conversion And Calibration Nodes

This page documents the Flow data-conversion, image-conversion, calibration, calibration ROI, and legacy color-difference calibration paths. The current source tree does not contain `Engine/ColorVision.Engine/Templates/FileConvert/`, `ImageTransform/`, or `Calibration/` template folders. These capabilities live in `FlowEngineLib` nodes, `ColorVision.Engine/Templates/Flow/NodeConfigurator/`, and Calibration device services.

Read these paths by Flow node, `operatorCode`, service, and parameter object rather than by a same-name template folder.

## Real Entry Points

| Capability | Node/object | Source entry | Handoff use |
| --- | --- | --- | --- |
| Data conversion | `AlgDataConvertNode` | `Engine/FlowEngineLib/Node/Algorithm/AlgDataConvertNode.cs` | Sends previous-step data and template info to the Algorithm service. |
| Data conversion param | `DataConvertData` | `Engine/FlowEngineLib/Node/Algorithm/DataConvertData.cs` | Carries `MethodType`, `InType`, `OutType`, and `TemplateParam`. |
| Image conversion | `AlgorithmImageConvertNode` | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmImageConvertNode.cs` | Sends image, channel, and target format to the Algorithm service. |
| Image conversion param | `AlgorithmImageConvertParam` | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmImageConvertParam.cs` | Carries `ResultImageFormat`, `ResultDataFileName`, and `Channel`. |
| Single-input calibration | `CalibrationNode` | `Engine/FlowEngineLib/Algorithm/CalibrationNode.cs` | Runs calibration with exposure template, image, and optional POI params. |
| Two-input calibration | `Calibration2InNode` | `Engine/FlowEngineLib/Node/OLED/Calibration2InNode.cs` | Uses the second input result as `POI_MasterId`. |
| Calibration ROI | `CalibrationROINode` | `Engine/FlowEngineLib/Node/Camera/CalibrationROINode.cs` | Sends a `SetROI` request to the Calibration service. |
| Legacy color correction | `AlgorithmCaliNode` | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmCaliNode.cs` | Compatible path for `CaliAngleShift` JSON templates and results. |
| Calibration template binding | `CalibrationNodeConfigurator` | `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/DeviceNodeConfigurators.cs` | Selects `DeviceCalibration` and adds camera-bound calibration templates. |
| Camera-side calibration binding | Camera node configurators | `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/CameraNodeConfigurators.cs` | Adds calibration templates to camera nodes from `DeviceCamera.PhyCamera`. |
| Color-correction binding | `AlgorithmCaliNodeConfigurator` | `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs` | Binds the legacy `TemplateCaliAngleShift` JSON template. |

## Node Matrix

| Node | Group | `operatorCode` | Service/device | Parameter object | Handoff focus |
| --- | --- | --- | --- | --- | --- |
| `AlgDataConvertNode` | Algorithm | `Math.DataConvert` | `SVR.Algorithm.Default` / `DEV.Algorithm.Default` | `DataConvertData` | Not a generic file converter; currently limited to the existing enum surface and previous-step data. |
| `AlgorithmImageConvertNode` | `/03_3 Image` | `Image.Convert` | `SVR.Algorithm.Default` / `DEV.Algorithm.Default` | `AlgorithmImageConvertParam` | Target formats are `CSV` and `TIF`; default channel is `GREEN`. |
| `CalibrationNode` | `/03_3 校正` | `Calibration` | `SVR.Calibration.Default` / `DEV.Calibration.Default` | `CalibrationData` | Single-input calibration with exposure template, image, and optional POI template chain. |
| `Calibration2InNode` | `/03_3 校正` | `Calibration` | `SVR.Calibration.Default` / `DEV.Calibration.Default` | `CalibrationData` | The `IN_POI` input provides `MasterId` for `POI_MasterId`. |
| `CalibrationROINode` | `/11 ROI` | `SetROI` | `SVR.Calibration.Default` / `DEV.Calibration.Default` | `CalibrationSetROIParam` | Sets ROI only; it does not run a full calibration. |
| `AlgorithmCaliNode` | `/03_3 校正` | `CaliAngleShift` | `SVR.Algorithm.Default` / `DEV.Algorithm.Default` | `AlgorithmCaliParam` | Legacy color-difference correction based on `TemplateCaliAngleShift`. |

## Data Conversion Boundary

`AlgDataConvertNode` creates `DataConvertData`, reads previous-step data through `getPreStepParam(start, dataConvertData)`, and writes `TemplateParam` with `BuildTemp()`. The current enum surface is intentionally small: `CVDataConvertMethodType` only has `Camera_Motor_VID`, while input and output types only have `None = -1`.

Do not describe it as universal file conversion. A real extension must update the enums, algorithm-service handling for `Math.DataConvert`, node UI, template binding, and acceptance data.

## Image Conversion Boundary

`AlgorithmImageConvertNode` builds `AlgorithmImageConvertParam(_OutputFileName, _ImageFormat, (int)_Channel)`, then calls `BuildImageParam(...)` and `getPreStepParam(...)`.

| Property | Meaning |
| --- | --- |
| `ImageFormat` | Target format, currently `CSV` or `TIF`. |
| `ImgFileName` | Image path used by `BuildImageParam()`. |
| `Channel` | `BLUE`, `GREEN`, `RED`, or `ALL`; default is `GREEN`. |
| `ResultDataFileName` | Backed by `_OutputFileName`; currently there is no separate visible output-file property on the node. |

## Calibration Chain

`CalibrationNode` runs a normal single-input calibration. It reads previous-step data into `AlgorithmPreStepParam`, creates `CalibrationData(_ExpTempName, param, _IsSaveCIE)`, writes image/template data through `BuildImageParam(calibrationData)`, and optionally writes `POIParam`.

`Calibration2InNode` uses two hub inputs: `IN_IMG` for the image or upstream result and `IN_POI` for a previous POI result. The second input's `MasterId` becomes `calibrationData.POI_MasterId`, so failed two-input calibration should first check whether the POI input produced a valid `MasterId`.

`CalibrationROINode` sends `CalibrationSetROIParam` with `ROI_X`, `ROI_Y`, `ROI_Width`, and `ROI_Height`. It sets device ROI only.

## Configurator Responsibilities

| Configurator | Adds |
| --- | --- |
| `CalibrationNodeConfigurator` | `DeviceCalibration` selector, image path selector, and `TemplateCalibrationParam(result.PhyCamera)` when the selected calibration device has a physical camera. |
| Camera node configurators | Calibration template selectors on camera nodes such as `CVAOICameraNode`, `AOILocatePixelsCameraNode`, `AOILocAndRegPixelsCameraNode`, `CVAOI2CameraNode`, `CommCameraNode`, `CVCameraNode`, and `LVCameraNode`. |
| `AlgorithmCaliNodeConfigurator` | Algorithm device selector, image path selector, and the legacy `TemplateCaliAngleShift` JSON template. |

If a Flow node does not show calibration templates, first verify the selected `DeviceCalibration` or `DeviceCamera` has `PhyCamera`.

## POI And Legacy JSON

- Calibration nodes can consume POI templates or a previous POI result. See [POI Template](../algorithms/templates/poi-template.md).
- `AlgorithmCaliNode` uses `CaliAngleShift` under `Engine/ColorVision.Engine/Templates/Jsons/Deprecated/CaliAngleShift/` and result type `ViewResultAlgType.CaliAngleShift`.
- General JSON template rules are in [JSON Templates](../algorithms/templates/json-templates.md).

## Acceptance Checklist

| Scenario | Verify |
| --- | --- |
| Data conversion | `Math.DataConvert` request contains previous-step data, `MethodType`, and `TemplateParam`. |
| Image conversion | `Image.Convert` runs for `CSV`/`TIF` and the selected channel. |
| Single-input calibration | Request contains `CalibrationData`, `ExpTemplateParam`, `IsSaveCIE`, and optional `POIParam`. |
| Two-input calibration | `POI_MasterId` is not `-1` when `IN_POI` is connected to a valid POI result. |
| Calibration ROI | `SetROI` updates the calibration device ROI. |
| Legacy color correction | `TemplateCaliAngleShift` loads and `CaliAngleShift` results are handled by the result view. |

## Continue Reading

- [Templates And Flow Chain](./template-flow-chain.md)
- [Device Service Chain](./device-service-chain.md)
- [Result Display And Project Handoff](./result-handoff-chain.md)
- [POI Template](../algorithms/templates/poi-template.md)
- [JSON Templates](../algorithms/templates/json-templates.md)
- [Calibration Service User Guide](../../01-user-guide/devices/calibration.md)

# Flow 변환 및 보정 노드

이 페이지는 Flow의 데이터 변환, 이미지 변환, 보정, 보정 ROI, 기존 색차 보정 체인을 설명합니다. 현재 소스에는 `Engine/ColorVision.Engine/Templates/FileConvert/`, `ImageTransform/`, `Calibration/` 같은 동일 이름 템플릿 폴더가 없습니다. 관련 기능은 `FlowEngineLib` 노드, `ColorVision.Engine/Templates/Flow/NodeConfigurator/`, Calibration 디바이스 서비스에 있습니다.

인수인계할 때는 동일 이름 폴더가 아니라 Flow node, `operatorCode`, service, parameter object 기준으로 추적하세요.

## 실제 진입점

| Capability | Node/object | Source entry | Handoff use |
| --- | --- | --- | --- |
| Data conversion | `AlgDataConvertNode` | `Engine/FlowEngineLib/Node/Algorithm/AlgDataConvertNode.cs` | 이전 단계 데이터와 템플릿 정보를 Algorithm service 로 보냅니다. |
| Data conversion param | `DataConvertData` | `Engine/FlowEngineLib/Node/Algorithm/DataConvertData.cs` | `MethodType`, `InType`, `OutType`, `TemplateParam` 을 담습니다. |
| Image conversion | `AlgorithmImageConvertNode` | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmImageConvertNode.cs` | 이미지, 채널, 대상 형식을 Algorithm service 로 보냅니다. |
| Image conversion param | `AlgorithmImageConvertParam` | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmImageConvertParam.cs` | `ResultImageFormat`, `ResultDataFileName`, `Channel` 을 담습니다. |
| Single-input calibration | `CalibrationNode` | `Engine/FlowEngineLib/Algorithm/CalibrationNode.cs` | 노출 템플릿, 이미지, 선택적 POI 파라미터로 보정을 실행합니다. |
| Two-input calibration | `Calibration2InNode` | `Engine/FlowEngineLib/Node/OLED/Calibration2InNode.cs` | 두 번째 입력 결과를 `POI_MasterId` 로 사용합니다. |
| Calibration ROI | `CalibrationROINode` | `Engine/FlowEngineLib/Node/Camera/CalibrationROINode.cs` | Calibration service 에 `SetROI` 를 보냅니다. |
| Legacy color correction | `AlgorithmCaliNode` | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmCaliNode.cs` | `CaliAngleShift` JSON 템플릿과 결과를 위한 호환 체인입니다. |

## 노드 매트릭스

| Node | Group | `operatorCode` | Service/device | Parameter object | Focus |
| --- | --- | --- | --- | --- | --- |
| `AlgDataConvertNode` | Algorithm | `Math.DataConvert` | `SVR.Algorithm.Default` / `DEV.Algorithm.Default` | `DataConvertData` | 범용 파일 변환기가 아니며 기존 enum 과 이전 단계 결과에 제한됩니다. |
| `AlgorithmImageConvertNode` | `/03_3 Image` | `Image.Convert` | `SVR.Algorithm.Default` / `DEV.Algorithm.Default` | `AlgorithmImageConvertParam` | 대상 형식은 `CSV`, `TIF`, 기본 channel 은 `GREEN` 입니다. |
| `CalibrationNode` | `/03_3 校正` | `Calibration` | `SVR.Calibration.Default` / `DEV.Calibration.Default` | `CalibrationData` | 단일 입력 보정. 이전 단계, 노출 템플릿, 이미지, 선택적 POI 를 사용합니다. |
| `Calibration2InNode` | `/03_3 校正` | `Calibration` | `SVR.Calibration.Default` / `DEV.Calibration.Default` | `CalibrationData` | `IN_POI` 의 `MasterId` 가 `POI_MasterId` 가 됩니다. |
| `CalibrationROINode` | `/11 ROI` | `SetROI` | `SVR.Calibration.Default` / `DEV.Calibration.Default` | `CalibrationSetROIParam` | ROI 설정만 수행하며 전체 보정은 실행하지 않습니다. |
| `AlgorithmCaliNode` | `/03_3 校正` | `CaliAngleShift` | `SVR.Algorithm.Default` / `DEV.Algorithm.Default` | `AlgorithmCaliParam` | 기존 `TemplateCaliAngleShift` 기반 색차 보정입니다. |

## 변환 경계

`AlgDataConvertNode` 는 `DataConvertData` 를 만들고 `getPreStepParam(start, dataConvertData)` 로 이전 단계 결과를 읽은 뒤 `BuildTemp()` 로 `TemplateParam` 을 설정합니다. 현재 `CVDataConvertMethodType` 은 `Camera_Motor_VID` 뿐이고 input/output enum 은 `None = -1` 뿐이므로 범용 파일 변환으로 설명하지 마세요.

`AlgorithmImageConvertNode` 는 `AlgorithmImageConvertParam(_OutputFileName, _ImageFormat, (int)_Channel)` 을 만들고 `BuildImageParam(...)`, `getPreStepParam(...)` 을 호출합니다. 형식은 `CSV`, `TIF` 이고 channel 은 `BLUE`, `GREEN`, `RED`, `ALL` 입니다. `ResultDataFileName` 은 `_OutputFileName` 기반이며 현재 독립적으로 보이는 출력 파일 속성은 없습니다.

## 보정 체인

`CalibrationNode` 는 단일 입력 보정입니다. 이전 단계 `AlgorithmPreStepParam` 을 읽고 `CalibrationData(_ExpTempName, param, _IsSaveCIE)` 를 만든 뒤 `BuildImageParam(calibrationData)` 로 이미지/템플릿 정보를 설정합니다. `POITempName` 이 있으면 `POIParam` 도 설정합니다.

`Calibration2InNode` 는 `IN_IMG` 와 `IN_POI` 를 가진 두 입력 노드입니다. `IN_POI` 의 `MasterId` 가 `calibrationData.POI_MasterId` 가 되므로 실패 시 POI 입력이 유효한 `MasterId` 를 반환하는지 먼저 확인합니다.

`CalibrationROINode` 는 `CalibrationSetROIParam(ROI_X, ROI_Y, ROI_Width, ROI_Height)` 만 보내며 전체 보정이나 결과 저장은 하지 않습니다.

## Configurator 책임

| Configurator | Adds |
| --- | --- |
| `CalibrationNodeConfigurator` | `DeviceCalibration` selector, image path selector, 선택한 디바이스에 `PhyCamera` 가 있을 때 `TemplateCalibrationParam(result.PhyCamera)` 를 추가합니다. |
| Camera node configurators | `CVAOICameraNode`, `AOILocatePixelsCameraNode`, `AOILocAndRegPixelsCameraNode`, `CVAOI2CameraNode`, `CommCameraNode`, `CVCameraNode`, `LVCameraNode` 등에 보정 템플릿 selector 를 추가합니다. |
| `AlgorithmCaliNodeConfigurator` | Algorithm device, image path, 기존 `TemplateCaliAngleShift` JSON template 을 추가합니다. |

Flow 화면에 보정 템플릿이 보이지 않으면 선택한 `DeviceCalibration` 또는 `DeviceCamera` 에 `PhyCamera` 가 있는지 먼저 확인합니다.

## 검수 체크

| Scenario | Verify |
| --- | --- |
| Data conversion | `Math.DataConvert` request 에 이전 단계 데이터, `MethodType`, `TemplateParam` 이 포함됩니다. |
| Image conversion | `Image.Convert` 가 `CSV`/`TIF` 와 선택 channel 로 동작합니다. |
| Single-input calibration | `CalibrationData`, `ExpTemplateParam`, `IsSaveCIE`, 선택적 `POIParam` 이 포함됩니다. |
| Two-input calibration | 유효한 POI 결과를 연결했을 때 `POI_MasterId` 가 `-1` 이 아닙니다. |
| Calibration ROI | `SetROI` 후 디바이스 ROI 가 갱신됩니다. |
| Legacy color correction | `TemplateCaliAngleShift` 가 로드되고 `CaliAngleShift` 결과가 표시됩니다. |

## 계속 읽기

- [Templates And Flow Chain](./template-flow-chain.md)
- [Device Service Chain](./device-service-chain.md)
- [Result Display And Project Handoff](./result-handoff-chain.md)
- [POI Template](../algorithms/templates/poi-template.md)
- [JSON Templates](../algorithms/templates/json-templates.md)
- [Calibration Service User Guide](../../01-user-guide/devices/calibration.md)

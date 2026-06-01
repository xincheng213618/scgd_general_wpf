# Flow Engine Node Complete Collection

> Auto-generated on 2026-05-22, containing all node classes defined in `FlowEngineLib`.

## Overview

A total of **90** node classes, grouped by type as follows:

| Type | Count |
|------|------|
| Algorithm | 25 |
| Camera | 14 |
| POI | 8 |
| Other | 8 |
| SMU | 7 |
| OLED | 7 |
| MQTT | 5 |
| Sensor | 3 |
| Start | 3 |
| Loop | 2 |
| End | 2 |
| Spectrum | 2 |
| FW | 1 |
| Manual | 1 |
| Device | 1 |
| PG | 1 |

## Node List

| Category | Node Class Name | Base Class | File | Property Count |
|------|----------|------|------|--------|
| Algorithm | `AlgComplianceContrastNode` | `CVBaseServerNodeHub` | `Node\Algorithm\AlgComplianceContrastNode.cs` | 2 |
| Algorithm | `AlgComplianceJudgmentNode` | `CVBaseServerNode` | `Node\Algorithm\AlgComplianceJudgmentNode.cs` | 1 |
| Algorithm | `AlgComplianceMathNode` | `CVBaseServerNode` | `Node\Algorithm\AlgComplianceMathNode.cs` | 3 |
| Algorithm | `AlgDataConvertNode` | `CVBaseServerNode` | `Node\Algorithm\AlgDataConvertNode.cs` | 4 |
| Algorithm | `AlgDataLoadNode` | `CVBaseServerNode` | `Node\Algorithm\AlgDataLoadNode.cs` | 1 |
| Algorithm | `AlgDataLoadNode2` | `CVBaseServerNode` | `Node\Algorithm\AlgDataLoadNode2.cs` | 4 |
| Algorithm | `Algorithm2InNode` | `CVBaseServerNodeHub` | `Node\OLED\Algorithm2InNode.cs` | 4 |
| Algorithm | `AlgorithmARVRNode` | `CVBaseServerNode` | `Algorithm\AlgorithmARVRNode.cs` | 6 |
| Algorithm | `AlgorithmBlackMuraNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmBlackMuraNode.cs` | 4 |
| Algorithm | `AlgorithmCaliNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmCaliNode.cs` | 3 |
| Algorithm | `AlgorithmCompoundImgNode` | `CVBaseServerNodeHub` | `Node\OLED\AlgorithmCompoundImgNode.cs` | 3 |
| Algorithm | `AlgorithmEQENode` | `CVBaseServerNodeHub` | `AlgorithmEQENode.cs` | 1 |
| Algorithm | `AlgorithmFindLEDNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmFindLEDNode.cs` | 7 |
| Algorithm | `AlgorithmFindLightAreaNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmFindLightAreaNode.cs` | 5 |
| Algorithm | `AlgorithmGhostV2Node` | `CVBaseServerNodeHub` | `Node\Algorithm\AlgorithmGhostV2Node.cs` | 3 |
| Algorithm | `AlgorithmImageConvertNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmImageConvertNode.cs` | 3 |
| Algorithm | `AlgorithmImageROINode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmImageROINode.cs` | 3 |
| Algorithm | `AlgorithmKBNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmKBNode.cs` | 2 |
| Algorithm | `AlgorithmKBOutputNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmKBOutputNode.cs` | 1 |
| Algorithm | `AlgorithmNode` | `CVBaseServerNode` | `Algorithm\AlgorithmNode.cs` | 6 |
| Algorithm | `AlgorithmOLEDNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmOLEDNode.cs` | 8 |
| Algorithm | `AlgorithmOLED_AOINode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmOLED_AOINode.cs` | 8 |
| Algorithm | `AlgorithmTMNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmTMNode.cs` | 3 |
| Algorithm | `TPAlgorithm2Node` | `CVBaseServerNodeHub` | `Node\Algorithm\TPAlgorithm2Node.cs` | 2 |
| Algorithm | `TPAlgorithmNode` | `CVBaseServerNode` | `Node\Algorithm\TPAlgorithmNode.cs` | 4 |
| Camera | `AOILocAndRegPixelsCameraNode` | `CVBaseServerNode` | `AOILocAndRegPixelsCameraNode.cs` | 13 |
| Camera | `AOILocatePixelsCameraNode` | `CVBaseServerNode` | `AOILocatePixelsCameraNode.cs` | 12 |
| Camera | `AOIRegisterPixelsCameraNode` | `CVBaseServerNodeHub` | `AOIRegisterPixelsCameraNode.cs` | 13 |
| Camera | `BaseCameraNode` | `CVBaseServerNode` | `BaseCameraNode.cs` | 8 |
| Camera | `CVAOI2CameraNode` | `CVBaseServerNodeHub` | `Node\Camera\CVAOI2CameraNode.cs` | 9 |
| Camera | `CVAOICameraNode` | `CVBaseServerNode` | `Node\Camera\CVAOICameraNode.cs` | 9 |
| Camera | `CVCameraDataFlow` | `CVMQTTRequest` | `CVCameraDataFlow.cs` | 0 |
| Camera | `CVCameraNode` | `CVBaseServerNode` | `CVCameraNode.cs` | 11 |
| Camera | `CVXRCameraNode` | `CVBaseServerNode` | `CVXRCameraNode.cs` | 12 |
| Camera | `CamMotorNode` | `CVBaseServerNode` | `CamMotorNode.cs` | 5 |
| Camera | `CameraROINode` | `CVBaseServerNode` | `Node\Camera\CameraROINode.cs` | 4 |
| Camera | `CommCameraNode` | `CVBaseServerNode` | `Node\Camera\CommCameraNode.cs` | 10 |
| Camera | `LVCameraNode` | `BaseCameraNode` | `LVCameraNode.cs` | 0 |
| Camera | `LVXRCameraNode` | `CVBaseServerNode` | `LVXRCameraNode.cs` | 10 |
| Device | `PhyDeviceControlNode` | `CVBaseServerNode` | `Node\Global\PhyDeviceControlNode.cs` | 2 |
| End | `CVEndNode` | `CVCommonNode` | `End\CVEndNode.cs` | 1 |
| End | `CVEndV5Node` | `CVCommonNode` | `End\CVEndV5Node.cs` | 1 |
| FW | `FWNode` | `CVBaseServerNode` | `FWNode.cs` | 2 |
| Loop | `LoopNextNode` | `CVCommonNode` | `LoopNextNode.cs` | 0 |
| Loop | `LoopNode` | `CVCommonNode` | `LoopNode.cs` | 3 |
| MQTT | `MQTTBaseNode` | `STNode` | `MQTT\MQTTBaseNode.cs` | 2 |
| MQTT | `MQTTCustomPublishNode` | `MQTTBaseNode` | `MQTTCustomPublishNode.cs` | 2 |
| MQTT | `MQTTCustomSubscribeNode` | `MQTTBaseNode` | `MQTTCustomSubscribeNode.cs` | 1 |
| MQTT | `MQTTStartNode` | `BaseStartNode` | `Start\MQTTStartNode.cs` | 2 |
| MQTT | `MQTTStartV5Node` | `BaseStartNode` | `Start\MQTTStartV5Node.cs` | 2 |
| Manual | `ManualConfirmNode` | `CVCommonNodeHub` | `ManualConfirmNode.cs` | 1 |
| OLED | `OLEDCombineQuaterImages_4In1Node` | `CVBaseServerNodeHub` | `Node\Algorithm\OLEDCombineQuaterImages_4In1Node.cs` | 5 |
| OLED | `OLEDFindPixelDefectsForQuardImgNode` | `CVBaseServerNode` | `Node\Algorithm\OLEDFindPixelDefectsForQuardImgNode.cs` | 3 |
| OLED | `OLEDImageCroppingNode` | `CVBaseServerNodeHub` | `Node\OLED\OLEDImageCroppingNode.cs` | 2 |
| OLED | `OLEDJNDCalVasNode` | `CVBaseServerNodeHub` | `Node\OLED\OLEDJNDCalVasNode.cs` | 5 |
| OLED | `OLEDParticlesFindAndFillNode` | `CVBaseServerNode` | `Node\Algorithm\OLEDParticlesFindAndFillNode.cs` | 4 |
| OLED | `OLEDRebuildPixelsNode` | `CVBaseServerNodeHub` | `Node\OLED\OLEDRebuildPixelsNode.cs` | 4 |
| OLED | `OLEDRebuildPixelsPosNode` | `CVBaseServerNodeHub` | `Node\OLED\OLEDRebuildPixelsPosNode.cs` | 4 |
| Other | `CVBaseServerNode` | `CVCommonNode` | `Base\CVBaseServerNode.cs` | 3 |
| Other | `CVBaseServerNodeHub` | `CVBaseServerNode` | `Base\CVBaseServerNodeHub.cs` | 0 |
| Other | `CVBaseServerNodeIn2Hub` | `CVBaseServerNode` | `Base\CVBaseServerNodeIn2Hub.cs` | 0 |
| Other | `CVCommonNode` | `STNode` | `Base\CVCommonNode.cs` | 8 |
| Other | `Calibration2InNode` | `CVBaseServerNodeHub` | `Node\OLED\Calibration2InNode.cs` | 7 |
| Other | `CalibrationNode` | `CVBaseServerNode` | `Algorithm\CalibrationNode.cs` | 8 |
| Other | `CalibrationROINode` | `CVBaseServerNode` | `Node\Camera\CalibrationROINode.cs` | 4 |
| Other | `MotorNode` | `CVBaseServerNode` | `MotorNode.cs` | 4 |
| PG | `PGNode` | `CVBaseServerNode` | `Node\PG\PGNode.cs` | 2 |
| POI | `BuildPOI2Node` | `CVBaseServerNodeHub` | `Node\POI\BuildPOI2Node.cs` | 10 |
| POI | `BuildPOINode` | `CVBaseServerNode` | `BuildPOINode.cs` | 14 |
| POI | `POIAnalysisAndSMUNode` | `CVBaseServerNodeHub` | `Node\POI\POIAnalysisAndSMUNode.cs` | 1 |
| POI | `POIAnalysisNode` | `CVBaseServerNode` | `Node\POI\POIAnalysisNode.cs` | 1 |
| POI | `POICADMappingNode` | `CVBaseServerNode` | `Node\POI\POICADMappingNode.cs` | 4 |
| POI | `POINode` | `CVBaseServerNode` | `POINode.cs` | 7 |
| POI | `POIReviseNode` | `CVBaseServerNodeHub` | `Node\POI\POIReviseNode.cs` | 3 |
| POI | `RealPOINode` | `CVBaseServerNodeHub` | `Node\POI\RealPOINode.cs` | 11 |
| SMU | `SMUBaseNode` | `CVBaseServerNode, ICVLoopNextNode` | `SMUBaseNode.cs` | 3 |
| SMU | `SMUFromCSVNode` | `SMUBaseNode` | `SMUFromCSVNode.cs` | 6 |
| SMU | `SMUModelNode` | `SMUBaseNode` | `SMUModelNode.cs` | 1 |
| SMU | `SMUNode` | `SMUBaseNode` | `SMUNode.cs` | 9 |
| SMU | `SMUReaderNode` | `CVBaseServerNode` | `Node\SMU\SMUReaderNode.cs` | 1 |
| SMU | `SMUSweepModelNode` | `CVBaseServerNode` | `Node\SMU\SMUSweepModelNode.cs` | 2 |
| SMU | `SMUSweepNode` | `CVBaseServerNode` | `Node\SMU\SMUSweepNode.cs` | 10 |
| Sensor | `CommonSensorNode` | `CVBaseServerNode` | `CommonSensorNode.cs` | 4 |
| Sensor | `RealCommonSensorNode` | `CVBaseServerNode` | `RealCommonSensorNode.cs` | 6 |
| Sensor | `TempCommonSensorNode` | `CVBaseServerNode` | `TempCommonSensorNode.cs` | 1 |
| Spectrum | `SpectrumEQENode` | `CVBaseServerNode` | `Node\Spectrum\SpectrumEQENode.cs` | 11 |
| Spectrum | `SpectrumNode` | `CVBaseServerNode` | `Node\Spectrum\SpectrumNode.cs` | 8 |
| Start | `CVStartCFC` | `CVBaseCFC` | `Base\CVStartCFC.cs` | 0 |
| Start | `ManualStartNode` | `BaseStartNode` | `ManualStartNode.cs` | 2 |
| Start | `ModbusStartNode` | `BaseStartNode` | `Start\ModbusStartNode.cs` | 0 |

## Detailed Properties by Category

### Algorithm Nodes (25)

#### 1. AlgComplianceContrastNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgComplianceContrastNode`
- **Implementation File**: `Node\Algorithm\AlgComplianceContrastNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Operation` | `OperationType` | Property |
| `TempName` | `string` | Property |

---

#### 2. AlgComplianceJudgmentNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgComplianceJudgmentNode`
- **Implementation File**: `Node\Algorithm\AlgComplianceJudgmentNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `IsBreak` | `bool` | Property |

---

#### 3. AlgComplianceMathNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgComplianceMathNode`
- **Implementation File**: `Node\Algorithm\AlgComplianceMathNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ComplianceMath` | `ComplianceMathType` | Property |
| `IsBreak` | `bool` | Property |

---

#### 4. AlgDataConvertNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgDataConvertNode`
- **Implementation File**: `Node\Algorithm\AlgDataConvertNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `MethodType` | `CVDataConvertMethodType` | Property |
| `TempName` | `string` | Property |
| `InType` | `CVDataConvertInputType` | Property |
| `OutType` | `CVDataConvertOutputType` | Property |

---

#### 5. AlgDataLoadNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgDataLoadNode`
- **Implementation File**: `Node\Algorithm\AlgDataLoadNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |

---

#### 6. AlgDataLoadNode2

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgDataLoadNode2`
- **Implementation File**: `Node\Algorithm\AlgDataLoadNode2.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `DataDeviceCode` | `string` | Property |
| `SerialNumber` | `string` | Property |
| `ResultType` | `CVResultType` | Property |
| `DataZIndex` | `int` | Property |

---

#### 7. Algorithm2InNode

- **Full Type**: `FlowEngineLib.Node.OLED;.Algorithm2InNode`
- **Implementation File**: `Node\OLED\Algorithm2InNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `Algorithm` | `Algorithm2Type` | Property |
| `BufferLen` | `int` | Property |
| `IsAdd` | `bool` | Property |

---

#### 8. AlgorithmARVRNode

- **Full Type**: `FlowEngineLib.Algorithm;.AlgorithmARVRNode`
- **Implementation File**: `Algorithm\AlgorithmARVRNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Algorithm` | `AlgorithmARVRType` | Property |
| `TempName` | `string` | Property |
| `POITempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `Color` | `CVOLED_COLOR` | Property |
| `BufferLen` | `int` | Property |

---

#### 9. AlgorithmBlackMuraNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgorithmBlackMuraNode`
- **Implementation File**: `Node\Algorithm\AlgorithmBlackMuraNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `OIndex` | `string` | Property |
| `SavePOITempName` | `string` | Property |

---

#### 10. AlgorithmCaliNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgorithmCaliNode`
- **Implementation File**: `Node\Algorithm\AlgorithmCaliNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `OutputFileName` | `string` | Property |

---

#### 11. AlgorithmCompoundImgNode

- **Full Type**: `FlowEngineLib.Node.OLED;.AlgorithmCompoundImgNode`
- **Implementation File**: `Node\OLED\AlgorithmCompoundImgNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `OutputFileName` | `string` | Property |
| `BufferLen` | `int` | Property |

---

#### 12. AlgorithmEQENode

- **Full Type**: `FlowEngineLib;.AlgorithmEQENode`
- **Implementation File**: `AlgorithmEQENode.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |

---

#### 13. AlgorithmFindLEDNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgorithmFindLEDNode`
- **Implementation File**: `Node\Algorithm\AlgorithmFindLEDNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Color` | `CVOLED_Channel` | Property |
| `TempName` | `string` | Property |
| `FDAType` | `CVOLED_FDAType` | Property |
| `FixedLEDPoint` | `PointFloat[]` | Property |
| `ImgFileName` | `string` | Property |
| `OutputFileName` | `string` | Property |
| `ImgPosResultFile` | `string` | Property |

---

#### 14. AlgorithmFindLightAreaNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgorithmFindLightAreaNode`
- **Implementation File**: `Node\Algorithm\AlgorithmFindLightAreaNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `SavePOITempName` | `string` | Property |
| `BufferLen` | `int` | Property |
| `OIndex` | `string` | Property |

---

#### 15. AlgorithmGhostV2Node

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgorithmGhostV2Node`
- **Implementation File**: `Node\Algorithm\AlgorithmGhostV2Node.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `BufferLen` | `int` | Property |

---

#### 16. AlgorithmImageConvertNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgorithmImageConvertNode`
- **Implementation File**: `Node\Algorithm\AlgorithmImageConvertNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `ImageFormat` | `ImageFormatType` | Property |
| `ImgFileName` | `string` | Property |
| `Channel` | `CVOLED_Channel` | Property |

---

#### 17. AlgorithmImageROINode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgorithmImageROINode`
- **Implementation File**: `Node\Algorithm\AlgorithmImageROINode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `OutputFileName` | `string` | Property |

---

#### 18. AlgorithmKBNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgorithmKBNode`
- **Implementation File**: `Node\Algorithm\AlgorithmKBNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |

---

#### 19. AlgorithmKBOutputNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgorithmKBOutputNode`
- **Implementation File**: `Node\Algorithm\AlgorithmKBOutputNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |

---

#### 20. AlgorithmNode

- **Full Type**: `FlowEngineLib.Algorithm;.AlgorithmNode`
- **Implementation File**: `Algorithm\AlgorithmNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Algorithm` | `AlgorithmType` | Property |
| `TempName` | `string` | Property |
| `POITempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `Color` | `CVOLED_COLOR` | Property |
| `BufferLen` | `int` | Property |

---

#### 21. AlgorithmOLEDNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgorithmOLEDNode`
- **Implementation File**: `Node\Algorithm\AlgorithmOLEDNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Algorithm` | `AlgorithmOLEDType` | Property |
| `Color` | `CVOLED_COLOR` | Property |
| `TempName` | `string` | Property |
| `FDAType` | `CVOLED_FDAType` | Property |
| `FixedLEDPoint` | `PointFloat[]` | Property |
| `ImgFileName` | `string` | Property |
| `OutputFileName` | `string` | Property |
| `ImgPosResultFile` | `string` | Property |

---

#### 22. AlgorithmOLED_AOINode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgorithmOLED_AOINode`
- **Implementation File**: `Node\Algorithm\AlgorithmOLED_AOINode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Algorithm` | `AlgorithmOLED_AOIType` | Property |
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `OutputFileName` | `string` | Property |
| `CustomSN` | `string` | Property |
| `VhLineEnable` | `bool` | Property |
| `PixelDefectEnable` | `bool` | Property |
| `MuraEnable` | `bool` | Property |

---

#### 23. AlgorithmTMNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.AlgorithmTMNode`
- **Implementation File**: `Node\Algorithm\AlgorithmTMNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `TemplateFile` | `string` | Property |
| `ImgFileName` | `string` | Property |

---

#### 24. TPAlgorithm2Node

- **Full Type**: `FlowEngineLib.Node.Algorithm;.TPAlgorithm2Node`
- **Implementation File**: `Node\Algorithm\TPAlgorithm2Node.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Operator` | `string` | Property |
| `TempName` | `string` | Property |

---

#### 25. TPAlgorithmNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.TPAlgorithmNode`
- **Implementation File**: `Node\Algorithm\TPAlgorithmNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Algorithm` | `TPAlgorithmType` | Property |
| `Operator` | `string` | Property |
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |

---

### Camera Nodes (14)

#### 1. AOILocAndRegPixelsCameraNode

- **Full Type**: `FlowEngineLib;.AOILocAndRegPixelsCameraNode`
- **Implementation File**: `AOILocAndRegPixelsCameraNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `ImgSaveMode` | `ImgSaveBppMode` | Property |
| `ImgSaveName` | `string` | Property |
| `AvgCount` | `int` | Property |
| `Gain` | `float` | Property |
| `ExpTime` | `float` | Property |
| `IsAutoExp` | `bool` | Property |
| `AutoExpTempName` | `string` | Property |
| `IsWithND` | `bool` | Property |
| `CaliTempName` | `string` | Property |
| `FlipMode` | `CVImageFlipMode` | Property |
| `AlgTempName` | `string` | Property |
| `Channel` | `CVOLED_Channel` | Property |
| `OutputTempName` | `string` | Property |

---

#### 2. AOILocatePixelsCameraNode

- **Full Type**: `FlowEngineLib;.AOILocatePixelsCameraNode`
- **Implementation File**: `AOILocatePixelsCameraNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `ImgSaveMode` | `ImgSaveBppMode` | Property |
| `ImgSaveName` | `string` | Property |
| `AvgCount` | `int` | Property |
| `Gain` | `float` | Property |
| `ExpTime` | `float` | Property |
| `IsAutoExp` | `bool` | Property |
| `AutoExpTempName` | `string` | Property |
| `IsWithND` | `bool` | Property |
| `CaliTempName` | `string` | Property |
| `FlipMode` | `CVImageFlipMode` | Property |
| `AlgTempName` | `string` | Property |
| `Channel` | `CVOLED_Channel` | Property |

---

#### 3. AOIRegisterPixelsCameraNode

- **Full Type**: `FlowEngineLib;.AOIRegisterPixelsCameraNode`
- **Implementation File**: `AOIRegisterPixelsCameraNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `ImgSaveMode` | `ImgSaveBppMode` | Property |
| `ImgSaveName` | `string` | Property |
| `AvgCount` | `int` | Property |
| `Gain` | `float` | Property |
| `ExpTime` | `float` | Property |
| `IsAutoExp` | `bool` | Property |
| `AutoExpTempName` | `string` | Property |
| `IsWithND` | `bool` | Property |
| `CaliTempName` | `string` | Property |
| `FlipMode` | `CVImageFlipMode` | Property |
| `AlgTempName` | `string` | Property |
| `Channel` | `CVOLED_Channel` | Property |
| `OutputTempName` | `string` | Property |

---

#### 4. BaseCameraNode

- **Full Type**: `FlowEngineLib;.BaseCameraNode`
- **Implementation File**: `BaseCameraNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `AvgCount` | `int` | Property |
| `Gain` | `float` | Property |
| `ExpTime` | `float` | Property |
| `CaliTempName` | `string` | Property |
| `FlipMode` | `CVImageFlipMode` | Property |
| `POITempName` | `string` | Property |
| `POIFilterTempName` | `string` | Property |
| `POIReviseTempName` | `string` | Property |

---

#### 5. CVAOI2CameraNode

- **Full Type**: `FlowEngineLib.Node.Camera;.CVAOI2CameraNode`
- **Implementation File**: `Node\Camera\CVAOI2CameraNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `CamTempName` | `string` | Property |
| `ImgSaveMode` | `ImgSaveBppMode` | Property |
| `FlipMode` | `CVImageFlipMode` | Property |
| `IsAutoExp` | `bool` | Property |
| `TempName` | `string` | Property |
| `IsWithND` | `bool` | Property |
| `CalibTempName` | `string` | Property |
| `AOIType` | `AOI2TypeEnum` | Property |
| `AlgTempName` | `string` | Property |

---

#### 6. CVAOICameraNode

- **Full Type**: `FlowEngineLib.Node.Camera;.CVAOICameraNode`
- **Implementation File**: `Node\Camera\CVAOICameraNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `CamTempName` | `string` | Property |
| `ImgSaveMode` | `ImgSaveBppMode` | Property |
| `FlipMode` | `CVImageFlipMode` | Property |
| `IsAutoExp` | `bool` | Property |
| `TempName` | `string` | Property |
| `IsWithND` | `bool` | Property |
| `CalibTempName` | `string` | Property |
| `AOIType` | `AOITypeEnum` | Property |
| `AlgTempName` | `string` | Property |

---

#### 7. CVCameraDataFlow

- **Full Type**: `FlowEngineLib;.CVCameraDataFlow`
- **Implementation File**: `CVCameraDataFlow.cs`
- **Base Class**: `CVMQTTRequest`

---

#### 8. CVCameraNode

- **Full Type**: `FlowEngineLib;.CVCameraNode`
- **Implementation File**: `CVCameraNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `AvgCount` | `int` | Property |
| `Gain` | `float` | Property |
| `TempR` | `float` | Property |
| `TempG` | `float` | Property |
| `TempB` | `float` | Property |
| `CV2LVChannel` | `CV2LVChannelMode` | Property |
| `CalibTempName` | `string` | Property |
| `FlipMode` | `CVImageFlipMode` | Property |
| `POITempName` | `string` | Property |
| `POIFilterTempName` | `string` | Property |
| `POIReviseTempName` | `string` | Property |

---

#### 9. CVXRCameraNode

- **Full Type**: `FlowEngineLib;.CVXRCameraNode`
- **Implementation File**: `CVXRCameraNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `AvgCount` | `int` | Property |
| `Gain` | `float` | Property |
| `TempR` | `float` | Property |
| `TempG` | `float` | Property |
| `TempB` | `float` | Property |
| `CalibTempName` | `string` | Property |
| `FlipMode` | `CVImageFlipMode` | Property |
| `POITempName` | `string` | Property |
| `POIFilterTempName` | `string` | Property |
| `POIReviseTempName` | `string` | Property |
| `Algorithm` | `AlgorithmARVRType` | Property |
| `XRTempName` | `string` | Property |

---

#### 10. CamMotorNode

- **Full Type**: `FlowEngineLib;.CamMotorNode`
- **Implementation File**: `CamMotorNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `RunType` | `CamMotorRunType` | Property |
| `IsAbs` | `bool` | Property |
| `Position` | `int` | Property |
| `Aperture` | `float` | Property |
| `AutoFocusTemp` | `string` | Property |

---

#### 11. CameraROINode

- **Full Type**: `FlowEngineLib.Node.Camera;.CameraROINode`
- **Implementation File**: `Node\Camera\CameraROINode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `ROI_X` | `int` | Property |
| `ROI_Y` | `int` | Property |
| `ROI_Width` | `int` | Property |
| `ROI_Height` | `int` | Property |

---

#### 12. CommCameraNode

- **Full Type**: `FlowEngineLib.Node.Camera;.CommCameraNode`
- **Implementation File**: `Node\Camera\CommCameraNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `IsHDR` | `bool` | Property |
| `CamTempName` | `string` | Property |
| `FlipMode` | `CVImageFlipMode` | Property |
| `IsAutoExp` | `bool` | Property |
| `TempName` | `string` | Property |
| `IsWithND` | `bool` | Property |
| `CalibTempName` | `string` | Property |
| `POITempName` | `string` | Property |
| `POIFilterTempName` | `string` | Property |
| `POIReviseTempName` | `string` | Property |

---

#### 13. LVCameraNode

- **Full Type**: `FlowEngineLib;.LVCameraNode`
- **Implementation File**: `LVCameraNode.cs`
- **Base Class**: `BaseCameraNode`

---

#### 14. LVXRCameraNode

- **Full Type**: `FlowEngineLib;.LVXRCameraNode`
- **Implementation File**: `LVXRCameraNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `AvgCount` | `int` | Property |
| `Gain` | `float` | Property |
| `ExpTime` | `float` | Property |
| `CaliTempName` | `string` | Property |
| `FlipMode` | `CVImageFlipMode` | Property |
| `POITempName` | `string` | Property |
| `POIFilterTempName` | `string` | Property |
| `POIReviseTempName` | `string` | Property |
| `Algorithm` | `AlgorithmARVRType` | Property |
| `XRTempName` | `string` | Property |

---

### Device Nodes (1)

#### 1. PhyDeviceControlNode

- **Full Type**: `FlowEngineLib.Node.Global;.PhyDeviceControlNode`
- **Implementation File**: `Node\Global\PhyDeviceControlNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `DeviceType` | `CVDeviceType` | Property |
| `CmdType` | `CVDeviceControlCmd` | Property |

---

### End Nodes (2)

#### 1. CVEndNode

- **Full Type**: `FlowEngineLib.End;.CVEndNode`
- **Implementation File**: `End\CVEndNode.cs`
- **Base Class**: `CVCommonNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `m_in_start` | `STNodeOption` | Field |

---

#### 2. CVEndV5Node

- **Full Type**: `FlowEngineLib.End;.CVEndV5Node`
- **Implementation File**: `End\CVEndV5Node.cs`
- **Base Class**: `CVCommonNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `m_in_start` | `STNodeOption` | Field |

---

### FW Nodes (1)

#### 1. FWNode

- **Full Type**: `FlowEngineLib;.FWNode`
- **Implementation File**: `FWNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Port` | `int` | Property |
| `ModelType` | `FWModelType` | Property |

---

### Loop Nodes (2)

#### 1. LoopNextNode

- **Full Type**: `FlowEngineLib;.LoopNextNode`
- **Implementation File**: `LoopNextNode.cs`
- **Base Class**: `CVCommonNode`

---

#### 2. LoopNode

- **Full Type**: `FlowEngineLib;.LoopNode`
- **Implementation File**: `LoopNode.cs`
- **Base Class**: `CVCommonNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `BeginVal` | `float` | Property |
| `EndVal` | `float` | Property |
| `StepVal` | `float` | Property |

---

### MQTT Nodes (5)

#### 1. MQTTBaseNode

- **Full Type**: `FlowEngineLib.MQTT;.MQTTBaseNode`
- **Implementation File**: `MQTT\MQTTBaseNode.cs`
- **Base Class**: `STNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Server` | `string` | Property |
| `Port` | `int` | Property |

---

#### 2. MQTTCustomPublishNode

- **Full Type**: `FlowEngineLib;.MQTTCustomPublishNode`
- **Implementation File**: `MQTTCustomPublishNode.cs`
- **Base Class**: `MQTTBaseNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Data` | `string` | Property |
| `Topic` | `string` | Property |

---

#### 3. MQTTCustomSubscribeNode

- **Full Type**: `FlowEngineLib;.MQTTCustomSubscribeNode`
- **Implementation File**: `MQTTCustomSubscribeNode.cs`
- **Base Class**: `MQTTBaseNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Topic` | `string` | Property |

---

#### 4. MQTTStartNode

- **Full Type**: `FlowEngineLib.Start;.MQTTStartNode`
- **Implementation File**: `Start\MQTTStartNode.cs`
- **Base Class**: `BaseStartNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Server` | `string` | Property |
| `Port` | `int` | Property |

---

#### 5. MQTTStartV5Node

- **Full Type**: `FlowEngineLib.Start;.MQTTStartV5Node`
- **Implementation File**: `Start\MQTTStartV5Node.cs`
- **Base Class**: `BaseStartNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Server` | `string` | Property |
| `Port` | `int` | Property |

---

### Manual Nodes (1)

#### 1. ManualConfirmNode

- **Full Type**: `FlowEngineLib;.ManualConfirmNode`
- **Implementation File**: `ManualConfirmNode.cs`
- **Base Class**: `CVCommonNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `MessageText` | `string` | Property |

---

### OLED Nodes (7)

#### 1. OLEDCombineQuaterImages_4In1Node

- **Full Type**: `FlowEngineLib.Node.Algorithm;.OLEDCombineQuaterImages_4In1Node`
- **Implementation File**: `Node\Algorithm\OLEDCombineQuaterImages_4In1Node.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `ImgFileName1` | `string` | Property |
| `ImgFileName2` | `string` | Property |
| `ImgFileName3` | `string` | Property |
| `ImgFileName4` | `string` | Property |
| `OutputFileName` | `string` | Property |

---

#### 2. OLEDFindPixelDefectsForQuardImgNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.OLEDFindPixelDefectsForQuardImgNode`
- **Implementation File**: `Node\Algorithm\OLEDFindPixelDefectsForQuardImgNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `OutputFileName` | `string` | Property |

---

#### 3. OLEDImageCroppingNode

- **Full Type**: `FlowEngineLib.Node.OLED;.OLEDImageCroppingNode`
- **Implementation File**: `Node\OLED\OLEDImageCroppingNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |

---

#### 4. OLEDJNDCalVasNode

- **Full Type**: `FlowEngineLib.Node.OLED;.OLEDJNDCalVasNode`
- **Implementation File**: `Node\OLED\OLEDJNDCalVasNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `OrderIndex` | `int` | Property |
| `TempName` | `string` | Property |
| `Algorithm` | `Algorithm2Type` | Property |
| `BufferLen` | `int` | Property |
| `IsAdd` | `bool` | Property |

---

#### 5. OLEDParticlesFindAndFillNode

- **Full Type**: `FlowEngineLib.Node.Algorithm;.OLEDParticlesFindAndFillNode`
- **Implementation File**: `Node\Algorithm\OLEDParticlesFindAndFillNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ParticlesType` | `ParticlesMode` | Property |
| `ImgFileName` | `string` | Property |
| `OutputFileName` | `string` | Property |

---

#### 6. OLEDRebuildPixelsNode

- **Full Type**: `FlowEngineLib.Node.OLED;.OLEDRebuildPixelsNode`
- **Implementation File**: `Node\OLED\OLEDRebuildPixelsNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Channel` | `CVOLED_Channel` | Property |
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `OutputTemplateName` | `string` | Property |

---

#### 7. OLEDRebuildPixelsPosNode

- **Full Type**: `FlowEngineLib.Node.OLED;.OLEDRebuildPixelsPosNode`
- **Implementation File**: `Node\OLED\OLEDRebuildPixelsPosNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `Channel` | `CVOLED_Channel` | Property |
| `OutputTemplateName` | `string` | Property |

---

### Other Nodes (8)

#### 1. CVBaseServerNode

- **Full Type**: `FlowEngineLib.Base;.CVBaseServerNode`
- **Implementation File**: `Base\CVBaseServerNode.cs`
- **Base Class**: `CVCommonNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Token` | `string` | Property |
| `MaxTime` | `int` | Property |
| `Subtitle` | `string` | Property |

---

#### 2. CVBaseServerNodeHub

- **Full Type**: `FlowEngineLib.Base;.CVBaseServerNodeHub`
- **Implementation File**: `Base\CVBaseServerNodeHub.cs`
- **Base Class**: `CVBaseServerNode`

---

#### 3. CVBaseServerNodeIn2Hub

- **Full Type**: `FlowEngineLib.Base;.CVBaseServerNodeIn2Hub`
- **Implementation File**: `Base\CVBaseServerNodeIn2Hub.cs`
- **Base Class**: `CVBaseServerNode`

---

#### 4. CVCommonNode

- **Full Type**: `FlowEngineLib.Base;.CVCommonNode`
- **Implementation File**: `Base\CVCommonNode.cs`
- **Base Class**: `STNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `NodeName` | `string` | Property |
| `NodeType` | `string` | Property |
| `DeviceCode` | `string` | Property |
| `NodeID` | `string` | Property |
| `ZIndex` | `int` | Property |
| `nodeEvent` | `FlowEngineNodeEvent` | Property |
| `nodeRunEvent` | `FlowEngineNodeRunEvent` | Property |
| `nodeEndEvent` | `FlowEngineNodeEndEvent` | Property |

---

#### 5. Calibration2InNode

- **Full Type**: `FlowEngineLib.Node.OLED;.Calibration2InNode`
- **Implementation File**: `Node\OLED\Calibration2InNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ExpTempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `IsSaveCIE` | `bool` | Property |
| `POIFilterTempName` | `string` | Property |
| `POIReviseTempName` | `string` | Property |
| `OutputTemplateName` | `string` | Property |

---

#### 6. CalibrationNode

- **Full Type**: `FlowEngineLib.Algorithm;.CalibrationNode`
- **Implementation File**: `Algorithm\CalibrationNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ExpTempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `IsSaveCIE` | `bool` | Property |
| `POITempName` | `string` | Property |
| `POIFilterTempName` | `string` | Property |
| `POIReviseTempName` | `string` | Property |
| `OutputTemplateName` | `string` | Property |

---

#### 7. CalibrationROINode

- **Full Type**: `FlowEngineLib.Node.Camera;.CalibrationROINode`
- **Implementation File**: `Node\Camera\CalibrationROINode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `ROI_X` | `int` | Property |
| `ROI_Y` | `int` | Property |
| `ROI_Width` | `int` | Property |
| `ROI_Height` | `int` | Property |

---

#### 8. MotorNode

- **Full Type**: `FlowEngineLib;.MotorNode`
- **Implementation File**: `MotorNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `RunType` | `MotorRunType` | Property |
| `IsAbs` | `bool` | Property |
| `Position` | `int` | Property |
| `Aperture` | `float` | Property |

---

### PG Nodes (1)

#### 1. PGNode

- **Full Type**: `FlowEngineLib.Node.PG;.PGNode`
- **Implementation File**: `Node\PG\PGNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `PGCmd` | `PGCommCmdType` | Property |
| `IndexFrame` | `int` | Property |

---

### POI Nodes (8)

#### 1. BuildPOI2Node

- **Full Type**: `FlowEngineLib.Node.POI;.BuildPOI2Node`
- **Implementation File**: `Node\POI\BuildPOI2Node.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TemplateName` | `string` | Property |
| `BuildType` | `POIBuildType` | Property |
| `PrefixName` | `string` | Property |
| `POIType` | `POIPointTypes` | Property |
| `POIHeight` | `int` | Property |
| `POIWidth` | `int` | Property |
| `ImgFileName` | `string` | Property |
| `POIOutput` | `POIStorageModel` | Property |
| `OutputFileName` | `string` | Property |
| `SavePOITempName` | `string` | Property |

---

#### 2. BuildPOINode

- **Full Type**: `FlowEngineLib;.BuildPOINode`
- **Implementation File**: `BuildPOINode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TemplateName` | `string` | Property |
| `RePOITemplateName` | `string` | Property |
| `LayoutROITemplate` | `string` | Property |
| `BuildType` | `POIBuildType` | Property |
| `PrefixName` | `string` | Property |
| `POIType` | `POIPointTypes` | Property |
| `POIHeight` | `int` | Property |
| `POIWidth` | `int` | Property |
| `ImgFileName` | `string` | Property |
| `CAD_PosFileName` | `string` | Property |
| `POIOutput` | `POIStorageModel` | Property |
| `OutputFileName` | `string` | Property |
| `SavePOITempName` | `string` | Property |
| `BufferLen` | `int` | Property |

---

#### 3. POIAnalysisAndSMUNode

- **Full Type**: `FlowEngineLib.Node.POI;.POIAnalysisAndSMUNode`
- **Implementation File**: `Node\POI\POIAnalysisAndSMUNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |

---

#### 4. POIAnalysisNode

- **Full Type**: `FlowEngineLib.Node.POI;.POIAnalysisNode`
- **Implementation File**: `Node\POI\POIAnalysisNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |

---

#### 5. POICADMappingNode

- **Full Type**: `FlowEngineLib.Node.POI;.POICADMappingNode`
- **Implementation File**: `Node\POI\POICADMappingNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TemplateName` | `string` | Property |
| `MappingType` | `POIBuildType` | Property |
| `CADFileName` | `string` | Property |
| `PrefixName` | `string` | Property |

---

#### 6. POINode

- **Full Type**: `FlowEngineLib;.POINode`
- **Implementation File**: `POINode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `FilterTemplateName` | `string` | Property |
| `ReviseTemplateName` | `string` | Property |
| `OutputTemplateName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `IsCCTWave` | `bool` | Property |
| `IsSubPixel` | `bool` | Property |

---

#### 7. POIReviseNode

- **Full Type**: `FlowEngineLib.Node.POI;.POIReviseNode`
- **Implementation File**: `Node\POI\POIReviseNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TemplateName` | `string` | Property |
| `POIPointName` | `string` | Property |
| `IsSelfResultRevise` | `bool` | Property |

---

#### 8. RealPOINode

- **Full Type**: `FlowEngineLib.Node.POI;.RealPOINode`
- **Implementation File**: `Node\POI\RealPOINode.cs`
- **Base Class**: `CVBaseServerNodeHub`

| Property Name | C# Type | Source |
|--------|---------|------|
| `ImgFileName` | `string` | Property |
| `FilterTemplateName` | `string` | Property |
| `ReviseTemplateName` | `string` | Property |
| `ReviseFileName` | `string` | Property |
| `OutputTemplateName` | `string` | Property |
| `SubPixelTemplateName` | `string` | Property |
| `POIType` | `POIPointTypes` | Property |
| `POIHeight` | `float` | Property |
| `POIWidth` | `float` | Property |
| `IsResultAdd` | `bool` | Property |
| `IsCCTWave` | `bool` | Property |

---

### SMU Nodes (7)

#### 1. SMUBaseNode

- **Full Type**: `FlowEngineLib;.SMUBaseNode`
- **Implementation File**: `SMUBaseNode.cs`
- **Base Class**: `CVBaseServerNode`, `ICVLoopNextNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `LoopName` | `string` | Property |
| `IsCloseOutput` | `bool` | Property |
| `IsStarted` | `bool` | Field |

---

#### 2. SMUFromCSVNode

- **Full Type**: `FlowEngineLib;.SMUFromCSVNode`
- **Implementation File**: `SMUFromCSVNode.cs`
- **Base Class**: `SMUBaseNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Source` | `SourceType` | Property |
| `Channel` | `SMUChannelType` | Property |
| `CsvFileName` | `string` | Property |
| `IsAutoRng` | `bool` | Property |
| `SrcRng` | `double` | Property |
| `LmtRng` | `double` | Property |

---

#### 3. SMUModelNode

- **Full Type**: `FlowEngineLib;.SMUModelNode`
- **Implementation File**: `SMUModelNode.cs`
- **Base Class**: `SMUBaseNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `ModelName` | `string` | Property |

---

#### 4. SMUNode

- **Full Type**: `FlowEngineLib;.SMUNode`
- **Implementation File**: `SMUNode.cs`
- **Base Class**: `SMUBaseNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Source` | `SourceType` | Property |
| `Channel` | `SMUChannelType` | Property |
| `BeginVal` | `float` | Property |
| `EndVal` | `float` | Property |
| `LimitVal` | `float` | Property |
| `PointNum` | `int` | Property |
| `IsAutoRng` | `bool` | Property |
| `SrcRng` | `double` | Property |
| `LmtRng` | `double` | Property |

---

#### 5. SMUReaderNode

- **Full Type**: `FlowEngineLib.Node.SMU;.SMUReaderNode`
- **Implementation File**: `Node\SMU\SMUReaderNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `WaitTime` | `int` | Property |

---

#### 6. SMUSweepModelNode

- **Full Type**: `FlowEngineLib.Node.SMU;.SMUSweepModelNode`
- **Implementation File**: `Node\SMU\SMUSweepModelNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `ModelName` | `string` | Property |
| `IsCloseOutput` | `bool` | Property |

---

#### 7. SMUSweepNode

- **Full Type**: `FlowEngineLib.Node.SMU;.SMUSweepNode`
- **Implementation File**: `Node\SMU\SMUSweepNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `Source` | `SourceType` | Property |
| `Channel` | `SMUChannelType` | Property |
| `BeginVal` | `float` | Property |
| `EndVal` | `float` | Property |
| `LimitVal` | `float` | Property |
| `PointNum` | `int` | Property |
| `IsCloseOutput` | `bool` | Property |
| `IsAutoRng` | `bool` | Property |
| `SrcRng` | `double` | Property |
| `LmtRng` | `double` | Property |

---

### Sensor Nodes (3)

#### 1. CommonSensorNode

- **Full Type**: `FlowEngineLib;.CommonSensorNode`
- **Implementation File**: `CommonSensorNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |
| `CmdType` | `CommCmdType` | Property |
| `CmdSend` | `string` | Property |
| `CmdReceive` | `string` | Property |

---

#### 2. RealCommonSensorNode

- **Full Type**: `FlowEngineLib;.RealCommonSensorNode`
- **Implementation File**: `RealCommonSensorNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `CmdType` | `CommSensorCmdType` | Property |
| `CmdSend` | `string` | Property |
| `CmdReceive` | `string` | Property |
| `CmdTimeout` | `int` | Property |
| `RetryCount` | `int` | Property |
| `Delay` | `int` | Property |

---

#### 3. TempCommonSensorNode

- **Full Type**: `FlowEngineLib;.TempCommonSensorNode`
- **Implementation File**: `TempCommonSensorNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `TempName` | `string` | Property |

---

### Spectrum Nodes (2)

#### 1. SpectrumEQENode

- **Full Type**: `FlowEngineLib.Node.Spectrum;.SpectrumEQENode`
- **Implementation File**: `Node\Spectrum\SpectrumEQENode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `AFactor` | `float` | Property |
| `IsCustomVI` | `bool` | Property |
| `Voltage` | `float` | Property |
| `Current` | `float` | Property |
| `Temp` | `float` | Property |
| `AveNum` | `int` | Property |
| `AutoIntTime` | `bool` | Property |
| `IsWithND` | `bool` | Property |
| `SelfDark` | `bool` | Property |
| `AutoInitDark` | `bool` | Property |
| `OutputDataFilename` | `string` | Property |

---

#### 2. SpectrumNode

- **Full Type**: `FlowEngineLib.Node.Spectrum;.SpectrumNode`
- **Implementation File**: `Node\Spectrum\SpectrumNode.cs`
- **Base Class**: `CVBaseServerNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `PGCmd` | `SPCommCmdType` | Property |
| `Temp` | `float` | Property |
| `AveNum` | `int` | Property |
| `AutoIntTime` | `bool` | Property |
| `IsWithND` | `bool` | Property |
| `SelfDark` | `bool` | Property |
| `AutoInitDark` | `bool` | Property |
| `OutputDataFilename` | `string` | Property |

---

### Start Nodes (3)

#### 1. CVStartCFC

- **Full Type**: `FlowEngineLib.Base;.CVStartCFC`
- **Implementation File**: `Base\CVStartCFC.cs`
- **Base Class**: `CVBaseCFC`

---

#### 2. ManualStartNode

- **Full Type**: `FlowEngineLib;.ManualStartNode`
- **Implementation File**: `ManualStartNode.cs`
- **Base Class**: `BaseStartNode`

| Property Name | C# Type | Source |
|--------|---------|------|
| `SN` | `string` | Property |
| `Action` | `ActionTypeEnum` | Property |

---

#### 3. ModbusStartNode

- **Full Type**: `FlowEngineLib.Start;.ModbusStartNode`
- **Implementation File**: `Start\ModbusStartNode.cs`
- **Base Class**: `BaseStartNode`

---

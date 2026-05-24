# Flow Engine 結點全集

> 自動生成於 2026-05-22，包含所有在 `FlowEngineLib` 中定義的結點類。

## 概覽

共 **90** 個結點類，按型別分組如下：

| 型別 | 數量 |
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

## 結點清單

| 類別 | 結點類名 | 基類 | 檔案 | 屬性數 |
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

## 各類別結點詳細屬性

### Algorithm 類結點 (25 個)

#### 1. AlgComplianceContrastNode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgComplianceContrastNode`
- **實現檔案**: `Node\Algorithm\AlgComplianceContrastNode.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `Operation` | `OperationType` | Property |
| `TempName` | `string` | Property |

---

#### 2. AlgComplianceJudgmentNode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgComplianceJudgmentNode`
- **實現檔案**: `Node\Algorithm\AlgComplianceJudgmentNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `IsBreak` | `bool` | Property |

---

#### 3. AlgComplianceMathNode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgComplianceMathNode`
- **實現檔案**: `Node\Algorithm\AlgComplianceMathNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ComplianceMath` | `ComplianceMathType` | Property |
| `IsBreak` | `bool` | Property |

---

#### 4. AlgDataConvertNode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgDataConvertNode`
- **實現檔案**: `Node\Algorithm\AlgDataConvertNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `MethodType` | `CVDataConvertMethodType` | Property |
| `TempName` | `string` | Property |
| `InType` | `CVDataConvertInputType` | Property |
| `OutType` | `CVDataConvertOutputType` | Property |

---

#### 5. AlgDataLoadNode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgDataLoadNode`
- **實現檔案**: `Node\Algorithm\AlgDataLoadNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |

---

#### 6. AlgDataLoadNode2

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgDataLoadNode2`
- **實現檔案**: `Node\Algorithm\AlgDataLoadNode2.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `DataDeviceCode` | `string` | Property |
| `SerialNumber` | `string` | Property |
| `ResultType` | `CVResultType` | Property |
| `DataZIndex` | `int` | Property |

---

#### 7. Algorithm2InNode

- **完整型別**: `FlowEngineLib.Node.OLED;.Algorithm2InNode`
- **實現檔案**: `Node\OLED\Algorithm2InNode.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |
| `Algorithm` | `Algorithm2Type` | Property |
| `BufferLen` | `int` | Property |
| `IsAdd` | `bool` | Property |

---

#### 8. AlgorithmARVRNode

- **完整型別**: `FlowEngineLib.Algorithm;.AlgorithmARVRNode`
- **實現檔案**: `Algorithm\AlgorithmARVRNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `Algorithm` | `AlgorithmARVRType` | Property |
| `TempName` | `string` | Property |
| `POITempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `Color` | `CVOLED_COLOR` | Property |
| `BufferLen` | `int` | Property |

---

#### 9. AlgorithmBlackMuraNode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgorithmBlackMuraNode`
- **實現檔案**: `Node\Algorithm\AlgorithmBlackMuraNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `OIndex` | `string` | Property |
| `SavePOITempName` | `string` | Property |

---

#### 10. AlgorithmCaliNode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgorithmCaliNode`
- **實現檔案**: `Node\Algorithm\AlgorithmCaliNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `OutputFileName` | `string` | Property |

---

#### 11. AlgorithmCompoundImgNode

- **完整型別**: `FlowEngineLib.Node.OLED;.AlgorithmCompoundImgNode`
- **實現檔案**: `Node\OLED\AlgorithmCompoundImgNode.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |
| `OutputFileName` | `string` | Property |
| `BufferLen` | `int` | Property |

---

#### 12. AlgorithmEQENode

- **完整型別**: `FlowEngineLib;.AlgorithmEQENode`
- **實現檔案**: `AlgorithmEQENode.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |

---

#### 13. AlgorithmFindLEDNode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgorithmFindLEDNode`
- **實現檔案**: `Node\Algorithm\AlgorithmFindLEDNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgorithmFindLightAreaNode`
- **實現檔案**: `Node\Algorithm\AlgorithmFindLightAreaNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `SavePOITempName` | `string` | Property |
| `BufferLen` | `int` | Property |
| `OIndex` | `string` | Property |

---

#### 15. AlgorithmGhostV2Node

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgorithmGhostV2Node`
- **實現檔案**: `Node\Algorithm\AlgorithmGhostV2Node.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `BufferLen` | `int` | Property |

---

#### 16. AlgorithmImageConvertNode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgorithmImageConvertNode`
- **實現檔案**: `Node\Algorithm\AlgorithmImageConvertNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `ImageFormat` | `ImageFormatType` | Property |
| `ImgFileName` | `string` | Property |
| `Channel` | `CVOLED_Channel` | Property |

---

#### 17. AlgorithmImageROINode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgorithmImageROINode`
- **實現檔案**: `Node\Algorithm\AlgorithmImageROINode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `OutputFileName` | `string` | Property |

---

#### 18. AlgorithmKBNode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgorithmKBNode`
- **實現檔案**: `Node\Algorithm\AlgorithmKBNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |

---

#### 19. AlgorithmKBOutputNode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgorithmKBOutputNode`
- **實現檔案**: `Node\Algorithm\AlgorithmKBOutputNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |

---

#### 20. AlgorithmNode

- **完整型別**: `FlowEngineLib.Algorithm;.AlgorithmNode`
- **實現檔案**: `Algorithm\AlgorithmNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `Algorithm` | `AlgorithmType` | Property |
| `TempName` | `string` | Property |
| `POITempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `Color` | `CVOLED_COLOR` | Property |
| `BufferLen` | `int` | Property |

---

#### 21. AlgorithmOLEDNode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgorithmOLEDNode`
- **實現檔案**: `Node\Algorithm\AlgorithmOLEDNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgorithmOLED_AOINode`
- **實現檔案**: `Node\Algorithm\AlgorithmOLED_AOINode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib.Node.Algorithm;.AlgorithmTMNode`
- **實現檔案**: `Node\Algorithm\AlgorithmTMNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |
| `TemplateFile` | `string` | Property |
| `ImgFileName` | `string` | Property |

---

#### 24. TPAlgorithm2Node

- **完整型別**: `FlowEngineLib.Node.Algorithm;.TPAlgorithm2Node`
- **實現檔案**: `Node\Algorithm\TPAlgorithm2Node.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `Operator` | `string` | Property |
| `TempName` | `string` | Property |

---

#### 25. TPAlgorithmNode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.TPAlgorithmNode`
- **實現檔案**: `Node\Algorithm\TPAlgorithmNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `Algorithm` | `TPAlgorithmType` | Property |
| `Operator` | `string` | Property |
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |

---

### Camera 類結點 (14 個)

#### 1. AOILocAndRegPixelsCameraNode

- **完整型別**: `FlowEngineLib;.AOILocAndRegPixelsCameraNode`
- **實現檔案**: `AOILocAndRegPixelsCameraNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib;.AOILocatePixelsCameraNode`
- **實現檔案**: `AOILocatePixelsCameraNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib;.AOIRegisterPixelsCameraNode`
- **實現檔案**: `AOIRegisterPixelsCameraNode.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib;.BaseCameraNode`
- **實現檔案**: `BaseCameraNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib.Node.Camera;.CVAOI2CameraNode`
- **實現檔案**: `Node\Camera\CVAOI2CameraNode.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib.Node.Camera;.CVAOICameraNode`
- **實現檔案**: `Node\Camera\CVAOICameraNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib;.CVCameraDataFlow`
- **實現檔案**: `CVCameraDataFlow.cs`
- **基類**: `CVMQTTRequest`

---

#### 8. CVCameraNode

- **完整型別**: `FlowEngineLib;.CVCameraNode`
- **實現檔案**: `CVCameraNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib;.CVXRCameraNode`
- **實現檔案**: `CVXRCameraNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib;.CamMotorNode`
- **實現檔案**: `CamMotorNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `RunType` | `CamMotorRunType` | Property |
| `IsAbs` | `bool` | Property |
| `Position` | `int` | Property |
| `Aperture` | `float` | Property |
| `AutoFocusTemp` | `string` | Property |

---

#### 11. CameraROINode

- **完整型別**: `FlowEngineLib.Node.Camera;.CameraROINode`
- **實現檔案**: `Node\Camera\CameraROINode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `ROI_X` | `int` | Property |
| `ROI_Y` | `int` | Property |
| `ROI_Width` | `int` | Property |
| `ROI_Height` | `int` | Property |

---

#### 12. CommCameraNode

- **完整型別**: `FlowEngineLib.Node.Camera;.CommCameraNode`
- **實現檔案**: `Node\Camera\CommCameraNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib;.LVCameraNode`
- **實現檔案**: `LVCameraNode.cs`
- **基類**: `BaseCameraNode`

---

#### 14. LVXRCameraNode

- **完整型別**: `FlowEngineLib;.LVXRCameraNode`
- **實現檔案**: `LVXRCameraNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

### Device 類結點 (1 個)

#### 1. PhyDeviceControlNode

- **完整型別**: `FlowEngineLib.Node.Global;.PhyDeviceControlNode`
- **實現檔案**: `Node\Global\PhyDeviceControlNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `DeviceType` | `CVDeviceType` | Property |
| `CmdType` | `CVDeviceControlCmd` | Property |

---

### End 類結點 (2 個)

#### 1. CVEndNode

- **完整型別**: `FlowEngineLib.End;.CVEndNode`
- **實現檔案**: `End\CVEndNode.cs`
- **基類**: `CVCommonNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `m_in_start` | `STNodeOption` | Field |

---

#### 2. CVEndV5Node

- **完整型別**: `FlowEngineLib.End;.CVEndV5Node`
- **實現檔案**: `End\CVEndV5Node.cs`
- **基類**: `CVCommonNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `m_in_start` | `STNodeOption` | Field |

---

### FW 類結點 (1 個)

#### 1. FWNode

- **完整型別**: `FlowEngineLib;.FWNode`
- **實現檔案**: `FWNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `Port` | `int` | Property |
| `ModelType` | `FWModelType` | Property |

---

### Loop 類結點 (2 個)

#### 1. LoopNextNode

- **完整型別**: `FlowEngineLib;.LoopNextNode`
- **實現檔案**: `LoopNextNode.cs`
- **基類**: `CVCommonNode`

---

#### 2. LoopNode

- **完整型別**: `FlowEngineLib;.LoopNode`
- **實現檔案**: `LoopNode.cs`
- **基類**: `CVCommonNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `BeginVal` | `float` | Property |
| `EndVal` | `float` | Property |
| `StepVal` | `float` | Property |

---

### MQTT 類結點 (5 個)

#### 1. MQTTBaseNode

- **完整型別**: `FlowEngineLib.MQTT;.MQTTBaseNode`
- **實現檔案**: `MQTT\MQTTBaseNode.cs`
- **基類**: `STNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `Server` | `string` | Property |
| `Port` | `int` | Property |

---

#### 2. MQTTCustomPublishNode

- **完整型別**: `FlowEngineLib;.MQTTCustomPublishNode`
- **實現檔案**: `MQTTCustomPublishNode.cs`
- **基類**: `MQTTBaseNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `Data` | `string` | Property |
| `Topic` | `string` | Property |

---

#### 3. MQTTCustomSubscribeNode

- **完整型別**: `FlowEngineLib;.MQTTCustomSubscribeNode`
- **實現檔案**: `MQTTCustomSubscribeNode.cs`
- **基類**: `MQTTBaseNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `Topic` | `string` | Property |

---

#### 4. MQTTStartNode

- **完整型別**: `FlowEngineLib.Start;.MQTTStartNode`
- **實現檔案**: `Start\MQTTStartNode.cs`
- **基類**: `BaseStartNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `Server` | `string` | Property |
| `Port` | `int` | Property |

---

#### 5. MQTTStartV5Node

- **完整型別**: `FlowEngineLib.Start;.MQTTStartV5Node`
- **實現檔案**: `Start\MQTTStartV5Node.cs`
- **基類**: `BaseStartNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `Server` | `string` | Property |
| `Port` | `int` | Property |

---

### Manual 類結點 (1 個)

#### 1. ManualConfirmNode

- **完整型別**: `FlowEngineLib;.ManualConfirmNode`
- **實現檔案**: `ManualConfirmNode.cs`
- **基類**: `CVCommonNodeHub`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `MessageText` | `string` | Property |

---

### OLED 類結點 (7 個)

#### 1. OLEDCombineQuaterImages_4In1Node

- **完整型別**: `FlowEngineLib.Node.Algorithm;.OLEDCombineQuaterImages_4In1Node`
- **實現檔案**: `Node\Algorithm\OLEDCombineQuaterImages_4In1Node.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `ImgFileName1` | `string` | Property |
| `ImgFileName2` | `string` | Property |
| `ImgFileName3` | `string` | Property |
| `ImgFileName4` | `string` | Property |
| `OutputFileName` | `string` | Property |

---

#### 2. OLEDFindPixelDefectsForQuardImgNode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.OLEDFindPixelDefectsForQuardImgNode`
- **實現檔案**: `Node\Algorithm\OLEDFindPixelDefectsForQuardImgNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `OutputFileName` | `string` | Property |

---

#### 3. OLEDImageCroppingNode

- **完整型別**: `FlowEngineLib.Node.OLED;.OLEDImageCroppingNode`
- **實現檔案**: `Node\OLED\OLEDImageCroppingNode.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |

---

#### 4. OLEDJNDCalVasNode

- **完整型別**: `FlowEngineLib.Node.OLED;.OLEDJNDCalVasNode`
- **實現檔案**: `Node\OLED\OLEDJNDCalVasNode.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `OrderIndex` | `int` | Property |
| `TempName` | `string` | Property |
| `Algorithm` | `Algorithm2Type` | Property |
| `BufferLen` | `int` | Property |
| `IsAdd` | `bool` | Property |

---

#### 5. OLEDParticlesFindAndFillNode

- **完整型別**: `FlowEngineLib.Node.Algorithm;.OLEDParticlesFindAndFillNode`
- **實現檔案**: `Node\Algorithm\OLEDParticlesFindAndFillNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ParticlesType` | `ParticlesMode` | Property |
| `ImgFileName` | `string` | Property |
| `OutputFileName` | `string` | Property |

---

#### 6. OLEDRebuildPixelsNode

- **完整型別**: `FlowEngineLib.Node.OLED;.OLEDRebuildPixelsNode`
- **實現檔案**: `Node\OLED\OLEDRebuildPixelsNode.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `Channel` | `CVOLED_Channel` | Property |
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `OutputTemplateName` | `string` | Property |

---

#### 7. OLEDRebuildPixelsPosNode

- **完整型別**: `FlowEngineLib.Node.OLED;.OLEDRebuildPixelsPosNode`
- **實現檔案**: `Node\OLED\OLEDRebuildPixelsPosNode.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |
| `ImgFileName` | `string` | Property |
| `Channel` | `CVOLED_Channel` | Property |
| `OutputTemplateName` | `string` | Property |

---

### Other 類結點 (8 個)

#### 1. CVBaseServerNode

- **完整型別**: `FlowEngineLib.Base;.CVBaseServerNode`
- **實現檔案**: `Base\CVBaseServerNode.cs`
- **基類**: `CVCommonNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `Token` | `string` | Property |
| `MaxTime` | `int` | Property |
| `Subtitle` | `string` | Property |

---

#### 2. CVBaseServerNodeHub

- **完整型別**: `FlowEngineLib.Base;.CVBaseServerNodeHub`
- **實現檔案**: `Base\CVBaseServerNodeHub.cs`
- **基類**: `CVBaseServerNode`

---

#### 3. CVBaseServerNodeIn2Hub

- **完整型別**: `FlowEngineLib.Base;.CVBaseServerNodeIn2Hub`
- **實現檔案**: `Base\CVBaseServerNodeIn2Hub.cs`
- **基類**: `CVBaseServerNode`

---

#### 4. CVCommonNode

- **完整型別**: `FlowEngineLib.Base;.CVCommonNode`
- **實現檔案**: `Base\CVCommonNode.cs`
- **基類**: `STNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib.Node.OLED;.Calibration2InNode`
- **實現檔案**: `Node\OLED\Calibration2InNode.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib.Algorithm;.CalibrationNode`
- **實現檔案**: `Algorithm\CalibrationNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib.Node.Camera;.CalibrationROINode`
- **實現檔案**: `Node\Camera\CalibrationROINode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `ROI_X` | `int` | Property |
| `ROI_Y` | `int` | Property |
| `ROI_Width` | `int` | Property |
| `ROI_Height` | `int` | Property |

---

#### 8. MotorNode

- **完整型別**: `FlowEngineLib;.MotorNode`
- **實現檔案**: `MotorNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `RunType` | `MotorRunType` | Property |
| `IsAbs` | `bool` | Property |
| `Position` | `int` | Property |
| `Aperture` | `float` | Property |

---

### PG 類結點 (1 個)

#### 1. PGNode

- **完整型別**: `FlowEngineLib.Node.PG;.PGNode`
- **實現檔案**: `Node\PG\PGNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `PGCmd` | `PGCommCmdType` | Property |
| `IndexFrame` | `int` | Property |

---

### POI 類結點 (8 個)

#### 1. BuildPOI2Node

- **完整型別**: `FlowEngineLib.Node.POI;.BuildPOI2Node`
- **實現檔案**: `Node\POI\BuildPOI2Node.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib;.BuildPOINode`
- **實現檔案**: `BuildPOINode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib.Node.POI;.POIAnalysisAndSMUNode`
- **實現檔案**: `Node\POI\POIAnalysisAndSMUNode.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |

---

#### 4. POIAnalysisNode

- **完整型別**: `FlowEngineLib.Node.POI;.POIAnalysisNode`
- **實現檔案**: `Node\POI\POIAnalysisNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |

---

#### 5. POICADMappingNode

- **完整型別**: `FlowEngineLib.Node.POI;.POICADMappingNode`
- **實現檔案**: `Node\POI\POICADMappingNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TemplateName` | `string` | Property |
| `MappingType` | `POIBuildType` | Property |
| `CADFileName` | `string` | Property |
| `PrefixName` | `string` | Property |

---

#### 6. POINode

- **完整型別**: `FlowEngineLib;.POINode`
- **實現檔案**: `POINode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib.Node.POI;.POIReviseNode`
- **實現檔案**: `Node\POI\POIReviseNode.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TemplateName` | `string` | Property |
| `POIPointName` | `string` | Property |
| `IsSelfResultRevise` | `bool` | Property |

---

#### 8. RealPOINode

- **完整型別**: `FlowEngineLib.Node.POI;.RealPOINode`
- **實現檔案**: `Node\POI\RealPOINode.cs`
- **基類**: `CVBaseServerNodeHub`

| 屬性名 | C# 型別 | 來源 |
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

### SMU 類結點 (7 個)

#### 1. SMUBaseNode

- **完整型別**: `FlowEngineLib;.SMUBaseNode`
- **實現檔案**: `SMUBaseNode.cs`
- **基類**: `CVBaseServerNode`, `ICVLoopNextNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `LoopName` | `string` | Property |
| `IsCloseOutput` | `bool` | Property |
| `IsStarted` | `bool` | Field |

---

#### 2. SMUFromCSVNode

- **完整型別**: `FlowEngineLib;.SMUFromCSVNode`
- **實現檔案**: `SMUFromCSVNode.cs`
- **基類**: `SMUBaseNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `Source` | `SourceType` | Property |
| `Channel` | `SMUChannelType` | Property |
| `CsvFileName` | `string` | Property |
| `IsAutoRng` | `bool` | Property |
| `SrcRng` | `double` | Property |
| `LmtRng` | `double` | Property |

---

#### 3. SMUModelNode

- **完整型別**: `FlowEngineLib;.SMUModelNode`
- **實現檔案**: `SMUModelNode.cs`
- **基類**: `SMUBaseNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `ModelName` | `string` | Property |

---

#### 4. SMUNode

- **完整型別**: `FlowEngineLib;.SMUNode`
- **實現檔案**: `SMUNode.cs`
- **基類**: `SMUBaseNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib.Node.SMU;.SMUReaderNode`
- **實現檔案**: `Node\SMU\SMUReaderNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `WaitTime` | `int` | Property |

---

#### 6. SMUSweepModelNode

- **完整型別**: `FlowEngineLib.Node.SMU;.SMUSweepModelNode`
- **實現檔案**: `Node\SMU\SMUSweepModelNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `ModelName` | `string` | Property |
| `IsCloseOutput` | `bool` | Property |

---

#### 7. SMUSweepNode

- **完整型別**: `FlowEngineLib.Node.SMU;.SMUSweepNode`
- **實現檔案**: `Node\SMU\SMUSweepNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

### Sensor 類結點 (3 個)

#### 1. CommonSensorNode

- **完整型別**: `FlowEngineLib;.CommonSensorNode`
- **實現檔案**: `CommonSensorNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |
| `CmdType` | `CommCmdType` | Property |
| `CmdSend` | `string` | Property |
| `CmdReceive` | `string` | Property |

---

#### 2. RealCommonSensorNode

- **完整型別**: `FlowEngineLib;.RealCommonSensorNode`
- **實現檔案**: `RealCommonSensorNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `CmdType` | `CommSensorCmdType` | Property |
| `CmdSend` | `string` | Property |
| `CmdReceive` | `string` | Property |
| `CmdTimeout` | `int` | Property |
| `RetryCount` | `int` | Property |
| `Delay` | `int` | Property |

---

#### 3. TempCommonSensorNode

- **完整型別**: `FlowEngineLib;.TempCommonSensorNode`
- **實現檔案**: `TempCommonSensorNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `TempName` | `string` | Property |

---

### Spectrum 類結點 (2 個)

#### 1. SpectrumEQENode

- **完整型別**: `FlowEngineLib.Node.Spectrum;.SpectrumEQENode`
- **實現檔案**: `Node\Spectrum\SpectrumEQENode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

- **完整型別**: `FlowEngineLib.Node.Spectrum;.SpectrumNode`
- **實現檔案**: `Node\Spectrum\SpectrumNode.cs`
- **基類**: `CVBaseServerNode`

| 屬性名 | C# 型別 | 來源 |
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

### Start 類結點 (3 個)

#### 1. CVStartCFC

- **完整型別**: `FlowEngineLib.Base;.CVStartCFC`
- **實現檔案**: `Base\CVStartCFC.cs`
- **基類**: `CVBaseCFC`

---

#### 2. ManualStartNode

- **完整型別**: `FlowEngineLib;.ManualStartNode`
- **實現檔案**: `ManualStartNode.cs`
- **基類**: `BaseStartNode`

| 屬性名 | C# 型別 | 來源 |
|--------|---------|------|
| `SN` | `string` | Property |
| `Action` | `ActionTypeEnum` | Property |

---

#### 3. ModbusStartNode

- **完整型別**: `FlowEngineLib.Start;.ModbusStartNode`
- **實現檔案**: `Start\ModbusStartNode.cs`
- **基類**: `BaseStartNode`

---

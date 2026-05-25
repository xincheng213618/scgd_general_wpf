# フロー エンジン ノードの完全なセット

> 2026 年 5 月 22 日に自動的に生成され、`FlowEngineLib` で定義されたすべてのノード クラスが含まれます。

## 概要

合計で **90** のノード クラスがあり、次のようにタイプ別にグループ化されています。

|タイプ |数量 |
|------|------|
|アルゴリズム | 25 |
|カメラ | 14 |
| POI | 8 |
|その他 | 8 |
| SMU | 7 |
|有機EL | 7 |
| MQTT | 5 |
|センサー | 3 |
|開始 | 3 |
|ループ | 2 |
|終わり | 2 |
|スペクトル | 2 |
| FW | 1 |
|マニュアル | 1 |
|デバイス|1|
| PG | 1 |

## ノードリスト| 类别 | 结点类名 | 基类 | 文例 | プロパティ数 |
|------|----------|------|------|----------|
|アルゴリズム | `AlgComplianceContrastNode` | `CVBaseServerNodeHub` | `Node\Algorithm\AlgComplianceContrastNode.cs` | 2 |
|アルゴリズム | `AlgComplianceJudgmentNode` | `CVBaseServerNode` | `Node\Algorithm\AlgComplianceJudgmentNode.cs` | 1 |
|アルゴリズム | `AlgComplianceMathNode` | `CVBaseServerNode` | `Node\Algorithm\AlgComplianceMathNode.cs` | 3 |
|アルゴリズム | `AlgDataConvertNode` | `CVBaseServerNode` | `Node\Algorithm\AlgDataConvertNode.cs` | 4 |
|アルゴリズム | `AlgDataLoadNode` | `CVBaseServerNode` | `Node\Algorithm\AlgDataLoadNode.cs` | 1 |
|アルゴリズム | `AlgDataLoadNode2` | `CVBaseServerNode` | `Node\Algorithm\AlgDataLoadNode2.cs` | 4 |
|アルゴリズム | `Algorithm2InNode` | `CVBaseServerNodeHub` | `Node\OLED\Algorithm2InNode.cs` | 4 |
|アルゴリズム | `AlgorithmARVRNode` | `CVBaseServerNode` | `Algorithm\AlgorithmARVRNode.cs` | 6 |
|アルゴリズム | `AlgorithmBlackMuraNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmBlackMuraNode.cs` | 4 |
|アルゴリズム | `AlgorithmCaliNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmCaliNode.cs` | 3 |
|アルゴリズム | `AlgorithmCompoundImgNode` | `CVBaseServerNodeHub` | `Node\OLED\AlgorithmCompoundImgNode.cs` | 3 |
|アルゴリズム | `AlgorithmEQENode` | `CVBaseServerNodeHub` | `AlgorithmEQENode.cs` | 1 |
|アルゴリズム | `AlgorithmFindLEDNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmFindLEDNode.cs` | 7 |
|アルゴリズム | `AlgorithmFindLightAreaNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmFindLightAreaNode.cs` | 5 |
|アルゴリズム | `AlgorithmGhostV2Node` | `CVBaseServerNodeHub` | `Node\Algorithm\AlgorithmGhostV2Node.cs` | 3 |
|アルゴリズム | `AlgorithmImageConvertNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmImageConvertNode.cs` | 3 |
|アルゴリズム | `AlgorithmImageROINode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmImageROINode.cs` | 3 |
|アルゴリズム | `AlgorithmKBNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmKBNode.cs` | 2 |
|アルゴリズム | `AlgorithmKBOutputNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmKBOutputNode.cs` | 1 |
|アルゴリズム | `AlgorithmNode` | `CVBaseServerNode` | `Algorithm\AlgorithmNode.cs` | 6 |
|アルゴリズム | `AlgorithmOLEDNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmOLEDNode.cs` | 8 |
|アルゴリズム | `AlgorithmOLED_AOINode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmOLED_AOINode.cs` | 8 |
|アルゴリズム | `AlgorithmTMNode` | `CVBaseServerNode` | `Node\Algorithm\AlgorithmTMNode.cs` | 3 |
|アルゴリズム | `TPAlgorithm2Node` | `CVBaseServerNodeHub` | `Node\Algorithm\TPAlgorithm2Node.cs` | 2 |
|アルゴリズム | `TPAlgorithmNode` | `CVBaseServerNode` | `Node\Algorithm\TPAlgorithmNode.cs` | 4 |
|カメラ | `AOILocAndRegPixelsCameraNode` | `CVBaseServerNode` | `AOILocAndRegPixelsCameraNode.cs` | 13 |
|カメラ | `AOILocatePixelsCameraNode` | `CVBaseServerNode` | `AOILocatePixelsCameraNode.cs` | 12 |
|カメラ | `AOIRegisterPixelsCameraNode` | `CVBaseServerNodeHub` | `AOIRegisterPixelsCameraNode.cs` | 13 |
|カメラ | `BaseCameraNode` | `CVBaseServerNode` | `BaseCameraNode.cs` | 8 |
|カメラ | `CVAOI2CameraNode` | `CVBaseServerNodeHub` | `Node\Camera\CVAOI2CameraNode.cs` | 9 |
|カメラ | `CVAOICameraNode` | `CVBaseServerNode` | `Node\Camera\CVAOICameraNode.cs` | 9 |
|カメラ | `CVCameraDataFlow` | `CVMQTTRequest` | `CVCameraDataFlow.cs` | 0 |
|カメラ | `CVCameraNode` | `CVBaseServerNode` | `CVCameraNode.cs` | 11 |
|カメラ | `CVXRCameraNode` | `CVBaseServerNode` | `CVXRCameraNode.cs` | 12 |
|カメラ | `CamMotorNode` | `CVBaseServerNode` | `CamMotorNode.cs` | 5 |
|カメラ | `CameraROINode` | `CVBaseServerNode` | `Node\Camera\CameraROINode.cs` | 4 |
|カメラ | `CommCameraNode` | `CVBaseServerNode` | `Node\Camera\CommCameraNode.cs` | 10 |
|カメラ | `LVCameraNode` | `BaseCameraNode` | `LVCameraNode.cs` | 0 |
|カメラ | `LVXRCameraNode` | `CVBaseServerNode` | `LVXRCameraNode.cs` | 10 |
|デバイス | `PhyDeviceControlNode` | `CVBaseServerNode` | `Node\Global\PhyDeviceControlNode.cs` | 2 |
|終わり | `CVEndNode` | `CVCommonNode` | `End\CVEndNode.cs` | 1 |
|終わり | `CVEndV5Node` | `CVCommonNode` | `End\CVEndV5Node.cs` | 1 |
| FW | `FWNode` | `CVBaseServerNode` | `FWNode.cs` | 2 |
|ループ | `LoopNextNode` | `CVCommonNode` | `LoopNextNode.cs` | 0 |
|ループ | `LoopNode` | `CVCommonNode` | `LoopNode.cs` | 3 |
| MQTT | `MQTTBaseNode` | `STNode` | `MQTT\MQTTBaseNode.cs` | 2 |
| MQTT | `MQTTCustomPublishNode` | `MQTTBaseNode` | `MQTTCustomPublishNode.cs` | 2 |
| MQTT | `MQTTCustomSubscribeNode` | `MQTTBaseNode` | `MQTTCustomSubscribeNode.cs` | 1 |
| MQTT | `MQTTStartNode` | `BaseStartNode` | `Start\MQTTStartNode.cs` | 2 |
| MQTT | `MQTTStartV5Node` | `BaseStartNode` | `Start\MQTTStartV5Node.cs` | 2 |
|マニュアル | `ManualConfirmNode` | `CVCommonNodeHub` | `ManualConfirmNode.cs` | 1 |
|有機EL | `OLEDCombineQuaterImages_4In1Node` | `CVBaseServerNodeHub` | `Node\Algorithm\OLEDCombineQuaterImages_4In1Node.cs` | 5 |
|有機EL | `OLEDFindPixelDefectsForQuardImgNode` | `CVBaseServerNode` | `Node\Algorithm\OLEDFindPixelDefectsForQuardImgNode.cs` | 3 |
|有機EL | `OLEDImageCroppingNode` | `CVBaseServerNodeHub` | `Node\OLED\OLEDImageCroppingNode.cs` | 2 |
|有機EL | `OLEDJNDCalVasNode` | `CVBaseServerNodeHub` | `Node\OLED\OLEDJNDCalVasNode.cs` | 5 |
|有機EL | `OLEDParticlesFindAndFillNode` | `CVBaseServerNode` | `Node\Algorithm\OLEDParticlesFindAndFillNode.cs` | 4 |
|有機EL | `OLEDRebuildPixelsNode` | `CVBaseServerNodeHub` | `Node\OLED\OLEDRebuildPixelsNode.cs` | 4 |
|有機EL | `OLEDRebuildPixelsPosNode` | `CVBaseServerNodeHub` | `Node\OLED\OLEDRebuildPixelsPosNode.cs` | 4 |
|その他 | `CVBaseServerNode` | `CVCommonNode` | `Base\CVBaseServerNode.cs` | 3 |
|その他 | `CVBaseServerNodeHub` | `CVBaseServerNode` | `Base\CVBaseServerNodeHub.cs` | 0 |
|その他 | `CVBaseServerNodeIn2Hub` | `CVBaseServerNode` | `Base\CVBaseServerNodeIn2Hub.cs` | 0 |
|その他 | `CVCommonNode` | `STNode` | `Base\CVCommonNode.cs` | 8 |
|その他 | `Calibration2InNode` | `CVBaseServerNodeHub` | `Node\OLED\Calibration2InNode.cs` | 7 |
|その他 | `CalibrationNode` | `CVBaseServerNode` | `Algorithm\CalibrationNode.cs` | 8 |
|その他 | `CalibrationROINode` | `CVBaseServerNode` | `Node\Camera\CalibrationROINode.cs` | 4 |
|その他 | `MotorNode` | `CVBaseServerNode` | `MotorNode.cs` | 4 |
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
|センサー | `CommonSensorNode` | `CVBaseServerNode` | `CommonSensorNode.cs` | 4 |
|センサー | `RealCommonSensorNode` | `CVBaseServerNode` | `RealCommonSensorNode.cs` | 6 |
|センサー | `TempCommonSensorNode` | `CVBaseServerNode` | `TempCommonSensorNode.cs` | 1 |
|スペクトル | `SpectrumEQENode` | `CVBaseServerNode` | `Node\Spectrum\SpectrumEQENode.cs` | 11 |
|スペクトル | `SpectrumNode` | `CVBaseServerNode` | `Node\Spectrum\SpectrumNode.cs` | 8 |
|開始 | `CVStartCFC` | `CVBaseCFC` | `Base\CVStartCFC.cs` | 0 |
|開始 | `ManualStartNode` | `BaseStartNode` | `ManualStartNode.cs` | 2 |
|開始 | `ModbusStartNode` | `BaseStartNode` | `Start\ModbusStartNode.cs` | 0 |## ノードの各カテゴリの詳細な属性

### アルゴリズム クラス ノード (25)

#### 1. AlgComplianceContrastNode

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm;.AlgComplianceContrastNode`
- **実装ファイル**: `Node\Algorithm\AlgComplianceContrastNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Operation` | `OperationType` |プロパティ |
| `TempName` | `string` |プロパティ |

---

#### 2. AlgComplianceJudgmentNode

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm;.AlgComplianceJudgmentNode`
- **実装ファイル**: `Node\Algorithm\AlgComplianceJudgmentNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `IsBreak` | `bool` |プロパティ |

---

#### 3. AlgComplianceMathNode

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm;.AlgComplianceMathNode`
- **実装ファイル**: `Node\Algorithm\AlgComplianceMathNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `ComplianceMath` | `ComplianceMathType` |プロパティ |
| `IsBreak` | `bool` |プロパティ |

---

#### 4. AlgDataConvertNode

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm;.AlgDataConvertNode`
- **実装ファイル**: `Node\Algorithm\AlgDataConvertNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `MethodType` | `CVDataConvertMethodType` |プロパティ |
| `TempName` | `string` |プロパティ |
| `InType` | `CVDataConvertInputType` |プロパティ |
| `OutType` | `CVDataConvertOutputType` |プロパティ |

---

#### 5. AlgDataLoadNode

- **フルタイプ**: `FlowEngineLib.Node.Algorithm;.AlgDataLoadNode`
- **実装ファイル**: `Node\Algorithm\AlgDataLoadNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |

---

#### 6. AlgDataLoadNode2

- **フルタイプ**: `FlowEngineLib.Node.Algorithm;.AlgDataLoadNode2`
- **実装ファイル**: `Node\Algorithm\AlgDataLoadNode2.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `DataDeviceCode` | `string` |プロパティ |
| `SerialNumber` | `string` |プロパティ |
| `ResultType` | `CVResultType` |プロパティ |
| `DataZIndex` | `int` |プロパティ |

---

#### 7. アルゴリズム 2InNode

- **完全なタイプ**: `FlowEngineLib.Node.OLED;.Algorithm2InNode`
- **実装ファイル**: `Node\OLED\Algorithm2InNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `Algorithm` | `Algorithm2Type` |プロパティ |
| `BufferLen` | `int` |プロパティ |
| `IsAdd` | `bool` |プロパティ |

---

#### 8. アルゴリズムARVRNode

- **フルタイプ**: `FlowEngineLib.Algorithm;.AlgorithmARVRNode`
- **実装ファイル**: `Algorithm\AlgorithmARVRNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Algorithm` | `AlgorithmARVRType` |プロパティ |
| `TempName` | `string` |プロパティ |
| `POITempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `Color` | `CVOLED_COLOR` |プロパティ |
| `BufferLen` | `int` |プロパティ |

---

#### 9. アルゴリズムBlackMuraNode

- **フルタイプ**: `FlowEngineLib.Node.Algorithm;.AlgorithmBlackMuraNode`
- **実装ファイル**: `Node\Algorithm\AlgorithmBlackMuraNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `OIndex` | `string` |プロパティ |
| `SavePOITempName` | `string` |プロパティ |

---

#### 10. アルゴリズムCaliNode

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm;.AlgorithmCaliNode`
- **実装ファイル**: `Node\Algorithm\AlgorithmCaliNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `OutputFileName` | `string` |プロパティ |

---

#### 11. アルゴリズムCompoundImgNode

- **フルタイプ**: `FlowEngineLib.Node.OLED;.AlgorithmCompoundImgNode`
- **実装ファイル**: `Node\OLED\AlgorithmCompoundImgNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `OutputFileName` | `string` |プロパティ |
| `BufferLen` | `int` |プロパティ |

---

#### 12.アルゴリズムEQEノード

- **完全なタイプ**: `FlowEngineLib;.AlgorithmEQENode`
- **実装ファイル**: `AlgorithmEQENode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |

---

#### 13. アルゴリズムFindLEDNode

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm;.AlgorithmFindLEDNode`
- **実装ファイル**: `Node\Algorithm\AlgorithmFindLEDNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Color` | `CVOLED_Channel` |プロパティ |
| `TempName` | `string` |プロパティ |
| `FDAType` | `CVOLED_FDAType` |プロパティ |
| `FixedLEDPoint` | `PointFloat[]` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `OutputFileName` | `string` |プロパティ |
| `ImgPosResultFile` | `string` |プロパティ |

---

#### 14. アルゴリズムFindLightAreaNode

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm;.AlgorithmFindLightAreaNode`
- **実装ファイル**: `Node\Algorithm\AlgorithmFindLightAreaNode.cs`
- **基本クラス**: `CVBaseServerNode`|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `SavePOITempName` | `string` |プロパティ |
| `BufferLen` | `int` |プロパティ |
| `OIndex` | `string` |プロパティ |

---

#### 15. アルゴリズムGhostV2Node

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm;.AlgorithmGhostV2Node`
- **実装ファイル**: `Node\Algorithm\AlgorithmGhostV2Node.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `BufferLen` | `int` |プロパティ |

---

#### 16. アルゴリズムImageConvertNode

- **フルタイプ**: `FlowEngineLib.Node.Algorithm;.AlgorithmImageConvertNode`
- **実装ファイル**: `Node\Algorithm\AlgorithmImageConvertNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `ImageFormat` | `ImageFormatType` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `Channel` | `CVOLED_Channel` |プロパティ |

---

#### 17.アルゴリズムImageROINode

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm;.AlgorithmImageROINode`
- **実装ファイル**: `Node\Algorithm\AlgorithmImageROINode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `OutputFileName` | `string` |プロパティ |

---

#### 18. アルゴリズムKBNode

- **フルタイプ**: `FlowEngineLib.Node.Algorithm;.AlgorithmKBNode`
- **実装ファイル**: `Node\Algorithm\AlgorithmKBNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |

---

#### 19. アルゴリズムKBOutputNode

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm;.AlgorithmKBOutputNode`
- **実装ファイル**: `Node\Algorithm\AlgorithmKBOutputNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |

---

#### 20.アルゴリズムノード

- **完全なタイプ**: `FlowEngineLib.Algorithm;.AlgorithmNode`
- **実装ファイル**: `Algorithm\AlgorithmNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Algorithm` | `AlgorithmType` |プロパティ |
| `TempName` | `string` |プロパティ |
| `POITempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `Color` | `CVOLED_COLOR` |プロパティ |
| `BufferLen` | `int` |プロパティ |

---

#### 21. アルゴリズムOLEDNode

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm;.AlgorithmOLEDNode`
- **実装ファイル**: `Node\Algorithm\AlgorithmOLEDNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Algorithm` | `AlgorithmOLEDType` |プロパティ |
| `Color` | `CVOLED_COLOR` |プロパティ |
| `TempName` | `string` |プロパティ |
| `FDAType` | `CVOLED_FDAType` |プロパティ |
| `FixedLEDPoint` | `PointFloat[]` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `OutputFileName` | `string` |プロパティ |
| `ImgPosResultFile` | `string` |プロパティ |

---

#### 22. アルゴリズムOLED_AOINode

- **フルタイプ**: `FlowEngineLib.Node.Algorithm;.AlgorithmOLED_AOINode`
- **実装ファイル**: `Node\Algorithm\AlgorithmOLED_AOINode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Algorithm` | `AlgorithmOLED_AOIType` |プロパティ |
| `TempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `OutputFileName` | `string` |プロパティ |
| `CustomSN` | `string` |プロパティ |
| `VhLineEnable` | `bool` |プロパティ |
| `PixelDefectEnable` | `bool` |プロパティ |
| `MuraEnable` | `bool` |プロパティ |

---

#### 23.アルゴリズムTMNode

- **フルタイプ**: `FlowEngineLib.Node.Algorithm;.AlgorithmTMNode`
- **実装ファイル**: `Node\Algorithm\AlgorithmTMNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `TemplateFile` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |

---

#### 24. TPAlgorithm2Node

- **フルタイプ**: `FlowEngineLib.Node.Algorithm;.TPAlgorithm2Node`
- **実装ファイル**: `Node\Algorithm\TPAlgorithm2Node.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Operator` | `string` |プロパティ |
| `TempName` | `string` |プロパティ |

---

#### 25. TPアルゴリズムノード

- **フルタイプ**: `FlowEngineLib.Node.Algorithm;.TPAlgorithmNode`
- **実装ファイル**: `Node\Algorithm\TPAlgorithmNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Algorithm` | `TPAlgorithmType` |プロパティ |
| `Operator` | `string` |プロパティ |
| `TempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |

---

### カメラクラスノード (14)

#### 1. AOILocAndRegPixelsCameraNode

- **完全なタイプ**: `FlowEngineLib;.AOILocAndRegPixelsCameraNode`
- **実装ファイル**: `AOILocAndRegPixelsCameraNode.cs`
- **基本クラス**: `CVBaseServerNode`| プロパティ名 | C# の種類 | 来源 |
|--------|---------|------|
| `ImgSaveMode` | `ImgSaveBppMode` |プロパティ |
| `ImgSaveName` | `string` |プロパティ |
| `AvgCount` | `int` |プロパティ |
| `Gain` | `float` |プロパティ |
| `ExpTime` | `float` |プロパティ |
| `IsAutoExp` | `bool` |プロパティ |
| `AutoExpTempName` | `string` |プロパティ |
| `IsWithND` | `bool` |プロパティ |
| `CaliTempName` | `string` |プロパティ |
| `FlipMode` | `CVImageFlipMode` |プロパティ |
| `AlgTempName` | `string` |プロパティ |
| `Channel` | `CVOLED_Channel` |プロパティ |
| `OutputTempName` | `string` |プロパティ |

---

#### 2. AOILocatePixelsCameraNode

- **完全な種類**: `FlowEngineLib;.AOILocatePixelsCameraNode`
- **实现文件**: `AOILocatePixelsCameraNode.cs`
- **基类**: `CVBaseServerNode`

| プロパティ名 | C# の種類 | 来源 |
|--------|---------|------|
| `ImgSaveMode` | `ImgSaveBppMode` |プロパティ |
| `ImgSaveName` | `string` |プロパティ |
| `AvgCount` | `int` |プロパティ |
| `Gain` | `float` |プロパティ |
| `ExpTime` | `float` |プロパティ |
| `IsAutoExp` | `bool` |プロパティ |
| `AutoExpTempName` | `string` |プロパティ |
| `IsWithND` | `bool` |プロパティ |
| `CaliTempName` | `string` |プロパティ |
| `FlipMode` | `CVImageFlipMode` |プロパティ |
| `AlgTempName` | `string` |プロパティ |
| `Channel` | `CVOLED_Channel` |プロパティ |

---

#### 3. AOIRegisterPixelsCameraNode

- **完整型**: `FlowEngineLib;.AOIRegisterPixelsCameraNode`
- **实现文件**: `AOIRegisterPixelsCameraNode.cs`
- **基类**: `CVBaseServerNodeHub`

| プロパティ名 | C# の種類 | 来源 |
|--------|---------|------|
| `ImgSaveMode` | `ImgSaveBppMode` |プロパティ |
| `ImgSaveName` | `string` |プロパティ |
| `AvgCount` | `int` |プロパティ |
| `Gain` | `float` |プロパティ |
| `ExpTime` | `float` |プロパティ |
| `IsAutoExp` | `bool` |プロパティ |
| `AutoExpTempName` | `string` |プロパティ |
| `IsWithND` | `bool` |プロパティ |
| `CaliTempName` | `string` |プロパティ |
| `FlipMode` | `CVImageFlipMode` |プロパティ |
| `AlgTempName` | `string` |プロパティ |
| `Channel` | `CVOLED_Channel` |プロパティ |
| `OutputTempName` | `string` |プロパティ |

---

#### 4.BaseCameraNode

- **完全型**: `FlowEngineLib;.BaseCameraNode`
- **实现文**: `BaseCameraNode.cs`
- **ベースクラス**: `CVBaseServerNode`

| プロパティ名 | C# の種類 | 来源 |
|--------|---------|------|
| `AvgCount` | `int` |プロパティ |
| `Gain` | `float` |プロパティ |
| `ExpTime` | `float` |プロパティ |
| `CaliTempName` | `string` |プロパティ |
| `FlipMode` | `CVImageFlipMode` |プロパティ |
| `POITempName` | `string` |プロパティ |
| `POIFilterTempName` | `string` |プロパティ |
| `POIReviseTempName` | `string` |プロパティ |

---

#### 5.CVAOI2カメラノード

- **完全な種類**: `FlowEngineLib.Node.Camera;.CVAOI2CameraNode`
- **实现文件**: `Node\Camera\CVAOI2CameraNode.cs`
- **ベースクラス**: `CVBaseServerNodeHub`

| プロパティ名 | C# の種類 | 来源 |
|--------|---------|------|
| `CamTempName` | `string` |プロパティ |
| `ImgSaveMode` | `ImgSaveBppMode` |プロパティ |
| `FlipMode` | `CVImageFlipMode` |プロパティ |
| `IsAutoExp` | `bool` |プロパティ |
| `TempName` | `string` |プロパティ |
| `IsWithND` | `bool` |プロパティ |
| `CalibTempName` | `string` |プロパティ |
| `AOIType` | `AOI2TypeEnum` |プロパティ |
| `AlgTempName` | `string` |プロパティ |

---

#### 6.CVAOIカメラノード

- **完整型**: `FlowEngineLib.Node.Camera;.CVAOICameraNode`
- **实现文件**: `Node\Camera\CVAOICameraNode.cs`
- **ベースクラス**: `CVBaseServerNode`

| プロパティ名 | C# の種類 | 来源 |
|--------|---------|------|
| `CamTempName` | `string` |プロパティ |
| `ImgSaveMode` | `ImgSaveBppMode` |プロパティ |
| `FlipMode` | `CVImageFlipMode` |プロパティ |
| `IsAutoExp` | `bool` |プロパティ |
| `TempName` | `string` |プロパティ |
| `IsWithND` | `bool` |プロパティ |
| `CalibTempName` | `string` |プロパティ |
| `AOIType` | `AOITypeEnum` |プロパティ |
| `AlgTempName` | `string` |プロパティ |

---

#### 7. CVCameraDataFlow

- **完整型**: `FlowEngineLib;.CVCameraDataFlow`
- **实现文件**: `CVCameraDataFlow.cs`
- **基类**: `CVMQTTRequest`

---

#### 8.CVCameraNode

- **完全な種類**: `FlowEngineLib;.CVCameraNode`
- **实现文件**: `CVCameraNode.cs`
- **基类**: `CVBaseServerNode`

| プロパティ名 | C# の種類 | 来源 |
|--------|---------|------|
| `AvgCount` | `int` |プロパティ |
| `Gain` | `float` |プロパティ |
| `TempR` | `float` |プロパティ |
| `TempG` | `float` |プロパティ |
| `TempB` | `float` |プロパティ |
| `CV2LVChannel` | `CV2LVChannelMode` |プロパティ |
| `CalibTempName` | `string` |プロパティ |
| `FlipMode` | `CVImageFlipMode` |プロパティ |
| `POITempName` | `string` |プロパティ |
| `POIFilterTempName` | `string` |プロパティ |
| `POIReviseTempName` | `string` |プロパティ |

---#### 9. CVXRCameraNode

- **フルタイプ**: `FlowEngineLib;.CVXRCameraNode`
- **実装ファイル**: `CVXRCameraNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `AvgCount` | `int` |プロパティ |
| `Gain` | `float` |プロパティ |
| `TempR` | `float` |プロパティ |
| `TempG` | `float` |プロパティ |
| `TempB` | `float` |プロパティ |
| `CalibTempName` | `string` |プロパティ |
| `FlipMode` | `CVImageFlipMode` |プロパティ |
| `POITempName` | `string` |プロパティ |
| `POIFilterTempName` | `string` |プロパティ |
| `POIReviseTempName` | `string` |プロパティ |
| `Algorithm` | `AlgorithmARVRType` |プロパティ |
| `XRTempName` | `string` |プロパティ |

---

#### 10.カムモーターノード

- **完全なタイプ**: `FlowEngineLib;.CamMotorNode`
- **実装ファイル**: `CamMotorNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `RunType` | `CamMotorRunType` |プロパティ |
| `IsAbs` | `bool` |プロパティ |
| `Position` | `int` |プロパティ |
| `Aperture` | `float` |プロパティ |
| `AutoFocusTemp` | `string` |プロパティ |

---

#### 11. CameraROINode

- **フルタイプ**: `FlowEngineLib.Node.Camera;.CameraROINode`
- **実装ファイル**: `Node\Camera\CameraROINode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `ROI_X` | `int` |プロパティ |
| `ROI_Y` | `int` |プロパティ |
| `ROI_Width` | `int` |プロパティ |
| `ROI_Height` | `int` |プロパティ |

---

#### 12. CommCameraNode

- **フルタイプ**: `FlowEngineLib.Node.Camera;.CommCameraNode`
- **実装ファイル**: `Node\Camera\CommCameraNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `IsHDR` | `bool` |プロパティ |
| `CamTempName` | `string` |プロパティ |
| `FlipMode` | `CVImageFlipMode` |プロパティ |
| `IsAutoExp` | `bool` |プロパティ |
| `TempName` | `string` |プロパティ |
| `IsWithND` | `bool` |プロパティ |
| `CalibTempName` | `string` |プロパティ |
| `POITempName` | `string` |プロパティ |
| `POIFilterTempName` | `string` |プロパティ |
| `POIReviseTempName` | `string` |プロパティ |

---

#### 13.LVカメラノード

- **フルタイプ**: `FlowEngineLib;.LVCameraNode`
- **実装ファイル**: `LVCameraNode.cs`
- **基本クラス**: `BaseCameraNode`

---

#### 14. LVXRカメラノード

- **完全なタイプ**: `FlowEngineLib;.LVXRCameraNode`
- **実装ファイル**: `LVXRCameraNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `AvgCount` | `int` |プロパティ |
| `Gain` | `float` |プロパティ |
| `ExpTime` | `float` |プロパティ |
| `CaliTempName` | `string` |プロパティ |
| `FlipMode` | `CVImageFlipMode` |プロパティ |
| `POITempName` | `string` |プロパティ |
| `POIFilterTempName` | `string` |プロパティ |
| `POIReviseTempName` | `string` |プロパティ |
| `Algorithm` | `AlgorithmARVRType` |プロパティ |
| `XRTempName` | `string` |プロパティ |

---

### デバイスクラスノード (1)

#### 1. PhyDeviceControlNode

- **フルタイプ**: `FlowEngineLib.Node.Global;.PhyDeviceControlNode`
- **実装ファイル**: `Node\Global\PhyDeviceControlNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `DeviceType` | `CVDeviceType` |プロパティ |
| `CmdType` | `CVDeviceControlCmd` |プロパティ |

---

### 終了クラスノード(2)

#### 1. CVEndNode

- **フルタイプ**: `FlowEngineLib.End;.CVEndNode`
- **実装ファイル**: `End\CVEndNode.cs`
- **基本クラス**: `CVCommonNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `m_in_start` | `STNodeOption` |フィールド |

---

#### 2. CVEndV5Node

- **フルタイプ**: `FlowEngineLib.End;.CVEndV5Node`
- **実装ファイル**: `End\CVEndV5Node.cs`
- **基本クラス**: `CVCommonNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `m_in_start` | `STNodeOption` |フィールド |

---

### FWクラスノード(1)

#### 1.FWノード

- **完全なタイプ**: `FlowEngineLib;.FWNode`
- **実装ファイル**: `FWNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Port` | `int` |プロパティ |
| `ModelType` | `FWModelType` |プロパティ |

---

### ループクラスノード (2)

#### 1.ループネクストノード

- **フルタイプ**: `FlowEngineLib;.LoopNextNode`
- **実装ファイル**: `LoopNextNode.cs`
- **基本クラス**: `CVCommonNode`

---

#### 2. ループノード

- **完全なタイプ**: `FlowEngineLib;.LoopNode`
- **実装ファイル**: `LoopNode.cs`
- **基本クラス**: `CVCommonNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `BeginVal` | `float` |プロパティ |
| `EndVal` | `float` |プロパティ |
| `StepVal` | `float` |プロパティ |

---

### MQTT クラス ノード (5)

#### 1.MQTTBaseNode

- **完全なタイプ**: `FlowEngineLib.MQTT;.MQTTBaseNode`
- **実装ファイル**: `MQTT\MQTTBaseNode.cs`
- **基本クラス**: `STNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Server` | `string` |プロパティ |
| `Port` | `int` |プロパティ |

---

#### 2.MQTTCustomPublishNode- **フルタイプ**: `FlowEngineLib;.MQTTCustomPublishNode`
- **実装ファイル**: `MQTTCustomPublishNode.cs`
- **基本クラス**: `MQTTBaseNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Data` | `string` |プロパティ |
| `Topic` | `string` |プロパティ |

---

#### 3.MQTTCustomSubscribeNode

- **フルタイプ**: `FlowEngineLib;.MQTTCustomSubscribeNode`
- **実装ファイル**: `MQTTCustomSubscribeNode.cs`
- **基本クラス**: `MQTTBaseNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Topic` | `string` |プロパティ |

---

#### 4.MQTTStartNode

- **フルタイプ**: `FlowEngineLib.Start;.MQTTStartNode`
- **実装ファイル**: `Start\MQTTStartNode.cs`
- **基本クラス**: `BaseStartNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Server` | `string` |プロパティ |
| `Port` | `int` |プロパティ |

---

#### 5.MQTTStartV5Node

- **フルタイプ**: `FlowEngineLib.Start;.MQTTStartV5Node`
- **実装ファイル**: `Start\MQTTStartV5Node.cs`
- **基本クラス**: `BaseStartNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Server` | `string` |プロパティ |
| `Port` | `int` |プロパティ |

---

### 手動クラスノード (1)

#### 1.ManualconfirmNode

- **フルタイプ**: `FlowEngineLib;.ManualConfirmNode`
- **実装ファイル**: `ManualConfirmNode.cs`
- **基本クラス**: `CVCommonNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `MessageText` | `string` |プロパティ |

---

### OLED クラス ノード (7)

#### 1. OLEDCombineQuaterImages_4In1Node

- **フルタイプ**: `FlowEngineLib.Node.Algorithm;.OLEDCombineQuaterImages_4In1Node`
- **実装ファイル**: `Node\Algorithm\OLEDCombineQuaterImages_4In1Node.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `ImgFileName1` | `string` |プロパティ |
| `ImgFileName2` | `string` |プロパティ |
| `ImgFileName3` | `string` |プロパティ |
| `ImgFileName4` | `string` |プロパティ |
| `OutputFileName` | `string` |プロパティ |

---

#### 2. OLEDFindPixelDefectsForQuardImgNode

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm;.OLEDFindPixelDefectsForQuardImgNode`
- **実装ファイル**: `Node\Algorithm\OLEDFindPixelDefectsForQuardImgNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `OutputFileName` | `string` |プロパティ |

---

#### 3. OLEDImageCroppingNode

- **完全なタイプ**: `FlowEngineLib.Node.OLED;.OLEDImageCroppingNode`
- **実装ファイル**: `Node\OLED\OLEDImageCroppingNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |

---

#### 4. OLEDJNDCalVasNode

- **フルタイプ**: `FlowEngineLib.Node.OLED;.OLEDJNDCalVasNode`
- **実装ファイル**: `Node\OLED\OLEDJNDCalVasNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `OrderIndex` | `int` |プロパティ |
| `TempName` | `string` |プロパティ |
| `Algorithm` | `Algorithm2Type` |プロパティ |
| `BufferLen` | `int` |プロパティ |
| `IsAdd` | `bool` |プロパティ |

---

#### 5. OLEDParticlesFindAndFillNode

- **フルタイプ**: `FlowEngineLib.Node.Algorithm;.OLEDParticlesFindAndFillNode`
- **実装ファイル**: `Node\Algorithm\OLEDParticlesFindAndFillNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `ParticlesType` | `ParticlesMode` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `OutputFileName` | `string` |プロパティ |

---

#### 6. OLEDRebuildPixelsNode

- **完全なタイプ**: `FlowEngineLib.Node.OLED;.OLEDRebuildPixelsNode`
- **実装ファイル**: `Node\OLED\OLEDRebuildPixelsNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Channel` | `CVOLED_Channel` |プロパティ |
| `TempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `OutputTemplateName` | `string` |プロパティ |

---

#### 7. OLEDRebuildPixelsPosNode

- **フルタイプ**: `FlowEngineLib.Node.OLED;.OLEDRebuildPixelsPosNode`
- **実装ファイル**: `Node\OLED\OLEDRebuildPixelsPosNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `Channel` | `CVOLED_Channel` |プロパティ |
| `OutputTemplateName` | `string` |プロパティ |

---

### その他のクラスノード (8)

#### 1. CVBaseServerNode

- **フルタイプ**: `FlowEngineLib.Base;.CVBaseServerNode`
- **実装ファイル**: `Base\CVBaseServerNode.cs`
- **基本クラス**: `CVCommonNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Token` | `string` |プロパティ |
| `MaxTime` | `int` |プロパティ |
| `Subtitle` | `string` |プロパティ |

---

#### 2. CVBaseServerNodeHub

- **フルタイプ**: `FlowEngineLib.Base;.CVBaseServerNodeHub`
- **実装ファイル**: `Base\CVBaseServerNodeHub.cs`
- **基本クラス**: `CVBaseServerNode`

---

#### 3. CVBaseServerNodeIn2Hub

- **フルタイプ**: `FlowEngineLib.Base;.CVBaseServerNodeIn2Hub`
- **実装ファイル**: `Base\CVBaseServerNodeIn2Hub.cs`
- **基本クラス**: `CVBaseServerNode`

---

#### 4. CVCommonNode

- **フルタイプ**: `FlowEngineLib.Base;.CVCommonNode`
- **実装ファイル**: `Base\CVCommonNode.cs`
- **基本クラス**: `STNode`|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `NodeName` | `string` |プロパティ |
| `NodeType` | `string` |プロパティ |
| `DeviceCode` | `string` |プロパティ |
| `NodeID` | `string` |プロパティ |
| `ZIndex` | `int` |プロパティ |
| `nodeEvent` | `FlowEngineNodeEvent` |プロパティ |
| `nodeRunEvent` | `FlowEngineNodeRunEvent` |プロパティ |
| `nodeEndEvent` | `FlowEngineNodeEndEvent` |プロパティ |

---

#### 5.Calibration2InNode

- **フルタイプ**: `FlowEngineLib.Node.OLED;.Calibration2InNode`
- **実装ファイル**: `Node\OLED\Calibration2InNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `ExpTempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `IsSaveCIE` | `bool` |プロパティ |
| `POIFilterTempName` | `string` |プロパティ |
| `POIReviseTempName` | `string` |プロパティ |
| `OutputTemplateName` | `string` |プロパティ |

---

#### 6. キャリブレーションノード

- **完全なタイプ**: `FlowEngineLib.Algorithm;.CalibrationNode`
- **実装ファイル**: `Algorithm\CalibrationNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `ExpTempName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `IsSaveCIE` | `bool` |プロパティ |
| `POITempName` | `string` |プロパティ |
| `POIFilterTempName` | `string` |プロパティ |
| `POIReviseTempName` | `string` |プロパティ |
| `OutputTemplateName` | `string` |プロパティ |

---

#### 7. キャリブレーションROINode

- **完全なタイプ**: `FlowEngineLib.Node.Camera;.CalibrationROINode`
- **実装ファイル**: `Node\Camera\CalibrationROINode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `ROI_X` | `int` |プロパティ |
| `ROI_Y` | `int` |プロパティ |
| `ROI_Width` | `int` |プロパティ |
| `ROI_Height` | `int` |プロパティ |

---

#### 8. モーターノード

- **完全なタイプ**: `FlowEngineLib;.MotorNode`
- **実装ファイル**: `MotorNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `RunType` | `MotorRunType` |プロパティ |
| `IsAbs` | `bool` |プロパティ |
| `Position` | `int` |プロパティ |
| `Aperture` | `float` |プロパティ |

---

### PG クラス ノード (1)

#### 1.PGNode

- **完全なタイプ**: `FlowEngineLib.Node.PG;.PGNode`
- **実装ファイル**: `Node\PG\PGNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `PGCmd` | `PGCommCmdType` |プロパティ |
| `IndexFrame` | `int` |プロパティ |

---

### POI クラス ノード (8)

#### 1.POI2Node を構築する

- **フルタイプ**: `FlowEngineLib.Node.POI;.BuildPOI2Node`
- **実装ファイル**: `Node\POI\BuildPOI2Node.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TemplateName` | `string` |プロパティ |
| `BuildType` | `POIBuildType` |プロパティ |
| `PrefixName` | `string` |プロパティ |
| `POIType` | `POIPointTypes` |プロパティ |
| `POIHeight` | `int` |プロパティ |
| `POIWidth` | `int` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `POIOutput` | `POIStorageModel` |プロパティ |
| `OutputFileName` | `string` |プロパティ |
| `SavePOITempName` | `string` |プロパティ |

---

#### 2. BuildPOINode

- **完全なタイプ**: `FlowEngineLib;.BuildPOINode`
- **実装ファイル**: `BuildPOINode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TemplateName` | `string` |プロパティ |
| `RePOITemplateName` | `string` |プロパティ |
| `LayoutROITemplate` | `string` |プロパティ |
| `BuildType` | `POIBuildType` |プロパティ |
| `PrefixName` | `string` |プロパティ |
| `POIType` | `POIPointTypes` |プロパティ |
| `POIHeight` | `int` |プロパティ |
| `POIWidth` | `int` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `CAD_PosFileName` | `string` |プロパティ |
| `POIOutput` | `POIStorageModel` |プロパティ |
| `OutputFileName` | `string` |プロパティ |
| `SavePOITempName` | `string` |プロパティ |
| `BufferLen` | `int` |プロパティ |

---

#### 3. POIAnamination と SMUNode

- **完全なタイプ**: `FlowEngineLib.Node.POI;.POIAnalysisAndSMUNode`
- **実装ファイル**: `Node\POI\POIAnalysisAndSMUNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |

---

#### 4. POIAna AnalysisNode

- **完全なタイプ**: `FlowEngineLib.Node.POI;.POIAnalysisNode`
- **実装ファイル**: `Node\POI\POIAnalysisNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |

---

#### 5. POICADMappingNode

- **完全なタイプ**: `FlowEngineLib.Node.POI;.POICADMappingNode`
- **実装ファイル**: `Node\POI\POICADMappingNode.cs`
- **基本クラス**: `CVBaseServerNode`|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TemplateName` | `string` |プロパティ |
| `MappingType` | `POIBuildType` |プロパティ |
| `CADFileName` | `string` |プロパティ |
| `PrefixName` | `string` |プロパティ |

---

#### 6. POINode

- **完全なタイプ**: `FlowEngineLib;.POINode`
- **実装ファイル**: `POINode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `FilterTemplateName` | `string` |プロパティ |
| `ReviseTemplateName` | `string` |プロパティ |
| `OutputTemplateName` | `string` |プロパティ |
| `ImgFileName` | `string` |プロパティ |
| `IsCCTWave` | `bool` |プロパティ |
| `IsSubPixel` | `bool` |プロパティ |

---

#### 7. POIREviseNode

- **完全なタイプ**: `FlowEngineLib.Node.POI;.POIReviseNode`
- **実装ファイル**: `Node\POI\POIReviseNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TemplateName` | `string` |プロパティ |
| `POIPointName` | `string` |プロパティ |
| `IsSelfResultRevise` | `bool` |プロパティ |

---

#### 8. RealPOINode

- **完全なタイプ**: `FlowEngineLib.Node.POI;.RealPOINode`
- **実装ファイル**: `Node\POI\RealPOINode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `ImgFileName` | `string` |プロパティ |
| `FilterTemplateName` | `string` |プロパティ |
| `ReviseTemplateName` | `string` |プロパティ |
| `ReviseFileName` | `string` |プロパティ |
| `OutputTemplateName` | `string` |プロパティ |
| `SubPixelTemplateName` | `string` |プロパティ |
| `POIType` | `POIPointTypes` |プロパティ |
| `POIHeight` | `float` |プロパティ |
| `POIWidth` | `float` |プロパティ |
| `IsResultAdd` | `bool` |プロパティ |
| `IsCCTWave` | `bool` |プロパティ |

---

### SMU クラス ノード (7)

#### 1.SMUBaseNode

- **完全なタイプ**: `FlowEngineLib;.SMUBaseNode`
- **実装ファイル**: `SMUBaseNode.cs`
- **基本クラス**: `CVBaseServerNode`、`ICVLoopNextNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `LoopName` | `string` |プロパティ |
| `IsCloseOutput` | `bool` |プロパティ |
| `IsStarted` | `bool` |フィールド |

---

#### 2.SMUFromCSVNode

- **完全なタイプ**: `FlowEngineLib;.SMUFromCSVNode`
- **実装ファイル**: `SMUFromCSVNode.cs`
- **基本クラス**: `SMUBaseNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Source` | `SourceType` |プロパティ |
| `Channel` | `SMUChannelType` |プロパティ |
| `CsvFileName` | `string` |プロパティ |
| `IsAutoRng` | `bool` |プロパティ |
| `SrcRng` | `double` |プロパティ |
| `LmtRng` | `double` |プロパティ |

---

#### 3.SMUModelNode

- **完全なタイプ**: `FlowEngineLib;.SMUModelNode`
- **実装ファイル**: `SMUModelNode.cs`
- **基本クラス**: `SMUBaseNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `ModelName` | `string` |プロパティ |

---

#### 4.SMUNode

- **完全なタイプ**: `FlowEngineLib;.SMUNode`
- **実装ファイル**: `SMUNode.cs`
- **基本クラス**: `SMUBaseNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Source` | `SourceType` |プロパティ |
| `Channel` | `SMUChannelType` |プロパティ |
| `BeginVal` | `float` |プロパティ |
| `EndVal` | `float` |プロパティ |
| `LimitVal` | `float` |プロパティ |
| `PointNum` | `int` |プロパティ |
| `IsAutoRng` | `bool` |プロパティ |
| `SrcRng` | `double` |プロパティ |
| `LmtRng` | `double` |プロパティ |

---

#### 5. SMUReaderNode

- **完全なタイプ**: `FlowEngineLib.Node.SMU;.SMUReaderNode`
- **実装ファイル**: `Node\SMU\SMUReaderNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `WaitTime` | `int` |プロパティ |

---

#### 6. SMUスイープモデルノード

- **完全なタイプ**: `FlowEngineLib.Node.SMU;.SMUSweepModelNode`
- **実装ファイル**: `Node\SMU\SMUSweepModelNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `ModelName` | `string` |プロパティ |
| `IsCloseOutput` | `bool` |プロパティ |

---

#### 7. SMUスイープノード

- **完全なタイプ**: `FlowEngineLib.Node.SMU;.SMUSweepNode`
- **実装ファイル**: `Node\SMU\SMUSweepNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `Source` | `SourceType` |プロパティ |
| `Channel` | `SMUChannelType` |プロパティ |
| `BeginVal` | `float` |プロパティ |
| `EndVal` | `float` |プロパティ |
| `LimitVal` | `float` |プロパティ |
| `PointNum` | `int` |プロパティ |
| `IsCloseOutput` | `bool` |プロパティ |
| `IsAutoRng` | `bool` |プロパティ |
| `SrcRng` | `double` |プロパティ |
| `LmtRng` | `double` |プロパティ |

---

### センサークラスノード (3)

#### 1. CommonSensorNode- **完全なタイプ**: `FlowEngineLib;.CommonSensorNode`
- **実装ファイル**: `CommonSensorNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |
| `CmdType` | `CommCmdType` |プロパティ |
| `CmdSend` | `string` |プロパティ |
| `CmdReceive` | `string` |プロパティ |

---

#### 2. RealCommonSensorNode

- **完全なタイプ**: `FlowEngineLib;.RealCommonSensorNode`
- **実装ファイル**: `RealCommonSensorNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `CmdType` | `CommSensorCmdType` |プロパティ |
| `CmdSend` | `string` |プロパティ |
| `CmdReceive` | `string` |プロパティ |
| `CmdTimeout` | `int` |プロパティ |
| `RetryCount` | `int` |プロパティ |
| `Delay` | `int` |プロパティ |

---

#### 3.TempCommonSensorNode

- **完全なタイプ**: `FlowEngineLib;.TempCommonSensorNode`
- **実装ファイル**: `TempCommonSensorNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `TempName` | `string` |プロパティ |

---

### スペクトル クラス ノード (2)

#### 1. SpectrumEQENode

- **完全なタイプ**: `FlowEngineLib.Node.Spectrum;.SpectrumEQENode`
- **実装ファイル**: `Node\Spectrum\SpectrumEQENode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `AFactor` | `float` |プロパティ |
| `IsCustomVI` | `bool` |プロパティ |
| `Voltage` | `float` |プロパティ |
| `Current` | `float` |プロパティ |
| `Temp` | `float` |プロパティ |
| `AveNum` | `int` |プロパティ |
| `AutoIntTime` | `bool` |プロパティ |
| `IsWithND` | `bool` |プロパティ |
| `SelfDark` | `bool` |プロパティ |
| `AutoInitDark` | `bool` |プロパティ |
| `OutputDataFilename` | `string` |プロパティ |

---

#### 2. スペクトルノード

- **完全なタイプ**: `FlowEngineLib.Node.Spectrum;.SpectrumNode`
- **実装ファイル**: `Node\Spectrum\SpectrumNode.cs`
- **基本クラス**: `CVBaseServerNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `PGCmd` | `SPCommCmdType` |プロパティ |
| `Temp` | `float` |プロパティ |
| `AveNum` | `int` |プロパティ |
| `AutoIntTime` | `bool` |プロパティ |
| `IsWithND` | `bool` |プロパティ |
| `SelfDark` | `bool` |プロパティ |
| `AutoInitDark` | `bool` |プロパティ |
| `OutputDataFilename` | `string` |プロパティ |

---

### 開始クラスノード (3)

#### 1.CVStartCFC

- **完全なタイプ**: `FlowEngineLib.Base;.CVStartCFC`
- **実装ファイル**: `Base\CVStartCFC.cs`
- **基本クラス**: `CVBaseCFC`

---

#### 2.ManualStartNode

- **フルタイプ**: `FlowEngineLib;.ManualStartNode`
- **実装ファイル**: `ManualStartNode.cs`
- **基本クラス**: `BaseStartNode`

|プロパティ名 | C# タイプ |出典 |
|--------|---------|------|
| `SN` | `string` |プロパティ |
| `Action` | `ActionTypeEnum` |プロパティ |

---

#### 3. ModbusStartNode

- **完全なタイプ**: `FlowEngineLib.Start;.ModbusStartNode`
- **実装ファイル**: `Start\ModbusStartNode.cs`
- **基本クラス**: `BaseStartNode`

---
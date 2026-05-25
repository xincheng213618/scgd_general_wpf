# Flow Engine 노드의 완전한 세트

> 'FlowEngineLib'에 정의된 모든 노드 클래스를 포함하여 2026년 5월 22일에 자동으로 생성됩니다.

## 개요

총 **90** 노드 클래스가 있으며 다음과 같이 유형별로 그룹화됩니다.

| 유형 | 수량 |
|------|------|
| 알고리즘 | 25 |
| 카메라 | 14 |
| POI | 8 |
| 기타 | 8 |
| SMU | 7 |
| OLED | 7 |
| MQTT | 5 |
| 센서 | 3 |
| 시작 | 3 |
| 루프 | 2 |
| 끝 | 2 |
| 스펙트럼 | 2 |
| FW | 1 |
| 매뉴얼 | 1 |
|장치|1|
| PG | 1 |

## 노드 목록| 类别 | 结点类名 | 基类 | 文件 | 属性数 |
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
| Start | `ModbusStartNode` | `BaseStartNode` | `Start\ModbusStartNode.cs` | 0 |## 노드 카테고리별 세부 속성

### 알고리즘 클래스 노드(25)

#### 1. AlgComplianceContrastNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgComplianceContrastNode`
- **구현 파일**: `Node\Algorithm\AlgComplianceContrastNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '작전' | `작업 유형` | 부동산 |
| `임시 이름` | `문자열` | 부동산 |

---

#### 2. AlgComplianceJudgmentNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgComplianceJudgmentNode`
- **구현 파일**: `Node\Algorithm\AlgComplianceJudgmentNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| 'IsBreak' | `부울` | 부동산 |

---

#### 3. AlgComplianceMathNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgComplianceMathNode`
- **구현 파일**: `Node\Algorithm\AlgComplianceMathNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `규정 준수 수학` | `ComplianceMathType` | 부동산 |
| 'IsBreak' | `부울` | 부동산 |

---

#### 4. AlgDataConvertNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgDataConvertNode`
- **구현 파일**: `Node\Algorithm\AlgDataConvertNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `메소드 유형` | `CVDataConvertMethodType` | 부동산 |
| `임시 이름` | `문자열` | 부동산 |
| `인타입` | `CVDataConvertInputType` | 부동산 |
| `아웃타입` | `CVDataConvertOutputType` | 부동산 |

---

#### 5. AlgDataLoadNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgDataLoadNode`
- **구현 파일**: `Node\Algorithm\AlgDataLoadNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |

---

#### 6. AlgDataLoadNode2

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgDataLoadNode2`
- **구현 파일**: `Node\Algorithm\AlgDataLoadNode2.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `DataDeviceCode` | `문자열` | 부동산 |
| `일련번호` | `문자열` | 부동산 |
| `결과 유형` | `CVResultType` | 부동산 |
| `DataZIndex` | `정수` | 부동산 |

---

#### 7. 알고리즘2인노드

- **전체 유형**: `FlowEngineLib.Node.OLED;.Algorithm2InNode`
- **구현 파일**: `Node\OLED\Algorithm2InNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| '알고리즘' | `알고리즘2유형` | 부동산 |
| `버퍼렌` | `정수` | 부동산 |
| 'IsAdd' | `부울` | 부동산 |

---

#### 8. 알고리즘ARVRNode

- **전체 유형**: `FlowEngineLib.Algorithm;.AlgorithmARVRNode`
- **구현 파일**: `Algorithm\AlgorithmARVRNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '알고리즘' | `알고리즘ARVR 유형` | 부동산 |
| `임시 이름` | `문자열` | 부동산 |
| `POITemp이름` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| '색상' | `CVOLED_COLOR` | 부동산 |
| `버퍼렌` | `정수` | 부동산 |

---

#### 9. 알고리즘BlackMuraNode- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgorithmBlackMuraNode`
- **구현 파일**: `Node\Algorithm\AlgorithmBlackMuraNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| `O인덱스` | `문자열` | 부동산 |
| `SavePOITempName` | `문자열` | 부동산 |

---

#### 10. 알고리즘CaliNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgorithmCaliNode`
- **구현 파일**: `Node\Algorithm\AlgorithmCaliNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| `출력파일 이름` | `문자열` | 부동산 |

---

#### 11. 알고리즘CompoundImgNode

- **전체 유형**: `FlowEngineLib.Node.OLED;.AlgorithmCompoundImgNode`
- **구현 파일**: `Node\OLED\AlgorithmCompoundImgNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `출력파일 이름` | `문자열` | 부동산 |
| `버퍼렌` | `정수` | 부동산 |

---

#### 12.알고리즘EQENode

- **전체 유형**: `FlowEngineLib;.AlgorithmEQENode`
- **구현 파일**: `AlgorithmEQENode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |

---

#### 13. 알고리즘FindLEDNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgorithmFindLEDNode`
- **구현 파일**: `Node\Algorithm\AlgorithmFindLEDNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '색상' | `CVOLED_채널` | 부동산 |
| `임시 이름` | `문자열` | 부동산 |
| 'FDA 유형' | `CVOLED_FDA유형` | 부동산 |
| '고정LED포인트' | `PointFloat[]` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| `출력파일 이름` | `문자열` | 부동산 |
| `ImgPosResultFile` | `문자열` | 부동산 |

---

#### 14. 알고리즘FindLightAreaNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgorithmFindLightAreaNode`
- **구현 파일**: `Node\Algorithm\AlgorithmFindLightAreaNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| `SavePOITempName` | `문자열` | 부동산 |
| `버퍼렌` | `정수` | 부동산 |
| `O인덱스` | `문자열` | 부동산 |

---

#### 15. 알고리즘GhostV2Node

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgorithmGhostV2Node`
- **구현 파일**: `Node\Algorithm\AlgorithmGhostV2Node.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| `버퍼렌` | `정수` | 부동산 |

---

#### 16. 알고리즘ImageConvertNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgorithmImageConvertNode`
- **구현 파일**: `Node\Algorithm\AlgorithmImageConvertNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `이미지 형식` | `이미지 형식 유형` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| '채널' | `CVOLED_채널` | 부동산 |

---#### 17.알고리즘ImageROINode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgorithmImageROINode`
- **구현 파일**: `Node\Algorithm\AlgorithmImageROINode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| `출력파일 이름` | `문자열` | 부동산 |

---

#### 18. 알고리즘KB노드

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgorithmKBNode`
- **구현 파일**: `Node\Algorithm\AlgorithmKBNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |

---

#### 19. 알고리즘KBOutputNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgorithmKBOutputNode`
- **구현 파일**: `Node\Algorithm\AlgorithmKBOutputNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |

---

#### 20.알고리즘노드

- **전체 유형**: `FlowEngineLib.Algorithm;.AlgorithmNode`
- **구현 파일**: `Algorithm\AlgorithmNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '알고리즘' | `알고리즘 유형` | 부동산 |
| `임시 이름` | `문자열` | 부동산 |
| `POITemp이름` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| '색상' | `CVOLED_COLOR` | 부동산 |
| `버퍼렌` | `정수` | 부동산 |

---

#### 21. 알고리즘OLED노드

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgorithmOLEDNode`
- **구현 파일**: `Node\Algorithm\AlgorithmOLEDNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '알고리즘' | '알고리즘OLED 유형' | 부동산 |
| '색상' | `CVOLED_COLOR` | 부동산 |
| `임시 이름` | `문자열` | 부동산 |
| 'FDA 유형' | `CVOLED_FDA유형` | 부동산 |
| '고정LED포인트' | `PointFloat[]` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| `출력파일 이름` | `문자열` | 부동산 |
| `ImgPosResultFile` | `문자열` | 부동산 |

---

#### 22. 알고리즘OLED_AOINode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgorithmOLED_AOINode`
- **구현 파일**: `Node\Algorithm\AlgorithmOLED_AOINode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '알고리즘' | `알고리즘OLED_AOIType` | 부동산 |
| `임시 이름` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| `출력파일 이름` | `문자열` | 부동산 |
| '커스텀SN' | `문자열` | 부동산 |
| `VhLineEnable` | `부울` | 부동산 |
| '픽셀 결함 활성화' | `부울` | 부동산 |
| '무라인에이블' | `부울` | 부동산 |

---

#### 23.알고리즘TMNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.AlgorithmTMNode`
- **구현 파일**: `Node\Algorithm\AlgorithmTMNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `템플릿파일` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |

---

#### 24. TPAlgorithm2Node- **전체 유형**: `FlowEngineLib.Node.Algorithm;.TPAlgorithm2Node`
- **구현 파일**: `Node\Algorithm\TPAlgorithm2Node.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `운영자` | `문자열` | 부동산 |
| `임시 이름` | `문자열` | 부동산 |

---

#### 25. TPAlgorithmNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.TPAlgorithmNode`
- **구현 파일**: `Node\Algorithm\TPAlgorithmNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '알고리즘' | `TP알고리즘 유형` | 부동산 |
| `운영자` | `문자열` | 부동산 |
| `임시 이름` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |

---

### 카메라 클래스 노드 (14)

#### 1. AOILocAndRegPixelsCameraNode

- **전체 유형**: `FlowEngineLib;.AOILocAndRegPixelsCameraNode`
- **구현 파일**: `AOILocAndRegPixelsCameraNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `Img저장 모드` | `ImgSaveBppMode` | 부동산 |
| `Img저장 이름` | `문자열` | 부동산 |
| '평균 개수' | `정수` | 부동산 |
| '이득' | `플로트` | 부동산 |
| 'ExpTime' | `플로트` | 부동산 |
| 'IsAutoExp' | `부울` | 부동산 |
| `자동ExpTemp이름` | `문자열` | 부동산 |
| IsWithND` | `부울` | 부동산 |
| `CaliTemp이름` | `문자열` | 부동산 |
| '플립모드' | `CVImageFlipMode` | 부동산 |
| `AlgTempName` | `문자열` | 부동산 |
| '채널' | `CVOLED_채널` | 부동산 |
| `출력 온도 이름` | `문자열` | 부동산 |

---

#### 2. AOILocatePixelsCameraNode

- **전체 유형**: `FlowEngineLib;.AOILocatePixelsCameraNode`
- **구현 파일**: `AOILocatePixelsCameraNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `Img저장 모드` | `ImgSaveBppMode` | 부동산 |
| `Img저장 이름` | `문자열` | 부동산 |
| '평균 개수' | `정수` | 부동산 |
| '이득' | `플로트` | 부동산 |
| 'ExpTime' | `플로트` | 부동산 |
| 'IsAutoExp' | `부울` | 부동산 |
| `자동ExpTemp이름` | `문자열` | 부동산 |
| IsWithND` | `부울` | 부동산 |
| `CaliTemp이름` | `문자열` | 부동산 |
| '플립모드' | `CVImageFlipMode` | 부동산 |
| `AlgTempName` | `문자열` | 부동산 |
| '채널' | `CVOLED_채널` | 부동산 |

---

#### 3. AOIRegisterPixelsCameraNode

- **전체 유형**: `FlowEngineLib;.AOIRegisterPixelsCameraNode`
- **구현 파일**: `AOIRegisterPixelsCameraNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `Img저장 모드` | `ImgSaveBppMode` | 부동산 |
| `Img저장 이름` | `문자열` | 부동산 |
| '평균 개수' | `정수` | 부동산 |
| '이득' | `플로트` | 부동산 |
| 'ExpTime' | `플로트` | 부동산 |
| 'IsAutoExp' | `부울` | 부동산 |
| `자동ExpTemp이름` | `문자열` | 부동산 |
| IsWithND` | `부울` | 부동산 |
| `CaliTemp이름` | `문자열` | 부동산 |
| '플립모드' | `CVImageFlipMode` | 부동산 |
| `AlgTempName` | `문자열` | 부동산 |
| '채널' | `CVOLED_채널` | 부동산 |
| `출력 온도 이름` | `문자열` | 부동산 |

---

#### 4. 베이스카메라노드

- **전체 유형**: `FlowEngineLib;.BaseCameraNode`
- **구현 파일**: `BaseCameraNode.cs`
- **기본 클래스**: `CVBaseServerNode`| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '평균 개수' | `정수` | 부동산 |
| '이득' | `플로트` | 부동산 |
| 'ExpTime' | `플로트` | 부동산 |
| `CaliTemp이름` | `문자열` | 부동산 |
| '플립모드' | `CVImageFlipMode` | 부동산 |
| `POITemp이름` | `문자열` | 부동산 |
| `POIFilterTempName` | `문자열` | 부동산 |
| `POIReviseTempName` | `문자열` | 부동산 |

---

#### 5. CVAOI2CameraNode

- **전체 유형**: `FlowEngineLib.Node.Camera;.CVAOI2CameraNode`
- **구현 파일**: `Node\Camera\CVAOI2CameraNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '캠온도이름' | `문자열` | 부동산 |
| `Img저장 모드` | `ImgSaveBppMode` | 부동산 |
| '플립모드' | `CVImageFlipMode` | 부동산 |
| 'IsAutoExp' | `부울` | 부동산 |
| `임시 이름` | `문자열` | 부동산 |
| IsWithND` | `부울` | 부동산 |
| `CalibTempName` | `문자열` | 부동산 |
| `AOI 유형` | `AOI2TypeEnum` | 부동산 |
| `AlgTempName` | `문자열` | 부동산 |

---

#### 6. CVAOICameraNode

- **전체 유형**: `FlowEngineLib.Node.Camera;.CVAOICameraNode`
- **구현 파일**: `Node\Camera\CVAOICameraNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '캠온도이름' | `문자열` | 부동산 |
| `Img저장 모드` | `ImgSaveBppMode` | 부동산 |
| '플립모드' | `CVImageFlipMode` | 부동산 |
| 'IsAutoExp' | `부울` | 부동산 |
| `임시 이름` | `문자열` | 부동산 |
| IsWithND` | `부울` | 부동산 |
| `CalibTempName` | `문자열` | 부동산 |
| `AOI 유형` | `AOITypeEnum` | 부동산 |
| `AlgTempName` | `문자열` | 부동산 |

---

#### 7. CVCameraDataFlow

- **전체 유형**: `FlowEngineLib;.CVCameraDataFlow`
- **구현 파일**: `CVCameraDataFlow.cs`
- **기본 클래스**: `CVMQTTRequest`

---

#### 8. CVCameraNode

- **전체 유형**: `FlowEngineLib;.CVCameraNode`
- **구현 파일**: `CVCameraNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '평균 개수' | `정수` | 부동산 |
| '이득' | `플로트` | 부동산 |
| '온도R' | `플로트` | 부동산 |
| '온도G' | `플로트` | 부동산 |
| '온도B' | `플로트` | 부동산 |
| `CV2LV채널` | `CV2LV채널 모드` | 부동산 |
| `CalibTempName` | `문자열` | 부동산 |
| '플립모드' | `CVImageFlipMode` | 부동산 |
| `POITemp이름` | `문자열` | 부동산 |
| `POIFilterTempName` | `문자열` | 부동산 |
| `POIReviseTempName` | `문자열` | 부동산 |

---

#### 9. CVXRCameraNode

- **전체 유형**: `FlowEngineLib;.CVXRCameraNode`
- **구현 파일**: `CVXRCameraNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '평균 개수' | `정수` | 부동산 |
| '이득' | `플로트` | 부동산 |
| '온도R' | `플로트` | 부동산 |
| '온도G' | `플로트` | 부동산 |
| '온도B' | `플로트` | 부동산 |
| `CalibTempName` | `문자열` | 부동산 |
| '플립모드' | `CVImageFlipMode` | 부동산 |
| `POITemp이름` | `문자열` | 부동산 |
| `POIFilterTempName` | `문자열` | 부동산 |
| `POIReviseTempName` | `문자열` | 부동산 |
| '알고리즘' | `알고리즘ARVR 유형` | 부동산 |
| `XRTemp이름` | `문자열` | 부동산 |

---

#### 10.캠모터노드- **전체 유형**: `FlowEngineLib;.CamMotorNode`
- **구현 파일**: `CamMotorNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `실행 유형` | 'CamMotorRunType' | 부동산 |
| 'IsAbs' | `부울` | 부동산 |
| '위치' | `정수` | 부동산 |
| '조리개' | `플로트` | 부동산 |
| '자동 초점 온도' | `문자열` | 부동산 |

---

#### 11. 카메라ROI노드

- **전체 유형**: `FlowEngineLib.Node.Camera;.CameraROINode`
- **구현 파일**: `Node\Camera\CameraROINode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `ROI_X` | `정수` | 부동산 |
| `ROI_Y` | `정수` | 부동산 |
| `ROI_폭` | `정수` | 부동산 |
| `ROI_높이` | `정수` | 부동산 |

---

#### 12. CommCameraNode

- **전체 유형**: `FlowEngineLib.Node.Camera;.CommCameraNode`
- **구현 파일**: `Node\Camera\CommCameraNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| 'IsHDR' | `부울` | 부동산 |
| '캠온도이름' | `문자열` | 부동산 |
| '플립모드' | `CVImageFlipMode` | 부동산 |
| 'IsAutoExp' | `부울` | 부동산 |
| `임시 이름` | `문자열` | 부동산 |
| IsWithND` | `부울` | 부동산 |
| `CalibTempName` | `문자열` | 부동산 |
| `POITemp이름` | `문자열` | 부동산 |
| `POIFilterTempName` | `문자열` | 부동산 |
| `POIReviseTempName` | `문자열` | 부동산 |

---

#### 13. LVCamera노드

- **전체 유형**: `FlowEngineLib;.LVCameraNode`
- **구현 파일**: `LVCameraNode.cs`
- **기본 클래스**: `BaseCameraNode`

---

#### 14. LVXRCameraNode

- **전체 유형**: `FlowEngineLib;.LVXRCameraNode`
- **구현 파일**: `LVXRCameraNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '평균 개수' | `정수` | 부동산 |
| '이득' | `플로트` | 부동산 |
| 'ExpTime' | `플로트` | 부동산 |
| `CaliTemp이름` | `문자열` | 부동산 |
| '플립모드' | `CVImageFlipMode` | 부동산 |
| `POITemp이름` | `문자열` | 부동산 |
| `POIFilterTempName` | `문자열` | 부동산 |
| `POIReviseTempName` | `문자열` | 부동산 |
| '알고리즘' | `알고리즘ARVR 유형` | 부동산 |
| `XRTemp이름` | `문자열` | 부동산 |

---

### 장치 클래스 노드(1)

#### 1. PhyDeviceControlNode

- **전체 유형**: `FlowEngineLib.Node.Global;.PhyDeviceControlNode`
- **구현 파일**: `Node\Global\PhyDeviceControlNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `장치 유형` | `CVDeviceType` | 부동산 |
| `명령 유형` | `CVDeviceControlCmd` | 부동산 |

---

### 종료 클래스 노드(2)

#### 1. CVEndNode

- **전체 유형**: `FlowEngineLib.End;.CVEndNode`
- **구현 파일**: `End\CVEndNode.cs`
- **기본 클래스**: `CVCommonNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `m_in_start` | `STNodeOption` | 필드 |

---

#### 2. CVEndV5Node

- **전체 유형**: `FlowEngineLib.End;.CVEndV5Node`
- **구현 파일**: `End\CVEndV5Node.cs`
- **기본 클래스**: `CVCommonNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `m_in_start` | `STNodeOption` | 필드 |

---

### FW 클래스 노드(1)

#### 1. FW노드

- **전체 유형**: `FlowEngineLib;.FWNode`
- **구현 파일**: `FWNode.cs`
- **기본 클래스**: `CVBaseServerNode`| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '항구' | `정수` | 부동산 |
| '모델 유형' | 'FWModelType' | 부동산 |

---

### 루프 클래스 노드(2)

#### 1. LoopNextNode

- **전체 유형**: `FlowEngineLib;.LoopNextNode`
- **구현 파일**: `LoopNextNode.cs`
- **기본 클래스**: `CVCommonNode`

---

#### 2. 루프노드

- **전체 유형**: `FlowEngineLib;.LoopNode`
- **구현 파일**: `LoopNode.cs`
- **기본 클래스**: `CVCommonNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `BeginVal` | `플로트` | 부동산 |
| 'EndVal' | `플로트` | 부동산 |
| `StepVal` | `플로트` | 부동산 |

---

### MQTT 클래스 노드(5)

#### 1. MQTTBaseNode

- **전체 유형**: `FlowEngineLib.MQTT;.MQTTBaseNode`
- **구현 파일**: `MQTT\MQTTBaseNode.cs`
- **기본 클래스**: `STNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `서버` | `문자열` | 부동산 |
| '항구' | `정수` | 부동산 |

---

#### 2. MQTTCustomPublishNode

- **전체 유형**: `FlowEngineLib;.MQTTCustomPublishNode`
- **구현 파일**: `MQTTCustomPublishNode.cs`
- **기본 클래스**: `MQTTBaseNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '데이터' | `문자열` | 부동산 |
| `주제` | `문자열` | 부동산 |

---

#### 3. MQTTCustomSubscribeNode

- **전체 유형**: `FlowEngineLib;.MQTTCustomSubscribeNode`
- **구현 파일**: `MQTTCustomSubscribeNode.cs`
- **기본 클래스**: `MQTTBaseNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `주제` | `문자열` | 부동산 |

---

#### 4. MQTTStartNode

- **전체 유형**: `FlowEngineLib.Start;.MQTTStartNode`
- **구현 파일**: `Start\MQTTStartNode.cs`
- **기본 클래스**: `BaseStartNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `서버` | `문자열` | 부동산 |
| '항구' | `정수` | 부동산 |

---

#### 5. MQTTStartV5Node

- **전체 유형**: `FlowEngineLib.Start;.MQTTStartV5Node`
- **구현 파일**: `Start\MQTTStartV5Node.cs`
- **기본 클래스**: `BaseStartNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `서버` | `문자열` | 부동산 |
| '항구' | `정수` | 부동산 |

---

### 수동 클래스 노드(1)

#### 1. ManualConfirmNode

- **전체 유형**: `FlowEngineLib;.ManualConfirmNode`
- **구현 파일**: `ManualConfirmNode.cs`
- **기본 클래스**: `CVCommonNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `메시지 텍스트` | `문자열` | 부동산 |

---

### OLED 클래스 노드 (7)

#### 1. OLEDCombineQuaterImages_4In1Node

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.OLEDCombineQuaterImages_4In1Node`
- **구현 파일**: `Node\Algorithm\OLEDCombineQuaterImages_4In1Node.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `Img파일 이름1` | `문자열` | 부동산 |
| `Img파일 이름2` | `문자열` | 부동산 |
| `Img파일 이름3` | `문자열` | 부동산 |
| `Img파일 이름4` | `문자열` | 부동산 |
| `출력파일 이름` | `문자열` | 부동산 |

---

#### 2. OLEDFindPixelDefectsForQuardImgNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.OLEDFindPixelDefectsForQuardImgNode`
- **구현 파일**: `Node\Algorithm\OLEDFindPixelDefectsForQuardImgNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| `출력파일 이름` | `문자열` | 부동산 |

---#### 3. OLEDImageCroppingNode

- **전체 유형**: `FlowEngineLib.Node.OLED;.OLEDImageCroppingNode`
- **구현 파일**: `Node\OLED\OLEDImageCroppingNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |

---

#### 4. OLEDJNDCalVasNode

- **전체 유형**: `FlowEngineLib.Node.OLED;.OLEDJNDCalVasNode`
- **구현 파일**: `Node\OLED\OLEDJNDCalVasNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `주문지수` | `정수` | 부동산 |
| `임시 이름` | `문자열` | 부동산 |
| '알고리즘' | `알고리즘2유형` | 부동산 |
| `버퍼렌` | `정수` | 부동산 |
| 'IsAdd' | `부울` | 부동산 |

---

#### 5. OLEDParticlesFindAndFillNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm;.OLEDParticlesFindAndFillNode`
- **구현 파일**: `Node\Algorithm\OLEDParticlesFindAndFillNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `입자 유형` | `입자 모드` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| `출력파일 이름` | `문자열` | 부동산 |

---

#### 6. OLEDRebuildPixelsNode

- **전체 유형**: `FlowEngineLib.Node.OLED;.OLEDRebuildPixelsNode`
- **구현 파일**: `Node\OLED\OLEDRebuildPixelsNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '채널' | `CVOLED_채널` | 부동산 |
| `임시 이름` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| `출력템플릿이름` | `문자열` | 부동산 |

---

#### 7. OLEDRebuildPixelsPosNode

- **전체 유형**: `FlowEngineLib.Node.OLED;.OLEDRebuildPixelsPosNode`
- **구현 파일**: `Node\OLED\OLEDRebuildPixelsPosNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| '채널' | `CVOLED_채널` | 부동산 |
| `출력템플릿이름` | `문자열` | 부동산 |

---

### 기타 클래스 노드 (8)

#### 1. CVBaseServerNode

- **전체 유형**: `FlowEngineLib.Base;.CVBaseServerNode`
- **구현 파일**: `Base\CVBaseServerNode.cs`
- **기본 클래스**: `CVCommonNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '토큰' | `문자열` | 부동산 |
| '최대 시간' | `정수` | 부동산 |
| `자막` | `문자열` | 부동산 |

---

#### 2. CVBaseServerNodeHub

- **전체 유형**: `FlowEngineLib.Base;.CVBaseServerNodeHub`
- **구현 파일**: `Base\CVBaseServerNodeHub.cs`
- **기본 클래스**: `CVBaseServerNode`

---

#### 3. CVBaseServerNodeIn2Hub

- **전체 유형**: `FlowEngineLib.Base;.CVBaseServerNodeIn2Hub`
- **구현 파일**: `Base\CVBaseServerNodeIn2Hub.cs`
- **기본 클래스**: `CVBaseServerNode`

---

#### 4. CVCommonNode

- **전체 유형**: `FlowEngineLib.Base;.CVCommonNode`
- **구현 파일**: `Base\CVCommonNode.cs`
- **기본 클래스**: `STNode`| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `노드 이름` | `문자열` | 부동산 |
| `노드 유형` | `문자열` | 부동산 |
| '디바이스코드' | `문자열` | 부동산 |
| `노드ID` | `문자열` | 부동산 |
| `Z인덱스` | `정수` | 부동산 |
| `노드이벤트` | `FlowEngineNodeEvent` | 부동산 |
| `nodeRunEvent` | `FlowEngineNodeRunEvent` | 부동산 |
| `nodeEndEvent` | `FlowEngineNodeEndEvent` | 부동산 |

---

#### 5. Calibration2InNode

- **전체 유형**: `FlowEngineLib.Node.OLED;.Calibration2InNode`
- **구현 파일**: `Node\OLED\Calibration2InNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `ExpTempName` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| 'IsSaveCIE' | `부울` | 부동산 |
| `POIFilterTempName` | `문자열` | 부동산 |
| `POIReviseTempName` | `문자열` | 부동산 |
| `출력템플릿이름` | `문자열` | 부동산 |

---

#### 6. 교정 노드

- **전체 유형**: `FlowEngineLib.Algorithm;.CalibrationNode`
- **구현 파일**: `Algorithm\CalibrationNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `ExpTempName` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| 'IsSaveCIE' | `부울` | 부동산 |
| `POITemp이름` | `문자열` | 부동산 |
| `POIFilterTempName` | `문자열` | 부동산 |
| `POIReviseTempName` | `문자열` | 부동산 |
| `출력템플릿이름` | `문자열` | 부동산 |

---

#### 7. 교정ROI노드

- **전체 유형**: `FlowEngineLib.Node.Camera;.CalibrationROINode`
- **구현 파일**: `Node\Camera\CalibrationROINode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `ROI_X` | `정수` | 부동산 |
| `ROI_Y` | `정수` | 부동산 |
| `ROI_폭` | `정수` | 부동산 |
| `ROI_높이` | `정수` | 부동산 |

---

#### 8. 모터노드

- **전체 유형**: `FlowEngineLib;.MotorNode`
- **구현 파일**: `MotorNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `실행 유형` | `MotorRunType` | 부동산 |
| 'IsAbs' | `부울` | 부동산 |
| '위치' | `정수` | 부동산 |
| '조리개' | `플로트` | 부동산 |

---

### PG 클래스 노드(1)

#### 1. PG노드

- **전체 유형**: `FlowEngineLib.Node.PG;.PGNode`
- **구현 파일**: `Node\PG\PGNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `PGCmd` | `PGCommCmdType` | 부동산 |
| `인덱스프레임` | `정수` | 부동산 |

---

### POI 클래스 노드 (8)

#### 1. POI2Node 빌드

- **전체 유형**: `FlowEngineLib.Node.POI;.BuildPOI2Node`
- **구현 파일**: `Node\POI\BuildPOI2Node.cs`
- **기본 클래스**: `CVBaseServerNodeHub`| 직업명 | C# 형식 | 来源 |
|---------|---------|------|
| `템플릿 이름` | `문자열` | 부동산 |
| `빌드 유형` | POIBuildType` | 부동산 |
| `접두사 이름` | `문자열` | 부동산 |
| `POI유형` | POIPointTypes` | 부동산 |
| POI높이' | `정수` | 부동산 |
| POI폭' | `정수` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| POI출력` | POIStorageModel` | 부동산 |
| `출력파일 이름` | `문자열` | 부동산 |
| `SavePOITempName` | `문자열` | 부동산 |

---

#### 2. POINode 빌드

- **유형**: `FlowEngineLib;.BuildPOINode`
- **문서 내용**: `BuildPOINode.cs`
- **기준**: `CVBaseServerNode`

| 직업명 | C# 형식 | 来源 |
|---------|---------|------|
| `템플릿 이름` | `문자열` | 부동산 |
| `RePOITemplateName` | `문자열` | 부동산 |
| `레이아웃ROI템플릿` | `문자열` | 부동산 |
| `빌드 유형` | POIBuildType` | 부동산 |
| `접두사 이름` | `문자열` | 부동산 |
| `POI유형` | POIPointTypes` | 부동산 |
| POI높이' | `정수` | 부동산 |
| POI폭' | `정수` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| `CAD_Pos파일 이름` | `문자열` | 부동산 |
| POI출력` | POIStorageModel` | 부동산 |
| `출력파일 이름` | `문자열` | 부동산 |
| `SavePOITempName` | `문자열` | 부동산 |
| `버퍼렌` | `정수` | 부동산 |

---

#### 3. POIAnalyticAndSMUnode

- **유형**: `FlowEngineLib.Node.POI;.POIAnalyticAndSMUnode`
- **문서 내용**: `Node\POI\POIAnalyticAndSMUnode.cs`
- **기준**: `CVBaseServerNodeHub`

| 직업명 | C# 형식 | 来源 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |

---

#### 4. POIAnalyticNode

- **유형**: `FlowEngineLib.Node.POI;.POIAnalyticNode`
- **문서 내용**: `Node\POI\POIAnalyticNode.cs`
- **기준**: `CVBaseServerNode`

| 직업명 | C# 형식 | 来源 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |

---

#### 5. POICADMappingNode

- **유형**: `FlowEngineLib.Node.POI;.POICADMappingNode`
- **문서 내용**: `Node\POI\POICADMappingNode.cs`
- **기준**: `CVBaseServerNode`

| 직업명 | C# 형식 | 来源 |
|---------|---------|------|
| `템플릿 이름` | `문자열` | 부동산 |
| `매핑 유형` | POIBuildType` | 부동산 |
| `CAD파일 이름` | `문자열` | 부동산 |
| `접두사 이름` | `문자열` | 부동산 |

---

#### 6. POI노드

- **저장 형식**: `FlowEngineLib;.POINode`
- **문서 내용**: `POINode.cs`
- **기준**: `CVBaseServerNode`

| 직업명 | C# 형식 | 来源 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `필터템플릿이름` | `문자열` | 부동산 |
| '템플릿 이름 수정' | `문자열` | 부동산 |
| `출력템플릿이름` | `문자열` | 부동산 |
| `Img파일 이름` | `문자열` | 부동산 |
| 'IsCCTWave' | `부울` | 부동산 |
| IsSubPixel` | `부울` | 부동산 |

---

#### 7. POIReviseNode

- **유형**: `FlowEngineLib.Node.POI;.POIReviseNode`
- **문서 내용**: `Node\POI\POIReviseNode.cs`
- **기준**: `CVBaseServerNodeHub`

| 직업명 | C# 형식 | 来源 |
|---------|---------|------|
| `템플릿 이름` | `문자열` | 부동산 |
| `POI포인트 이름` | `문자열` | 부동산 |
| `IsSelfResultRevise` | `부울` | 부동산 |

---

#### 8. RealPOI노드- **전체 유형**: `FlowEngineLib.Node.POI;.RealPOINode`
- **구현 파일**: `Node\POI\RealPOINode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `Img파일 이름` | `문자열` | 부동산 |
| `필터템플릿이름` | `문자열` | 부동산 |
| '템플릿 이름 수정' | `문자열` | 부동산 |
| `파일 이름 수정` | `문자열` | 부동산 |
| `출력템플릿이름` | `문자열` | 부동산 |
| `하위 픽셀 템플릿 이름` | `문자열` | 부동산 |
| `POI유형` | POIPointTypes` | 부동산 |
| POI높이' | `플로트` | 부동산 |
| POI폭' | `플로트` | 부동산 |
| 'IsResultAdd' | `부울` | 부동산 |
| 'IsCCTWave' | `부울` | 부동산 |

---

### SMU 클래스 노드 (7)

#### 1. SMUBaseNode

- **전체 유형**: `FlowEngineLib;.SMUBaseNode`
- **구현 파일**: `SMUBaseNode.cs`
- **기본 클래스**: `CVBaseServerNode`, `ICVLoopNextNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `루프 이름` | `문자열` | 부동산 |
| 'IsCloseOutput' | `부울` | 부동산 |
| `시작됨` | `부울` | 필드 |

---

#### 2. SMUFromCSVNode

- **전체 유형**: `FlowEngineLib;.SMUFromCSVNode`
- **구현 파일**: `SMUFromCSVNode.cs`
- **기본 클래스**: `SMUBaseNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '출처' | `소스 유형` | 부동산 |
| '채널' | `SMU채널 유형` | 부동산 |
| `Csv파일 이름` | `문자열` | 부동산 |
| 'IsAutoRng' | `부울` | 부동산 |
| `SrcRng` | `더블` | 부동산 |
| `LmtRng` | `더블` | 부동산 |

---

#### 3. SMUModelNode

- **전체 유형**: `FlowEngineLib;.SMUModelNode`
- **구현 파일**: `SMUModelNode.cs`
- **기본 클래스**: `SMUBaseNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '모델명' | `문자열` | 부동산 |

---

#### 4. SMU노드

- **전체 유형**: `FlowEngineLib;.SMUnode`
- **구현 파일**: `SMUnode.cs`
- **기본 클래스**: `SMUBaseNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '출처' | `소스 유형` | 부동산 |
| '채널' | `SMU채널 유형` | 부동산 |
| `BeginVal` | `플로트` | 부동산 |
| 'EndVal' | `플로트` | 부동산 |
| 'LimitVal' | `플로트` | 부동산 |
| '포인트넘버' | `정수` | 부동산 |
| 'IsAutoRng' | `부울` | 부동산 |
| `SrcRng` | `더블` | 부동산 |
| `LmtRng` | `더블` | 부동산 |

---

#### 5. SMUReaderNode

- **전체 유형**: `FlowEngineLib.Node.SMU;.SMUReaderNode`
- **구현 파일**: `Node\SMU\SMUReaderNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '대기 시간' | `정수` | 부동산 |

---

#### 6. SMUSweepModelNode

- **전체 유형**: `FlowEngineLib.Node.SMU;.SMUSweepModelNode`
- **구현 파일**: `Node\SMU\SMUSweepModelNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '모델명' | `문자열` | 부동산 |
| 'IsCloseOutput' | `부울` | 부동산 |

---

#### 7. SMUSweepNode

- **전체 유형**: `FlowEngineLib.Node.SMU;.SMUSweepNode`
- **구현 파일**: `Node\SMU\SMUSweepNode.cs`
- **기본 클래스**: `CVBaseServerNode`| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| '출처' | `소스 유형` | 부동산 |
| '채널' | `SMU채널 유형` | 부동산 |
| `BeginVal` | `플로트` | 부동산 |
| 'EndVal' | `플로트` | 부동산 |
| 'LimitVal' | `플로트` | 부동산 |
| '포인트넘버' | `정수` | 부동산 |
| 'IsCloseOutput' | `부울` | 부동산 |
| 'IsAutoRng' | `부울` | 부동산 |
| `SrcRng` | `더블` | 부동산 |
| `LmtRng` | `더블` | 부동산 |

---

### 센서 클래스 노드(3)

#### 1. 커먼센서노드

- **전체 유형**: `FlowEngineLib;.CommonSensorNode`
- **구현 파일**: `CommonSensorNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |
| `명령 유형` | `CommCmdType` | 부동산 |
| `CmdSend` | `문자열` | 부동산 |
| `CmdReceive` | `문자열` | 부동산 |

---

#### 2. RealCommonSensorNode

- **전체 유형**: `FlowEngineLib;.RealCommonSensorNode`
- **구현 파일**: `RealCommonSensorNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `명령 유형` | `CommSensorCmdType` | 부동산 |
| `CmdSend` | `문자열` | 부동산 |
| `CmdReceive` | `문자열` | 부동산 |
| `CmdTimeout` | `정수` | 부동산 |
| '재시도 횟수' | `정수` | 부동산 |
| `지연` | `정수` | 부동산 |

---

#### 3. TempCommonSensorNode

- **전체 유형**: `FlowEngineLib;.TempCommonSensorNode`
- **구현 파일**: `TempCommonSensorNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `임시 이름` | `문자열` | 부동산 |

---

### 스펙트럼 클래스 노드(2)

#### 1. 스펙트럼EQENode

- **전체 유형**: `FlowEngineLib.Node.Spectrum;.SpectrumEQENode`
- **구현 파일**: `Node\Spectrum\SpectrumEQENode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `A팩터` | `플로트` | 부동산 |
| 'IsCustomVI' | `부울` | 부동산 |
| '전압' | `플로트` | 부동산 |
| '현재' | `플로트` | 부동산 |
| '온도' | `플로트` | 부동산 |
| `AveNum` | `정수` | 부동산 |
| `AutoIntTime` | `부울` | 부동산 |
| IsWithND` | `부울` | 부동산 |
| '셀프다크' | `부울` | 부동산 |
| `AutoInitDark` | `부울` | 부동산 |
| `출력데이터파일 이름` | `문자열` | 부동산 |

---

#### 2. 스펙트럼 노드

- **전체 유형**: `FlowEngineLib.Node.Spectrum;.SpectrumNode`
- **구현 파일**: `Node\Spectrum\SpectrumNode.cs`
- **기본 클래스**: `CVBaseServerNode`

| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `PGCmd` | `SPCommCmdType` | 부동산 |
| '온도' | `플로트` | 부동산 |
| `AveNum` | `정수` | 부동산 |
| `AutoIntTime` | `부울` | 부동산 |
| IsWithND` | `부울` | 부동산 |
| '셀프다크' | `부울` | 부동산 |
| `AutoInitDark` | `부울` | 부동산 |
| `출력데이터파일 이름` | `문자열` | 부동산 |

---

### 클래스 노드 시작(3)

#### 1. CVStartCFC

- **전체 유형**: `FlowEngineLib.Base;.CVStartCFC`
- **구현 파일**: `Base\CVStartCFC.cs`
- **기본 클래스**: `CVBaseCFC`

---

#### 2. 수동시작노드

- **전체 유형**: `FlowEngineLib;.ManualStartNode`
- **구현 파일**: `ManualStartNode.cs`
- **기본 클래스**: `BaseStartNode`| 부동산 이름 | C# 유형 | 소스 |
|---------|---------|------|
| `SN` | `문자열` | 부동산 |
| '액션' | `ActionTypeEnum` | 부동산 |

---

#### 3. ModbusStartNode

- **전체 유형**: `FlowEngineLib.Start;.ModbusStartNode`
- **구현 파일**: `Start\ModbusStartNode.cs`
- **기본 클래스**: `BaseStartNode`

---
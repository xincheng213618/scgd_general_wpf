# Flow Engine 노드 참조 문서

> 다음 소스 코드 디렉터리를 기반으로 2026-05-22에 자동 생성됩니다.
> - 노드 구성: `Engine\ColorVision.Engine\Templates\Flow\NodeConfigurator\`
> - 노드 구현: `Engine\FlowEngineLib\`

## 개요

총 **42**개의 구성된 노드가 있으며 다음과 같이 유형별로 그룹화됩니다.

| 유형 | 수량 |
|------|------|
| 알고리즘 | 17 |
| 카메라 | 8 |
| OLED | 2 |
| POI | 5 |
| PG | 1 |
| SMU | 3 |
| 센서 | 2 |
| FW | 1 |
| 스펙트럼 | 3 |

## 알고리즘 클래스 노드

### 1.알고리즘ARVRNode

- **전체 유형**: `FlowEngineLib.Algorithm.AlgorithmARVRNode`
- **구성자**: `AlgorithmNodeConfigurators.cs`
- **구현 파일**: `Algorithm\AlgorithmARVRNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Img파일 이름` | 이미지 경로 | 이미지 파일 경로 |
| `임시 이름` | 템플릿참조 | 기동특무부대 |
| `POITemp이름` | 템플릿참조 | POI |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| '알고리즘' | `알고리즘ARVR 유형` |
| `임시 이름` | `문자열` |
| `POITemp이름` | `문자열` |
| `Img파일 이름` | `문자열` |
| '색상' | `CVOLED_COLOR` |
| `버퍼렌` | `정수` |

---

### 2. 알고리즘 노드

- **전체 유형**: `FlowEngineLib.Algorithm.AlgorithmNode`
- **구성자**: `AlgorithmNodeConfigurators.cs`
- **구현 파일**: `Algorithm\AlgorithmNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Img파일 이름` | 이미지 경로 | 이미지 파일 경로 |
| `POITemp이름` | 템플릿참조 | POI |
| `임시 이름` | 템플릿참조 | 기동특무부대 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| '알고리즘' | `알고리즘 유형` |
| `임시 이름` | `문자열` |
| `POITemp이름` | `문자열` |
| `Img파일 이름` | `문자열` |
| '색상' | `CVOLED_COLOR` |
| `버퍼렌` | `정수` |

---

### 3. 교정 노드

- **전체 유형**: `FlowEngineLib.Algorithm.CalibrationNode`
- **구성자**: `DeviceNodeConfigurators.cs`
- **구현 파일**: `Algorithm\CalibrationNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Img파일 이름` | 이미지 경로 | 이미지 파일 경로 |
| `임시 이름` | 템플릿참조 | 교정 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |
| `ExpTempName` | `문자열` |
| `Img파일 이름` | `문자열` |
| 'IsSaveCIE' | `부울` |
| `POITemp이름` | `문자열` |
| `POIFilterTempName` | `문자열` |
| `POIReviseTempName` | `문자열` |
| `출력템플릿이름` | `문자열` |

---

### 4. AlgComplianceMathNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm.AlgComplianceMathNode`
- **구성자**: `AlgorithmNodeConfigurators.cs`
- **구현 파일**: `Node\Algorithm\AlgComplianceMathNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `임시 이름` | 템플릿참조 | JND |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |
| `규정 준수 수학` | `ComplianceMathType` |
| 'IsBreak' | `부울` |

---

### 5. AlgDataLoadNode- **전체 유형**: `FlowEngineLib.Node.Algorithm.AlgDataLoadNode`
- **구성자**: `AlgorithmNodeConfigurators.cs`
- **구현 파일**: `Node\Algorithm\AlgDataLoadNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `임시 이름` | 템플릿참조 | 템플릿 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |

---

### 6. 알고리즘BlackMuraNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm.AlgorithmBlackMuraNode`
- **구성자**: `AlgorithmNodeConfigurators.cs`
- **구현 파일**: `Node\Algorithm\AlgorithmBlackMuraNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Img파일 이름` | 이미지 경로 | 이미지 파일 경로 |
| `임시 이름` | 템플릿JsonRef | 블랙무라 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |
| `Img파일 이름` | `문자열` |
| `O인덱스` | `문자열` |
| `SavePOITempName` | `문자열` |

---

### 7. 알고리즘CaliNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm.AlgorithmCaliNode`
- **구성자**: `AlgorithmNodeConfigurators.cs`
- **구현 파일**: `Node\Algorithm\AlgorithmCaliNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Img파일 이름` | 이미지 경로 | 이미지 파일 경로 |
| `임시 이름` | 템플릿JsonRef | 색상 차이 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |
| `Img파일 이름` | `문자열` |
| `출력파일 이름` | `문자열` |

---

### 8. 알고리즘FindLEDNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm.AlgorithmFindLEDNode`
- **구성자**: `AlgorithmNodeConfigurators.cs`
- **구현 파일**: `Node\Algorithm\AlgorithmFindLEDNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Img파일 이름` | 이미지 경로 | 이미지 파일 경로 |
| `임시 이름` | 템플릿참조 | 픽셀 수준의 램프 비드 감지 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| '색상' | `CVOLED_채널` |
| `임시 이름` | `문자열` |
| 'FDA 유형' | `CVOLED_FDA유형` |
| '고정LED포인트' | `PointFloat[]` |
| `Img파일 이름` | `문자열` |
| `출력파일 이름` | `문자열` |
| `ImgPosResultFile` | `문자열` |

---

### 9. 알고리즘FindLightAreaNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm.AlgorithmFindLightAreaNode`
- **구성자**: `AlgorithmNodeConfigurators.cs`
- **구현 파일**: `Node\Algorithm\AlgorithmFindLightAreaNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Img파일 이름` | 이미지 경로 | 이미지 파일 경로 |
| `임시 이름` | 템플릿참조 | 빛나는 영역 위치 |
| `SavePOITempName` | 템플릿참조 | POI 저장 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |
| `Img파일 이름` | `문자열` |
| `SavePOITempName` | `문자열` |
| `버퍼렌` | `정수` |
| `O인덱스` | `문자열` |

---### 10. 알고리즘GhostV2Node

- **전체 유형**: `FlowEngineLib.Node.Algorithm.AlgorithmGhostV2Node`
- **구성자**: `AlgorithmNodeConfigurators.cs`
- **구현 파일**: `Node\Algorithm\AlgorithmGhostV2Node.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Img파일 이름` | 이미지 경로 | 이미지 파일 경로 |
| `임시 이름` | 템플릿참조 | 유령 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |
| `Img파일 이름` | `문자열` |
| `버퍼렌` | `정수` |

---

### 11.알고리즘ImageROINode

- **전체 유형**: `FlowEngineLib.Node.Algorithm.AlgorithmImageROINode`
- **구성자**: `AlgorithmNodeConfigurators.cs`
- **구현 파일**: `Node\Algorithm\AlgorithmImageROINode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Img파일 이름` | 이미지 경로 | 이미지 파일 경로 |
| `임시 이름` | 템플릿JsonRef | 템플릿 이름 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |
| `Img파일 이름` | `문자열` |
| `출력파일 이름` | `문자열` |

---

### 12.알고리즘KBNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm.AlgorithmKBNode`
- **구성자**: `AlgorithmNodeConfigurators.cs`
- **구현 파일**: `Node\Algorithm\AlgorithmKBNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Img파일 이름` | 이미지 경로 | 이미지 파일 경로 |
| `임시 이름` | 템플릿KBRef | KB |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |
| `Img파일 이름` | `문자열` |

---

### 13. 알고리즘KBOutputNode

- **전체 유형**: `FlowEngineLib.Node.Algorithm.AlgorithmKBOutputNode`
- **구성자**: `AlgorithmNodeConfigurators.cs`
- **구현 파일**: `Node\Algorithm\AlgorithmKBOutputNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `임시 이름` | 템플릿KBRef | KB |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |

---

### 14. 알고리즘OLED노드

- **전체 유형**: `FlowEngineLib.Node.Algorithm.AlgorithmOLEDNode`
- **구성자**: `OLEDNodeConfigurators.cs`
- **구현 파일**: `Node\Algorithm\AlgorithmOLEDNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Img파일 이름` | 이미지 경로 | 이미지 파일 경로 |
| `임시 이름` | 템플릿JsonRef | 하위 픽셀 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| '알고리즘' | '알고리즘OLED 유형' |
| '색상' | `CVOLED_COLOR` |
| `임시 이름` | `문자열` |
| 'FDA 유형' | `CVOLED_FDA유형` |
| '고정LED포인트' | `PointFloat[]` |
| `Img파일 이름` | `문자열` |
| `출력파일 이름` | `문자열` |
| `ImgPosResultFile` | `문자열` |

---

### 15.알고리즘OLED_AOINode- **전체 유형**: `FlowEngineLib.Node.Algorithm.AlgorithmOLED_AOINode`
- **구성자**: `OLEDNodeConfigurators.cs`
- **구현 파일**: `Node\Algorithm\AlgorithmOLED_AOINode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Img파일 이름` | 이미지 경로 | 이미지 파일 경로 |
| `임시 이름` | 템플릿JsonRef | 아오이 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| '알고리즘' | `알고리즘OLED_AOIType` |
| `임시 이름` | `문자열` |
| `Img파일 이름` | `문자열` |
| `출력파일 이름` | `문자열` |
| '커스텀SN' | `문자열` |
| `VhLineEnable` | `부울` |
| '픽셀 결함 활성화' | `부울` |
| '무라인에이블' | `부울` |

---

### 16. 알고리즘2인노드

- **전체 유형**: `FlowEngineLib.Node.OLED.Algorithm2InNode`
- **구성자**: `OLEDNodeConfigurators.cs`
- **구현 파일**: `Node\OLED\Algorithm2InNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `임시 이름` | 템플릿참조 | 기동특무부대 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |
| '알고리즘' | `알고리즘2유형` |
| `버퍼렌` | `정수` |
| 'IsAdd' | `부울` |

---

### 17. 알고리즘CompoundImgNode

- **전체 유형**: `FlowEngineLib.Node.OLED.AlgorithmCompoundImgNode`
- **구성자**: `OLEDNodeConfigurators.cs`
- **구현 파일**: `Node\OLED\AlgorithmCompoundImgNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `임시 이름` | 템플릿JsonRef | 매개변수 템플릿 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |
| `출력파일 이름` | `문자열` |
| `버퍼렌` | `정수` |

---

##카메라 클래스 노드

### 1. AOILocAndRegPixelsCameraNode

- **전체 유형**: `FlowEngineLib.AOILocAndRegPixelsCameraNode`
- **구성자**: `CameraNodeConfigurators.cs`
- **구현 파일**: `AOILocAndRegPixelsCameraNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `자동ExpTemp이름` | 템플릿참조 | 노출 템플릿 |
| `CaliTemp이름` | 템플릿참조 | 교정 |
| `AlgTempName` | 템플릿JsonRef | 아오이 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `Img저장 모드` | `ImgSaveBppMode` |
| `Img저장 이름` | `문자열` |
| '평균 개수' | `정수` |
| '이득' | `플로트` |
| 'ExpTime' | `플로트` |
| 'IsAutoExp' | `부울` |
| `자동ExpTemp이름` | `문자열` |
| IsWithND` | `부울` |
| `CaliTemp이름` | `문자열` |
| '플립모드' | `CVImageFlipMode` |
| `AlgTempName` | `문자열` |
| '채널' | `CVOLED_채널` |
| `출력 온도 이름` | `문자열` |

---

### 2. AOILocatePixelsCameraNode

- **전체 유형**: `FlowEngineLib.AOILocatePixelsCameraNode`
- **구성자**: `CameraNodeConfigurators.cs`
- **구현 파일**: `AOILocatePixelsCameraNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `자동ExpTemp이름` | 템플릿참조 | 노출 템플릿 |
| `CaliTemp이름` | 템플릿참조 | 교정 |
| `AlgTempName` | 템플릿JsonRef | 하위 픽셀 램프 비드 감지 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `Img저장 모드` | `ImgSaveBppMode` |
| `Img저장 이름` | `문자열` |
| '평균 개수' | `정수` |
| '이득' | `플로트` |
| 'ExpTime' | `플로트` |
| 'IsAutoExp' | `부울` |
| `자동ExpTemp이름` | `문자열` |
| IsWithND` | `부울` |
| `CaliTemp이름` | `문자열` |
| '플립모드' | `CVImageFlipMode` |
| `AlgTempName` | `문자열` |
| '채널' | `CVOLED_채널` |

---

### 3. CVCameraNode

- **전체 유형**: `FlowEngineLib.CVCameraNode`
- **구성자**: `CameraNodeConfigurators.cs`
- **구현 파일**: `CVCameraNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `CalibTempName` | 템플릿참조 | 교정 |
| `POITemp이름` | 템플릿참조 | POI 템플릿 |
| `POIFilterTempName` | 템플릿참조 | POI 필터링 |
| `POIReviseTempName` | 템플릿참조 | POI 수정 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| '평균 개수' | `정수` |
| '이득' | `플로트` |
| '온도R' | `플로트` |
| '온도G' | `플로트` |
| '온도B' | `플로트` |
| `CV2LV채널` | `CV2LV채널 모드` |
| `CalibTempName` | `문자열` |
| '플립모드' | `CVImageFlipMode` |
| `POITemp이름` | `문자열` |
| `POIFilterTempName` | `문자열` |
| `POIReviseTempName` | `문자열` |

---

### 4.캠모터노드

- **전체 유형**: `FlowEngineLib.CamMotorNode`
- **구성자**: `CameraNodeConfigurators.cs`
- **구현 파일**: `CamMotorNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| '자동 초점 온도' | 템플릿참조 | 카메라 템플릿 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `실행 유형` | 'CamMotorRunType' |
| 'IsAbs' | `부울` |
| '위치' | `정수` |
| '조리개' | `플로트` |
| '자동 초점 온도' | `문자열` |

---

### 5. LVCameraNode

- **전체 유형**: `FlowEngineLib.LVCameraNode`
- **구성자**: `CameraNodeConfigurators.cs`
- **구현 파일**: `LVCameraNode.cs`
- **기본 클래스**: `BaseCameraNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `CaliTemp이름` | 템플릿참조 | 교정 |
| `POITemp이름` | 템플릿참조 | POI 템플릿 |
| `POIFilterTempName` | 템플릿참조 | POI 필터링 |
| `POIReviseTempName` | 템플릿참조 | POI 수정 |

---

### 6. CVAOI2CameraNode

- **전체 유형**: `FlowEngineLib.Node.Camera.CVAOI2CameraNode`
- **구성자**: `CameraNodeConfigurators.cs`
- **구현 파일**: `Node\Camera\CVAOI2CameraNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| '캠온도이름' | 템플릿참조 | 카메라 템플릿 |
| `임시 이름` | 템플릿참조 | 노출 템플릿 |
| `CalibTempName` | 템플릿참조 | 교정 |
| `AlgTempName` | 템플릿JsonRef | 아오이 |

**클래스 수준 속성**(노드 구현)| 부동산 이름 | C# 유형 |
|---------|---------|
| '캠온도이름' | `문자열` |
| `Img저장 모드` | `ImgSaveBppMode` |
| '플립모드' | `CVImageFlipMode` |
| 'IsAutoExp' | `부울` |
| `임시 이름` | `문자열` |
| IsWithND` | `부울` |
| `CalibTempName` | `문자열` |
| `AOI 유형` | `AOI2TypeEnum` |
| `AlgTempName` | `문자열` |

---

### 7. CVAIOICameraNode

- **전체 유형**: `FlowEngineLib.Node.Camera.CVAOICameraNode`
- **구성자**: `CameraNodeConfigurators.cs`
- **구현 파일**: `Node\Camera\CVAOICameraNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| '캠온도이름' | 템플릿참조 | 카메라 템플릿 |
| `임시 이름` | 템플릿참조 | 노출 템플릿 |
| `CalibTempName` | 템플릿참조 | 교정 |
| `AlgTempName` | 템플릿JsonRef | 하위 픽셀 램프 비드 감지 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| '캠온도이름' | `문자열` |
| `Img저장 모드` | `ImgSaveBppMode` |
| '플립모드' | `CVImageFlipMode` |
| 'IsAutoExp' | `부울` |
| `임시 이름` | `문자열` |
| IsWithND` | `부울` |
| `CalibTempName` | `문자열` |
| `AOI 유형` | `AOITypeEnum` |
| `AlgTempName` | `문자열` |

---

### 8. CommCameraNode

- **전체 유형**: `FlowEngineLib.Node.Camera.CommCameraNode`
- **구성자**: `CameraNodeConfigurators.cs`
- **구현 파일**: `Node\Camera\CommCameraNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `CalibTempName` | 템플릿참조 | 교정 |
| '캠온도이름' | 템플릿참조 | 카메라 템플릿 |
| `임시 이름` | 템플릿참조 | 노출 템플릿 |
| `POITemp이름` | 템플릿참조 | POI 템플릿 |
| `POIFilterTempName` | 템플릿참조 | POI 필터링 |
| `POIReviseTempName` | 템플릿참조 | POI 수정 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| 'IsHDR' | `부울` |
| '캠온도이름' | `문자열` |
| '플립모드' | `CVImageFlipMode` |
| 'IsAutoExp' | `부울` |
| `임시 이름` | `문자열` |
| IsWithND` | `부울` |
| `CalibTempName` | `문자열` |
| `POITemp이름` | `문자열` |
| `POIFilterTempName` | `문자열` |
| `POIReviseTempName` | `문자열` |

---

## OLED 클래스 노드

### 1. OLEDImageCroppingNode

- **전체 유형**: `FlowEngineLib.Node.OLED.OLEDImageCroppingNode`
- **구성자**: `OLEDNodeConfigurators.cs`
- **구현 파일**: `Node\OLED\OLEDImageCroppingNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `임시 이름` | 템플릿참조 | 매개변수 템플릿 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |
| `Img파일 이름` | `문자열` |

---

### 2. OLEDRebuildPixelsNode

- **전체 유형**: `FlowEngineLib.Node.OLED.OLEDRebuildPixelsNode`
- **구성자**: `OLEDNodeConfigurators.cs`
- **구현 파일**: `Node\OLED\OLEDRebuildPixelsNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Img파일 이름` | 이미지 경로 | 이미지 파일 경로 |
| `출력템플릿이름` | 템플릿참조 | 포이아웃풋 |
| `임시 이름` | 템플릿JsonRef | 하위 픽셀 램프 비드 감지 |**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| '채널' | `CVOLED_채널` |
| `임시 이름` | `문자열` |
| `Img파일 이름` | `문자열` |
| `출력템플릿이름` | `문자열` |

---

## POI 클래스 노드

### 1. POINode 빌드

- **전체 유형**: `FlowEngineLib.BuildPOINode`
- **구성자**: `POINodeConfigurators.cs`
- **구현 파일**: `BuildPOINode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Img파일 이름` | 이미지 경로 | 이미지 파일 경로 |
| `템플릿 이름` | 템플릿참조 | 포인트 레이아웃 템플릿 |
| `RePOITemplateName` | 템플릿참조 | 리포이 |
| `레이아웃ROI템플릿` | 템플릿참조 | 레이아웃 ROI |
| `SavePOITempName` | 템플릿참조 | 저장POI |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `템플릿 이름` | `문자열` |
| `RePOITemplateName` | `문자열` |
| `레이아웃ROI템플릿` | `문자열` |
| `빌드 유형` | POIBuildType` |
| `접두사 이름` | `문자열` |
| `POI유형` | POIPointTypes` |
| POI높이' | `정수` |
| POI폭' | `정수` |
| `Img파일 이름` | `문자열` |
| `CAD_Pos파일 이름` | `문자열` |
| POI출력` | POIStorageModel` |
| `출력파일 이름` | `문자열` |
| `SavePOITempName` | `문자열` |
| `버퍼렌` | `정수` |

---

### 2. POIA분석노드

- **전체 유형**: `FlowEngineLib.Node.POI.POIAnalyticNode`
- **구성자**: `POINodeConfigurators.cs`
- **구현 파일**: `Node\POI\POIAnalyticNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `임시 이름` | 템플릿JsonRef | 포이분석 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |

---

### 3. POIReviseNode

- **전체 유형**: `FlowEngineLib.Node.POI.POIReviseNode`
- **구성자**: `POINodeConfigurators.cs`
- **구현 파일**: `Node\POI\POIReviseNode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `템플릿 이름` | 템플릿참조 | POI 보정 교정 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `템플릿 이름` | `문자열` |
| `POI포인트 이름` | `문자열` |
| `IsSelfResultRevise` | `부울` |

---

### 4. RealPOINode

- **전체 유형**: `FlowEngineLib.Node.POI.RealPOINode`
- **구성자**: `POINodeConfigurators.cs`
- **구현 파일**: `Node\POI\RealPOINode.cs`
- **기본 클래스**: `CVBaseServerNodeHub`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `필터템플릿이름` | 템플릿참조 | POI 필터링 |
| '템플릿 이름 수정' | 템플릿참조 | POI 개정 |
| `출력템플릿이름` | 템플릿참조 | 파일 출력 템플릿 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `Img파일 이름` | `문자열` |
| `필터템플릿이름` | `문자열` |
| '템플릿 이름 수정' | `문자열` |
| `파일 이름 수정` | `문자열` |
| `출력템플릿이름` | `문자열` |
| `하위 픽셀 템플릿 이름` | `문자열` |
| `POI유형` | POIPointTypes` |
| POI높이' | `플로트` |
| POI폭' | `플로트` |
| 'IsResultAdd' | `부울` |
| 'IsCCTWave' | `부울` |---

### 5. POI노드

- **전체 유형**: `FlowEngineLib.POINode`
- **구성자**: `POINodeConfigurators.cs`
- **구현 파일**: `POINode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Img파일 이름` | 이미지 경로 | 이미지 파일 경로 |
| `임시 이름` | 템플릿참조 | POI 템플릿 |
| `필터템플릿이름` | 템플릿참조 | POI 필터링 |
| '템플릿 이름 수정' | 템플릿참조 | POI 개정 |
| `출력템플릿이름` | 템플릿참조 | 파일 출력 템플릿 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |
| `필터템플릿이름` | `문자열` |
| '템플릿 이름 수정' | `문자열` |
| `출력템플릿이름` | `문자열` |
| `Img파일 이름` | `문자열` |
| 'IsCCTWave' | `부울` |
| IsSubPixel` | `부울` |

---

## PG 클래스 노드

### 1. PG노드

- **전체 유형**: `FlowEngineLib.Node.PG.PGNode`
- **구성자**: `DeviceNodeConfigurators.cs`
- **구현 파일**: `Node\PG\PGNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `PGCmd` | `PGCommCmdType` |
| `인덱스프레임` | `정수` |

---

## SMU 클래스 노드

### 1. SMUFromCSVNode

- **전체 유형**: `FlowEngineLib.SMUFromCSVNode`
- **구성자**: `DeviceNodeConfigurators.cs`
- **구현 파일**: `SMUFromCSVNode.cs`
- **기본 클래스**: `SMUBaseNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `Csv파일 이름` | 이미지 경로 | 이미지 파일 경로 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| '출처' | `소스 유형` |
| '채널' | `SMU채널 유형` |
| `Csv파일 이름` | `문자열` |
| 'IsAutoRng' | `부울` |
| `SrcRng` | `더블` |
| `LmtRng` | `더블` |

---

### 2. SMUModelNode

- **전체 유형**: `FlowEngineLib.SMUModelNode`
- **구성자**: `DeviceNodeConfigurators.cs`
- **구현 파일**: `SMUModelNode.cs`
- **기본 클래스**: `SMUBaseNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| '모델명' | 템플릿참조 | SMUParam 설정 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| '모델명' | `문자열` |

---

### 3. SMU노드

- **전체 유형**: `FlowEngineLib.SMUnode`
- **구성자**: `DeviceNodeConfigurators.cs`
- **구현 파일**: `SMUnode.cs`
- **기본 클래스**: `SMUBaseNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| '출처' | `소스 유형` |
| '채널' | `SMU채널 유형` |
| `BeginVal` | `플로트` |
| 'EndVal' | `플로트` |
| 'LimitVal' | `플로트` |
| '포인트넘버' | `정수` |
| 'IsAutoRng' | `부울` |
| `SrcRng` | `더블` |
| `LmtRng` | `더블` |

---

## 센서 클래스 노드

### 1. 커먼센서노드

- **전체 유형**: `FlowEngineLib.CommonSensorNode`
- **구성자**: `DeviceNodeConfigurators.cs`
- **구현 파일**: `CommonSensorNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `임시 이름` | 센서템플릿참조 | 센서 템플릿 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |
| `명령 유형` | `CommCmdType` |
| `CmdSend` | `문자열` |
| `CmdReceive` | `문자열` |

---

### 2. TempCommonSensorNode

- **전체 유형**: `FlowEngineLib.TempCommonSensorNode`
- **구성자**: `DeviceNodeConfigurators.cs`
- **구현 파일**: `TempCommonSensorNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |
| `임시 이름` | 센서템플릿참조 | 센서 템플릿 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| `임시 이름` | `문자열` |

---

## FW 클래스 노드

### 1. FW노드

- **전체 유형**: `FlowEngineLib.FWNode`
- **구성자**: `DeviceNodeConfigurators.cs`
- **구현 파일**: `FWNode.cs`
- **기본 클래스**: `CVBaseServerNode`

**구성 패널 속성**(NodeConfigurator)

| 속성 이름 | 유형 | 설명 |
|---------|------|------|
| '디바이스코드' | 장치코드 | 장치 인코딩 |

**클래스 수준 속성**(노드 구현)

| 부동산 이름 | C# 유형 |
|---------|---------|
| '항구' | `정수` |
| '모델 유형' | 'FWModelType' |

---

## 스펙트럼 클래스 노드

### 1. 스펙트럼EQENode

- **전체 유형**: `SpectrumEQENode`
- **구성자**: `SpectrumNodeConfigurators.cs`

---

### 2. SpectrumLoopNode

- **완전한 유형**: `SpectrumLoopNode`
- **구성자**: `SpectrumNodeConfigurators.cs`

---

### 3. 스펙트럼 노드

- **완전한 유형**: `SpectrumNode`
- **구성자**: `SpectrumNodeConfigurators.cs`

---
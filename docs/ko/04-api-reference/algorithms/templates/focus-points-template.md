# FocusPoints 포커스 포인트 템플릿

`FocusPoints/` 는 기존 발광 영역/포커스 포인트 검출 체인의 파라미터 템플릿입니다. 이진화, 블러, 형태학 처리, 면적/사각형 필터, ROI 경계를 저장하고 수동 알고리즘 화면이나 Flow 노드로 전달합니다.

## 빠른 정보

| 항목 | 값 |
| --- | --- |
| 템플릿 클래스 | `TemplateFocusPoints` |
| 파라미터 클래스 | `FocusPointsParam` |
| `TemplateDicId` | `15` |
| Code | `focusPoints` |
| 수동 알고리즘 | `AlgorithmFocusPoints` |
| MQTT 이벤트 | `Event_LightArea_GetData` |
| Flow operator | `FocusPoints` |
| 메뉴 진입점 | `ExportFocusPoints` |

## 파라미터 그룹

| 그룹 | 필드 | 인수인계 의미 |
| --- | --- | --- |
| `Binarize` | `Binarize`, `BinarizeThresh` | 이진화 사용 여부와 임계값 |
| `Blur` | `Blur`, `BlurSize` | 평균 블러 사용 여부와 크기 |
| `Erode` | `Erode`, `ErodeSize` | 침식 사용 여부와 크기 |
| `Dilate` | `Dilate`, `DilateSize` | 팽창 사용 여부와 크기 |
| `Param` | `FilterRect`, `Width`, `Height` | 사각형 필터와 폭/높이 제한 |
| `FilterArea` | `FilterArea`, `MaxArea`, `MinArea` | 면적 필터와 상하한 |
| `Roi` | `Roi`, `Left`, `Right`, `Top`, `Bottom` | ROI 경계 |

여기의 ROI 는 템플릿 입력이며 결과 overlay 좌표가 아닙니다. 결과 점과 POI 재사용은 [ROI 프리미티브](../primitives/roi.md)와 [POI 프리미티브](../primitives/poi.md)를 참고하세요.

## 실행 체인

`DisplayFocusPoints` 는 템플릿, 이미지 소스, 배치 번호를 선택하고 `AlgorithmFocusPoints.SendCommand(...)` 가 `ImgFileName`, `FileType`, `DeviceCode`, `DeviceType`, `TemplateParam` 을 전송합니다. 이벤트 이름은 `MQTTAlgorithmEventEnum.Event_LightArea_GetData` 입니다.

Flow 에서는 `AlgorithmType.发光区检测` 가 `operatorCode = "FocusPoints"` 로 매핑됩니다. 같은 발광 영역 노드는 ROI, AA 찾기, POI 저장 템플릿도 노출할 수 있으므로 `FocusPoints/` 폴더만 보고 전체 기능을 판단하지 마세요.

## 인수인계 주의점

- `TemplateDicId = 15` 와 `Code = "focusPoints"` 가 핵심 식별자입니다.
- 이 템플릿은 전처리 임계값이며 프로젝트 판정 규칙이 아닙니다.
- 수동 실행은 `Event_LightArea_GetData`, Flow 는 `FocusPoints` operator code 를 사용합니다.
- `FocusPoints/` 에는 전용 `ViewHandle*.cs` 가 없으며 결과 표시는 발광 영역, ROI, POI 체인을 추적합니다.

## 관련 페이지

- [FindLightArea 발광 영역 템플릿](./find-light-area.md)
- [POI 템플릿](./poi-template.md)
- [템플릿 및 Flow 체인](../../engine-components/template-flow-chain.md)
- [현재 알고리즘 템플릿 커버리지](../current-algorithm-template-coverage.md)

# ARVR 템플릿

이 페이지는 현재 저장소에 실제로 존재하는 ARVR 템플릿군만 설명합니다. 광학 알고리즘 교재나 범용 파라미터 표가 아니라, 인수인계자가 현재 구현을 따라갈 수 있게 만든 지도입니다.

## 현재 역할

현재 ARVR은 단일 템플릿이 아니라 전통적인 강타입 템플릿, JSON V2 템플릿, POI 템플릿, Flow 노드가 함께 구성하는 실행 면입니다.

- `MTF`
- `SFR`
- `FOV`
- `Distortion`
- `Ghost`

이 구현들은 비슷한 호스트 구조를 쓰지만 파라미터, 결과 표시, POI 의존성은 서로 다릅니다. Flow 안에서는 `SFR_FindROI`, `ARVR.BinocularFusion`, `FindCross` 같은 JSON 분기도 함께 사용됩니다.

## 중요한 파일

- `Engine/ColorVision.Engine/Templates/ARVR/MTF/TemplateMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/ViewHandleMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/AlgorithmSFR.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/WindowSFR.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/AlgorithmFOV.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/DisplayFOV.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/AlgorithmDistortion.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/ViewResultDistortion.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs`
- `Engine/FlowEngineLib/Algorithm/AlgorithmARVRNode.cs`

## 현재 템플릿 매트릭스

| 계열 | 전통 템플릿 | 사전/코드 | 실행 이벤트 | 주요 요청 파라미터 | 결과 진입점 |
| --- | --- | --- | --- | --- | --- |
| `FOV` | `TemplateFOV` | `TemplateDicId = 6`, `Code = FOV` | `Event_FOV_GetData` | `TemplateParam` | `ViewHandleFOV`, `ViewResultAlgType.FOV` |
| `Ghost` | `TemplateGhost` | `TemplateDicId = 7`, `Code = ghost` | `Ghost` | `TemplateParam`, `Color` | `ViewHandleGhost`, `ViewResultAlgType.Ghost` |
| `MTF` | `TemplateMTF` | `TemplateDicId = 8`, `Code = MTF` | `Event_MTF_GetData` | `TemplateParam`, `POITemplateParam` | `ViewHandleMTF`, `ViewResultAlgType.MTF` |
| `SFR` | `TemplateSFR` | `TemplateDicId = 9`, `Code = SFR` | `Event_SFR_GetData` | `TemplateParam`, `POITemplateParam` | `ViewHandleSFR`, `ViewResultAlgType.SFR` |
| `Distortion` | `TemplateDistortionParam` | `TemplateDicId = 10`, `Code = distortion` | `Distortion` | `TemplateParam` | `ViewHandleDistortion`, `ViewResultAlgType.Distortion` |
| `AOI` | `TemplateAOIParam` | `TemplateDicId = 12`, `Code = AOI` | 현재 독립 주 실행 진입점은 아님 | 템플릿 설정 | 주로 ARVR/AOI 파라미터 설정 |

위 이벤트는 수동 알고리즘 클래스에서 보이는 경로입니다. Flow의 `operatorCode`는 JSON 분기도 포함하므로 아래 Flow 표도 함께 봐야 합니다.

## 주요 수동 실행 체인

### MTF

`TemplateMTF`는 `TemplateDicId = 8`, `Code = MTF`인 전통 템플릿입니다. `AlgorithmMTF`는 로컬에서 계산을 끝내는 클래스가 아니라, `TemplateMTF`와 `TemplatePoi`를 선택하고 `TemplateParam`, `POITemplateParam`을 포함한 `Event_MTF_GetData` 요청을 보냅니다.

결과 측에서는 `ViewHandleMTF`가 `ViewResultAlgType.MTF`를 처리하며 CSV 내보내기와 최대값, 최소값, 평균, 분산, 균일도 통계를 담당합니다.

### SFR

`TemplateSFR`는 `TemplateDicId = 9`, `Code = SFR`입니다. `AlgorithmSFR`도 POI를 필요로 하며 `Event_SFR_GetData`를 보냅니다. 결과 표시에서는 `WindowSFR`가 `Pdfrequency`와 `PdomainSamplingData`를 곡선으로 되돌리고 임계값과 주파수 변환을 처리합니다.

### FOV

`TemplateFOV`는 `TemplateDicId = 6`, `Code = FOV`입니다. `AlgorithmFOV`는 `Event_FOV_GetData`를 보냅니다. `DisplayFOV`는 서비스 관리자에서 이미지 소스 장치를 가져오고 배치, Raw 파일, 로컬 이미지 입력을 처리합니다.

### Distortion

`TemplateDistortionParam`은 `TemplateDicId = 10`, `Code = distortion`입니다. `AlgorithmDistortion`은 `Distortion` 이벤트를 보냅니다. `ViewResultDistortion`은 enum과 최종 점 배열을 표시용 모델로 변환하므로 결과 표시 문제를 볼 때 반드시 확인해야 합니다.

### Ghost

`TemplateGhost`는 `TemplateDicId = 7`, `Code = ghost`입니다. `AlgorithmGhost`는 `TemplateParam` 외에 `Color`를 보내고 `Ghost` 이벤트를 발행합니다. 색상 채널은 부가 정보가 아니라 현재 Ghost 체인의 정식 입력입니다.

## Flow 연결

| Flow 연산자 | `operatorCode` | 구성자가 연결하는 템플릿 | 인수인계 핵심 |
| --- | --- | --- | --- |
| `MTF` | `MTF` | `TemplateMTF` + `TemplatePoi` | POI가 없으면 `POITemplateParam`이 빠지고 점 위치 해석이 불완전합니다. |
| `SFR` | `SFR` | `TemplateSFR` + `TemplatePoi` | SFR 곡선은 ROI/POI의 공간 정의에 의존합니다. |
| `FOV` | `FOV` | `TemplateDFOV` + `TemplateFOV` | 같은 슬롯에 JSON V2와 전통 템플릿이 보이므로 실제 선택 원천을 봐야 합니다. |
| `Distortion` | `Distortion` | `TemplateDistortion2` + `TemplateDistortionParam` | JSON V2와 전통 파라미터가 공존합니다. |
| `SFR_FindROI` | `ARVR.SFR.FindROI` | `TemplateSFRFindROI` + `TemplatePoi` | 전통 `TemplateSFR`가 아니라 JSON ROI 검출 체인입니다. |
| `BinocularFusion` | `ARVR.BinocularFusion` | `TemplateBinocularFusion` | JSON 템플릿 경로입니다. |
| `FindCross` | `FindCross` | `TemplateFindCross` + `TemplatePoi` | UI에는 ROI로 보일 수 있지만 선택기는 `TemplatePoi`를 사용합니다. |

`AlgorithmARVRNode.getBaseEventData(...)`는 `BufferLen`, 색상, 이전 단계 이미지 파라미터, SMU 결과도 요청에 넣습니다. 수동 실행은 되는데 Flow 실행이 안 되면 수동 알고리즘 요청과 Flow 생성 요청을 비교해야 합니다.

## 결과 저장 및 표시

| 결과 | 결과 테이블/필드 | 표시 진입점 | 확인 포인트 |
| --- | --- | --- | --- |
| `FOV` | `t_scgd_algorithm_result_detail_fov`, `pattern`, `radio`, `camera_degrees`, `dist`, `threshold`, `degrees` | `ViewHandleFOV` | 이미지 입력, 템플릿, 각도/거리 필드를 함께 봅니다. |
| `Ghost` | `t_scgd_algorithm_result_detail_ghost`, `rows`, `cols`, `radius`, `led_centers`, `ghost_pixels` | `ViewHandleGhost` | 색상 채널과 점 배열 수가 overlay에 영향을 줍니다. |
| `SFR` | `t_scgd_algorithm_result_detail_sfr`, ROI, `gamma`, `pdfrequency`, `pdomain_sampling_data` | `ViewHandleSFR`, `WindowSFR` | 곡선 표시는 샘플링 데이터를 복원한 결과입니다. |
| `Distortion` | `t_scgd_algorithm_result_detail_distortion`, `layout_type`, `slope_type`, `corner_type`, `max_ratio`, `final_points` | `ViewHandleDistortion`, `ViewResultDistortion` | enum 매핑과 최종 점 배열을 함께 확인합니다. |

## 자주 생기는 오해

### ARVR은 통합 schema가 아닙니다

각 하위 디렉터리는 비슷한 호스트를 공유하지만 같은 JSON schema나 같은 파라미터 구조를 공유하지 않습니다.

### 알고리즘 클래스는 주로 호스트와 명령 어댑터입니다

`AlgorithmMTF`, `AlgorithmSFR`, `AlgorithmFOV`, `AlgorithmDistortion`, `AlgorithmGhost`는 화면을 열고 입력을 모아 MQTT 요청을 보내는 역할이 중심입니다.

### POI는 부가 요소가 아닙니다

MTF, SFR, SFR_FindROI는 명시적으로 `TemplatePoi`를 사용합니다. POI를 무시하면 현재 ARVR 실행 체인을 설명할 수 없습니다.

### 전통 템플릿과 JSON V2는 단순한 대체 관계가 아닙니다

FOV, Ghost, Distortion, SFR_FindROI 등은 Flow에서 전통 템플릿과 JSON 템플릿을 동시에 보여줍니다. `operatorCode`, 템플릿 유형, 결과 버전을 확인해야 합니다.

## 인수인계 검증

| 상황 | 필수 확인 |
| --- | --- |
| 수동 MTF/SFR | `TemplateParam`과 `POITemplateParam`이 모두 보내지고 결과가 해당 `ViewHandle*`에 들어갑니다. |
| Flow ARVR 노드 | 연산자를 바꾸면 템플릿 선택기가 바뀌고 `operatorCode`도 일치합니다. |
| FOV/Distortion V2 | 전통 템플릿과 JSON 템플릿이 섞이지 않고 결과 handler도 맞습니다. |
| SFR 곡선 | `WindowSFR`가 곡선을 열고 CSV와 `pdomain_sampling_data`가 대응합니다. |
| Ghost | 요청에 `Color`가 있고 결과 테이블의 점 배열 수와 overlay가 일치합니다. |

## 추천 읽기 순서

1. `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`
2. `Engine/ColorVision.Engine/Templates/ARVR/SFR/AlgorithmSFR.cs`
3. `Engine/ColorVision.Engine/Templates/ARVR/FOV/DisplayFOV.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/ARVR/Distortion/ViewResultDistortion.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs`

## 계속 읽기

- [POI 템플릿](./poi-template.md)
- [JSON 템플릿](./json-templates.md)
- [Flow 엔진](./flow-engine.md)

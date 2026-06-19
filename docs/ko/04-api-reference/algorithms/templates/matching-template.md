# Matching 템플릿 매칭

이 문서는 `Engine/ColorVision.Engine/Templates/Matching/` 의 템플릿, 수동 실행 UI, Flow 노드, AOI 결과 표시를 설명합니다. Matching 은 `MatchTemplate` 을 알고리즘 서비스로 보내고, 반환된 AOI 결과를 네 점 다각형으로 그립니다.

## 적용 범위

| 항목 | 현재 구현 |
| --- | --- |
| 템플릿 클래스 | `TemplateMatch : ITemplate<MatchParam>, IITemplateLoad` |
| 파라미터 클래스 | `MatchParam : ParamModBase` |
| 템플릿 코드 | `MatchTemplate` |
| 사전 ID | `TemplateDicId = 34` |
| 수동 진입점 | `AlgorithmMatching` |
| UI | `DisplayMatching.xaml(.cs)` |
| MQTT 이벤트 | `MQTTAlgorithmEventEnum.Event_MatchTemplate` |
| Flow 노드 | `AlgorithmTMNode` |
| 결과 타입 | `ViewResultAlgType.AOI` |
| 결과 테이블 | `t_scgd_algorithm_result_detail_aoi` |
| 결과 handler | `ViewHandleMatching` |

## 파라미터

| 파라미터 | 기본값 | 설명 |
| --- | --- | --- |
| `MinReducedArea` | `256` | 샘플링 세밀도, 설명 범위 `64 ~ 2048`. |
| `ToleranceAngle` | `0` | 각도 오차, 설명 범위 `0-180`. |
| `Similarity` | `0.7` | 유사도 임계값, 설명 범위 `0-1`. |
| `MaxOverlapRatio` | `0` | 최대 중첩 비율, 설명 범위 `0-0.8`. |
| `TargetNumber` | `70` | 목표 수량. |

`TemplateFile` 은 `MatchParam` 필드가 아니라 `AlgorithmMatching` 과 `AlgorithmTMNode` 의 런타임 파라미터입니다. 인수인계 시 파라미터 템플릿과 템플릿 파일을 분리해서 기록합니다.

## 실행 흐름

수동 UI 에서는 파라미터 템플릿, `TemplateFile`, 로컬 이미지, 서비스 Raw/CIE 또는 배치 번호를 선택한 뒤 `AlgorithmMatching.SendCommand(...)` 를 호출합니다. 요청에는 `ImgFileName`, `FileType`, `DeviceCode`, `DeviceType`, `TemplateFile`, `TemplateParam` 이 들어갑니다.

Flow 는 `AlgorithmTMNode` 를 사용하며 `operatorCode` 는 `MatchTemplate` 입니다. `TMParam` 으로 `TemplateFile` 과 이미지 파라미터를 보냅니다.

현재 XAML 의 템플릿 ComboBox `SelectedIndex` 는 `TemplatePoiSelectedIndex` 에 바인딩되어 있지만, 전송 코드는 `TemplateSelectedIndex` 를 읽습니다. UI 에서 선택한 템플릿이 반영되지 않으면 이 바인딩을 먼저 확인하세요.

## 결과 표시

`ViewHandleMatching` 은 `ViewResultAlgType.AOI` 를 처리합니다. `AlgResultAoiDao.GetAllByPid(result.Id)` 로 상세를 읽고, 원본 이미지를 열고, 네 모서리 좌표로 볼록 껍질을 만든 뒤 파란색 `DVPolygon` 으로 overlay 를 그립니다. 표에는 점수, 각도, 중심점, 네 모서리 좌표가 표시됩니다.

현재 `Load(...)` 는 `result.ViewResults != null` 일 때만 DAO 를 다시 읽습니다. 과거 결과에 AOI 상세가 없으면 호출 측 초기화와 이 조건을 확인하세요.

## 문제 해결

| 현상 | 먼저 확인할 것 |
| --- | --- |
| 서비스가 실행되지 않음 | `DeviceCode`, `DeviceType`, `Event_MatchTemplate`, 서비스 상태. |
| 템플릿 파일 오류 | `TemplateFile` 존재 여부와 서비스 측 접근 가능성. |
| 파라미터가 적용되지 않음 | ComboBox 바인딩, `TemplateSelectedIndex`, `TemplateMatch.Params`. |
| 결과가 비어 있음 | 주 결과 타입 `AOI` 와 상세 테이블 `pid`. |
| overlay 위치 오류 | 네 모서리 좌표, 원본 이미지, 배율, 좌표계. |

## 더 읽기

- [Engine 결과 표시와 프로젝트 인수인계 체인](../../engine-components/result-handoff-chain.md)
- [Engine 템플릿 및 Flow 체인](../../engine-components/template-flow-chain.md)
- [ROI 프리미티브](../primitives/roi.md)
- [현재 알고리즘 템플릿 커버리지](../current-algorithm-template-coverage.md)

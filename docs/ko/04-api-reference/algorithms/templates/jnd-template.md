# JND 템플릿

이 문서는 `Engine/ColorVision.Engine/Templates/JND/`의 업무 체인을 설명합니다. JND 템플릿 자체는 소수의 알고리즘 파라미터만 저장하지만, 실행 시 POI 템플릿도 필요하며 결과는 POI 포인트 단위로 표시되고 내보내집니다.

## 범위

| 항목 | 현재 구현 |
| --- | --- |
| 템플릿 코드 | `OLED.JND.CalVas` |
| 템플릿 클래스 | `TemplateJND : ITemplate<JNDParam>, IITemplateLoad` |
| 파라미터 클래스 | `JNDParam` |
| 의존 템플릿 | `TemplatePoi` |
| 실행 진입점 | `AlgorithmJND`, 표시명 `JND` |
| UI 패널 | `DisplayJND.xaml(.cs)` |
| MQTT 이벤트 | `MQTTAlgorithmEventEnum.Event_OLED_JND_CalVas_GetData` |
| 결과 핸들러 | `ViewHandleJND` |
| 주요 결과 타입 | `Compliance_Math_JND`, `JND_CalVas` |

## 소스 진입점

| 파일 | 인수인계 용도 |
| --- | --- |
| `TemplateJND.cs` | JND 템플릿을 등록하고 `TemplateDicId = 30`, `Code = OLED.JND.CalVas`를 설정한다. |
| `JNDParam.cs` | `CutOff`를 정의한다. |
| `AlgorithmJND.cs` | JND 템플릿과 POI 템플릿을 함께 수집하고 요청을 발행한다. |
| `DisplayJND.xaml.cs` | JND 템플릿, POI 템플릿, 이미지, 장치 출처를 선택한다. |
| `ViewHandleJND.cs` | 결과 로드, 표 표시, POI 포인트 그리기, CSV 및 이미지 저장을 처리한다. |
| `ViewRsultJND.cs` | POI 결과 JSON을 `POIResultDataJND`로 파싱한다. |
| `MysqlJND.cs` | 사전을 복구하며 기본 `CutOff = 0.3`을 제공한다. |

## 실행 체인

1. `TemplateJND`가 `TemplateJND.Params`에 로드된다.
2. `DisplayJND`는 `TemplateJND.Params`와 `TemplatePoi.Params`를 함께 바인딩한다.
3. 사용자가 JND 템플릿, POI 템플릿, 입력 이미지를 선택한다.
4. `AlgorithmJND.SendCommand(...)`는 `ImgFileName`, `FileType`, `DeviceCode`, `DeviceType`, `TemplateParam`, `POITemplateParam`을 보낸다.
5. 명령은 `Event_OLED_JND_CalVas_GetData`로 발행된다.
6. `ViewHandleJND`는 `PoiPointResultDao`에서 포인트를 읽고 `ViewRsultJND`가 `h_jnd` / `v_jnd`를 파싱한다.

## 파라미터와 결과

| 항목 | 설명 |
| --- | --- |
| `CutOff` | 윤곽 컷오프 계수, 기본값 `0.3`. 변경 시 이미지, POI 템플릿, 서비스 버전을 함께 남긴다. |
| `h_jnd` | 가로 방향 JND 결과. |
| `v_jnd` | 세로 방향 JND 결과. |
| POI 포인트 | JND는 `TemplatePoi`를 소비하므로 포인트 변경이 결과에 직접 영향을 준다. |

## 프로젝트 경계

`ProjectShiyuan`은 JND/POI 내보내기와 JND 검증을 사용합니다. "JND CSV 생성"은 PASS와 같지 않습니다. 프로젝트 로직은 `Compliance_Math_JND`, `Validate`, 이미지 복사, pseudo-color 출력을 추가로 확인할 수 있습니다.

관련 문서: [ProjectShiyuan](../../projects/project-shiyuan.md).

## 문제 해결

| 현상 | 먼저 확인할 것 |
| --- | --- |
| POI 관련 오류 | `TemplatePoi.Params`와 `TemplatePoiSelectedIndex`. |
| JND 결과가 비어 있음 | 결과 타입과 `PoiPointResultDao`의 `Pid` 데이터. |
| 포인트는 있으나 JND 값이 없음 | `PoiPointResultModel.Value`가 `POIResultDataJND`로 역직렬화되는지. |
| 프로젝트 OK/NG가 맞지 않음 | 프로젝트 측 JND 2차 검증. |
| 내보내기 경로 이상 | `SideSave(...)`의 `selectedPath` 의미. |

## 인수인계 체크리스트

- `CutOff`를 바꾸면 `JNDParam.cs`, `MysqlJND.cs`, 현장 추천값을 함께 갱신한다.
- POI 선택이나 좌표계를 바꾸면 [POI 템플릿](./poi-template.md)과 프로젝트 문서를 갱신한다.
- 결과 필드를 바꾸면 `ViewRsultJND.cs`, 내보내기 열, 승인 샘플을 갱신한다.
- 프로젝트가 JND 판정에 의존하면 최종 OK/NG 출처를 프로젝트 문서에 명시한다.

## 이어서 읽기

- [POI 템플릿](./poi-template.md)
- [POI 프리미티브](../primitives/poi.md)
- [ProjectShiyuan](../../projects/project-shiyuan.md)
- [결과 인수인계 체인](../../engine-components/result-handoff-chain.md)
- [현재 알고리즘 템플릿 커버리지](../current-algorithm-template-coverage.md)

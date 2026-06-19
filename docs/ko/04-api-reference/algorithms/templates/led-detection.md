# LED 검출 템플릿

이 문서는 현재 저장소의 LED 검출 관련 템플릿 인수인계 경계를 설명합니다. `LEDStripDetection/`과 `LedCheck/` 강타입 템플릿을 중심으로, `Jsons/LEDStripDetectionV2/` 및 `Jsons/LedCheck2/`와의 관계도 정리합니다.

## 네 가지 진입점

| 진입점 | 유형 | 코드/이벤트 | 용도 |
| --- | --- | --- | --- |
| `LEDStripDetection/` | 강타입 템플릿 | `Code = LEDStripDetection`, `Event_LED_StripDetection` | 기존 LED 스트립 위치 검출. |
| `LedCheck/` | 강타입 템플릿 | `Code = FindLED`, `Event_LED_Check_GetData` | LED 점 검출, POI 의존, 원형 표시. |
| `Jsons/LEDStripDetectionV2/` | JSON 템플릿 | `Code = LEDStripDetection`, 이벤트명 `LEDStripDetection`, `Version = 2.0` | 신규 LED 스트립/POI 중심 계산. |
| `Jsons/LedCheck2/` | JSON 템플릿 | `Code = FindLED`, `Event_OLED_FindDotsArrayMem_GetData` | 서브픽셀 OLED 점 배열 검출. |

`Code` 값만으로 구현을 하나로 판단하지 마십시오. `LEDStripDetection`과 `FindLED` 모두 기존 강타입 구현과 신규 JSON 구현이 함께 존재합니다.

## 강타입 LEDStripDetection

| 파일 | 인수인계 용도 |
| --- | --- |
| `TemplateLEDStripDetection.cs` | 템플릿 등록, `TemplateDicId = 21`, `IsUserControl = true`. |
| `LEDStripDetectionParam.cs` | 포인트 수, 거리, 시작 위치, 이진화 비율, 디버그, 저장 경로를 저장한다. |
| `EditLEDStripDetection.xaml(.cs)` | 사용자 정의 파라미터 편집기. |
| `AlgorithmLEDStripDetection.cs` | `Event_LED_StripDetection` 요청을 조립한다. |
| `DisplayLEDStripDetection.xaml(.cs)` | 템플릿, 이미지 출처, 배치/Raw/로컬 파일을 선택하고 실행한다. |

요청에는 `ImgFileName`, `FileType`, `DeviceCode`, `DeviceType`, `TemplateParam`, `IsInversion`이 포함됩니다.

## 강타입 LedCheck

| 파일 | 인수인계 용도 |
| --- | --- |
| `TemplateLedCheck.cs` | LED 점 검출 등록, `Code = FindLED`. |
| `LedCheckParam.cs` | 채널, 고정 반경, 윤곽 면적, 이진화 보정, 그리드 수 등을 저장한다. |
| `AlgorithmLedCheck.cs` | LED 템플릿과 POI 템플릿을 수집하고 `Event_LED_Check_GetData`를 발행한다. |
| `DisplayLedCheck.xaml(.cs)` | LED 템플릿, POI 템플릿, 이미지 출처를 선택한다. |
| `ViewHandleMTF.cs` | POI 결과에서 포인트를 복원하고 원을 그린다. |
| `ViewResultLedCheck.cs` | 포인트와 반경을 저장한다. |

`LedCheck`는 `TemplateParam` 외에 `POITemplateParam`도 보냅니다. UI가 `TemplatePoi.Params.CreateEmpty()`를 사용하므로 현장에서 빈 POI가 허용되는지 확인해야 합니다.

`ViewHandleLedCheck.CanHandle`은 현재 빈 목록입니다. 실행은 성공했지만 결과 표시가 되지 않으면 먼저 결과 타입 등록을 확인하십시오.

## JSON V2 진입점

- `TemplateLEDStripDetectionV2`: `TemplateDicId = 26`, `Name = LedStripDetectionV2`, `debugCfg`, `mathMaskRect`, `nV1`, `threshold`, `dRatio`, `pattern`, `CalcMethod` 등 JSON 파라미터.
- `AlgorithmLEDStripDetectionV2`: 이벤트명 `LEDStripDetection`, `Version = 2.0`, 필요 시 `POITemplateParam` 포함.
- `TemplateLedCheck2`: `TemplateDicId = 18`, `Code = FindLED`.
- `AlgorithmLedCheck2`: `Event_OLED_FindDotsArrayMem_GetData`, `Color`, `FDAType`, 네 개의 `FixedLEDPoint` 전송.

## 어떤 진입점을 쓸 것인가

| 요구 | 권장 진입점 |
| --- | --- |
| 기존 LED 스트립 위치 검출 유지보수 | `LEDStripDetection/` |
| 복잡한 LED 스트립 JSON 파라미터 추가 | `Jsons/LEDStripDetectionV2/` |
| 전통 LED 점 검출과 POI 반경 표시 유지보수 | `LedCheck/` |
| 서브픽셀 OLED 점 배열 검출 | `Jsons/LedCheck2/` |
| 결과 표시 문제 조사 | handler 결과 타입 등록을 먼저 확인한다. |

## 문제 해결

| 현상 | 먼저 확인할 것 |
| --- | --- |
| 스트립 템플릿이 비어 있음 | `TemplateLEDStripDetection.Params`와 `TemplateDicId = 21`. |
| V2 템플릿이 비어 있음 | `TemplateLEDStripDetectionV2`와 `TemplateDicId = 26` JSON 사전. |
| LED 점 검출 실패 | `TemplateParam`, `POITemplateParam`, 이미지 타입, 장치 `Code/Type`. |
| JSON 변경이 적용되지 않음 | V2 JSON 템플릿을 수정했는지, 기존 강타입 템플릿을 수정했는지 확인한다. |
| 결과가 표시되지 않음 | `ViewResultAlgType`과 handler 등록. |
| CSV 내보내기가 이상함 | `ViewHandleLedCheck.SideSave(...)`는 별도 승인 확인이 필요하다. |

## 인수인계 체크리스트

- `Code = LEDStripDetection` 변경은 기존 강타입인지 JSON V2인지 명시한다.
- `Code = FindLED` 변경은 `LedCheck`인지 `LedCheck2`인지 명시한다.
- 강타입 파라미터 변경 시 파라미터 클래스, 기본값, 편집 UI, 현장 샘플을 함께 갱신한다.
- JSON 파라미터 변경 시 schema/설명 JSON, `Mysql*` 복구, 버전 전략을 갱신한다.
- 결과 표시 변경 시 handler, 내보내기, 프로젝트 승인, 스크린샷 샘플을 함께 갱신한다.

## 이어서 읽기

- [JSON 템플릿](./json-templates.md)
- [POI 템플릿](./poi-template.md)
- [결과 인수인계 체인](../../engine-components/result-handoff-chain.md)
- [현재 알고리즘 템플릿 커버리지](../current-algorithm-template-coverage.md)

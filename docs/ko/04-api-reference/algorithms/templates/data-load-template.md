# DataLoad 데이터 로드 템플릿

이 문서는 `Engine/ColorVision.Engine/Templates/DataLoad/` 와 Flow 데이터 로드 노드를 설명합니다. `DataLoad` 는 이미지 알고리즘이 아니며 전용 결과 handler 도 없습니다. 장치, 시리얼 번호, 결과 타입, ZIndex 로 데이터를 찾기 위한 조건을 서비스로 전달합니다.

## 적용 범위

| 항목 | 현재 구현 |
| --- | --- |
| 템플릿 클래스 | `TemplateDataLoad : ITemplate<DataLoadParam>, IITemplateLoad` |
| 파라미터 클래스 | `DataLoadParam : ParamModBase` |
| 템플릿 코드 | `DataLoad` |
| 사전 ID | `TemplateDicId = 22` |
| Flow 노드 | `AlgDataLoadNode`, `AlgDataLoadNode2` |
| Flow 연산 코드 | `operatorCode = "DataLoad"` |
| 설정기 | `AlgDataLoadNodeConfigurator` |
| 결과 handler | 현재 `ViewHandleDataLoad` 없음 |

## 파라미터

| 파라미터 | 타입 | 설명 |
| --- | --- | --- |
| `DeviceCode` | `string?` | 데이터 원본 장치 Code. |
| `ResultType` | `CVCommCore.CVResultType` | 로드할 결과 타입. |
| `SerialNumber` | `string?` | 배치 또는 시리얼 번호. |
| `ZIndex` | `int` | Flow 또는 서비스 측 데이터 계층 인덱스. |

`AlgDataLoadNode` 는 템플릿 기반으로 DataLoad 템플릿을 선택하고 `TemplateParam` 을 전송합니다. `AlgDataLoadNode2` 는 명시 파라미터 기반으로 `DeviceCode`, `SerialNumber`, `ResultType` 문자열, `ZIndex` 를 `DataLoadInput` 으로 보냅니다.

## 경계

DataLoad 를 파일 가져오기 기능으로 설명하지 마세요. 현재 구현은 파일 선택이나 파싱을 하지 않고, 데이터 위치 조건을 알고리즘 서비스 또는 Flow 체인으로 전달합니다.

## 문제 해결

| 현상 | 먼저 확인할 것 |
| --- | --- |
| Flow 가 템플릿을 찾지 못함 | `TemplateDataLoad.Params` 와 `TemplateDicId = 22`. |
| 잘못된 배치 로드 | `SerialNumber` 출처. |
| 잘못된 장치 로드 | `DeviceCode/DataDeviceCode` 와 대상 서비스. |
| 결과 타입 오류 | `CVResultType.ToString()` 이 서비스 기대값인지 확인. |
| 데이터 계층 오류 | `ZIndex/DataZIndex` 와 Flow 노드 순서. |

## 인수인계 체크리스트

- `AlgDataLoadNode` 와 `AlgDataLoadNode2` 중 어느 경로인지 명시합니다.
- 장치 Code, 결과 타입, 시리얼 번호 출처, ZIndex 의미를 기록합니다.
- 서비스 프로토콜 변경 시 `DataLoadData`, `DataLoadData2`, Flow 문서를 갱신합니다.
- 실제 파일 가져오기가 필요하면 별도 파일 파라미터를 추가합니다.

## 더 읽기

- [Engine 템플릿 및 Flow 체인](../../engine-components/template-flow-chain.md)
- [Flow 엔진](./flow-engine.md)
- [현재 알고리즘 템플릿 커버리지](../current-algorithm-template-coverage.md)

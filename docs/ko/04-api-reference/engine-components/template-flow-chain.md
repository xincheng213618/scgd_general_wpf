# Engine 템플릿 및 Flow 체인

이 페이지는 템플릿이 어떻게 편집 가능하고, 저장 가능하고, 실행 가능한 Flow 가 되는지 설명합니다.

## 메인 체인

```text
TemplateControl / TemplateModel<T>
  -> TemplateFlow
  -> FlowEngineControl / FlowControl
  -> NodeConfiguratorRegistry
  -> Flow node execution
  -> FlowCompleted
  -> batch / result / Projects
```

## 템플릿 타입

| 타입 | 일반 위치 | 인수인계 포인트 |
| --- | --- | --- |
| JSON 템플릿 | `Templates/Jsons/` | 파라미터, 기본값, 호환성 |
| POI / 알고리즘 템플릿 | `Templates/POI/`, `Templates/ARVR/` | 결과 key, 표시 모델, DAO |
| Flow 템플릿 | `Templates/Flow/` | 노드, 연결, `.cvflow` 저장/가져오기 |
| 디바이스 액션 템플릿 | `Services/Devices/**` 또는 템플릿 디렉터리 | 디바이스 명령, MQTT, 타임아웃 |

## NodeConfigurator 의 역할

`NodeConfiguratorRegistry` 는 노드가 UI 에서 무엇을 설정할 수 있는지 결정합니다. 실행 노드만 추가하고 설정기를 추가하지 않으면, 노드는 있어도 템플릿이나 디바이스를 올바르게 선택할 수 없습니다.

확인 항목:

- 노드 타입이 등록되어 있다.
- 선택 가능한 템플릿 타입이 올바르다.
- 선택 가능한 디바이스 타입이 올바르다.
- 저장 후 파라미터를 다시 로드할 수 있다.
- 기존 `.cvflow` 가져오기와 호환된다.

## 자주 쓰는 템플릿 연결점

| 템플릿/진입점 | Flow 인수인계 지점 |
| --- | --- |
| [FocusPoints 포커스 포인트 템플릿](../algorithms/templates/focus-points-template.md) | `AlgorithmNode` 발광 영역 검출이 `operatorCode = "FocusPoints"` 로 매핑됩니다. |
| [ImageCropping 이미지 크롭 템플릿](../algorithms/templates/image-cropping-template.md) | `AlgorithmType.图像裁剪` 과 `OLEDImageCroppingNode` 가 `TemplateImageCropping` 을 바인딩합니다. |
| [템플릿 메뉴 진입점](../algorithms/templates/template-menu-entries.md) | 메뉴는 편집 창을 열 뿐이며 Flow 노드에서 선택 가능한지는 `NodeConfigurator` 가 결정합니다. |

## Flow 실행 수락 확인

1. Flow 를 새로 만들고 노드를 추가한 뒤 저장합니다.
2. 닫았다가 다시 열어 노드와 파라미터를 확인합니다.
3. 기존 `.cvflow` 를 가져옵니다.
4. 실행하고 시작/종료 노드 상태를 확인합니다.
5. `FlowCompleted` 를 확인합니다.
6. 배치, 결과 테이블, 프로젝트가 결과를 읽을 수 있는지 확인합니다.

## 자주 나는 문제

| 현상 | 가능 원인 |
| --- | --- |
| 저장 후 노드 파라미터가 사라짐 | 모델 필드 미직렬화, 속성명 변경 |
| 노드는 실행되지만 결과가 없음 | 결과 key 불일치, DAO 미저장 |
| Flow 가져오기 실패 | 노드 타입명, 템플릿 ID, 버전 호환성 |
| 디바이스 노드 선택 불가 | `NodeConfigurator` 필터, 서비스 미생성 |

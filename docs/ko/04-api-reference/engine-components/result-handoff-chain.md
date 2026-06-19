# Engine 결과 표시 및 프로젝트 인수인계 체인

이 페이지는 Engine 이 알고리즘 결과를 표시하는 책임과 Projects 가 결과를 고객용 판정/납품 형식으로 바꾸는 책임을 나누어 설명합니다.

## 메인 체인

```text
AlgResultMasterModel / detail DAO
  -> ViewResultAlg / IViewResult
  -> ViewHandleXxx / IResultHandleBase / IDisplayAlgorithm
  -> UI/ColorVision.ImageEditor/Draw overlay
  -> Projects/<Project>/Process / Recipe / Fix
  -> ObjectiveTestResult / export / Socket / MES
```

## Engine 표시 책임

Engine 은 결과를 추적 가능하고 조회 가능하고 시각화 가능하게 만들어야 합니다. 주 결과, 상세 결과, 이미지 경로, ROI 또는 좌표, 결과 타입, 표시 핸들러가 필요합니다.

주요 위치:

- `Templates/**/ViewHandle*.cs`
- `Abstractions/IResultHandlers.cs`
- `ViewResultAlg`
- `AlgResultMasterModel`
- `UI/ColorVision.ImageEditor/Draw/**`

관련 주제:

- [Compliance 결과 인수인계](../algorithms/templates/compliance-results.md): `ViewHandleComplianceY/XYZ/JND` 와 Y/XYZ/JND 판정 결과.
- [Validate 판정 규칙 템플릿](../algorithms/templates/validate-rules.md): 판정 규칙 출처와 `ValidateResult` 해석.
- [BuzProduct 제품 업무 파라미터 템플릿](../algorithms/templates/buz-product-template.md): 제품 상세가 `val_rule_temp_id` 로 규칙을 참조하는 흐름.
- [Matching 템플릿 매칭](../algorithms/templates/matching-template.md): `ViewHandleMatching`, AOI detail, 네 점 overlay.
- [ImageCropping 이미지 크롭 템플릿](../algorithms/templates/image-cropping-template.md): `ViewHandleImageCropping` 과 크롭 파일 detail.

## Projects 납품 책임

프로젝트는 Engine 결과를 읽고 고객 규칙을 적용합니다.

- Recipe 파라미터.
- Fix 또는 보정 규칙.
- 결과 필드 매핑.
- `ObjectiveTestResult`.
- CSV, DB, Socket, MES 출력.

고객 판정을 이미지 오버레이에 쓰지 말고, Engine 이 만들어야 할 결과를 프로젝트에서 임시로 만들지 않습니다.

## 결과 누락 점검

| 현상 | 확인 순서 |
| --- | --- |
| UI 에 오버레이 없음 | DAO -> `ViewResultAlg` -> `CanHandle` -> 이미지 경로 -> Draw 객체 |
| 오버레이 위치가 틀림 | 좌표계, ROI, 배율, 원본 이미지 크기 |
| 프로젝트 결과가 비어 있음 | Engine 결과 key, `Process` 읽기, Recipe/Fix |
| Socket 응답이 틀림 | `ObjectiveTestResult`, 프로토콜 필드, 오류 코드 |
| 출력 필드 누락 | exporter, 필드 매핑, 배치 번호, 결과 ID |

## 새 결과 타입 추가

1. 결과 모델과 DAO 를 정의합니다.
2. 알고리즘 실행 후 주 결과와 상세 결과를 저장합니다.
3. `IViewResult` 또는 표시 모델을 추가합니다.
4. `ViewHandleXxx` 를 추가하고 `CanHandle` 을 구현합니다.
5. ImageEditor Draw 오버레이를 추가합니다.
6. 필요하면 `Projects/<Project>/Process` 와 `ObjectiveTestResult` 매핑을 추가합니다.
7. 이 페이지와 해당 프로젝트 문서를 업데이트합니다.

## 더 읽기

- [Compliance 결과 인수인계](../algorithms/templates/compliance-results.md)
- [Validate 판정 규칙 템플릿](../algorithms/templates/validate-rules.md)
- [BuzProduct 제품 업무 파라미터 템플릿](../algorithms/templates/buz-product-template.md)

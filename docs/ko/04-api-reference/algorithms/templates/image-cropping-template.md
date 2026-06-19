# ImageCropping 이미지 크롭 템플릿

`ImageCropping/` 는 기존 강타입 이미지 크롭 템플릿, 수동 알고리즘 화면, Flow 노드, 크롭 결과 표시를 담당합니다. `Jsons/ImageROI` 와는 다른 모듈입니다.

## 빠른 정보

| 항목 | 값 |
| --- | --- |
| 템플릿 클래스 | `TemplateImageCropping` |
| 파라미터 클래스 | `ImageCroppingParam` |
| `TemplateDicId` | `32` |
| Code | `ImageCropping` |
| 수동 알고리즘 | `AlgorithmImageCropping` |
| MQTT 이벤트 | `Event_Image_Cropping` |
| Flow operator | `OLED.GetRIAand` |
| 결과 타입 | `ViewResultAlgType.Image_Cropping` |
| 결과 handler | `ViewHandleImageCropping` |

## 파라미터와 ROI

`ImageCroppingParam` 에는 현재 두 개의 저장 필드만 있습니다.

| 필드 | 의미 |
| --- | --- |
| `UnEgde` | edge 관련 크롭 파라미터. 철자는 소스와 동일하게 유지합니다 |
| `O_Index` | 출력 순서/인덱스. 복구 SQL 기본값은 `[0,1,2,3]` 입니다 |

`Point1` 부터 `Point4` 는 `AlgorithmImageCropping` 의 런타임 ROI 점입니다. 수동 실행 시 `ROI` 배열로 전송되며 템플릿 필드로 저장되지 않습니다.

## Flow 와 결과

수동 실행은 `ImgFileName`, `FileType`, `DeviceCode`, `DeviceType`, `TemplateParam`, `ROI` 를 `Event_Image_Cropping` 으로 보냅니다.

Flow 에는 두 경로가 있습니다.

- 일반 `AlgorithmNode`: `AlgorithmType.图像裁剪` 이 `operatorCode = "OLED.GetRIAand"` 로 매핑됩니다.
- `OLEDImageCroppingNode`: `图像裁剪2` 는 `IN_IMG`, `IN_ROI` 를 가지고 상위 ROI master id 를 `ROI_MasterId` 에 넣습니다.

`ViewHandleImageCropping` 은 `ViewResultAlgType.Image_Cropping` 을 처리하고 `AlgResultImageDao` 에서 detail 을 읽어 `file_name`, `order_index`, `FileInfo` 를 표시합니다.

## 인수인계 주의점

- 이 문서는 강타입 `ImageCropping` 을 설명하며 JSON `ImageROI` 가 아닙니다.
- 수동 네 점 ROI 는 런타임 입력이고 템플릿 저장 필드가 아닙니다.
- Flow 의 두 입력 노드는 상위 ROI 결과에 의존합니다.
- `SideSave(...)` 는 `selectedPath` 를 CSV 경로와 이미지 디렉터리처럼 함께 사용하므로 export 는 현장에서 검증해야 합니다.

## 관련 페이지

- [결과 인수인계 체인](../../engine-components/result-handoff-chain.md)
- [템플릿 및 Flow 체인](../../engine-components/template-flow-chain.md)
- [ROI 프리미티브](../primitives/roi.md)
- [JSON 템플릿](./json-templates.md)

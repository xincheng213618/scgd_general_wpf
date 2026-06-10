# ProjectARVR

`Projects/ProjectARVR/`는 초기 AR/VR 광학 검사 패키지이며 런타임에는 `ProjectARVR.dll`로 로드됩니다. 고정 PG 전환, FlowEngine 실행, `ObjectiveTestResult`, CSV, Socket 응답을 하나의 흐름으로 묶습니다.

## 런타임 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectARVR` |
| `version` | `1.0` |
| `dllpath` | `ProjectARVR.dll` |
| `requires` | `1.3.9.10` |

## 업무 범위

현재 자동화 순서는 고정이며 사실상 `OpticCenter`에서 완료됩니다.

```text
White2 -> White -> White1 -> Black -> Chessboard -> MTFH -> MTFV -> Distortion -> OpticCenter -> ProjectARVRResult
```

`Ghost`, `DotMatrix` 등의 enum은 있지만 현재 `SwitchPGCompleted()` 체인에서 템플릿 실행이 없습니다.

## 주요 코드

| 파일 | 역할 |
| --- | --- |
| `ARVRWindow.xaml(.cs)` | 메인 창, 이미지 전환 상태기계, Flow, 결과, Socket |
| `ProjectARVRConfig.cs` | 설정과 템플릿 편집 |
| `ObjectiveTestResult.cs` | 제품 결과 DTO와 CSV |
| `ARVRRecipeConfig.cs` | 각 검사 항목 제한 |
| `ObjectiveTestResultFix.cs` | 보정 계수 |
| `ViewResultManager.cs` | 결과 목록, 저장, CSV path |
| `Services/SocketControl.cs` | Socket event |

## 인수인계 주의

- `ProjectARVRInit`은 창이 먼저 열려 있어야 합니다.
- 템플릿 매칭은 `White255`, `MTF_H`, `OpticCenter` 같은 keyword에 의존합니다.
- 뒤쪽 enum을 구현 완료 자동화로 설명하지 마세요.
- 신규 AR/VR 납품은 ProjectARVRPro 또는 ProjectARVRLite를 먼저 검토합니다.

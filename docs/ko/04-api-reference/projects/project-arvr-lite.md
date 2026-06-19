# ProjectARVRLite

`Projects/ProjectARVRLite/`는 경량 AR/VR 빠른 검사 패키지이며 `ProjectARVRLite.dll`로 로드됩니다. 설정 가능한 검사 항목, 전처리, 단순 납품을 중점으로 합니다.

## 런타임 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectARVRLite` |
| `version` | `1.0` |
| `dllpath` | `ProjectARVRLite.dll` |
| `requires` | `1.3.15.6` |

## 업무 범위

`ProjectARVRLiteTestTypeConfig.json`이 enabled test를 결정합니다. 현재 구현된 분기:

```text
W51, White, W25, Chessboard, MTFHV, Distortion, Ghost, OpticCenter
```

`DotMatrix`, white-screen defect, black-screen defect는 설정 값이 있지만 자동화 분기가 완성되어 있지 않습니다.

## 주요 코드

| 파일 | 역할 |
| --- | --- |
| `ARVRWindow.xaml(.cs)` | 메인 창, 상태기계, 전처리, 결과 |
| `TestTypeConfig.cs` | enabled test 설정 |
| `ObjectiveTestResult.cs` | 제품 결과와 CSV |
| `ARVRRecipeConfig.cs` | 각 검사 항목 제한 |
| `Services/SocketControl.cs` | Socket event |

## 인수인계 주의

- `%AppData%\ColorVision\Config\ProjectARVRLiteTestTypeConfig.json`을 먼저 확인합니다.
- 전처리 실패는 Flow 시작 전에 중단됩니다.
- CSV는 `ViewResultManager.Config.IsSaveCsv`와 날짜/SN 설정에 의존합니다.
- 템플릿 이름은 `White51`, `White255_Ghost_Test`, `MTF_HV` 같은 keyword와 맞아야 합니다.

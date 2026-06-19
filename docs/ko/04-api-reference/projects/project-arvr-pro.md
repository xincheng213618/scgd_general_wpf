# ProjectARVRPro

`Projects/ProjectARVRPro/`는 현재 주요 AR/VR 프로젝트 패키지이며 `ProjectARVRPro.dll`로 로드됩니다. 최신 AR/VR 고객 워크플로우를 인수인계할 때 가장 먼저 읽을 폴더입니다.

## 런타임 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectARVRPro` |
| `version` | `1.1.7.7` |
| `dllpath` | `ProjectARVRPro.dll` |
| `requires` | `1.3.15.15` |

## 업무 범위

ARVRPro는 휘도, 균일도, 색, FOFO, Chessboard, MTF, Distortion, OpticCenter, OLED AOI를 다룹니다. 중심 모델은 `ProcessGroup`과 `ProcessMeta`입니다.

ARVRPro는 JSON `EventName` dispatch를 사용하고, `ProjectARVRInit` -> `SwitchPGCompleted` -> `ProjectARVRResult` 흐름으로 이미지 전환과 검사를 진행합니다. 각 step은 `PictureSwitchConfig`를 가질 수 있습니다.

## 주요 코드

| 파일/디렉터리 | 역할 |
| --- | --- |
| `ARVRWindow.xaml(.cs)` | 메인 검사 창 |
| `Process/` | 검사 step framework와 구현 |
| `Recipe/` | 제한과 보정 설정 |
| `Services/SocketControl.cs` | TCP JSON event dispatch |
| `Services/RunAllSocket.cs` | RunAll |
| `Services/SwitchGroupSocket.cs` | ProcessGroup 전환 |
| `SocketRelay/` | AOI relay |
| `ObjectiveTestResult.cs` | 집계 결과 |
| `ViewResultManager.cs` | 결과 조회와 저장 |

## 인수인계 주의

- `ProcessGroup`은 제품/시나리오 단위 워크플로우입니다.
- `ProcessMeta`는 FlowTemplate, enabled state, image switching, private config를 가집니다.
- 고객 판정은 generic Engine이 아니라 프로젝트 `IProcess`에 둡니다.
- `UseLegacyARVROutput`은 CSV와 Socket `Data` 모두에 영향을 줍니다.
- `SocketRelay/`는 보통 `127.0.0.1:9200`입니다. main Socket 연결만으로 relay 정상 동작을 보장할 수 없습니다.

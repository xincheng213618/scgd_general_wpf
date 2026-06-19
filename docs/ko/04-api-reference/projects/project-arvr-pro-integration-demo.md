# ProjectARVRPro.IntegrationDemo

`Projects/ProjectARVRPro.IntegrationDemo/`는 고객, MES, PLC, 상위 제어기가 ARVRPro TCP/JSON 프로토콜을 검증하기 위한 최소 샘플입니다. ColorVision 플러그인이 아니며 내부 알고리즘 DLL에 의존하지 않도록 유지해야 합니다.

## 위치

| 항목 | 값 |
| --- | --- |
| Target framework | .NET Framework 4.8 |
| 형태 | WPF demo + CLI arguments |
| ColorVision dependency | 없음 |
| 목적 | TCP 연결, command, result parse, CSV export 예제 |

## 기능

- ARVRPro TCP port, 보통 `6666`에 연결합니다.
- `ProjectARVRInit`, `SwitchPGCompleted`, `RunAll`, `AOITestSwitchImageComplete`를 전송합니다.
- sample JSON 또는 현장 저장 `ProjectARVRResult`를 읽습니다.
- `ObjectiveTestResult`와 flat item table을 표시합니다.
- raw JSON 저장과 CSV export를 수행합니다.
- partial/sticky packet 읽기를 보여줍니다.

## 인수인계 주의

- 고객 측 샘플로 유지하고 내부 업무 로직을 넣지 않습니다.
- 공개 필드 변경 시 `Contracts/`, sample JSON, README, 이 페이지를 함께 업데이트합니다.

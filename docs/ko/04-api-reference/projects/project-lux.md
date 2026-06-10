# ProjectLUX

`Projects/ProjectLUX/`는 광학 자동화 패키지이며 `ProjectLUX.dll`로 로드됩니다. 휘도, 색, contrast, MTF, distortion, optic center, VID, luminous flux를 다룹니다.

## 런타임 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectLUX` |
| `version` | `1.0` |
| `dllpath` | `ProjectLUX.dll` |
| `requires` | `1.3.15.10` |

## 업무 범위

LUX는 ARVRPro와 달리 text command를 사용합니다.

```text
T00XX,SN;
```

`XX`는 active ProcessGroup의 `ProcessMeta.SocketCode`와 대응합니다. 따라서 인수인계 시 FlowTemplate, active group, SocketCode, Recipe, Fix, return code를 함께 확인합니다.

## 주요 코드

| 파일/디렉터리 | 역할 |
| --- | --- |
| `LUXWindow.xaml(.cs)` | 메인 검사 창 |
| `Process/` | test framework와 items |
| `Recipe/` | limit config |
| `Fix/` | correction factor |
| `Services/SocketControl.cs` | TCP text command |
| `ObjectiveTestResult.cs` | aggregated result |
| `ViewResultManager.cs` | SQLite result |

## 인수인계 주의

- Socket은 text-based이며 ARVRPro JSON이 아닙니다.
- FlowTemplate rename 시 `SocketCode`를 확인합니다.
- `FixConfig`는 최종 값에 영향을 주므로, calibration 문제를 바로 algorithm 문제로 보지 마세요.
- `%APPDATA%\ColorVision\Config\ProcessGroups.json` 저장을 확인합니다.

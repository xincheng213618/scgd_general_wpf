# ProjectBlackMura

`Projects/ProjectBlackMura/`는 디스플레이 패널 Black Mura 검사 패키지이며 `ProjectBlackMura.dll`로 로드됩니다.

## 런타임 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectBlackMura` |
| `version` | `1.0` |
| `dllpath` | `ProjectBlackMura.dll` |
| `requires` | `1.3.15.10` |

## 업무 범위

PG power, PG picture switch, 5색 Flow, Engine result parse, POI overlay, Excel report, MES/Serial state를 묶은 현장 워크플로우입니다.

```text
None -> White -> Black -> Red -> Green -> Blue
```

## 주요 코드

| 파일 | 역할 |
| --- | --- |
| `MainWindow.xaml(.cs)` | 메인 창과 플로우 제어 |
| `ProjectBlackMuraConfig.cs` | 설정 |
| `PluginConfig/BlackMuraProject.cs` | launcher |
| `PluginConfig/BlackMuraMenu.cs` | menu |
| `ExcelReportGenerator.cs` | Excel report |
| `HYMesManager.cs` | MES와 PG Serial |

## 인수인계 주의

- 중단 시 `CCPICompleted`, STX/ETX frame, `HYMesConfig.DeviceId`를 먼저 확인합니다.
- Excel path는 EPPlus에 의존합니다.
- PG/MES는 고객 현장 경계이며 Engine으로 이동하지 않습니다.
- template 이름 변경 시 main window의 keyword matching을 업데이트합니다.

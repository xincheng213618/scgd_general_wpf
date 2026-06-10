# ProjectKB

`Projects/ProjectKB/`는 키보드 백라이트 검사 패키지이며 `ProjectKB.dll`로 로드됩니다. FlowEngine, KB template, POI luminance, Recipe, backlight autotune, PLC/Modbus, MES DLL, CSV/summary를 조합합니다.

## 런타임 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectKB` |
| `version` | `1.0` |
| `dllpath` | `ProjectKB.dll` |
| `requires` | `1.3.15.10` |

## Entry modes

| Entry | 내용 |
| --- | --- |
| Manual | operator가 SN과 FlowTemplate을 선택해 실행 |
| Modbus | PLC가 holding register에 `1`을 쓰고 완료 시 `0`을 writeback |
| MES | `FunTestDll.dll`의 `CheckWIP`, `Collect_test` 사용 |

## 주요 코드

| 파일 | 역할 |
| --- | --- |
| `ProjectKBWindow.xaml(.cs)` | Flow, 결과, CSV/MES/Modbus |
| `KBRecipeConfig.cs` | luminance, uniformity, local contrast, autotune limits |
| `BacklightAutotuneService.cs` | Q1/Q3와 sigmoid 보정 |
| `KBItemMaster.cs` | master result |
| `Modbus/ModbusControl.cs` | Modbus TCP |
| `MesDll.cs` | `FunTestDll.dll` P/Invoke |

## 인수인계 주의

- `FunTestDll.dll`과 `FunTestDllConfig.INI`는 납품 검증에 필수입니다.
- `CheckWIP` 반환 규칙은 고객 DLL 버전에 의존합니다.
- `KBLVSacle`은 calibration과 과거 결과 해석에 영향을 줍니다.
- POI 이름과 크기는 KB template와 일치해야 합니다.
- Modbus, Socket, MES는 별도 경로입니다. 현장에서 쓰는 경로를 먼저 확인합니다.

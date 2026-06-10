# ProjectHeyuan

`Projects/ProjectHeyuan/`는 Heyuan 고객용 패키지이며 `ProjectHeyuan.dll`로 로드됩니다.

## 런타임 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectHeyuan` |
| `version` | `1.0` |
| `dllpath` | `ProjectHeyuan.dll` |
| `requires` | `1.3.15.10` |

## 업무 범위

4점 색/휘도 검사와 고객 Serial pass-through를 담당합니다. 고정 순서:

```text
White, Blue, Red, Orange
```

값은 `TempResult`에 집계되어 PASS/FAIL, CSV, MES upload에 사용됩니다.

## 주요 코드

| 파일 | 역할 |
| --- | --- |
| `ProjectHeyuanWindow.xaml(.cs)` | 메인 창 |
| `MenuItemHeyuan.cs` | launcher/menu |
| `HYMesManager.cs` | MES/Serial |
| `SerialMsg.cs` | message model |
| `TempResult.cs` | 4점 결과 |
| `NumSet.cs` | limit |

## 인수인계 주의

- Serial message는 `0x02 + ASCII + 0x03`입니다.
- `CSN`, `CMI`, `CGI`, `CPT`의 return code는 현장 프로토콜 경계입니다.
- Flow 출력이 4 POI보다 적으면 업무 데이터 오류입니다.
- 색 순서, `TestName`, field format 변경 시 CSV와 protocol을 함께 업데이트합니다.

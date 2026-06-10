# ProjectShiyuan

`Projects/ProjectShiyuan/`는 Shiyuan 고객용 패키지이며 `ProjectShiyuan.dll`로 로드됩니다.

## 런타임 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectShiyuan` |
| `name` | `视源项目` |
| `version` | `1.0` |
| `dllpath` | `ProjectShiyuan.dll` |
| `requires` | `1.3.15.10` |

## 업무 범위

현재는 FlowEngine template 실행, JND/POI result extraction, 고객 data directory 출력, pseudo-color image 저장이 중심입니다. Heyuan이나 BlackMura처럼 완전한 Serial/MES upload chain이 아니라, `Flow 실행 -> result summary -> customer files` 형태입니다.

## 주요 코드

| 파일 | 역할 |
| --- | --- |
| `ShiyuanProjectWindow.xaml(.cs)` | 메인 창 |
| `ShiyuanProjectExport.cs` | launcher/menu |
| `ProjectShiYuanConfig.cs` | config |
| `TempResult.cs`, `NumSet.cs` | temporary result and range |
| `SerialMsg.cs` | retained serial model |

## 인수인계 주의

- `UploadSN` handler는 현재 비어 있으므로 자동 SN upload 구현 완료로 쓰지 않습니다.
- `SerialMsg.cs`는 구조 보존이며 완전한 MES chain이 아닙니다.
- `C:\Windows\System32\pic\`는 현장 의존 path입니다.
- `DataPath` 변경 시 JND CSV, POI CSV, image copy, pseudo-color 설명을 함께 업데이트합니다.

# 當前外掛文件覆蓋清單

本頁用來確認目前 `Plugins/` 目錄裡的每個真實外掛，是否都有對應能力頁、交接頁、發版檢查點和運行時說明文件。

## 當前覆蓋結論

| 外掛目錄 | 工程文件 | manifest Id / version | 當前能力頁 | 交接與驗收覆蓋 |
| --- | --- | --- | --- | --- |
| `Plugins/Conoscope/` | `Conoscope.csproj` | `Conoscope` / `1.4.6.1` | [Conoscope](./standard-plugins/conoscope.md) | [矩陣](./plugin-capability-matrix.md)、[場景手冊](./plugin-handoff-playbook.md)、[現場驗收](./plugin-field-acceptance.md) |
| `Plugins/EventVWR/` | `EventVWR.csproj` | `EventVWR` / `1.0` | [EventVWR](./standard-plugins/eventvwr.md) | [矩陣](./plugin-capability-matrix.md)、[場景手冊](./plugin-handoff-playbook.md)、[現場驗收](./plugin-field-acceptance.md) |
| `Plugins/Spectrum/` | `Spectrum.csproj` | `Spectrum` / `1.0` | [Spectrum](./standard-plugins/spectrum.md) | [矩陣](./plugin-capability-matrix.md)、[場景手冊](./plugin-handoff-playbook.md)、[現場驗收](./plugin-field-acceptance.md) |
| `Plugins/SystemMonitor/` | `SystemMonitor.csproj` | `SystemMonitor` / `1.0.1` | [SystemMonitor](./standard-plugins/system-monitor.md) | [矩陣](./plugin-capability-matrix.md)、[場景手冊](./plugin-handoff-playbook.md)、[現場驗收](./plugin-field-acceptance.md) |
| `Plugins/WindowsServicePlugin/` | `WindowsServicePlugin.csproj` | `WindowsServicePlugin` / `1.0` | [WindowsServicePlugin](./standard-plugins/windows-service.md) | [矩陣](./plugin-capability-matrix.md)、[場景手冊](./plugin-handoff-playbook.md)、[現場驗收](./plugin-field-acceptance.md) |

## 目前工作樹核查證據

2026-06-10 核查目前工作樹時，5 個外掛目錄全部具備 `.csproj`、`manifest.json`、運行時 `README.md`、運行時 `CHANGELOG.md` 和 docs 單外掛頁。

| 外掛目錄 | `.csproj` | `manifest.json` | 運行時 README | 運行時 CHANGELOG | 結論 |
| --- | --- | --- | --- | --- | --- |
| `Plugins/Conoscope/` | 有 | `Conoscope` / `1.4.6.1` | 有 | 有 | 覆蓋完整 |
| `Plugins/EventVWR/` | 有 | `EventVWR` / `1.0` | 有 | 有 | 覆蓋完整 |
| `Plugins/Spectrum/` | 有 | `Spectrum` / `1.0` | 有 | 有 | 覆蓋完整 |
| `Plugins/SystemMonitor/` | 有 | `SystemMonitor` / `1.0.1` | 有 | 有 | 覆蓋完整 |
| `Plugins/WindowsServicePlugin/` | 有 | `WindowsServicePlugin` / `1.0` | 有 | 有 | 覆蓋完整 |

運行時 README/CHANGELOG 和 docs 站點頁要同步維護：前者會進入外掛包和現場目錄，後者負責交接人員理解能力、邊界、風險和驗收方式。

## 外部邊界覆蓋

| 外掛 | 必須說明的邊界 |
| --- | --- |
| Conoscope | MVS 相機、`MvCameraControl.dll`、圖像資源、關注點、CSV 匯出 |
| Spectrum | 光譜儀 native DLL、串口、SMU/Shutter/CFW、授權、SQLite 結果庫、Socket JSON 指令 |
| SystemMonitor | Windows 效能計數器、CUDA 資訊、磁碟/網路/程序、快取目錄權限 |
| EventVWR | Windows EventLog、WER LocalDumps、HKLM 登錄檔、管理員權限 |
| WindowsServicePlugin | Windows 服務、MySQL、MQTT、服務 ZIP、配置同步、管理員權限 |

## 不在當前清單中的名稱

Pattern、ImageProjector、ScreenRecorder 目前不是當前外掛。恢復前必須先補 `Plugins/<Name>/`、`.csproj`、`manifest.json`、README、CHANGELOG、構建複製、打包驗證和 docs 導航。

## 覆蓋率檢查

```powershell
Get-ChildItem Plugins -Directory | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem docs/zh-tw/04-api-reference/plugins/standard-plugins -File | Sort-Object Name | Select-Object -ExpandProperty Name
```

檢查結果應只包含目前五個真實外掛頁；歷史外掛只能出現在恢復檢查語境中，不能作為當前能力入口。

# 現有外掛現場驗收與交接清單

本頁只覆蓋目前 `Plugins/` 目錄裡真實存在的外掛：Conoscope、Spectrum、SystemMonitor、EventVWR、WindowsServicePlugin。

## 驗收總表

| 外掛 | 最小煙測 | 需要記錄 |
| --- | --- | --- |
| Conoscope | 打開 Tool -> `VAM`，載入圖像，新增/移動關注點，執行色域或對比度分析，匯出 CSV | 圖像、關注點、分析結果、CSV、MVS 依賴 |
| Spectrum | 打開 Spectrum，檢查狀態列；有設備時連接、校零、測量、匯出；Socket 發 `SpectrumStatus` | SN、標定組、SQLite 結果、授權、native DLL |
| SystemMonitor | 打開效能監控，切換狀態列開關，刷新磁碟/網路/程序 | 配置開關、性能計數器、失敗降級 |
| EventVWR | 管理員模式打開事件窗口，查看 Application 錯誤，保存當前程序 Dump | DumpFolder、DumpType、登錄檔回退 |
| WindowsServicePlugin | 管理員模式打開服務管理器，刷新服務狀態，檢查配置文件，在測試環境執行安裝流程 | BaseLocation、服務包、MySQL/MQTT、CFG 同步、回退包 |

## 發版記錄

每次外掛發版或現場替換至少記錄：

- `manifest.version`
- `.csproj VersionPrefix`
- 輸出 DLL `FileVersion`
- `.cvxp` 文件名
- 必帶文件
- 主程式 `ColorVision.*.dll` 相容性
- 外部設備、授權、資料庫、登錄檔和管理員權限
- 回退包和已知限制

## 歷史名稱

Pattern、ImageProjector、ScreenRecorder 不在當前驗收範圍。恢復為當前外掛前，先更新 [當前外掛文件覆蓋清單](./current-plugin-coverage.md)。

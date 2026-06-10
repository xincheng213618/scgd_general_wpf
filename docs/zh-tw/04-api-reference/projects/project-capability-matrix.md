# 專案包能力與交接矩陣

本頁橫向比較所有 `Projects/` 專案包：解決什麼現場問題、如何被外部系統觸發、哪段程式碼負責流程、交付時要驗哪些輸出。發版、替換或回退證據見 [專案包發布證據與版本核查表](./project-release-evidence.md)。

## 專案摘要

| 專案 | 業務角色 | 外部觸發 | 主要輸出 | 先看程式碼 |
| --- | --- | --- | --- | --- |
| `ProjectARVR` | 早期 AR/VR 固定切圖測試 | JSON Socket：`ProjectARVRInit`, `SwitchPGCompleted` | `ObjectiveTestResult`, CSV, Socket 結果 | `ARVRWindow.xaml.cs`, `Services/SocketControl.cs` |
| `ProjectARVRLite` | 輕量 AR/VR 快速測試 | JSON Socket，自動開窗 | CSV, Socket 結果 | `ARVRWindow.xaml.cs`, `TestTypeConfig.cs` |
| `ProjectARVRPro` | 主要 AR/VR 流程組專案 | JSON Socket, `RunAll`, `SwitchGroup`, 串口切圖 | SQLite, CSV, Legacy CSV, XLSX, Socket | `Process/`, `Recipe/`, `Services/` |
| `ProjectARVRPro.IntegrationDemo` | 客戶側 TCP/JSON 示例 | 客戶端連接 `6666` 發 JSON | JSON、結果表、CSV | `Program.cs`, `MainWindow.xaml.cs`, `Contracts/` |
| `ProjectBlackMura` | 面板 Black Mura 檢測 | PG/MES 串口：`CON`, `CCPI`, `CSN`, `CGI` | Excel、POI 覆蓋、Mura 結果 | `MainWindow.xaml.cs`, `HYMesManager.cs` |
| `ProjectHeyuan` | 河源四點 WBRO 測試 | STX/ETX 串口 | WBRO CSV, MES 上傳 | `ProjectHeyuanWindow.xaml.cs`, `TempResult.cs` |
| `ProjectKB` | 鍵盤背光測試 | Modbus、MES DLL、可選 Socket | SQLite、CSV、summary、MES | `ProjectKBWindow.xaml.cs`, `Modbus/`, `MesDll.cs` |
| `ProjectLUX` | LUX 光學自動化 | 文字 Socket：`T00XX,SN;` | SQLite、`C_*.csv`、`B_*.csv`、`D_*.csv`、PDF | `LUXWindow.xaml.cs`, `Process/`, `Recipe/`, `Fix/` |
| `ProjectShiyuan` | 視源 JND/POI 匯出 | 目前以手動 Flow 為主 | JND CSV、POI CSV、偽彩圖 | `ShiyuanProjectWindow.xaml.cs`, `ShiyuanProjectExport.cs` |

## 按協議分類

| 類型 | 專案 | 交接重點 |
| --- | --- | --- |
| JSON Socket | ARVR、ARVRLite、ARVRPro | `EventName`、SN、切圖完成、最後 `ProjectARVRResult` |
| 文字 Socket | LUX | `T00XX` 與 `ProcessMeta.SocketCode` 是否匹配 |
| 串口/MES | BlackMura、Heyuan | STX/ETX、DeviceId、返回碼、NG 放行 |
| PLC/Modbus | KB | holding register、觸發值 `1`、完成回寫 `0`、SN 來源 |
| 客戶示例 | ARVRPro.IntegrationDemo | 只保留公開 JSON 合約，不引用 ColorVision 內部業務 |
| 手動/離線 | Shiyuan | `DataPath`、模板選擇、固定圖像路徑 |

## 最小冒煙驗收

| 專案 | 驗收點 |
| --- | --- |
| ARVR | 發 `ProjectARVRInit`，完成切圖到 `OpticCenter`，生成 CSV 和 Socket 結果 |
| ARVRLite | 確認啟用測項配置、預處理、CSV 和 Socket 結果 |
| ARVRPro | 切換一個流程組，執行 `RunAll`，驗證切圖、Recipe、CSV/Legacy/Socket |
| IntegrationDemo | 解析樣例 JSON，連線測試服務，能處理半包/黏包 |
| BlackMura | PG 五色切圖完成，輸出 `<SN>.xlsx` 和 POI 覆蓋 |
| Heyuan | 串口連接，產生四個 POI，生成 WBRO CSV 並上傳 |
| KB | Modbus 寫 `1` 觸發，完成寫回 `0`，CSV/summary/MES 一致 |
| LUX | 發 `T00XX,SN;`，匹配 `SocketCode`，生成 CSV/SQLite |
| Shiyuan | 手動執行 Flow，生成 JND/POI CSV 和偽彩圖 |

## 變更歸屬

| 變更 | 優先位置 | 文件同步 |
| --- | --- | --- |
| 新增客戶測項 | 專案 `Process/`, `Recipe/`, `Fix/`, 結果模型 | 專案頁和本矩陣 |
| 新增外部命令 | `Services/SocketControl.cs`, `HYMesManager`, `ModbusControl` | 協議分類和專案頁 |
| 變更結果欄位 | `ObjectiveTestResult`, exporter, Socket 回傳, SQLite model | 結果輸出和專案頁 |
| 變更流程或模板名 | `ProcessGroup`, `ProcessMeta`, 主視窗關鍵字匹配 | 交接手冊和專案頁 |
| 變更打包內容 | `manifest.json`, `.csproj`, 打包腳本, README/CHANGELOG | 專案總覽和腳本文件 |

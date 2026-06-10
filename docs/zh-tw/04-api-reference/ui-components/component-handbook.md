# UI DLL 元件手冊

本頁按 `UI/` 下的發布單元說明每個 DLL。目標是讓接手人員先知道每個元件負責什麼、誰會引用它、入口在哪裡、發布時要檢查什麼，再進入單獨模組頁。

如果要按具體控制項、視窗或擴充點查原始碼，配合閱讀 [UI 元件目錄](./control-catalog.md)。如果要排查為什麼沒有發現選單、設定項、ImageEditor 工具、Socket handler 或 Solution 編輯器，配合閱讀 [UI 執行時元件交接手冊](./ui-runtime-handoff.md)。發布 DLL 或 NuGet 包時，配合閱讀 [UI DLL 發布矩陣](./release-matrix.md)。

## 元件分層

| 層級 | DLL | 說明 |
| --- | --- | --- |
| 基礎契約層 | `ColorVision.Common.dll` | MVVM、共享介面、狀態列後設資料、初始化器、粗粒度權限和工具類 |
| 主題資源層 | `ColorVision.Themes.dll` | 主題資源字典、窗口基類、標題列外觀、通用控制項 |
| UI 基礎設施層 | `ColorVision.UI.dll` | 配置、選單、外掛載入、屬性編輯器、快捷鍵、多語言、日誌和狀態列 |
| 原生影像橋接層 | `ColorVision.Core.dll` | `HImage`、OpenCV helper P/Invoke、WPF bitmap 橋接 |
| 資料接入層 | `ColorVision.Database.dll` | SqlSugar DAO、MySQL/SQLite 配置、資料庫瀏覽器 Provider |
| 桌面通訊層 | `ColorVision.SocketProtocol.dll` | 本地 TCP server、JSON/Text 分發、訊息 SQLite 歷史、狀態列與管理視窗 |
| 排程層 | `ColorVision.Scheduler.dll` | Quartz 排程、任務配置、執行歷史和管理視窗 |
| 影像編輯層 | `ColorVision.ImageEditor.dll` | `ImageView`、繪圖圖元、overlay、工具列、偽彩、CIE、3D、即時影像 |
| 桌面工具層 | `ColorVision.UI.Desktop.exe` / package | 設定、嚮導、外掛市場、下載器、診斷視窗 |
| 工作區層 | `ColorVision.Solution.dll` | `.cvsln` 工作區、檔案樹、編輯器、AvalonDock、終端、本地 RBAC |

## 快速落點

| 要做什麼 | 優先引用或修改 |
| --- | --- |
| 新增 ViewModel、Command、共享介面 | `ColorVision.Common` |
| 新增主題、通用窗口樣式 | `ColorVision.Themes` |
| 新增選單、設定、狀態列、PropertyGrid | `ColorVision.UI` |
| 調用 OpenCV/native 影像能力 | `ColorVision.Core` |
| 新增資料庫瀏覽來源或 DAO | `ColorVision.Database` |
| 新增 Socket JSON 事件處理 | `ColorVision.SocketProtocol` |
| 新增定時任務 | `ColorVision.Scheduler` |
| 新增影像工具、圖元、overlay | `ColorVision.ImageEditor` |
| 新增設定頁、嚮導、市場、下載器 | `ColorVision.UI.Desktop` |
| 新增工作區編輯器、檔案樹、終端、RBAC | `ColorVision.Solution` |

## 維護邊界

- `Common`、`Themes`、`Core` 不應反向依賴高層視窗、Engine 業務或客戶專案流程。
- `ImageEditor` 負責顯示、工具、圖元和 overlay，不負責客戶 CSV、MES 或 Socket 結果格式。
- `Solution` 是工作區殼層，不是設備控制或專案流程中心。
- 新增公開窗口、Provider、PropertyEditor、EditorTool、IEditor 時，必須同步更新本頁或對應 DLL 頁。

## 發布前必看

- [UI DLL 發布手冊](./publishing.md)
- [UI DLL 發布矩陣](./release-matrix.md)
- [UI 執行時元件交接手冊](./ui-runtime-handoff.md)
- 每個 `.csproj` 中的 `TargetFrameworks`、`VersionPrefix`、`GeneratePackageOnBuild`、資源項和 native runtime。

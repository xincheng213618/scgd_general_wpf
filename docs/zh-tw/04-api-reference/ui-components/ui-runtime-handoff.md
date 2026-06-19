# UI 執行時元件交接手冊

本頁面向接手 `UI/` 執行時鏈路的人。它回答主程序啟動後，選單、設定、外掛載入、PropertyGrid、ImageEditor、Socket、Scheduler、市場和 Solution 工作區如何被發現、裝配和排障。

如果任務是發布 DLL 或 NuGet 包，先看 [UI DLL 發布場景手冊](./ui-dll-release-playbook.md) 和 [UI DLL 發布矩陣](./release-matrix.md)。如果任務是改控制項或定位界面問題，先看本頁，再進入 [UI 元件目錄](./control-catalog.md) 和具體 DLL 頁。

## 邊界表

| 模組 | 執行時定位 | 不應放入 |
| --- | --- | --- |
| `ColorVision.Common` | 基礎契約、MVVM、命令、選單/狀態列介面 | 具體窗口、客戶業務、Engine 設備邏輯 |
| `ColorVision.Themes` | 主題資源、窗口基類、通用控制項 | 外掛、專案包、算法業務 |
| `ColorVision.UI` | 選單、外掛載入、配置、設定發現、PropertyGrid、日誌、熱鍵 | 市場 UI、Solution 工作區、專案流程 |
| `ColorVision.Core` | 影像 native bridge、`HImage`、OpenCV helper | WPF 互動控制項、客戶判定 |
| `ColorVision.Database` | SqlSugar、MySQL/SQLite 配置、資料庫瀏覽器 | 設備協議、專案匯出格式 |
| `ColorVision.SocketProtocol` | 本地 TCP server、JSON/Text 事件分發、訊息歷史 | 具體專案測試流程 |
| `ColorVision.Scheduler` | Quartz 任務、任務窗口、執行歷史 | 長耗時算法實作 |
| `ColorVision.ImageEditor` | `ImageView`、工具列、圖元、overlay、CIE/3D | 客戶結果判定和匯出 |
| `ColorVision.UI.Desktop` | 設定、嚮導、市場、下載器、診斷 | 主程序啟動中心、Engine 流程 |
| `ColorVision.Solution` | `.cvsln` 工作區、檔案樹、編輯器、終端、RBAC | 設備控制、算法執行、專案測試主鏈 |

## 發現機制

| 能力 | 發現入口 | 第一檢查點 |
| --- | --- | --- |
| 外掛載入 | `PluginLoader.LoadPlugins("Plugins")` | 目錄、manifest、`.deps.json`、依賴 DLL 版本、是否禁用 |
| 選單 | `MenuManager.LoadMenuForWindow` | `OwnerGuid`、`GuidId`、`Order`、目標窗口、權限 |
| 設定窗口 | `ConfigSettingManager.GetAllSettings` | `ConfigService`、`IConfig`、`[ConfigSetting]`、搜尋過濾 |
| PropertyGrid | `PropertyEditorWindow` | public get/set、`PropertyEditorTypeAttribute`、clone/reset |
| 狀態列 | `StatusBarManager` | Provider 是否被發現，主窗口是否綁定 |
| ImageEditor 工具 | `IEditorToolFactory` | 工具是否可建立、`GuidId`、可見性配置 |
| Image openers | `IImageOpen` + `FileExtensionAttribute` | 副檔名、構造參數、檔案路徑 |
| Socket 事件 | `SocketManager` / `ISocketJsonHandler` | 端口、協議模式、`EventName`、訊息歷史、專案 handler |
| Scheduler | `QuartzSchedulerManager` | `scheduler_tasks.json`、Job 類型、歷史 DB |
| Solution 編輯器 | `EditorManager` | 副檔名、註冊順序、檔案鎖和權限 |

## 常見排障

| 現象 | 先查 | 再查 |
| --- | --- | --- |
| 外掛安裝後沒有選單 | `PluginLoader` 是否載入程序集 | `MenuManager` 的父選單、過濾和權限 |
| 設定項沒有出現 | `ConfigSettingManager` 是否掃到類型 | `IConfigSettingProvider` 或 `[ConfigSetting]` |
| 圖片能開但工具列少 | `IEditorToolFactory` 是否發現工具 | 可見性配置和 `GuidId` |
| overlay 坐標不對 | Draw 圖元和縮放坐標系 | Engine result handler 坐標轉換 |
| Socket 收到消息但專案沒跑 | Socket 消息歷史 | 專案 `ISocketJsonHandler` 和 `EventName` |
| 任務不執行 | Quartz 是否啟動 | 任務 JSON 和 Job 類型 |
| Solution 打開檔案無反應 | 編輯器擴展名匹配 | AvalonDock/AvalonEdit/WebView2 依賴 |

## 新增 UI 元件交接清單

新增或重構 UI 元件時，記錄：所屬 DLL、發現方式、入口類、配置、依賴、驗證步驟，以及要同步更新的文檔。

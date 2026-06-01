# SystemMonitor 外掛

本頁只描述當前倉庫裡實際存在的 SystemMonitor 外掛實現，不再繼續維護“版本資訊 + 調優手冊 + 理想化架構圖”式舊稿。

## 先看這個外掛現在是什麼

按當前原始碼狀態，SystemMonitor 是一個偏輕量的系統監控外掛，核心不是獨立應用外殼，而是一組圍繞單例監控服務展開的整合點：

- `SystemMonitors`：監控資料和命令的中心單例。
- `SystemMonitorProvider`：把外掛接入設定頁和工具選單。
- `SystemMonitorIStatusBarProvider`：把可選監控項接入主程式狀態列。
- `SystemMonitorControl`：實際顯示監控資料的 WPF 控制元件。

因此它更接近“系統監控服務 + UI 接入層”，而不是一個體量很重的獨立視窗程式。

## 當前最重要的檔案

- `Plugins/SystemMonitor/manifest.json`
- `Plugins/SystemMonitor/SystemMonitors.cs`
- `Plugins/SystemMonitor/SystemMonitorControl.xaml(.cs)`
- `Plugins/SystemMonitor/SystemMonitorIStatusBarProvider.cs`

其中 `SystemMonitors.cs` 承擔了絕大多數真正的執行時邏輯；另外兩個型別主要負責把它接到宿主 UI。

## 當前功能面實際包括什麼

從 `SystemMonitors` 的實現看，這個外掛當前覆蓋的監控面明顯比舊文件裡“時間 + RAM”更廣：

### 效能計數器

外掛會非同步初始化 Windows 效能計數器，並定時更新：

- 系統 CPU 使用率
- 當前程序 CPU 使用率
- 系統 RAM 使用率
- 當前程序私有工作集

如果效能計數器初始化失敗，當前實現會降級為不重新整理這些數值，而不是中止整個外掛。

### 磁碟與網路

外掛當前會主動載入並維護：

- 所有已就緒磁碟的容量、已用空間、空閒空間、佔用比例
- 非 loopback / tunnel 的網路介面資訊
- 網路介面的 IPv4 地址、MAC 地址、鏈路速率和狀態

這部分資料並不依賴狀態列開關，狀態列只是決定是否把其中一部分投影到主視窗底部。

### 程序與執行時環境

當前還會收集：

- 前 10 個高記憶體佔用程序
- 當前程序執行緒數和控制代碼數
- 系統啟動時間、應用執行時長、系統執行時長
- CPU 名稱、主機名、.NET 執行時、系統架構、使用者名稱
- 主螢幕解析度

### GPU 與快取

外掛還會讀取 `ConfigCuda.Instance`，在可用時展示 CUDA 裝置名稱和視訊記憶體資訊；同時提供快取大小統計和清理命令。

## 當前接入宿主的三條鏈

### 設定頁

`SystemMonitorProvider` 實現了 `IConfigSettingProvider`，會把 `SystemMonitors.GetInstance()` 作為設定頁資料來源，並用 `SystemMonitorControl` 作為顯示控制元件。

這意味著設定頁和單獨彈窗看的其實是同一份單例資料，而不是兩套監控例項。

### 工具選單

同一個 `SystemMonitorProvider` 還實現了 `IMenuItemProvider`，當前會往 `Tool` 選單下注入“效能監控”入口，並開啟一個承載 `SystemMonitorControl` 的普通 WPF 視窗。

### 狀態列

`SystemMonitorIStatusBarProvider` 實現的是 `IStatusBarProviderUpdatable`，會根據配置開關動態決定狀態列項是否存在。當前可投影到狀態列的項包括：

- 時間
- 應用執行時長
- CPU 文字
- RAM 文字
- 磁碟圖示與剩餘空間

因此它不是舊文件裡那種固定兩項的靜態狀態列提供器。

## 當前配置模型

`SystemMonitorSetting` 目前至少包含這些開關和參數：

- `UpdateSpeed`
- `DefaultTimeFormat`
- `IsShowTime`
- `IsShowRAM`
- `IsShowCPU`
- `IsShowUptime`
- `IsShowDisk`

舊文件裡只寫時間與 RAM，已經覆蓋不全。

## 當前命令面

`SystemMonitors` 當前暴露的使用者命令主要有：

- `ClearCacheCommand`
- `RefreshDrivesCommand`
- `RefreshNetworkCommand`
- `RefreshProcessesCommand`

這些命令對應的真實行為分別是清理應用資料與日誌目錄、過載磁碟列表、過載網路介面列表、過載高佔用程序列表。

## 當前幾個最容易寫錯的點

### 它不是獨立視窗程式為中心的外掛

雖然選單會開啟一個視窗，但視窗裡只是掛載 `SystemMonitorControl`。真正持續執行的核心物件是 `SystemMonitors` 單例。

### 它不只是狀態列時間外掛

當前狀態列只是三條整合鏈之一。大量資料其實服務於完整監控控制元件，包括磁碟、網路、GPU、程序列表和快取統計。

### `IStatusBarProviderUpdatable` 很關鍵

狀態列顯示項的重新整理當前依賴 `SystemMonitorIStatusBarProvider` 監聽配置變更後觸發 `StatusBarItemsChanged`。如果把它誤寫成普通靜態 provider，會把現在這條動態重新整理鏈寫偏。

### 型別命名和名稱空間不要想當然

`SystemMonitors` 和 `SystemMonitorSetting` 當前位於 `ColorVision.UI.Configs` 名稱空間，而不是外掛自己的 `SystemMonitor` 名稱空間。這是現狀程式碼的一部分，不要擅自按“外掛內部自成體系”去重述。

## 推薦閱讀順序

1. `Plugins/SystemMonitor/SystemMonitors.cs`
2. `Plugins/SystemMonitor/SystemMonitorControl.xaml.cs`
3. `Plugins/SystemMonitor/SystemMonitorIStatusBarProvider.cs`
4. `Plugins/SystemMonitor/manifest.json`

這樣能先抓住真實控制面，再回到選單、狀態列和裝載資訊。

## 繼續閱讀

- [Plugins/README.md](../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/pattern.md](./pattern.md)
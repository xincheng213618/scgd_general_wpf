# ColorVision.Scheduler

本頁只描述 `UI/ColorVision.Scheduler/` 當前已經落地的排程能力，不再繼續維護舊文件裡那種“通用 Quartz 教程 + 想象中的任務平台功能清單”。

## 模組定位

`ColorVision.Scheduler` 當前是桌面側的任務排程與監控模組，核心不是“抽象任務型別大全”，而是這三條真實鏈：

- `QuartzSchedulerManager` 管理 Quartz 排程器和任務恢復
- `scheduler_tasks.json` 儲存任務配置
- `SchedulerHistory.db` 儲存執行歷史和統計恢復資料

所以它既不是純 UI 控制元件，也不是隻有 Quartz 包裝層。

## 當前最關鍵的檔案

從專案目錄看，最值得先認識的是：

- `QuartzSchedulerManager.cs`：排程器主入口
- `TaskViewerWindow.xaml(.cs)`：任務檢視、過濾和右鍵操作視窗
- `CreateTask.xaml(.cs)`：新建和編輯任務視窗
- `TaskExecutionListener.cs`：執行監聽與統計更新
- `Data/SchedulerDbManager.cs`：歷史記錄 SQLite 持久化
- `MenuTaskViewer.cs`：選單入口和初始化器
- `SchedulerInfo.cs`：任務展示與持久化模型

## 關鍵入口型別

### `QuartzSchedulerManager`

`QuartzSchedulerManager` 是當前排程模組的中心物件。它負責：

- 啟動 Quartz 排程器
- 掃描已載入程式集中的 `IJob` 型別
- 維護 `TaskInfos`
- 從 JSON 檔案載入任務配置
- 在啟動後恢復歷史任務
- 提供暫停、恢復、刪除、更新和建立任務的方法

當前任務配置檔案預設放在：

- `%AppData%/ColorVision/scheduler_tasks.json`

這說明當前任務定義並不是完全存在資料庫裡，而是以 JSON 配置為主、SQLite 歷史為輔。

### `TaskViewerWindow`

`TaskViewerWindow` 是當前任務管理主視窗。它負責：

- 繫結 `TaskInfos`
- 按名稱、分組、狀態過濾
- 從排程器讀取已註冊任務的下一次和上一次執行時間
- 透過右鍵選單執行編輯、檢視屬性、暫停、繼續、立即執行、刪除、檢視歷史

這頁舊文件裡那些“大而全的監控面板設計圖”都不如這裡的實際視窗更有參考價值。

### `CreateTask`

`CreateTask` 視窗承擔新建和編輯任務。它和 `SchedulerInfo` 配合，決定一個任務最終如何被序列化、恢復和更新。

### `SchedulerDbManager`

執行歷史不是存在同一個 JSON 檔案裡，而是單獨存在 SQLite 資料庫中。`SchedulerDbManager` 當前負責：

- 初始化 `%AppData%/ColorVision/SchedulerHistory.db`
- 寫入執行記錄
- 查詢單任務或全量執行歷史
- 計算統計資料用於重啟後恢復
- 清理舊記錄

這也是當前“執行次數、成功失敗數、平均耗時”這類資料能在重啟後延續的原因。

### `TaskExecutionListener`

執行時統計更新和執行反饋，並不是視窗自己輪詢拿到的，而是透過監聽器回寫任務狀態和執行歷史。

## 當前執行時主鏈

排程模組當前更接近下面這條鏈：

1. `TaskViewerInitializer` 或選單入口觸發 `QuartzSchedulerManager.GetInstance()`。
2. `QuartzSchedulerManager` 啟動 Quartz 排程器。
3. 它掃描當前已載入程式集裡的 `IJob` 型別，建立任務型別字典。
4. 讀取 `%AppData%/ColorVision/scheduler_tasks.json`。
5. 啟動後延遲恢復已有任務。
6. `TaskExecutionListener` 在任務執行時更新狀態與統計。
7. `SchedulerDbManager` 把執行記錄寫入 `SchedulerHistory.db`。
8. `TaskViewerWindow` 再把這些狀態、歷史和統計展示給使用者。

這個鏈路比舊文件裡那種“任務編輯器/監控面板/日誌檢視器三層架構”更貼近現有實現。

## 當前實現有哪些邊界

### 任務型別來自已載入程式集

當前 `QuartzSchedulerManager` 會遍歷 `AssemblyService.Instance.GetAssemblies()`，收集實現 `IJob` 的型別，並優先用 `DisplayNameAttribute` 作為顯示名。

所以新增任務型別，本質上是新增可被程式集掃描到的 `IJob` 實現，而不是往某張任務型別表裡登記。

### 配置恢復和執行歷史是兩套儲存

當前任務定義和恢復主要靠 JSON；執行歷史和統計恢復主要靠 SQLite。不要把這兩者混寫成單一資料庫排程中心。

### 任務視窗是真實管理入口，不是示意圖

當前最重要的使用者入口就是 `TaskViewerWindow` 和 `CreateTask`。很多舊文件裡編造的“批次匯出、統計報告、複雜面板分割槽”並沒有必要繼續作為既有能力列出，除非程式碼裡能直接對應到具體實現。

## 當前更適合怎樣讀這個專案

### 想看排程器怎麼啟動和恢復

先看：

- `QuartzSchedulerManager.cs`
- `MenuTaskViewer.cs`

### 想看任務介面和操作入口

先看：

- `TaskViewerWindow.xaml(.cs)`
- `CreateTask.xaml(.cs)`

### 想看執行歷史和統計

先看：

- `Data/SchedulerDbManager.cs`
- `TaskExecutionListener.cs`
- `ExecutionHistoryWindow.xaml(.cs)`

## 這頁不再做什麼

本頁不再繼續維護這些高風險內容：

- 通用 Quartz 示例程式碼大全
- 未經核實的系統任務/業務任務/維護任務分類表
- 想象中的統一任務平台功能矩陣
- 過時版本號和目標框架清單

如果後續要補某個具體任務型別，應直接落到實際任務實現或視窗頁，而不是在這裡繼續寫教程式內容。

## 繼續閱讀

- [UI元件概覽](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Database](./ColorVision.Database.md)
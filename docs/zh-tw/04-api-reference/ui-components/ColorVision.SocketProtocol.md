# ColorVision.SocketProtocol

本頁只描述 UI/ColorVision.SocketProtocol 當前已經落地的通訊實現，不再延續舊文件裡那種“通用 JSON 協議層示例”和不匹配的訊息模型說明。

## 模組定位

ColorVision.SocketProtocol 當前是一個桌面側本地 TCP 通訊模組，主要負責：

- 啟動和停止 Socket 伺服器
- 分發 JSON 或純文字請求
- 持久化訊息記錄到 SQLite
- 提供管理視窗和狀態列入口
- 接入設定系統

它不是一個抽象的“裝置協議規範文件”，而是一套已經和 UI、配置、資料庫瀏覽入口耦合在一起的實際模組。

## 當前最關鍵的檔案

從專案目錄看，最值得優先閱讀的是：

- `SocketManager.cs`：伺服器、客戶端、分發器和訊息管理主入口
- `SocketInitializer.cs`：啟動時初始化和啟停監聽
- `SocketConfig.cs`：通訊配置
- `ISocketJsonHandler.cs`：JSON 請求處理擴充套件點
- `SocketMessage.cs`：訊息持久化實體
- `SocketMessageManager.cs`：SQLite 持久化和查詢
- `SocketManagerWindow.xaml(.cs)`：管理和檢視視窗
- `SocketStatusBarProvider.cs`：狀態列入口
- `SocketConfigProvider.cs`：設定系統接入點

## 關鍵入口型別

### SocketManager

`SocketManager` 是當前通訊模組的中心物件。它負責：

- 持有 `SocketConfig`
- 建立 `SocketJsonDispatcher` 和 `SocketTextDispatcher`
- 管理 `SocketMessageManager`
- 啟動和停止伺服器
- 跟蹤連線狀態
- 暴露配置編輯命令

如果只讀一個檔案來理解整個模組，首選就是 `SocketManager.cs`。

### SocketInitializer

當前模組確實存在 `SocketInitializer`，而且它是實際啟動入口之一。它會：

- 啟動時讀取 `SocketConfig.Instance.IsServerEnabled`
- 在啟用時呼叫 `SocketManager.GetInstance().StartServer()`
- 訂閱 `ServerEnabledChanged`，在執行中動態啟停服務

這意味著通訊服務是否上線，當前主要受配置驅動，而不是僅靠使用者手動開啟視窗。

### SocketConfig

`SocketConfig` 當前配置內容主要包括：

- 是否啟用伺服器
- 監聽 IP
- 埠
- Buffer 大小
- 協議模式：`Json` 或 `Text`

舊文件裡寫的超時、自動重連等欄位，並不是當前類裡真實存在的配置項。

### SocketJsonDispatcher / SocketTextDispatcher

當前協議分發分成兩套：

- `SocketJsonDispatcher`：掃描 `ISocketJsonHandler`
- `SocketTextDispatcher`：掃描 `ISocketTextDispatcher`

其中 JSON 處理器當前按 `EventName` 匹配，請求和響應的真實模型是：

- `SocketRequest`：`Version`、`MsgID`、`EventName`、`SerialNumber`、`Params`
- `SocketResponse`：`Version`、`MsgID`、`EventName`、`SerialNumber`、`Code`、`Msg`、`Data`

因此它不是舊文件裡那種泛化的 `type/data/timestamp` 訊息格式。

### SocketMessage / SocketMessageManager

當前訊息持久化不是一個概念層功能，而是直接落地在 SQLite。`SocketMessage` 儲存的主要是：

- 客戶端地址
- 方向（接收/傳送）
- 內容
- 時間
- EventName / MsgID / ResponseCode

`SocketMessageManager` 則負責：

- 初始化 `SocketMessages.db`
- 載入最近訊息
- 插入、刪除和查詢訊息
- 開啟資料庫檔案位置
- 提供資料庫瀏覽入口

資料庫預設路徑在：

- `%AppData%/ColorVision/Config/SocketMessages.db`

### SocketManagerWindow 與 SocketStatusBarProvider

當前使用者側主要入口不是一堆協議示例程式碼，而是兩個 UI 接入點：

- `SocketManagerWindow`：檢視歷史訊息、訊息詳情、複製、重發、刪除
- `SocketStatusBarProvider`：在狀態列反映連線狀態，並點選開啟管理視窗

另外，`SocketManagerWindow.xaml.cs` 裡還定義了一個選單入口類 `MenuProjectManager`，當前掛在 Help 選單下開啟管理視窗。

當前管理視窗已經不只是“訊息列表 + 詳情”的最小形態。視窗頂部會顯示服務啟用狀態、服務是否開啟、監聽地址、協議模式和客戶端數量；開啟失敗時會直接顯示最後一次錯誤資訊。訊息區支援文字過濾、方向過濾、自動滾動和列表虛擬化；右側透過“訊息詳情 / 連線的客戶端 / 服務診斷”標籤頁組織資訊，詳情區支援 JSON 格式化檢視。重發訊息時會優先按原始客戶端地址匹配連線，找不到時可以使用當前選中的客戶端作為兜底目標。

常用快捷鍵：

- `Ctrl+F`：聚焦過濾框
- `Esc`：清空過濾
- `F5`：重新載入最近訊息
- `Ctrl+C`：複製選中訊息內容
- `Delete`：刪除選中訊息

## 當前執行時主鏈

現有鏈路大致是：

1. `SocketInitializer` 啟動並監聽 `SocketConfig.Instance.IsServerEnabled`。
2. 服務啟用時，`SocketManager` 啟動 TCP 伺服器。
3. 收到請求後，按當前配置的協議模式走 JSON 或 Text 分發。
4. JSON 請求按 `EventName` 匹配到 `ISocketJsonHandler` 實現。
5. 收發訊息被寫入 `SocketMessageManager` 管理的 SQLite 資料庫。
6. `SocketStatusBarProvider` 和 `SocketManagerWindow` 從管理器讀取狀態與訊息列表。

## 當前實現有哪些邊界

### 不是純 JSON 協議庫

雖然 JSON 是主要模式之一，但當前實現同時支援 `SocketPhraseType.Text`。把整個模組寫成“統一 JSON 協議層”會漏掉文字模式和狀態列、視窗、持久化這些真實職責。

### 不是隻有處理器介面

舊文件把重點壓在 `ISocketJsonHandler` 上，但當前模組的價值同樣來自：

- 初始化器
- 管理視窗
- 狀態列入口
- SQLite 訊息歷史

如果只寫 handler 擴充套件點，很容易把模組寫扁。

### 配置欄位要按真實類描述

當前 `SocketConfig` 沒有舊文件裡聲稱的 `ReceiveTimeout`、`SendTimeout`、`AutoReconnect` 這些欄位。描述通訊配置時必須以真實屬性為準。

## 當前更適合怎樣讀這個模組

### 想看伺服器和分發主鏈

先看：

- `SocketManager.cs`
- `SocketInitializer.cs`
- `ISocketJsonHandler.cs`

### 想看設定和狀態列接入

先看：

- `SocketConfig.cs`
- `SocketConfigProvider.cs`
- `SocketStatusBarProvider.cs`

### 想看訊息歷史和管理視窗

先看：

- `SocketMessage.cs`
- `SocketMessageManager.cs`
- `SocketManagerWindow.xaml.cs`

## 最佳化路線

這個模組後續最佳化建議分四層推進：

| 階段 | 目標 | 重點 |
| --- | --- | --- |
| P0 穩定性 | 把服務生命週期和 TCP 邊界收緊 | 防重複啟動、取消令牌、統一停止路徑、粘包/半包處理 |
| P1 可觀測性 | 提高現場排查效率 | 訊息匯出、連線生命週期、錯誤統計、處理耗時 |
| P2 協議化 | 降低外部裝置對接成本 | 錯誤碼、Handler 後設資料、JSON Schema、版本相容 |
| P3 效能與容量 | 支援長期執行和更大歷史量 | 分頁載入、資料庫索引、批次寫庫、保留策略 |

詳細路線見 [Socket 通訊模組最佳化路線](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md)。

## 這頁不再做什麼

本頁不再繼續維護這些高風險內容：

- 編造的統一訊息欄位模型
- 與真實類不匹配的配置項列表
- 只有 handler 示例、沒有管理視窗和持久化邊界的介紹
- 把當前模組寫成純協議規範而不是實際 UI 通訊模組

## 繼續閱讀

- [UI元件概覽](./README.md)
- [ColorVision.Database](./ColorVision.Database.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [Socket 通訊模組最佳化路線](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md)

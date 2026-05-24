# FlowEngineLib 架構

本頁只描述當前倉庫裡實際在執行的流程編輯與執行鏈，不再繼續維護把 FlowEngineLib 寫成一套獨立分層框架的舊稿。

## 先看它在系統裡的位置

FlowEngine 相關能力並不只存在於 `Engine/FlowEngineLib/`。當前真正的使用鏈跨了兩層：

- `FlowEngineLib/` 提供節點編輯器裡的執行控制、開始節點、結束節點和服務節點基礎能力
- `Engine/ColorVision.Engine/Templates/Flow/` 負責把流程模板、編輯視窗、執行視窗和批次處理接到主程式裡

因此討論 FlowEngine 架構時，只寫庫本身會漏掉一半真實執行時。

## 當前最關鍵的物件

### `FlowEngineControl`

`FlowEngineControl` 是執行控制的中心物件。它當前負責：

- 繫結 `STNodeEditor`
- 在節點加入編輯器時識別開始節點和服務節點
- 維護 `startNodeNames` 和 `services`
- 從檔案或 Base64 載入畫布
- 選擇開始節點並啟動、停止流程
- 在開始節點完成時向外丟擲 `Finished`

這意味著它並不是一個抽象排程介面，而是直接和節點編輯器例項、節點物件、服務集合綁在一起的執行時控制器。

### `BaseStartNode`

開始節點負責把一次流程執行封裝為 `CVStartCFC`，並把啟動、停止、完成事件沿流程圖派發出去。

當前要點是：

- `Start(serialNumber)` 會建立 `CVStartCFC`
- `DoDispatch(...)` 會把動作向下遊傳遞
- `FireFinished(...)` 才是真正發出流程結束事件的地方

因此“流程完成”不是控制器自己推斷出來的，而是由開始節點最終發出的。

### `CVBaseServerNode`

大多數裝置節點、演算法節點都落在 `CVBaseServerNode` 體系裡。它們負責：

- 傳送和等待執行時動作
- 處理超時、失敗和返回資料
- 把節點結果繼續傳給下游
- 透過 `nodeEndEvent` 報告單個節點結束狀態

這裡的 `nodeEndEvent` 很重要，但它只表示節點級別結束，不等於整條流程已經結束。

### `CVEndNode`

結束節點是流程完成鏈的最後一跳。當前實現裡，結束節點會在結束處理時呼叫 `startAction.FireFinished()`，從而把整條流程標記為完成。

這就是為什麼“某個節點執行完了”和“整條流程 finished 了”在系統裡是兩件不同的事。

## 流程真正是怎麼跑起來的

當前主鏈大致是：

1. `TemplateFlow` 或 `FlowEngineToolWindow` 準備流程資料。
2. `FlowEngineToolWindow` 把 `FlowEngineLib.dll` 載入到節點編輯器。
3. `FlowEngineControl` 繫結 `STNodeEditor`，在節點加入時識別開始節點和服務節點。
4. `LoadFromBase64(...)` 或 `Load(...)` 把流程圖載入畫布。
5. `StartNode(...)` 選擇指定開始節點，或者預設取第一個開始節點。
6. `BaseStartNode` 建立 `CVStartCFC` 並向下遊節點派發。
7. 各 `CVBaseServerNode` 派生節點處理自己的執行、超時和資料傳遞。
8. `CVEndNode` 在結束時呼叫 `startAction.FireFinished()`。
9. `BaseStartNode.Finished` 被觸發。
10. `FlowEngineControl.Start_Finished(...)` 再把它轉成自己的 `Finished` 事件。

這個完成鏈比舊文件裡的“某節點結束即流程結束”要嚴格，也更接近當前程式碼。

## Engine 層是怎麼把它接進主程式的

### 流程模板

`TemplateFlow` 讓流程圖能以模板形式存在於系統裡，支援：

- 模板列表管理
- 雙擊直接開啟流程編輯器
- `.stn` / `.cvflow` 匯入
- 流程包匯入時處理關聯模板

### 編輯視窗

`FlowEngineToolWindow` 是獨立流程編輯面。它負責：

- 承載 `STNodeEditor`
- 載入 `FlowEngineLib.dll`
- 接入撤銷、重做、複製、貼上、縮放和自動對齊
- 透過 `STNodeEditorHelper` 連線屬性面板和節點樹

所以當前編輯體驗不是 `FlowEngineLib` 自帶 UI，而是 Engine 層包了一層 WPF 視窗來接它。

### 執行視窗

真正落到主程式日常使用裡的，是 `DisplayFlow` 和 `FlowControl` 這條線。

`DisplayFlow` 當前負責：

- 重新整理當前流程模板
- 啟動前執行預處理
- 監聽流程完成
- 寫入執行日誌、批次資訊和進度
- 在流程完成後觸發自訂批處理

這說明主程式裡的流程執行，不只是“把圖跑完”，還和批次記錄、日誌文字、後處理擴充套件綁在一起。

## 當前有哪些容易寫錯的邊界

### `nodeEndEvent` 不是流程完成事件

`CVCommonNode` 上的 `nodeEndEvent` 只用於節點級反饋。真正的流程完成鏈是：

- EndNode 呼叫 `startAction.FireFinished()`
- `CVStartCFC.FireFinished()` 回到開始節點
- `BaseStartNode.Finished` 被觸發
- `FlowEngineControl.Finished` 再向外丟擲

如果把這兩種事件混為一談，就會把失敗傳播、進度更新和最終完成判斷全寫偏。

### 開始節點不是任意節點

`FlowEngineControl` 只會在節點加入時把 `BaseStartNode` 收進 `startNodeNames`。啟動流程時，如果沒有指定名稱，預設取第一個開始節點。

所以流程是否可啟動，和開始節點是否存在、是否 ready 直接相關。

### 失敗傳播要看節點型別

流程失敗並不是統一由控制器兜底判斷。很多失敗、超時或取消是在節點內部生成，再沿連線繼續傳遞。尤其是多輸入節點，失敗傳播行為要看具體節點實現，而不是隻看控制器表面狀態。

## 擴充套件時通常落在哪

### 新節點

如果是新增流程節點，重點通常在 `FlowEngineLib` 的節點實現本身，以及它如何接入開始、結束或服務節點鏈。

### 新模板型流程

如果是新增一類可編輯流程模板，重點通常在 `TemplateFlow` 相鄰的模板管理、匯入匯出和編輯視窗接入。

### 節點屬性面板擴充套件

如果是新增流程節點配置 UI，通常會落到 `STNodeEditorHelper` 或 `NodeConfigurator` 一帶，而不是隻改節點類本身。

## 推薦閱讀順序

推薦按這條線讀：

1. `Engine/FlowEngineLib/FlowEngineControl.cs`
2. `Engine/FlowEngineLib/Start/BaseStartNode.cs`
3. `Engine/FlowEngineLib/End/CVEndNode.cs`
4. `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
6. `Engine/ColorVision.Engine/Templates/Flow/FlowEngineToolWindow.xaml.cs`
7. `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`

這樣能先建立執行主鏈，再回到編輯和主程式整合。

## 這頁不再做什麼

本頁不再繼續維護這些高風險內容：

- 把 FlowEngineLib 描述成與當前實現脫節的標準分層框架
- 用一組抽象設計模式覆蓋所有節點行為
- 把 MQTT、日誌、序列化等周邊都包裝成獨立基礎設施層承諾

如果後續要討論重構方向，應以具體執行鏈和實際節點體系為起點。

## 繼續閱讀

- [元件互動](../../overview/component-interactions.md)
- [架構執行時](../../overview/runtime.md)
- [Templates 架構設計](../templates/design.md)
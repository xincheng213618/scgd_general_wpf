# FlowEngineLib

本頁只描述當前倉庫裡真實可用的 FlowEngineLib 實現，不再繼續維護“類圖 + 理想化資料流 + 偽 API 表”式舊稿。

## 先看這個模組現在是什麼

按當前原始碼狀態，FlowEngineLib 不是一個抽象的流程設計概念，而是一套直接建立在節點編輯器之上的執行時執行核心。它當前至少承擔四類事情：

- 承載流程畫布與節點物件。
- 管理開始節點、服務節點和已載入畫布。
- 把節點加入 `FlowNodeManager` 的裝置檢視。
- 在開始節點與結束節點之間閉合整條流程的完成事件。

因此它更接近“節點執行核心”，而不是舊文件裡那種獨立於宿主存在的通用 DSL 平台。

## 當前最關鍵的檔案

- `Engine/FlowEngineLib/FlowEngineControl.cs`
- `Engine/FlowEngineLib/CVFlowContainer.cs`
- `Engine/FlowEngineLib/Base/CVCommonNode.cs`
- `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`
- `Engine/FlowEngineLib/Start/BaseStartNode.cs`
- `Engine/FlowEngineLib/End/CVEndNode.cs`
- `Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs`
- `Engine/FlowEngineLib/Base/CVStartCFC.cs`

如果只是想弄清流程如何載入、啟動、轉發和結束，這幾處程式碼已經覆蓋主鏈路。

## 當前控制面怎麼分層

### 流程控制器

`FlowEngineControl` 是當前最核心的執行時控制器。按實現看，它負責：

- 掛接 `STNodeEditor`
- 跟蹤開始節點字典 `startNodeNames`
- 跟蹤服務節點字典 `services`
- 快取已載入畫布 `loadedCanvas`
- 觸發流程完成事件 `Finished`

節點進入編輯器後，`FlowEngineControl` 會在 `NodeAdded` 事件裡把它們分成兩類處理：

- `BaseStartNode` 進入開始節點字典，並訂閱 `Finished`
- `CVBaseServerNode` 進入服務節點集合，並同步到 `FlowNodeManager`

這比舊文件裡“載入圖後直接執行”那種描述更貼近真實實現。

### 多流程容器

`CVFlowContainer` 是和 `FlowEngineControl` 相鄰的另一條控制線。它保留了：

- 多個開始節點的對映
- `startNodesFlowMap`
- append / load / start 組合能力

這說明 FlowEngineLib 當前不只服務於單張固定畫布，也考慮了按 key 追加和啟動流程的場景。

## 節點體系當前實際長什麼樣

### `CVCommonNode`

這是所有核心節點的共同基類，當前提供：

- `NodeName`
- `NodeType`
- `DeviceCode`
- `NodeID`
- `ZIndex`
- `nodeEvent`
- `nodeRunEvent`
- `nodeEndEvent`

另外它還統一了控制元件建立輔助方法，並在 `OnOwnerChanged()` 時向節點編輯器註冊型別顏色。

### `BaseStartNode`

開始節點當前負責：

- 建立 `OUT_START` 與多個 `OUT_LOOP` 輸出
- 維護 `Ready`、`Running` 和 `startActions`
- 把 `CVStartCFC` 分發到第一批連線節點
- 在流程完成後丟擲 `Finished`

所以流程“開始”不是外部控制器單獨完成的，而是落實在開始節點內部。

### `CVBaseServerNode`

這是當前最常見的執行節點基類。按實現看，它負責：

- 建立 `IN` / `OUT` 等節點埠
- 維護模板 ID、模板名、圖片檔名、Token 和超時配置
- 組裝基礎請求資料
- 接收服務端響應並沿流程繼續傳遞

舊文件裡一直出現的 `DoServerWork` 並不是當前應被強調的擴充套件面；現在更真實的關注點是 `OnCreate()`、請求參數建置、響應處理和重置邏輯。

### `CVEndNode`

結束節點當前做的事情非常明確：

- 接收 `CVStartCFC` 或迴圈下一步輸入
- 呼叫 `startAction.DoFinishing()`
- 最後呼叫 `startAction.FireFinished()`

這才是整條流程 finished 的真正閉環位置。

### `AlgorithmNode`

`AlgorithmNode` 是理解服務節點的一個很典型樣本。它當前會：

- 維護運算元型別、模板、POI 模板、顏色和快取長度
- 在 `OnCreate()` 中建立節點內編輯控制元件
- 在 `getBaseEventData(...)` 中把模板、影像、顏色和 SMU 資料打包成演算法請求參數

這再次說明 FlowEngineLib 當前節點的核心工作是“建置和轉發執行參數”，而不是在節點裡本地跑完整演算法。

## 流程完成鏈當前怎麼閉合

`CVStartCFC` 當前是整條流程狀態在節點間傳遞的關鍵物件。它會記錄：

- 起止時間
- 流程狀態
- 串號
- 資料字典
- 對應的開始節點

流程結束時，`CVEndNode` 呼叫 `DoFinishing()` 和 `FireFinished()`，再回到 `BaseStartNode` 的 `Finished` 事件，最後由 `FlowEngineControl` 對外丟擲 `FlowEngineEventArgs`。

這條鏈如果不連起來看，就很容易把“節點結束”和“流程結束”混成同一件事。

## 當前和宿主程式碼的邊界

FlowEngineLib 本身只負責節點執行核心。真正把它接進 ColorVision 主程式的是 `Engine/ColorVision.Engine/Templates/Flow/` 那一層，例如：

- `FlowEngineManager.cs`
- `DisplayFlow.xaml.cs`
- `TemplateFlow.cs`

那裡才負責：

- 結合 MQTT RC 服務令牌重新整理流程畫布
- 把流程模板從 Base64 載入進控制器
- 在 UI 裡選擇、編輯和執行流程

因此如果只讀 FlowEngineLib 而不看模板層，會知道“怎麼跑”，但不知道“誰在主程式裡觸發它跑”。

## 當前幾個最容易寫錯的點

### 它不是宿主級完整工作流系統的全部程式碼

FlowEngineLib 只實現節點執行核心。進入主程式後的模板管理、視窗互動和資料載入，仍然在 `ColorVision.Engine/Templates/Flow/` 那一層。

### “節點完成”不等於“流程完成”

當前真正讓流程完成落地的是 `CVEndNode -> CVStartCFC.FireFinished() -> BaseStartNode.Finished -> FlowEngineControl.Finished` 這條鏈，而不是任意一個節點發出 `nodeEndEvent`。

### 服務節點擴充套件點不要再圍繞舊稿寫法理解

當前真實擴充套件路徑更接近：

- `OnCreate()`
- 參數組裝
- 響應處理
- `Reset()`

繼續照舊文件去找統一的“本地執行業務函式”，會把節點模型理解偏。

### `loadedCanvas` 不是裝飾快取

`FlowEngineControl` 和 `CVFlowContainer` 都會用畫布內容雜湊避免重複載入。這個細節會影響你對“為什麼同一份流程不會重複重建”的理解。

## 推薦閱讀順序

1. `Engine/FlowEngineLib/FlowEngineControl.cs`
2. `Engine/FlowEngineLib/Base/CVCommonNode.cs`
3. `Engine/FlowEngineLib/Start/BaseStartNode.cs`
4. `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`
5. `Engine/FlowEngineLib/End/CVEndNode.cs`
6. `Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs`
7. `Engine/FlowEngineLib/Base/CVStartCFC.cs`
8. `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`

這樣能先建立核心認知，再把它和宿主側 UI 觸發鏈連起來。

## 繼續閱讀

- [docs/04-api-reference/extensions/flow-node.md](../extensions/flow-node.md)
- [docs/03-architecture/components/engine/flow-engine.md](../../03-architecture/components/engine/flow-engine.md)
- [docs/04-api-reference/engine-components/ColorVision.Engine.md](./ColorVision.Engine.md)
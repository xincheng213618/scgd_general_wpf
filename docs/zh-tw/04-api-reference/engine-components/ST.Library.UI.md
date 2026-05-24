# ST.Library.UI

本頁只描述當前倉庫裡真實可用的 `ST.Library.UI` 模組，不再繼續維護“完整 UI 平台手冊 + 海量示例 + 統一擴充套件框架”式舊稿。

## 先看這個模組現在是什麼

按當前原始碼狀態，`ST.Library.UI` 是一套偏底層的 WinForms 節點編輯器庫。它當前最明確的角色不是獨立應用殼，而是為 Flow 相關功能提供：

- 節點畫布與互動編輯器
- 節點基類與埠連線模型
- 屬性編輯面板
- 節點樹與節點面板組合控制元件

因此它更接近“節點編輯器基礎設施”，而不是 ColorVision 業務層本身。

## 當前最關鍵的檔案

- `Engine/ST.Library.UI/NodeEditor/STNodeEditor.cs`
- `Engine/ST.Library.UI/NodeEditor/STNode.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodeOption.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodePropertyGrid.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodeTreeView.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodeEditorPannel.cs`
- `Engine/ST.Library.UI/FrmSTNodePropertyInput.cs`

如果只是想弄清這個庫在當前倉庫裡真正做什麼，這幾處檔案已經覆蓋主體。

## 當前控制面怎麼分塊

### 畫布控制元件

`STNodeEditor` 是整個庫的中心控制元件。按當前實現看，它負責：

- 維護 `Nodes`
- 維護畫布偏移與縮放
- 管理節點選中、懸停、活動態
- 處理節點連線、斷線與畫布互動
- 觸發節點和畫布相關事件

這說明當前節點編輯器的控制邏輯都集中在一個 WinForms `Control` 裡，而不是拆成一堆獨立 MVVM 服務。

### 節點物件模型

`STNode` 是當前所有節點的共同基類，負責：

- 標題、尺寸、位置
- 輸入輸出選項集合
- 節點內嵌控制元件集合
- 選中態與活動態
- 自動尺寸與重繪

而 `STNodeOption` 則承擔埠模型，當前提供：

- 埠文字與資料型別
- 單連線/多連線限制
- 連線數量和已連埠集合
- 連線、斷開和資料傳遞事件

因此這個庫的基礎心智模型並不是“節點只是一張圖”，而是“節點 + 埠 + 控制元件 + 事件”的組合物件。

### 屬性面板

`STNodePropertyGrid` 當前是一個專門為節點屬性設計的控制元件，不是直接複用 .NET 標準 PropertyGrid。它會圍繞當前 `STNode`：

- 讀取屬性描述符
- 渲染項、描述和錯誤區
- 根據節點標題色或自訂顏色做高亮
- 處理只讀與編輯態切換

`FrmSTNodePropertyInput` 則是與之配套的輕量輸入窗體，用來編輯單個屬性值。

### 節點樹與組合面板

`STNodeTreeView` 當前負責：

- 組織節點型別樹
- 維護搜尋與分組顯示
- 和編輯器、屬性面板聯動

`STNodeEditorPannel` 則把：

- `STNodeEditor`
- `STNodeTreeView`
- `STNodePropertyGrid`

組合成一個可直接使用的整體面板，並補上分割線、縮放提示和連線狀態提示。

這說明 `ST.Library.UI` 當前並不只是單個編輯器控制元件，還提供了比較完整的一套組合宿主面板。

## 當前和 ColorVision 的關係

在這個倉庫裡，`ST.Library.UI` 更多被 `FlowEngineLib` 及其宿主層當成基礎設施使用。當前業務層通常會：

- 繼承 `STNode` 做自己的節點型別
- 把 `STNodeEditor` 作為流程畫布
- 借 `STNodePropertyGrid` 暴露節點屬性
- 借 `STNodeTreeView` 管理節點分類與拖放建立

所以文件不應該把它寫成和業務同一層的“流程系統”，它是流程系統下面的 UI 基礎庫。

## 當前幾個最容易寫錯的點

### 它是 WinForms 庫，不是 WPF 流程框架

雖然上層主程式大量使用 WPF，但 `ST.Library.UI` 當前核心控制元件仍然是 WinForms `Control`。這個邊界對理解宿主嵌入方式很重要。

### 這套庫不只提供一個編輯器控制元件

除了 `STNodeEditor`，當前還有節點物件模型、埠模型、屬性網格、節點樹和組合面板。把它縮寫成“一個畫布控制元件”會低估實際範圍。

### 屬性編輯是自訂實現，不是直接用系統 PropertyGrid

`STNodePropertyGrid` 和 `FrmSTNodePropertyInput` 是庫內自己的節點屬性編輯鏈。繼續照舊文件把它描述成通用反射面板，會模糊掉當前專用實現。

### 它主要被上層節點系統消費

當前真實用法是上層定義節點型別後交給這裡的編輯器、樹和屬性面板承載，而不是在 `ST.Library.UI` 裡直接寫業務節點邏輯。

## 推薦閱讀順序

1. `Engine/ST.Library.UI/NodeEditor/STNodeEditor.cs`
2. `Engine/ST.Library.UI/NodeEditor/STNode.cs`
3. `Engine/ST.Library.UI/NodeEditor/STNodeOption.cs`
4. `Engine/ST.Library.UI/NodeEditor/STNodePropertyGrid.cs`
5. `Engine/ST.Library.UI/NodeEditor/STNodeTreeView.cs`
6. `Engine/ST.Library.UI/NodeEditor/STNodeEditorPannel.cs`

這樣能先建立畫布和節點模型，再理解屬性面板與節點庫是怎樣掛上來的。

## 繼續閱讀

- [docs/04-api-reference/engine-components/FlowEngineLib.md](./FlowEngineLib.md)
- [docs/04-api-reference/extensions/flow-node.md](../extensions/flow-node.md)
- [docs/03-architecture/components/engine/flow-engine.md](../../03-architecture/components/engine/flow-engine.md)
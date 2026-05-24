# 流程引擎

本頁只描述當前倉庫裡 `Engine/ColorVision.Engine/Templates/Flow` 這一層的真實職責，不再繼續維護“把整個 Flow 執行核心、宿主橋接、節點庫都混成一頁”的舊稿。

## 先看這頁現在講什麼

當前這頁講的不是 `FlowEngineLib` 執行核心本身，而是主程式裡圍繞流程模板的宿主層，重點包括：

- 流程模板怎樣從資料庫和資源表載入。
- 雙擊流程模板後怎樣開啟編輯視窗。
- 編輯視窗怎樣託管 `STNodeEditor`、屬性面板和節點樹。
- 宿主層怎樣把裝置、模板和節點配置器掛到流程編輯器裡。

如果要看節點執行語義和節點基類，請轉到 [FlowEngineLib](../../engine-components/FlowEngineLib.md)。

## 當前最關鍵的檔案

- `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
- `Engine/ColorVision.Engine/Templates/Flow/FlowEngineToolWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Flow/STNodeEditorHelper.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/*.cs`

這幾處程式碼共同決定了主程式裡的流程模板是如何編輯、儲存和配置的。

## 當前主鏈怎麼跑

### 流程模板入口

`MenuTemplateFlow` 會開啟 `TemplateEditorWindow(new TemplateFlow())`。`TemplateFlow` 本身是 `ITemplate<FlowParam>` 的一個具體實現，當前負責：

- 從 MySQL 讀取流程模板主表
- 把節點圖內容從 `SysResourceModel.Value` 取回成 Base64
- 將其包裝進 `FlowParam`
- 管理儲存、刪除、匯入、匯出和新建

因此當前流程模板不是單純的磁碟檔案列表，而是“資料庫主記錄 + 資源表二進位制內容”的組合。

### 雙擊後的編輯視窗

`TemplateFlow.PreviewMouseDoubleClick(...)` 會直接開啟 `FlowEngineToolWindow`。這說明流程模板和很多普通模板不同：

- 列表視窗只是入口
- 真正的流程編輯發生在獨立視窗裡

視窗裡再透過 `STNodeEditorHelper` 託管節點畫布、屬性面板、節點樹、剪貼簿和右鍵選單。

### 編輯器輔助層

`STNodeEditorHelper` 當前負責的事情很多，遠不止“幫忙調一下節點樹”：

- 節點複製和貼上的壓縮序列化
- 當前選中節點與屬性面板同步
- 節點樹初始化和裝配
- 右鍵選單、刪除、全選等命令
- 合法性檢查和自動佈局
- 裝置與模板選擇面板的宿主掛接

這意味著流程編輯視窗的大量互動邏輯都集中在這個 helper 裡，而不是散落在每個節點控制元件中。

### 節點配置器橋接

`NodeConfigurator` 目錄當前是主程式和節點庫之間的重要橋接層。這裡會把：

- 裝置服務列表
- 本地影像路徑輸入
- 普通模板選擇器
- JSON 模板選擇器

裝進節點屬性面板。

例如 POI 相關配置器會把 `TemplatePoi`、`TemplatePoiFilterParam`、`TemplatePoiReviseParam`、`TemplatePoiOutputParam` 等模板接回流程節點。也就是說，節點在宿主裡的可編輯體驗，並不完全由 `FlowEngineLib` 決定。

## 當前儲存與匯出邊界

### 主儲存仍是資料庫

`TemplateFlow.Load()` 和 `Save2DB(...)` 當前都圍繞 MySQL 主表、明細表以及 `SysResourceModel` 展開。Base64 節點圖內容會落到資源表，再透過明細記錄關聯回來。

### 匯出不只是一種格式

當前流程模板匯出至少有兩種實際形式：

- `.stn`：節點圖原始檔案
- `.cvflow`：帶關聯模板資訊的流程包

因此把流程模板簡單寫成“只是一張節點圖檔案”，會漏掉當前包匯出的能力。

## 當前幾個最容易寫錯的點

### 這頁不是 FlowEngineLib 重複頁

`FlowEngineLib` 負責節點執行與基類體系；本頁這層負責主程式裡的模板管理、視窗編輯和宿主橋接。兩層都叫“流程引擎”，但邊界不同。

### 流程模板不是純磁碟資產

當前主路徑仍然是資料庫 + 資源表，不是掃描某個目錄裡的 `.stn` 檔案。匯入匯出只是附加能力。

### 節點屬性編輯大量依賴宿主程式碼

真正把裝置下拉框、模板下拉框、JSON 模板下拉框掛進節點屬性區的，是 `NodeConfigurator` 和 `STNodeEditorHelper` 這一層，而不只是節點類本身。

### 視窗行為和一般模板編輯器不同

普通模板多半在 `TemplateEditorWindow` 右側編輯；流程模板當前走的是“列表視窗 + 獨立流程編輯器視窗”的路徑。繼續沿用一般模板的敘述會誤導讀者。

## 推薦閱讀順序

1. `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
2. `Engine/ColorVision.Engine/Templates/Flow/FlowEngineToolWindow.xaml.cs`
3. `Engine/ColorVision.Engine/Templates/Flow/STNodeEditorHelper.cs`
4. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator` 下其它配置器

## 繼續閱讀

- [FlowEngineLib](../../engine-components/FlowEngineLib.md)
- [Flow 節點擴充套件](../../extensions/flow-node.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)
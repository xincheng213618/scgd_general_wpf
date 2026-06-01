# 演算法系統概覽

本頁只描述當前倉庫裡實際在跑的模板與演算法接入鏈，不再繼續維護“演算法分類百科 + 示例程式碼 + GPU 能力總論”式舊稿。

## 先看這套系統真正落在哪

當前和“演算法”最直接相關的程式碼，並不只在一個目錄裡：

- `Engine/ColorVision.Engine/Templates/`：模板定義、模板管理、模板編輯和大部分業務演算法 UI 接入點。
- `Engine/FlowEngineLib/`：流程節點、開始/結束鏈和執行控制。
- `Engine/ColorVision.Engine/Services/Devices/Algorithm/`：演算法裝置服務接入面。
- `Engine/cvColorVision/` 與更底層原生庫：承接部分真正的底層計算與互操作。

因此如果只把這章理解成“託管演算法函式目錄”，會直接偏離當前實現。

## 當前主鏈是怎麼串起來的

從現狀看，演算法/模板最常見的執行鏈大致是：

1. `TemplateContorl` 掃描已載入程式集中的 `IITemplateLoad` 實現，並把模板註冊進系統。
2. `TemplateManagerWindow` 和 `TemplateEditorWindow` 負責讓使用者瀏覽、建立、編輯模板。
3. 具體業務演算法的 UI 類通常繼承 `DisplayAlgorithmBase`，並暴露 `OpenTemplateCommand` 一類入口。
4. 這些演算法 UI 在 `SendCommand(...)` 中組裝 `CVTemplateParam`、檔案路徑、裝置資訊等參數。
5. 參數再透過 `MQTTAlgorithm` 或相鄰服務鏈發給真正執行端。
6. 如果是流程模板，則會進入 `TemplateFlow` + `FlowEngineToolWindow` + `FlowEngineLib` 這一條執行鏈。

這意味著：很多你在 `Templates/*/Algorithm*.cs` 裡看到的類，當前職責更接近“演算法前端介面卡”，而不是最終運算元本身。

## 當前模板系統裡最重要的幾塊

### 模板註冊與管理

這部分核心關注點在：

- `ITemplate.cs`
- `TemplateContorl.cs`
- `TemplateManagerWindow.xaml(.cs)`
- `TemplateEditorWindow.xaml(.cs)`

它們決定模板怎麼出現、怎麼開啟、怎麼進入編輯流程。

### Flow 模板

`Templates/Flow/` 不是普通參數模板的簡單分支，而是把流程圖、流程編輯視窗、匯入匯出和批次執行接到一起的特殊模板族。

當前關鍵入口包括：

- `TemplateFlow.cs`
- `FlowEngineToolWindow.xaml(.cs)`
- `DisplayFlow.xaml(.cs)`

### JSON 模板

`Templates/Jsons/` 當前承接了一批以 JSON 配置為核心的模板實現。它的共同鏈路主要是：

- `ITemplateJson<T>`：裝載、儲存、匯入匯出公共邏輯。
- `TemplateJsonParam`：JSON 模板參數基礎型別。
- `EditTemplateJson.xaml(.cs)`：雙模式編輯控制元件，支援文字編輯和屬性編輯切換。

這也是為什麼你會在模板系統裡同時看到傳統參數物件和 JSON 文字編輯器兩種形態。

### 業務模板族

當前仍然能直接看出的主要模板族包括：

- `POI/`
- `ARVR/`
- `JND/`
- `LedCheck/`
- `Compliance/`
- `Jsons/` 下的多個業務模板實現

這些目錄並不是同一時期按同一規則設計出來的，閱讀時不要預設它們一定擁有完全一致的抽象層級。

## 當前幾個最容易誤讀的點

### 誤區 1：把 `Algorithm*.cs` 當成最終演算法實現

很多這類類當前主要做的是：

- 開啟模板編輯視窗
- 維護 UI 側選擇狀態
- 組裝訊息參數
- 呼叫 `PublishAsyncClient(...)`

真正的底層處理經常在裝置服務端、MQTT 對端、原生庫或其他鏈路上完成。

### 誤區 2：認為 `POI` 只是一個獨立小專題

從當前程式碼看，POI 仍然是多個 ARVR/定位/分析類演算法共享的上游模板依賴。它的模板與點位資料會被多個演算法 UI 重複引用。

### 誤區 3：把 Flow 模板排除在模板系統之外

Flow 模板只是表現形式更複雜，但它仍然透過 Templates 系統進入主程式，並由相鄰視窗和流程庫接管後續執行。

### 誤區 4：以為 JSON 模板只是“臨時相容層”

當前 `Jsons/` 目錄和 `ITemplateJson<T>` 仍然是實際在用的主路徑之一，不應被寫成已經被強型別模板完全取代。

## 推薦閱讀順序

1. `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
2. `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
3. `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
5. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
6. `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
7. 具體業務演算法目錄，如 `POI/`、`ARVR/`、`Jsons/` 下各 `Algorithm*.cs`

## 繼續閱讀

- [演算法與模板概覽](./README.md)
- [Templates 模組分析](../../03-architecture/components/templates/analysis.md)
- [FlowEngineLib 架構](../../03-architecture/components/engine/flow-engine.md)

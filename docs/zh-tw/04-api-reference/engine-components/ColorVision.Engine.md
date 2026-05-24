# ColorVision.Engine

本頁只描述當前倉庫裡真實可用的 `ColorVision.Engine` 模組，不再繼續維護“完整 API 表 + 統一分層藍圖 + 偽示例”式舊稿。

## 先看這個模組現在是什麼

按當前原始碼狀態，`ColorVision.Engine` 不是一個單純的演算法庫，而是 ColorVision 主程式最核心的引擎拼裝層。它當前至少負責：

- 裝置與服務物件的宿主側抽象。
- 模板系統的載入、編輯和持久化。
- MQTT 請求、心跳和訊息記錄。
- FlowEngineLib 在主程式中的 UI 與模板橋接。
- 演算法顯示層與模板編輯器之間的連線。

因此它更接近“執行時引擎宿主層”，而不是把所有業務都直接算在本地的單體模組。

## 當前最關鍵的檔案

- `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Flow/FlowEngineManager.cs`
- `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`
- `Engine/ColorVision.Engine/Services/DeviceService.cs`
- `Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs`
- `Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs`
- `Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs`
- `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`

如果只是想弄清主引擎怎麼組織模板、裝置、訊息鏈和流程，這些程式碼已經覆蓋主幹。

## 當前控制面怎麼分塊

### 模板載入與模板註冊

`TemplateControl` 是當前模板體系的總入口。它會在 MySQL 可用後掃描所有程式集裡的 `IITemplateLoad` 實現並執行 `Load()`，再把模板例項註冊到 `ITemplateNames`。

這意味著模板系統當前不是手寫靜態列表，而是靠：

- 初始化器觸發
- 程式集掃描
- 模板例項登錄檔

三步串起來的。

### JSON 模板編輯

`ITemplateJson<T>` 展示了當前 JSON 模板的真實落點：

- 模板資料從 MySQL 讀取
- 模板物件透過 `Activator.CreateInstance` 包裝成參數物件
- 儲存與刪除也直接回寫資料庫

對應的編輯器 `EditTemplateJson` 則提供：

- 文字模式
- 屬性編輯模式
- 註釋檢視切換
- 外部 JSON 校驗網站快捷入口

這說明引擎層當前並不只是存模板，還直接承載了模板編輯 UI 的一部分。

### 流程橋接層

`FlowEngineManager` 和 `DisplayFlow` 是 `ColorVision.Engine` 與 `FlowEngineLib` 的橋接面。它們當前負責：

- 初始化 Flow 的 MQTT 預設配置
- 維護流程模板列表和當前選擇
- 用 Base64 資料把模板載入進 `FlowEngineControl`
- 結合 `MqttRCService` 的 service token 重新整理可用服務節點
- 提供流程編輯、模板編輯、批次記錄檢視等 UI 操作

所以主程式裡的流程功能不是由 `FlowEngineLib` 單獨完成的，而是要經過這層橋接程式碼才能真正進入視窗和模板體系。

### 裝置與服務抽象

`DeviceService` 是當前宿主側裝置物件的基礎抽象，負責：

- 樹節點行為
- 圖示與上下文選單
- 匯入匯出配置
- 重置、重啟和屬性命令
- 與 MQTT 服務物件或顯示控制元件的掛接

而 `DeviceServiceFactoryRegistry` 則把 Camera、PG、Spectrum、SMU、Sensor 等服務型別統一註冊成工廠。

這說明當前裝置例項化已經不再是 scattered switch-case，而是中心化工廠註冊。

### MQTT 執行時

`MQTTServiceBase` 是當前訊息鏈最重要的宿主基類。它負責：

- 訂閱/釋出 MQTT 訊息
- 維護 `MsgRecord`
- 基於心跳判斷 `IsAlive`
- 處理超時和回包狀態

`MqttRCService` 則進一步承擔註冊中心客戶端角色，負責：

- RC 主題建置
- 重新註冊
- 服務令牌快取
- RC 連線狀態

引擎層很多“服務是否線上、流程能否重新整理、裝置 token 從哪裡來”的問題，最終都要回到這層。

## 演算法在這一層當前扮演什麼角色

從 `AlgorithmPOI` 和 `AlgorithmMTF` 這些實現看，`ColorVision.Engine` 裡的演算法類當前更多是：

- 開啟模板編輯器
- 組織模板選擇狀態
- 組裝 MQTT 參數
- 呼叫裝置服務釋出命令

也就是說，這一層的演算法物件通常是“顯示和命令介面卡”，而不是直接在本地完成影像計算的純演算法核心。

## 當前幾個最容易寫錯的點

### 它不是“所有演算法都在本地執行”的模組

當前很多演算法類做的其實是把模板、檔名和裝置資訊組裝成 MQTT 請求，再交給裝置或服務端處理。繼續把這層寫成純本地演算法實現，會和真實控制鏈不符。

### 模板系統離不開初始化和資料庫

`TemplateControl` 依賴 MySQL 初始化之後的程式集掃描；`ITemplateJson<T>` 也直接和資料庫互動。把它寫成“完全本地靜態模板集”會丟失關鍵前提。

### 流程功能並不全在 FlowEngineLib

主程式裡真正能編輯、選擇和執行 Flow 模板，還需要 `Templates/Flow/` 這層橋接程式碼。只描述 FlowEngineLib，會漏掉宿主側的實際控制面。

### 裝置服務例項化當前是註冊中心化的

`DeviceServiceFactoryRegistry` 已經是當前真實的例項化入口。繼續沿用舊文件裡的分散構造描述，會把擴充套件點寫偏。

## 推薦閱讀順序

1. `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
2. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
3. `Engine/ColorVision.Engine/Services/DeviceService.cs`
4. `Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs`
5. `Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs`
6. `Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs`
7. `Engine/ColorVision.Engine/Templates/Flow/FlowEngineManager.cs`
8. `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`

這樣能先看到模板與服務宿主層，再連線訊息鏈和流程橋接層。

## 繼續閱讀

- [docs/04-api-reference/engine-components/FlowEngineLib.md](./FlowEngineLib.md)
- [docs/03-architecture/components/templates/analysis.md](../../03-architecture/components/templates/analysis.md)
- [docs/04-api-reference/algorithms/overview.md](../algorithms/overview.md)
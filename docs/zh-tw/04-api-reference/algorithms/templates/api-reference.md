# Templates API 參考

本頁只保留當前原始碼裡比較穩定的模板宿主入口，不再試圖維護“完整簽名手冊”。原因很直接：很多模板行為依賴具體子類覆寫、資料庫狀態和使用者控制元件掛接，舊式 API 表很容易漂移。

## 先看哪些入口最值得認識

按當前程式碼，模板系統裡最穩定、最值得優先理解的是這幾類型別：

- `ITemplate`
- `ITemplate<T>`
- `ITemplateJson<T>`
- `TemplateControl` / `IITemplateLoad`
- `ParamBase` / `ModelBase` / `ParamModBase`
- `TemplateModel<T>`
- `TemplateEditorWindow` / `TemplateCreate`

這頁的重點是說明這些入口在當前實現裡分別承擔什麼職責。

## 核心宿主型別

### ITemplate

`ITemplate` 是所有模板的宿主基類。當前最重要的職責包括：

- 在構造時把自己註冊到 `TemplateControl.ITemplateNames`
- 提供 `Load()`、`Save()`、`Import()`、`Export()`、`Delete()`、`Create()` 等生命週期鉤子
- 暴露 `ItemsSource`、`Count`、`GetValue(...)`、`GetParamValue(...)`
- 控制宿主視窗行為，如 `IsSideHide`、`IsUserControl`
- 為建立視窗提供 `HasCreateTemplateSource`、`ImportName`、`CreateDefault()` 等來源能力

需要特別注意的是：`ITemplate` 當前是一個具體基類，不只是介面定義。

### `ITemplate<T>`

`ITemplate<T>` 是普通參數模板最常見的泛型基類，其中 `T : ParamModBase, new()`。它當前主要把：

- `ObservableCollection<TemplateModel<T>> TemplateParams`
- `ItemsSource`
- `Count`
- `GetTemplateNames()`
- `GetTemplateIndex(...)`
- `GetParamValue(...)`

這些常規列表行為統一起來。

此外，它還負責根據 `TemplateDicId` 從字典模板生成預設參數物件，所以這層並不只是一個簡單集合包裝器。

### `ITemplateJson<T>`

`ITemplateJson<T>` 是 JSON 模板分支的宿主基類，其中 `T : TemplateJsonParam, new()`。它和 `ITemplate<T>` 的主要差異在於：

- 資料來源是 `ModMasterModel.JsonVal`
- 建立預設值時走 `SysDictionaryModModel.JsonVal`
- 匯入匯出圍繞 `.cfg` 和 ZIP
- 複製邏輯基於 JSON 序列化副本

如果模板內容本質是 JSON 文字，這層通常比 `ITemplate<T>` 更接近真實實現。

## 註冊與發現入口

### TemplateControl

`TemplateControl` 是當前模板登錄檔。它主要維護：

- `ITemplateNames`
- `AddITemplateInstance(...)`
- `ExitsTemplateName(...)`
- `FindDuplicateTemplate(...)`

並在初始化時掃描所有 `IITemplateLoad` 實現，以便讓具體模板型別自己裝載內容。

### IITemplateLoad

`IITemplateLoad` 是模板載入擴充套件點。當前很多模板類都會實現它，以便在 `TemplateControl.Init()` 掃描時執行自己的 `Load()`。

這也是當前模板系統和應用啟動順序耦合的重要原因之一。

## 參數與模型基類

### ParamBase

`ParamBase` 是最薄的一層，只提供：

- `Id`
- `Name`

它適合做所有模板參數物件的共同父類。

### ModelBase

`ModelBase` 在當前實現裡的價值比名字更具體。它會把 `ModDetailModel` 列表對映成按符號名索引的參數字典，並提供：

- `GetValue<T>(...)`
- `SetProperty(...)`
- `GetParameter(...)`
- `GetDetail(...)`
- `StringToDoubleArray(...)`
- `DoubleArrayToString(...)`

也就是說，很多模板參數屬性之所以能像普通 C# 屬性一樣寫，底層其實是這層在做字典對映和型別轉換。

### ParamModBase

`ParamModBase` 繼續往上，把模板主記錄和參數細節記錄組合起來，是大多數資料庫驅動模板參數物件的直接基類。

## UI 宿主相關型別

### `TemplateModel<T>`

`TemplateModel<T>` 是當前列表項包裝物件。除了 `Value` 之外，它還承擔：

- `Key`
- `IsSelected`
- `IsEditMode`
- 右鍵選單
- 重新命名和複製名稱命令

因此列表裡使用者看到的“模板項”並不是裸參數物件，而是這層帶 UI 狀態的包裝。

### TemplateEditorWindow

`TemplateEditorWindow` 是最通用的模板編輯宿主。它會根據模板是否為 `IsUserControl` 決定右側顯示：

- `PropertyGrid`
- 模板自訂 `UserControl`

同時統一接管新建、複製、儲存、刪除、重新命名、搜尋、排序和選中切換。

### TemplateCreate

`TemplateCreate` 當前負責模板建立來源選擇。除了預設模板，它還支援：

- 當前副本
- SQLite 樣例庫中的樣例

所以它已經不是一個只負責輸入模板名稱的小彈窗。

## 當前幾個最容易寫錯的點

### `ITemplate` 不是純介面

當前很多預設行為直接寫在 `ITemplate` 這個基類裡，包括註冊、建立視窗和多種生命週期方法。把它寫成純抽象契約會誤導讀者。

### 很多行為只有在具體模板覆寫後才成立

例如 `Import()`、`Export()`、`CreateDefault()`、`GetUserControl()` 等方法，在基類裡未必有完整實現。不能把基類方法表直接當成“所有模板都完全支援的功能清單”。

### 資料模型和 UI 模型是混合的

`TemplateModel<T>`、`TemplateEditorWindow`、`TemplateCreate` 這些型別說明當前模板系統並沒有把 UI 狀態完全剝離出去。API 解釋時必須保留這個現實邊界。

### JSON 模板和普通參數模板是兩條宿主分支

雖然它們都歸在 Templates 下，但 `ITemplate<T>` 與 `ITemplateJson<T>` 的預設持久化、建立和匯入匯出路徑並不相同。

## 推薦閱讀順序

1. `Engine/ColorVision.Engine/Templates/ITemplate.cs`
2. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
3. `Engine/ColorVision.Engine/Templates/ModelBase.cs`
4. `Engine/ColorVision.Engine/Templates/ParamModBase.cs`
5. `Engine/ColorVision.Engine/Templates/TemplateModel.cs`
6. `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
7. `Engine/ColorVision.Engine/Templates/TemplateCreate.xaml.cs`

## 繼續閱讀

- [模板管理](./template-management.md)
- [JSON 模板](./json-templates.md)
- [流程引擎](./flow-engine.md)
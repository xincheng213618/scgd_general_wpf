# 模板管理

本頁只描述當前倉庫裡真實可用的模板宿主鏈，不再繼續維護“統一框架藍圖 + 理想化 MVVM 分層 + 大段偽示例”式舊稿。

## 先看這頁現在在講什麼

按當前原始碼狀態，模板管理不是單獨一個後端服務，而是一條由 `ITemplate` 基類、全域性登錄檔、管理視窗、編輯視窗和建立視窗拼起來的宿主鏈。它當前負責：

- 啟動後掃描並註冊具體模板型別。
- 在主程式裡按名稱空間組織模板入口。
- 提供通用的編輯、建立、匯入匯出、複製和重新命名視窗。
- 讓 JSON 模板、流程模板、POI 模板、字典模板等共用一套宿主介面。
- 提供 SQLite 樣例庫和全域性搜尋接入。

所以這頁真正要講的，不是“模板理論”，而是主程式現在怎樣託管各類别範本。

## 當前最關鍵的檔案

- `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
- `Engine/ColorVision.Engine/Templates/ITemplate.cs`
- `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/TemplateCreate.xaml.cs`
- `Engine/ColorVision.Engine/Templates/TemplateSearchProvider.cs`
- `Engine/ColorVision.Engine/Templates/TemplateSampleLibrary.cs`
- `Engine/ColorVision.Engine/Templates/TemplateSampleSaveWindow.xaml.cs`

如果只讀這幾處，已經足夠建立當前模板系統的主心智模型。

## 當前主鏈怎麼跑

### 初始化與註冊

`TemplateInitializer` 啟動後會觸發 `TemplateControl.GetInstance()`；`TemplateControl` 再掃描程式集裡所有 `IITemplateLoad` 實現並執行 `Load()`。

另一方面，`ITemplate` 建構函式本身也會把模板例項非同步註冊進 `TemplateControl.ITemplateNames`。因此當前模板發現是兩層機制並行工作的：

- 模板物件構造時進全域性登錄檔。
- 具體模板載入器在 MySQL 可用後重新整理內容。

這就是為什麼很多模板頁不能脫離初始化和資料庫前提來理解。

### 模板管理視窗

`MenuTemplateManagerWindow` 會開啟 `TemplateManagerWindow`。這個視窗當前不是簡單列表，而是：

- 讀取 `TemplateControl.ITemplateNames`
- 按型別名稱空間分組
- 支援搜尋和篩選
- 支援按卡片方式顯示模板
- 在選中模板後直接開啟對應編輯器

因此它承擔的是“模板入口聚合器”角色，不只是一個選單彈窗。

### 模板編輯視窗

`TemplateEditorWindow` 是當前最通用的模板宿主視窗。它會先 `template.Load()`，然後根據模板型別走兩條路徑：

- 普通模板：右側放 `PropertyGrid`
- 自訂模板：呼叫 `GetUserControl()` 並讓模板自己接管右側區域

視窗還統一接好了：

- 新建、複製、儲存、刪除命令
- 選中項切換時的 `SetSaveIndex(...)`
- `SetUserControlDataContext(...)` 或 `GetParamValue(...)`
- 列排序、搜尋和雙擊行為

這也是當前各種模板雖然介面差異很大，但仍能共用同一個宿主殼的原因。

### 模板建立視窗

`TemplateCreate` 現在已經不是“只給一個名稱輸入框”的視窗了。按當前實現，它會為新模板提供多種來源：

- 系統預設模板
- 當前副本（複製後暫存的模板內容）
- SQLite 樣例庫中的歷史樣例

這些來源會被渲染成卡片，並按組過濾。最終由 `ApplyTemplateSource(...)` 把選中的來源注入到待建立模板裡。

這說明當前模板建立鏈已經不只是“CreateDefault() + 手填參數”。

### 搜尋與樣例庫

`TemplateSearchProvider` 會把所有模板名註冊到全域性搜尋入口；`TemplateSampleLibrary` 則把模板樣例存到使用者文件目錄下的 SQLite 庫：

- `.../Templates/TemplateSamples.db`

它當前儲存的是：

- 模板程式碼與模板型別
- 分組名與樣例名
- 描述文字
- 序列化後的模板內容

所以模板管理現在除了 MySQL 主儲存之外，還有一條本地樣例複用鏈。

## 當前幾個最容易寫錯的點

### 它不是純服務層系統

當前很多關鍵邏輯都直接寫在 `TemplateManagerWindow`、`TemplateEditorWindow`、`TemplateCreate` 這些 WPF 視窗裡。繼續把它描述成“宿主只繫結 ViewModel，邏輯都在服務層”，和真實程式碼不符。

### 不同模板的持久化方式並不統一

有些模板主要依賴 MySQL，有些模板支援檔案匯入匯出，有些模板還會額外走 SQLite 樣例庫。文件不能再假設所有模板都是同一種儲存模型。

### `IsUserControl` 和 `IsSideHide` 會顯著改變行為

當前模板宿主不是固定佈局。`IsUserControl` 會把右側改成交給模板自訂控制元件，`IsSideHide` 甚至會改變視窗布局與雙擊行為。忽略這兩個開關，會解釋不通很多模板頁。

### 模板註冊和資料庫連線仍然耦合

雖然 `ITemplate` 構造會註冊例項，但許多具體模板內容仍然要等 MySQL 連線後才能真正載入。把模板系統寫成“純本地靜態註冊”會遺漏關鍵前提。

## 推薦閱讀順序

1. `Engine/ColorVision.Engine/Templates/ITemplate.cs`
2. `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
3. `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
5. `Engine/ColorVision.Engine/Templates/TemplateCreate.xaml.cs`
6. `Engine/ColorVision.Engine/Templates/TemplateSearchProvider.cs`
7. `Engine/ColorVision.Engine/Templates/TemplateSampleLibrary.cs`

## 繼續閱讀

- [JSON 模板](./json-templates.md)
- [流程引擎](./flow-engine.md)
- [Templates 分析總結](../../../03-architecture/components/templates/analysis.md)
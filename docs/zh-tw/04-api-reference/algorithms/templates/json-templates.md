# JSON 模板

本頁只描述當前倉庫裡真實可用的 JSON 模板宿主鏈，不再繼續維護“通用演算法 DSL 平台 + 跨專案配置框架”式舊稿。

## 先看這個模組現在是什麼

按當前原始碼狀態，JSON 模板系統不是一套脫離資料庫獨立存在的配置平台，而是 `ColorVision.Engine` 模板體系中的一個具體分支。它當前的核心目標是：

- 把 `ModMasterModel.JsonVal` 裡的 JSON 內容託管成模板項。
- 透過通用編輯器 `EditTemplateJson` 提供文字編輯和屬性編輯兩種模式。
- 讓具體模板型別以 `ITemplateJson<T>` 的形式複用同一套載入、儲存、匯入匯出邏輯。
- 為像 `PoiAnalysis`、`SFRFindROI` 這類 JSON 驅動模板提供統一宿主。

因此它更像“資料庫中的 JSON 模板分支”，而不是一個完全獨立的配置子系統。

## 當前最關鍵的檔案

- `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/TemplateJsonParam.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/TemplatePoiAnalysis.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

如果只是想看“JSON 模板現在怎麼存、怎麼編、怎麼掛進模板視窗”，這些檔案已經覆蓋主幹。

## 當前主鏈怎麼跑

### 宿主基類

`ITemplateJson<T>` 是 JSON 模板分支的通用宿主。它當前負責：

- 用 `TemplateDicId` 從 MySQL 讀取 `ModMasterModel`
- 把每條記錄包裝成 `TemplateModel<T>`
- 提供儲存、刪除、複製、匯入、匯出
- 在建立新模板時，從字典模板預設 JSON 生成初始內容

這意味著 JSON 模板雖然長得像純文字編輯，但當前仍然深度依賴模板字典和資料庫記錄。

### 參數物件

`TemplateJsonParam` 當前是最基礎的 JSON 模板參數物件。它持有：

- `TemplateJsonModel`
- `ResetCommand`
- `CheckCommand`
- `JsonValueChanged` 事件

其中 `JsonValue` 的真實語義是：

- 讀取時用 `JsonHelper.BeautifyJson(...)` 格式化
- 寫入時只有在 JSON 合法時才回寫 `TemplateJsonModel.JsonVal`

`ResetValue()` 則會回到字典模板記錄的預設 JSON，而不是簡單清空本地文字。

### 編輯器控制元件

`EditTemplateJson` 是當前真正的編輯入口。它現在同時支援：

- AvalonEdit 文字模式
- `JsonPropertyEditorControl` 屬性模式
- 描述註釋檢視切換
- 校驗按鈕
- 外部 JSON 網站輔助入口

其中右下角的 `json` 按鈕當前實際行為很明確：

- 開啟 `https://www.json.cn/`
- 把當前 JSON 複製到剪貼簿

這就是當前活動檔案裡 `Button_Click_1` 的真實作用，不是其它隱藏命令。

### 模式切換與同步

`EditTemplateJson` 當前不是簡單文字框包裝。它會：

- 用防抖定時器同步文字改動
- 透過 `IEditTemplateJson.JsonValueChanged` 反向重新整理介面
- 在文字模式與屬性模式之間切換時同步 JSON 內容
- 用 `EditTemplateJsonConfig` 記住寬度和預設編輯模式

因此這裡的複雜度主要在“兩個編輯面保持同一份 JSON 一致”，而不是演算法本身。

## 當前幾個最容易寫錯的點

### 它不是通用檔案模板平台

當前 JSON 模板的主儲存是 MySQL 的 `ModMasterModel.JsonVal`，不是倉庫裡一組任意 JSON 檔案。繼續把它寫成“讀取磁碟配置目錄”會偏離真實實現。

### 不是所有 JSON 模板共享同一個業務 schema

`ITemplateJson<T>` 只提供宿主鏈；每個具體模板實際需要什麼欄位，仍由各自的 JSON 約定決定。文件不能再把某一類 JSON 結構寫成全系統統一規範。

### 編輯器已經不只是文字編輯器

當前 `EditTemplateJson` 已經支援屬性模式和描述模式切換。只描述 AvalonEdit 文字框，會漏掉使用者實際看到的一半功能。

### “校驗”當前主要是事件觸發，不是完整編譯器

`CheckCommand` 觸發的是 `JsonValueChanged` 事件鏈，具體怎麼響應取決於呼叫方。不要把它寫成獨立的 JSON 規則引擎。

## 推薦閱讀順序

1. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
2. `Engine/ColorVision.Engine/Templates/Jsons/TemplateJsonParam.cs`
3. `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/TemplatePoiAnalysis.cs`
5. `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

## 繼續閱讀

- [Templates API 參考](./api-reference.md)
- [模板管理](./template-management.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)
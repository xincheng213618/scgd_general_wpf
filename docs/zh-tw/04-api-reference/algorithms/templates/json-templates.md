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

## 當前 JSON 子模板目錄

`Jsons/` 目錄下不是一種模板，而是一組共享同一 JSON 宿主的具體算法模板。當前源碼可分為下面幾類：

| 目錄 | 模板/字典 | 算法事件 | 結果/選單 | 交接重點 |
| --- | --- | --- | --- | --- |
| `LedCheck2/` | `TemplateLedCheck2`，`TemplateDicId = 18`，`Code = FindLED` | `Event_OLED_FindDotsArrayMem_GetData` | 無專屬 handler | LED 點陣 V2 JSON 模板，schema 為 `FindLED.schema.json`。 |
| `LEDStripDetectionV2/` | `TemplateLEDStripDetectionV2`，`TemplateDicId = 26`，`Code = LEDStripDetection` | `LEDStripDetection`，`Version = 2.0` | `ViewHandleLEDStripDetectionV2`，`MenuLEDStripDetectionV2` | LED 燈條 V2 路徑，區別於舊強類型 `LEDStripDetection/`。 |
| `OLEDAOI/` | `TemplateOLEDAOI`，`TemplateDicId = 28`，`Code = OLED.AOI` | `OLEDAOI`，`Version = 2.0` | `ViewHandleOLEDAOI`，`MenuOLEDAOI` | OLED AOI 主模板，下面還有黑屏、四合一、複判子模板。 |
| `BinocularFusion/` | `TemplateBinocularFusion`，`TemplateDicId = 35`，`Code = ARVR.BinocularFusion` | `ARVR.BinocularFusion` | `ViewHandleBinocularFusion` | ARVR 雙目融合 JSON 模板。 |
| `SFRFindROI/` | `TemplateSFRFindROI`，`TemplateDicId = 36`，`Code = ARVR.SFR.FindROI` | `ARVR.SFR.FindROI` | `ViewHandleSFRFindROI` | SFR 找 ROI，常與 ARVR/SFR 鏈路一起排查。 |
| `BlackMura/` | `TemplateBlackMura`，`TemplateDicId = 37`，`Code = BlackMura.Caculate` | `BlackMura.Caculate` | `ViewHandleBlackMura` | BlackMura 計算模板和結果展示。 |
| `Ghost2/` | `TemplateGhostQK`，`TemplateDicId = 38`，`Code = ghost` | `Ghost`，`Version = 2.0` | `ViewHandleGhostQK`，`MenuGhost2` | Ghost V2，handler 依賴結果版本 `2.0`。 |
| `FOV2/` | `TemplateDFOV`，`TemplateDicId = 39`，`Code = FOV` | `FOV`，`Version = 2.0` | `ViewHandleDFOV` | DFOV/FOV V2 JSON 路徑。 |
| `Distortion2/` | `TemplateDistortion2`，`TemplateDicId = 40`，`Code = distortion` | `Distortion`，`Version = 2.0` | `ViewHandleDistortion2` | 畸變 V2，handler 依賴結果版本 `2.0`。 |
| `BuildPOIAA/` | `TemplateBuildPOIAA`，`TemplateDicId = 41`，`Code = BuildPOI` | `ARVR.AA.FindPoints`，`Version = 2.0` | 無專屬 handler | 根據 AA 找點結果構建 POI 的 JSON 模板。 |
| `AAFindPoints/` | `TemplateAAFindPoints`，`TemplateDicId = 42`，`Code = FindLightArea` | `ARVR.AA.FindPoints`，`Version = 2.0` | `ViewHandleAAFindPoints` | AA 找點/發光區 V2，handler 還會看結果版本。 |
| `PoiAnalysis/` | `TemplatePoiAnalysis`，`TemplateDicId = 44`，`Code = PoiAnalysis` | `PoiAnalysis`，`Version = 1.0` | `ViewHandlePoiAnalysis` | POI 分析 JSON 模板，版本仍是 `1.0`。 |
| `FindCross/` | `TemplateFindCross`，`TemplateDicId = 45`，`Code = FindCross` | `FindCross` | `ViewHandleFindCross` | 十字計算模板，handler 當前檢查結果版本 `1.0`。 |
| `MTF2/` | `TemplateMTF2`，`TemplateDicId = 48`，`Code = MTF` | `MTF`，`Version = 2.0` | `ViewHandleMTF2` | MTF V2，區別於 ARVR/MTF 舊模板。 |
| `SFR2/` | `TemplateSFR2`，`TemplateDicId = 49`，`Code = SFR` | `SFR`，`Version = 2.0` | `ViewHandleSFR2` | SFR V2，區別於 ARVR/SFR 舊模板。 |
| `ImageROI/` | `TemplateImageROI`，`TemplateDicId = 52`，`Code = Image.ROI` | `Image.ROI` | 無專屬 handler | JSON 圖像 ROI，區別於強類型 [ImageCropping 圖像裁剪模板](./image-cropping-template.md)。 |
| `KB/` | `TemplateKB`，`TemplateDicId = 150`，`Code = KB` | `KB` | `ViewHandleKB` | KB 專案/算法相關 JSON 模板。 |
| `Deprecated/` | `TemplateCaliAngleShift`、`TemplateCompoundImg` | `CaliAngleShift`、`CompoundImg` | 對應舊 handler | 歷史相容目錄，新交接不要優先擴充這裡。 |

`Schemas/schema-index.json` 是當前 schema 索引，列出多數元件 schema 檔案，例如 `FindLED.schema.json`、`LEDStripDetection.schema.json`、`OLED.AOI.schema.json`、`ARVR.SFR.FindROI.schema.json`、`SFR.schema.json` 和 `Image.ROI.schema.json`。新增 JSON 模板時，應同步考慮是否需要把 schema 放入對應目錄並登記到 schema index。

## V2 與舊強類型模板邊界

很多目錄名帶 `2` 或 `V2`，但真正影響結果 handler 的不是目錄名，而是請求參數和結果版本：

| 模板族 | 當前 JSON 路徑 | 舊/強類型路徑 | 交接邊界 |
| --- | --- | --- | --- |
| LED 點/燈條 | `LedCheck2/`、`LEDStripDetectionV2/` | `LedCheck/`、`LEDStripDetection/` | V2 主要走 JSON schema 和 `Version = 2.0`，不要混用舊模板欄位。 |
| MTF/SFR/FOV/Ghost/Distortion | `MTF2/`、`SFR2/`、`FOV2/`、`Ghost2/`、`Distortion2/` | `ARVR/MTF`、`ARVR/SFR`、`ARVR/FOV`、`ARVR/Ghost`、舊畸變模板 | handler 通常透過 `result.Version` 區分，排查結果展示時必須看版本。 |
| ROI/裁剪 | `ImageROI/`、`SFRFindROI/` | `ImageCropping/`、`FindLightArea/`、`POI/` | JSON ROI 和強類型裁剪不是同一條鏈，參數來源和結果表不同。 |
| OLED AOI | `OLEDAOI/` 及其子目錄 | 專案包或舊 OLED 節點 | 主模板與黑屏、四合一、複判子模板共享 AOI 領域，但事件名和 schema 不同。 |

交接時如果看到同一個算法名既有舊模板又有 JSON 模板，應按“模板類型 -> MQTT 事件 -> Version -> ViewHandle”四步確認當前走哪條鏈。

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

### Deprecated 目錄不是新功能入口

`Deprecated/` 下仍保留 `CaliAngleShift`、`CompoundImg` 等舊模板和 handler，用於相容歷史資料。新增功能、現場交接和新專案說明不要優先引用這個目錄，除非明確在維護舊流程。

## 驗收建議

| 場景 | 必驗項 |
| --- | --- |
| 編輯 JSON | 文字模式和屬性模式互相切換後 JSON 不丟欄位 |
| schema 維護 | 新增或修改 schema 後，`Schemas/schema-index.json` 能定位對應檔案 |
| V2 算法執行 | MQTT 參數裡 `TemplateParam`、`Version`、事件名和服務端預期一致 |
| 結果展示 | `ViewHandle*.cs` 的 `CanHandle1` 或版本判斷能匹配實際結果 |
| 匯入匯出 | JSON 模板匯出後重新匯入，名稱、`Code`、預設值和 JSON 內容正確 |

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

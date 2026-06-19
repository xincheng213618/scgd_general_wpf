# 目前演算法模板覆蓋清單

本文把原始碼中的 `Engine/ColorVision.Engine/Templates/` 目錄和目前文件入口逐項對齊。它不是演算法功能承諾表，而是交接時用來判斷“這個模板目錄先讀哪篇文件、還缺什麼說明”的覆蓋清單。

## 覆蓋狀態說明

| 狀態 | 含義 |
| --- | --- |
| 已有單頁 | 已有面向交接的專題頁，可說明主要入口、執行鏈路和邊界。 |
| 橫向覆蓋 | 目前歸入模板管理、ROI/POI、通用演算法模組或 Engine 鏈路頁，尚無獨立專題頁。 |
| 待補單頁 | 已能定位歸屬，但業務含義或驗收口徑需要後續拆成獨立文件。 |

## Templates 目錄覆蓋

| 模板目錄 | 業務角色 | 先讀文件 | 狀態 | 交接重點 |
| --- | --- | --- | --- | --- |
| `ARVR/` | AR/VR 檢測模板族，連接模板參數、演算法請求和結果展示。 | [ARVR 模板](./templates/arvr-template.md)、[結果交接鏈路](../engine-components/result-handoff-chain.md) | 已有單頁 | 已覆蓋模板矩陣、手動事件、Flow `operatorCode`、POI 依賴、結果表和 handler 驗收項。 |
| `BuzProduct/` | 產品/業務參數模板，把產品主檔、明細、POI 和 Validate 規則綁在一起。 | [BuzProduct 產品業務參數模板](./templates/buz-product-template.md)、[Validate 判定規則模板](./templates/validate-rules.md) | 已有單頁 | 追蹤 `BuzProduc` 原始碼拼寫、主/明細表、`poi_id` 與 `val_rule_temp_id`。 |
| `Compliance/` | 合規結果展示與判定解讀層，讀取 Y/XYZ/JND 結果和 `ValidateResult`。 | [Compliance 結果交接](./templates/compliance-results.md)、[結果交接鏈路](../engine-components/result-handoff-chain.md) | 已有單頁 | 追蹤三張結果明細表、handler 類型映射與 `ValidateRuleResultType.M` 判定邏輯。 |
| `DataLoad/` | 資料載入模板，給 Flow 的 DataLoad 節點提供設備、批號、結果類型和 ZIndex 參數。 | [DataLoad 資料載入模板](./templates/data-load-template.md)、[模板與 Flow 鏈路](../engine-components/template-flow-chain.md) | 已有單頁 | 區分 `AlgDataLoadNode` 模板路徑與 `AlgDataLoadNode2` 顯式參數路徑。 |
| `FindLightArea/` | 發光區域/ROI 定位模板，和 OpenCV helper、ROI 結果強相關。 | [FindLightArea 發光區定位模板](./templates/find-light-area.md)、[ROI 原語](./primitives/roi.md) | 已有單頁 | `Event_LightArea2_GetData`、`RoiParam`、點位表和凸包覆蓋層。 |
| `Flow/` | 流程模板，把模板系統和 `FlowEngineLib` 可視化流程連接起來。 | [流程引擎](./templates/flow-engine.md)、[Engine 模板與 Flow 鏈路](../engine-components/template-flow-chain.md) | 已有單頁 | 已覆蓋 `TemplateFlow` 儲存路徑、`.cvflow` 包、匯入匯出、執行排程和節點配置器邊界。 |
| `FocusPoints/` | 舊發光區/關注點參數模板，保存二值化、濾波、形態學、過濾與 ROI 邊界。 | [FocusPoints 關注點模板](./templates/focus-points-template.md)、[FindLightArea 發光區定位模板](./templates/find-light-area.md) | 已有單頁 | 區分手動 `Event_LightArea_GetData` 與 Flow `operatorCode = "FocusPoints"`。 |
| `ImageCropping/` | 強型別圖像裁剪模板，連接四點 ROI、Flow 雙輸入裁剪節點和裁剪結果展示。 | [ImageCropping 圖像裁剪模板](./templates/image-cropping-template.md)、[結果交接鏈路](../engine-components/result-handoff-chain.md) | 已有單頁 | 追蹤 `Event_Image_Cropping`、`OLED.GetRIAand`、`ROI_MasterId` 與 `ViewHandleImageCropping`。 |
| `JND/` | JND 相關檢測模板，通常與 AR/VR 或顯示品質業務關聯。 | [JND 模板](./templates/jnd-template.md)、[POI 模板](./templates/poi-template.md) | 已有單頁 | `CutOff`、`POITemplateParam`、`h_jnd/v_jnd` 和專案側 OK/NG 邊界。 |
| `Jsons/` | JSON 模板體系，提供文字/屬性兩種編輯和匯入匯出路徑。 | [JSON 模板](./templates/json-templates.md)、[Templates API 參考](./templates/api-reference.md) | 已有單頁 | 已覆蓋當前 JSON 子模板目錄、schema index、V2/舊強型別邊界、handler 和驗收項。 |
| `LedCheck/` | LED 檢測模板族，面向燈珠、亮度或缺陷類檢查。 | [LED 檢測模板](./templates/led-detection.md)、[POI 模板](./templates/poi-template.md) | 已有單頁 | `FindLED` 新舊入口、POI 依賴、結果 handler 註冊和導出邊界。 |
| `LEDStripDetection/` | LED 燈條檢測模板，和 JSON 模板、條帶定位、缺陷結果關聯。 | [LED 檢測模板](./templates/led-detection.md)、[JSON 模板](./templates/json-templates.md) | 已有單頁 | 區分舊強型別 `Event_LED_StripDetection` 和 JSON V2 `Version = 2.0`。 |
| `Matching/` | 模板匹配與定位鏈路，包含手動頁、Flow 節點、MQTT 請求和 AOI 結果展示。 | [Matching 模板匹配](./templates/matching-template.md)、[結果交接鏈路](../engine-components/result-handoff-chain.md) | 已有單頁 | 追蹤 `MatchTemplate`、`TemplateFile`、`t_scgd_algorithm_result_detail_aoi` 與四點 overlay。 |
| `Menus/` | 模板選單/入口包裝，決定模板選單分組、父子關係和預設編輯視窗。 | [模板選單入口](./templates/template-menu-entries.md)、[模板管理](./templates/template-management.md) | 已有單頁 | 追蹤 `OwnerGuid`、`Order`、`Header`、`Template` 與 `ShowTemplateWindow()`。 |
| `POI/` | POI 模板族，提供點位、區域和上游演算法參數。 | [POI 模板](./templates/poi-template.md)、[POI 原語](./primitives/poi.md) | 已有單頁 | 已覆蓋主/伴生模板矩陣、專用點表、執行參數、BuildPOI、Flow 消費和結果 handler。 |
| `SysDictionary/` | 系統字典模板，維護 `mod_type = 7` 的演算法預設字典主檔和明細。 | [SysDictionary 系統字典模板](./templates/sys-dictionary-template.md)、[Templates API 參考](./templates/api-reference.md) | 已有單頁 | 追蹤 `TemplateModParam`、`symbol`、`default_val`、`val_type` 與遷移邊界。 |
| `Validate/` | 判定規則模板體系，包含預設合規字典和實際判定模板兩層。 | [Validate 判定規則模板](./templates/validate-rules.md)、[模板管理](./templates/template-management.md) | 已有單頁 | 追蹤 `mod_type = 110/111/120`、`CIEParams/JNDParams` 和規則主/明細表。 |

## 核心入口檔案

| 檔案 | 交接用途 |
| --- | --- |
| `TemplateContorl.cs` | 模板發現、`IITemplateLoad` 裝載和註冊入口。 |
| `TemplateManagerWindow.xaml(.cs)` | 模板管理視窗，適合追 UI 操作到模板資料。 |
| `TemplateEditorWindow.xaml(.cs)` | 通用模板編輯視窗，適合追屬性編輯、保存和校驗。 |
| `TemplateSearchProvider.cs` | 模板搜尋入口，適合追“為什麼搜不到模板”。 |
| `TemplateSampleLibrary.cs` | 模板樣例和復用入口，適合追預設模板來源。 |

## 維護規則

- 新增 `Templates/<Name>/` 目錄時，先在本文補一行，再決定是否拆獨立專題頁。
- 如果目錄包含 `Algorithm*`、結果視圖或 MQTT 執行請求，文件必須說明參數來源、執行服務、結果欄位和失敗處理。
- 如果目錄只是選單、字典或包裝層，也要寫清它服務於哪個模板族。
- 當“待補單頁”的目錄進入客戶專案交付、DLL 釋出或現場驗收範圍時，應優先補成獨立文件。

## 下一批優先補齊

1. Flow 轉換/校準節點已移到 [Flow 轉換與校準節點](../engine-components/flow-conversion-calibration-nodes.md)：當前源碼沒有 `Templates/FileConvert/`、`Templates/ImageTransform/`、`Templates/Calibration/` 三個目錄，後續按節點鏈路維護。
2. `Menus/`、`SysDictionaryMod/`：繼續把選單入口、字典預設值和模板視窗註冊關係補成可交接清單。
3. `Projects/` 中仍未細化的客戶專案包：繼續把專案業務入口、依賴模板、插件能力和現場驗收口徑對齊。

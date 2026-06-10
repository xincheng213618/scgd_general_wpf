# LED 檢測模板

本頁說明目前倉庫中 LED 檢測相關模板的交接邊界，重點覆蓋 `LEDStripDetection/` 和 `LedCheck/` 兩個強型別模板，並說明它們和 `Jsons/LEDStripDetectionV2/`、`Jsons/LedCheck2/` 的關係。

## 先分清四個入口

| 入口 | 類型 | 代碼/事件 | 適用場景 |
| --- | --- | --- | --- |
| `LEDStripDetection/` | 強型別模板 | `Code = LEDStripDetection`，`Event_LED_StripDetection` | 舊燈條定位。 |
| `LedCheck/` | 強型別模板 | `Code = FindLED`，`Event_LED_Check_GetData` | 燈珠檢測，依賴 POI 並繪製圓點。 |
| `Jsons/LEDStripDetectionV2/` | JSON 模板 | `Code = LEDStripDetection`，事件名 `LEDStripDetection`，`Version = 2.0` | 新燈條/POI 中心計算。 |
| `Jsons/LedCheck2/` | JSON 模板 | `Code = FindLED`，`Event_OLED_FindDotsArrayMem_GetData` | 亞像素級 OLED 點陣檢測。 |

交接時不要只憑 `Code` 判斷唯一實現：`LEDStripDetection` 和 `FindLED` 都同時存在舊強型別和新 JSON 入口。

## 強型別 LEDStripDetection

| 檔案 | 交接用途 |
| --- | --- |
| `TemplateLEDStripDetection.cs` | 註冊模板，`TemplateDicId = 21`，`IsUserControl = true`。 |
| `LEDStripDetectionParam.cs` | 保存點數、點距、起始位置、二值化比例、調試和保存路徑。 |
| `EditLEDStripDetection.xaml(.cs)` | 自定義參數編輯控件。 |
| `AlgorithmLEDStripDetection.cs` | 組裝 `Event_LED_StripDetection` 請求。 |
| `DisplayLEDStripDetection.xaml(.cs)` | 選擇模板、圖像來源、批次號/Raw/本地檔案並執行。 |

請求會帶 `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateParam` 和 `IsInversion`。

## 強型別 LedCheck

| 檔案 | 交接用途 |
| --- | --- |
| `TemplateLedCheck.cs` | 註冊燈珠檢測，`Code = FindLED`。 |
| `LedCheckParam.cs` | 保存通道、固定半徑、輪廓面積、二值化補正、網格數量等。 |
| `AlgorithmLedCheck.cs` | 收集燈珠模板和 POI 模板，發布 `Event_LED_Check_GetData`。 |
| `DisplayLedCheck.xaml(.cs)` | 選擇燈珠模板、POI 模板和圖像來源。 |
| `ViewHandleMTF.cs` | 從 POI 結果恢復點位並繪製圓。 |
| `ViewResultLedCheck.cs` | 保存點位和半徑。 |

`LedCheck` 會額外傳 `POITemplateParam`。目前 UI 使用 `TemplatePoi.Params.CreateEmpty()`，所以要確認現場是否允許空 POI。

`ViewHandleLedCheck.CanHandle` 目前是空列表。若演算法成功但結果頁不展示，先查結果類型註冊。

## JSON V2 入口

- `TemplateLEDStripDetectionV2`：`TemplateDicId = 26`，`Name = LedStripDetectionV2`，JSON 包含 `debugCfg`、`mathMaskRect`、`nV1`、`threshold`、`dRatio`、`pattern`、`CalcMethod`。
- `AlgorithmLEDStripDetectionV2`：事件名 `LEDStripDetection`，會傳 `Version = 2.0`，可附帶 `POITemplateParam`。
- `TemplateLedCheck2`：`TemplateDicId = 18`，`Code = FindLED`。
- `AlgorithmLedCheck2`：事件為 `Event_OLED_FindDotsArrayMem_GetData`，傳 `Color`、`FDAType` 和四個 `FixedLEDPoint`。

## 選擇哪個入口

| 需求 | 建議入口 |
| --- | --- |
| 維護舊燈條定位 | `LEDStripDetection/` |
| 新增複雜燈條參數或 JSON 版本治理 | `Jsons/LEDStripDetectionV2/` |
| 維護傳統燈珠檢測和 POI 半徑展示 | `LedCheck/` |
| 亞像素級 OLED 點陣檢測 | `Jsons/LedCheck2/` |
| 排查結果展示 | 先查 handler 結果類型註冊，再看繪圖/導出。 |

## 常見排查

| 現象 | 優先排查 |
| --- | --- |
| 燈條模板下拉為空 | `TemplateLEDStripDetection.Params` 和 `TemplateDicId = 21`。 |
| V2 模板下拉為空 | `TemplateLEDStripDetectionV2` 和 `TemplateDicId = 26` 的 JSON 字典。 |
| 燈珠檢測執行失敗 | `TemplateParam`、`POITemplateParam`、圖像類型和設備 `Code/Type`。 |
| JSON 參數不生效 | 確認改的是 V2 JSON 模板，不是舊強型別模板。 |
| 結果不顯示 | `ViewResultAlgType` 是否匹配 handler，`ViewHandleLedCheck.CanHandle` 是否需要補註冊。 |
| CSV 導出不對 | `ViewHandleLedCheck.SideSave(...)` 目前需要單獨補驗收。 |

## 交接清單

- 涉及 `Code = LEDStripDetection` 的變更必須說明是舊強型別還是 JSON V2。
- 涉及 `Code = FindLED` 的變更必須說明是 `LedCheck` 還是 `LedCheck2`。
- 修改強型別參數時，同步更新參數類、預設值、編輯控件和現場樣例。
- 修改 JSON 參數時，同步更新 schema/說明 JSON、`Mysql*` 恢復命令和版本策略。
- 修改結果展示時，同步更新 handler、導出、專案驗收頁和截圖樣例。

## 繼續閱讀

- [JSON 模板](./json-templates.md)
- [POI 模板](./poi-template.md)
- [結果交接鏈路](../../engine-components/result-handoff-chain.md)
- [目前演算法模板覆蓋清單](../current-algorithm-template-coverage.md)

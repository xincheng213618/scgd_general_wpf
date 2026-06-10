# ARVR 模板

本頁只描述當前倉庫裡真實可見的 ARVR 模板族，不再維護“光學演算法教材 + 統一參數手冊”式舊稿。

## 這個模板族當前在做什麼

按當前原始碼狀態，ARVR 不是一個單模板，而是一組並行存在的模板和顯示演算法：

- `MTF`
- `SFR`
- `FOV`
- `Distortion`
- `Ghost`

這些實現共享同一種宿主框架，但參數模型、結果表現和是否依賴 POI 並不統一。再往前走到 Flow 節點時，還會混入 JSON 變體，例如 `SFR_FindROI` 這類别範本。

所以這頁更適合當成“ARVR 家族地圖”，而不是試圖維護一張萬能參數列。

## 當前最關鍵的檔案

- `Engine/ColorVision.Engine/Templates/ARVR/MTF/TemplateMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/MTFParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/ViewHandleMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/SFRParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/AlgorithmSFR.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/WindowSFR.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/FOVParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/AlgorithmFOV.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/DisplayFOV.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/DistortionParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/AlgorithmDistortion.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/ViewResultDistortion.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs`
- `Engine/FlowEngineLib/Algorithm/AlgorithmARVRNode.cs`

## 當前模板矩陣

ARVR 目錄下既有傳統強型別模板，也會在 Flow 中接入 JSON V2 模板。交接時先用下面這張表確認當前鏈路，不要只按目錄名判斷。

| 模板族 | 傳統模板 | 字典/程式碼 | 執行事件 | 關鍵請求參數 | 結果入口 |
| --- | --- | --- | --- | --- | --- |
| `FOV` | `TemplateFOV` | `TemplateDicId = 6`，`Code = FOV` | `Event_FOV_GetData` | `TemplateParam` | `ViewHandleFOV`，`ViewResultAlgType.FOV` |
| `Ghost` | `TemplateGhost` | `TemplateDicId = 7`，`Code = ghost` | `Ghost` | `TemplateParam`、`Color` | `ViewHandleGhost`，`ViewResultAlgType.Ghost` |
| `MTF` | `TemplateMTF` | `TemplateDicId = 8`，`Code = MTF` | `Event_MTF_GetData` | `TemplateParam`、`POITemplateParam` | `ViewHandleMTF`，`ViewResultAlgType.MTF` |
| `SFR` | `TemplateSFR` | `TemplateDicId = 9`，`Code = SFR` | `Event_SFR_GetData` | `TemplateParam`、`POITemplateParam` | `ViewHandleSFR`，`ViewResultAlgType.SFR` |
| `Distortion` | `TemplateDistortionParam` | `TemplateDicId = 10`，`Code = distortion` | `Distortion` | `TemplateParam` | `ViewHandleDistortion`，`ViewResultAlgType.Distortion` |
| `AOI` | `TemplateAOIParam` | `TemplateDicId = 12`，`Code = AOI` | 當前不是獨立主執行入口 | 模板參數配置 | 主要作為 ARVR/AOI 參數配置，不要誤寫成完整結果鏈 |

這裡的“執行事件”來自當前手動演算法類。Flow 節點的 `operatorCode` 還會覆蓋到 `ARVR.BinocularFusion`、`ARVR.SFR.FindROI`、`FindCross` 這些 JSON 模板分支。

## 當前主鏈怎麼分

### MTF

`TemplateMTF` 是經典參數模板，當前：

- `Code = MTF`
- `TemplateDicId = 8`

`MTFParam` 裡當前最直接可見的參數包括：

- `MTF_dRatio`
- `eEvaFunc`
- `dx`
- `dy`
- `ksize`

`AlgorithmMTF` 的實際行為不是本地算圖，而是：

- 開啟 `TemplateMTF`
- 開啟 `TemplatePoi`
- 組裝 `POITemplateParam`
- 釋出 `Event_MTF_GetData`

這說明當前 MTF 執行鏈明確依賴 POI 模板，而不是獨立於 POI 存在。

結果側最值得看的不是參數類，而是 `ViewHandleMTF`。它會：

- 把結果匯出成 CSV
- 統計最大值、最小值、均值、方差和均勻性
- 作為 `ViewResultAlgType.MTF` 的處理器接入 UI

### SFR

`SFRParam` 當前比舊文件裡簡單得多，直接可見的核心參數只有 `Gamma`。真正的顯示和結果互動更多落在：

- `AlgorithmSFR`
- `WindowSFR`

`AlgorithmSFR` 和 MTF 一樣，會額外要求 `TemplatePoi`，再發布 `Event_SFR_GetData`。`WindowSFR` 則負責把結果裡的 `Pdfrequency`、`PdomainSamplingData` 反序列化成曲線，並提供閾值和頻率換算。

因此當前 SFR 文件不能再只講模板參數，也要把結果視窗算進去。

### FOV

`FOVParam` 當前是一個較完整的參數模型，直接包含：

- `Radio`
- `CameraDegrees`
- `ThresholdValus`
- `DFovDist`
- `FovPattern`
- `FovType`
- `Xc`、`Yc`、`Xp`、`Yp`

`AlgorithmFOV` 負責打包 `Event_FOV_GetData`，而 `DisplayFOV` 則承擔了當前非常實際的一層工作：

- 從服務管理器取影像源裝置
- 支援批次、原始檔案和本地影像三種輸入
- 拉取 Raw 檔案列表並允許直接開啟

這說明 FOV 當前並不是“只配參數然後跑演算法”的極簡模板。

### Distortion

`DistortionParam` 當前是真正的大參數物件，包含多組 blob 閾值、面積過濾、形狀過濾和全域性策略項，例如：

- `filterByColor`
- `minThreshold` / `maxThreshold`
- `minArea` / `maxArea`
- `filterByCircularity`
- `filterByConvexity`
- `filterByInertia`
- `CornerType`
- `SlopeType`
- `LayoutType`
- `DistortionType`

`AlgorithmDistortion` 負責釋出 `Distortion` 事件，`ViewResultDistortion` 則把列舉值和最終點陣結果重新對映成可展示的描述物件。

### Ghost

`GhostParam` 當前可見的核心參數並不複雜，主要圍繞檢測點陣：

- `Ghost_radius`
- `Ghost_cols`
- `Ghost_rows`
- `Ghost_ratioH`
- `Ghost_ratioL`

`AlgorithmGhost` 額外附帶了 `Color` 參數，再發布 `Ghost` 事件。也就是說，顏色通道當前是 Ghost 鏈上的一等輸入，不是頁面註釋裡的附加項。

## Flow 裡怎麼接進來

`AlgorithmARVRNode` 與 `AlgorithmNodeConfigurators` 共同揭示了當前 ARVR 家族在 Flow 裡的真實用法：

- `MTF`、`SFR` 節點會同時要求參數模板和 `POI` 模板。
- `FOV`、`畸變` 節點既能接經典參數模板，也能接 JSON 變體。
- `SFR_FindROI` 這類分支會同時接 `TemplateSFRFindROI` 和 `TemplatePoi`。

因此當前 ARVR 族不是一條平坦目錄，而是傳統模板、JSON 模板、POI 模板和 Flow 節點共同拼出來的執行面。

| Flow 算子 | `operatorCode` | 配置器掛載 | 交接重點 |
| --- | --- | --- | --- |
| `MTF` | `MTF` | `TemplateMTF` + `TemplatePoi` | 缺 POI 時請求會少 `POITemplateParam`，結果點位解釋不完整。 |
| `SFR` | `SFR` | `TemplateSFR` + `TemplatePoi` | SFR 結果曲線依賴 ROI/POI 的空間定義。 |
| `FOV` | `FOV` | `TemplateDFOV` + `TemplateFOV` | 同一屬性名可掛 JSON V2 或傳統模板，排查時要看實際模板來源。 |
| `畸變` | `Distortion` | `TemplateDistortion2` + `TemplateDistortionParam` | JSON V2 和傳統強型別參數共存，結果展示還要看 `result.Version`。 |
| `SFR_FindROI` | `ARVR.SFR.FindROI` | `TemplateSFRFindROI` + `TemplatePoi` | 它不是傳統 `TemplateSFR`，而是 JSON ROI 檢出鏈。 |
| `雙目融合` | `ARVR.BinocularFusion` | `TemplateBinocularFusion` | 走 JSON 模板，不要在 ARVR 傳統目錄裡找參數類。 |
| `十字計算` | `FindCross` | `TemplateFindCross` + `TemplatePoi` 欄位作為 ROI | 名稱是 ROI，但底層仍用 `TemplatePoi` 模板選擇器。 |

`AlgorithmARVRNode.getBaseEventData(...)` 還會把 `BufferLen`、顏色通道、上一步圖像參數和 SMU 結果一起組進請求。現場如果看到手動執行正常但 Flow 執行異常，需要同時比對手動演算法類和 Flow 節點生成的參數。

## 結果落庫與展示

結果交接不能只看 `Algorithm*.cs`。當前 ARVR 傳統結果至少有下面幾條落庫/展示鏈：

| 結果 | 結果表/欄位線索 | 展示入口 | 排查重點 |
| --- | --- | --- | --- |
| `FOV` | `t_scgd_algorithm_result_detail_fov`，包含 `pattern`、`radio`、`camera_degrees`、`dist`、`threshold`、`degrees` | `ViewHandleFOV` | 圖像輸入、模板參數和結果表中的角度/距離欄位要一起看。 |
| `Ghost` | `t_scgd_algorithm_result_detail_ghost`，包含 `rows`、`cols`、`radius`、`led_centers`、`ghost_pixels` | `ViewHandleGhost` | 顏色通道和點陣數量會影響最終 overlay。 |
| `SFR` | `t_scgd_algorithm_result_detail_sfr`，包含 ROI、`gamma`、`pdfrequency`、`pdomain_sampling_data` | `ViewHandleSFR`、`WindowSFR` | CSV/曲線展示來自採樣資料反序列化，不只是單個數值。 |
| `Distortion` | `t_scgd_algorithm_result_detail_distortion`，包含 `layout_type`、`slope_type`、`corner_type`、`max_ratio`、`final_points` | `ViewHandleDistortion`、`ViewResultDistortion` | 枚舉映射和最終點陣要一起校驗。 |

## 當前幾個最容易寫錯的點

### ARVR 不是統一 schema

各子目錄共享的是模板宿主和顯示演算法風格，不是同一套參數欄位。

### 多數演算法類是宿主和命令介面卡

`AlgorithmMTF`、`AlgorithmSFR`、`AlgorithmFOV`、`AlgorithmDistortion`、`AlgorithmGhost` 主要負責開視窗、取輸入、打 MQTT 請求，而不是在本地直接完成數值計算。

### POI 在 ARVR 裡不是邊角料

至少 MTF、SFR、SFR_FindROI 當前都顯式依賴 `TemplatePoi`。如果忽略 POI，這頁就解釋不通當前執行鏈。

### 結果處理程式碼同樣重要

像 `ViewHandleMTF`、`WindowSFR`、`ViewResultDistortion` 這些結果層實現，是理解使用者最終看到什麼的重要入口，不該被舊文件省掉。

### 傳統模板和 JSON V2 不是替代關係

FOV、Ghost、Distortion、SFR_FindROI 等鏈路在 Flow 中會同時暴露傳統模板和 JSON 模板。不能簡單寫成“已經升級到 V2”，也不能只保留舊模板說明；要按實際 `operatorCode`、模板類型和結果版本確認。

## 驗收建議

| 場景 | 必驗項 |
| --- | --- |
| 手動 MTF/SFR | 請求參數同時包含 `TemplateParam` 和 `POITemplateParam`，結果能被對應 `ViewHandle*` 接住。 |
| Flow ARVR 節點 | 切換演算法類型後，模板選擇器隨類型切換，並且 `operatorCode` 與演算法類型一致。 |
| FOV/Distortion V2 | 同一節點能區分傳統模板和 JSON 模板，結果展示不串 handler。 |
| SFR 曲線 | `WindowSFR` 能開啟曲線，CSV 匯出欄位和結果表 `pdomain_sampling_data` 對得上。 |
| Ghost | 請求包含 `Color`，結果表裡的點陣數量和 overlay 展示一致。 |

## 推薦閱讀順序

1. `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`
2. `Engine/ColorVision.Engine/Templates/ARVR/SFR/AlgorithmSFR.cs`
3. `Engine/ColorVision.Engine/Templates/ARVR/FOV/DisplayFOV.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/ARVR/Distortion/ViewResultDistortion.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs`

## 繼續閱讀

- [POI 模板](./poi-template.md)
- [JSON 模板](./json-templates.md)
- [流程引擎](./flow-engine.md)

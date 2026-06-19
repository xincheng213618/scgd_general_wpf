# POI 模板

本頁只描述當前倉庫裡真實存在的 POI 模板族，不再維護“檢測器介面大全 + 可插拔演算法樣例”式舊稿。

## 這個模板族當前在做什麼

按當前原始碼狀態，POI 不是一個孤立模板，而是一組圍繞“點集資料”展開的模板和演算法宿主：

- 主 POI 模板負責儲存點集、尺寸和配置。
- 過濾、修正、標定、輸出分別有自己的伴生模板。
- 執行時演算法負責把這些模板拼成 MQTT 請求。
- Flow 節點和若干 JSON 演算法會繼續消費 POI 模板。

因此這頁真正要講的不是“某一種 POI 檢測演算法”，而是當前系統裡 POI 模板怎樣被建立、編輯、儲存和複用。

## 當前最關鍵的檔案

- `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs`
- `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
- `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIFilters/TemplatePoiFilterParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIRevise/TemplatePoiReviseParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIOutput/TemplatePoiOutputParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIGenCali/TemplatePOICalParam.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 當前模板矩陣

POI 族裡只有主 POI 模板走專用點表，其它伴生模板仍然是字典模板。交接時不要把它們混成同一種持久化方式。

| 模板 | 字典/程式碼 | 編輯入口 | 主要用途 |
| --- | --- | --- | --- |
| `TemplatePoi` | `TemplateDicId = -1`，`Code = POI` | `EditPoiParam` 獨立視窗 | 儲存點集、尺寸、四角、配置 JSON 和點明細。 |
| `TemplateBuildPoi` | `TemplateDicId = 16`，`Code = BuildPOI` | 模板編輯器/布點介面 | 按規則或 CAD 映射生成 POI。 |
| `TemplatePoiFilterParam` | `TemplateDicId = 23`，`Code = POIFilter` | 自訂過濾編輯控件 | 執行 POI 時可選過濾模板。 |
| `TemplatePoiReviseParam` | `TemplateDicId = 24`，`Code = PoiRevise` | 模板編輯器 | 執行 POI 時可選修正模板。 |
| `TemplatePoiGenCalParam` | `TemplateDicId = 25`，`Code = POIGenCali` | 自訂標定修正編輯控件 | Flow 中 POI 修正標定節點使用。 |
| `TemplatePoiOutputParam` | `TemplateDicId = 27`，`Code = PoiOutput` | 自訂輸出編輯控件 | 執行 POI 時可選文件輸出模板。 |
| `TemplateBuildPOIAA` | `TemplateDicId = 41`，`Code = BuildPOI` | JSON 模板編輯器 | AA 找點結果構建 POI 的 JSON V2 分支。 |

主模板與伴生模板的差異是 POI 文件的核心：主模板儲存真實點位，伴生模板描述點位如何生成、過濾、修正和輸出。

## 當前主鏈怎麼跑

### 主模板與資料模型

`TemplatePoi` 是主入口。它當前有幾個很重要的實現特徵：

- 繼承 `ITemplate<PoiParam>`
- `IsSideHide = true`
- 模板程式碼固定為 `POI`
- 雙擊列表項時直接開啟 `EditPoiParam`

和很多普通模板不同，POI 主模板不是單純依賴右側 `PropertyGrid`，而是有自己的編輯視窗。

`PoiParam` 則不是一個只存幾個數值的簡單參數類。它當前承載：

- 模板尺寸 `Width`、`Height`
- 四角座標 `LeftTopX/Y`、`RightTopX/Y`、`RightBottomX/Y`、`LeftBottomX/Y`
- `CfgJson` 與 `PoiConfig` 的雙向轉換
- `ObservableCollection<PoiPoint> PoiPoints`

`PoiPoint` 本身儲存的是當前系統真實在用的點資訊：

- `Id`
- `Name`
- `PointType`
- `PixX`、`PixY`
- `PixWidth`、`PixHeight`

所以 POI 模板當前更接近“點集模板 + 配置模板”的組合。

### 當前持久化方式

POI 主模板不是普通 `ModMasterModel`/`ModDetailModel` 那套預設路徑。當前它走的是專用表：

- `PoiMasterDao` -> `t_scgd_algorithm_poi_template_master`
- `PoiDetailDao` -> `t_scgd_algorithm_poi_template_detail`

`PoiParam.LoadPoiDetailFromDB(...)` 會把點明細裝回 `PoiPoints`；擴充套件方法 `Save2DB(...)` 則會：

- 儲存主記錄
- 刪除舊點明細
- 用 BulkCopy 重寫整組 `PoiDetailModel`

這也是 POI 頁面最容易被寫偏的地方之一：它不是“通用模板表裡一組普通 detail 項”，而是自己帶點表。

| 表 | 關鍵欄位 | 交接含義 |
| --- | --- | --- |
| `t_scgd_algorithm_poi_template_master` | `name`、`type`、`width`、`height`、四角座標、`cfg_json`、`tenant_id`、`is_delete` | POI 模板主體、畫布尺寸和配置 JSON。 |
| `t_scgd_algorithm_poi_template_detail` | `pid`、`pt_type`、`pix_x`、`pix_y`、`pix_width`、`pix_height`、`remark` | 每個 POI 點或區域的像素位置和尺寸。 |

刪除模板時當前程式碼直接刪除 master 記錄並從列表移除，複製/匯入時會把模板和點的 `Id` 重置為 `-1`。如果現場出現“複製模板後覆蓋舊點位”，優先檢查匯入副本的 Id 是否被正確重置。

### 匯入、複製與新建

`TemplatePoi` 當前支援：

- 從當前模板複製為 JSON 臨時副本
- 從 `.cfg` 匯入點集模板
- 匯出前主動載入點明細
- 建立時把匯入副本或空模板寫回資料庫

而且複製或匯入後會把模板 `Id` 和每個點的 `Id` 重置為 `-1`，避免直接複用舊主鍵。

### 執行時演算法鏈

`AlgorithmPoi` 是當前最主要的 POI 執行入口。它負責：

- 開啟 POI 主模板編輯視窗
- 開啟過濾、修正、輸出模板編輯視窗
- 在檔案模式下選擇外部點檔案
- 組裝 `Event_POI_GetData` 的 MQTT 參數

當前傳送的參數不只包含主模板，還可能包含：

- `FilterTemplate`
- `ReviseTemplate`
- `OutputTemplate`
- `POIStorageType`
- `POIPointFileName`
- `IsSubPixel`
- `IsCCTWave`

這說明 POI 執行鏈當前已經是“多模板組合請求”，不是單模板獨跑。

| 參數 | 來源 | 說明 |
| --- | --- | --- |
| `TemplateParam` | `TemplatePoi` | 必選主 POI 模板。 |
| `FilterTemplate` | `TemplatePoiFilterParam` | 可選，`Id != -1` 時傳送。 |
| `ReviseTemplate` | `TemplatePoiReviseParam` | 可選，`Id != -1` 時傳送。 |
| `OutputTemplate` | `TemplatePoiOutputParam` | 可選，`Id != -1` 時傳送。 |
| `POIStorageType` | `POIStorageModel` | 檔案模式時傳送，區分 DB 點集和外部點文件。 |
| `POIPointFileName` | 檔案選擇器 | 檔案模式時傳送外部點文件路徑。 |
| `IsSubPixel`、`IsCCTWave` | 演算法介面配置 | 控制子像素/CCT 波形相關執行選項。 |

### 布點與伴生模板

`AlgorithmBuildPoi` 是另一條關鍵鏈。它當前負責：

- 開啟布點模板 `TemplateBuildPoi`
- 可選載入 CAD 檔案
- 在 `POIBuildType == CADMapping` 時附帶四點多邊形和 `CADMappingParam`
- 釋出 `Event_Build_POI`

除此之外，POI 族當前還包含多個伴生模板：

- `TemplatePoiFilterParam`：過濾模板，`Code = POIFilter`，使用自訂編輯控制元件
- `TemplatePoiReviseParam`：修正模板，`Code = PoiRevise`
- `TemplatePoiGenCalParam`：修正標定模板，`Code = POIGenCali`，使用自訂編輯控制元件
- `TemplatePoiOutputParam`：輸出模板，`Code = PoiOutput`，使用自訂編輯控制元件

這些模板不是註釋裡的“可選擴充套件”，而是當前 Flow 和演算法鏈裡實際會被引用的物件。

### Flow 與其它演算法怎樣消費 POI

POI 現在已經是共享原語，而不是單演算法私有模板。當前至少有三條明確的消費路徑：

1. `POINodeConfigurators` 會把 `TemplatePoi`、過濾、修正、輸出、標定等模板掛到 POI 節點屬性面板。
2. `AlgorithmPoiAnalysis` 會在 JSON 分析模板之外繼續附帶 `POITemplateParam`。
3. `AlgorithmSFRFindROI`、`AlgorithmOLEDAOI` 這類演算法也會額外引用 `TemplatePoi`。

| Flow 配置器分支 | 設備/輸入 | 模板選擇器 | 交接重點 |
| --- | --- | --- | --- |
| POI 修正標定 | `DeviceAlgorithm` | `TemplatePoiGenCalParam` | 只處理標定修正模板，不直接選擇主 POI。 |
| POI 過濾/修正/輸出 | `DeviceAlgorithm` | `TemplatePoiFilterParam`、`TemplatePoiReviseParam`、`TemplatePoiOutputParam` | 用於已有 POI 結果的後處理組合。 |
| POI 執行 | `DeviceAlgorithm` + 圖像路徑 | `TemplatePoi`、過濾、修正、輸出 | 對應 `Event_POI_GetData` 的完整執行鏈。 |
| BuildPOI | `DeviceAlgorithm` + 圖像路徑 | `TemplateBuildPoi` 或 `TemplateBuildPOIAA`，以及 `RePOI`、`LayoutROI`、`SavePOI` | 同時支援傳統布點和 JSON AA 布點分支。 |
| PoiAnalysis | `DeviceAlgorithm` | `TemplatePoiAnalysis` | JSON 分析模板仍會消費 POI 相關結果。 |

## 結果落庫與展示

POI 的結果不是單一種類，handler 會按 `ViewResultAlgType` 分流：

| 結果類型 | 展示/匯出入口 | 結果表/檔案線索 |
| --- | --- | --- |
| `POI`、`POI_Y` | `ViewHanlePOIY` | 可匯出 CSV，結果點值來自 POI 明細結果。 |
| `POI_XYZ` | `ViewHanlePOIXZY` | 可匯出 CSV，並和 XYZ 結果展示關聯。 |
| `POI_XYZ_File`、`POI_Y_File`、`POI_CIE_File` | `ViewHanlePOIXZY` | 檔案型結果，常落到 `t_scgd_algorithm_result_detail_poi_cie_file`。 |
| `RealPOI`、`POI_XYZ_V2`、`POI_Y_V2`、`KB_Output_Lv`、`KB_Output_CIE` | `ViewHandleRealPOI` | V2/專案輸出鏈，排查時要看實際 `ResultType`。 |
| `BuildPOI`、`BuildPOI_File` | `ViewHandleBuildPoi`、`ViewHandleBuildPoiFile` | 布點結果或檔案結果，可能生成新的 POI 資料。 |

點值明細表當前包括 `t_scgd_algorithm_result_detail_poi_mtf`，欄位覆蓋 `poi_id`、`poi_name`、`poi_type`、`poi_x/y`、`poi_width/height` 和 `value`。如果 UI 展示和匯出不一致，先確認 result type 走到哪個 handler，再查對應明細或檔案表。

## 當前幾個最容易寫錯的點

### POI 不是一種單獨演算法

當前倉庫裡的 POI 更像共享點集模板體系，既能生成點、過濾點，也會被其它演算法消費。

### 主儲存不是普通 detail 表

主模板依賴 `PoiMasterDao` 和 `PoiDetailDao`，如果繼續按通用模板表去解釋，會漏掉點明細這一層。

### 主編輯器不是純 `PropertyGrid`

`TemplatePoi` 雙擊後會進入 `EditPoiParam`；過濾和輸出模板也帶自己的 `UserControl` 編輯器。繼續把它們寫成統一右側屬性面板，會和真實介面不符。

### 檔案模式和資料庫模式並存

`AlgorithmPoi` 明確支援 `POIStorageModel.Db` 與 `POIStorageModel.File` 兩條路徑。文件不能再把 POI 寫成“只存在資料庫裡”。

### BuildPOI 和 POI 執行不是同一個事件

`AlgorithmBuildPoi` 發布 `Event_Build_POI`，`AlgorithmPoi` 發布 `Event_POI_GetData`。前者偏生成點集，後者偏基於點集取值/輸出。現場排查時不要把兩個事件的模板參數混用。

## 驗收建議

| 場景 | 必驗項 |
| --- | --- |
| 新建/儲存 POI | `t_scgd_algorithm_poi_template_master` 有主記錄，`t_scgd_algorithm_poi_template_detail` 有對應點明細。 |
| 複製/匯入 POI | 模板和點明細 `Id` 被重置，建立新模板後不會覆蓋舊模板。 |
| 檔案模式執行 | MQTT 參數包含 `POIStorageType` 和 `POIPointFileName`，DB 模式則不依賴外部點文件。 |
| 過濾/修正/輸出 | 選中對應模板時請求裡出現 `FilterTemplate`、`ReviseTemplate`、`OutputTemplate`。 |
| BuildPOI CADMapping | 請求包含 `LayoutPolygon` 和 `CADMappingParam`，四點 ROI 與 CAD 檔案路徑正確。 |
| 結果展示 | 根據 `ViewResultAlgType` 命中正確 handler，CSV 匯出欄位和結果表/檔案一致。 |

## 推薦閱讀順序

1. `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
2. `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
3. `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
4. `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 繼續閱讀

- [POI 原語](../primitives/poi.md)
- [JSON 模板](./json-templates.md)
- [流程引擎](./flow-engine.md)

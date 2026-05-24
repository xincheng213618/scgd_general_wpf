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
- `Engine/ColorVision.Engine/Templates/POI/POIGenCali/TemplatePoiGenCalParam.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

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

- `PoiMasterDao`
- `PoiDetailDao`

`PoiParam.LoadPoiDetailFromDB(...)` 會把點明細裝回 `PoiPoints`；擴充套件方法 `Save2DB(...)` 則會：

- 儲存主記錄
- 刪除舊點明細
- 用 BulkCopy 重寫整組 `PoiDetailModel`

這也是 POI 頁面最容易被寫偏的地方之一：它不是“通用模板表裡一組普通 detail 項”，而是自己帶點表。

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

## 當前幾個最容易寫錯的點

### POI 不是一種單獨演算法

當前倉庫裡的 POI 更像共享點集模板體系，既能生成點、過濾點，也會被其它演算法消費。

### 主儲存不是普通 detail 表

主模板依賴 `PoiMasterDao` 和 `PoiDetailDao`，如果繼續按通用模板表去解釋，會漏掉點明細這一層。

### 主編輯器不是純 `PropertyGrid`

`TemplatePoi` 雙擊後會進入 `EditPoiParam`；過濾和輸出模板也帶自己的 `UserControl` 編輯器。繼續把它們寫成統一右側屬性面板，會和真實介面不符。

### 檔案模式和資料庫模式並存

`AlgorithmPoi` 明確支援 `POIStorageModel.Db` 與 `POIStorageModel.File` 兩條路徑。文件不能再把 POI 寫成“只存在資料庫裡”。

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
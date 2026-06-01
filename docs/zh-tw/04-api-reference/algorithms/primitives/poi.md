# POI

本頁只描述當前倉庫裡作為共享原語存在的 POI，不再維護“POI 檢測演算法百科”式舊稿。

## 先看 POI 現在在系統裡扮演什麼角色

按當前原始碼狀態，POI 更像一套可複用的資料與模板體系，而不是單個演算法結果：

- 主模板儲存點集和配置。
- 布點、過濾、修正、標定、輸出圍繞這份點集工作。
- JSON 演算法和 ARVR 演算法會繼續引用 POI 模板。
- Flow 節點也把 POI 當成共享輸入輸出物件。

因此本頁重點不是“如何找特徵點”，而是 POI 這個原語當前怎樣被儲存、傳遞和消費。

## 當前最關鍵的檔案

- `Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
- `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIFilters/TemplatePoiFilterParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIRevise/TemplatePoiReviseParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIOutput/TemplatePoiOutputParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIGenCali/TemplatePoiGenCalParam.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/AlgorithmPoiAnalysis.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/AlgorithmSFRFindROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/OLEDAOI/AlgorithmOLEDAOI.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 當前資料長什麼樣

### 點物件

`PoiPoint` 現在儲存的是一套很直接的顯示與定位欄位：

- `Id`
- `Name`
- `PointType`
- `PixX`、`PixY`
- `PixWidth`、`PixHeight`

它不是一個抽象“興趣點介面”，而是已經貼近當前影像編輯和結果展示所需欄位的具體物件。

### 模板物件

`PoiParam` 負責把點集、尺寸、角點和配置打包成一個模板。它當前至少包含：

- 模板尺寸 `Width`、`Height`
- 模板型別 `Type`
- 四角座標
- `PoiPoints`
- `CfgJson` 與 `PoiConfig`

而且 `CfgJson` 不是單純字串快取，當前會和 `PoiConfig` 相互序列化、反序列化。

## 當前怎麼儲存

POI 的一個核心現實是：它當前有自己專門的主從資料結構。

- 主記錄透過 `PoiMasterDao` 儲存
- 點明細透過 `PoiDetailDao` 儲存

`PoiParam.LoadPoiDetailFromDB(...)` 會按 `Pid` 回填點集合；擴充套件方法 `Save2DB(...)` 則會清空舊明細後整批寫入新點。

這使得 POI 與一般只依賴 `ModMasterModel`/`ModDetailModel` 的模板明顯不同。

## 當前執行鏈怎麼消費 POI

### 主 POI 演算法

`AlgorithmPoi` 是最直接的 POI 消費者和生產者。它當前支援：

- 主模板 `TemplatePoi`
- 過濾模板 `TemplatePoiFilterParam`
- 修正模板 `TemplatePoiReviseParam`
- 輸出模板 `TemplatePoiOutputParam`
- 檔案模式 `POIStorageModel.File`

最終透過 `Event_POI_GetData` 釋出帶多模板參數的 MQTT 請求。

### 布點演算法

`AlgorithmBuildPoi` 負責把其它資訊轉成 POI 點集。它當前支援：

- 普通布點
- CADMapping 布點
- 四點多邊形 `LayoutPolygon`
- `CADMappingParam`
- `Event_Build_POI`

所以當前系統裡“得到一份 POI”並不只靠檢測，也可以靠建置。

### 下游演算法引用

POI 現在已經被多條其它演算法鏈消費：

- `AlgorithmPoiAnalysis` 會附帶 `POITemplateParam`
- `AlgorithmSFRFindROI` 會附帶 `POITemplateParam`
- `AlgorithmOLEDAOI` 也會附帶 `POITemplateParam`

因此 POI 當前是其它演算法的輸入格式之一，不是結果頁末端才出現的附屬物件。

### Flow 節點引用

`POINodeConfigurators` 說明 POI 在 Flow 裡已經成為共享節點資源：

- `POINode` 需要主模板、過濾、修正、輸出模板
- `BuildPOINode` 會同時接布點模板、回寫 POI 模板和佈局 ROI 模板
- `POIReviseNode` 會接修正標定模板
- `POIAnalysisNode` 會接 JSON 分析模板

這也說明 POI 當前是流程設計期就要選定的核心原語。

## 當前幾個最容易寫錯的點

### POI 不是單一檢測演算法的結果結構

當前它同時被用在檢測、布點、分析、AOI 和 Flow 節點中，是一套共享資料模板。

### 儲存不是隻有資料庫，也不是隻有檔案

主模板走資料庫，但 `AlgorithmPoi` 也明確支援檔案模式和外部點檔案。

### 伴生模板是當前系統的一等成員

過濾、修正、標定、輸出模板都有真實實現和編輯入口，不是註釋裡的“未來擴充套件”。

### 某些演算法是消費 POI，而不是生產 POI

像 `PoiAnalysis`、`SFR_FindROI`、`OLEDAOI` 這些鏈，本質上是在讀取和使用已有 POI 模板。

## 推薦閱讀順序

1. `Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs`
2. `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
3. `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
4. `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 繼續閱讀

- [POI 模板](../templates/poi-template.md)
- [JSON 模板](../templates/json-templates.md)
- [流程引擎](../templates/flow-engine.md)
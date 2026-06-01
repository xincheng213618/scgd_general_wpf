# ROI

本頁只描述當前倉庫裡真實存在的 ROI 相關原語，不再維護“統一 ROI 模組設計圖”式舊稿。

## 先看當前倉庫裡 ROI 實際分成哪幾支

按當前原始碼狀態，ROI 並不是一個單獨目錄下的統一庫，而是至少有三條相關分支：

1. 經典發光區定位模板，位於 `Templates/FindLightArea`
2. 影像裁剪 JSON 模板，位於 `Templates/Jsons/ImageROI`
3. ARVR 的 `SFR_FindROI` JSON 模板，位於 `Templates/Jsons/SFRFindROI`

所以這頁更像“ROI 入口地圖”，而不是“全域性 ROI 抽象類說明”。

## 當前最關鍵的檔案

- `Engine/ColorVision.Engine/Templates/FindLightArea/TemplateRoi.cs`
- `Engine/ColorVision.Engine/Templates/FindLightArea/ROIParam.cs`
- `Engine/ColorVision.Engine/Templates/FindLightArea/AlgorithmRoi.cs`
- `Engine/ColorVision.Engine/Templates/FindLightArea/DisplayRoi.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/ImageROI/TemplateImageROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/ImageROI/AlgorithmImageROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/AlgorithmSFRFindROI.cs`

## 經典 ROI 鏈當前是什麼樣

### 模板入口

當前經典 ROI 實際落在 `FindLightArea` 這組程式碼裡，而不是舊文件寫的 `Templates/ROI`。

`TemplateRoi` 的實現特徵很明確：

- `Name = FindLightArea`
- `Code = FindLightArea`
- `TemplateDicId = 31`
- 透過 `GetMysqlCommand()` 返回 `MysqlRoi`

因此這條鏈當前本質上是“發光區定位模板”，不是全系統統一 ROI 定義。

### 參數模型

`RoiParam` 當前非常直接，只暴露三項參數：

- `Threshold`
- `Times`
- `SmoothSize`

這和舊稿裡那種通用矩形 ROI 或多邊形 ROI API 不是一回事。它更像具體演算法的閾值模板，而不是一個抽象幾何物件。

### 執行與 UI

`AlgorithmRoi` 負責：

- 開啟 `TemplateRoi` 的編輯視窗
- 獲取 `DisplayRoi`
- 組裝 `Event_LightArea2_GetData` 請求

`DisplayRoi` 則承擔當前真實的使用者輸入流程：

- 選擇模板
- 選擇影像源服務
- 支援批次號、原始檔案和本地影像三種輸入
- 拉取 Raw 檔案列表並支援直接開啟

這說明當前經典 ROI 更接近“發光區檢測演算法的前端宿主”，而不是單獨的繪圖部件。

## 兩條 JSON ROI 分支

### ImageROI

`TemplateImageROI` 是 JSON 模板分支，當前：

- `Code = Image.ROI`
- `TemplateDicId = 52`
- `IsUserControl = true`

它透過 `EditTemplateJson` 承載結構化裁剪參數，而 `AlgorithmImageROI` 則釋出 `Image.ROI` 事件。

這條鏈講的是影像裁剪配置，不是經典發光區模板的復刻。

### SFR_FindROI

`TemplateSFRFindROI` 也是 JSON 模板分支，當前：

- `Code = ARVR.SFR.FindROI`
- `TemplateDicId = 36`
- `IsUserControl = true`

它在說明文字里明確給出了 `SfrRoiParam` 結構提示；`AlgorithmSFRFindROI` 則除了 JSON 模板本身，還會額外附帶 `POITemplateParam`，再發布 `ARVR.SFR.FindROI`。

這說明 ARVR 裡的“找 ROI”已經不是單純 ROI 模板，而是 ROI 與 POI 聯動的一條演算法鏈。

## 當前幾個最容易寫錯的點

### ROI 不是統一基礎庫

當前倉庫裡的 ROI 相關實現分散在經典參數模板和 JSON 模板兩條路徑中，沒有一個統一的 `ROI` 根模組負責所有場景。

### 經典 ROI 當前主要指發光區定位

如果不把 `FindLightArea` 當作主錨點，這頁很容易寫成一份不存在的“通用 ROI SDK”。

### JSON ROI 和經典 ROI 不是同一套配置模型

`TemplateImageROI`、`TemplateSFRFindROI` 都是 JSON 模板宿主，而 `TemplateRoi` 是傳統參數模板。三者不能混成一張參數列。

### 某些 ROI 鏈已經和 POI 繫結

`AlgorithmSFRFindROI` 明確要求 `TemplatePoi`。在當前 ARVR 鏈裡，ROI 和 POI 已經不是徹底分開的兩個概念層。

## 推薦閱讀順序

1. `Engine/ColorVision.Engine/Templates/FindLightArea/TemplateRoi.cs`
2. `Engine/ColorVision.Engine/Templates/FindLightArea/AlgorithmRoi.cs`
3. `Engine/ColorVision.Engine/Templates/FindLightArea/DisplayRoi.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/Jsons/ImageROI/TemplateImageROI.cs`
5. `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

## 繼續閱讀

- [POI 原語](./poi.md)
- [POI 模板](../templates/poi-template.md)
- [ARVR 模板](../templates/arvr-template.md)
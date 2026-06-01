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

## 當前幾個最容易寫錯的點

### ARVR 不是統一 schema

各子目錄共享的是模板宿主和顯示演算法風格，不是同一套參數欄位。

### 多數演算法類是宿主和命令介面卡

`AlgorithmMTF`、`AlgorithmSFR`、`AlgorithmFOV`、`AlgorithmDistortion`、`AlgorithmGhost` 主要負責開視窗、取輸入、打 MQTT 請求，而不是在本地直接完成數值計算。

### POI 在 ARVR 裡不是邊角料

至少 MTF、SFR、SFR_FindROI 當前都顯式依賴 `TemplatePoi`。如果忽略 POI，這頁就解釋不通當前執行鏈。

### 結果處理程式碼同樣重要

像 `ViewHandleMTF`、`WindowSFR`、`ViewResultDistortion` 這些結果層實現，是理解使用者最終看到什麼的重要入口，不該被舊文件省掉。

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
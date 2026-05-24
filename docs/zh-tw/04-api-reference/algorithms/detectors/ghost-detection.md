# Ghost Detection

本頁只描述當前倉庫裡真實存在的 Ghost 檢測接入鏈，不再維護“獨立 `ghost-detection` 演算法 API”式舊稿。

## 先看當前這頁實際在講什麼

按當前原始碼狀態，Ghost 檢測不是一個獨立公共演算法包，而是 `ColorVision.Engine` 裡 ARVR 模板族的一支。它當前由這幾層組成：

- Ghost 參數模板
- Ghost 演算法 UI 宿主
- 影像輸入與顏色選擇介面
- MQTT 命令打包
- 結果載入、疊加顯示和 CSV 匯出

因此這頁真正要講的是“主程式裡 Ghost 是怎樣被託管和執行的”，而不是虛構一套脫離宿主存在的 Process API。

## 當前最關鍵的檔案

- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/TemplateGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/DisplayGhost.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/ViewHandleGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgResultGhostDao.cs`

如果只是想弄清 Ghost 當前如何配置、如何傳送命令、如何顯示結果，這幾處已經覆蓋主幹。

## 當前主鏈怎麼跑

### 模板入口

`TemplateGhost` 是 Ghost 的參數模板入口。當前實現非常直接：

- 繼承 `ITemplate<GhostParam>`
- `TemplateDicId = 7`
- `Code = ghost`

這說明 Ghost 當前走的是經典強型別參數模板鏈，而不是 JSON 模板或獨立配置檔案鏈。

### 參數模型

`GhostParam` 當前暴露的是一組針對鬼影點陣檢測的參數，而不是舊稿裡那種泛化的閾值、面積、形態學開關全集。當前能直接看到的核心欄位包括：

- `Ghost_radius`
- `Ghost_cols`
- `Ghost_rows`
- `Ghost_ratioH`
- `Ghost_ratioL`

從欄位命名和描述看，這套參數更偏向“待檢測鬼影點陣”的幾何與灰度約束，而不是任意影像缺陷檢測器的通用參數列。

### 演算法宿主

`AlgorithmGhost` 當前不是底層影像處理核心，而是一個 `DisplayAlgorithmBase` 派生的宿主類。它主要負責：

- 開啟 `TemplateGhost` 的編輯視窗
- 提供 `DisplayGhost` 使用者控制元件
- 維護當前顏色選擇 `CVOLEDCOLOR`
- 把模板、顏色、裝置資訊和影像路徑打包進訊息

最終它會發布事件名為 `Ghost` 的訊息，而不是對外暴露一個統一的 `ghost-detection` 呼叫介面。

### 輸入與執行介面

`DisplayGhost` 是當前使用者真正接觸到的執行介面。它承擔的工作比舊文件裡的“輸入影像 + 參數”更具體：

- 繫結 `TemplateGhost.Params`
- 提供 `BLUE`、`GREEN`、`RED` 三種 `CVOLEDCOLOR` 選擇
- 從 `ServiceManager` 獲取影像源裝置
- 支援批次號、Raw/CIE 檔案和本地影像三種輸入路徑
- 允許重新整理裝置側 Raw/CIE 檔案列表
- 允許直接在本地或裝置側開啟影像

因此當前 Ghost 執行面本質上是一個帶裝置互動能力的 WPF 面板，不是純演算法函式入口。

### MQTT 命令鏈

`AlgorithmGhost.SendCommand(...)` 當前會打包這些資訊：

- `ImgFileName`
- `FileType`
- `DeviceCode`
- `DeviceType`
- `TemplateParam`
- `Color`

然後構造 `MsgSend` 併發布 `Ghost` 事件。

這也說明當前 Ghost 計算真正的執行端並不在這個 UI 類內部，而是在訊息鏈的另一側。

## 結果當前怎麼處理

`ViewHandleGhost` 是當前結果顯示鏈最關鍵的入口。它負責：

- 透過 `AlgResultGhostDao.Instance.GetAllByPid(...)` 載入結果明細
- 把結果列表接回 `ViewResultAlg`
- 根據 `GhostPixel` 和 `LedPixel` 在影像上繪製疊加點位
- 在左側列表中展示 `LEDCenters`、`LEDBlobGray`、`GhostAverageGray`
- 匯出 CSV

和舊稿裡那種“返回一個統一 JSON 結構體”不同，當前 Ghost 結果主要透過資料庫結果模型、影像疊加和列表檢視來呈現。

## 當前幾個最容易寫錯的點

### 它不是獨立公共 API

當前 Ghost 檢測明確屬於 ARVR 模板族的一部分，入口在 `Templates/ARVR/Ghost`，不是一個通用 `ghost-detection` 庫。

### 演算法類不是本地計算核心

`AlgorithmGhost` 當前主要負責視窗、輸入、模板和訊息組裝。把它寫成直接處理 `Mat` 的本地演算法實現，會和真實程式碼不符。

### 參數面遠比舊稿窄

當前 `GhostParam` 暴露的是點陣半徑、行列數和灰度比例上下限，不存在舊文件裡那套完整的閾值/面積/形態學大表。

### 結果展示依賴 UI 和結果處理器

真實輸出鏈是 `ViewHandleGhost` + 結果 DAO + 影像疊加，而不是單次呼叫返回一份示例 JSON。

## 推薦閱讀順序

1. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/TemplateGhost.cs`
2. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs`
3. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
4. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/DisplayGhost.xaml.cs`
5. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/ViewHandleGhost.cs`

## 繼續閱讀

- [ARVR 模板](../templates/arvr-template.md)
- [演算法系統概覽](../overview.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)
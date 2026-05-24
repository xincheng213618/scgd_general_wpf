# 通用演算法模組

本頁不再把當前倉庫描述成一個獨立的“通用演算法平台”。按原始碼現狀，它更適合作為共享演算法構件的導航頁。

## 現在這頁真正應該覆蓋什麼

當前倉庫裡反覆被多個演算法、模板和 Flow 節點複用的公共構件，主要集中在這幾組：

- ROI / 發光區定位
- POI 點集與伴生模板
- Matching 模板匹配
- JSON 形式的裁剪或尋邊模板

這些構件的共同點不是“純演算法核心”，而是都同時帶有：

- 模板編輯入口
- 顯示演算法宿主
- MQTT 命令打包
- 有時還帶結果處理或 Flow 接入

所以本頁的作用更像“去哪看哪一組共享構件”，而不是繼續抽象一套並不存在的統一框架。

## 當前最值得優先看的幾支

### ROI

經典 ROI 現在主要落在 `Templates/FindLightArea`：

- `TemplateRoi`
- `RoiParam`
- `AlgorithmRoi`
- `DisplayRoi`

它當前代表的是發光區定位模板鏈，而不是全域性通用 ROI SDK。

除此之外，還有 JSON 版 ROI 入口：

- `TemplateImageROI`
- `TemplateSFRFindROI`

如果你要看的是裁剪或 ARVR 的找 ROI，這兩支比經典 `TemplateRoi` 更貼近現狀。

### POI

POI 是當前最典型的共享原語。它至少覆蓋：

- `TemplatePoi`
- `PoiParam`
- `PoiPoint`
- `AlgorithmPOI`
- `AlgorithmBuildPoi`
- 過濾、修正、標定、輸出伴生模板

而且它會被 JSON 演算法和 Flow 節點繼續引用，所以 POI 不是單一演算法頁，而是跨模組資料結構。

### Matching

Matching 當前也是一條完整但相對精簡的共享鏈：

- `TemplateMatch`
- `MatchParam`
- `AlgorithmMatching`
- `DisplayMatching`
- `ViewHandleMatching`

`AlgorithmMatching` 當前會：

- 開啟 `TemplateMatch`
- 可選開啟 `TemplatePoi`
- 允許指定 `TemplateFile`
- 釋出 `Event_MatchTemplate`

所以 Matching 也不是隻剩一個計算函式，而是完整的模板與命令宿主。

## 當前這些共享模組怎樣串到系統裡

按當前實現，它們大體都走同一類執行模式：

1. 透過 `TemplateEditorWindow` 或自訂編輯控制元件維護模板。
2. 透過 `DisplayAlgorithmBase` 派生類暴露 UI 和命令。
3. 在演算法類裡組裝 `CVTemplateParam` 和其它輸入參數。
4. 透過 MQTT 事件把請求發給服務側。
5. 視情況再由結果處理器或 Flow 節點繼續消費。

這也是為什麼把這些模組單純寫成“演算法庫”會失真，因為當前實現裡 UI、模板和命令宿主是一體的。

## 如果現在要按需求讀原始碼

### 想看區域選擇或裁剪

優先讀 [ROI](./roi.md)。

### 想看點集模板、點集建置或 POI 複用

優先讀 [POI](./poi.md) 和 [POI 模板](../templates/poi-template.md)。

### 想看影像模板匹配

優先讀 `Engine/ColorVision.Engine/Templates/Matching/AlgorithmMatching.cs` 和 `TemplateMatch.cs`。

### 想看這些構件怎樣被編排進流程

優先讀 [流程引擎](../templates/flow-engine.md) 以及 `Templates/Flow/NodeConfigurator`。

## 當前幾個最容易寫錯的點

### 通用不等於獨立框架

這些共享模組並沒有組成一套單獨釋出的公共 SDK，而是散佈在 `ColorVision.Engine/Templates` 下、由主程式宿主統一託管。

### 共享模組並不純粹

它們通常混合了模板、UI、MQTT 訊息和結果顯示。繼續按嚴格三層架構去寫，很容易和現狀錯位。

### POI、ROI、Matching 之間有交叉引用

例如 Matching 可以繼續開啟 `TemplatePoi`，而 ARVR 的 `SFR_FindROI` 又會要求 POI 模板。這些模組不是完全彼此獨立的島。

## 繼續閱讀

- [ROI](./roi.md)
- [POI](./poi.md)
- [ARVR 模板](../templates/arvr-template.md)
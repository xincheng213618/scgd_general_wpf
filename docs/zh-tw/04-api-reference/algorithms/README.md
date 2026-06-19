# 演算法與模板概覽

本章現在收束成“模板系統與演算法接入鏈”的導讀，不再繼續維護把所有影像處理方法平鋪成百科目錄的舊寫法。

## 這一章在講什麼

這裡的“演算法”主要對應 `Engine/ColorVision.Engine/Templates/` 及其周邊接入鏈，而不是倉庫裡所有底層影像處理程式碼的總表。當前重點包括：

- 模板如何被發現、載入、管理和編輯。
- Flow 模板如何接入 `FlowEngineLib`。
- JSON 模板如何透過專門編輯器進入系統。
- ARVR、POI 等業務模板族怎樣和演算法服務對接。

如果你要找的是 OpenCV 級別的底層處理函式，入口通常不在這一章，而更接近 `Engine/cvColorVision/`、`UI/ColorVision.Core/` 或原生 DLL 側。

## 當前章節結構

### 入口頁

- [演算法系統概覽](./overview.md)：當前實現鏈的整體說明，先看這頁最省時間。
- [目前演算法模板覆蓋清單](./current-algorithm-template-coverage.md)：把 `Templates/` 實際目錄逐項映射到文件入口和後續補齊重點。

### 專題目錄

- `templates/`：模板管理、流程模板、JSON 模板、POI/ARVR、FindLightArea、JND、LED 檢測、BuzProduct、Validate、Compliance、DataLoad、Matching、SysDictionary、FocusPoints、ImageCropping、模板選單等專題頁。
- `detectors/`：少量缺陷/檢測類專題。
- `primitives/`：少量基礎構件說明。

這幾個目錄下仍保留一些歷史頁面，但本章首頁不再把它們全部當成穩定入口平鋪出來。

## 當前最值得先認識的程式碼錨點

從現狀看，模板與演算法鏈路最值得先認識的是這幾類檔案：

- `Templates/TemplateContorl.cs`：模板發現與註冊入口。
- `Templates/TemplateManagerWindow.xaml.cs`：模板管理視窗。
- `Templates/TemplateEditorWindow.xaml.cs`：模板編輯視窗。
- `Templates/Flow/TemplateFlow.cs`：流程模板與流程編輯器接入點。
- `Templates/Jsons/ITemplateJson.cs`：JSON 模板的公共裝載/匯入匯出邏輯。
- `Templates/Jsons/EditTemplateJson.xaml(.cs)`：JSON 模板編輯控制元件，負責文字/屬性兩種編輯模式。
- `Templates/POI/AlgorithmImp/AlgorithmPOI.cs`、`Templates/ARVR/*/Algorithm*.cs`：典型業務演算法 UI 與訊息組裝入口。

## 當前幾個關鍵邊界

- 很多 `Algorithm*` 類本身不是最終計算核心，它們當前更多負責收集模板參數、檔案路徑、裝置資訊，再透過 MQTT/服務鏈發出執行請求。
- `POI` 不是孤立專題，它在當前程式碼裡仍然是多個演算法族共享的上游模板和參數來源。
- `Flow` 模板雖然表現形態不同，但它仍屬於同一個 Templates 系統的一部分，不應和普通模板鏈完全割裂。
- JSON 模板和傳統強型別模板目前並存，閱讀時不要預設系統只保留一種模板定義方式。

## 推薦閱讀順序

1. 先看 [演算法系統概覽](./overview.md)，建立執行時主鏈認知。
2. 再看 [目前演算法模板覆蓋清單](./current-algorithm-template-coverage.md)，確認每個 `Templates/` 子目錄的文件入口。
3. 再對照 [Templates 模組分析](../../03-architecture/components/templates/analysis.md)，理解目錄和註冊入口。
4. 如果關注流程模板，再看 [FlowEngineLib 架構](../../03-architecture/components/engine/flow-engine.md)。
5. 最後按具體業務域進入 `templates/` 下的單頁，例如 [FindLightArea 發光區定位模板](./templates/find-light-area.md)、[JND 模板](./templates/jnd-template.md)、[LED 檢測模板](./templates/led-detection.md)、[BuzProduct 產品業務參數模板](./templates/buz-product-template.md)、[Validate 判定規則模板](./templates/validate-rules.md)、[Compliance 結果交接](./templates/compliance-results.md)、[DataLoad 資料載入模板](./templates/data-load-template.md)、[Matching 模板匹配](./templates/matching-template.md)、[SysDictionary 系統字典模板](./templates/sys-dictionary-template.md)、[FocusPoints 關注點模板](./templates/focus-points-template.md)、[ImageCropping 圖像裁剪模板](./templates/image-cropping-template.md)、[模板選單入口](./templates/template-menu-entries.md)，並始終與原始碼對照閱讀。

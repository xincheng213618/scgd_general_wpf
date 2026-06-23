# 系統架構概覽

本頁不再嘗試用一套抽象分層把整個倉庫講成標準教材，而是直接按當前程式碼倉庫的主目錄說明系統怎麼被組織起來，以及讀程式碼時通常從哪裡切入。

## 先怎麼理解這個倉庫

從當前目錄結構看，ColorVision 更接近一套以桌面主程式為核心、圍繞引擎、UI、外掛、專案包和安裝更新體系展開的 Windows WPF 平台。

最重要的幾個頂層區域是：

- `ColorVision/`：主應用入口和主視窗
- `UI/`：WPF UI 框架、主題、屬性編輯器、影像編輯器、資料庫與桌面選單等
- `Engine/`：裝置服務、模板系統、流程執行、OpenCV 整合、檔案處理
- `Plugins/`：執行時外掛擴充套件
- `Projects/`：客戶專案和定製業務組合
- `ColorVisionSetup/`：安裝器與更新相關程式
- `Web/Backend/`：外掛市場後端
- `Scripts/`：建置、打包、釋出指令碼

## 按系統角色看結構

### 主程式層

`ColorVision/` 是桌面應用入口，負責主視窗、應用啟動、全域性配置、更新入口和整體工作臺組織。

如果你在追“程式啟動後先發生什麼”，通常從這裡開始，再聯動看 `UI/` 和 `Engine/`。

### UI 層

`UI/` 不是單一專案，而是一組介面相關模組的集合。當前比較關鍵的包括：

- `ColorVision.UI/`：通用 UI 框架和選單、面板、屬性編輯器等能力
- `ColorVision.Themes/`：主題和視覺資源
- `ColorVision.ImageEditor/`：影像檢視、標註和結果展示
- `ColorVision.Database/`：資料庫瀏覽器等資料庫相關 UI 能力
- `ColorVision.UI.Desktop/`：桌面級選單和設定入口

### 引擎層

`Engine/` 是系統的業務核心，但也不是一個單專案名字空間。當前主要由幾塊組成：

- `ColorVision.Engine/`：裝置服務、模板系統、流程視窗、MQTT 與業務協調
- `FlowEngineLib/`：流程節點編輯與執行底座
- `cvColorVision/`：底層視覺處理與 OpenCV 相關整合
- `ColorVision.FileIO/`：檔案讀寫處理
- `ColorVision.ShellExtension/`：外部整合相關擴充套件

### 外掛與專案層

- `Plugins/` 提供執行時外掛擴充套件，例如 Conoscope、Spectrum、SystemMonitor 等
- `Projects/` 放客戶專案或業務打包實現，通常是把現有引擎與 UI 能力重新組合成特定方案

### 交付與外圍層

- `ColorVisionSetup/` 負責安裝與更新側程式
- `Web/Backend/` 負責外掛市場後端
- `Scripts/` 和根目錄批處理指令碼負責建置、打包和釋出

## 執行時最常見的主鏈路

如果從使用者操作一路往下看，最常見的鏈路通常是：

1. 使用者從 `ColorVision/` 的主視窗進入某個功能。
2. `UI/` 中對應視窗或面板負責展示與互動。
3. `Engine/ColorVision.Engine/` 中的裝置服務、模板或流程邏輯接手業務處理。
4. 需要流程執行時進一步呼叫 `Engine/FlowEngineLib/`。
5. 需要影像或演算法處理時繼續聯動 `Engine/cvColorVision/`、`UI/ColorVision.ImageEditor/` 或具體模板實現。
6. 如果功能來自外部擴充套件，再進入 `Plugins/` 或 `Projects/` 中的實現。

## 讀程式碼時的常見切入點

### 想理解主介面和入口

先看：

- `ColorVision/`
- `UI/ColorVision.UI/`
- `UI/ColorVision.UI.Desktop/`

### 想理解裝置、模板和流程

先看：

- `Engine/ColorVision.Engine/Services/`
- `Engine/ColorVision.Engine/Templates/`
- `Engine/FlowEngineLib/`

### 想理解影像結果和顯示

先看：

- `UI/ColorVision.ImageEditor/`
- `Engine/cvColorVision/`

### 想理解擴充套件能力

先看：

- `Plugins/`
- `Projects/`
- [外掛開發概覽](../../02-developer-guide/plugin-development/overview.md)

## 這頁不再做什麼

本頁不再繼續維護這些容易失真的內容：

- 虛構的標準化六層架構命名
- 與當前目錄不一致的模組名清單
- 泛化的“依賴注入容器”“物件池”“報告模板”等教材式概括

如果某個專題需要更細的執行時關係、流程執行鏈或模板結構，應進入對應專題頁說明，而不是在這裡一次講完。

## 繼續閱讀

- [架構執行時](./runtime.md)
- [元件互動](./component-interactions.md)
- [FlowEngineLib 架構](../components/engine/flow-engine.md)
- [Templates 架構設計](../components/templates/design.md)

## 說明

- 本頁只作為當前倉庫結構下的系統入口圖，不再繼續維護脫離程式碼目錄的抽象分層稿。

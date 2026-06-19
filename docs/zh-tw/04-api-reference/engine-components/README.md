# Engine元件概覽

本章現在只保留和當前倉庫結構能直接對上的 Engine 側模組入口，不再繼續維護“版本表 + 示例程式碼 + 統一分層藍圖”式舊稿。

## 這一章實際覆蓋什麼

`Engine/` 目錄下的程式碼並不是單一演算法庫，而是一組彼此配合的執行時模組：

- `ColorVision.Engine/`：主引擎層，承接服務、模板、MQTT、資料庫和流程接入。
- `FlowEngineLib/`：流程節點與執行控制核心。
- `cvColorVision/`：原生能力封裝與互操作橋接。
- `ColorVision.FileIO/`：影像與自訂格式檔案讀寫。
- `ST.Library.UI/`：節點編輯器與相關 UI 基礎控制元件。
- `ColorVision.ShellExtension/`：Windows Explorer 中 `.cvraw` / `.cvcie` 檔案縮略圖擴充。

因此閱讀 Engine 章節時，不要把它理解成“只有演算法實現”，它同時包含執行時編排、流程執行、底層封裝和編輯器支撐層。

## 交接先讀

如果你是接手 Engine 業務邏輯，建議先看這幾頁，再回到具體模組：

- [目前 Engine 文件覆蓋清單](./current-engine-coverage.md)：確認 `Engine/` 專案、核心業務目錄和交接頁的對應關係。
- [Engine 業務鏈路矩陣](./business-flow-matrix.md)：按業務場景定位程式碼入口、配置與驗收點。
- [Engine 業務場景交接手冊](./business-scenario-playbook.md)：按常見需求和故障描述查該去哪裡改。
- [Engine 業務交接手冊](./business-handoff.md)：從裝置資源、模板、Flow、結果到專案包的完整主鏈路。
- [Engine 執行時物件目錄](./runtime-object-map.md)：按類名查責任、來源和第一檢查點。
- [設備服務鏈路](./device-service-chain.md)：資料庫資源如何變成 `DeviceService`、MQTT 和 Flow 可選設備。
- [模板與 Flow 鏈路](./template-flow-chain.md)：模板、節點配置、Flow 保存/匯入/執行的接線方式。
- [Flow 轉換與校準節點](./flow-conversion-calibration-nodes.md)：資料轉換、影像轉換、校準、校準 ROI 和舊色差校正節點的真實入口。
- [結果展示與專案交接鏈路](./result-handoff-chain.md)：演算法結果、圖像覆蓋層和 `Projects/*` 交付邊界。

## 怎麼讀這一章

如果你第一次進入 Engine 程式碼，建議按下面順序建立認知：

1. 先看 `ColorVision.Engine`，理解服務、模板和流程是怎麼被主程式接起來的。
2. 再看 `FlowEngineLib`，理解節點執行、開始/結束鏈和流程完成事件來自哪裡。
3. 然後補 `ColorVision.FileIO` 和 `cvColorVision`，區分檔案讀寫層與原生演算法/裝置封裝層。
4. 最後再看 `ST.Library.UI`，理解流程編輯器所依賴的節點 UI 基礎設施。
5. 如果問題發生在 Explorer 檔案預覽，再看 `ColorVision.ShellExtension`；它不是主業務鏈。

## 模組地圖

### 主引擎層

- [ColorVision.Engine](./ColorVision.Engine.md)：當前系統最重要的 Engine 入口，主要關注 `Services/`、`Templates/`、`MQTT/`、`Messages/` 等目錄。
- [Engine 業務交接手冊](./business-handoff.md)：用交接視角串起裝置、模板、Flow、結果和專案包。

### 流程執行層

- [FlowEngineLib](./FlowEngineLib.md)：節點執行與流程控制核心，但它需要和 `ColorVision.Engine/Templates/Flow/` 一起看，才是完整的實際執行鏈。
- [模板與 Flow 鏈路](./template-flow-chain.md)：補充 Flow 模板、節點配置器和執行驗收。

### 底層支撐層

- [ColorVision.FileIO](./ColorVision.FileIO.md)：檔案格式、匯入匯出和相關 I/O 處理。
- [cvColorVision](./cvColorVision.md)：原生視覺能力封裝與裝置/演算法互操作橋接。

### 編輯器基礎層

- [ST.Library.UI](./ST.Library.UI.md)：流程節點編輯器和屬性面板等 UI 基礎能力。

### 外部 Shell 整合層

- [ColorVision.ShellExtension](./ColorVision.ShellExtension.md)：Explorer 縮略圖擴充、COM 註冊、OpenCvSharp runtime、註冊表和回退驗收。

## 當前幾個容易寫錯的邊界

- `ColorVision.Engine` 不是“所有演算法都在這裡算完”的單體模組，它更多是把模板、裝置、流程和訊息鏈組織起來。
- `FlowEngineLib` 不是整個流程系統的全部實現；真正進入主程式時，還要經過 `Templates/Flow/` 裡的模板和視窗層。
- `cvColorVision` 與 `ColorVision.FileIO` 都屬於支撐層，不應和模板/UI 側能力混寫成同一層。
- `ColorVision.ShellExtension` 不是 Engine 主業務鏈，只處理 Explorer 內 `.cvraw` / `.cvcie` 縮略圖預覽。

## 建議先看的原始碼錨點

如果目標是理解 Engine 側真實控制面，優先看這些程式碼比先翻舊文件更有效：

- `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
- `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
- `Engine/FlowEngineLib/FlowEngineControl.cs`
- `Engine/FlowEngineLib/Start/BaseStartNode.cs`
- `Engine/FlowEngineLib/End/CVEndNode.cs`
- `Engine/ColorVision.ShellExtension/CVThumbnailProviderBase.cs`

## 繼續閱讀

- [Templates 模組分析](../../03-architecture/components/templates/analysis.md)
- [FlowEngineLib 架構](../../03-architecture/components/engine/flow-engine.md)
- [Flow 轉換與校準節點](./flow-conversion-calibration-nodes.md)
- [ColorVision.ShellExtension](./ColorVision.ShellExtension.md)
- [系統執行時](../../03-architecture/overview/runtime.md)

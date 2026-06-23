# 模組與文件對照表

本文件只保留當前倉庫結構和仍然有效的文件入口，用於快速定位“程式碼在哪，先看什麼文件”。

## 程式碼區域到文件入口

| 程式碼區域 | 關注點 | 首選文件入口 | 補充入口 |
| --- | --- | --- | --- |
| `ColorVision/` | 主程式入口、主視窗、應用啟動 | [入門指南](../../00-getting-started/README.md) | [主視窗導覽](../../01-user-guide/interface/main-window.md) |
| `UI/` | WPF UI 框架、主題、編輯器 | [UI 元件總覽](../../04-api-reference/ui-components/README.md) | [使用者指南](../../01-user-guide/README.md) |
| `UI/ColorVision.SocketProtocol/` | TCP 服務、JSON/Text 分發、訊息歷史、管理視窗 | [ColorVision.SocketProtocol](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) | [Socket 通訊模組最佳化路線](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md) |
| `Engine/ColorVision.Engine/Services/` | 裝置服務、服務協調 | [裝置服務概覽](../../01-user-guide/devices/overview.md) | [Engine 開發指南](../../02-developer-guide/engine-development/README.md) |
| `Engine/ColorVision.Engine/Templates/` | 模板系統、參數化演算法、結果處理 | [演算法總覽](../../04-api-reference/algorithms/README.md) | [目前演算法模板覆蓋清單](../../04-api-reference/algorithms/current-algorithm-template-coverage.md)、[BuzProduct 產品業務參數模板](../../04-api-reference/algorithms/templates/buz-product-template.md)、[Validate 判定規則模板](../../04-api-reference/algorithms/templates/validate-rules.md)、[Compliance 結果交接](../../04-api-reference/algorithms/templates/compliance-results.md)、[DataLoad 資料載入模板](../../04-api-reference/algorithms/templates/data-load-template.md)、[Matching 模板匹配](../../04-api-reference/algorithms/templates/matching-template.md)、[SysDictionary 系統字典模板](../../04-api-reference/algorithms/templates/sys-dictionary-template.md)、[FocusPoints 關注點模板](../../04-api-reference/algorithms/templates/focus-points-template.md)、[ImageCropping 圖像裁剪模板](../../04-api-reference/algorithms/templates/image-cropping-template.md)、[模板選單入口](../../04-api-reference/algorithms/templates/template-menu-entries.md)、[Templates 架構設計](../../03-architecture/components/templates/design.md) |
| `Engine/FlowEngineLib/` | 流程節點、執行模型、視覺化流程 | [FlowEngineLib 架構](../../03-architecture/components/engine/flow-engine.md) | [FlowNode 開發](../../04-api-reference/extensions/flow-node.md) |
| `Engine/cvColorVision/` | OpenCV 整合、底層視覺處理 | [Engine 元件總覽](../../04-api-reference/engine-components/README.md) | [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md) |
| `Engine/ColorVision.ShellExtension/` | `.cvraw` / `.cvcie` 檔案的 Explorer 縮圖擴充 | [ColorVision.ShellExtension](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) | [Engine 元件總覽](../../04-api-reference/engine-components/README.md) |
| `Plugins/` | 執行時外掛和擴充套件能力 | [現有外掛能力說明](../../04-api-reference/plugins/README.md) | [當前外掛文件覆蓋清單](../../04-api-reference/plugins/current-plugin-coverage.md)、[外掛能力與交接矩陣](../../04-api-reference/plugins/plugin-capability-matrix.md)、[外掛開發概覽](../../02-developer-guide/plugin-development/overview.md) |
| `Projects/` | 客戶專案、定製業務拼裝、對接示例 | [專案說明](../../00-projects/README.md) | [專案包總覽](../../04-api-reference/projects/README.md)、[目前專案文件覆蓋清單](../../04-api-reference/projects/current-project-coverage.md)、[專案包能力與交接矩陣](../../04-api-reference/projects/project-capability-matrix.md)、[專案包執行與交接場景手冊](../../04-api-reference/projects/project-package-playbook.md) |
| `ColorVisionSetup/` | 安裝器和更新流程 | [部署概覽](../../02-developer-guide/deployment/overview.md) | [自動更新系統](../../02-developer-guide/deployment/auto-update.md) |
| `Web/Backend/` | 外掛市場後端 | [外掛市場後端](../../02-developer-guide/backend/README.md) | [開發指南](../../02-developer-guide/README.md) |
| `Scripts/` | 建置、打包、釋出指令碼 | [建置與釋出指令碼](../../02-developer-guide/scripts/README.md) | [部署概覽](../../02-developer-guide/deployment/overview.md) |
| `Test/` | xUnit、native helper、後端和指令碼驗證 | [測試與驗證交接手冊](../../02-developer-guide/testing.md) | [開發指南](../../02-developer-guide/README.md) |
| `docs/` | 當前文件站原始碼 | [附錄與資源](../README.md) | 當前文件 |

## 按任務查詢

### 想新增裝置服務

1. 先看 [裝置服務概覽](../../01-user-guide/devices/overview.md)
2. 再看 [Engine 開發指南](../../02-developer-guide/engine-development/README.md)
3. 最後進入 [Engine 元件總覽](../../04-api-reference/engine-components/README.md) 找具體模組頁

### 想開發外掛

1. [擴充套件性概覽](../../02-developer-guide/core-concepts/extensibility.md)
2. [外掛開發概覽](../../02-developer-guide/plugin-development/overview.md)
3. [外掛開發入門](../../02-developer-guide/plugin-development/getting-started.md)

### 想維護客戶專案

1. [專案說明](../../00-projects/README.md)
2. [專案包總覽](../../04-api-reference/projects/README.md)
3. [目前專案文件覆蓋清單](../../04-api-reference/projects/current-project-coverage.md)
4. [專案包能力與交接矩陣](../../04-api-reference/projects/project-capability-matrix.md)
5. [專案包執行與交接場景手冊](../../04-api-reference/projects/project-package-playbook.md)

### 想理解模板或流程

1. [演算法總覽](../../04-api-reference/algorithms/README.md)
2. [目前演算法模板覆蓋清單](../../04-api-reference/algorithms/current-algorithm-template-coverage.md)
3. [FlowEngineLib 架構](../../03-architecture/components/engine/flow-engine.md)
4. [Templates 架構設計](../../03-architecture/components/templates/design.md)
5. [Templates API 參考](../../04-api-reference/algorithms/templates/api-reference.md)
6. 具體交接頁可看 [FindLightArea 發光區定位模板](../../04-api-reference/algorithms/templates/find-light-area.md)、[JND 模板](../../04-api-reference/algorithms/templates/jnd-template.md)、[LED 檢測模板](../../04-api-reference/algorithms/templates/led-detection.md)、[BuzProduct 產品業務參數模板](../../04-api-reference/algorithms/templates/buz-product-template.md)、[Validate 判定規則模板](../../04-api-reference/algorithms/templates/validate-rules.md)、[Compliance 結果交接](../../04-api-reference/algorithms/templates/compliance-results.md)、[DataLoad 資料載入模板](../../04-api-reference/algorithms/templates/data-load-template.md)、[Matching 模板匹配](../../04-api-reference/algorithms/templates/matching-template.md)、[SysDictionary 系統字典模板](../../04-api-reference/algorithms/templates/sys-dictionary-template.md)、[FocusPoints 關注點模板](../../04-api-reference/algorithms/templates/focus-points-template.md)、[ImageCropping 圖像裁剪模板](../../04-api-reference/algorithms/templates/image-cropping-template.md)、[模板選單入口](../../04-api-reference/algorithms/templates/template-menu-entries.md)

### 想改 UI 或屬性編輯

1. [使用者指南](../../01-user-guide/README.md)
2. [UI 元件總覽](../../04-api-reference/ui-components/README.md)
3. [屬性編輯器](../../01-user-guide/interface/property-editor.md)

### 想看建置、釋出和更新

1. [部署概覽](../../02-developer-guide/deployment/overview.md)
2. [自動更新系統](../../02-developer-guide/deployment/auto-update.md)
3. [建置與釋出指令碼](../../02-developer-guide/scripts/README.md)

### 想選擇測試和驗收命令

1. [測試與驗證交接手冊](../../02-developer-guide/testing.md)
2. 按變更模組選擇 `Test/ColorVision.UI.Tests/`、`Test/opencv_helper_test/`、後端測試、指令碼測試或 `npm run docs:build`

## 使用原則

- 先從章節首頁進入，再跳轉到具體專題頁。
- 歷史草案、孤立文件和舊路徑頁面不再作為主入口。
- 如果找不到完全對應的專題頁，優先回到總覽頁，而不是繼續依賴舊目錄命名。

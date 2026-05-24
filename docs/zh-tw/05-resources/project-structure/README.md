# 專案結構總覽

本文件用於快速說明當前倉庫的主目錄分工，並給出每個目錄最合適的文件入口。

## 主目錄分割槽

| 目錄 | 作用 | 建議先看 |
| --- | --- | --- |
| `ColorVision/` | 主 WPF 應用入口與主視窗 | [入門指南](../../00-getting-started/README.md) / [主視窗導覽](../../01-user-guide/interface/main-window.md) |
| `UI/` | UI 框架、主題、屬性編輯器、影像編輯器 | [UI 元件總覽](../../04-api-reference/ui-components/README.md) |
| `UI/ColorVision.SocketProtocol/` | 本地 TCP 服務、訊息歷史和管理視窗 | [ColorVision.SocketProtocol](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) / [Socket 通訊最佳化路線](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md) |
| `Engine/` | 核心引擎、裝置服務、模板系統、流程執行 | [Engine 開發指南](../../02-developer-guide/engine-development/README.md) / [Engine 元件總覽](../../04-api-reference/engine-components/README.md) |
| `Plugins/` | 執行時外掛和擴充套件能力 | [外掛開發概覽](../../02-developer-guide/plugin-development/overview.md) |
| `Projects/` | 客戶專案包和業務定製實現 | [元件互動](../../03-architecture/overview/component-interactions.md) |
| `Backend/marketplace/` | 外掛市場後端服務 | [外掛市場後端](../../02-developer-guide/backend/README.md) |
| `Scripts/` | 建置、打包、釋出指令碼 | [建置與釋出指令碼](../../02-developer-guide/scripts/README.md) |
| `ColorVisionSetup/` | 安裝器與更新程式 | [自動更新系統](../../02-developer-guide/deployment/auto-update.md) |
| `Test/` | 測試專案 | [開發指南](../../02-developer-guide/README.md) |
| `docs/` | VitePress 文件原始碼 | 當前文件 / [模組與文件對照表](./module-documentation-map.md) |

## 按角色閱讀

### 新使用者或實施同學

1. [入門指南](../../00-getting-started/README.md)
2. [使用者指南](../../01-user-guide/README.md)
3. [常見問題](../../01-user-guide/troubleshooting/common-issues.md)

### 引擎或演算法開發

1. [架構設計](../../03-architecture/README.md)
2. [Engine 開發指南](../../02-developer-guide/engine-development/README.md)
3. [演算法總覽](../../04-api-reference/algorithms/README.md)

### 外掛開發

1. [擴充套件性概覽](../../02-developer-guide/core-concepts/extensibility.md)
2. [外掛開發概覽](../../02-developer-guide/plugin-development/overview.md)
3. [標準外掛專題](../../04-api-reference/plugins/standard-plugins/pattern.md)

### 文件維護

1. [附錄與資源](../README.md)
2. [模組與文件對照表](./module-documentation-map.md)

## 說明

- 這裡提供的是“從哪裡開始看”的入口，不替代詳細 API 或專題頁。
- 若某個目錄缺少獨立文件，優先從相鄰章節的總覽頁進入，而不是繼續擴散新的散頁索引。

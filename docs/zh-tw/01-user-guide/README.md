# 使用者指南

本章節按“先會用，再深入”的順序整理，優先保留日常使用相關頁面。

## 章節入口

### 介面與基礎互動

- [主視窗導覽](./interface/main-window.md)
- [屬性編輯器](./interface/property-editor.md)
- [日誌檢視器](./interface/log-viewer.md)
- [終端](./interface/terminal.md)

### 影像編輯器

- [影像編輯器概覽](./image-editor/overview.md)

### 裝置管理

- [裝置服務概覽](./devices/overview.md)
- [新增與配置裝置](./devices/configuration.md)
- [相機服務](./devices/camera.md)
- [相機管理](./devices/camera-management.md)
- [相機參數配置](./devices/camera-configuration.md)
- [校準服務](./devices/calibration.md)
- [電機服務](./devices/motor.md)
- [SMU 服務](./devices/smu.md)
- [流程裝置服務](./devices/flow-device.md)
- [檔案伺服器](./devices/file-server.md)

### 工作流程

- [工作流程概覽](./workflow/README.md)
- [流程設計](./workflow/design.md)
- [流程執行與除錯](./workflow/execution.md)

### 資料管理

- [資料管理概覽](./data-management/README.md)
- [資料庫操作](./data-management/database.md)
- [資料匯出與匯入](./data-management/export-import.md)

### 故障排查

- [常見問題](./troubleshooting/common-issues.md)

## 推薦閱讀路線

1. 先看 [主視窗導覽](./interface/main-window.md)，瞭解主介面佈局。
2. 再看 [屬性編輯器](./interface/property-editor.md) 和 [影像編輯器概覽](./image-editor/overview.md)，建立基本操作路徑。
3. 涉及硬體時進入 [裝置服務概覽](./devices/overview.md) 和對應裝置專題頁。
4. 需要自動化時進入 [工作流程概覽](./workflow/README.md)。
5. 遇到異常先查 [常見問題](./troubleshooting/common-issues.md)。

## 章節邊界

- 偏實現和擴充套件機制的內容已經移到 [開發指南](../02-developer-guide/README.md)。
- 偏類庫、介面和模組級說明的內容已經移到 [API 參考](../04-api-reference/README.md)。
- 需要整體理解系統設計時，直接進入 [架構設計](../03-architecture/README.md)。

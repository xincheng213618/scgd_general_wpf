# 使用手冊操作工作流矩陣

本頁面向操作員、測試工程師和現場交付人員，用「我要完成什麼操作」來定位文件入口。它不講源碼實現，也不替代具體裝置頁，而是把日常操作、完成標準和失敗時第一檢查點放在同一張表裡。

## 什麼時候先看本頁

| 場景 | 本頁能幫你做什麼 |
| --- | --- |
| 第一次上機 | 確認安裝、啟動、主視窗、裝置、流程、資料的閱讀順序 |
| 現場交付 | 按專案、裝置、流程、匯出、外部系統逐項驗收 |
| 產線操作 | 找到日常運行、切換專案、查看結果、匯出資料的入口 |
| 異常排查 | 先定位是介面、裝置、流程、資料還是外部系統問題 |

## 按操作目標查入口

| 操作目標 | 首選入口 | 關鍵動作 | 完成標準 | 失敗先查 |
| --- | --- | --- | --- | --- |
| 安裝並首次啟動 | [安裝指南](../00-getting-started/installation.md)、[首次執行](../00-getting-started/first-steps.md) | 安裝環境、啟動主程式、確認配置和日誌目錄 | 主視窗能開啟，日誌無啟動級錯誤 | 系統要求、缺 DLL、權限、日誌 |
| 熟悉主介面與 UI 元件 | [主視窗導覽](./interface/main-window.md)、[UI 元件使用手冊](./interface/ui-component-handbook.md) | 確認菜單、狀態列、設定、日誌、資料庫、Socket、調度入口 | 能按元件找到裝置、流程、插件、資料和診斷入口 | 插件/專案菜單、權限、語言配置、狀態列 Provider |
| 修改配置參數 | [屬性編輯器](./interface/property-editor.md) | 打開配置物件、修改欄位、保存並重啟驗證 | 欄位保存後重啟仍存在 | 配置路徑、只讀欄位、資料型別、權限 |
| 查看日誌與現場錯誤 | [日誌檢視器](./interface/log-viewer.md) | 按時間和等級查啟動、裝置、流程、匯出錯誤 | 能定位第一條有意義異常 | 日誌等級、日誌目錄、是否看錯插件或服務 |
| 新增或配置裝置 | [新增與配置裝置](./devices/configuration.md)、[裝置服務概覽](./devices/overview.md) | 新增裝置資源，填通信/路徑參數，保存刷新 | 裝置出現在列表，狀態能刷新 | 裝置類型、驅動、端口/IP、服務是否啟用 |
| 使用相機取圖 | [相機服務](./devices/camera.md)、[相機管理](./devices/camera-management.md)、[相機參數配置](./devices/camera-configuration.md) | 連接相機，設曝光/增益，拍照或預覽 | 能得到圖像並打開 | 實體相機、驅動、曝光、檔案伺服器 |
| 設計自動化流程 | [工作流程概覽](./workflow/README.md)、[流程設計](./workflow/design.md) | 加節點、連線、綁定裝置/模板、保存 | 流程能保存並重新打開 | 節點參數、裝置清單、模板名、保存路徑 |
| 執行和除錯流程 | [流程執行與除錯](./workflow/execution.md) | 選流程、運行、觀察節點狀態和結果 | 能看到完成狀態或第一個失敗節點 | 起始節點、裝置狀態、模板綁定、日誌 |
| 打開圖像和 overlay | [影像編輯器概覽](./image-editor/overview.md) | 打開結果圖，查看縮放、圖層、ROI/POI/偽彩 | 圖像、圖層和標註顯示正常 | 檔案路徑、圖片是否寫完、overlay 座標 |
| 查資料庫和歷史結果 | [資料管理概覽](./data-management/README.md)、[資料庫操作](./data-management/database.md) | 打開資料庫或結果查詢，按 SN/時間篩選 | 能查到批次、流程、結果或專案資料 | MySQL/SQLite 連線、查詢條件、批次號 |
| 匯出或匯入資料 | [資料匯出與匯入](./data-management/export-import.md) | 選匯出目錄、格式和範圍 | CSV/Excel/PDF/圖片存在且欄位正確 | 路徑權限、欄位映射、專案 exporter |
| 運行客戶專案包 | [專案說明](../00-projects/README.md)、[專案包能力與交接矩陣](../04-api-reference/projects/project-capability-matrix.md) | 打開專案視窗，輸入 SN，選流程組或模板，運行 | 專案完成並生成客戶結果 | 專案配置、流程組、Recipe/Fix、Socket/MES |
| 使用插件能力 | [現有插件能力說明](../04-api-reference/plugins/README.md)、[插件能力與交接矩陣](../04-api-reference/plugins/plugin-capability-matrix.md) | 打開插件視窗，連接設備或執行功能 | 插件菜單、視窗、結果和匯出正常 | manifest、插件 DLL、設備依賴、管理員權限 |
| 外部系統聯機測試 | [專案包能力與交接矩陣](../04-api-reference/projects/project-capability-matrix.md)、[SocketProtocol](../04-api-reference/ui-components/ColorVision.SocketProtocol.md) | 確認協議、端口、事件/命令、SN、返回欄位 | 外部系統能觸發並收到結果 | 端口占用、協議模式、Socket/MES/Modbus 配置 |
| 常見問題排查 | [常見問題](./troubleshooting/common-issues.md) | 按現象分類，先定位入口，再查日誌和配置 | 能找到下一步明確檢查項 | 日誌、配置、裝置、流程、專案邊界 |

## 按角色看日常流程

| 角色 | 日常動作 | 最常用文件 |
| --- | --- | --- |
| 操作員 | 開主程式、選專案/流程、輸入 SN、運行、查看 PASS/FAIL、匯出結果 | 本頁、主視窗導覽、專案說明、資料匯出 |
| 測試工程師 | 配裝置、調相機、調流程、確認結果欄位、對比歷史資料 | 裝置服務、流程設計/執行、影像編輯器、資料管理 |
| 現場交付人員 | 安裝部署、插件/專案包驗收、Socket/MES 聯調、培訓操作員 | 安裝指南、本頁、專案矩陣、插件矩陣、常見問題 |
| 維護開發人員 | 判斷問題屬於 UI、Engine、插件還是專案包，再進入模組參考 | 本頁、UI 元件使用手冊、Engine 矩陣、UI 元件目錄 |

## 排查分流

| 現象 | 先判斷 | 下一步 |
| --- | --- | --- |
| 菜單或視窗沒有出現 | 插件/專案包是否載入 | 看插件矩陣、專案矩陣、日誌 |
| 裝置不在線 | 裝置服務還是實體硬體問題 | 看裝置頁、驅動、端口/IP、服務日誌 |
| 流程啟動但不結束 | 流程節點或裝置命令卡住 | 看流程執行頁和第一個未完成節點 |
| 結果有但匯出為空 | 專案結果映射問題 | 看專案包 Process/Recipe/Fix/exporter |
| 圖像能打開但沒有 overlay | 顯示結果處理問題 | 看影像編輯器和 Engine 結果展示鏈路 |
| 外部系統收不到結果 | 協議、端口或專案視窗狀態問題 | 看專案矩陣、SocketProtocol、日誌 |

## 章節邊界

- 操作步驟和現場檢查留在使用手冊。
- 代碼結構、業務鏈路和擴展點進入 [模組參考](../04-api-reference/README.md)。
- 專案包業務邏輯進入 [專案說明](../00-projects/README.md)。
- 插件開發進入 [插件開發手冊](../02-developer-guide/plugin-development/README.md)。
- UI DLL 發版進入 [UI 元件與 DLL 發布](../04-api-reference/ui-components/README.md)。


# 專案說明

本章說明 `Projects/` 目錄中的客戶專案包。這些專案通常會像外掛一樣載入到主程式，但它們的核心是客戶業務流程：測試順序、Recipe/Fix、外部協議、結果輸出和現場交付。

第一次接手時，先看本頁建立專案地圖，再讀 [專案包能力與交接矩陣](../04-api-reference/projects/project-capability-matrix.md)、[專案包執行與交接場景手冊](../04-api-reference/projects/project-package-playbook.md) 和 [專案包發布證據與版本核查表](../04-api-reference/projects/project-release-evidence.md)。如果要理解共用執行鏈，再看 [專案包交接手冊](../04-api-reference/projects/project-handoff.md)，最後進入具體專案頁。

## 目前專案地圖

| 專案 | 業務定位 | 詳細文件 |
| --- | --- | --- |
| ProjectARVR | 早期 AR/VR 光學測試，重點是固定 PG 切圖、Socket 事件與 `ObjectiveTestResult` 彙總 | [ProjectARVR](../04-api-reference/projects/project-arvr.md) |
| ProjectARVRLite | 輕量 AR/VR 快速測試，重點是可配置測項、預處理、Socket 切圖和 CSV | [ProjectARVRLite](../04-api-reference/projects/project-arvr-lite.md) |
| ProjectARVRPro | 目前主要 AR/VR 專業專案，包含流程組、Recipe、切圖、Socket 和多種輸出 | [ProjectARVRPro](../04-api-reference/projects/project-arvr-pro.md) |
| ProjectARVRPro.IntegrationDemo | 給客戶、MES、PLC 或上位機驗證 TCP/JSON 協議的對接示例 | [ARVRPro 對接 Demo](../04-api-reference/projects/project-arvr-pro-integration-demo.md) |
| ProjectBlackMura | 顯示面板 Black Mura 檢測，重點是 PG 串口切圖、五色流程和 Excel 報告 | [ProjectBlackMura](../04-api-reference/projects/project-black-mura.md) |
| ProjectHeyuan | 河源精電客製四點 WBRO 色彩/亮度測試，含 STX/ETX 串口與 CSV 留痕 | [ProjectHeyuan](../04-api-reference/projects/project-heyuan.md) |
| ProjectKB | 鍵盤背光亮度、均勻性與自動修正，含 Modbus、MES DLL、CSV/summary | [ProjectKB](../04-api-reference/projects/project-kb.md) |
| ProjectLUX | LUX 光學自動化專案，覆蓋亮度、色彩、MTF、畸變、光心、VID 和光通量 | [ProjectLUX](../04-api-reference/projects/project-lux.md) |
| ProjectShiyuan | 視源客製 JND/POI 匯出與固定圖像後處理 | [ProjectShiyuan](../04-api-reference/projects/project-shiyuan.md) |

## 閱讀順序

1. [專案包能力與交接矩陣](../04-api-reference/projects/project-capability-matrix.md)：橫向比較觸發方式、輸出和交付風險。
2. [目前專案文件覆蓋清單](../04-api-reference/projects/current-project-coverage.md)：確認每個 `Projects/<Name>/` 都有對應文件。
3. [專案包執行與交接場景手冊](../04-api-reference/projects/project-package-playbook.md)：按現場問題排查觸發、流程、Recipe、輸出和打包。
4. [專案包發布證據與版本核查表](../04-api-reference/projects/project-release-evidence.md)：發版、替換、回退時記錄版本、包內容、配置、協議和結果樣例。
5. [專案包交接手冊](../04-api-reference/projects/project-handoff.md)：理解共用執行鏈。
6. 對應專案頁：確認特殊協議、結果欄位和維護邊界。

## 專案包和通用外掛的邊界

| 類型 | 目錄 | 重點 |
| --- | --- | --- |
| 通用外掛 | `Plugins/<Name>/` | 提供可重用工具、服務或管理視窗 |
| 客戶專案包 | `Projects/<Name>/` | 組合 Engine、Flow、Recipe、協議和輸出，交付客戶生產流程 |

如果改動只服務某個客戶流程，優先放在專案包；只有多個專案都要複用時，才考慮下沉到 Engine、UI 或通用外掛。

## 維護要求

- 新增 `Projects/<Name>/` 後，必須補本頁、專案包總覽、覆蓋清單和具體專案頁。
- 修改外部協議、結果出口、流程組、Recipe/Fix 或交付驗收時，同步更新專案頁、矩陣、場景手冊和發布證據頁。
- 專案文件只寫目前原始碼能對上的行為，不把客戶歷史口頭流程寫成系統承諾。

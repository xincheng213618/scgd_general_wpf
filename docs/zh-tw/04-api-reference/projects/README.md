# 專案包總覽

`Projects/` 放的是客戶專案、業務方案包和對接示例。它們通常會被打包到主程式的 `Plugins/<Name>/` 目錄，但文件上要和通用外掛分開：專案包更關注客戶流程、Recipe/Fix、Socket/MES/串口協議和結果輸出。

接手時建議先看 [專案包能力與交接矩陣](./project-capability-matrix.md)，再用 [專案包執行與交接場景手冊](./project-package-playbook.md) 處理具體問題。發版、現場替換或回退時，用 [專案包發布證據與版本核查表](./project-release-evidence.md) 留下版本、包內容、配置和驗收證據。最後讀 [專案包交接手冊](./project-handoff.md) 和具體專案頁。[目前專案文件覆蓋清單](./current-project-coverage.md) 用於確認文件是否覆蓋所有真實專案目錄。

## 目前專案總覽

| 專案 | 原始碼目錄 | manifest Id | 業務定位 | 文件 |
| --- | --- | --- | --- | --- |
| ProjectARVR | `Projects/ProjectARVR/` | `ProjectARVR` | 固定 PG 切圖、Socket 事件、產品結果彙總 | [詳細](./project-arvr.md) |
| ProjectARVRLite | `Projects/ProjectARVRLite/` | `ProjectARVRLite` | 可配置測項、預處理、Socket 切圖、CSV | [詳細](./project-arvr-lite.md) |
| ProjectARVRPro | `Projects/ProjectARVRPro/` | `ProjectARVRPro` | AR/VR 流程組、Recipe、Socket 與客製輸出 | [詳細](./project-arvr-pro.md) |
| ProjectARVRPro.IntegrationDemo | `Projects/ProjectARVRPro.IntegrationDemo/` | 無 manifest | 客戶 TCP/JSON 對接示例 | [詳細](./project-arvr-pro-integration-demo.md) |
| ProjectBlackMura | `Projects/ProjectBlackMura/` | `ProjectBlackMura` | PG 串口切圖、五色流程、Excel 報告 | [詳細](./project-black-mura.md) |
| ProjectHeyuan | `Projects/ProjectHeyuan/` | `ProjectHeyuan` | STX/ETX 串口、WBRO 四點測試、CSV 上傳 | [詳細](./project-heyuan.md) |
| ProjectKB | `Projects/ProjectKB/` | `ProjectKB` | Modbus、MES DLL、背光修正、CSV/summary | [詳細](./project-kb.md) |
| ProjectLUX | `Projects/ProjectLUX/` | `ProjectLUX` | 亮度、色彩、MTF、畸變等光學自動化 | [詳細](./project-lux.md) |
| ProjectShiyuan | `Projects/ProjectShiyuan/` | `ProjectShiyuan` | JND/POI 匯出、固定圖像後處理 | [詳細](./project-shiyuan.md) |

## 打包命令

```powershell
Scripts\package_project.bat ProjectLUX --no-upload
```

底層流程與外掛打包類似，會呼叫 `Scripts/package_cvxp.py`，收集輸出 DLL、README、CHANGELOG、manifest 和 PackageIcon，生成 `.cvxp`。

發版、替換、回退證據見 [專案包發布證據與版本核查表](./project-release-evidence.md)。

## 維護要求

- 每個 `Projects/<Name>/` 都要有專案 README、docs 站點頁和覆蓋清單列。
- 變更 manifest、菜單、Socket/MES/串口事件、Recipe 欄位、結果欄位或打包內容時，同步更新對應專案頁和 [專案包發布證據與版本核查表](./project-release-evidence.md)。
- 專案頁應描述業務鏈、配置、協議、輸出和排查路徑，不只列目錄。

# 現場操作驗收清單

本頁用於首次交付、版本升級、現場復測或培訓操作員時，逐項確認 ColorVision 是否真的可用。它把 UI、裝置、流程、資料、外部系統、插件、專案包和回退證據串成一張驗收表。

如果不知道從哪裡開始，先看 [使用手冊操作工作流矩陣](./operation-workflow-matrix.md)。某項失敗時，再進入對應專題頁。

## 驗收總表

| 驗收項 | 最小動作 | 通過標準 | 失敗先看 |
| --- | --- | --- | --- |
| 主程式啟動 | 啟動 ColorVision，打開主視窗 | 主視窗、菜單、狀態列、日誌入口可見 | [主視窗導覽](./interface/main-window.md)、[日誌檢視器](./interface/log-viewer.md) |
| UI 入口 | 打開設定、日誌、資料庫、Socket、調度、插件市場 | 每個視窗能打開且無啟動級錯誤 | [UI 元件使用手冊](./interface/ui-component-handbook.md) |
| 配置保存 | 修改一個安全配置，保存並重啟 | 重啟後值仍在，服務狀態正確 | [屬性編輯器](./interface/property-editor.md) |
| 裝置服務 | 檢查相機/電機/SMU/檔案服務等關鍵裝置 | 裝置存在、狀態刷新、最小動作成功 | [裝置服務概覽](./devices/overview.md) |
| 相機取圖 | 拍一張圖或打開即時預覽 | 圖像生成並能打開 | [相機服務](./devices/camera.md)、[影像編輯器](./image-editor/overview.md) |
| 流程設計 | 打開現場流程模板 | 起始節點和關鍵參數正確 | [流程設計](./workflow/design.md) |
| 流程執行 | 跑一條最小流程或專案流程 | 能完成或定位第一個失敗節點 | [流程執行與除錯](./workflow/execution.md) |
| 圖像與 overlay | 打開結果圖並查看 ROI/POI/overlay | 圖像、圖層、座標對齊 | [影像編輯器](./image-editor/overview.md) |
| 資料落庫 | 按 SN、時間或批次查一條結果 | SQLite/MySQL 有記錄且核心欄位完整 | [資料庫操作](./data-management/database.md) |
| 文件匯出 | 匯出 CSV/Excel/PDF/圖片或專案結果 | 文件存在且欄位符合客戶格式 | [資料匯出與匯入](./data-management/export-import.md) |
| Socket/MES/Modbus | 發一條現場最小命令 | 外部系統能觸發並收到狀態/資料 | [SocketProtocol](../04-api-reference/ui-components/ColorVision.SocketProtocol.md) |
| 插件能力 | 打開現場插件並執行最小功能 | 菜單、視窗、設備連線、結果/匯出正常 | [現有插件能力說明](../04-api-reference/plugins/README.md) |
| 專案包流程 | 打開專案，輸入 SN，跑最小流程 | 客戶結果和回傳符合專案頁 | [專案說明](../00-projects/README.md) |
| 回退證據 | 找到上一版包、配置和資料庫備份 | 現場可退回上一套可用狀態 | 插件/專案包發布證據頁 |

## 裝置驗收

| 檢查項 | 通過標準 |
| --- | --- |
| 裝置資源 | 關鍵裝置已建立，名稱和 Code 能區分現場真實裝置 |
| 通信參數 | IP、端口、串口、波特率、設備號、檔案路徑與現場一致 |
| 最小動作 | 相機能拍照，電機能移動或回零，SMU 能讀數，檔案服務能下載/上傳 |
| 流程引用 | 流程節點或專案視窗能選到正確裝置 |
| 日誌證據 | 連線、超時、驅動、權限錯誤已處理或記錄 |

手動裝置頁可用但流程失敗時，先查節點綁定和模板參數；手動頁也失敗時，先查硬體、驅動、端口/IP 和服務配置。

## 流程與資料驗收

| 檢查項 | 通過標準 |
| --- | --- |
| Flow 模板 | 當前模板名稱與現場要求一致 |
| 起始節點 | 有明確起點，執行後節點狀態刷新 |
| 關鍵輸入 | 裝置、模板、圖片、SN、批次或專案配置能讀取 |
| 失敗定位 | 能找到第一個失敗節點和對應日誌 |
| 結果回看 | 結果列表、圖像、資料庫或匯出文件能對上同一輪 |

| 交付物 | 驗收方式 | 重點 |
| --- | --- | --- |
| SQLite/MySQL | 按 SN、時間、批次查詢 | 批次、模板、結果欄位 |
| CSV/Excel/PDF | 打開文件核對欄位和單位 | 欄位順序、PASS/FAIL、舊格式兼容 |
| 圖像/overlay | 打開結果圖和標註 | 點位、框線、圖層座標 |
| Socket/MES 回傳 | 保存請求和回應樣例 | 狀態碼、錯誤訊息、`Data` 欄位 |

匯出為空時，不要只重試匯出按鈕。先確認源資料已落庫，再確認目前批次和匯出對象一致。

## 外部系統驗收

| 類型 | 最小證據 |
| --- | --- |
| JSON Socket | `EventName`、SN、request JSON、response JSON、專案視窗狀態 |
| 文字 Socket | 原始命令，例如 `T00XX,SN;`，返回碼和資料 |
| MES/串口 | STX/ETX 原始報文、設備號、返回碼、超時 |
| Modbus | IP、端口、寄存器地址、觸發值、完成回寫值 |
| 檔案伺服器 | 請求路徑、返回文件列表、下載/上傳目標路徑 |

## 交付記錄模板

```text
site/customer:
host version:
project package:
plugin package:
config folder:
device smoke result:
workflow smoke result:
image/overlay result:
database query result:
export file sample:
external protocol sample:
known failures:
rollback package/config:
operator trained:
owner/date:
```

## 繼續閱讀

- [使用手冊操作工作流矩陣](./operation-workflow-matrix.md)
- [UI 元件使用手冊](./interface/ui-component-handbook.md)
- [裝置服務概覽](./devices/overview.md)
- [流程執行與除錯](./workflow/execution.md)
- [資料管理概覽](./data-management/README.md)
- [專案說明](../00-projects/README.md)
- [現有插件能力說明](../04-api-reference/plugins/README.md)


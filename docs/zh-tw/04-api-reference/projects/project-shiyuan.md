# ProjectShiyuan

`Projects/ProjectShiyuan/` 是視源客製專案包，執行時載入為 `ProjectShiyuan.dll`。

## 執行身份

| 欄位 | 值 |
| --- | --- |
| `Id` | `ProjectShiyuan` |
| `name` | `視源項目` |
| `version` | `1.0` |
| `dllpath` | `ProjectShiyuan.dll` |
| `requires` | `1.3.15.10` |

## 業務範圍

目前 ProjectShiyuan 重點是 FlowEngine 模板執行、JND/POI 結果提取、客戶資料目錄輸出和偽彩圖保存。它不像 Heyuan 或 BlackMura 那樣完成完整串口/MES 上傳鏈，更接近「跑 Flow -> 彙總算法結果 -> 複製或生成客戶檔案」。

## 主要入口

| 檔案 | 責任 |
| --- | --- |
| `ShiyuanProjectWindow.xaml(.cs)` | 主視窗 |
| `ShiyuanProjectExport.cs` | 啟動和工具菜單 |
| `ProjectShiYuanConfig.cs` | 專案配置 |
| `TempResult.cs`, `NumSet.cs` | 暫存判定和數值範圍 |
| `SerialMsg.cs` | 保留的串口訊息模型 |
| `README.md`, `CHANGELOG.md` | 執行時說明和版本記錄 |

## 輸出

| 結果類型 | 輸出 |
| --- | --- |
| `Compliance_Math_JND` | JND 列表，全部 `Validate` 為 true 才保持 OK |
| `POI_XYZ` | POI CSV 和結果列表 |
| `OLED_JND_CalVas` | `{timestamp}_{SN}_JND.csv` 和輸入圖像複製 |
| Flow `TPAlgorithmNode.ImgFileName` | 以時間戳複製到 `DataPath` |
| 固定圖像 | 從 `C:\Windows\System32\pic\` 複製 `h_gap.tif`、`v_gap.tif`、`luminance.tif`，並生成 h/v gap 偽彩圖 |

## 交接注意

- `UploadSN` handler 目前為空，不應寫成已完成自動 SN 上傳。
- `SerialMsg.cs` 只是保留結構，不代表完整 MES 鏈已實作。
- 固定路徑 `C:\Windows\System32\pic\` 是現場耦合點。
- 改 `DataPath` 規則時，要同步 JND CSV、POI CSV、圖像複製和偽彩圖說明。

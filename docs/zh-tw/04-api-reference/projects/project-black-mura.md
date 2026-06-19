# ProjectBlackMura

`Projects/ProjectBlackMura/` 是顯示面板 Black Mura 檢測專案包，執行時載入為 `ProjectBlackMura.dll`。

## 執行身份

| 欄位 | 值 |
| --- | --- |
| `Id` | `ProjectBlackMura` |
| `version` | `1.0` |
| `dllpath` | `ProjectBlackMura.dll` |
| `requires` | `1.3.15.10` |

## 業務範圍

它不是單一算法包，而是把 PG 上電、PG 切圖、五張圖 Flow、Engine 結果解析、POI 覆蓋、Excel 報告和 MES/串口狀態組成現場流程。

目前切圖順序：

```text
None -> White -> Black -> Red -> Green -> Blue
```

## 主要入口

| 檔案 | 責任 |
| --- | --- |
| `MainWindow.xaml(.cs)` | 主視窗和流程控制 |
| `ProjectBlackMuraConfig.cs` | 專案配置 |
| `PluginConfig/BlackMuraProject.cs` | 啟動器 |
| `PluginConfig/BlackMuraMenu.cs` | 工具菜單 |
| `ExcelReportGenerator.cs` | Excel 報告 |
| `HYMesManager.cs` | MES 和 PG 串口控制 |
| `Config/EditARVRConfig.xaml(.cs)` | 配置視窗 |

## 串口/MES

訊息以 `0x02` 和 `0x03` 包住。常見命令：

| 命令 | 用途 |
| --- | --- |
| `CON,C,{DeviceId}` | PG 上電 |
| `COFF,C,{DeviceId}` | PG 下電 |
| `CCPI,C,{DeviceId},{id}` | 切換 PG 圖 |
| `CSN,C,{DeviceId},{sn}` | 上傳產品 SN |
| `CGI,C,{DeviceId},Default,{Msg}` | 上傳 NG 訊息 |

## 交接注意

- 停流程時先查 `CCPICompleted`、完整 STX/ETX 幀和 `HYMesConfig.DeviceId`。
- Excel 依賴 EPPlus，升級前要確認授權和輸出相容。
- PG/MES 是客戶現場邊界，不應搬到 Engine。
- 模板名變更要同步主視窗的關鍵字匹配。

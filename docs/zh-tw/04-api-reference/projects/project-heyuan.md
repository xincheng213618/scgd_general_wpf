# ProjectHeyuan

`Projects/ProjectHeyuan/` 是河源精電客製專案包，執行時載入為 `ProjectHeyuan.dll`。

## 執行身份

| 欄位 | 值 |
| --- | --- |
| `Id` | `ProjectHeyuan` |
| `version` | `1.0` |
| `dllpath` | `ProjectHeyuan.dll` |
| `requires` | `1.3.15.10` |

## 業務範圍

ProjectHeyuan 負責四點色彩/亮度測試和客戶串口透傳。固定點位順序：

```text
White, Blue, Red, Orange
```

這些值會進入 `TempResult`，用於 PASS/FAIL、CSV 和 MES 上傳。

## 主要入口

| 檔案 | 責任 |
| --- | --- |
| `ProjectHeyuanWindow.xaml(.cs)` | 主視窗 |
| `MenuItemHeyuan.cs` | 啟動和工具菜單 |
| `HYMesManager.cs` | MES/串口管理 |
| `SerialMsg.cs` | 串口訊息模型 |
| `TempResult.cs` | 四點暫存結果 |
| `NumSet.cs` | 數值限制 |

## 流程

1. 工具菜單開啟河源視窗。
2. `HYMesManager.OpenPort()` 以 38400 baud 連接串口。
3. 操作員選擇 `TemplateFlow.Params` 中的模板。
4. `FlowControl.Start(sn)` 執行 Flow。
5. 完成後讀取 `POI_XYZ` 和 `Compliance_Math_CIE_XYZ`。
6. 必須解析到四個 POI，否則是業務資料錯誤。
7. 按 White -> Blue -> Red -> Orange 排序、判定、寫 CSV、上傳。

## 交接注意

- 串口訊息同樣以 `0x02 + ASCII + 0x03` 包住。
- `CSN`、`CMI`、`CGI`、`CPT` 的返回碼規則是現場協議邊界。
- 流程輸出少於四個 POI 是業務錯誤，不要先改算法。
- 改顏色順序、`TestName` 或欄位格式時，要同步 CSV 和協議。

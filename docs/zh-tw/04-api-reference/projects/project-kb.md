# ProjectKB

`Projects/ProjectKB/` 是鍵盤背光檢測專案包，執行時載入為 `ProjectKB.dll`。它組合 FlowEngine、KB 模板、POI 亮度、Recipe 判定、背光自動修正、PLC/Modbus、MES DLL 和 CSV/summary 留痕。

## 執行身份

| 欄位 | 值 |
| --- | --- |
| `Id` | `ProjectKB` |
| `version` | `1.0` |
| `dllpath` | `ProjectKB.dll` |
| `requires` | `1.3.15.10` |

## 入口模式

| 入口 | 說明 |
| --- | --- |
| 手動執行 | 操作員輸入 SN、選模板、在視窗執行 |
| Modbus 觸發 | PLC 寫 holding register 值 `1`，專案執行後寫回 `0` |
| MES/SN | `Summary.AutoUploadSN` 呼叫 `FunTestDll.dll` 的 `CheckWIP`，完成時呼叫 `Collect_test` |

## 主要入口

| 檔案 | 責任 |
| --- | --- |
| `ProjectKBWindow.xaml(.cs)` | 主視窗、Flow、結果解析、CSV/MES/Modbus |
| `KBRecipeConfig.cs` | 亮度、均勻性、局部對比、自動修正限制 |
| `BacklightAutotuneService.cs` | Q1/Q3 和 sigmoid 修正 |
| `KBItemMaster.cs` | 主結果實體 |
| `KBItem.cs` | 單鍵結果 |
| `Summary.cs` | 良率、MES 工站/線別/操作員配置 |
| `Modbus/ModbusControl.cs` | Modbus TCP 連線、輪詢、回寫 |
| `MesDll.cs` | `FunTestDll.dll` P/Invoke |

## 交接注意

- `FunTestDll.dll` 和 `FunTestDllConfig.INI` 必須納入交付驗證。
- `CheckWIP` 返回約定與客戶 DLL 版本相關，現場要實測。
- `KBLVSacle` 對校準和歷史結果解讀很敏感。
- POI 名稱和尺寸必須與 KB 模板一致。
- Modbus、Socket、MES 是三條不同外部路徑，先確認現場用哪一條。

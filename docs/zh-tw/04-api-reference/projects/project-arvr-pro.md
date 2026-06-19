# ProjectARVRPro

`Projects/ProjectARVRPro/` 是目前主要的專業 AR/VR 專案包，執行時載入為 `ProjectARVRPro.dll`。現代 AR/VR 客戶流程交接時，這是最重要的目錄。

## 執行身份

| 欄位 | 值 |
| --- | --- |
| `Id` | `ProjectARVRPro` |
| `version` | `1.1.7.7` |
| `dllpath` | `ProjectARVRPro.dll` |
| `requires` | `1.3.15.15` |

## 業務範圍

ARVRPro 覆蓋亮度、均勻性、色彩、FOFO 對比、棋盤格、MTF、畸變、光心和 OLED AOI。核心模型是 `ProcessGroup` + `ProcessMeta`，適合多產品、多流程組、多客戶和多輸出格式。

與 LUX 不同，ARVRPro 使用 JSON `EventName` 分發，可透過 `ProjectARVRInit` -> `SwitchPGCompleted` -> `ProjectARVRResult` 協調切圖與執行。每個步驟也可以配置 `PictureSwitchConfig` 在 Flow 前執行串口切圖。

## 主要入口

| 檔案或目錄 | 責任 |
| --- | --- |
| `ARVRWindow.xaml(.cs)` | 主測試視窗 |
| `Process/` | 測試步驟框架和各測項實作 |
| `Recipe/` | 限值與修正配置 |
| `Services/SocketControl.cs` | TCP JSON 事件分發 |
| `Services/RunAllSocket.cs` | RunAll 處理 |
| `Services/SwitchGroupSocket.cs` | 外部切換流程組 |
| `SocketRelay/` | AOI relay |
| `ObjectiveTestResult.cs` | 彙總結果模型 |
| `ViewResultManager.cs` | 結果查詢和持久化 |

## 執行鏈

1. 外部發 `ProjectARVRInit`，或操作員在視窗輸入 SN。
2. 選擇目前 `ProcessGroup`。
3. 選擇第一個啟用的 `ProcessMeta`。
4. 如啟用 `PictureSwitchConfig`，先執行串口切圖。
5. 執行綁定的 FlowEngine 模板。
6. 對應 `IProcess.Execute()` 讀取 Engine 結果。
7. 套用 Recipe/Fix 並寫入 `ObjectiveTestResult`。
8. 依配置輸出 SQLite、CSV、Legacy CSV、客製 XLSX 和 Socket 結果。

## 交接注意

- `ProcessGroup` 是產品或場景層級的工作流單位。
- `ProcessMeta` 控制 FlowTemplate、啟用狀態、切圖和私有配置。
- 客戶判定規則應放在專案 `IProcess`，不要回寫到通用 Engine 模板。
- `UseLegacyARVROutput` 會同時影響 CSV 和 Socket `Data` 結構。
- `SocketRelay/` 預設 `127.0.0.1:9200`，主 Socket 連上不代表 relay 鏈路正常。

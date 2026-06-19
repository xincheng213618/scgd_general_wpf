# ProjectLUX

`Projects/ProjectLUX/` 是光學自動化專案包，執行時載入為 `ProjectLUX.dll`，覆蓋亮度、色彩、對比、MTF、畸變、光心、VID 和光通量。

## 執行身份

| 欄位 | 值 |
| --- | --- |
| `Id` | `ProjectLUX` |
| `version` | `1.0` |
| `dllpath` | `ProjectLUX.dll` |
| `requires` | `1.3.15.10` |

## 業務範圍

LUX 與 ARVRPro 最大差異是協議風格。LUX 使用文字命令：

```text
T00XX,SN;
```

`XX` 對應目前流程組中 `ProcessMeta.SocketCode`。因此 LUX 交接必須一起檢查 FlowTemplate、目前流程組、SocketCode、Recipe、Fix 和客戶返回碼。

## 主要入口

| 檔案或目錄 | 責任 |
| --- | --- |
| `LUXWindow.xaml(.cs)` | 主測試視窗 |
| `ProjectLUXConfig.cs` | 專案配置 |
| `PluginConfig/` | 啟動、菜單、視窗單例 |
| `Process/` | 測項框架與測項實作 |
| `Recipe/` | 限值配置 |
| `Fix/` | 修正係數 |
| `Services/SocketControl.cs` | TCP 文字命令 |
| `ObjectiveTestResult.cs` | 彙總結果 |
| `ViewResultManager.cs` | SQLite 結果管理 |

## 輸出

| 類型 | 檔案 |
| --- | --- |
| 一般流程 | `C_<SN>.csv` |
| VID | `B_<SN>.csv` |
| 光通量 | `D_<SN>.csv` |
| 本地結果 | `ProjectLUX.db` |

## 交接注意

- Socket 是文字協議，不是 ARVRPro JSON。
- 改 FlowTemplate 時一定要檢查 `SocketCode`。
- `FixConfig` 會影響最終值，現場校準問題不要過早判成算法問題。
- 確認 `%APPDATA%\ColorVision\Config\ProcessGroups.json` 變更後能持久化。

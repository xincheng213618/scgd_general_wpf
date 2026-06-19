# ProjectARVRLite

`Projects/ProjectARVRLite/` 是輕量 AR/VR 快速測試專案包，執行時載入為 `ProjectARVRLite.dll`。它保留 AR/VR 切圖和 Socket 工作流，重點是可配置測項、預處理和簡化交付。

## 執行身份

| 欄位 | 值 |
| --- | --- |
| `Id` | `ProjectARVRLite` |
| `version` | `1.0` |
| `dllpath` | `ProjectARVRLite.dll` |
| `requires` | `1.3.15.6` |

## 業務範圍

`ProjectARVRLiteTestTypeConfig.json` 決定啟用哪些測項。目前已實作的模板分支包括：

```text
W51, White, W25, Chessboard, MTFHV, Distortion, Ghost, OpticCenter
```

`DotMatrix`、白屏缺陷、黑屏缺陷雖有 enum/config 選項，但目前 `SwitchPGCompleted()` 沒有完整自動化分支，交付前應保持停用。

## 主要入口

| 檔案 | 責任 |
| --- | --- |
| `ARVRWindow.xaml(.cs)` | 主視窗、啟用測項狀態機、預處理、結果回傳 |
| `TestTypeConfig.cs` | 測項啟用配置 |
| `ObjectiveTestResult.cs` | 產品級結果和 CSV |
| `ARVRRecipeConfig.cs` | W51/W255/W25/MTFHV/畸變/Ghost/光軸限制 |
| `Services/SocketControl.cs` | Socket 事件 |
| `EditTestTypeConfigWindow.xaml(.cs)` | 測項配置視窗 |

## 流程

1. 外部發 `ProjectARVRInit`。
2. Lite 如果視窗未開啟會自動建立。
3. `InitTest(SN)` 重置狀態和產品結果。
4. 讀取第一個啟用測項並回傳 `SwitchPG`。
5. 收到 `SwitchPGCompleted` 後執行下一個啟用測項。
6. `RunTemplate()` 先執行預處理，再啟動 Flow。
7. 完成後彙總 CSV 和 Socket `ProjectARVRResult`。

## 交接注意

- 先檢查 `%AppData%\ColorVision\Config\ProjectARVRLiteTestTypeConfig.json`。
- 預處理失敗會在算法前停止 Flow。
- CSV 受 `ViewResultManager.Config.IsSaveCsv`、`SaveByDate` 和 SN 命名規則影響。
- 模板名稱需要包含 `White51`、`White255_Ghost_Test`、`MTF_HV` 等關鍵字。

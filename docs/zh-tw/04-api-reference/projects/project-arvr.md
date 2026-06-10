# ProjectARVR

`Projects/ProjectARVR/` 是早期 AR/VR 光學測試專案包，執行時載入為 `ProjectARVR.dll`。它把固定 PG 切圖、FlowEngine 模板執行、`ObjectiveTestResult` 彙總、CSV 匯出和 Socket 結果回傳串成一條流程。

## 執行身份

| 欄位 | 值 |
| --- | --- |
| `Id` | `ProjectARVR` |
| `version` | `1.0` |
| `dllpath` | `ProjectARVR.dll` |
| `requires` | `1.3.9.10` |

## 業務範圍

目前自動化鏈路是固定順序，實際完成到 `OpticCenter`：

```text
White2 -> White -> White1 -> Black -> Chessboard -> MTFH -> MTFV -> Distortion -> OpticCenter -> ProjectARVRResult
```

後續 enum 如 `Ghost`、`DotMatrix` 和屏缺項雖存在於程式碼中，但目前 `SwitchPGCompleted()` 鏈路沒有對它們執行模板。

## 主要入口

| 檔案 | 責任 |
| --- | --- |
| `ARVRWindow.xaml(.cs)` | 主視窗、切圖狀態機、Flow 執行、結果解析、Socket 回傳 |
| `ProjectARVRConfig.cs` | 專案配置與模板編輯 |
| `ObjectiveTestResult.cs` | 產品級結果 DTO 和 CSV 匯出 |
| `ARVRRecipeConfig.cs` | 白/黑/棋盤/MTF/畸變/光軸限制 |
| `ObjectiveTestResultFix.cs` | 結果修正係數 |
| `ViewResultManager.cs` | 結果列表、持久化、CSV 路徑 |
| `Services/SocketControl.cs` | Socket 事件 |
| `PluginConfig/ProjectARVRMenu.cs` | 工具菜單入口 |

## Socket 流程

1. 外部發 `ProjectARVRInit`。
2. 視窗必須已開啟，`InitTest(SN)` 重置狀態。
3. 專案回傳第一張圖的 `SwitchPG`。
4. 外部切圖後發 `SwitchPGCompleted`。
5. 專案按目前 `ARVRTestType` 找模板並執行 Flow。
6. 到 `OpticCenter` 後彙總結果，寫 CSV，回傳 `ProjectARVRResult`。

## 交接注意

- `ProjectARVRInit` 需要視窗先打開；Lite 版本會自動開窗。
- 模板匹配依賴 `White255`、`MTF_H`、`OpticCenter` 等關鍵字。
- 不要把後續 enum 寫成已完成自動化。
- 新 AR/VR 交付優先評估 ProjectARVRPro 或 ProjectARVRLite。

# 目前 Engine 文件覆蓋清單

本頁用來確認 `Engine/` 的業務邏輯是否有交接入口。它不是逐檔 API 清單，而是把真實 Engine 專案、`ColorVision.Engine` 關鍵目錄和交接頁對應起來。

## 當前覆蓋結論

| Engine 專案 | 工程文件 | README | 目前文件頁 | 交接入口 | 結論 |
| --- | --- | --- | --- | --- | --- |
| `Engine/ColorVision.Engine/` | `ColorVision.Engine.csproj` | 有 | [ColorVision.Engine](./ColorVision.Engine.md) | [業務鏈路矩陣](./business-flow-matrix.md)、[場景手冊](./business-scenario-playbook.md)、[執行時物件目錄](./runtime-object-map.md) | 主業務執行時覆蓋完整 |
| `Engine/FlowEngineLib/` | `FlowEngineLib.csproj` | 有 | [FlowEngineLib](./FlowEngineLib.md) | [模板與 Flow 鏈路](./template-flow-chain.md) | Flow 執行鏈覆蓋完整 |
| `Engine/cvColorVision/` | `cvColorVision.csproj` | 有 | [cvColorVision](./cvColorVision.md) | [結果展示與專案交接鏈路](./result-handoff-chain.md) | native/視覺邊界已說明 |
| `Engine/ColorVision.FileIO/` | `ColorVision.FileIO.csproj` | 有 | [ColorVision.FileIO](./ColorVision.FileIO.md) | [資料匯出與匯入](../../01-user-guide/data-management/export-import.md)、結果鏈路 | 檔案格式和 I/O 已說明 |
| `Engine/ST.Library.UI/` | `ST.Library.UI.csproj` | 有 | [ST.Library.UI](./ST.Library.UI.md) | [模板與 Flow 鏈路](./template-flow-chain.md) | 節點編輯 UI 基礎已說明 |
| `Engine/ColorVision.ShellExtension/` | `ColorVision.ShellExtension.csproj` | 無 | [ColorVision.ShellExtension](./ColorVision.ShellExtension.md) | Shell 縮略圖擴充頁、[ColorVision.FileIO](./ColorVision.FileIO.md) | 外部 Explorer 整合已說明 |

## `ColorVision.Engine` 業務目錄覆蓋

| 源碼目錄 | 業務含義 | 目前交接頁 | 接手時先問什麼 |
| --- | --- | --- | --- |
| `Services/` | 服務管理、裝置基類、Terminal、Cache、RC service | [裝置服務鏈路](./device-service-chain.md)、[業務鏈路矩陣](./business-flow-matrix.md) | 資源是否能生成正確 `DeviceService` |
| `Services/Devices/` | Camera、Motor、SMU、FileServer、FlowDevice 等具體裝置 | [裝置服務鏈路](./device-service-chain.md) | 手動動作和 Flow node 是否引用同一裝置 |
| `Templates/` | 模板參數、Flow 模板、算法模板、POI/ROI、ARVR 模板 | [模板與 Flow 鏈路](./template-flow-chain.md)、[結果鏈路](./result-handoff-chain.md) | 模板版本、節點綁定和結果映射是否一致 |
| `FlowEngineLib/Node/Algorithm/`、`FlowEngineLib/Algorithm/` | Flow 算法、轉換和校準節點 | [Flow 轉換與校準節點](./flow-conversion-calibration-nodes.md)、[模板與 Flow 鏈路](./template-flow-chain.md) | `operatorCode`、參數物件和配置器是否一致 |
| `MQTT/` | MQTT 配置、連線、控制物件 | [裝置服務鏈路](./device-service-chain.md)、[場景手冊](./business-scenario-playbook.md) | topic、連線狀態和 device Code 是否匹配 |
| `Batch/`、`Dao/`、`Mysql/` | 批次、結果記錄、MySQL/SQLite 存取 | [結果鏈路](./result-handoff-chain.md) | 資料是否落庫，批次/SN 是否一致 |
| `Messages/` | MQTT 和業務訊息模型 | [業務鏈路矩陣](./business-flow-matrix.md) | 專案或外部系統使用哪類訊息 |
| `Archive/`、`Reports/` | 歸檔結果查詢和報表 | [結果鏈路](./result-handoff-chain.md) | 結果來源、欄位、路徑和報表版本是否對應 |
| `ToolPlugins/` | 內建工具入口，例如 ImageJ、CVRaw 轉 CSV | [場景手冊](./business-scenario-playbook.md)、[ColorVision.Engine](./ColorVision.Engine.md) | 工具是調試輔助還是產線交付物 |
| `Abstractions/`、`PropertyEditor/`、`Utilities/` | 公共接口、屬性編輯、工具類 | [執行時物件目錄](./runtime-object-map.md) | 是否被業務鏈路直接調用 |
| `Assets/`、`Properties/`、`CalFile/`、`Media/` | 資源、屬性、標定/媒體輔助文件 | 由場景頁按需引用 | 是否需要隨包複製 |
| `bin/`、`obj/` | 構建輸出和中間產物 | 不作為文件對象 | 不作為業務交接依據 |

## 交接閱讀順序

1. 不知道歸屬時，先看 [Engine 業務鏈路矩陣](./business-flow-matrix.md)。
2. 已知場景時，進入 [Engine 業務場景交接手冊](./business-scenario-playbook.md)。
3. 已知類名或 runtime object 時，進入 [Engine 執行時物件目錄](./runtime-object-map.md)。
4. 已知鏈路類型時，分別看 [裝置服務鏈路](./device-service-chain.md)、[模板與 Flow 鏈路](./template-flow-chain.md)、[結果展示與專案交接鏈路](./result-handoff-chain.md)。
5. 涉及客戶專案輸出時，繼續看 [專案包能力與交接矩陣](../projects/project-capability-matrix.md) 和具體專案頁。

新增 Engine 專案、裝置類型、模板目錄或結果鏈路時，必須同步更新本頁和對應鏈路頁。

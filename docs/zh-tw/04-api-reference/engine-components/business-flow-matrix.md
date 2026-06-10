# Engine 業務鏈路矩陣

這一頁給交接人員一張 Engine 側業務地圖：從裝置資源、模板、Flow、演算法結果到專案輸出，先判斷需求落在哪條鏈，再去對應程式碼。

## 主鏈路

```text
SysResourceModel / 資料庫資源
  -> ServiceTypes / DeviceServiceFactoryRegistry
  -> DeviceService / MQTTDeviceService / MQTTControl
  -> TemplateControl / TemplateModel / TemplateFlow
  -> FlowEngineControl / Flow 節點 / NodeConfiguratorRegistry
  -> AlgResult / ViewResult / ImageEditor Overlay
  -> Projects/* / ObjectiveTestResult / 匯出或 Socket 回傳
```

Engine 不是單純的演算法庫。它負責把資料庫資源、裝置服務、模板配置、流程節點、結果展示和專案包後處理串成一條可執行鏈路。

## 場景矩陣

| 業務場景 | 第一入口 | 主要程式碼 | 交接時先確認 |
| --- | --- | --- | --- |
| 啟動與初始化 | 主程式載入 Engine 初始器 | `ColorVision.Engine/Services/`, `MQTT/`, `Templates/` | 服務是否註冊、MQTT 是否連上、模板是否載入 |
| 資料庫裝置顯示 | `SysResourceModel`、`ServiceTypes` | `Services/Type/TypeService.cs`, `Services/DeviceServiceFactoryRegistry.cs` | Type 是否對上、Factory 是否註冊、資源是否被過濾 |
| 新增裝置類型 | `DeviceService<TConfig>` | `Services/Devices/**`, `Services/Devices/*/MQTT*.cs` | Config、服務、顯示頁、MQTT、Flow 節點是否都補齊 |
| 遠端控制 / MQTT | `MQTTControl`、`MQTTDeviceService` | `Services/Devices/**/MQTT*.cs`, `MQTT/` | Topic、Token、回包欄位、超時與日誌 |
| 模板參數 | `TemplateControl`、`TemplateModel<T>` | `Templates/Jsons/`, `Templates/ARVR/`, `Templates/POI/` | `Code`、`Title`、`TemplateDicId` 是否相容 |
| Flow 編輯與執行 | `TemplateFlow`、`FlowControl` | `Templates/Flow/`, `FlowEngineLib/` | 節點能否開啟、儲存、匯入、執行、觸發 `FlowCompleted` |
| Flow 節點綁定 | `NodeConfiguratorRegistry` | `Templates/Flow/NodeConfigurator/`, `Templates/Flow/Nodes/` | 節點類型、模板類型、裝置類型是否互相匹配 |
| 演算法結果展示 | `AlgResultMasterModel`、`ViewResultAlg` | `Templates/**/ViewHandle*.cs`, `Abstractions/IResultHandlers.cs` | DAO、結果欄位、影像路徑、座標系是否完整 |
| 圖像覆蓋層 | `IViewResult`、`IResultHandleBase` | `UI/ColorVision.ImageEditor/Draw/**` | 顯示只負責可視化，不應混入客戶判定 |
| 專案包輸出 | `Projects/<Project>/Process` | `Projects/*/Recipe`, `Fix`, `Process`, exporter | 專案讀到的 key 是否與 Engine 結果一致 |
| 批次與歸檔 | `MeasureBatchModel` | `Services/Batch`, DAO, CSV/SQLite/MySQL | 批次號、結果 ID、歸檔路徑和匯出時機 |
| 檔案 / 影像 | `ColorVision.FileIO`, `cvColorVision` | `Engine/ColorVision.FileIO/`, `Engine/cvColorVision/` | 原生 DLL、檔案格式、CopyLocal、x64 runtime |

## 裝置類型對照

常見裝置類型包括 Camera、PG、Spectrum、SMU、Sensor、FileServer、Algorithm、Calibration、Motor、CfwPort、FlowDevice、ThirdPartyAlgorithms。新增或修改時不要只改顯示頁，還要同步檢查：

- `ServiceTypes` 是否有類型定義。
- `DeviceServiceFactoryRegistry` 是否能建立對應服務。
- `ServiceManager.DeviceServices` 是否會持有該服務。
- Flow 節點配置器是否允許選到該裝置。
- MQTT 命令、狀態和結果回包是否可追蹤。

## 改動歸屬

| 要改什麼 | 優先放哪裡 | 同步更新 |
| --- | --- | --- |
| 新裝置類型 | `Services/Type/TypeService.cs`, `Services/Devices/` | [設備服務鏈路](./device-service-chain.md) |
| 新 Flow 節點 | `FlowEngineLib/`, `Templates/Flow/Nodes/`, `Templates/Flow/NodeConfigurator/` | [模板與 Flow 鏈路](./template-flow-chain.md) |
| 新模板 | `Templates/Jsons/`, `Templates/ARVR/`, `Templates/POI/` | 節點配置、結果展示、專案讀取 |
| 新結果覆蓋 | `Templates/**/ViewHandle*.cs`, `UI/ColorVision.ImageEditor/Draw/` | [結果展示與專案交接鏈路](./result-handoff-chain.md) |
| 客戶欄位 / 判定 | `Projects/<Project>/Recipe`, `Fix`, `Process` | 專案說明與匯出格式 |

## 排查順序

1. 先確認資料庫資源、模板和批次資料存在。
2. 再看服務是否被建立，MQTT 是否有命令與回包。
3. 接著看 Flow 節點是否綁到正確模板和裝置。
4. 然後看 Engine 是否產生 `AlgResultMasterModel` 與明細結果。
5. 最後再看 UI 覆蓋層與 `Projects/*` 的判定、匯出或 Socket 回傳。


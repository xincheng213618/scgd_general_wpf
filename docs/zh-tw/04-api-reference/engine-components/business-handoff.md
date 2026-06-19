# Engine 業務交接手冊

這份手冊用來交接 Engine 的主業務邏輯。讀完後，接手人應該能回答：裝置從哪裡建立、模板怎麼進 Flow、演算法結果怎麼展示、專案包怎麼拿到最終結果。

## 一句話理解 Engine

`ColorVision.Engine` 是業務編排層。它不是單一演算法 DLL，也不是單一裝置驅動，而是把資料庫資源、裝置服務、MQTT、模板、Flow、演算法結果和專案包交付串起來。

## 主要目錄

| 目錄 | 責任 |
| --- | --- |
| `Engine/ColorVision.Engine/Services/` | 裝置服務、資源映射、MQTT 控制、批次與結果服務 |
| `Engine/ColorVision.Engine/Templates/` | 模板管理、Flow 模板、演算法模板、結果展示入口 |
| `Engine/FlowEngineLib/` | Flow 節點模型、開始/結束節點、執行控制 |
| `Engine/ColorVision.FileIO/` | CVRAW、CVCIE 等檔案格式讀寫 |
| `Engine/cvColorVision/` | OpenCV 和原生能力封裝 |
| `Projects/*` | 客戶規則、Recipe、Fix、匯出、Socket/MES 對接 |

## 裝置服務怎麼接起來

資料庫裡的資源先被讀成 `SysResourceModel`，再依 `ServiceTypes` 找到對應 Factory。Factory 建立 `DeviceService<TConfig>` 後交給 `ServiceManager` 管理。需要遠端控制的裝置會再接 `MQTTDeviceService` 或 `MQTTControl`。

新增裝置時，至少檢查類型、Config、Service、Factory、顯示頁、MQTT、Flow 節點配置器。只補其中一半會導致 UI 看得到但 Flow 選不到，或 Flow 能選但命令發不出去。

## 模板和 Flow 怎麼接起來

模板入口是 `TemplateControl` 和 `TemplateModel<T>`。Flow 模板由 `TemplateFlow` 管理，執行時進入 `FlowEngineControl`、`FlowControl` 和各節點。節點顯示與參數配置通常在 `Templates/Flow/NodeConfigurator/`。

交接時要特別核對模板 ID、模板 Code、節點類型、裝置類型和結果 key。這幾個欄位一旦不一致，表現常常是“流程能跑，但結果對不上”。

## 結果怎麼展示與交付

演算法結果通常先落到 `AlgResultMasterModel` 和明細結果，再由 `ViewResultAlg`、`IResultHandleBase`、`IViewResult` 轉成可顯示物件。圖像覆蓋層在 `UI/ColorVision.ImageEditor/Draw/`。

專案包不應替代 Engine 產生結果。它負責讀 Engine 結果、套用客戶判定、修正欄位、生成 `ObjectiveTestResult`，再輸出 CSV、資料庫、Socket 或 MES 需要的格式。

## 常見修改放哪裡

| 需求 | 優先修改 |
| --- | --- |
| 新增裝置 | `Services/Devices/`, `Services/Type/TypeService.cs` |
| 新增裝置命令 | `Services/Devices/*/MQTT*.cs`, `MQTT/` |
| 新增模板參數 | `Templates/**`, 對應模板模型 |
| 新增 Flow 節點 | `FlowEngineLib/`, `Templates/Flow/Nodes/`, `NodeConfigurator/` |
| 新增結果圖層 | `Templates/**/ViewHandle*.cs`, `UI/ColorVision.ImageEditor/Draw/` |
| 客戶判定或匯出 | `Projects/<Project>/Recipe`, `Fix`, `Process`, exporter |

## 排查手順

1. 先看資源、模板、批次、結果 ID 是否存在。
2. 再看 Engine 日誌和 MQTT 日誌。
3. 查 `ServiceManager` 裡是否有服務。
4. 查 Flow 節點是否選到正確裝置與模板。
5. 查 DAO 是否有主結果和明細結果。
6. 查 ViewHandle 是否命中。
7. 最後查專案包的欄位映射與匯出。

## 維護要求

每次改 Engine 業務鏈路，都要同步更新本章對應頁面。新增裝置更新 [設備服務鏈路](./device-service-chain.md)，新增 Flow 或模板更新 [模板與 Flow 鏈路](./template-flow-chain.md)，新增結果或專案欄位更新 [結果展示與專案交接鏈路](./result-handoff-chain.md)。


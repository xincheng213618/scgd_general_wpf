# Engine 業務場景交接手冊

這一頁按常見需求與故障描述來定位 Engine 程式碼。交接時先回答三個問題：

1. 觸發入口是 UI、Flow、專案包、Socket/MES、排程還是遠端 MQTT？
2. 所屬層是資源/裝置、模板/Flow、遠端服務、結果展示、專案映射還是匯出？
3. 可用證據是日誌、資源 ID、模板 ID、批次號、結果 ID，還是客戶端回包？

## A. 資料庫有裝置，但 UI 或 Flow 看不到

先查 `SysResourceModel` 仍存在且 Type 未被改壞，再看 `Services/Type/TypeService.cs` 是否把 Type 映射到正確 `ServiceTypes`。如果類型正確，繼續查 `DeviceServiceFactoryRegistry` 是否註冊了 Factory，以及 `ServiceManager.DeviceServices` 是否真的建立服務。

Flow 端還要看 `Templates/Flow/NodeConfigurator/` 的類型過濾。很多“裝置不存在”的問題其實是節點配置器不允許該類型，而不是服務沒有建立。

## B. 新增一種裝置

最小交接清單：

- 在 `ServiceTypes` 補類型。
- 新增 `ConfigXxx : DeviceServiceConfig`。
- 新增 `DeviceXxx : DeviceService<ConfigXxx>`。
- 在 `DeviceServiceFactoryRegistry` 或對應 Factory 補註冊。
- 補裝置顯示控制元件與設定入口。
- 若要進 Flow，補 `NodeConfigurator` 和節點可選條件。
- 若有遠端控制，補 `MQTTDeviceService`、Topic、回包與超時日誌。
- 更新本章文檔與專案包使用說明。

## C. 模板參數改動

先判斷模板屬於 JSON 模板、POI 模板、Flow 模板還是裝置動作模板。常見入口在 `Templates/Jsons/`、`Templates/POI/`、`Templates/Flow/` 與各裝置服務目錄。

改模板時要維護相容性：`Code`、`Title`、`TemplateDicId`、預設值、屬性描述、舊資料反序列化都要檢查。若參數會被 Flow 節點使用，還要同步更新節點配置器和結果顯示邏輯。

## D. 新增或修改 Flow 節點

Flow 節點通常分兩層：`FlowEngineLib/` 負責節點執行模型，`ColorVision.Engine/Templates/Flow/` 負責主程式中的模板、配置器和視窗接入。

驗收時至少覆蓋：

- 打開舊 Flow。
- 新增節點並保存。
- 關閉後重新打開。
- 匯入 `.cvflow`。
- 執行並觸發 `FlowCompleted`。
- 檢查節點結果是否能被批次、結果展示和專案包讀到。

## E. 結果有資料，但圖像不顯示

先看 Engine 是否產生 `AlgResultMasterModel` 和對應明細 DAO。再看 `ViewResultAlg` 是否能建立展示模型，`DisplayAlgorithmManager` 是否選到正確 `ViewHandleXxx`，以及 `IResultHandleBase.CanHandle` 是否命中。

如果模型都正確，再查影像路徑、座標系、ROI、比例尺和 `UI/ColorVision.ImageEditor/Draw/` 的繪製物件。不要把客戶判定邏輯寫進覆蓋層，覆蓋層只負責把結果畫出來。

## F. 專案包結果為空或欄位錯

先確認 Engine 結果存在，再看 `Projects/<Project>/Process` 讀的是哪個 key。接著檢查 `Recipe`、`Fix`、`ObjectiveTestResult`、匯出器、Socket/MES 回包欄位。

專案包只應做客戶規則、欄位映射和交付格式；若 Engine 根本沒有產生結果，不要在專案包裡硬補。

## G. 遠端服務沒有結果

排查順序是 MQTT 連線、Topic、服務 Token、命令序列、FileServer 路徑、結果 ID、DAO 落庫、Flow 狀態。程式碼優先看 `Services/Devices/*/MQTT*.cs` 和 `MQTT/` 目錄。

## H. Socket/MES 回傳錯

先分清是 UI 內通用 `SocketProtocol`，還是 `Projects/<Project>/SocketControl` 的客戶協議。逐項核對 EventName、SN、模板、Flow、結果 ID、回傳欄位和錯誤碼。

## 快速查類

| 目的 | 先找 |
| --- | --- |
| 裝置建立 | `ServiceManager`, `DeviceServiceFactoryRegistry` |
| 模板載入 | `TemplateControl`, `TemplateModel<T>` |
| Flow 執行 | `TemplateFlow`, `FlowControl`, `NodeConfiguratorRegistry` |
| 結果展示 | `ViewResultAlg`, `IResultHandleBase`, `IViewResult` |
| 專案判定 | `ObjectiveTestResult`, `Projects/<Project>/Process` |


# Engine MQTT 訊息處理交接手冊

本頁說明 Engine 層 MQTT 的真實收發模型。主線不是每個模組各自建立 MQTT client，而是 `MQTTControl` 管理連線、訂閱、發布和 trace，設備服務透過 `MQTTServiceBase` / `MQTTDeviceService<T>` 發命令並等待返回。

## 目前分層

| 層級 | 關鍵物件 | 職責 |
| --- | --- | --- |
| 全域連線 | `MQTTControl` | 建立 `IMqttClient`、連線、斷線重連、訂閱快取、發布、保存最近 200 條 trace |
| 設定 | `MQTTSetting`、`MQTTConfig` | Host、Port、UserName、UserPwd 和安全保存 |
| 啟動 | `MqttInitializer` | 主程式初始化時連線 MQTT |
| 設備命令 | `MQTTServiceBase` | 建立 `MsgRecord`，發送 `MsgSend`，按 `MsgID` 匹配 `MsgReturn` |
| 設備綁定 | `MQTTDeviceService<T>` | 從設備設定讀取 `SendTopic`、`SubscribeTopic` |
| Flow MQTT 節點 | `FlowEngineLib/MQTT/` | 可視化流程中的 publish/subscribe hub |

## 命令執行鏈

1. 設備 UI、Flow 節點或專案包呼叫具體 `MQTT*` 方法。
2. `MQTT*` 建立 `MsgSend`，設定 `EventName` 和參數。
3. `MQTTServiceBase.PublishAsyncClient()` 補 `MsgID`、`DeviceCode`、`Token`、`ServiceName`。
4. 建立 `MsgRecord`，寫入訊息資料庫並啟動超時計時器。
5. `MQTTControl.PublishAsyncClient()` 發布到 `SendTopic`。
6. broker 將返回送到 `SubscribeTopic`。
7. `MQTTServiceBase.Processing()` 解析 `MsgReturn`，按 `MsgID` 匹配等待中的記錄。

## 修改 MQTT 行為時看哪裡

| 目標 | 主要文件 |
| --- | --- |
| broker 設定 | `MQTTSetting.cs`、`MQTTConnect.xaml.cs` |
| 連線和重連 | `MQTTControl.cs`、`MqttInitializer.cs` |
| 新增設備命令 | 對應 `Services/Devices/*/MQTT*.cs` |
| 修改設備 topic | `DeviceServiceConfig` 和設備設定 UI |
| 修改返回處理 | `MQTTServiceBase` 或具體 `MQTT*` 回調 |
| Flow MQTT 節點 | `FlowEngineLib/MQTT/` |

## 驗收清單

- MQTT 設定保存後重啟仍能連線。
- 設備命令能在 trace 或日誌中看到 SEND/RECV。
- `MsgRecord` 有發送時間、接收時間、狀態和返回內容。
- 失敗返回不會被誤標成成功。
- 超時後等待記錄會清理。
- 斷線重連後已快取 topic 會重新訂閱。

## 相關文件

- [Engine 設備服務鏈路](../../04-api-reference/engine-components/device-service-chain.md)
- [Engine 業務場景交接手冊](../../04-api-reference/engine-components/business-scenario-playbook.md)
- [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md)
- [測試與驗證交接手冊](../testing.md)

# Engine 服務開發交接手冊

本頁說明 `Engine/ColorVision.Engine/Services/` 目前真實存在的設備服務模型。這裡的服務不是通用 DI 服務，而是主程式執行時可見、可設定、可顯示，且通常可透過 MQTT 發送命令的設備或業務服務。

第一次接手請先讀 [Engine 設備服務鏈路](../../04-api-reference/engine-components/device-service-chain.md)。

## 目前服務鏈路

| 階段 | 關鍵物件 | 說明 |
| --- | --- | --- |
| 服務類型 | `ServiceTypes` | Camera、PG、Spectrum、SMU、Sensor、FileServer、Algorithm、FilterWheel、Calibration、Motor、Flow、ThirdPartyAlgorithms 等類型編號 |
| 設定來源 | `SysResourceModel.Value` | 保存設備設定 JSON，`DeviceService<T>` 會反序列化為具體 `Config*` |
| 實例建立 | `DeviceServiceFactoryRegistry` | 按 `SysResourceModel.Type` 建立具體 `Device*` |
| 執行集合 | `ServiceManager.GetInstance().DeviceServices` | 主程式目前已載入的設備服務 |
| UI 入口 | `GetDeviceInfo()`、`GetDisplayControl()` | 資訊頁、控制面板、設備樹和屬性視窗 |
| 命令鏈路 | `GetMQTTService()`、`MQTTDeviceService<T>` | 設備命令、返回、超時和訊息記錄 |

典型載入順序：`SysResourceModel` 保存資源，`ServiceManager.Load()` 遍歷資源，`DeviceServiceFactoryRegistry.CreateService()` 建立服務，`DeviceService<T>` 還原設定，具體 `Device*` 建立對應 `MQTT*` 和 UI 控件。

## 目前預設註冊項

| 類型 | 目錄 | 設備類 | MQTT 類 | 主要職責 |
| --- | --- | --- | --- | --- |
| Camera | `Services/Devices/Camera/` | `DeviceCamera` | `MQTTCamera` | 相機、即時畫面、拍圖、曝光和校準命令 |
| PG | `Services/Devices/PG/` | `DevicePG` | `MQTTPG` | 圖案發生器切圖、PG 參數和專案切圖聯動 |
| Spectrum | `Services/Devices/Spectrum/` | `DeviceSpectrum` | `MQTTSpectrum` | 光譜儀連線、暗電流、量測和光譜資料 |
| SMU | `Services/Devices/SMU/` | `DeviceSMU` | `MQTTSMU` | 電源表、掃描、結果讀取和部分光譜聯動 |
| Sensor | `Services/Devices/Sensor/` | `DeviceSensor` | `MQTTSensor` | 串口/網路感測器命令和模板化指令 |
| FileServer | `Services/Devices/FileServer/` | `DeviceFileServer` | `MQTTFileServer` | 檔案路徑、下載、快取和檔案服務命令 |
| Algorithm | `Services/Devices/Algorithm/` | `DeviceAlgorithm` | `MQTTAlgorithm` | 演算法服務呼叫、結果查詢和演算法視圖 |
| FilterWheel | `Services/Devices/CfwPort/` | `DeviceCfwPort` | `MQTTCfwPort` | 濾光輪連接埠和位置控制 |
| Calibration | `Services/Devices/Calibration/` | `DeviceCalibration` | `MQTTCalibration` | 校準命令、檔案和校準結果 |
| Motor | `Services/Devices/Motor/` | `DeviceMotor` | `MQTTMotor` | 電機回零、移動、位置讀取和光闌控制 |
| Flow | `Services/Devices/FlowDevice/` | `DeviceFlowDevice` | `MQTTFlowDevice` | 流程設備服務 |
| ThirdPartyAlgorithms | `Services/Devices/ThirdPartyAlgorithms/` | `DeviceThirdPartyAlgorithms` | `MQTTThirdPartyAlgorithms` | 第三方演算法接入 |

## 新增設備服務步驟

1. 確認是否能復用既有 `ServiceTypes`。
2. 新增 `Config* : DeviceServiceConfig`，保持舊 JSON 反序列化相容。
3. 新增 `Device* : DeviceService<Config*>`，建構時建立 `DService = new MQTT*(Config)`。
4. 覆寫 `GetDeviceInfo()`，必要時覆寫 `GetDisplayControl()` 和 `GetMQTTService()`。
5. 新增 `MQTT* : MQTTDeviceService<Config*>`，只封裝設備命令，不寫客戶判定。
6. 在 `DeviceServiceFactoryRegistry.RegisterDefaults()` 註冊工廠。
7. 驗證終端建立時 `Code`、`Name`、`SendTopic`、`SubscribeTopic` 能寫入設定。

## 驗收清單

| 項目 | 驗收方式 |
| --- | --- |
| 註冊 | 啟動主程式，設備樹可看到新服務或既有服務能恢復 |
| 設定 | 匯出、匯入、重置、保存重開後設定不遺失 |
| MQTT | `SendTopic` / `SubscribeTopic` 正確，返回可匹配 `MsgID` |
| UI | 屬性頁、資訊頁、顯示控件開啟不拋例外 |
| Flow/專案 | 依賴該服務的流程節點或專案包能選中正確設備 |

## 相關文件

- [Engine 設備服務鏈路](../../04-api-reference/engine-components/device-service-chain.md)
- [MQTT 訊息處理](./mqtt.md)
- [Engine 執行時物件目錄](../../04-api-reference/engine-components/runtime-object-map.md)
- [測試與驗證交接手冊](../testing.md)

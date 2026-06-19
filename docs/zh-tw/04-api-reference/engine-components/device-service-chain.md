# Engine 設備服務鏈路

設備服務鏈路負責把資料庫資源變成可顯示、可控制、可被 Flow 使用的執行時服務。

## 鏈路概覽

```text
SysDictionaryModel / SysResourceModel
  -> ServiceTypes
  -> DeviceServiceFactoryRegistry
  -> DeviceService<TConfig>
  -> 顯示控制元件 / MQTTDeviceService / Flow NodeConfigurator
```

## 關鍵類與目錄

| 位置 | 作用 |
| --- | --- |
| `Services/ServiceManager.cs` | 持有已建立的裝置服務集合 |
| `Services/Type/TypeService.cs` | 資源 Type 與服務類型對照 |
| `Services/DeviceService.cs` | 裝置服務基底 |
| `Services/DeviceServiceFactory.cs` | 建立服務的 Factory |
| `Services/DeviceServiceFactoryRegistry.cs` | Factory 註冊與查找 |
| `Services/MQTTDeviceService.cs` | 裝置遠端命令與狀態基底 |
| `Services/Devices/**` | 各類裝置的具體服務、Config、MQTT 邏輯 |

## 新增設備檢查清單

1. 新增或確認 `ServiceTypes` 枚舉/常量。
2. 新增裝置配置類，繼承 `DeviceServiceConfig`。
3. 新增裝置服務類，繼承 `DeviceService<TConfig>`。
4. 註冊 Factory，保證 `SysResourceModel.Type` 能建立服務。
5. 補 UI 顯示頁和設定入口。
6. 補 MQTT 命令、狀態回包和超時日誌。
7. 若 Flow 需要使用，補 `NodeConfigurator` 的可選類型。
8. 補文檔、測試流程和專案包使用說明。

## 常見故障

| 現象 | 優先檢查 |
| --- | --- |
| 資料庫有資源但 UI 不顯示 | Type 是否映射、Factory 是否註冊、服務是否被建立 |
| UI 有裝置但 Flow 選不到 | `NodeConfigurator` 類型過濾 |
| Flow 能選但命令失敗 | MQTT Topic、Token、命令格式、裝置線上狀態 |
| 狀態不刷新 | 服務狀態事件、MQTT 回包、UI 綁定 |
| 多台裝置混用錯誤 | Resource ID、Code、Name、服務字典 key |

## 交接邊界

設備服務只負責把裝置能力暴露給系統。模板決定何時用、Flow 決定怎麼編排、結果展示決定怎麼看、專案包決定怎麼交付給客戶。


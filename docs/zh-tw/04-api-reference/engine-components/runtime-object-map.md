# Engine 執行時物件目錄

這一頁是排查時的類名索引。看到日誌或呼叫棧裡的物件，可以先從這裡判斷它屬於哪條業務鏈。

| 物件 / 類 | 責任 | 常見來源 | 第一檢查點 |
| --- | --- | --- | --- |
| Startup initializers | 啟動時註冊 Engine 能力 | 主程式啟動、模組載入 | 是否執行、是否拋例外 |
| `ServiceManager` | 持有裝置服務集合 | `Services/` | 服務是否存在、狀態是否正確 |
| `DeviceServiceFactoryRegistry` | 查找和建立裝置服務 | 資源載入 | Type 是否註冊 Factory |
| `DeviceService<TConfig>` | 單個裝置執行時服務 | `Services/Devices/**` | Config、Resource ID、連線狀態 |
| `MQTTControl` | MQTT 連線與命令通道 | `MQTT/`, 裝置服務 | Topic、Token、回包、超時 |
| `TemplateControl` | 模板管理入口 | `Templates/` | 模板是否載入、字典 ID 是否正確 |
| `TemplateModel<T>` | 具體模板模型 | 各模板目錄 | `Code`、`Title`、預設值 |
| `TemplateFlow` | Flow 模板管理 | `Templates/Flow/` | `.cvflow` 保存/匯入 |
| `FlowControl` | Flow UI 與執行接入 | Flow 視窗 | 節點、連線、狀態 |
| `FlowEngineControl` | Flow 執行控制核心 | `FlowEngineLib/` | 開始/結束節點、`FlowCompleted` |
| `NodeConfiguratorRegistry` | 節點配置器註冊 | `Templates/Flow/NodeConfigurator/` | 節點能否選模板/裝置 |
| `AlgResultMasterModel` | 演算法主結果 | DAO / 結果服務 | 結果 ID、批次號、狀態 |
| `ViewResultAlg` | 結果展示入口 | 結果視圖 | 是否拿到明細與影像 |
| `IResultHandleBase` | 結果處理器介面 | `ViewHandleXxx` | `CanHandle` 是否命中 |
| `IViewResult` | 可視化結果模型 | 各演算法結果 | 座標、ROI、顯示欄位 |
| `MeasureBatchModel` | 批次資料 | 批次服務 / 結果服務 | 批次號與結果關聯 |
| `ObjectiveTestResult` | 專案最終判定 | `Projects/*` | 客戶欄位、結果映射、輸出 |

## 使用方式

1. 從日誌或錯誤訊息拿到類名。
2. 在表中判斷它屬於裝置、模板、Flow、結果還是專案輸出。
3. 回到對應專題頁查鏈路。
4. 再進程式碼定位具體方法。


# DataLoad 資料載入模板

本頁說明 `Engine/ColorVision.Engine/Templates/DataLoad/` 與 Flow 資料載入節點的關係。`DataLoad` 不是影像演算法，也沒有獨立結果 handler；它負責把設備、流水號、結果類型和 ZIndex 等資料定位資訊送到服務端。

## 適用範圍

| 事項 | 目前實作 |
| --- | --- |
| 模板類別 | `TemplateDataLoad : ITemplate<DataLoadParam>, IITemplateLoad` |
| 參數類別 | `DataLoadParam : ParamModBase` |
| 模板代碼 | `DataLoad` |
| 字典 ID | `TemplateDicId = 22` |
| Flow 節點 | `AlgDataLoadNode`、`AlgDataLoadNode2` |
| Flow 操作碼 | `operatorCode = "DataLoad"` |
| 配置器 | `AlgDataLoadNodeConfigurator` |
| 結果 handler | 目前沒有 `ViewHandleDataLoad` |

## 參數模型

| 參數 | 類型 | 說明 |
| --- | --- | --- |
| `DeviceCode` | `string?` | 資料來源設備 Code。 |
| `ResultType` | `CVCommCore.CVResultType` | 要載入的結果類型。 |
| `SerialNumber` | `string?` | 批次或流水號。 |
| `ZIndex` | `int` | Flow 或服務端用於定位資料層級的索引。 |

`AlgDataLoadNode` 是模板驅動，選擇 DataLoad 模板後送出 `TemplateParam`。`AlgDataLoadNode2` 是顯式參數驅動，直接送出 `DeviceCode`、`SerialNumber`、`ResultType` 字串和 `ZIndex`。

## 邊界

不要把 DataLoad 寫成檔案匯入器。當前實作沒有選擇檔案或解析檔案格式，而是把資料定位參數交給演算法服務或 Flow 鏈路，由下游服務讀取資料。

## 常見排查

| 現象 | 優先排查 |
| --- | --- |
| Flow 找不到模板 | `TemplateDataLoad.Params` 是否載入，`TemplateDicId = 22` 是否存在。 |
| 載入錯批次 | `SerialNumber` 來源是模板、節點屬性還是上游 Flow。 |
| 載入錯設備 | `DeviceCode/DataDeviceCode` 是否指向預期服務。 |
| 結果類型錯 | `CVResultType.ToString()` 是否符合服務端期望。 |
| 多層結果取錯 | `ZIndex/DataZIndex` 是否與 Flow 節點層級一致。 |

## 交接清單

- 說明流程使用 `AlgDataLoadNode` 還是 `AlgDataLoadNode2`。
- 記錄設備 Code、結果類型、流水號來源和 ZIndex 語義。
- 服務協議改動時同步更新 `DataLoadData`、`DataLoadData2` 和 Flow 文檔。
- 若後續需要真正檔案匯入，應新增明確檔案參數和失敗提示。

## 延伸閱讀

- [Engine 模板與 Flow 鏈路](../../engine-components/template-flow-chain.md)
- [流程引擎](./flow-engine.md)
- [目前演算法模板覆蓋清單](../current-algorithm-template-coverage.md)

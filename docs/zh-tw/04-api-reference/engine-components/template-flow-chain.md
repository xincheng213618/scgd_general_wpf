# Engine 模板與 Flow 鏈路

這一頁說明模板如何變成可編輯、可保存、可執行的 Flow。

## 主鏈路

```text
TemplateControl / TemplateModel<T>
  -> TemplateFlow
  -> FlowEngineControl / FlowControl
  -> NodeConfiguratorRegistry
  -> Flow 節點執行
  -> FlowCompleted
  -> 批次 / 結果 / Projects
```

## 模板類型

| 類型 | 常見位置 | 交接重點 |
| --- | --- | --- |
| JSON 模板 | `Templates/Jsons/` | 參數、預設值、相容性 |
| POI / 演算法模板 | `Templates/POI/`, `Templates/ARVR/` | 結果 key、顯示模型、DAO |
| Flow 模板 | `Templates/Flow/` | 節點、連線、`.cvflow` 保存/匯入 |
| 裝置動作模板 | `Services/Devices/**` 或模板目錄 | 裝置命令、MQTT、超時 |

## NodeConfigurator 的角色

`NodeConfiguratorRegistry` 決定節點在 UI 裡能配置什麼。新增節點時，如果只加執行節點而不加配置器，就會出現節點能存在但無法正確選模板或裝置的情況。

要檢查：

- 節點類型是否註冊。
- 可選模板類型是否正確。
- 可選裝置類型是否正確。
- 參數保存後是否能重新載入。
- 匯入舊 `.cvflow` 是否保持相容。

## 常見模板接入點

| 模板/入口 | Flow 交接點 |
| --- | --- |
| [FocusPoints 關注點模板](../algorithms/templates/focus-points-template.md) | `AlgorithmNode` 的發光區檢測映射到 `operatorCode = "FocusPoints"`。 |
| [ImageCropping 圖像裁剪模板](../algorithms/templates/image-cropping-template.md) | `AlgorithmType.圖像裁剪` 與 `OLEDImageCroppingNode` 都會綁定 `TemplateImageCropping`。 |
| [模板選單入口](../algorithms/templates/template-menu-entries.md) | 選單只負責打開模板編輯器，Flow 節點能否選模板仍由 `NodeConfigurator` 決定。 |

## Flow 執行驗收

1. 新建 Flow，新增節點，保存。
2. 關閉後重新打開，確認節點和參數仍在。
3. 匯入已有 `.cvflow`。
4. 執行流程，確認開始/結束節點狀態。
5. 確認 `FlowCompleted` 觸發。
6. 確認批次、結果表、專案包都能讀到結果。

## 常見問題

| 現象 | 可能原因 |
| --- | --- |
| 節點保存後丟參數 | 模型欄位未序列化或屬性名變更 |
| 節點能跑但沒有結果 | 結果 key 未對上或 DAO 未落庫 |
| Flow 匯入失敗 | 節點類型名、模板 ID、版本相容問題 |
| 裝置節點不可選 | `NodeConfigurator` 過濾條件或服務未建立 |

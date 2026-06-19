# JND 模板

本頁說明 `Engine/ColorVision.Engine/Templates/JND/` 的業務鏈路。JND 模板本身只保存少量演算法參數，但執行時必須同時選擇 POI 模板，結果也按 POI 點位展示和匯出。

## 適用範圍

| 事項 | 當前實現 |
| --- | --- |
| 模板代碼 | `OLED.JND.CalVas` |
| 模板類 | `TemplateJND : ITemplate<JNDParam>, IITemplateLoad` |
| 參數類 | `JNDParam` |
| 依賴模板 | `TemplatePoi` |
| 執行入口 | `AlgorithmJND`，顯示名 `JND` |
| UI 面板 | `DisplayJND.xaml(.cs)` |
| MQTT 事件 | `MQTTAlgorithmEventEnum.Event_OLED_JND_CalVas_GetData` |
| 結果處理 | `ViewHandleJND` |
| 主要結果類型 | `Compliance_Math_JND`、`JND_CalVas` |

## 源碼入口

| 檔案 | 交接用途 |
| --- | --- |
| `TemplateJND.cs` | 註冊 JND 模板，`TemplateDicId = 30`，`Code = OLED.JND.CalVas`。 |
| `JNDParam.cs` | 保存 `CutOff`。 |
| `AlgorithmJND.cs` | 同時收集 JND 和 POI 模板並發布請求。 |
| `DisplayJND.xaml.cs` | 選擇 JND 模板、POI 模板、圖像和設備來源。 |
| `ViewHandleJND.cs` | 載入結果、展示表格、繪製 POI 點、導出 CSV 和圖像。 |
| `ViewRsultJND.cs` | 把 POI 結果中的 JSON 解析為 `POIResultDataJND`。 |
| `MysqlJND.cs` | 恢復字典，預設 `CutOff = 0.3`。 |

## 執行鏈路

1. `TemplateJND` 載入到 `TemplateJND.Params`。
2. `DisplayJND` 同時綁定 `TemplateJND.Params` 和 `TemplatePoi.Params`。
3. 使用者選擇 JND 模板、POI 模板和輸入圖像。
4. `AlgorithmJND.SendCommand(...)` 發送 `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateParam` 和 `POITemplateParam`。
5. 命令發布到 `Event_OLED_JND_CalVas_GetData`。
6. `ViewHandleJND` 透過 `PoiPointResultDao` 取點位，`ViewRsultJND` 解析 `h_jnd` / `v_jnd`。

## 參數與結果

| 項目 | 說明 |
| --- | --- |
| `CutOff` | 輪廓裁剪係數，預設 `0.3`。變更時保留圖像、POI 模板和服務版本。 |
| `h_jnd` | 橫向 JND 結果。 |
| `v_jnd` | 縱向 JND 結果。 |
| POI 點位 | JND 消費 `TemplatePoi`，點位變更會直接影響結果。 |

## 專案交接邊界

`ProjectShiyuan` 會使用 JND/POI 匯出和 JND 驗證。不要把「JND CSV 已生成」等同於產品 PASS；專案側還可能讀取 `Compliance_Math_JND`、檢查 `Validate`、複製圖像或生成偽彩圖。

相關頁：[ProjectShiyuan](../../projects/project-shiyuan.md)。

## 常見排查

| 現象 | 優先排查 |
| --- | --- |
| POI 相關錯誤 | `TemplatePoi.Params` 是否載入，`TemplatePoiSelectedIndex` 是否有效。 |
| JND 結果為空 | 結果類型和 `PoiPointResultDao` 是否能按 `Pid` 查到資料。 |
| 表格有點但 JND 值空 | `PoiPointResultModel.Value` 是否能反序列化為 `POIResultDataJND`。 |
| 專案 OK/NG 不一致 | 回看專案側 JND 二次驗證。 |
| 導出路徑異常 | 檢查 `SideSave(...)` 中 `selectedPath` 的語義。 |

## 交接清單

- 修改 `CutOff` 時，同步更新 `JNDParam.cs`、`MysqlJND.cs` 和現場推薦值。
- 修改 POI 選擇或座標系時，同步更新 [POI 模板](./poi-template.md) 和專案文件。
- 修改結果欄位時，同步更新 `ViewRsultJND.cs`、導出列和驗收樣例。
- 專案依賴 JND 判定時，專案頁必須說明最終 OK/NG 來源。

## 繼續閱讀

- [POI 模板](./poi-template.md)
- [POI 原語](../primitives/poi.md)
- [ProjectShiyuan](../../projects/project-shiyuan.md)
- [結果交接鏈路](../../engine-components/result-handoff-chain.md)
- [目前演算法模板覆蓋清單](../current-algorithm-template-coverage.md)

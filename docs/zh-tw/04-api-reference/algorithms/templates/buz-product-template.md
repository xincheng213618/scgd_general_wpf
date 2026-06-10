# BuzProduct 產品業務參數模板

本頁說明 `Engine/ColorVision.Engine/Templates/BuzProduct/` 的業務邊界。`BuzProduct` 不是獨立演算法執行入口，而是把產品設定、POI 點位和 Validate 判定模板綁在一起的產品/業務模板，供專案包和現場模板複用。

## 適用範圍

| 事項 | 目前實作 |
| --- | --- |
| 模板代碼 | `BuzProduc`，原始碼目前就是這個拼寫 |
| 模板類別 | `TemplateBuzProduc : ITemplateBuzProduc<TemplateBuzProductParam>, IITemplateLoad` |
| 參數類別 | `TemplateBuzProductParam` |
| 編輯控制項 | `EditTemplateBuzProduct.xaml(.cs)` |
| MySQL 恢復入口 | `MysqlBuzProduct` |
| 主表 | `t_scgd_buz_product_master` |
| 明細表 | `t_scgd_buz_product_detail` |
| 關鍵依賴 | `TemplateComplyParam.CIEParams`、POI 模板、Validate 規則模板 |

## 原始碼入口

| 檔案 | 交接用途 |
| --- | --- |
| `TemplateBuzProduc.cs` | 註冊模板標題、代碼、編輯控制項和 MySQL 恢復命令。 |
| `ITemplateBuzProduc.cs` | 實作載入、儲存、建立、複製、匯入、匯出和刪除。 |
| `TemplateBuzProductParam.cs` | 將主表模型、明細集合和新增明細命令暴露給編輯器。 |
| `BuzProductMasterDao.cs` | `t_scgd_buz_product_master` 的 SqlSugar 模型與 DAO。 |
| `BuzProductDetailDao.cs` | `t_scgd_buz_product_detail` 的 SqlSugar 模型與 DAO。 |
| `EditTemplateBuzProduct.xaml(.cs)` | 編輯產品業務項，並從 `CIEParams` 載入 Validate 選項。 |
| `MysqlBuzProduct.cs` | 恢復主表和明細表結構。 |

## 資料表

`t_scgd_buz_product_master` 儲存產品或業務模板主檔，常用欄位包括 `code`、`name`、`buz_type`、`cfg_json`、`img_file`、`is_enable`、`is_delete`、`tenant_id` 和 `remark`。

`t_scgd_buz_product_detail` 儲存主檔下的檢測項或業務點位設定，常用欄位包括 `code`、`name`、`pid`、`poi_id`、`order_index`、`cfg_json` 和 `val_rule_temp_id`。

`val_rule_temp_id` 是交接時最重要的欄位，它指向 Validate 產生的判定規則模板，改動後會影響產品明細的最終合規結果。

## 生命週期

1. 模板系統發現 `TemplateBuzProduc` 後呼叫 `Load()`。
2. `Load()` 讀取 `is_delete = 0` 的主檔。
3. 每個主檔透過 `BuzProductDetailDao.GetAllByPid(...)` 載入明細。
4. 編輯器綁定 `TemplateBuzProductParam.BuzProductDetailModels`。
5. `Save()` 儲存主檔名稱和每條明細。
6. `Create()` 建立新主檔，並複製匯入或複製來源的明細，入庫前重新產生主鍵。
7. `Delete()` 刪除主檔和對應明細。

## 與 Validate 的關係

`EditTemplateBuzProduct` 初始化判定規則下拉框時會讀取：

```csharp
TemplateComplyParam.CIEParams.SelectMany(kvp => kvp.Value).ToList()
```

| BuzProduct 明細 | Validate 規則 | 結果影響 |
| --- | --- | --- |
| `poi_id` | 指定檢測點位或區域。 | 決定演算法結果落在哪個業務點。 |
| `val_rule_temp_id` | 指定使用哪套規則模板。 | 影響 Compliance 或專案層 OK/NG。 |
| `cfg_json` | 儲存明細額外設定。 | 可能被專案包二次解讀。 |

## 匯入與匯出

單模板匯出為 `.cfg`，多模板匯出為 `.zip`。匯入和複製會重新產生資料庫 ID。將模板搬到另一台機器時，必須同時確認 POI 模板、Validate 字典和 Validate 規則模板。

## 常見排查

| 現象 | 優先排查 |
| --- | --- |
| 找不到模板 | 搜尋 `BuzProduc`，不要只搜修正拼寫後的 `BuzProduct`。 |
| 判定規則下拉為空 | `TemplateComplyParam` 是否已載入 `CIEParams`。 |
| 儲存後判定沒變 | 明細 `val_rule_temp_id` 是否指向預期模板。 |
| 換專案包後點位不對 | `poi_id` 是否對應目前專案的 POI 模板。 |
| 匯入後 ID 混亂 | 匯入/複製會重建 ID，需在目標庫重新檢查引用。 |

## 交接清單

- 說明主表和明細表的用途，不要只寫「產品模板」。
- 記錄每個產品模板對應的 POI 模板、Validate 規則和專案包。
- 改動 `val_rule_temp_id` 時，同步更新驗收樣例和專案說明。
- 現場遷移時同時檢查 BuzProduct、POI、Validate 字典和 Validate 規則。
- 不要隨意修正 `BuzProduc` 拼寫，這已經是持久化模板代碼邊界。

## 延伸閱讀

- [Validate 判定規則模板](./validate-rules.md)
- [Compliance 結果交接](./compliance-results.md)
- [POI 模板](./poi-template.md)
- [模板管理](./template-management.md)
- [目前演算法模板覆蓋清單](../current-algorithm-template-coverage.md)

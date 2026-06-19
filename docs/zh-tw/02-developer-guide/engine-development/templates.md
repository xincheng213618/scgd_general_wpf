# Engine 模板系統開發交接手冊

本頁說明 `Engine/ColorVision.Engine/Templates/` 的真實模板模型。模板負責參數、編輯、保存、匯入匯出和演算法命令參數；客戶最終判定和報表格式應放在專案包。

## 模板執行鏈路

| 階段 | 關鍵物件 | 說明 |
| --- | --- | --- |
| 模板實例註冊 | `ITemplate`、`TemplateControl.AddITemplateInstance` | 模板建立後進入全域模板表 |
| 模板發現 | `IITemplateLoad`、`TemplateControl` | 啟動時掃描並實例化可載入模板 |
| 參數集合 | `TemplateModel<T>`、`TemplateParams` | UI 下拉和編輯視窗綁定的真實集合 |
| MySQL 模板 | `ITemplate<T>`、`ParamModBase` | 按 `TemplateDicId` 讀取 `ModMasterModel` / `ModDetailModel` |
| JSON 模板 | `ITemplateJson<T>`、`TemplateJsonParam` | 以 JSON 承載複雜演算法參數 |
| Flow 綁定 | `Templates/Flow/`、`NodeConfigurator` | 節點配置面板讀取模板並寫入節點參數 |

## 選擇哪種模板

| 場景 | 推薦模型 |
| --- | --- |
| 字段穩定、來自系統字典 | `ITemplate<T>` + `ParamModBase` |
| 參數層級複雜、隨演算法版本變動 | `ITemplateJson<T>` + `TemplateJsonParam` |
| 設備自己的執行參數 | 設備目錄下的 `Templates/` |
| Flow 流程模板 | `Templates/Flow/TemplateFlow` |
| 客戶輸出格式 | 專案包 `Process` / exporter |

## 新增模板步驟

1. 確認參數屬於通用演算法、設備、Flow 節點還是客戶專案規則。
2. 建立參數類，MySQL 模板繼承 `ParamModBase`，JSON 模板繼承 `TemplateJsonParam`。
3. 建立 `Template*`，繼承 `ITemplate<T>` 或 `ITemplateJson<T>`，需要自動載入時實作 `IITemplateLoad`。
4. 準備靜態 `Params` 集合並賦給 `TemplateParams`。
5. 需要資料庫恢復時實作 `GetMysqlCommand()`，並確認 `TemplateDicId`。
6. 準備編輯入口，並在 Flow 或演算法請求中寫入模板 ID/名稱。

## 常見錯誤

| 現象 | 優先排查 |
| --- | --- |
| 下拉框沒有新模板 | `IITemplateLoad`、`TemplateParams`、`TemplateControl` |
| 保存後重開遺失 | `GetMysqlCommand()`、`TemplateDicId`、`SaveIndex`、MySQL 連線 |
| Flow 節點仍是舊值 | 節點存儲字段、模板 ID 和模板名稱 |
| 演算法拿不到參數 | `Algorithm*` 是否寫入 `TemplateParam` / `POITemplateParam` |

## 驗收清單

- 新建、複製、重新命名、匯入、匯出、刪除模板。
- 保存後重啟主程式，模板仍存在且字段一致。
- 使用新模板跑最小流程。
- 歷史結果、overlay、表格和專案匯出能讀到新結果。
- 舊模板仍可執行。

## 相關文件

- [Engine 模板與 Flow 鏈路](../../04-api-reference/engine-components/template-flow-chain.md)
- [Engine 結果展示與專案交接鏈路](../../04-api-reference/engine-components/result-handoff-chain.md)
- [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md)
- [測試與驗證交接手冊](../testing.md)

# 專案包交接手冊

專案包不是普通工具外掛。它把客戶測試順序、FlowEngine 模板、設備動作、Recipe/Fix、Socket/MES 協議和結果匯出組合成一條生產流程。交接時不要從單個 `Process` 類孤立看起，要先串起：誰觸發測試、跑哪條 Flow、結果寫到哪裡、外部系統怎麼拿結果。發版和現場替換證據見 [專案包發布證據與版本核查表](./project-release-evidence.md)。

## 先判斷專案類型

| 類型 | 典型專案 | 交接重點 |
| --- | --- | --- |
| AR/VR 流程組專案 | `ProjectARVRPro/`, `ProjectLUX/` | `ProcessGroup`, `ProcessMeta`, FlowTemplate, Recipe, Socket |
| 輕量或歷史 AR/VR | `ProjectARVR/`, `ProjectARVRLite/` | 相容性、固定測項順序、CSV 欄位 |
| 業務算法專案 | `ProjectBlackMura/`, `ProjectKB/` | 算法參數、結果模型、報告/匯出 |
| 客製專案 | `ProjectHeyuan/`, `ProjectShiyuan/` | 客戶協議、現場配置、菜單入口、設備依賴 |
| 對接示例 | `ProjectARVRPro.IntegrationDemo/` | 外部系統如何發 JSON 和解析結果 |

## 共用執行鏈

| 步驟 | 程式入口 | 確認點 |
| --- | --- | --- |
| 載入 | `manifest.json`, `PluginConfig/` | `Id`, `dllpath`, 菜單名、最低主程式版本 |
| 初始化 | 主視窗 `InitTest()` | SN 如何寫入、舊結果是否重置 |
| 流程選擇 | `ProcessManager`, `ProcessGroup` | 當前組、啟用步驟、順序和舊配置遷移 |
| 模板綁定 | `ProcessMeta.FlowTemplate` | 名稱能否在 `TemplateFlow.Params` 找到 |
| Flow 執行 | `RunTemplate()` 或 `RunAllAsync()` | 批次、預處理、超時、重試 |
| 結果解析 | `IProcess.Execute(ctx)` | 讀哪個 Engine 結果，Recipe/Fix 如何參與 |
| 結果彙總 | `ObjectiveTestResult` | 欄位或動態集合是否完整 |
| 保存/匯出 | `ViewResultManager`, CSV/XLSX/PDF exporter | SQLite、匯出路徑、Legacy 開關 |
| 外部回傳 | `Services/SocketControl.cs` 或 handler | JSON/文字協議、狀態碼、最終事件 |

## 高風險欄位

| 欄位 | 風險 |
| --- | --- |
| `ProcessMeta.FlowTemplate` | 名稱不匹配會導致 Flow 不啟動 |
| `ProcessMeta.ProcessTypeFullName` | 類名或命名空間改動會破壞舊配置 |
| `ProcessMeta.IsEnabled` | 影響自動化和最終結果完整性 |
| `ProcessMeta.SocketCode` | ProjectLUX 外部命令能否找到步驟 |
| `PictureSwitchConfig` | ARVRPro 串口切圖、返回、超時和延時 |

## 交接檢查

| 項 | 通過條件 |
| --- | --- |
| manifest | `Id`, `dllpath`, `requires` 和包名一致 |
| 菜單入口 | 主程式能開啟專案視窗 |
| 流程組 | 至少一個有效流程組，關鍵步驟已啟用 |
| 模板綁定 | 每個 `FlowTemplate` 都能對到真實模板 |
| Recipe/Fix | 編輯器可開啟、保存、重啟後仍有效 |
| 外部協議 | 命令能初始化、切圖/執行、收到結果 |
| 結果輸出 | SQLite、CSV/XLSX/PDF 或客戶上傳路徑可寫 |
| 相容性 | 舊配置、舊格式或舊協議開關已記錄 |

# 專案包執行與交接場景手冊

本頁面向接手客戶專案包、排查現場協議/結果/流程問題、發布 `.cvxp` 的維護人員。它不替代具體專案頁，而是把常見場景整理成排查路徑。發版、替換或回退時，還要填寫 [專案包發布證據與版本核查表](./project-release-evidence.md)。

## 場景入口

| 問題 | 先看 | 典型專案 |
| --- | --- | --- |
| 安裝後菜單或視窗打不開 | 場景 A | 全部專案包 |
| 外部系統發命令後沒有啟動測試 | 場景 B | ARVR、ARVRLite、ARVRPro、LUX、KB、BlackMura、Heyuan |
| 流程啟動但跑錯測項或模板找不到 | 場景 C | ARVRPro、LUX、KB、Shiyuan |
| 有算法結果但 PASS/FAIL 或客戶欄位不對 | 場景 D | ARVRPro、LUX、KB、BlackMura、Heyuan |
| CSV/XLSX/PDF/SQLite/MES 缺欄位 | 場景 E | ARVR 系列、LUX、KB、BlackMura、Heyuan、Shiyuan |
| 串口、Modbus、MES、PG、切圖異常 | 場景 F | BlackMura、Heyuan、KB、ARVRPro |
| 要打包 `.cvxp` 交付現場 | 場景 G | 除 IntegrationDemo 外全部專案 |
| 客戶只需要對接 Demo | 場景 H | ARVRPro.IntegrationDemo |

## 通用執行鏈

```text
manifest / PluginConfig
  -> 專案視窗
  -> 外部命令或人工啟動
  -> 目前流程組 / 固定流程
  -> FlowTemplate
  -> Engine Flow 執行
  -> IProcess 讀結果並套用 Recipe/Fix
  -> ObjectiveTestResult 彙總
  -> SQLite / CSV / XLSX / PDF / MES / Socket 回傳
```

排查時不要只看 exporter 或 CSV。很多問題發生在更前面：命令沒有匹配、流程組當前項不對、`FlowTemplate` 名稱變了、Recipe/Fix 沒讀到或 `IProcess.Execute()` 沒跑。

## 主要排查清單

| 場景 | 檢查點 |
| --- | --- |
| A：入口打不開 | `Plugins/<ProjectName>/` 是否存在、`manifest.json`、`PluginConfig/` 或 `Menu*.cs`、主程式日誌 |
| B：外部觸發失效 | Socket/串口/Modbus 服務是否開啟、視窗是否已建立、SN/流程組/狀態是否允許啟動 |
| C：流程或模板錯誤 | `ProcessGroups.json`、`ProcessMeta.FlowTemplate`、`ProcessTypeFullName`、`SocketCode` |
| D：判定錯誤 | Flow 是否完成、`IProcess.Execute()` 是否匹配、Recipe/Fix、`ObjectiveTestResult` |
| E：輸出錯誤 | 本地結果、Legacy 開關、文件輸出、Socket/MES 回傳和 SQLite 一起驗 |
| F：外設鏈路錯誤 | 原始命令、返回碼、超時、DeviceId、寄存器地址、切圖期望返回 |
| G：打包交付 | manifest、README/CHANGELOG、配置、外部 DLL、輸出路徑、回退包 |
| H：Demo 驗證 | 樣例 JSON、線上連接、半包/黏包處理、CSV 匯出 |

## 專案包交接記錄

每次交接至少記錄：專案名、客戶/場景、版本、構建命令、協議、流程組、Recipe/Fix、輸出路徑、驗收結果、回退包和已知限制。

## 繼續閱讀

- [專案包能力與交接矩陣](./project-capability-matrix.md)
- [專案包發布證據與版本核查表](./project-release-evidence.md)
- [專案包交接手冊](./project-handoff.md)
- 各具體專案頁

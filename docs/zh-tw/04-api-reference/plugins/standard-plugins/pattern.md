# Pattern / 圖卡生成功能

本頁不再把 Pattern 寫成一個“當前倉庫裡完整存在、可直接開發的標準外掛”。按現有原始碼狀態看，舊文件描述的那套獨立 `Plugins/Pattern/` 外掛實現已經無法在倉庫中對上。

## 當前結論

從當前原始碼樹看：

- `Plugins/` 目錄下實際存在的是 `Conoscope/`、`EventVWR/`、`Spectrum/`、`SystemMonitor/`、`WindowsServicePlugin/` 等專案。
- 當前倉庫裡不存在 `Plugins/Pattern/` 目錄，也沒有對應的 `.csproj`、視窗、`manifest.json` 或獨立外掛入口實現。
- 舊文件裡提到的 `IPattern`、`IPatternBase<T>`、`PatternManager`、批次生成介面等型別，在當前倉庫原始碼中也沒有可對應的真實定義。

因此這頁現在只能作為“圖卡相關能力在倉庫裡的剩餘落點說明”，而不能繼續充當獨立外掛 API 參考。

## 倉庫裡實際還能對上的相關程式碼

雖然獨立 Pattern 外掛專案缺失，但倉庫裡仍然保留了和 PG/圖卡切換相關的兩條程式碼線：

### `Engine/cvColorVision/PG.cs`

這部分是對 `cvCamera.dll` 中 PG 相關原生介面的 P/Invoke 封裝，當前能明確看到的能力包括：

- `CM_InitPG`
- `CM_ConnectToPG`
- `CM_StartPG`
- `CM_StopPG`
- `CM_ReSetPG`
- `CM_SwitchUpPG`
- `CM_SwitchDownPG`
- `CM_SwitchFramePG`

這說明當前倉庫裡更可靠的“圖卡相關能力”是 PG 裝置控制，而不是一個自帶圖案編輯器的高層外掛工程。

### `Engine/FlowEngineLib/PG/PGLoopNode.cs`

FlowEngine 側還有 `PGLoopNode`，它會把迴圈節點中的 PG 參數轉換為命令列表，再透過流程執行鏈下發：

- `開始` -> `CM_StartPG`
- `停止` -> `CM_StopPG`
- `重置` -> `CM_ReSetPG`
- `上` / `下` -> 切換圖卡
- `指定` -> `CM_SwitchFramePG`

這更像“流程裡控制 PG 裝置切換圖卡”的能力，而不是舊文件裡那種本地生成 11 種圖案並匯出 PNG/JPEG/BMP 的完整外掛。

## 為什麼舊文件現在不能繼續照用

舊頁裡有幾類內容，當前都已經無法用原始碼證明：

- 聲稱存在獨立 `Pattern` 外掛專案和選單入口。
- 聲稱存在 `IPattern` / `IPatternBase<T>` 這類擴充套件介面。
- 聲稱支援 11 種圖案、本地模板管理、批次匯出、預覽最佳化等一整套高層功能。
- 給出了大量並不存在於當前倉庫的示例 API 和擴充套件程式碼。

繼續保留這些內容，只會讓讀者誤以為當前倉庫裡還能直接找到對應實現。

## 當前更合理的理解方式

如果你現在在這個倉庫裡追“圖卡”能力，優先應這樣理解：

1. 先把它視為 PG 裝置控制鏈的一部分，而不是獨立 WPF 外掛。
2. 先看 `cvColorVision/PG.cs`，確認底層能發哪些 PG 命令。
3. 再看 `FlowEngineLib/PG/PGLoopNode.cs`，理解流程裡如何批次或迴圈切換 PG。
4. 如果還需要查更高層的圖卡生成 UI，請先確認對應原始碼是否已經遷出倉庫、位於其他專案，或只是文件殘留。

## 當前應如何理解這些入口

- 根目錄的 [Plugins/README.md](../../../../Plugins/README.md) 現在已經明確區分了“當前原始碼裡真實存在的外掛目錄”和歷史殘留名稱。
- API 參考中的本頁則只保留 Pattern 相關能力在當前倉庫中的剩餘落點說明，不再把它寫成現存標準外掛。
- 如果兩個入口存在表述差異，應優先以原始碼目錄和執行時裝載行為為準。

## 繼續閱讀

- [Engine 元件總覽](../../engine-components/README.md)
- [FlowEngineLib 架構](../../../03-architecture/components/engine/flow-engine.md)
- [演算法系統概覽](../../algorithms/overview.md)

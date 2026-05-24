# 外掛與現狀頁

本章只保留兩類內容：

- 當前工作區裡仍能直接對上原始碼的外掛專題頁
- 對應原始碼已缺失或不再完整維護、因此改寫成“歷史狀態說明”的舊外掛頁

它不再承擔“完整外掛目錄”這個角色，也不預設這裡列到的每一頁都代表當前原始碼樹中可直接開發的外掛專案。

## 先理解這章的邊界

- 當前外掛裝載模型應以 `manifest.json` 和 `UI/ColorVision.UI/Plugins/PluginLoader.cs` 的實際實現為準。
- 外掛 API 參考頁只覆蓋當前文件裡已經收束過的少數專題，不等於 `Plugins/` 目錄的完整映象。
- 如果文件描述和當前原始碼目錄不一致，應優先以原始碼目錄和執行時裝載行為為準。

## 當前包含哪些頁面

### 當前仍能和原始碼直接對上的專題

- [Spectrum 外掛](./standard-plugins/spectrum.md)
- [SystemMonitor 外掛](./standard-plugins/system-monitor.md)
- [EventVWR 外掛](./standard-plugins/eventvwr.md)
- [Windows 服務外掛](./standard-plugins/windows-service.md)

### 歷史狀態說明頁

- [Pattern / 圖卡生成功能](./standard-plugins/pattern.md)
- [ImageProjector（歷史狀態）](./standard-plugins/image-projector.md)
- [ScreenRecorder（歷史狀態）](./standard-plugins/screen-recorder.md)

這些頁面保留的目的，是解釋“當前倉庫裡還能不能對上原始碼、應該去哪裡找現狀”，而不是繼續扮演功能承諾頁。

## 怎麼讀這一章更有效

1. 先看 [外掛開發概覽](../../02-developer-guide/plugin-development/overview.md)，理解外掛入口、產物形態和執行時邊界。
2. 再確認目標外掛當前是否真在 `Plugins/` 目錄中存在對應原始碼。
3. 如果頁面明確寫成“歷史狀態”，應把它當作現狀說明，而不是當作當前開發手冊。
4. 如果要追執行時裝載鏈，應回到 `PluginLoader` 和各外掛目錄下的 `manifest.json` 對照閱讀。

## 當前已知空白

- 當前 API 參考並沒有覆蓋 `Plugins/` 目錄裡的全部真實專案。
- 像 Conoscope 這類當前仍存在原始碼的外掛，目前還沒有單獨的 API 參考頁。
- 因此這章更適合作為“已整理專題入口”，不適合作為“外掛全量索引”。

## 繼續閱讀

- [API 參考總覽](../README.md)
- [外掛開發概覽](../../02-developer-guide/plugin-development/overview.md)
- [FlowEngineLib 節點擴充套件](../extensions/flow-node.md)
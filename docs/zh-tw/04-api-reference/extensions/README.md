# 擴充套件點概覽

本章只保留當前能直接和程式碼對上的擴充套件點專題，不再維護“所有擴充套件機制一覽”的舊式總表。

## 當前覆蓋範圍

目前這一分支只收束了一類穩定專題：

- [FlowEngineLib 節點擴充套件](./flow-node.md)

這意味著當前 `extensions/` 不是完整擴充套件百科，而是一個很窄的“已整理擴充套件點入口”。

## 先把邊界分清

- 外掛發現、裝載和部署不屬於這裡，應該去看 [外掛與現狀頁](../plugins/README.md) 和 [外掛開發概覽](../../02-developer-guide/plugin-development/overview.md)。
- 演算法模板和流程模板不屬於這裡，應該去看 [演算法與模板概覽](../algorithms/README.md)。
- 執行時模組之間的依賴關係，也不在這裡展開，應該回到 [架構設計](../../03-architecture/README.md)。

## 怎麼使用這一章

1. 先確認你要擴的是“Flow 節點”還是“外掛/模板/服務”。
2. 如果是 Flow 節點，再進入 [FlowEngineLib 節點擴充套件](./flow-node.md)。
3. 如果問題更偏執行時執行鏈，再結合 [FlowEngineLib 架構](../../03-architecture/components/engine/flow-engine.md) 一起讀。

## 為什麼這裡只有一頁

- 當前倉庫裡真正被文件收束成穩定專題的擴充套件點並不多。
- 與其繼續維護一個表面完整、實際很快過期的擴充套件目錄，不如只保留和程式碼能直接核對的入口。

## 繼續閱讀

- [API 參考總覽](../README.md)
- [外掛與現狀頁](../plugins/README.md)
- [FlowEngineLib 架構](../../03-architecture/components/engine/flow-engine.md)
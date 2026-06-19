# API 參考

本章節現在只保留已經收束成“當前實現導讀”的穩定入口，不再繼續把所有專題頁平鋪成一層目錄。

## 推薦入口

### UI 與客戶端層

- [UI 元件總覽](./ui-components/README.md)

### Engine 與執行時層

- [Engine 元件總覽](./engine-components/README.md)

### 模板與演算法接入層

- [演算法與模板概覽](./algorithms/README.md)
- [演算法系統概覽](./algorithms/overview.md)

### 外掛與擴充套件層

- [外掛與現狀頁](./plugins/README.md)
- [擴充套件點概覽](./extensions/README.md)

### 客戶專案包層

- [專案包總覽](./projects/README.md)
- [目前專案文件覆蓋清單](./projects/current-project-coverage.md)

## 當前整理原則

- 頂層首頁只掛經過收束的總覽頁，不再把所有單頁都當成首屏入口。
- 細分專題頁仍保留在各自目錄中，但預設需要和原始碼對照閱讀。
- 如果文件與實現不一致，以原始碼、XML 註釋和實際執行行為為準。

## 當前章節邊界

- `ui-components/` 主要覆蓋 WPF UI 側模組與桌面殼層。
- `engine-components/` 主要覆蓋 Engine 目錄下的執行時模組，而不是完整演算法百科。
- `algorithms/` 主要覆蓋 Templates 系統和演算法接入鏈，而不是所有底層影像運算元目錄。
- `plugins/` 主要覆蓋當前工作區裡仍能對上原始碼的標準外掛，以及少量歷史殘留外掛的現狀說明頁。
- `projects/` 主要覆蓋 `Projects/` 中的客戶專案包、對接示例、交接矩陣和現場排查手冊。
- `extensions/` 當前主要保留 Flow 節點擴充套件這一類和實際程式碼能直接對上的擴充套件點專題。

## 補充閱讀方式

- `plugins/` 下的頁面同時包含“當前原始碼可對上”的外掛專題和“歷史狀態說明”頁，進入單頁前應先看章節概覽。
- `extensions/` 當前覆蓋範圍很窄，主要是 Flow 節點擴充套件這類可以直接和程式碼錨點對上的專題，不是完整擴充套件機制百科。
- 這兩類頁面都更適合作為按需查閱入口，而不是替代使用者指南或開發指南的總入口。

## 建議閱讀順序

1. 先看 [UI 元件總覽](./ui-components/README.md)，理解客戶端殼層和 UI 基礎設施。
2. 再看 [Engine 元件總覽](./engine-components/README.md)，理解服務、模板和流程執行時。
3. 最後看 [演算法與模板概覽](./algorithms/README.md) 與 [演算法系統概覽](./algorithms/overview.md)，把模板和演算法接入鏈串起來。
4. 需要檢視客戶專案包時，進入 [專案包總覽](./projects/README.md) 和 [目前專案文件覆蓋清單](./projects/current-project-coverage.md)。
5. 需要檢視外掛現狀或擴充套件點時，再進入 [外掛與現狀頁](./plugins/README.md) 和 [擴充套件點概覽](./extensions/README.md)。

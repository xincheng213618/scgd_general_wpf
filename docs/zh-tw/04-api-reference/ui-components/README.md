# UI元件概覽

本章現在只保留和當前程式碼實現一致的 UI 模組導讀頁，不再繼續維護舊版總覽裡那種“版本相容矩陣 + 示例程式碼 + 擴充套件教程”的混合寫法。

## 怎麼讀這一章

如果你是第一次進入這個倉庫，建議按下面順序建立認知：

1. 先看 [目前 UI DLL 文件覆蓋清單](./current-ui-dll-coverage.md)，確認目前 10 個 UI 專案、對應文件頁和發布證據沒有缺口。
2. 再看 [UI DLL 元件手冊](./component-handbook.md)，按發布 DLL 建立模組邊界。
3. 如果要發布或替換 DLL，進入 [UI DLL 發布場景手冊](./ui-dll-release-playbook.md) 和 [UI DLL 發布矩陣](./release-matrix.md)。
4. 如果要排查選單、設定、外掛載入、ImageEditor、Socket、Scheduler 或 Solution 工作區，進入 [UI 執行時元件交接手冊](./ui-runtime-handoff.md)。
5. 如果要定位具體控制項或窗口，進入 [UI 元件目錄](./control-catalog.md)。
6. 最後再進入單個 DLL 頁。

## 發布與交接入口

- [UI DLL 元件手冊](./component-handbook.md)：按 DLL 說明每個發布單元的職責和邊界。
- [目前 UI DLL 文件覆蓋清單](./current-ui-dll-coverage.md)：確認目前 UI 專案、文件頁、發布證據和維護規則。
- [UI 元件目錄](./control-catalog.md)：按控制項、窗口、Provider 和擴充點定位源碼。
- [UI 執行時元件交接手冊](./ui-runtime-handoff.md)：說明選單、設定、PropertyGrid、ImageEditor、Socket、Scheduler、Solution 的發現與排障鏈。
- [UI DLL 發布場景手冊](./ui-dll-release-playbook.md)：按發布場景判斷要構建和驗收哪些包。
- [UI DLL 發布矩陣](./release-matrix.md)：列出每個 DLL 的版本、目標框架、依賴、資源和煙測項。
- [UI DLL 發布證據與現場核查表](./dll-release-evidence.md)：整理 `.csproj` 證據、包內容、輸出目錄、消費方驗證和回退記錄。
- [UI DLL 發布手冊](./publishing.md)：通用構建命令、包內容檢查和發布後驗證。

## 模組地圖

### 基礎層

- [ColorVision.Common](./ColorVision.Common.md)：MVVM、共享介面、狀態列後設資料和粗粒度權限基礎。
- [ColorVision.Core](./ColorVision.Core.md)：原生影像/影片能力橋接層，負責 `HImage` 和 P/Invoke 匯出面。

### 功能層

- [ColorVision.Database](./ColorVision.Database.md)：資料庫瀏覽器、Provider 註冊、SQLite 日誌與通用 DAO。
- [ColorVision.ImageEditor](./ColorVision.ImageEditor.md)：`ImageView`、`DrawCanvas`、編輯器工具、開啟器和影像互動主鏈。
- [ColorVision.Scheduler](./ColorVision.Scheduler.md)：Quartz 排程器、任務恢復、執行歷史和管理視窗。
- [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md)：本地 TCP 服務、請求分發、訊息歷史和管理視窗。

### 殼層與工作區

- [ColorVision.Solution](./ColorVision.Solution.md)：工作區、編輯器、終端、多圖檢視和 Solution 側本地 RBAC。
- [ColorVision.UI](./ColorVision.UI.md)：UI 基礎設施集合，涵蓋配置、外掛、選單、屬性編輯器、多語言和日誌等橫切能力。
- [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md)：設定視窗、嚮導、選單管理、配置管理和其他桌面輔助視窗。

### 主題層

- [ColorVision.Themes](./ColorVision.Themes.md)：主題資源字典、主題切換入口和視窗外觀支援。

## 當前幾個容易混淆的邊界

- `ColorVision.UI` 不是單一控制元件庫，而是橫切的 UI 基礎設施集合。
- `ColorVision.Solution` 不是“只有解決方案檔案樹”，它還承接工作區殼層和本地 RBAC 子模組。
- `ColorVision.UI.Desktop` 不是整個產品主入口，它更像桌面輔助視窗和管理工具集合。
- `ColorVision.Core` 不是高層託管影像框架，而是原生互操作層。
- `ColorVision.ImageEditor` 不是純顯示控制元件，它會把開啟器、工具、圖元、overlay 和執行時服務編排在一起。

## 繼續閱讀建議

### 想看配置、選單、權限和外掛

先看：

- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Solution](./ColorVision.Solution.md)
- [ColorVision.Common](./ColorVision.Common.md)

### 想看影像鏈路

先看：

- [ColorVision.Core](./ColorVision.Core.md)
- [ColorVision.ImageEditor](./ColorVision.ImageEditor.md)
- [ColorVision.Themes](./ColorVision.Themes.md)

### 想看桌面工具和運維輔助功能

先看：

- [ColorVision.Database](./ColorVision.Database.md)
- [ColorVision.Scheduler](./ColorVision.Scheduler.md)
- [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md)
- [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md)

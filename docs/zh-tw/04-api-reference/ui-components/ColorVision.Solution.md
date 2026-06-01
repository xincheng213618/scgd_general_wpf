# ColorVision.Solution

本頁只保留當前 `UI/ColorVision.Solution/` 裡最重要、最穩定的入口型別和子模組，不再繼續維護舊文件裡那種“完整 API 白皮書 + 版本清單 + 全面 RBAC 接管”式寫法。

## 模組定位

`ColorVision.Solution` 當前更適合被理解成桌面工作區殼層，而不是單純的“解決方案管理器”。

它現在實際承接的內容包括：

- `.cvsln` 解決方案的建立、開啟和最近檔案管理
- 左側樹形工程瀏覽與新建項入口
- 檔案/資料夾編輯器選擇與開啟
- AvalonDock 文件區和麵板佈局管理
- 內建終端控制元件
- 多影像檢視器和縮圖快取
- Markdown 預覽
- Solution 側本地 RBAC 子模組

這意味著它不是單一視窗，也不是隻圍繞 `SolutionManager` 組織的一層很薄的 UI。

## 當前最關鍵的目錄

從專案目錄看，最值得先認識的是：

- `Editor/`：檔案和資料夾編輯器註冊、選擇和開啟
- `Explorer/`：解決方案樹、節點模型、新建項和上下文選單
- `Workspace/`：AvalonDock 文件區與面板佈局管理
- `Terminal/`：內建終端控制元件和 ConPTY 封裝
- `MultiImageViewer/`：資料夾多圖預覽和縮圖快取
- `RecentFile/`：最近檔案歷史
- `Rbac/`：Solution 側本地使用者、角色、權限、會話與審計模組
- 根目錄的 `SolutionManager.cs`：解決方案開啟、建立與當前工作區切換入口

## 關鍵入口型別

### `SolutionManager`

`SolutionManager` 是當前工作區入口的中心物件。它負責：

- 開啟或建立 `.cvsln`
- 維護最近開啟的解決方案列表
- 生成當前 `SolutionExplorer`
- 根據命令列或最近檔案決定啟動時開啟哪個解決方案

如果要追“解決方案是怎麼進來的”，通常先看它，而不是先看樹控制元件。

### `SolutionExplorer`

`SolutionExplorer` 和 `Explorer/` 目錄下的節點型別一起，負責把目錄、檔案、新建項和右鍵動作組織成樹形工作區。

這部分是“使用者怎麼看到工程結構”的主入口。

### `EditorManager`

`EditorManager` 負責編輯器註冊和分發。當前實現特點很明確：

- 從已載入程式集掃描實現 `IEditor` 的型別
- 透過 `EditorForExtensionAttribute`、`GenericEditorAttribute`、`FolderEditorAttribute` 註冊
- 允許為副檔名配置預設編輯器
- 也支援資料夾編輯器

所以當前編輯器系統不是手寫 switch 表，而是屬性驅動的序號產生器制。

### `WorkspaceManager` 和 `DockLayoutManager`

這兩者負責當前文件工作區的停靠與恢復：

- 查詢並啟用當前文件
- 維護 `ContentId` 和文件選擇狀態
- 儲存和載入 AvalonDock 佈局
- 在佈局恢復時按登錄檔恢復面板和文件內容

如果問題表現為“標籤頁去哪了”“佈局沒恢復”“文件區丟了”，通常先看這條鏈。

### `TerminalControl`

終端能力當前就在這個專案裡，而不是單獨外接服務。`TerminalControl` 當前負責：

- 啟動 `cmd` 或 `powershell`
- 承接 ConPTY 輸出
- 維護螢幕緩衝和命令歷史
- 執行指令碼並處理 URL 點選

所以它更接近一個內建終端 UI 元件，而不是僅僅“呼叫系統終端”。

### `MultiImageViewer`

`MultiImageViewer` 既可以作為獨立 `UserControl` 使用，也透過 `MultiImageViewerEditor` 接到編輯器系統裡。

它當前主要負責：

- 資料夾內多圖載入
- 支援副檔名過濾
- 縮圖顯示
- 與工作區文件標籤頁協同開啟和釋放

## 關於 RBAC，這個模組當前到底承擔什麼

舊文件最大的問題，是把 `ColorVision.Solution` 寫成了“全專案統一 RBAC 權限控制層”。當前程式碼並不是這個狀態。

### 當前真實情況

`Rbac/` 的確是 `ColorVision.Solution` 的一個重要子模組，裡面已經有：

- `RbacManager`
- `LoginWindow`、`UserManagerWindow`、`PermissionManagerWindow`
- 使用者、角色、權限、會話、審計相關實體和服務
- 本地 SQLite 持久化
- `PermissionChecker` 的細粒度權限碼快取

### 但當前邊界也要寫清

這套 RBAC 目前主要作用在它自己的管理視窗和 Solution 側本地權限子系統。

從當前搜尋結果看，`HasPermissionAsync` 和 `PermissionChecker` 的細粒度呼叫幾乎都還留在 `Rbac/` 子目錄中；與此同時，很多視窗入口仍先依賴全域性的 `Authorization.Instance.PermissionMode` 做粗粒度判斷。

所以更準確的描述是：

- `ColorVision.Solution` 內含一個本地 RBAC 子模組
- 它和全域性 `PermissionMode` 並存
- 不能把整個解決方案樹、所有編輯器和全部檔案操作都描述成已經全面接入細粒度權限碼控制

## 當前更適合怎樣讀這個專案

### 想看解決方案入口

先看：

- `SolutionManager.cs`
- `SolutionManagerInitializer.cs`
- `OpenSolutionWindow.xaml(.cs)`

### 想看樹和檔案節點

先看：

- `Explorer/SolutionExplorer.cs`
- `Explorer/SolutionNodeFactory.cs`
- `TreeViewControl.xaml(.cs)`

### 想看檔案怎麼被不同編輯器開啟

先看：

- `Editor/EditorManager.cs`
- `Editor/EditorForExtensionAttribute.cs`
- `Editor/*.cs`

### 想看工作區佈局和文件宿主

先看：

- `Workspace/WorkspaceManager.cs`
- `Workspace/DockLayoutManager.cs`
- `Workspace/LayoutMenuItems.cs`

### 想看本地權限子系統

先看：

- `Rbac/RbacManager.cs`
- `Rbac/Services/`
- `Rbac/Entity/`

## 這頁不再做什麼

本頁不再繼續維護這些高風險內容：

- 過時版本號和目標框架清單
- 假定存在完整公共 API 的大段虛擬碼
- 把 `RbacManager` 寫成全專案統一權限入口
- 把所有檔案操作都寫成已經被細粒度權限完全攔截

如果要補具體類或方法，應在對應子模組頁裡單獨展開，而不是在這裡繼續堆一整頁偽 API。

## 繼續閱讀

- [UI元件概覽](./README.md)
- [安全與權限控制](../../03-architecture/security/overview.md)
- [RBAC 模組](../../03-architecture/security/rbac.md)
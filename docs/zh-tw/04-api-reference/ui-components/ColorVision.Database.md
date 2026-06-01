# ColorVision.Database

本頁只描述 UI/ColorVision.Database 當前已經落地的資料訪問與資料庫瀏覽能力，不再繼續維護舊模板裡那種“資料庫教程 + 示例片段 + 建置驗證記錄”的混合寫法。

## 模組定位

`ColorVision.Database` 當前同時承擔兩類職責：

- 業務實體和 DAO 的基礎資料訪問層
- 面向執行時維護的資料庫瀏覽器與 Provider 體系

其中現在更值得優先關注的主線，是“資料庫優先”的瀏覽器鏈，而不是傳統的實體類掃描模式。

## 當前最關鍵的目錄和檔案

從專案目錄看，最值得先認識的是：

- `DatabaseBrowserWindow.xaml(.cs)`：資料庫瀏覽器主視窗
- `DatabaseBrowserProviderRegistry.cs`：Provider 註冊與懶載入入口
- `IDatabaseBrowserProvider.cs`：瀏覽器 Provider 契約
- `DatabaseBrowserModels.cs`：庫、表、列、分頁模型
- `MySqlControl.cs`：MySQL 配置和 Provider 建立
- `SqliteLog/SqliteLogManager.cs`：SQLite 日誌資料庫和 Provider 建立
- `BaseTableDao.cs`、`EntityBase.cs`、`ViewEntity.cs`：業務實體訪問層基礎型別

## 關鍵入口型別

### DatabaseBrowserWindow

`DatabaseBrowserWindow` 是當前資料庫維護體驗的主入口。它負責：

- 展示資料來源、庫、表的樹形結構
- 在右側按 `DataTable` 方式瀏覽結果集
- 支援搜尋、分頁、排序
- 執行新增、更新、刪除等通用表級操作

它的關鍵特點是：當前瀏覽器不再依賴 C# 實體定義來驅動 UI，而是先從真實資料庫連線拿庫、表、列資訊，再決定如何展示和寫回。

### DatabaseBrowserProviderRegistry

`DatabaseBrowserProviderRegistry` 負責統一管理可瀏覽的資料來源。它當前會懶載入預設 Provider，並向瀏覽器暴露：

- MySQL 預設 Provider
- SQLite 日誌 Provider
- 其他呼叫方自行註冊的 Provider

因此它是當前資料庫瀏覽器體系的排程入口。

### IDatabaseBrowserProvider

`IDatabaseBrowserProvider` 是資料庫瀏覽器最重要的抽象邊界。當前它要求實現方提供：

- 庫列表
- 表列表
- 列資訊
- 分頁查詢
- 插入、更新、刪除

所以這個模組的核心擴充套件點不是“加一個實體類”，而是“註冊一個新的 Provider”。

### MySqlControl

`MySqlControl` 當前不只是連線配置物件，它還承擔：

- MySQL 配置持久化
- 連線字串構造
- MySQL 瀏覽器 Provider 建立

因此 MySQL 相關入口應直接順著它去看，而不是隻看 `BaseTableDao<T>`。

### SqliteLogManager

`SqliteLogManager` 既是 SQLite 日誌管理器，也是瀏覽器體系裡的一個實際 Provider 來源。它會提供日誌資料庫路徑和對應的 SQLite 瀏覽入口。

這也說明 `ColorVision.Database` 當前並不是只服務“業務資料”，還承接了一部分執行日誌落地與瀏覽職責。

### BaseTableDao / EntityBase / ViewEntity

這些型別仍然是當前業務層實體訪問的基礎：

- `IEntity` 統一 `Id`
- `EntityBase` 提供主鍵對映基類
- `ViewEntity` 用於可繫結實體
- `BaseTableDao<T>` 繼續服務已有業務程式碼

但它們已經不是當前資料庫 UI 瀏覽鏈的唯一中心。

## 當前執行時主鏈

這套模組當前更接近下面這條鏈：

1. `DatabaseBrowserWindow` 向 `DatabaseBrowserProviderRegistry` 取可用 Provider。
2. Provider 返回庫、表、列資訊。
3. 瀏覽器按表結構動態展示 `DataTable` 結果。
4. 新增、編輯、刪除透過 Provider 的通用寫介面落回資料庫。
5. 對於業務程式碼，實體和 DAO 體系仍可以並行使用，但不再控制瀏覽器 UI。

## 當前實現有哪些邊界

### 瀏覽器主線已經是“資料庫優先”

這是當前最重要的邊界變化。舊思路更偏向“先有實體，再有表格介面”；現在更重要的是直接從真實資料庫結構生成瀏覽和維護介面。

### Provider 比實體更關鍵

如果要擴一個新的資料庫來源，當前更優先的切入點是實現 `IDatabaseBrowserProvider` 並註冊，而不是給系統補一批實體類。

### DAO 體系仍在，但不是唯一入口

`BaseTableDao<T>` 等型別依然服務現有業務程式碼，但閱讀這個模組時不能再把它們寫成資料庫能力的唯一中心。

## 當前更適合怎樣讀這個模組

### 想看資料庫瀏覽器主鏈

先看：

- `DatabaseBrowserWindow.xaml.cs`
- `DatabaseBrowserProviderRegistry.cs`
- `IDatabaseBrowserProvider.cs`

### 想看 MySQL 和 SQLite 的實際接入

先看：

- `MySqlControl.cs`
- `SqliteLog/SqliteLogManager.cs`

### 想看業務實體訪問層

先看：

- `IEntity.cs`
- `EntityBase.cs`
- `ViewEntity.cs`
- `BaseTableDao.cs`

## 這頁不再做什麼

本頁不再繼續維護這些高風險內容：

- 教程式示例程式碼堆疊
- “最佳實踐”式泛化段落
- 手工建置驗證記錄
- 把資料庫模組寫成只圍繞實體類工作的舊模型

## 繼續閱讀

- [UI元件概覽](./README.md)
- [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md)
- [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md)
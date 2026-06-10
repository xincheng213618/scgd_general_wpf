# UI 元件目錄

本頁按元件類型整理 `UI/` 下真實存在的 WPF 控制項、窗口和擴充點。它和 [UI DLL 元件手冊](./component-handbook.md) 的區別是：元件手冊按發布 DLL 講邊界，本頁按“我要改哪個 UI 能力”定位原始碼。

## 使用方式

| 要找什麼 | 先看 |
| --- | --- |
| 選單、狀態列、快捷鍵 | `ColorVision.UI/Menus`、`StatusBar`、`HotKey` |
| 設定窗口、屬性編輯器 | `ColorVision.UI/ConfigSetting`、`PropertyEditor`、`ColorVision.UI.Desktop/Settings` |
| 主題、窗口外觀、通用控制項 | `ColorVision.Themes/Themes`、`Controls` |
| 圖片顯示、繪圖、overlay | `ColorVision.ImageEditor/ImageView`、`Draw`、`EditorTools` |
| 資料庫、Socket、排程窗口 | `ColorVision.Database`、`ColorVision.SocketProtocol`、`ColorVision.Scheduler` |
| 工作區、檔案樹、終端、RBAC | `ColorVision.Solution` |
| 外掛市場、下載器、嚮導 | `ColorVision.UI.Desktop` |

## 常用入口

| 能力 | 入口 | 所屬 DLL |
| --- | --- | --- |
| MVVM 基類 | `ViewModelBase`、`RelayCommand` | `ColorVision.Common` |
| 選單契約 | `IMenuItem`、`IMenuItemProvider`、`MenuItemAttribute` | `ColorVision.Common` / `ColorVision.UI` |
| 選單載入 | `MenuManager.LoadMenuForWindow` | `ColorVision.UI` |
| 設定發現 | `ConfigSettingManager.GetAllSettings` | `ColorVision.UI` |
| 屬性編輯 | `PropertyEditorWindow`、`PropertyEditorTypeAttribute` | `ColorVision.UI` |
| 主題切換 | `ThemeManager`、`Application.ApplyTheme` | `ColorVision.Themes` |
| 資料庫瀏覽 | `DatabaseBrowserWindow`、`IDatabaseBrowserProvider` | `ColorVision.Database` |
| Socket 管理 | `SocketManagerWindow`、`ISocketJsonHandler` | `ColorVision.SocketProtocol` |
| 任務管理 | `TaskViewerWindow`、`QuartzSchedulerManager` | `ColorVision.Scheduler` |
| 影像主控件 | `ImageView`、`EditorContext`、`EditorToolFactory` | `ColorVision.ImageEditor` |
| 影像打開器 | `IImageOpen` + `FileExtensionAttribute` | `ColorVision.ImageEditor` |
| 影像工具 | `IEditorTool`、`IEditorToggleTool`、`IEditorCustomControlTool` | `ColorVision.ImageEditor` |
| 工作區 | `SolutionManager`、`SolutionExplorer`、`EditorManager` | `ColorVision.Solution` |
| 內建終端 | `TerminalControl` | `ColorVision.Solution` |
| 外掛市場 | `MarketplaceWindow`、`MarketplaceManager` | `ColorVision.UI.Desktop` |
| 下載器 | `DownloadWindow`、`Aria2cDownloadService` | `ColorVision.UI.Desktop` |

## 新增元件時同步更新

| 新能力 | 建議落點 | 同步文檔 |
| --- | --- | --- |
| 通用 ViewModel/命令/共享介面 | `ColorVision.Common` | [ColorVision.Common](./ColorVision.Common.md) |
| 通用主題控制項 | `ColorVision.Themes/Controls` | [ColorVision.Themes](./ColorVision.Themes.md)、本頁 |
| 選單、熱鍵、狀態列、屬性編輯器 | `ColorVision.UI` | [ColorVision.UI](./ColorVision.UI.md)、本頁 |
| 資料庫維護窗口 | `ColorVision.Database` | [ColorVision.Database](./ColorVision.Database.md)、本頁 |
| Socket handler 或管理基礎設施 | `ColorVision.SocketProtocol` | [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md)、本頁 |
| 圖像工具、圖元、overlay | `ColorVision.ImageEditor` | [ColorVision.ImageEditor](./ColorVision.ImageEditor.md)、本頁 |
| 工作區編輯器、終端、RBAC | `ColorVision.Solution` | [ColorVision.Solution](./ColorVision.Solution.md)、本頁 |

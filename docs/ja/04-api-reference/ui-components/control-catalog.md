# UI コンポーネントカタログ

このページは `UI/` に実在する WPF control、window、extension point を機能別に整理します。[UI DLL コンポーネントハンドブック](./component-handbook.md) は DLL 境界を説明し、本ページは「どの UI 能力を変更するか」からソース位置を探します。

## 早見表

| 探すもの | 先に見る場所 |
| --- | --- |
| menu、status bar、hotkey | `ColorVision.UI/Menus`、`StatusBar`、`HotKey` |
| settings、property editor | `ColorVision.UI/ConfigSetting`、`PropertyEditor`、`ColorVision.UI.Desktop/Settings` |
| theme、window style、shared controls | `ColorVision.Themes/Themes`、`Controls` |
| image view、draw、overlay | `ColorVision.ImageEditor/ImageView`、`Draw`、`EditorTools` |
| database、Socket、scheduler windows | `ColorVision.Database`、`ColorVision.SocketProtocol`、`ColorVision.Scheduler` |
| workspace、explorer、terminal、RBAC | `ColorVision.Solution` |
| marketplace、downloader、wizard | `ColorVision.UI.Desktop` |

## 主要エントリ

| 能力 | エントリ | DLL |
| --- | --- | --- |
| MVVM | `ViewModelBase`、`RelayCommand` | `ColorVision.Common` |
| menu contract | `IMenuItem`、`IMenuItemProvider`、`MenuItemAttribute` | `Common` / `UI` |
| menu loading | `MenuManager.LoadMenuForWindow` | `ColorVision.UI` |
| setting discovery | `ConfigSettingManager.GetAllSettings` | `ColorVision.UI` |
| property editing | `PropertyEditorWindow`、`PropertyEditorTypeAttribute` | `ColorVision.UI` |
| theme | `ThemeManager`、`Application.ApplyTheme` | `ColorVision.Themes` |
| database browser | `DatabaseBrowserWindow`、`IDatabaseBrowserProvider` | `ColorVision.Database` |
| Socket manager | `SocketManagerWindow`、`ISocketJsonHandler` | `ColorVision.SocketProtocol` |
| scheduler | `TaskViewerWindow`、`QuartzSchedulerManager` | `ColorVision.Scheduler` |
| image control | `ImageView`、`EditorContext`、`EditorToolFactory` | `ColorVision.ImageEditor` |
| image opener | `IImageOpen` + `FileExtensionAttribute` | `ColorVision.ImageEditor` |
| image tool | `IEditorTool` and derived interfaces | `ColorVision.ImageEditor` |
| workspace | `SolutionManager`、`SolutionExplorer`、`EditorManager` | `ColorVision.Solution` |
| terminal | `TerminalControl` | `ColorVision.Solution` |
| marketplace | `MarketplaceWindow`、`MarketplaceManager` | `ColorVision.UI.Desktop` |
| downloader | `DownloadWindow`、`Aria2cDownloadService` | `ColorVision.UI.Desktop` |

## 更新ルール

新しい public window、Provider、PropertyEditor、EditorTool、IEditor を追加した場合は、本ページ、該当 DLL ページ、必要に応じて [UI ランタイムコンポーネント引き継ぎ](./ui-runtime-handoff.md) と [UI DLL リリースマトリクス](./release-matrix.md) を更新します。

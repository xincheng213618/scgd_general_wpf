# UI 컴포넌트 카탈로그

이 페이지는 `UI/`에 실제로 존재하는 WPF control, window, extension point를 기능별로 정리합니다. [UI DLL 컴포넌트 핸드북](./component-handbook.md)은 DLL 경계를 설명하고, 이 페이지는 “어떤 UI 기능을 수정할 것인가”에서 소스 위치를 찾습니다.

## 빠른 찾기

| 찾는 것 | 먼저 볼 위치 |
| --- | --- |
| menu, status bar, hotkey | `ColorVision.UI/Menus`, `StatusBar`, `HotKey` |
| settings, property editor | `ColorVision.UI/ConfigSetting`, `PropertyEditor`, `ColorVision.UI.Desktop/Settings` |
| theme, window style, shared controls | `ColorVision.Themes/Themes`, `Controls` |
| image view, draw, overlay | `ColorVision.ImageEditor/ImageView`, `Draw`, `EditorTools` |
| database, Socket, scheduler windows | `ColorVision.Database`, `ColorVision.SocketProtocol`, `ColorVision.Scheduler` |
| workspace, explorer, terminal, RBAC | `ColorVision.Solution` |
| marketplace, downloader, wizard | `ColorVision.UI.Desktop` |

## 주요 진입점

| 기능 | 진입점 | DLL |
| --- | --- | --- |
| MVVM | `ViewModelBase`, `RelayCommand` | `ColorVision.Common` |
| menu contract | `IMenuItem`, `IMenuItemProvider`, `MenuItemAttribute` | `Common` / `UI` |
| menu loading | `MenuManager.LoadMenuForWindow` | `ColorVision.UI` |
| setting discovery | `ConfigSettingManager.GetAllSettings` | `ColorVision.UI` |
| property editing | `PropertyEditorWindow`, `PropertyEditorTypeAttribute` | `ColorVision.UI` |
| theme | `ThemeManager`, `Application.ApplyTheme` | `ColorVision.Themes` |
| database browser | `DatabaseBrowserWindow`, `IDatabaseBrowserProvider` | `ColorVision.Database` |
| Socket manager | `SocketManagerWindow`, `ISocketJsonHandler` | `ColorVision.SocketProtocol` |
| scheduler | `TaskViewerWindow`, `QuartzSchedulerManager` | `ColorVision.Scheduler` |
| image control | `ImageView`, `EditorContext`, `EditorToolFactory` | `ColorVision.ImageEditor` |
| image opener | `IImageOpen` + `FileExtensionAttribute` | `ColorVision.ImageEditor` |
| image tool | `IEditorTool` and derived interfaces | `ColorVision.ImageEditor` |
| workspace | `SolutionManager`, `SolutionExplorer`, `EditorManager` | `ColorVision.Solution` |
| terminal | `TerminalControl` | `ColorVision.Solution` |
| marketplace | `MarketplaceWindow`, `MarketplaceManager` | `ColorVision.UI.Desktop` |
| downloader | `DownloadWindow`, `Aria2cDownloadService` | `ColorVision.UI.Desktop` |

## 업데이트 규칙

새 public window, Provider, PropertyEditor, EditorTool, IEditor를 추가하면 이 페이지, 해당 DLL 페이지, 필요하면 [UI 런타임 컴포넌트 인수인계](./ui-runtime-handoff.md)와 [UI DLL 릴리스 매트릭스](./release-matrix.md)를 업데이트합니다.

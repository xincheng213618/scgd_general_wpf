# UI Control Catalog

This page organizes the real WPF controls, windows, and extension points under `UI/` by capability. It differs from the [UI DLL Component Handbook](./component-handbook.md): the handbook is package-oriented, while this page answers "which UI capability do I change and where is the source?" For runtime discovery and troubleshooting paths, read [UI Runtime Component Handoff](./ui-runtime-handoff.md).

## How to Use

| Looking for | Start here |
| --- | --- |
| Menus, status bar, hotkeys | [Menus, Status Bar, Hotkeys](#menus-status-bar-hotkeys) |
| Settings and PropertyGrid | [PropertyGrid and Settings Editing](#propertygrid-and-settings-editing) |
| Themes, window appearance, common controls | [Themes and Common Controls](#themes-and-common-controls) |
| Image display, drawing, overlays | [Image Editor Components](#image-editor-components) |
| Database, Socket, scheduler windows | [Runtime Tool Windows](#runtime-tool-windows) |
| Workspace, file tree, terminal, RBAC | [Solution Workspace Components](#solution-workspace-components) |
| Marketplace, downloader, wizard | [Desktop Helper Windows](#desktop-helper-windows) |

## Basic MVVM and Extension Contracts

| Component | Source | DLL | Purpose |
| --- | --- | --- | --- |
| `ViewModelBase` | `UI/ColorVision.Common/MVVM/` | `ColorVision.Common` | Base property-notification ViewModel |
| `RelayCommand` / `RelayCommand<T>` | `UI/ColorVision.Common/MVVM/` | `ColorVision.Common` | WPF command binding |
| `IInitializer` / `InitializerBase` | `UI/ColorVision.Common/Interfaces/` | `ColorVision.Common` | Startup initializer extension |
| `IMainWindowInitialized` | `UI/ColorVision.Common/Interfaces/Window/` | `ColorVision.Common` | Extension after main window initialization |
| `IConfig` | `UI/ColorVision.Common/Interfaces/Config/` | `ColorVision.Common` | Config object contract |
| `IWizardStep` | `UI/ColorVision.Common/Interfaces/` | `ColorVision.Common` | Wizard step contract |

If a new UI module only needs ViewModels, commands, or shared interfaces, keep it near `ColorVision.Common` instead of pulling in high-level packages.

## Menus, Status Bar, Hotkeys

| Capability | Entry | Source | Note |
| --- | --- | --- | --- |
| Menu contract | `IMenuItem`, `IMenuItemProvider`, `IRightMenuItemProvider` | `UI/ColorVision.Common/Interfaces/Menus/` | Bottom-level plugin/module menu contracts |
| Menu base | `MenuItemBase` | `UI/ColorVision.Common/Interfaces/Menus/MenuItemBase.cs` | Command, order, permission checks |
| Global menu base | `GlobalMenuBase` | `UI/ColorVision.UI/Menus/GlobalMenuBase.cs` | Common main-menu extension base |
| Menu manager | `MenuManager` | `UI/ColorVision.UI/Menus/MenuManager.cs` | Reflection, providers, dynamic menu refresh |
| File/Edit bases | `MenuItemFileBase`, `MenuItemEditBase` | `UI/ColorVision.UI/Menus/Base/` | File/Edit menu grouping |
| Status bar control | `StatusBarControl` | `UI/ColorVision.UI/StatusBar/StatusBarControl.xaml` | Host status bar UI |
| Status bar providers | `IStatusBarProvider`, `IStatusBarProviderUpdatable` | `UI/ColorVision.Common/Interfaces/StatusBar/` | Modules contribute status content |
| Global hotkeys | `IHotKey`, `HotKeys`, `GlobalHotKey/` | `UI/ColorVision.UI/HotKey/` | System/application hotkeys |
| Window hotkeys | `WindowHotKeyManager` | `UI/ColorVision.UI/HotKey/WindowHotKey/` | Scope-limited hotkeys |
| Hotkey settings | `HotKeysSetting`, `HoyKeyControl` | `UI/ColorVision.UI/HotKey/` | Shortcut settings page and editor |

Add menus through bases/providers. Add status-bar information through providers. Do not manipulate the host status bar control directly.

## PropertyGrid and Settings Editing

ColorVision uses metadata-driven property editing rather than hand-writing every settings form.

| Component | Source | Purpose |
| --- | --- | --- |
| `PropertyEditorWindow` | `UI/ColorVision.UI/PropertyEditor/PropertyEditorWindow.xaml` | Main property editor window |
| `PropertyTreeNode` | `UI/ColorVision.UI/PropertyEditor/` | Property tree and grouping model |
| `PropertyEditorTypeAttribute` | `ColorVision.Common` / `ColorVision.UI` usage path | Bind property to custom editor |
| `BoolPropertiesEditor` | `UI/ColorVision.UI/PropertyEditor/Editor/` | bool editor |
| `EnumPropertiesEditor` | same | enum editor |
| `TextboxPropertiesEditor` | same | text editor |
| `TextSelectFilePropertiesEditor` | same | file picker |
| `TextSelectFolderPropertiesEditor` | same | folder picker |
| `TextBaudRatePropertiesEditor` | same | baud-rate editor |
| `CronExpressionPropertiesEditor` | same | cron editor |
| `DictionaryJsonEditor` / `ListNumericJsonEditor` | same | JSON dictionary and numeric list |
| `ThemePropertiesEditor` | `UI/ColorVision.UI/Themes/ThemeConfig.cs` | theme config editor |
| `LanguagePropertiesEditor` | `UI/ColorVision.UI/Languages/LanguageConfig.cs` | language config editor |
| `TextJsonPropertiesEditor` | `UI/ColorVision.Solution/Editor/AvalonEditor/` | large JSON text editor |

New settings should use `[Category]`, `[DisplayName]`, and `[Description]`. Add a custom `IPropertyEditor` only when default editors are not enough.

## Themes and Common Controls

| Component | Source | DLL | Purpose |
| --- | --- | --- | --- |
| `ThemeManager` | `UI/ColorVision.Themes/` | `ColorVision.Themes` | Theme switching and resource injection |
| `ThemeManagerExtensions` | same | `ColorVision.Themes` | `Application.ApplyTheme`, caption application |
| `BaseWindow` | `UI/ColorVision.Themes/Themes/Window/BaseWindow.xaml` | `ColorVision.Themes` | Common base window |
| `LoadingOverlay` | `UI/ColorVision.Themes/Controls/LoadingOverlay.xaml` | `ColorVision.Themes` | Loading overlay |
| `ProgressRing` | `UI/ColorVision.Themes/Controls/ProgressRing.xaml` | `ColorVision.Themes` | Ring progress |
| `ToggleSwitch` | `UI/ColorVision.Themes/Controls/ToggleSwitch.cs` | `ColorVision.Themes` | Toggle control |
| `MessageBoxWindow` | `UI/ColorVision.Themes/Controls/MessageBoxWindow.xaml` | `ColorVision.Themes` | Themed message window |
| `UploadControl` / `UploadWindow` | `UI/ColorVision.Themes/Controls/Uploads/` | `ColorVision.Themes` | Upload prompt/window |

Theme controls should remain reusable. A control used by only one window should stay in that module.

## Runtime Tool Windows

| Capability | Window/control | Source | DLL |
| --- | --- | --- | --- |
| MySQL connection | `MySqlConnect` | `UI/ColorVision.Database/MySqlConnect.xaml` | `ColorVision.Database` |
| Database browsing | `DatabaseBrowserWindow` | `UI/ColorVision.Database/DatabaseBrowserWindow.xaml` | `ColorVision.Database` |
| Generic query | `GenericQueryWindow` | `UI/ColorVision.Database/GenericQueryWindow.xaml` | `ColorVision.Database` |
| Row editing | `DatabaseRowEditWindow` | `UI/ColorVision.Database/DatabaseRowEditWindow.xaml` | `ColorVision.Database` |
| Socket management | `SocketManagerWindow` | `UI/ColorVision.SocketProtocol/SocketManagerWindow.xaml` | `ColorVision.SocketProtocol` |
| Socket status bar | `SocketStatusBarProvider` | `UI/ColorVision.SocketProtocol/SocketStatusBarProvider.cs` | `ColorVision.SocketProtocol` |
| Task manager | `TaskViewerWindow` | `UI/ColorVision.Scheduler/TaskViewerWindow.xaml` | `ColorVision.Scheduler` |
| New task | `CreateTask` | `UI/ColorVision.Scheduler/CreateTask.xaml` | `ColorVision.Scheduler` |
| Execution history | `ExecutionHistoryWindow` | `UI/ColorVision.Scheduler/ExecutionHistoryWindow.xaml` | `ColorVision.Scheduler` |

If a runtime tool window does not open, first check whether its provider/menu was loaded, then check config and database files.

## Image Editor Components

| Capability | Entry | Source |
| --- | --- | --- |
| Main image control | `ImageView` | `UI/ColorVision.ImageEditor/ImageView.xaml` |
| Runtime context | `EditorContext` | `UI/ColorVision.ImageEditor/EditorContext.cs` |
| Drawing canvas | `DrawCanvas` | `UI/ColorVision.ImageEditor/DrawCanvas.cs` |
| Tool factory | `IEditorToolFactory` | `UI/ColorVision.ImageEditor/EditorToolFactory.cs` |
| Open extension | `IImageOpen` | `UI/ColorVision.ImageEditor/Abstractions/IImageEditor.cs` |
| Tool extension | `IEditorTool`, `IEditorToggleTool`, `IEditorCustomControlTool` | `UI/ColorVision.ImageEditor/Abstractions/IEditorTool.cs` |
| Context menu extension | `IDVContextMenu`, `IIEditorToolContextMenu` | `UI/ColorVision.ImageEditor/Abstractions/` |
| Primitives and overlay | `Draw/` | `UI/ColorVision.ImageEditor/Draw/` |
| Annotation import/export | `Draw/Annotations/` | `UI/ColorVision.ImageEditor/Draw/Annotations/` |
| Image settings | `ImageViewSettingsWindow`, `ImageViewContextSettingsView`, `ImageViewWorkspaceSettingsView` | `UI/ColorVision.ImageEditor/Settings/` |

Tool groups include file commands, zoom/view commands, image processing, pseudo-color, shader filters, histogram, graphic editing, 3D, and CIE diagrams. Add image tools through editor-tool interfaces and reuse `Draw/` primitives for algorithm overlays.

## Desktop Helper Windows

| Capability | Window/entry | Source |
| --- | --- | --- |
| Settings | `SettingWindow`, `SettingWindowController` | `UI/ColorVision.UI.Desktop/Settings/` |
| Wizard | `WizardWindow`, `WizardManager` | `UI/ColorVision.UI.Desktop/Wizards/` |
| Marketplace | `MarketplaceWindow`, `MarketplaceManager` | `UI/ColorVision.UI.Desktop/Marketplace/` |
| DLL version viewer | `ViewDllVersionsWindow` | `UI/ColorVision.UI.Desktop/Marketplace/` |
| Downloader | `DownloadWindow`, `AddDownloadDialog` | `UI/ColorVision.UI.Desktop/Download/` |
| Menu manager | `MenuItemManagerWindow` | `UI/ColorVision.UI.Desktop/MenuItemManager/` |
| Config manager | `ConfigManagerWindow` | `UI/ColorVision.UI.Desktop/ConfigManagerWindow.xaml` |
| Third-party apps | `ThirdPartyAppsWindow`, `AddCustomAppWindow` | `UI/ColorVision.UI.Desktop/ThirdPartyApps/` |
| Feedback | `FeedbackWindow` | `UI/ColorVision.UI.Desktop/Feedback/` |

## Solution Workspace Components

| Capability | Entry | Source |
| --- | --- | --- |
| Solution management | `SolutionManager` | `UI/ColorVision.Solution/SolutionManager.cs` |
| Recent files | `MenuRecentFile` | `UI/ColorVision.Solution/RecentFile/` |
| File tree | `SolutionExplorer`, `SolutionNode`, `TreeViewControl` | `UI/ColorVision.Solution/Explorer/` |
| New item/project | `AddNewItemWindow`, `AddNewProjectWindow` | `UI/ColorVision.Solution/Explorer/` |
| Editor registration | `IEditor`, `EditorManager`, `EditorForExtensionAttribute` | `UI/ColorVision.Solution/Editor/` |
| Text editing | `AvalonEditControll`, `AvalonEditWindow` | `UI/ColorVision.Solution/Editor/AvalonEditor/` |
| Hex editing | `HexEditorView` | `UI/ColorVision.Solution/Editor/` |
| Workspace layout | `WorkspaceManager`, `DockLayoutManager`, `LayoutMenuItems` | `UI/ColorVision.Solution/Workspace/` |
| Terminal | `TerminalControl` | `UI/ColorVision.Solution/Terminal/` |
| Multi-image view | `MultiImageViewer` | `UI/ColorVision.Solution/MultiImageViewer/` |
| Markdown preview | `MarkdownViewWindow` | `UI/ColorVision.Solution/MarkdownViewWindow.xaml` |
| Local RBAC | `RbacManagerWindow`, `UserManagerWindow`, `PermissionManagerWindow`, `LoginWindow` | `UI/ColorVision.Solution/Rbac/` |

Add editors by implementing `IEditor` and using `EditorForExtensionAttribute`, `GenericEditorAttribute`, or `FolderEditorAttribute`.

## Where to Put New UI Capability

| New capability | Suggested location | Docs to update |
| --- | --- | --- |
| ViewModel, command, shared interface | `ColorVision.Common` | [ColorVision.Common](./ColorVision.Common.md) |
| Theme control | `ColorVision.Themes/Controls/` | [ColorVision.Themes](./ColorVision.Themes.md), this page |
| Menu, hotkey, status bar, property editor | `ColorVision.UI/` | [ColorVision.UI](./ColorVision.UI.md), this page |
| Database maintenance window | `ColorVision.Database/` | [ColorVision.Database](./ColorVision.Database.md), this page |
| Socket manager or handler infrastructure | `ColorVision.SocketProtocol/` | [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md), this page |
| Quartz task window | `ColorVision.Scheduler/` | [ColorVision.Scheduler](./ColorVision.Scheduler.md), this page |
| Image tool, primitive, overlay | `ColorVision.ImageEditor/` | [ColorVision.ImageEditor](./ColorVision.ImageEditor.md), this page |
| Settings, wizard, marketplace, diagnostics | `ColorVision.UI.Desktop/` | [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md), this page |
| Workspace, editor, terminal, RBAC | `ColorVision.Solution/` | [ColorVision.Solution](./ColorVision.Solution.md), this page |

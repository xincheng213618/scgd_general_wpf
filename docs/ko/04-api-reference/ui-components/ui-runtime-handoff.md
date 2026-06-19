# UI 런타임 컴포넌트 인수인계

이 페이지는 `UI/` runtime path를 인수인계하는 사람을 위한 문서입니다. 앱 시작 후 menu, settings, plugin loading, PropertyGrid, ImageEditor, Socket, Scheduler, Marketplace, Solution workspace가 어떻게 discovery, assembly, debug되는지 설명합니다.

DLL/NuGet 릴리스 작업은 [UI DLL 릴리스 플레이북](./ui-dll-release-playbook.md)과 [UI DLL 릴리스 매트릭스](./release-matrix.md)를 먼저 봅니다. control 또는 runtime UI issue는 이 페이지에서 시작해 [UI 컴포넌트 카탈로그](./control-catalog.md)와 해당 DLL 페이지로 이동합니다.

## runtime boundaries

| Module | Runtime role | Do not put here |
| --- | --- | --- |
| `ColorVision.Common` | contracts, MVVM, commands, menu/status bar interfaces | concrete windows, customer business, Engine device logic |
| `ColorVision.Themes` | theme resources, base windows, shared controls | plugins, projects, algorithm business |
| `ColorVision.UI` | menu, plugin loading, config, settings discovery, PropertyGrid, log, hotkey | marketplace UI, Solution workspace, project workflow |
| `ColorVision.Core` | image native bridge, `HImage`, OpenCV helper | WPF interactive controls, customer judgment |
| `ColorVision.Database` | SqlSugar, MySQL/SQLite config, database browser | device protocol, project export format |
| `ColorVision.SocketProtocol` | local TCP server, JSON/Text dispatch, message history | concrete project workflow |
| `ColorVision.Scheduler` | Quartz tasks, task windows, execution history | long-running algorithm implementation |
| `ColorVision.ImageEditor` | `ImageView`, toolbar, draw primitives, overlay, CIE/3D | customer result judgment/export |
| `ColorVision.UI.Desktop` | settings, wizard, marketplace, downloader, diagnostics | main startup center, Engine flow business |
| `ColorVision.Solution` | `.cvsln`, explorer, editors, terminal, RBAC | device control, algorithm execution, project main path |

## discovery matrix

| Capability | Entry | First check |
| --- | --- | --- |
| plugin loading | `PluginLoader.LoadPlugins("Plugins")` | directory, manifest, `.deps.json`, dependency DLL, enabled |
| menu | `MenuManager.LoadMenuForWindow` | `OwnerGuid`, `GuidId`, `Order`, target window, permission |
| settings | `ConfigSettingManager.GetAllSettings` | `ConfigService`, `IConfig`, `[ConfigSetting]`, search filter |
| PropertyGrid | `PropertyEditorWindow` | public get/set, `PropertyEditorTypeAttribute`, clone/reset |
| status bar | `StatusBarManager` | provider discovery, main window binding |
| ImageEditor tools | `IEditorToolFactory` | construction, `GuidId`, visibility config |
| image openers | `IImageOpen` + `FileExtensionAttribute` | extension, constructor, file path |
| Socket events | `SocketManager` / `ISocketJsonHandler` | port, protocol mode, `EventName`, message history, project handler |
| Scheduler | `QuartzSchedulerManager` | `scheduler_tasks.json`, Job type, history DB |
| Solution editors | `EditorManager` | extension match, registration order, file lock/permission |

## common troubleshooting

| Symptom | First check | Then check |
| --- | --- | --- |
| plugin installed but no menu | whether `PluginLoader` loaded assembly | menu owner/filter/permission |
| setting is missing | `ConfigSettingManager` scan | provider or `[ConfigSetting]` |
| image opens but toolbar incomplete | tool factory discovery | visibility and `GuidId` |
| overlay coordinate wrong | draw primitive and zoom coordinates | Engine result handler conversion |
| Socket receives message but project does not run | Socket history | project `ISocketJsonHandler` and `EventName` |
| task does not execute | Quartz startup | task JSON and Job type |
| Solution file open does nothing | extension matching | AvalonDock/AvalonEdit/WebView2 dependencies |

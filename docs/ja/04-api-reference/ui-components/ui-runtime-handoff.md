# UI ランタイムコンポーネント引き継ぎ

このページは `UI/` の runtime path を引き継ぐ人向けです。アプリ起動後、menu、settings、plugin loading、PropertyGrid、ImageEditor、Socket、Scheduler、Marketplace、Solution workspace がどのように discovery、assembly、debug されるかを説明します。

DLL/NuGet のリリース作業は [UI DLL リリースプレイブック](./ui-dll-release-playbook.md) と [UI DLL リリースマトリクス](./release-matrix.md) を先に見ます。control や runtime UI issue は本ページから [UI コンポーネントカタログ](./control-catalog.md) と該当 DLL ページへ進みます。

## runtime boundaries

| Module | Runtime role | Do not put here |
| --- | --- | --- |
| `ColorVision.Common` | contracts、MVVM、commands、menu/status bar interfaces | concrete windows、customer business、Engine device logic |
| `ColorVision.Themes` | theme resources、base windows、shared controls | plugins、projects、algorithm business |
| `ColorVision.UI` | menu、plugin loading、config、settings discovery、PropertyGrid、log、hotkey | marketplace UI、Solution workspace、project workflow |
| `ColorVision.Core` | image native bridge、`HImage`、OpenCV helper | WPF interactive controls、customer judgment |
| `ColorVision.Database` | SqlSugar、MySQL/SQLite config、database browser | device protocol、project export format |
| `ColorVision.SocketProtocol` | local TCP server、JSON/Text dispatch、message history | concrete project workflow |
| `ColorVision.Scheduler` | Quartz tasks、task windows、execution history | long-running algorithm implementation |
| `ColorVision.ImageEditor` | `ImageView`、toolbar、draw primitives、overlay、CIE/3D | customer result judgment/export |
| `ColorVision.UI.Desktop` | settings、wizard、marketplace、downloader、diagnostics | main startup center、Engine flow business |
| `ColorVision.Solution` | `.cvsln`、explorer、editors、terminal、RBAC | device control、algorithm execution、project main path |

## discovery matrix

| Capability | Entry | First check |
| --- | --- | --- |
| plugin loading | `PluginLoader.LoadPlugins("Plugins")` | directory、manifest、`.deps.json`、dependency DLL、enabled |
| menu | `MenuManager.LoadMenuForWindow` | `OwnerGuid`、`GuidId`、`Order`、target window、permission |
| settings | `ConfigSettingManager.GetAllSettings` | `ConfigService`、`IConfig`、`[ConfigSetting]`、search filter |
| PropertyGrid | `PropertyEditorWindow` | public get/set、`PropertyEditorTypeAttribute`、clone/reset |
| status bar | `StatusBarManager` | provider discovery、main window binding |
| ImageEditor tools | `IEditorToolFactory` | construction、`GuidId`、visibility config |
| image openers | `IImageOpen` + `FileExtensionAttribute` | extension、constructor、file path |
| Socket events | `SocketManager` / `ISocketJsonHandler` | port、protocol mode、`EventName`、message history、project handler |
| Scheduler | `QuartzSchedulerManager` | `scheduler_tasks.json`、Job type、history DB |
| Solution editors | `EditorManager` | extension match、registration order、file lock/permission |

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

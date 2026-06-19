# UI DLL リリースマトリクス

このページは UI DLL のリリース、現場 DLL 交換、missing dependency 調査を担当する保守者向けです。UI 操作ではなく、`UI/` のリリース単位、dependency、package resource、acceptance をまとめます。

## リリース単位

| Unit | Project file | Target | Version | Output | Dependency focus |
| --- | --- | --- | --- | --- | --- |
| `ColorVision.Common` | `UI/ColorVision.Common/ColorVision.Common.csproj` | net8/net10 Windows | `1.5.5.2` | DLL + packages | WPF、WinForms、shared interfaces |
| `ColorVision.Themes` | `UI/ColorVision.Themes/ColorVision.Themes.csproj` | net8/net10 Windows | `1.5.5.3` | DLL + packages | `HandyControl`、theme resources |
| `ColorVision.UI` | `UI/ColorVision.UI/ColorVision.UI.csproj` | net8/net10 Windows | `1.5.5.3` | DLL + packages | `Common`、`Themes`、log4net、Newtonsoft.Json |
| `ColorVision.Core` | `UI/ColorVision.Core/ColorVision.Core.csproj` | net8/net10 Windows | `1.5.5.2` | DLL + packages + native runtime | `opencv_helper.dll`、OpenCV runtime |
| `ColorVision.Database` | `UI/ColorVision.Database/ColorVision.Database.csproj` | net8/net10 Windows | `1.5.5.3` | DLL + packages | `ColorVision.UI`、SqlSugarCore |
| `ColorVision.SocketProtocol` | `UI/ColorVision.SocketProtocol/ColorVision.SocketProtocol.csproj` | net8/net10 Windows | `1.5.5.2` | DLL + packages | `ColorVision.UI`、`ColorVision.Database` |
| `ColorVision.Scheduler` | `UI/ColorVision.Scheduler/ColorVision.Scheduler.csproj` | net8/net10 Windows | `1.5.5.2` | DLL + packages | `ColorVision.UI`、Quartz、SqlSugarCore |
| `ColorVision.ImageEditor` | `UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj` | net10 Windows | `1.5.5.5` | DLL + packages + resources | `Core`、`UI`、OpenCvSharp、HelixToolkit、ScottPlot |
| `ColorVision.UI.Desktop` | `UI/ColorVision.UI.Desktop/ColorVision.UI.Desktop.csproj` | net10 Windows | `1.5.5.3` | `WinExe` + packages | `Database`、`UI`、WebView2、Markdig |
| `ColorVision.Solution` | `UI/ColorVision.Solution/ColorVision.Solution.csproj` | net10 Windows | `1.5.5.2` | DLL + packages | `ImageEditor`、`UI.Desktop`、AvalonDock、AvalonEdit、WebView2 |

## package resource checks

| Unit | Must check | Symptom if missing |
| --- | --- | --- |
| `Common` | README、cursor resources | basic tool cursor or package docs missing |
| `Themes` | icons、`uploadbg.avif`、theme XAML | icon/background/theme load failure |
| `UI` | plugin/config/property-editor types | menu、settings、property editor failures |
| `Core` | `runtimes/win-x64/native/opencv_helper.dll` and OpenCV DLLs | `DllNotFoundException`、image/video failure |
| `Database` | README、SqlSugar dependencies | database browser or DAO errors |
| `SocketProtocol` | README、Socket config、message entities | Socket manager/history/dispatch errors |
| `Scheduler` | README、Quartz/SqlSugar | task manager or history DB errors |
| `ImageEditor` | shaders、colormap、CIE CSV、icons、OpenCvSharp runtime | pseudo-color、CIE、3D、image open failure |
| `UI.Desktop` | `github-markdown.css`、`aria2c.exe` | marketplace preview or downloader failure |
| `Solution` | AvalonDock/AvalonEdit/WebView2/WPFHexaEditor | workspace/editor/terminal/RBAC failure |

## post-release smoke

| Capability | Verify |
| --- | --- |
| host startup | Release output starts |
| plugin loading | manifest、README、CHANGELOG are readable |
| settings and PropertyGrid | settings save; config object opens editor |
| ImageEditor | image、pseudo-color、CIE、annotation、3D |
| database | MySQL/SQLite providers list tables |
| Socket | service starts; JSON/Text message history works |
| Scheduler | task list and history read |
| Solution | `.cvsln`、explorer、text editor、terminal、layout restore |

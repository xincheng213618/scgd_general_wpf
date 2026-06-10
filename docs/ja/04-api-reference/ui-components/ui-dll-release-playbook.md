# UI DLL リリースプレイブック

このページは UI DLL の公開、現場 DLL 交換、dependency missing の調査、Engine/plugin/project package 向け UI packages の準備に使います。source folder ではなく release scenario で整理しています。

## リリース範囲判断

| Task | Minimum scope | Also validate |
| --- | --- | --- |
| README または help text のみ | matching UI project package | README in package root |
| `Common` interface/helper | `Common` and upper UI packages | host、plugins、PropertyGrid、ImageEditor |
| `Themes` style/base window | `Themes`、`UI`、theme users | main/settings/plugin/project windows |
| `UI` menu/config/plugin loader/PropertyGrid | `UI` and plugin/project consumers | plugin manager、settings、property editor、status bar |
| `Core` native bridge | `Core`、`ImageEditor`、Engine image paths | native DLL、image open、OpenCV |
| `ImageEditor` overlay/pseudo-color/CIE/3D | `ImageEditor` and usually `Core` | resources、normal image、result image、CIE/3D |
| `Database`/`SocketProtocol`/`Scheduler` | matching package and UI entry | management windows、config、history DB |
| `UI.Desktop`/`Solution` | desktop tool or workspace layer | marketplace、downloader、WebView2、`.cvsln`、terminal |
| field plugin/project replacement | host `ColorVision.*.dll` set | `.deps.json`、manifest、host DLL versions |

## build and artifact checks

```powershell
rg -n "VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile|PackagePath|CopyToOutputDirectory|TargetFrameworks|OutputType" UI -g "*.csproj"
dotnet restore
dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64
```

単一 package failure を調べる場合は、`Common`、`Themes`、`UI`、`Core`、`ImageEditor`、`Database`、`SocketProtocol`、`Scheduler`、`UI.Desktop`、`Solution` の順に下層から構築します。

## acceptance by scenario

| Scenario | Acceptance |
| --- | --- |
| base UI packages | host starts; menu、settings、PropertyGrid、plugin manager work |
| Core/ImageEditor | `.nupkg` has native runtime、shaders、colormaps、CIE; image/pseudo-color/overlay/3D work |
| Database/Socket/Scheduler | database browser、Socket service/history、task list/history work |
| UI.Desktop/Solution | settings、marketplace、downloader、DLL version window、workspace、terminal、WebView2 work |
| external NuGet delivery | lock `UIProjectPackageVersion`; restore/build resolves expected packages |

## release handoff record

Record release date, scope, versions, build commands, package spot checks, runtime acceptance, external delivery method, rollback package location, and known limits.

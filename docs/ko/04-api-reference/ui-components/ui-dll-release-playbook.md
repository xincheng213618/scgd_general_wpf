# UI DLL 릴리스 플레이북

이 페이지는 UI DLL 공개, 현장 DLL 교체, dependency missing 조사, Engine/plugin/project package용 UI packages 준비에 사용합니다. source folder가 아니라 release scenario 기준으로 정리합니다.

## 릴리스 범위 판단

| Task | Minimum scope | Also validate |
| --- | --- | --- |
| README 또는 help text만 변경 | matching UI project package | README in package root |
| `Common` interface/helper | `Common` and upper UI packages | host, plugins, PropertyGrid, ImageEditor |
| `Themes` style/base window | `Themes`, `UI`, theme users | main/settings/plugin/project windows |
| `UI` menu/config/plugin loader/PropertyGrid | `UI` and plugin/project consumers | plugin manager, settings, property editor, status bar |
| `Core` native bridge | `Core`, `ImageEditor`, Engine image paths | native DLL, image open, OpenCV |
| `ImageEditor` overlay/pseudo-color/CIE/3D | `ImageEditor` and usually `Core` | resources, normal image, result image, CIE/3D |
| `Database`/`SocketProtocol`/`Scheduler` | matching package and UI entry | management windows, config, history DB |
| `UI.Desktop`/`Solution` | desktop tool or workspace layer | marketplace, downloader, WebView2, `.cvsln`, terminal |
| field plugin/project replacement | host `ColorVision.*.dll` set | `.deps.json`, manifest, host DLL versions |

## build and artifact checks

```powershell
rg -n "VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile|PackagePath|CopyToOutputDirectory|TargetFrameworks|OutputType" UI -g "*.csproj"
dotnet restore
dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64
```

단일 package failure를 조사할 때는 `Common`, `Themes`, `UI`, `Core`, `ImageEditor`, `Database`, `SocketProtocol`, `Scheduler`, `UI.Desktop`, `Solution` 순서로 하위 레이어부터 빌드합니다.

## scenario별 acceptance

| Scenario | Acceptance |
| --- | --- |
| base UI packages | host starts; menu, settings, PropertyGrid, plugin manager work |
| Core/ImageEditor | `.nupkg` has native runtime, shaders, colormaps, CIE; image/pseudo-color/overlay/3D work |
| Database/Socket/Scheduler | database browser, Socket service/history, task list/history work |
| UI.Desktop/Solution | settings, marketplace, downloader, DLL version window, workspace, terminal, WebView2 work |
| external NuGet delivery | lock `UIProjectPackageVersion`; restore/build resolves expected packages |

## release handoff record

릴리스 날짜, 범위, versions, build commands, package spot checks, runtime acceptance, external delivery method, rollback package location, known limits를 기록합니다.

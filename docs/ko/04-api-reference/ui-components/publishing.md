# UI DLL 릴리스 절차

이 페이지는 `UI/` 아래 DLL/NuGet 패키지의 릴리스 방법을 설명합니다. UI library 유지보수, Engine 의존 패키지 제공, plugin missing DLL 조사에 사용합니다.

각 DLL의 version, TFM, dependency, package resource, smoke test를 빠르게 확인할 때는 [UI DLL 릴리스 매트릭스](./release-matrix.md)를 봅니다. 현장 DLL 교체, plugin dependency, Engine package fallback 같은 작업은 [UI DLL 릴리스 플레이북](./ui-dll-release-playbook.md)에서 범위를 결정합니다.

## 대상

- base packages: `ColorVision.Common`, `ColorVision.Themes`, `ColorVision.UI`
- data and communication: `ColorVision.Database`, `ColorVision.SocketProtocol`, `ColorVision.Scheduler`
- image packages: `ColorVision.Core`, `ColorVision.ImageEditor`
- desktop shell packages: `ColorVision.UI.Desktop`, `ColorVision.Solution`

`ColorVision.UI.Desktop`은 `WinExe`이지만 package를 생성합니다. 주 앱 진입점은 root의 `ColorVision/`입니다.

## build

```powershell
dotnet restore
dotnet build UI/ColorVision.Common/ColorVision.Common.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Themes/ColorVision.Themes.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.UI/ColorVision.UI.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Core/ColorVision.Core.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Database/ColorVision.Database.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.SocketProtocol/ColorVision.SocketProtocol.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Scheduler/ColorVision.Scheduler.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.UI.Desktop/ColorVision.UI.Desktop.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Solution/ColorVision.Solution.csproj -c Release -p:Platform=x64
```

대부분의 UI project는 `GeneratePackageOnBuild=True`이므로 Release build에서 `.nupkg`와 `.snupkg`도 생성합니다.

## release 전 체크

```powershell
rg -n "VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile|PackagePath|CopyToOutputDirectory|TargetFrameworks|OutputType" UI -g "*.csproj"
Get-Content Directory.Build.props
```

| 항목 | 확인 |
| --- | --- |
| version | 각 `.csproj`의 `VersionPrefix` |
| strong name | `ColorVision.snk`가 있으면 signing을 끄지 않음 |
| target framework | host, Engine, plugins, projects와 호환 |
| README | package root에 포함 |
| native runtime | `ColorVision.Core`의 OpenCV DLL |
| resources | ImageEditor shader/colormap/CIE, Themes icons, UI.Desktop `aria2c.exe` |

## release 후 검증

- `dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64`
- output directory의 `ColorVision.*.dll` 확인.
- host를 시작하고 settings, PropertyGrid, ImageEditor, database, Socket, Scheduler, plugin manager 확인.
- `Core` 또는 `ImageEditor` 포함 시 normal image, pseudo-color, CIE, annotation import/export, 3D, native DLL 추가 확인.
- Engine package-only delivery에서는 `UIProjectPackageVersion`을 명시적으로 고정합니다.

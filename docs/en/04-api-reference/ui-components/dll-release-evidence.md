# UI DLL Release Evidence Checklist

This page is for people who publish UI DLLs or replace them in the field. It records the evidence needed to prove that a UI release is complete, version-aligned, and consumable by Engine, plugins, and project packages.

## Evidence To Keep

| Evidence | Keep | Why |
| --- | --- | --- |
| Project configuration | `TargetFrameworks`, `VersionPrefix`, `GeneratePackageOnBuild`, `PackageReadmeFile`, packed resources | Proves the release matches current `.csproj` files |
| Build logs | `dotnet restore`, UI project builds, main app/Engine builds | Proves DLLs, `.nupkg`, and `.snupkg` came from one build round |
| Package contents | Expanded `.nupkg` listing | Proves README, native runtime, shaders, CIE data, CSS, and tools are present |
| Output directory | `ColorVision.*.dll` versions and timestamps in the host output | Proves runtime loads the intended DLL set |
| Consumers | Engine, key plugins, key project packages | Proves the release works outside the UI project itself |
| Rollback | Previous DLLs and packages | Allows fast recovery |

## Configuration Checks

```powershell
rg -n "TargetFrameworks|VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile|PackagePath|CopyToOutputDirectory|OutputType" UI -g "*.csproj"
Get-Content Directory.Build.props
```

Check target frameworks, package generation, README packing, resource packing, and `OutputType`. `ColorVision.UI.Desktop` is a `WinExe`, but it still participates in package and dependency release checks.

## DLL Evidence Matrix

| Unit | `.csproj` evidence | Package/output evidence |
| --- | --- | --- |
| `ColorVision.Common` | `net8.0-windows7.0;net10.0-windows7.0`, `VersionPrefix=1.5.5.2`, cursor resource | README in package root, cursor resource loads |
| `ColorVision.Themes` | `VersionPrefix=1.5.5.3`, `HandyControl`, icon resources, `uploadbg.avif` | theme XAML, icons, upload background |
| `ColorVision.UI` | `VersionPrefix=1.5.5.3`, references `Common`/`Themes` | plugin loader, menus, config, PropertyGrid from same DLL set |
| `ColorVision.Core` | `opencv_helper.dll`, OpenCV `4130` DLLs, optional `opencv_cuda.dll` under `runtimes/win-x64/native` | native runtime exists in package and host output |
| `ColorVision.Database` | `SqlSugarCore`, `VersionPrefix=1.5.5.3` | MySQL/SQLite providers and browser open |
| `ColorVision.SocketProtocol` | references `Database` and `UI`, `VersionPrefix=1.5.5.2` | socket window, port config, message history |
| `ColorVision.Scheduler` | `Quartz`, `SqlSugarCore`, README packing | task JSON, `SchedulerHistory.db`, task window |
| `ColorVision.ImageEditor` | OpenCvSharp, shaders, colormaps, CIE CSV | image open, pseudo color, CIE, 3D, overlay, annotations |
| `ColorVision.UI.Desktop` | `OutputType=WinExe`, WebView2, CSS, `aria2c.exe` | marketplace README preview, downloader, DLL version window |
| `ColorVision.Solution` | AvalonDock, AvalonEdit, WebView2, WPFHexaEditor | `.cvsln`, explorer, editors, terminal, RBAC windows |

## Build Evidence

Save the commands actually executed. For single-package failures, build from lower-level dependencies upward:

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
dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64
```

If Engine consumes packages without UI source, also record `UIProjectPackageVersion`, restore, and build for `Engine/ColorVision.Engine`.

## Package Spot Checks

```powershell
$out = "ColorVision/bin/x64/Release/net10.0-windows"
Get-ChildItem $out -Filter "ColorVision*.dll" |
  Select-Object Name, LastWriteTime, @{Name="FileVersion";Expression={$_.VersionInfo.FileVersion}}
Get-ChildItem $out -Recurse -Filter "opencv*.dll" |
  Select-Object FullName, LastWriteTime, Length
```

For plugin issues, compare plugin `.deps.json` requirements with the actual DLLs in the host root. Replacing only the plugin folder is often not enough.

## Handoff Template

```text
Release date:
Owner:
Scope:
UI project versions:
Build commands:
Generated nupkg/snupkg:
Host output directory:
Package content checks:
Native runtime checks:
Resource checks:
Engine/plugin/project validation:
Smoke tests:
Known limits:
Rollback location:
```


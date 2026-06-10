# UI DLL Release Playbook

This page is for maintainers who publish UI DLLs, replace field DLLs, troubleshoot missing dependencies, or prepare UI packages for Engine, plugins, or project packages. It is organized by release scenarios instead of source folders.

For DLL responsibilities, read [UI DLL Component Handbook](./component-handbook.md). For versions, package resources, and smoke-test coverage, use this page together with [UI DLL Release Matrix](./release-matrix.md).

## How To Use This Page

1. Use [Release Scope Decisions](#release-scope-decisions) to decide whether this is a single-DLL release, a low-level UI release, an image package release, a desktop-shell release, or a release that must also validate Engine, plugins, and project packages.
2. Follow [Build And Artifact Checks](#build-and-artifact-checks) to produce DLLs, `.nupkg`, and `.snupkg` artifacts.
3. Use [Scenario Handling](#scenario-handling) for resource checks and runtime acceptance.
4. Fill in [Release Handoff Record](#release-handoff-record) before handing the package over.

## Release Scope Decisions

| Task | Minimum release scope | Must also validate |
| --- | --- | --- |
| Only README, help text, or documentation resources changed | The matching UI project package | README is packed at package root |
| `ColorVision.Common` interface, command, permission, or helper changed | `Common` plus upper UI packages that reference it | Host startup, plugin loading, PropertyGrid, ImageEditor |
| `ColorVision.Themes` theme, base window, or control style changed | `Themes`, `UI`, and windows using themes | Main window, settings, plugin windows, project windows |
| `ColorVision.UI` menu, config, plugin loader, PropertyGrid, or status bar changed | `UI` plus plugin/project-package consumers | Plugin manager, settings, property editor, status bar |
| `ColorVision.Core` `HImage`, OpenCV helper, or native bridge changed | `Core`, `ImageEditor`, and Engine image paths | Native DLLs, image open, video/pseudo-color/OpenCV calls |
| `ColorVision.ImageEditor` drawing, overlay, pseudo-color, CIE, 3D changed | `ImageEditor`, usually with `Core` | Image open, result overlay, CIE, 3D, resources |
| `Database`, `SocketProtocol`, or `Scheduler` changed | The matching package and UI entry | Database browser, Socket manager, task manager |
| `UI.Desktop` or `Solution` changed | Desktop tool layer and workspace layer | Marketplace, downloader, WebView2, `.cvsln`, terminal |
| Replacing a plugin or project package in the field | Host `ColorVision.*.dll` set plus plugin/project folder | Plugin `.deps.json`, manifest, host DLL versions |
| Delivering NuGet packages without UI source | All UI packages consumed by Engine/package fallback | `UIProjectPackageVersion`, restore/build, locked package versions |

## Build And Artifact Checks

First confirm release settings from actual project files:

```powershell
rg -n "VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile|PackagePath|CopyToOutputDirectory|TargetFrameworks|OutputType" UI -g "*.csproj"
Get-Content Directory.Build.props
```

These fields control DLL version, target framework, package generation, README packing, and whether resources enter the output directory or NuGet package.

Recommended build order:

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

Common artifact locations:

```text
UI/<Project>/bin/Release/ColorVision.<Name>.<Version>.nupkg
UI/<Project>/bin/Release/ColorVision.<Name>.<Version>.snupkg
UI/<Project>/bin/x64/Release/<TFM>/ColorVision.<Name>.dll
ColorVision/bin/x64/Release/net10.0-windows/
```

Use the build log and actual directories as the authority. Do not rely on an old package name.

## Scenario Handling

### Scenario A: Publish Base UI DLLs

Applies to `ColorVision.Common`, `ColorVision.Themes`, and `ColorVision.UI`.

Steps:

1. Confirm `VersionPrefix`.
2. Build the changed project and upper-layer dependent projects.
3. Build the host and confirm `ColorVision.Common.dll`, `ColorVision.Themes.dll`, and `ColorVision.UI.dll` in the host output belong to the same release batch.
4. Start the host and open settings, menus, PropertyGrid, and plugin manager.

Acceptance focus:

| Change area | Acceptance |
| --- | --- |
| `Common` interface/helper | No `MissingMethodException` or type-load failure in plugins/project packages |
| `Themes` resources | Main, plugin, and project windows load themes, icons, and background resources |
| `UI` plugin loader | Plugins read manifest, README, CHANGELOG; status bar and menus work |

### Scenario B: Publish Core Or ImageEditor

Applies to OpenCV, `HImage`, image display, result overlay, pseudo-color, CIE, 3D, and video paths.

Steps:

1. Build `ColorVision.Core` first, then `ColorVision.ImageEditor`.
2. Inspect `ColorVision.Core.*.nupkg` for `runtimes/win-x64/native`.
3. Inspect `ColorVision.ImageEditor.*.nupkg` for shaders, colormaps, CIE CSV, and image resources.
4. Build the host, Engine, and key plugins/project packages that reference ImageEditor.
5. Open a normal image, switch pseudo-color, open CIE or 3D windows, and confirm result overlays display.

Core native package check:

```powershell
$pkg = Get-ChildItem UI/ColorVision.Core/bin -Recurse -Filter "ColorVision.Core.*.nupkg" |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
$tmp = Join-Path $env:TEMP "cv-core-nupkg"
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item $tmp -ItemType Directory | Out-Null
Copy-Item $pkg.FullName "$tmp/core.zip"
Expand-Archive "$tmp/core.zip" "$tmp/core"
Get-ChildItem "$tmp/core/runtimes/win-x64/native"
```

ImageEditor resource check:

```powershell
$pkg = Get-ChildItem UI/ColorVision.ImageEditor/bin -Recurse -Filter "ColorVision.ImageEditor.*.nupkg" |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
$tmp = Join-Path $env:TEMP "cv-imageeditor-nupkg"
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item $tmp -ItemType Directory | Out-Null
Copy-Item $pkg.FullName "$tmp/imageeditor.zip"
Expand-Archive "$tmp/imageeditor.zip" "$tmp/imageeditor"
Get-ChildItem "$tmp/imageeditor" -Recurse |
  Where-Object { $_.Name -match "colorscale_|CIE_cc|\.ps$|CIE1931" }
```

### Scenario C: Publish Database, SocketProtocol, Or Scheduler

Applies to the database browser, Socket manager, message history, and Quartz tasks.

Steps:

1. Build the matching UI package and host.
2. Open the database browser and confirm MySQL/SQLite providers list databases and tables.
3. Open the Socket manager and confirm port config, JSON/Text mode, and message history.
4. Open the task manager and confirm task JSON and `SchedulerHistory.db` can be read.

First failure checks:

| Symptom | Check first |
| --- | --- |
| Database window is empty | `ColorVision.Database`, SqlSugar dependency, connection config |
| Socket service does not start | Port conflict, `SocketConfig`, protocol mode |
| Task list does not load | `scheduler_tasks.json`, Quartz job scanning, history DB path |

### Scenario D: Publish UI.Desktop Or Solution

Applies to settings, wizard, marketplace, DLL version window, downloader, workspace, text editor, terminal, and RBAC.

Steps:

1. Build `ColorVision.UI.Desktop`, `ColorVision.Solution`, and the host.
2. Check the UI.Desktop package for `README.md`, `Assets/css/github-markdown.css`, and `Assets/Tool/aria2c.exe`.
3. Open marketplace and DLL version window; confirm README preview, version list, and downloader path.
4. Open or create `.cvsln`; confirm file tree, text editor, image editor, terminal, and layout restore.

### Scenario E: Field Replacement Reports Missing DLL Or Plugin Does Not Load

Steps:

1. Determine whether the missing file is a managed `ColorVision.*.dll` or a native DLL.
2. Compare plugin `.deps.json` `ColorVision.*` dependency versions with actual host-root DLL versions.
3. Do not replace only the plugin folder; plugins often depend on the host-root UI DLL set.
4. If OpenCV/native DLLs are missing, return to [Scenario B](#scenario-b-publish-core-or-imageeditor) and inspect `Core` package plus output directory.

Version check example:

```powershell
$out = "ColorVision/bin/x64/Release/net10.0-windows"
Get-ChildItem $out -Filter "ColorVision*.dll" |
  Select-Object Name, LastWriteTime, @{Name="FileVersion";Expression={$_.VersionInfo.FileVersion}}
```

### Scenario F: Deliver UI NuGet Packages For Engine Or External Environments

`Engine/ColorVision.Engine/ColorVision.Engine.csproj` uses `ProjectReference` when UI source exists. When source projects are unavailable, it falls back to `PackageReference Include="ColorVision.*"`. That means a source build alone does not prove a package-only delivery works.

Steps:

1. Inspect Engine fallback references:

```powershell
rg -n 'UIProjectPackageVersion|PackageReference Include="ColorVision' Engine/ColorVision.Engine/ColorVision.Engine.csproj
```

2. Lock explicit versions in external package environments instead of relying on `*`.
3. Run `restore` and `build` in a package-only environment.
4. Build the host, Engine, key plugins, and key project packages with the same UI DLL set.

## Release-Unit Spot Checks

| Release unit | Evidence in project file | Must inspect |
| --- | --- | --- |
| `ColorVision.Common` | `VersionPrefix`, `PackageReadmeFile=README.md`, cursor resource | README, `Assets/Cursor/eraser.cur`, shared interface compatibility |
| `ColorVision.Themes` | `HandyControl`, theme icons, `uploadbg.avif` resource | Theme resources, icons, upload background |
| `ColorVision.UI` | Plugin loader, menu, config, PropertyGrid package | Plugin loading, settings, property editor, status bar |
| `ColorVision.Core` | `opencv_helper.dll`, OpenCV runtime `Content Pack=true` | Complete `runtimes/win-x64/native` |
| `ColorVision.Database` | `SqlSugarCore`, `ColorVision.UI` reference | MySQL/SQLite providers and database browser |
| `ColorVision.SocketProtocol` | `ColorVision.Database`, `ColorVision.UI` references | Socket manager, message history, JSON/Text dispatch |
| `ColorVision.Scheduler` | `Quartz`, `SqlSugarCore`, packed `Properties/README.md` | Task config, execution history, README at package root |
| `ColorVision.ImageEditor` | shaders, colormaps, CIE CSV, OpenCvSharp runtime | Image open, pseudo-color, CIE, 3D, overlay |
| `ColorVision.UI.Desktop` | `OutputType=WinExe`, WebView2, `aria2c.exe` | Marketplace, DLL version window, downloader |
| `ColorVision.Solution` | AvalonDock, AvalonEdit, WebView2, WPFHexaEditor | `.cvsln`, editors, terminal, RBAC |

## Release Handoff Record

Every UI DLL release should record:

| Item | What to write |
| --- | --- |
| Date | For example `2026-06-10` |
| Release scope | Which UI projects, and whether `Core` or `ImageEditor` is included |
| Versions | Each `.csproj` `VersionPrefix` and host output `FileVersion` |
| Build commands | Which `dotnet build` commands ran, and whether host/Engine/plugins/projects were built |
| Package spot checks | README, native runtime, shaders, colormaps, CIE, CSS, `aria2c.exe` |
| Runtime acceptance | Host, PropertyGrid, ImageEditor, database, Socket, Scheduler, Solution, plugin loading |
| External delivery | Whether `UIProjectPackageVersion` is locked, package feed location, verification method |
| Rollback | Previous DLL, `.nupkg`, `.snupkg`, and plugin-folder backup location |
| Known limits | Untested modules, field environment differences, admin permission, runtime dependencies |

## Continue Reading

- [UI DLL Publishing](./publishing.md)
- [UI DLL Release Matrix](./release-matrix.md)
- [UI DLL Component Handbook](./component-handbook.md)
- [UI Control Catalog](./control-catalog.md)
- [Plugin Capability Matrix](../plugins/plugin-capability-matrix.md)
- [Project Capability & Handoff Matrix](../projects/project-capability-matrix.md)

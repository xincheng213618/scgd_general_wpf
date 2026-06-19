# Current UI DLL Documentation Coverage

This page is the module-level ledger for `UI/`. Use it first to confirm which UI projects currently exist, where their documentation lives, and which release evidence must be checked before replacing or publishing DLLs.

Last updated: 2026-06-10.

## Current Status

- `UI/` currently contains 10 real project directories, and each directory has a `.csproj` plus `README.md`.
- All 10 projects have matching documentation pages under `docs/04-api-reference/ui-components/`.
- All 10 projects enable `GeneratePackageOnBuild`, so release verification must inspect NuGet package contents as well as host output folders.
- `ColorVision.Common`, `ColorVision.Themes`, `ColorVision.UI`, `ColorVision.Core`, `ColorVision.Database`, `ColorVision.SocketProtocol`, and `ColorVision.Scheduler` target both `net8.0-windows7.0` and `net10.0-windows7.0`.
- `ColorVision.ImageEditor`, `ColorVision.UI.Desktop`, and `ColorVision.Solution` currently target `net10.0-windows7.0`.
- `ColorVision.Core` carries the highest native/runtime risk, `ColorVision.ImageEditor` carries the highest image interaction and result overlay risk, and `ColorVision.UI.Desktop` carries the highest desktop tooling and field-operation risk.

## Coverage Table

| UI project | Project file | Source README | Documentation | Release shape | Handoff focus |
| --- | --- | --- | --- | --- | --- |
| `UI/ColorVision.Common/` | `ColorVision.Common.csproj` | Yes | [ColorVision.Common](./ColorVision.Common.md) | DLL + NuGet | MVVM base, plugin interfaces, shared contracts, status bar metadata |
| `UI/ColorVision.Themes/` | `ColorVision.Themes.csproj` | Yes | [ColorVision.Themes](./ColorVision.Themes.md) | DLL + NuGet | Theme dictionaries, window styles, light/dark switching |
| `UI/ColorVision.UI/` | `ColorVision.UI.csproj` | Yes | [ColorVision.UI](./ColorVision.UI.md) | DLL + NuGet | Configuration, menus, plugin loading, PropertyGrid, hotkeys, localization |
| `UI/ColorVision.Core/` | `ColorVision.Core.csproj` | Yes | [ColorVision.Core](./ColorVision.Core.md) | DLL + NuGet + native runtime | `HImage`, OpenCV helper, image/video interop, `runtimes/win-x64/native` |
| `UI/ColorVision.Database/` | `ColorVision.Database.csproj` | Yes | [ColorVision.Database](./ColorVision.Database.md) | DLL + NuGet | SqlSugar DAO, database browser, MySQL/SQLite access |
| `UI/ColorVision.SocketProtocol/` | `ColorVision.SocketProtocol.csproj` | Yes | [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md) | DLL + NuGet | Local TCP service, JSON/Text dispatch, message history |
| `UI/ColorVision.Scheduler/` | `ColorVision.Scheduler.csproj` | Yes | [ColorVision.Scheduler](./ColorVision.Scheduler.md) | DLL + NuGet | Quartz scheduling, task recovery, task history, management window |
| `UI/ColorVision.ImageEditor/` | `ColorVision.ImageEditor.csproj` | Yes | [ColorVision.ImageEditor](./ColorVision.ImageEditor.md) | DLL + NuGet + image resources | `ImageView`, `DrawCanvas`, toolbar, result overlays, 3D/CIE views |
| `UI/ColorVision.UI.Desktop/` | `ColorVision.UI.Desktop.csproj` | Yes | [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md) | WinExe + NuGet | Settings window, wizard, marketplace, download tool, DLL version viewer |
| `UI/ColorVision.Solution/` | `ColorVision.Solution.csproj` | Yes | [ColorVision.Solution](./ColorVision.Solution.md) | DLL + NuGet | Workspace, editors, terminal, multi-image viewer, local RBAC, project management |

## Release Boundaries

| Boundary | Modules | Read first | Field risk |
| --- | --- | --- | --- |
| Shared foundation | `ColorVision.Common`, `ColorVision.Themes`, `ColorVision.UI` | [UI DLL Component Handbook](./component-handbook.md), [UI DLL Release Matrix](./release-matrix.md) | Menu, configuration, plugin entry, and theme failures affect many upper windows |
| Image and native layer | `ColorVision.Core`, `ColorVision.ImageEditor` | [UI DLL Release Evidence Checklist](./dll-release-evidence.md), [UI Runtime Component Handoff](./ui-runtime-handoff.md) | Missing native DLLs, image resources, or overlay registration directly affect result viewing |
| Data and service windows | `ColorVision.Database`, `ColorVision.SocketProtocol`, `ColorVision.Scheduler` | [UI DLL Release Playbook](./ui-dll-release-playbook.md), [UI Control Catalog](./control-catalog.md) | Data source, socket listener, scheduler history, and background task diagnosis depend on these windows |
| Desktop tools and workspace | `ColorVision.UI.Desktop`, `ColorVision.Solution` | [UI Runtime Component Handoff](./ui-runtime-handoff.md), each module page | Marketplace, download tools, Solution workspace, RBAC, and local project management live here |

## Evidence Used

This audit uses the following evidence:

- `Get-ChildItem UI -Directory`: confirms the real UI project directories.
- Each `UI/ColorVision.*/` `.csproj`: confirms target frameworks, `VersionPrefix`, `GeneratePackageOnBuild`, `PackageReadmeFile`, and resource copy rules.
- Each `UI/ColorVision.*/README.md`: confirms the README packed with the package.
- `docs/04-api-reference/ui-components/*.md`: confirms every UI project has a dedicated page.
- `Directory.Build.props`: confirms shared metadata, author, repository URL, and conditional strong-name signing through `ColorVision.snk`.

## Main Risks

| Risk | Affected modules | How to check |
| --- | --- | --- |
| Missing native runtime | `ColorVision.Core` | Check that NuGet packages and host output include OpenCV/helper DLLs under `runtimes/win-x64/native` |
| Result overlays not visible | `ColorVision.ImageEditor`, Engine result display chain | Start with [UI Runtime Component Handoff](./ui-runtime-handoff.md), then read Engine [Result Handoff Chain](../engine-components/result-handoff-chain.md) |
| Menus or plugin entries missing | `ColorVision.UI`, `ColorVision.Common`, `ColorVision.UI.Desktop` | Check menu registration, plugin discovery, permissions, and configuration loading |
| Desktop tool files missing | `ColorVision.UI.Desktop` | Check `OutputType=WinExe`, WebView2, CSS, `aria2c.exe`, and resource copy rules |
| Workspace behavior broken | `ColorVision.Solution` | Check editors, terminal, multi-image view, local RBAC, and project directory permissions |
| net8/net10 mismatch | All UI DLLs | Check host target framework, plugin target framework, and Engine package fallback versions |

## Maintenance Rules

When adding, removing, or renaming a UI DLL, update:

1. This coverage page.
2. The package map in `docs/04-api-reference/ui-components/README.md`.
3. The dedicated module page, such as `ColorVision.Xxx.md`.
4. [UI DLL Component Handbook](./component-handbook.md).
5. [UI Control Catalog](./control-catalog.md), if controls, windows, providers, or extension points changed.
6. [UI DLL Release Matrix](./release-matrix.md).
7. [UI DLL Release Evidence Checklist](./dll-release-evidence.md).
8. [UI Runtime Component Handoff](./ui-runtime-handoff.md), if runtime discovery, menus, settings, or service windows changed.
9. `docs/.vitepress/i18n/navigation-data.json`.

## Quick Audit Commands

```powershell
Get-ChildItem UI -Directory | Sort-Object Name | Select-Object -ExpandProperty Name

Get-ChildItem docs/04-api-reference/ui-components -File |
  Sort-Object Name |
  Select-Object -ExpandProperty Name

Get-ChildItem UI -Directory | Sort-Object Name | ForEach-Object {
  $csproj = Get-ChildItem $_.FullName -Filter *.csproj -File | Select-Object -First 1
  $readme = Test-Path (Join-Path $_.FullName 'README.md')
  "$($_.Name): csproj=$($csproj.Name) readme=$readme"
}
```

If a source project has no documentation page, or a documentation page points to a project that no longer exists, update this page and the sidebar first, then synchronize the translated versions.

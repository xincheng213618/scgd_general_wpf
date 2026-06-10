# UI Components & DLL Publishing

`UI/` is a set of WPF libraries that can be built and published independently. Most projects enable `GeneratePackageOnBuild`, so a release build produces DLLs plus NuGet packages.

## Publishing Facts

- Shared metadata, signing, and repository settings come from root `Directory.Build.props`.
- If `ColorVision.snk` exists, assemblies are strong-name signed.
- Most UI packages target `net8.0-windows7.0` and `net10.0-windows7.0`.
- README files are packed into NuGet packages through `PackageReadmeFile` and `Pack`.
- Image-related packages such as `ColorVision.Core` and `ColorVision.ImageEditor` must be checked for native runtime files under `runtimes/win-x64/native`.

Start with these pages:

- [Current UI DLL Documentation Coverage](./current-ui-dll-coverage.md): current UI projects, matching docs pages, release evidence, and maintenance rules.
- [UI DLL Component Handbook](./component-handbook.md): what each DLL owns and who consumes it.
- [UI Control Catalog](./control-catalog.md): concrete controls, windows, and extension points.
- [UI Runtime Component Handoff](./ui-runtime-handoff.md): runtime discovery and troubleshooting for menus, settings, plugin loading, ImageEditor, Socket, Scheduler, Marketplace, and Solution.
- [UI DLL Release Playbook](./ui-dll-release-playbook.md): scenario-based steps for publishing, field replacement, package checks, and handoff records.
- [UI DLL Release Matrix](./release-matrix.md): release units, dependencies, resources, and smoke tests.
- [UI DLL Release Evidence Checklist](./dll-release-evidence.md): `.csproj` evidence, package contents, host output checks, consumer validation, and rollback records.

Detailed publishing steps: [UI DLL Publishing](./publishing.md).

## Package Map

| Module | Version | Target | Role |
| --- | --- | --- | --- |
| `ColorVision.Common` | `1.5.5.2` | net8/net10 Windows | MVVM, plugin interfaces, shared contracts |
| `ColorVision.Themes` | `1.5.5.3` | net8/net10 Windows | themes, resources, window appearance |
| `ColorVision.UI` | `1.5.5.3` | net8/net10 Windows | config, menus, plugin loading, property editor |
| `ColorVision.Core` | `1.5.5.2` | net8/net10 Windows | OpenCV helper and image interop |
| `ColorVision.Database` | `1.5.5.3` | net8/net10 Windows | DAO, database browser, MySQL/SQLite |
| `ColorVision.SocketProtocol` | `1.5.5.2` | net8/net10 Windows | local TCP service and message history |
| `ColorVision.Scheduler` | `1.5.5.2` | net8/net10 Windows | Quartz scheduling and history |
| `ColorVision.ImageEditor` | `1.5.5.5` | net10 Windows | image viewer, drawing, overlays, 3D/CIE |
| `ColorVision.UI.Desktop` | `1.5.5.3` | net10 Windows | settings, wizard, marketplace, desktop tools |
| `ColorVision.Solution` | `1.5.5.2` | net10 Windows | workspace, editors, terminal, RBAC |

## Release Checklist

- Confirm target framework and x64 output.
- Keep strong-name signing when the key exists.
- Check README and package metadata.
- Inspect native runtime files for image packages.
- Verify resource packing and `CopyToOutputDirectory`.
- Confirm `VersionPrefix` matches the intended release.
- Verify Engine package fallback versions if distributing without UI source.

## Continue Reading

- [Current UI DLL Documentation Coverage](./current-ui-dll-coverage.md)
- [UI DLL Component Handbook](./component-handbook.md)
- [UI Control Catalog](./control-catalog.md)
- [UI Runtime Component Handoff](./ui-runtime-handoff.md)
- [UI DLL Release Playbook](./ui-dll-release-playbook.md)
- [UI DLL Release Matrix](./release-matrix.md)
- [UI DLL Release Evidence Checklist](./dll-release-evidence.md)
- [UI DLL Publishing](./publishing.md)

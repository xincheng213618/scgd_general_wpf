# UI DLL Publishing

This page explains how to publish DLL/NuGet outputs from `UI/`. For field replacement, missing-DLL incidents, image-package checks, or Engine package-only delivery, first use the [UI DLL Release Playbook](./ui-dll-release-playbook.md) to decide the release scope.

## Build

Run from the repository root:

```powershell
dotnet restore
dotnet build UI/ColorVision.UI/ColorVision.UI.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Solution/ColorVision.Solution.csproj -c Release -p:Platform=x64
```

Build the other UI projects in the same way. Most of them generate `.nupkg` and `.snupkg` automatically. For the full lower-to-upper build order, see [UI DLL Release Playbook](./ui-dll-release-playbook.md#build-and-artifact-checks).

## Output

Typical outputs are:

```text
UI/<Project>/bin/Release/ColorVision.<Name>.<Version>.nupkg
UI/<Project>/bin/Release/ColorVision.<Name>.<Version>.snupkg
UI/<Project>/bin/x64/Release/<TFM>/ColorVision.<Name>.dll
```

Actual paths depend on framework and platform settings in each `.csproj`.

## Engine Consumption

`Engine/ColorVision.Engine/ColorVision.Engine.csproj` uses project references when UI source exists. For several UI modules it falls back to `PackageReference Include="ColorVision.*"` when source projects are unavailable.

That means UI releases must work in both source-development and package-only delivery environments.

## Verification

After publishing, build the main app:

```powershell
dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64
```

Then verify:

- `ColorVision.*.dll` files exist in the main output directory.
- Native OpenCV/runtime files exist where required.
- Property editor, image editor, database browser, socket manager, and plugin manager open correctly.
- If the release includes `Core` or `ImageEditor`, inspect `runtimes/win-x64/native`, shaders, colormaps, CIE data, and OpenCvSharp runtime.
- If the release is for package-only Engine delivery, verify `UIProjectPackageVersion` and restore/build without relying on local UI source.

## Common Issues

- Missing managed DLL: check project references and NuGet resolution.
- Missing native DLL: check `Content`, `Pack`, `PackagePath`, and `CopyToOutputDirectory`.
- Plugin dependency warning: compare plugin `.deps.json` requirements with DLL versions in the main output directory.

## Continue Reading

- [UI DLL Release Playbook](./ui-dll-release-playbook.md)
- [UI DLL Release Matrix](./release-matrix.md)
- [UI DLL Component Handbook](./component-handbook.md)

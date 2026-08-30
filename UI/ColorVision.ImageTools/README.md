# ColorVision.ImageTools

`ColorVision.ImageTools` provides reusable WPF image tools extracted from the ColorVision desktop application: a file list with a single-image preview, thumbnail caching, and a focus-stacking window with folder-menu integration. It is not the Solution workspace store; some implementation namespaces still start with `ColorVision.Solution`.

## Source and knowledge

- `MultiImageViewer/`: [selection, refresh, formats, and SQLite cache](../../docs/04-api-reference/ui-components/ColorVision.ImageTools.md) (`ui.image-tools`).
- `Fusion/`: [CPU/CUDA execution, input restrictions, and result lifetime](../../docs/04-api-reference/ui-components/image-fusion.md) (`ui.image-fusion`).

These are the canonical contracts, not separate user/developer manuals. This README is packed at the NuGet package root; `docs/` is not included by this project. The relative links work in the [source repository](https://github.com/xincheng213618/scgd_general_wpf); package consumers must consult the matching source version rather than assume the latest branch describes an older package.

## Install

Use the published `ColorVision.ImageTools` package version that matches the other ColorVision packages in the host. Do not copy a version from this README; the project and package feed are the sources of truth.

The package targets `net10.0-windows7.0` and is intended for Windows WPF applications. Frameworks, dependencies, and packaging inputs are defined in [ColorVision.ImageTools.csproj](./ColorVision.ImageTools.csproj) and the referenced projects. Keep the matching ColorVision Common, UI, Solution, Core, and ImageEditor packages aligned; Core/native image operations require compatible x64 runtime DLLs. A package reference does not prove those DLLs or CUDA are usable on the host.

## Register the module

Register the assembly with the same `ModuleCatalog` used by the host, before it is sealed:

```csharp
using ColorVision.ImageTools;

// moduleCatalog is supplied by the host.
ImageToolsModule.Register(moduleCatalog);
```

Registration records the assembly for discovery; it does not open a viewer, load a folder, or run fusion. The host still owns discovery and UI integration. CVRAW/CVCIE decoding and thumbnail providers come from Engine and are not supplied merely by installing ImageTools.

## Runtime boundaries

- Viewing files may create/query/write `%APPDATA%/ColorVision/Cache/ThumbnailCache.db`; disabling thumbnails or their cache does not guarantee zero database access. Closing the viewer does not delete this persistent cache.
- Fusion reads source files through native DLLs; Auto does not retry on CPU after a GPU failure. The current CUDA fitting window has an out-of-bounds risk for 2–4 images, including Auto when it selects GPU. Do not treat the UI's two-file minimum as safe GPU support; check the full input contract before execution.
- Closing the fusion window does not cancel an in-flight native call. A displayed result is not a saved file or a validated measurement. Loading files, clearing cache, running native code, and saving results have distinct effects and require the relevant task scope.

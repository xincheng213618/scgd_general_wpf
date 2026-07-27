# ColorVision.ImageTools

`ColorVision.ImageTools` provides reusable WPF image tools extracted from the ColorVision desktop application.

## Included tools

- Multi-image viewer with thumbnail caching and file metadata.
- Image fusion window and folder-menu integration.
- Built-in module registration through `ImageToolsModule`.

## Install

```xml
<PackageReference Include="ColorVision.ImageTools" Version="1.5.6.1" />
```

The package targets `net10.0-windows7.0` and is intended for WPF applications.

## Register the module

Register the assembly with the same `ModuleCatalog` used by the host:

```csharp
ImageToolsModule.Register(moduleCatalog);
```

The package depends on the matching ColorVision UI, Solution, Core, and ImageEditor packages. Keep the ColorVision package versions aligned when consuming it outside the main repository.

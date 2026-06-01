# UI Components Overview

This chapter now only retains UI module guide pages consistent with current code implementation, no longer maintaining the old overview's mixed writing style of "version compatibility matrix + sample code + extension tutorials."

## How to Read This Chapter

If this is your first time entering this repository, it is recommended to build awareness in the following order:

1. First read [ColorVision.UI](./ColorVision.UI.md) to understand cross-cutting infrastructure such as configuration, plugins, menus, property editors, and keyboard shortcuts.
2. Then read [ColorVision.Solution](./ColorVision.Solution.md) and [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md) to understand the workspace shell and desktop auxiliary windows.
3. For image-related capabilities, follow [ColorVision.Core](./ColorVision.Core.md) -> [ColorVision.ImageEditor](./ColorVision.ImageEditor.md) upward.
4. When a specific independent subsystem needs deep exploration, then enter the corresponding individual page.

## Module Map

### Foundation Layer

- [ColorVision.Common](./ColorVision.Common.md): MVVM, shared interfaces, status bar metadata, and coarse-grained permission foundations.
- [ColorVision.Core](./ColorVision.Core.md): Native image/video capability bridging layer, responsible for `HImage` and P/Invoke export surface.

### Functional Layer

- [ColorVision.Database](./ColorVision.Database.md): Database browser, Provider registration, SQLite logging, and general-purpose DAO.
- [ColorVision.ImageEditor](./ColorVision.ImageEditor.md): `ImageView`, `DrawCanvas`, editor tools, openers, and main image interaction chain.
- [ColorVision.Scheduler](./ColorVision.Scheduler.md): Quartz scheduler, task recovery, execution history, and management window.
- [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md): Local TCP service, request dispatch, message history, and management window.

### Shell and Workspace

- [ColorVision.Solution](./ColorVision.Solution.md): Workspace, editor, terminal, multi-image viewer, and Solution-side local RBAC.
- [ColorVision.UI](./ColorVision.UI.md): UI infrastructure collection, covering cross-cutting capabilities such as configuration, plugins, menus, property editors, localization, and logging.
- [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md): Settings window, wizard, menu management, configuration management, and other desktop auxiliary windows.

### Theme Layer

- [ColorVision.Themes](./ColorVision.Themes.md): Theme resource dictionaries, theme switching entry point, and window appearance support.

## Currently Easily Confused Boundaries

- `ColorVision.UI` is not a single control library, but a cross-cutting UI infrastructure collection.
- `ColorVision.Solution` is not "only a solution file tree" — it also handles the workspace shell and local RBAC sub-module.
- `ColorVision.UI.Desktop` is not the entire product main entry point — it is more like a collection of desktop auxiliary windows and management tools.
- `ColorVision.Core` is not a high-level managed image framework, but a native interop layer.
- `ColorVision.ImageEditor` is not a pure display control — it orchestrates openers, tools, primitives, overlays, and runtime services together.

## Continue Reading Suggestions

### To View Configuration, Menus, Permissions, and Plugins

Read first:

- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Solution](./ColorVision.Solution.md)
- [ColorVision.Common](./ColorVision.Common.md)

### To View Image Pipeline

Read first:

- [ColorVision.Core](./ColorVision.Core.md)
- [ColorVision.ImageEditor](./ColorVision.ImageEditor.md)
- [ColorVision.Themes](./ColorVision.Themes.md)

### To View Desktop Tools and Operations Auxiliary Features

Read first:

- [ColorVision.Database](./ColorVision.Database.md)
- [ColorVision.Scheduler](./ColorVision.Scheduler.md)
- [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md)
- [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md)
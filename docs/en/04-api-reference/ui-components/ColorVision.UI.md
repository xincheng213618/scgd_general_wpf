# ColorVision.UI

This page only retains the most critical infrastructure and entry point types currently in `UI/ColorVision.UI/`, no longer continuing the old documentation's writing style of "version list + full pseudo-API + changelog."

## Module Positioning

`ColorVision.UI` is not a single individual control, but the location of a large amount of UI infrastructure for the desktop application. Its current role is closer to "UI shell and common service collection," primarily reused by the main program, Engine, and other UI sub-projects.

From the directory structure, what it covers includes at least:

- Configuration read/write and environment paths
- Plugin loading and plugin package handling
- Menu system
- Property editor
- Hotkey system
- Multi-language resources
- Log-related UI configuration
- Shell, Search, Status Bar, Pages, and common controls

So it is not a single "control library" and is also not suitable to continue being written as a standard SDK with a stable public API surface.

## Most Critical Directories

If you just want to quickly build awareness, it is recommended to look at these directories first:

- `Plugins/`: Plugin discovery, metadata, dependency checks, unpacking, and updates
- `PropertyEditor/`: Property editing window, tree nodes, editor type system
- `Menus/`: Menu registration and dynamic menu refresh
- `HotKey/`: Global and window-level hotkeys
- `Languages/`: Language and resource switching
- `LogImp/`: Log-related configuration and window state
- `ConfigSetting/` and root-level `ConfigHandler.cs`: Configuration system entry points
- `Shell/`, `Serach/`, `StatusBar/`: Desktop interaction auxiliary capabilities

## Key Entry Point Types

### `ConfigHandler`

`ConfigHandler` is one of the most core infrastructure components in this project. Many `IConfig` configuration objects ultimately revolve around it or related configuration services to complete reading, caching, and saving.

If the problem manifests as "settings not saved," "configuration not loaded," "default value anomalies," typically look at this chain first.

### `PluginLoader`

`PluginLoader` is currently responsible for plugin runtime loading. What it does is not just "scan DLLs," but also includes:

- Scanning the `Plugins/` directory
- Reading `manifest.json`
- Parsing optional `.deps.json`
- Checking `ColorVision.*` dependency versions
- Finally loading plugin assemblies

This is also why plugin-related documentation almost always distorts if only written as "reflectively scan plugin types."

### `MenuManager`

`MenuManager` is the central object of the menu system. Many dynamic menus, recent file refreshes, and plugin menu entry points ultimately land on its registration or refresh chain.

So this part is more like the application shell's menu coordinator rather than a set of static XAML menu definitions.

### `PropertyEditor`

`PropertyEditor/` currently handles the main chain of the property editing experience:

- `PropertyEditorWindow`
- `PropertyTreeNode`
- Editor types and helper classes

This system works in conjunction with a large number of objects in the repository carrying attributes like `Category`, `DisplayName`, `Description`, `PropertyEditorType`, and is the foundation of the current dynamic property editing experience.

### `HotKey`

The hotkey system is currently not a single-point implementation, but split into:

- `GlobalHotKey/`
- `WindowHotKey/`
- `HotKeys` and its configuration and settings windows

Therefore, when modifying hotkeys, you typically need to first distinguish whether you are modifying a system-level hotkey or a window-level hotkey.

### `Languages`

Multi-language resources and UI culture switching related capabilities are centrally managed here. After setting the UI Culture during the main program startup phase, many interface resource loads are affected by this.

### `LogImp`

Log-related UI configuration and local log window state are also placed in this project. It is more oriented toward "log display and configuration support" rather than the complete log backend itself.

## Where This Project Is Currently Most Easily Miswritten

### It Is Not a Single Control Library

Old documentation liked to write `ColorVision.UI` as the "core UI control package." The current code is far more complex than this, simultaneously handling cross-cutting capabilities like plugins, configuration, menus, hotkeys, property editing, and multi-language.

### The Plugin System Does Not Equal Extension Point Definitions Themselves

`PluginLoader` is located here, but what capabilities plugins truly extend still depends on individual plugin assemblies and the menu, template, service, and result view interfaces they implement.

### Permissions Should Not Be Generalized on This Page as a "Global RBAC Center"

Current global coarse-grained permissions come from `Authorization.Instance.PermissionMode`, while the finer local RBAC subsystem is primarily located in `UI/ColorVision.Solution/Rbac/`. `ColorVision.UI` provides the authorization infrastructure and common dependencies and should not continue to be written here as a complete permission platform.

## How to Better Read This Project Currently

### To View Configuration and Global Services

Read first:

- `ConfigHandler.cs`
- `Environments.cs`
- `FileProcessorFactory.cs`

### To View Plugin Runtime

Read first:

- `Plugins/PluginLoader.cs`
- `Plugins/PluginManifest.cs`
- `Plugins/PluginInfo.cs`

### To View the Property Editing System

Read first:

- `PropertyEditor/PropertyEditorWindow.xaml(.cs)`
- `PropertyEditor/PropertyTreeNode.cs`
- `PropertyEditor/PropertyEditors.cs`

### To View Menus and Hotkeys

Read first:

- `Menus/MenuManager.cs`
- `HotKey/HotKeys.cs`
- `HotKey/GlobalHotKey/`
- `HotKey/WindowHotKey/`

## What This Page No Longer Does

This page no longer continues to maintain these high-risk contents:

- Outdated version numbers and target framework lists
- Extensive unverified class member pseudo-code
- Describing `ColorVision.UI` as a stable public SDK
- Presenting cross-cutting capabilities like permissions, plugins, and logs as their own complete platforms

If a subsystem needs to be supplemented later, it should directly land on the corresponding topic page rather than continuing to stack "comprehensive" descriptions here.

## Continue Reading

- [UI Components Overview](./README.md)
- [ColorVision.Solution](./ColorVision.Solution.md)
- [Security and Permission Control](../../03-architecture/security/overview.md)
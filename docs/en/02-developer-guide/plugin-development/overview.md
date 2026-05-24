# Plugin Development Overview

This page only describes the plugin development model actually available in the current repository, avoiding reliance on old generic interface examples.

## Current Plugin Model

ColorVision plugins are deployed as independent directories under `Plugins/` in the main application runtime directory. When the main application starts, it scans each plugin directory, reads `manifest.json`, and loads assemblies according to the DLL specified in the manifest.

Several key points directly confirmable in the current code:

- The plugin base interface is located at `UI/ColorVision.Common/Interfaces/IPlugin.cs`
- The plugin manifest is located at `UI/ColorVision.UI/Plugins/PluginManifest.cs`
- The plugin loading logic is located at `UI/ColorVision.UI/Plugins/PluginLoader.cs`

## Key Components

### 1. Plugin Interface

The currently visible base interface in the repository is very lightweight:

```csharp
public interface IPlugin
{
    string Header { get; }
    string Description { get; }
    void Execute();
}
```

If you just want a simple plugin entry point, starting from `IPluginBase` is usually more convenient.

### 2. manifest.json

Plugin directories typically need to provide `manifest.json`. The current manifest object contains at least these fields:

- `id`
- `manifest_version`
- `name`
- `version`
- `requires`
- `description`
- `dllpath`
- `author`
- `url`
- `entry_point`
- `icon`

The most critical are plugin identifier, description, and DLL path; `entry_point` is used when explicitly specifying the entry type.

### 3. Loading Process

After the main application starts, `PluginLoader` will:

1. Scan plugin directories under `Plugins/`.
2. Read `manifest.json` from each directory.
3. Calculate DLL path based on manifest.
4. Validate dependencies and versions.
5. Load assemblies and write plugin info to internal cache.

If a plugin directory has no manifest, the platform will still attempt to load using the "directory-name-matching DLL" approach, but this is no longer the recommended form.

## Recommended Directory Structure

```text
Plugins/
└── MyPlugin/
    ├── manifest.json
    ├── MyPlugin.csproj
    ├── MyPlugin.dll
    ├── README.md
    ├── CHANGELOG.md
    ├── Assets/
    └── Sources/ or *.cs/*.xaml
```

## Development Recommendations

### In-Platform Development

- Create `Plugins/<PluginId>/` project within the repository.
- After building, copy output to `Plugins/<PluginId>/` under the main application output directory.
- Prioritize referencing existing standard plugin directory and packaging approaches.

### External Independent Development

- The final plugin deliverable should remain as a directly copyable complete directory.
- Do not repackage `ColorVision.*.dll` already included in the platform main application.
- Third-party runtime dependencies should be distributed together with plugin artifacts.

## Suggested Reading Order

1. First read [Plugin Development Overview](./README.md)
2. Then read [Plugin Development Getting Started](./getting-started.md)
3. When you need to understand loading and runtime phases, read [Plugin Lifecycle](./lifecycle.md)
4. To reference existing plugins, directly check [Pattern Plugin](../../04-api-reference/plugins/standard-plugins/pattern.md)

## Notes

- `IPluginContext`, async lifecycle interfaces, and standalone plugin host models that appear in old documentation are not the directly visible base interfaces in the current repository's main path.
- If documentation and code are inconsistent, follow `UI/ColorVision.Common/Interfaces/IPlugin.cs`, `UI/ColorVision.UI/Plugins/PluginLoader.cs`, and existing plugin projects.
# EventVWR Plugin

This page only describes the EventVWR plugin implementation that actually exists in the current repository, no longer maintaining the old "complete subsystem manual + idealized API table" draft.

## What This Plugin Does Now

From the current source code, EventVWR primarily does two things:

- Provides a read-only Windows Application event error viewing window.
- Provides a set of Dump configuration menus for writing or clearing Windows Error Reporting LocalDumps registry entries.

Therefore, it is not a complex diagnostic platform, but two very direct functional chains: "event window + Dump configuration menus."

## Most Critical Files

- `Plugins/EventVWR/EventVWRPlugins.cs`
- `Plugins/EventVWR/ExportEventWindow.cs`
- `Plugins/EventVWR/EventWindow.xaml(.cs)`
- `Plugins/EventVWR/Dump/DumpConfig.cs`
- `Plugins/EventVWR/Dump/MenuDump.cs`
- `Plugins/EventVWR/manifest.json`

If you just want to understand how the plugin integrates into the host, how to open the event window, and how to modify Dump settings, these files are already sufficient.

## Two Menu Chains Connecting to the Host

### Event Window Entry Point

`ExportEventWindow` inherits `MenuItemBase` and is currently placed under the `Help` menu:

- `OwnerGuid = "Help"`
- `GuidId = "EventWindow"`
- `Order = 1000`

Upon execution, it opens the `EventWindow` dialog.

This entry point also has an important constraint: `Execute()` currently carries `RequiresPermission(PermissionMode.Administrator)`, indicating that it is not a purely local auxiliary menu, but is constrained by the host permission mode.

### Dump Settings Entry Point

`MenuDump` is also a parent menu item under the `Help` menu, and `MenuThemeProvider` further provides sub-menus for it:

- Individual `DumpType` enum items
- Clear Dmp
- Save Dmp

Therefore, EventVWR currently does not only have a single window entry point, but two independent sets of capabilities under the Help menu.

## How the Event Window Currently Works

The logic of `EventWindow.xaml.cs` is very direct:

1. Opens the Windows `Application` event log upon window initialization.
2. Reads all `EventLogEntry`.
3. Keeps only events with `EntryType == Error`.
4. Sorts in reverse chronological order by `TimeGenerated`.
5. Binds the results to the left-side list.
6. When a record is selected, displays the `Message` in the detail area.

This means the current window does not have complex filters, searchers, or async pagination logic — it is essentially a "quick error event browser."

## How Dump Configuration Currently Lands

`DumpConfig` handles the actual system settings writing, with current core points including:

- The target registry path is `HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps`.
- It preferentially reads default LocalDumps configuration, then overrides it for the current process's corresponding `LocalDumps\{Name}.exe`.
- The key fields currently managed are:
  - `DumpFolder`
  - `DumpCount`
  - `DumpType`
  - `CustomDumpFlags`

Both writing configuration and clearing configuration require administrator privileges; if not currently running as administrator, it will directly show a dialog prompt without continuing execution.

Beyond registry configuration, `SaveDump()` also calls `DumpHelper.WriteMiniDump(...)` to write the current process dump to the target directory.

## Current Manifest Information

According to `manifest.json`, the basic information currently publicly available for this plugin is:

- `Id = "EventVWR"`
- `name = "Event Plugin"`
- `version = "1.0"`
- `dllpath = "EventVWR.dll"`
- `requires = "1.3.15.10"`

This is closer to the information that the current plugin loading model actually cares about than the old documentation's "target framework, dependency matrix, complete API table."

## Most Common Mistakes to Avoid

### It Is Not a Complete Event Diagnostic Center

The current implementation only reads error items from the Windows Application log and displays message text. Do not continue writing it as a platform with advanced search, export, and multi-log-source analysis.

### Dump Configuration Is System-Level Writing

`DumpConfig` currently operates on LocalDumps registry entries under HKLM, not application-internal configuration files. Precisely because of this, both writing and cleanup require administrator privileges.

### The Plugin Entry Class Itself Is Very Lightweight

`EventVWRPlugins` is now just a very thin `IPluginBase` shell, primarily providing Header and Description. The real functional entry points are not here, but in the menu items and corresponding window/configuration classes.

### Permission Boundaries Are Split into Two Layers

- The event window menu entry itself is constrained by `RequiresPermission(PermissionMode.Administrator)`.
- Dump registry writing and cleanup additionally check at runtime whether administrator privileges are available.

If only one layer is documented, the documentation would oversimplify the current behavior.

## Recommended Reading Order

1. `Plugins/EventVWR/ExportEventWindow.cs`
2. `Plugins/EventVWR/EventWindow.xaml.cs`
3. `Plugins/EventVWR/Dump/DumpConfig.cs`
4. `Plugins/EventVWR/Dump/MenuDump.cs`
5. `Plugins/EventVWR/manifest.json`

This allows seeing the host entry point first, then the window behavior and system-level configuration landing points.

## Continue Reading

- [Plugins/README.md](../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/system-monitor.md](./system-monitor.md)
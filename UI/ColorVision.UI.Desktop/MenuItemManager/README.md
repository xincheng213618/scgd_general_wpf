# MenuItemManager

Configuration and startup loading for desktop menu display overrides. Menu declarations live in `ColorVision.Common`. Menu discovery and rendering live in `ColorVision.UI/Menus`.

- `MenuItemManagerInitialized`: applies saved overrides at startup.
- `MenuItemManagerService`: editing snapshots, migration, validation and runtime override application.
- `MenuItemManagerConfig` / `MenuItemSetting`: persisted overrides and editing models, identified by `TargetName + GuidId`.
- Keyboard shortcut registration belongs to `UI/ColorVision.UI/HotKey`, not this editor.

See the [menu contract](../../../docs/04-api-reference/ui-components/menus.md) for configuration loading, migration, filtering and troubleshooting.

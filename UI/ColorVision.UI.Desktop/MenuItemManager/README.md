# MenuItemManager

Desktop editor for menu display overrides. The implementation lives here; shared menu declarations live in `ColorVision.Common`, and menu discovery/tree rendering live in `ColorVision.UI/Menus`.

- `MenuItemManagerAppProvider`: entry in Apps & Tools, outside the customizable menu tree.
- `MenuItemManagerWindow`: editing and preview surface.
- `MenuItemManagerService`: editing snapshots, migration, validation and runtime override application.
- `MenuItemManagerConfig` / `MenuItemSetting`: persisted overrides and editing models, identified by `TargetName + GuidId`.
- Keyboard shortcut registration belongs to `UI/ColorVision.UI/HotKey`, not this editor.

See the canonical [menu contract](../../../docs/04-api-reference/ui-components/menus.md) for discovery/cache boundaries, filtering, draft versus live state, migration, Apply/Cancel, persistence failures and test coverage. The website renders that same topic; this README does not maintain a second runtime contract.

In particular, an Apply success message is not proof that the configuration file was saved. Follow the canonical topic when diagnosing runtime/disk differences.

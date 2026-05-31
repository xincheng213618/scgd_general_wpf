# MenuItemManager

`MenuItemManager` is the desktop management surface for menu display overrides. It keeps menu customization outside `IMenuItem` implementations and applies persisted configuration to `MenuManager`.

## Responsibilities

- Show or hide menu items by `GuidId`.
- Override menu item order.
- Override `OwnerGuid` to move menu items in the menu tree.
- Persist settings through `MenuItemManagerConfig`.

Keyboard shortcut registration is owned by `UI/ColorVision.UI/HotKey`.

## Configuration

`MenuItemSetting` stores:

- `GuidId`
- `OwnerGuid`
- `Header`
- `DefaultOrder`
- `IsVisible`
- `OrderOverride`
- `OwnerGuidOverride`
- `SourceType`
- `SourceAssembly`

`MenuItemManagerConfig` stores the settings collection and `LastSelectedTreeNode`.

## Runtime Flow

1. `MenuItemManagerService.SyncSettingsFromMenuItems()` discovers current menu items and adds missing settings.
2. `MenuItemManagerService.ApplySettings()` applies visibility, order, and owner overrides to `MenuManager`.
3. `MenuItemManagerService.RebuildMenu()` reapplies settings and rebuilds all registered menus.
4. Owner overrides are validated so a menu item cannot be moved under itself or one of its descendants.

`MenuManager` keeps per-window menu registrations, including the original `TargetName`, `Menu`, and optional type filter used at registration time.

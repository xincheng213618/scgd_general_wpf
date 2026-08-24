# MenuItemManager

`MenuItemManager` is the desktop management surface for menu display overrides. It keeps menu customization outside `IMenuItem` implementations and applies persisted configuration to `MenuManager`.

## Responsibilities

- Show or hide menu items by the scoped identity `TargetName + GuidId`.
- Override menu item order.
- Override `OwnerGuid` to move menu items in the menu tree.
- Persist settings through `MenuItemManagerConfig`.

Keyboard shortcut registration is owned by `UI/ColorVision.UI/HotKey`.

## Configuration

`MenuItemSetting` stores:

- `TargetName`
- `GuidId`
- `OwnerGuid`
- `Header`
- `DefaultOrder`
- `IsVisible`
- `OrderOverride`
- `OwnerGuidOverride`
- `SourceType`
- `SourceAssembly`

`MenuItemManagerConfig` stores only customized overrides and the last selected target/tree node.
The legacy full `Settings` snapshot is retained only as a deserialization migration input and is removed on the next save.
Legacy overrides without `TargetName` are expanded to every matching live scope, then become explicitly scoped after the next Apply.

## Runtime Flow

1. `MenuItemManagerService.CreateEditingSnapshot()` combines the live menu catalog with persisted overrides in a detached editing copy.
2. The window changes only that copy; Cancel or closing the window discards it.
3. `MenuItemManagerService.CommitEditingSnapshot()` validates and prunes the copy, applies it to `MenuManager`, rebuilds registered menus, and saves the config.
4. Reset changes only the editing copy until Apply is selected.
5. Owner overrides are validated so a menu item cannot be moved under itself, one of its descendants, or a parent from an unrelated target window. A target-specific item may use a `Global` parent.
6. With no persisted override, startup clears stale runtime maps without enumerating the live menu catalog.

`MenuManager` keeps per-window menu registrations, including the original `TargetName`, `Menu`, and optional type filter used at registration time.

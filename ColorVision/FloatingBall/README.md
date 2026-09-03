# ColorVision Desktop Pets

This is the Windows/WPF desktop-pet component of the ColorVision host. It
renders the embedded 小彩 image and supported local sprite sheets, with
optional ColorVision Copilot activity and approval notifications.

The settings page's `创建` dialog offers two separate actions:

- `用 Codex 创建` prepares Hatch Pet when needed and opens a prefilled task
  in the local Codex app. The user sends the task there; opening it does not
  mean a pet has been generated. The settings page can discover a new local
  package while its creation watch remains active.
- `导入精灵表` validates a local PNG/WebP sheet and writes a new package under
  `%APPDATA%\ColorVision\DesktopPets`. Import preserves the source image
  and uses a new folder name instead of overwriting an existing package.

小彩 and local imports work without Codex. Reading Codex's installed or
custom assets depends on their local availability and supported layout;
installation alone does not guarantee usable assets. Selecting an asset
also does not enable the desktop-pet window.

The [desktop-pet documentation](../../docs/04-api-reference/ui-components/desktop-pet.md)
owns the setup steps, asset discovery, manifest example, sprite dimensions
and animation rows, configuration, and troubleshooting. Keep those
contracts there rather than duplicating a second format specification here.

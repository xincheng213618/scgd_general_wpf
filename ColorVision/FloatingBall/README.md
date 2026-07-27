# ColorVision Desktop Pets

The desktop pet uses one native WPF sprite renderer for built-in, Codex, and
custom pets.

## Asset sources

- `小彩` is embedded in ColorVision and is always available.
- If Codex is installed locally, ColorVision reads its current built-in sprite
  sheets directly from that installation. Codex files are not copied into the
  ColorVision package.
- Custom pets are loaded from
  `%APPDATA%\ColorVision\DesktopPets\<pet-folder>\pet.json`.
- The `创建` button in desktop pet settings validates and imports a PNG or
  WebP sprite sheet, writes `pet.json`, refreshes the catalog, and selects the
  new pet.
- Existing Codex custom pets under `%CODEX_HOME%\pets` and the legacy
  `%CODEX_HOME%\avatars` folder are also recognized.

## Custom pet format

Each pet has its own directory with this shape:

```text
my-pet/
  pet.json
  spritesheet.webp
```

Example `pet.json`:

```json
{
  "id": "my-pet",
  "displayName": "My Pet",
  "description": "A custom ColorVision companion.",
  "spriteVersionNumber": 2,
  "spritesheetPath": "spritesheet.webp"
}
```

Version 1 sprite sheets are 8 columns by 9 rows. Version 2 sheets are 8
columns by 11 rows. A sheet may be at most 20 MB; the current Codex frame size
is 192 by 208 pixels.

Imports are staged under the target directory and then moved into place. An
existing pet folder is never overwritten; repeated names receive a numeric
suffix.

The state rows used by ColorVision are idle `0`, running right `1`, running
left `2`, waving `3`, jumping `4`, failed `5`, waiting `6`, running `7`, and
review/completed `8`. Horizontal dragging switches between the directional
running rows using the same four-pixel threshold as Codex.

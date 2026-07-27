# Removed localization cultures

Marker: `REMOVED_LOCALIZATION_CULTURES`

The following UI cultures were intentionally removed on 2026-07-28 to reduce localization maintenance:

- `fr` — French
- `ru` — Russian
- `ja` — Japanese
- `ko` — Korean

The retained UI resources are Simplified Chinese (neutral resources), English (`en`), and Traditional Chinese (`zh-Hant`).

To restore a culture later, search the Git history for its `Properties/Resources.<culture>.resx` files and restore the corresponding language-name entries in `ColorVision.UI`.

# Property Editor

ColorVision uses a metadata-based property editor to uniformly display and edit object properties. Most templates, devices, and configuration objects expose parameters through the same PropertyGrid mechanism.

## What You Will See

- A property list in the left panel or popup
- Parameter areas grouped by category
- Edit controls that automatically switch based on type, such as text, enum, boolean, color, and file path

## Common Usage

1. Select an object in the object tree, template editor, or device configuration interface.
2. View grouped parameters in the property panel.
3. After modifying values, observe whether the interface or object state updates immediately.
4. If there are reset or validation buttons, prioritize using buttons rather than directly manually modifying underlying configuration files.

## What This Editor Depends On

- Metadata such as `Category`, `DisplayName`, `Description` on properties
- Custom editor types used when necessary to determine control styles
- Some complex objects expand into nested structures in the editor rather than simple strings

## Recommended Pages for Further Reading

- [Main Window Guide](./main-window.md)
- [Image Editor Overview](../image-editor/overview.md)
- [Developer Guide](../../02-developer-guide/README.md)

## Notes

- This page only describes the usage perspective and no longer carries PropertyGrid implementation details.
- If you need to modify property editor behavior, it is recommended to start by reading the existing metadata annotations in `UI/ColorVision.UI/` and `Engine/ColorVision.Engine/Templates/`.
# Template Menu Entries

`Templates/Menus/` is not an algorithm module. It is the menu skeleton that places template features under the main Template menu and opens the correct `TemplateEditorWindow`.

## Quick Facts

| Item | Class |
| --- | --- |
| Top-level menu | `MenuTemplate` |
| Algorithm template group | `MenuITemplateAlgorithm` |
| Generic template menu base | `MenuItemTemplateBase` |
| Algorithm template menu base | `MenuITemplateAlgorithmBase` |
| Default action | `new TemplateEditorWindow(Template).Show()` |

## Menu Hierarchy

```text
MenuTemplate
  -> MenuITemplateAlgorithm
       -> MenuITemplateAlgorithmBase derived menu entries
```

`MenuItemTemplateBase` owns the default behavior:

| Member | Behavior |
| --- | --- |
| `OwnerGuid` | Defaults to `nameof(MenuTemplate)` |
| `Execute()` | Calls `ShowTemplateWindow()` |
| `Template` | Abstract property implemented by each concrete entry |
| `ShowTemplateWindow()` | Opens `TemplateEditorWindow(Template)` |

Algorithm template entries usually inherit `MenuITemplateAlgorithmBase`, set `Header` and `Order`, and return a new template instance.

## Examples

| Menu class | Parent | Template |
| --- | --- | --- |
| `ExportFocusPoints` | `MenuITemplateAlgorithm` | `TemplateFocusPoints` |
| `ExportRoi` | `MenuITemplateAlgorithm` | `TemplateRoi` |
| `ExportMenuItemMatching` | `MenuITemplateAlgorithm` | `TemplateMatch` |
| `MenuDefalutDicAlg` | `MenuITemplateAlgorithm` | `TemplateModParam` |
| `MenuGhost2` | `MenuITemplateAlgorithm` | `TemplateGhostQK` |
| `MenuLEDStripDetectionV2` | `MenuITemplateAlgorithm` | `TemplateLEDStripDetectionV2` |

## Handoff Notes

- `Menus/` organizes entry points; it does not execute algorithms.
- Changing `OwnerGuid` changes where the menu appears and can hide a template from operators.
- The default window is non-modal `Show()`. Override `ShowTemplateWindow()` only when a template really needs custom behavior.
- Save/import/export behavior belongs to the template implementation, not the menu item.
- Search using current source spellings such as `MenuDefalutDicAlg`.

## Related Pages

- [Template Management](./template-management.md)
- [Templates API Reference](./api-reference.md)
- [Plugin Development Overview](../../../02-developer-guide/plugin-development/README.md)
- [Extension Points](../../extensions/README.md)
- [Current Algorithm Template Coverage](../current-algorithm-template-coverage.md)

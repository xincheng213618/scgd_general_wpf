# LED Detection Templates

This page defines the handoff boundary for LED-related detection templates. It covers the strongly typed `LEDStripDetection/` and `LedCheck/` directories, and explains how they coexist with `Jsons/LEDStripDetectionV2/` and `Jsons/LedCheck2/`.

## Four Entry Points

| Entry | Type | Code / event | Use case |
| --- | --- | --- | --- |
| `LEDStripDetection/` | Strongly typed template | `Code = LEDStripDetection`, `Event_LED_StripDetection` | Legacy LED strip location. |
| `LedCheck/` | Strongly typed template | `Code = FindLED`, `Event_LED_Check_GetData` | LED bead detection with POI dependency and circle rendering. |
| `Jsons/LEDStripDetectionV2/` | JSON template | `Code = LEDStripDetection`, event string `LEDStripDetection`, `Version = 2.0` | New LED strip / POI center calculation. |
| `Jsons/LedCheck2/` | JSON template | `Code = FindLED`, `Event_OLED_FindDotsArrayMem_GetData` | Sub-pixel OLED dot-array detection. |

Do not assume a `Code` value uniquely identifies one implementation: both `LEDStripDetection` and `FindLED` have legacy strongly typed and newer JSON entries.

## Strongly Typed LEDStripDetection

| File | Handoff use |
| --- | --- |
| `TemplateLEDStripDetection.cs` | Registers the template, with `TemplateDicId = 21` and `IsUserControl = true`. |
| `LEDStripDetectionParam.cs` | Stores point count, distance, start position, binary percentage, debug flag, and save path. |
| `EditLEDStripDetection.xaml(.cs)` | Custom parameter editor. |
| `AlgorithmLEDStripDetection.cs` | Builds `Event_LED_StripDetection` request. |
| `DisplayLEDStripDetection.xaml(.cs)` | Selects template, image source, batch/raw/local file, and executes. |

The request sends `ImgFileName`, `FileType`, `DeviceCode`, `DeviceType`, `TemplateParam`, and `IsInversion`.

## Strongly Typed LedCheck

| File | Handoff use |
| --- | --- |
| `TemplateLedCheck.cs` | Registers LED bead detection, `Code = FindLED`. |
| `LedCheckParam.cs` | Stores channel, fixed radius, contour area, binary correction, grid count, and local point settings. |
| `AlgorithmLedCheck.cs` | Collects LED and POI templates, then publishes `Event_LED_Check_GetData`. |
| `DisplayLedCheck.xaml(.cs)` | Selects LED template, POI template, and image source. |
| `ViewHandleMTF.cs` | Restores points from POI results and draws circles. |
| `ViewResultLedCheck.cs` | Stores point and radius. |

`LedCheck` sends `POITemplateParam` in addition to `TemplateParam`. The UI uses `TemplatePoi.Params.CreateEmpty()`, so confirm whether the field workflow allows an empty POI or requires a concrete POI template.

`ViewHandleLedCheck.CanHandle` is currently empty. If execution succeeds but result display is not taken over, check result-type registration before changing drawing code.

## JSON V2 Entries

- `TemplateLEDStripDetectionV2`: `TemplateDicId = 26`, `Name = LedStripDetectionV2`, JSON fields such as `debugCfg`, `mathMaskRect`, `nV1`, `threshold`, `dRatio`, `pattern`, and `CalcMethod`.
- `AlgorithmLEDStripDetectionV2`: event name `LEDStripDetection`, sends `Version = 2.0`, and can include `POITemplateParam`.
- `TemplateLedCheck2`: `TemplateDicId = 18`, `Code = FindLED`.
- `AlgorithmLedCheck2`: sends `Event_OLED_FindDotsArrayMem_GetData`, `Color`, `FDAType`, and four `FixedLEDPoint` values.

## Which Entry to Use

| Need | Recommended entry |
| --- | --- |
| Maintain legacy LED strip location | `LEDStripDetection/` |
| Add complex LED strip parameters or versioned JSON config | `Jsons/LEDStripDetectionV2/` |
| Maintain traditional LED bead detection with POI radius rendering | `LedCheck/` |
| Sub-pixel OLED dot-array detection | `Jsons/LedCheck2/` |
| Debug result display | Check handler result-type registration first, then drawing/export code. |

## Troubleshooting

| Symptom | Check first |
| --- | --- |
| Strip template dropdown is empty | `TemplateLEDStripDetection.Params` and `TemplateDicId = 21`. |
| V2 template dropdown is empty | `TemplateLEDStripDetectionV2` and JSON dictionary recovery for `TemplateDicId = 26`. |
| LED bead execution fails | `TemplateParam`, `POITemplateParam`, file type, and device code/type. |
| JSON parameter change has no effect | Confirm you edited the V2 JSON template, not the legacy strongly typed template. |
| Result does not display | Whether `ViewResultAlgType` matches a registered handler; `ViewHandleLedCheck.CanHandle` may need registration. |
| CSV export is wrong | `ViewHandleLedCheck.SideSave(...)` currently writes header values where row values should be written; treat export as needing acceptance. |

## Handoff Checklist

- Any `Code = LEDStripDetection` change must state whether it affects the legacy template or JSON V2.
- Any `Code = FindLED` change must state whether it affects `LedCheck` or `LedCheck2`.
- Strongly typed parameter changes require source, defaults, editor, and field sample updates.
- JSON parameter changes require schema/description JSON, `Mysql*` recovery command, and version strategy updates.
- Result-display changes require handler `CanHandle`, export, project acceptance, and screenshot samples to be updated together.

## Continue Reading

- [JSON Templates](./json-templates.md)
- [POI Template](./poi-template.md)
- [Result Handoff Chain](../../engine-components/result-handoff-chain.md)
- [Current Algorithm Template Coverage](../current-algorithm-template-coverage.md)

# JND Template

This page documents `Engine/ColorVision.Engine/Templates/JND/`. The JND template itself stores only a small set of algorithm parameters, but runtime execution must also select a POI template, and results are displayed and exported per POI point.

## Scope

| Item | Current implementation |
| --- | --- |
| Template code | `OLED.JND.CalVas` |
| Template class | `TemplateJND : ITemplate<JNDParam>, IITemplateLoad` |
| Parameter class | `JNDParam` |
| Dependent template | `TemplatePoi` |
| Algorithm entry | `AlgorithmJND`, display name `JND` |
| UI panel | `DisplayJND.xaml(.cs)` |
| MQTT event | `MQTTAlgorithmEventEnum.Event_OLED_JND_CalVas_GetData` |
| Result handler | `ViewHandleJND` |
| Main result types | `Compliance_Math_JND`, `JND_CalVas` |

## Source Entry Points

| File | Handoff use |
| --- | --- |
| `TemplateJND.cs` | Registers the JND template, with `TemplateDicId = 30` and `Code = OLED.JND.CalVas`. |
| `JNDParam.cs` | Defines `CutOff`. |
| `AlgorithmJND.cs` | Collects both JND and POI templates, then publishes the algorithm request. |
| `DisplayJND.xaml.cs` | Selects JND template, POI template, image file, and device source. |
| `ViewHandleJND.cs` | Loads results, displays table columns, draws POI points, exports CSV, and saves image view. |
| `ViewRsultJND.cs` | Parses POI result JSON into `POIResultDataJND`. |
| `MysqlJND.cs` | Restores dictionary data; default `CutOff = 0.3`. |

## Runtime Chain

1. `TemplateJND` is discovered and loaded into `TemplateJND.Params`.
2. `DisplayJND` binds both `TemplateJND.Params` and `TemplatePoi.Params`.
3. The user selects a JND template, POI template, and input image.
4. `AlgorithmJND.SendCommand(...)` sends `ImgFileName`, `FileType`, `DeviceCode`, `DeviceType`, `TemplateParam`, and `POITemplateParam`.
5. The command is published to `Event_OLED_JND_CalVas_GetData`.
6. `ViewHandleJND` reads point results through `PoiPointResultDao` and `ViewRsultJND` parses `h_jnd` / `v_jnd`.

## Parameters and Results

| Item | Meaning |
| --- | --- |
| `CutOff` | Contour cutoff coefficient, default `0.3`. Keep image, POI template, and service version with any change. |
| `h_jnd` | Horizontal JND result from `POIResultDataJND`. |
| `v_jnd` | Vertical JND result from `POIResultDataJND`. |
| POI points | JND consumes `TemplatePoi`; point changes directly change JND output. |

## Project Boundary

`ProjectShiyuan` consumes JND/POI exports and JND validation. Do not equate "JND CSV exists" with product PASS; project logic may still read `Compliance_Math_JND`, inspect `Validate`, copy images, or generate pseudo-color output.

Related page: [ProjectShiyuan](../../projects/project-shiyuan.md).

## Display and Export

The displayed fields are `Name`, `PixelPos`, `PixelSize`, `Shapes`, `JND.h_jnd`, and `JND.v_jnd`.

`SideSave(...)` writes CSV and attempts to save the current image view. The implementation currently treats `selectedPath` both as a CSV path and as part of a PNG path, so confirm caller semantics before changing export behavior.

## Troubleshooting

| Symptom | Check first |
| --- | --- |
| POI-related execution error | Whether `TemplatePoi.Params` is loaded and `TemplatePoiSelectedIndex` is valid. |
| JND result is empty | Result type and whether `PoiPointResultDao` can find rows by `Pid`. |
| Table has points but no JND values | Whether `PoiPointResultModel.Value` deserializes into `POIResultDataJND`. |
| Project OK/NG does not match algorithm page | Check project-level JND validation. |
| Export path is odd | Check the `selectedPath` semantics in `SideSave(...)`. |

## Handoff Checklist

- When changing `CutOff`, update `JNDParam.cs`, `MysqlJND.cs`, and field recommended values.
- When changing POI selection or coordinate behavior, update [POI Template](./poi-template.md) and project docs.
- When changing result fields, update `ViewRsultJND.cs`, export columns, project pages, and acceptance samples.
- If a project depends on JND judgement, document the final OK/NG source in that project page.

## Continue Reading

- [POI Template](./poi-template.md)
- [POI Primitive](../primitives/poi.md)
- [ProjectShiyuan](../../projects/project-shiyuan.md)
- [Result Handoff Chain](../../engine-components/result-handoff-chain.md)
- [Current Algorithm Template Coverage](../current-algorithm-template-coverage.md)

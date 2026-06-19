# FocusPoints Template

`FocusPoints/` is the legacy light-area and focus-point parameter template. It stores preprocessing settings such as binarization, blur, morphology, area/rectangle filters, and ROI bounds, then feeds manual algorithm pages and Flow nodes.

## Quick Facts

| Item | Value |
| --- | --- |
| Template class | `TemplateFocusPoints` |
| Parameter class | `FocusPointsParam` |
| `TemplateDicId` | `15` |
| Code | `focusPoints` |
| Manual algorithm | `AlgorithmFocusPoints` |
| MQTT event | `Event_LightArea_GetData` |
| Flow operator | `FocusPoints` |
| Menu entry | `ExportFocusPoints` |

## Source Entry Points

| File | Role |
| --- | --- |
| `Templates/FocusPoints/TemplateFocusPoints.cs` | Template registration and identity |
| `Templates/FocusPoints/FocusPointsParam.cs` | Parameter fields |
| `Templates/FocusPoints/AlgorithmFocusPoints.cs` | Manual algorithm and MQTT request |
| `Templates/FocusPoints/DisplayFocusPoints.xaml(.cs)` | Manual run page |
| `Templates/FocusPoints/ExportFocusPoints.cs` | Template menu entry |
| `Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs` | Flow property-panel binding |
| `FlowEngineLib/Algorithm/AlgorithmNode.cs` | Maps light-area detection to `FocusPoints` |

## Parameter Groups

| Group | Fields | Handoff Meaning |
| --- | --- | --- |
| `Binarize` | `Binarize`, `BinarizeThresh` | Enable binarization and threshold |
| `Blur` | `Blur`, `BlurSize` | Mean blur toggle and size |
| `Erode` | `Erode`, `ErodeSize` | Erosion toggle and size |
| `Dilate` | `Dilate`, `DilateSize` | Dilation toggle and size |
| `Param` | `FilterRect`, `Width`, `Height` | Rectangle filter and size limits |
| `FilterArea` | `FilterArea`, `MaxArea`, `MinArea` | Area filter and bounds |
| `Roi` | `Roi`, `Left`, `Right`, `Top`, `Bottom` | Optional ROI boundary |

These ROI bounds are template inputs, not result overlay coordinates. For result points and POI reuse, continue to [ROI Primitive](../primitives/roi.md) and [POI Primitive](../primitives/poi.md).

## Runtime Chain

`DisplayFocusPoints` selects a template, image source, and optional batch number, then `AlgorithmFocusPoints.SendCommand(...)` sends:

- `ImgFileName`
- `FileType`
- `DeviceCode`
- `DeviceType`
- `TemplateParam`

The event name is `MQTTAlgorithmEventEnum.Event_LightArea_GetData`.

In Flow, `AlgorithmType.发光区检测` maps to `operatorCode = "FocusPoints"`. The same family may also expose ROI, AA-find-points, and POI-save templates through the FindLightArea node configurator, so do not judge the full light-area capability only from the `FocusPoints/` folder.

## Handoff Notes

- `TemplateDicId = 15` and `Code = "focusPoints"` are the key identifiers.
- This template stores preprocessing thresholds, not project judgement rules.
- Manual execution uses `Event_LightArea_GetData`; Flow nodes use the `FocusPoints` operator code.
- The `FocusPoints/` folder does not currently own a dedicated `ViewHandle*.cs`; result display usually lives in light-area, ROI, or POI chains.
- `DilateSize` has a historical Chinese description that mentions erosion. Follow the field name when handing over behavior.

## Acceptance Checklist

| Scenario | Check |
| --- | --- |
| Template management | FocusPoints appears and fields/defaults are correct |
| Manual run | A local image, service Raw/CIE file, or batch input can be sent |
| Flow run | `AlgorithmNode` with light-area detection can select a FocusPoints template |
| Result tracing | Follow light-area/POI result pages for actual output and overlays |

## Related Pages

- [FindLightArea Template](./find-light-area.md)
- [POI Template](./poi-template.md)
- [Engine Template And Flow Chain](../../engine-components/template-flow-chain.md)
- [Current Algorithm Template Coverage](../current-algorithm-template-coverage.md)

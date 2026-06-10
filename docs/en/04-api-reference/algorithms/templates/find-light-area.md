# FindLightArea Template

This page documents the real handoff chain for `Engine/ColorVision.Engine/Templates/FindLightArea/`. It is not a generic ROI SDK; it is a business template that turns ROI parameters and an input image into an MQTT algorithm request, then displays light-area points as an image overlay.

## Scope

| Item | Current implementation |
| --- | --- |
| Template code | `FindLightArea` |
| Template class | `TemplateRoi : ITemplate<RoiParam>, IITemplateLoad` |
| Parameter class | `RoiParam` |
| Algorithm entry | `AlgorithmRoi`, display name "发光区定位1" |
| UI panel | `DisplayRoi.xaml(.cs)` |
| MQTT event | `MQTTAlgorithmEventEnum.Event_LightArea2_GetData` |
| Result handler | `ViewHandleFindLightArea` |
| Result table | `t_scgd_algorithm_result_detail_light_area` |

## Source Entry Points

| File | Handoff use |
| --- | --- |
| `TemplateRoi.cs` | Registers the `FindLightArea` template, sets `TemplateDicId = 31`, and restores dictionary data through `MysqlRoi`. |
| `ROIParam.cs` | Defines `Threshold`, `Times`, and `SmoothSize`. |
| `AlgorithmRoi.cs` | Builds the algorithm request and publishes the MQTT command. |
| `DisplayRoi.xaml.cs` | Handles template selection, image source selection, batch/raw/local image input, and execution. |
| `AlgResultLightAreaDao.cs` | Defines result loading, overlay rendering, and list display. |
| `MysqlRoi.cs` | Restores MySQL dictionary and default template items. |

## Runtime Chain

1. `TemplateRoi` is discovered by the template system and registered into `TemplateControl`.
2. The UI selects one item from `TemplateRoi.Params`.
3. `DisplayRoi` accepts batch number, raw/CIE file from the algorithm service, or local image file input.
4. File extensions are mapped to `Raw`, `CIE`, `Tif`, or `Src`; `HistoryFilePath` can replace a historical file name with a full path.
5. `AlgorithmRoi.SendCommand(...)` sends `ImgFileName`, `FileType`, `DeviceCode`, `DeviceType`, and `TemplateParam`.
6. The command is published to `Event_LightArea2_GetData`.
7. `ViewHandleFindLightArea` handles `LightArea` and `FindLightArea` results.

## Parameters

| Parameter | Default | Handoff note |
| --- | --- | --- |
| `Threshold` | `1` | Light-area threshold; record image type and exposure when changing it. |
| `Times` | `1` | Algorithm-side iteration/processing count, interpreted by the algorithm service. |
| `SmoothSize` | `1` | Smoothing size; validate the output polygon, not only the point list. |

## Result Display

`AlgResultLightAreaModel` stores `PosX`, `PosY`, and `Pid`. The handler builds a convex hull with `GrahamScan.ComputeConvexHull(...)` and draws it as a transparent blue `DVPolygon`.

Two boundaries matter during handoff:

- The point list and the convex hull are not the same artifact. If the hull is strange, check the input image and ROI parameters first.
- The current `SideSave(...)` creates an export file but does not write point rows. Do not treat it as a stable CSV export until implementation and acceptance samples are added.

## Troubleshooting

| Symptom | Check first |
| --- | --- |
| Template dropdown is empty | Assembly loading, `IITemplateLoad`, and dictionary recovery for `TemplateDicId = 31`. |
| Execution says no template selected | Whether `TemplateRoi.Params` was bound to `ComboxTemplate.ItemsSource`. |
| Algorithm service cannot read the image | `ImgFileName`, `FileType`, historical path replacement, and device code/type. |
| Result page has no points | Result type `LightArea`/`FindLightArea` and rows in `t_scgd_algorithm_result_detail_light_area`. |
| Overlay polygon is wrong | `Threshold`, `Times`, `SmoothSize`, input image, and convex-hull input points. |

## Handoff Checklist

- When changing parameters, update `ROIParam.cs`, `MysqlRoi.cs`, and field recommended values together.
- When changing the execution event, update `AlgorithmRoi.SendCommand(...)`, flow-node docs, and this page.
- When changing result structure, update `AlgResultLightAreaModel`, the result table, display columns, and export behavior.
- If project packages consume the result, state whether they consume raw points, the hull, or an image region.

## Continue Reading

- [ROI Primitive](../primitives/roi.md)
- [OpenCV Integration](../../../02-developer-guide/engine-development/opencv-integration.md)
- [Result Handoff Chain](../../engine-components/result-handoff-chain.md)
- [Current Algorithm Template Coverage](../current-algorithm-template-coverage.md)

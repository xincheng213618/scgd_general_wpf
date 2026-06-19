# Matching Template

This page explains `Engine/ColorVision.Engine/Templates/Matching/`: template parameters, manual execution UI, Flow integration, and AOI result display. Matching sends `MatchTemplate` to the algorithm service and displays returned AOI rectangles as image overlays.

## Scope

| Item | Current implementation |
| --- | --- |
| Template class | `TemplateMatch : ITemplate<MatchParam>, IITemplateLoad` |
| Parameter class | `MatchParam : ParamModBase` |
| Template code | `MatchTemplate` |
| Dictionary ID | `TemplateDicId = 34` |
| Manual algorithm entry | `AlgorithmMatching` |
| UI | `DisplayMatching.xaml(.cs)` |
| Menu entry | `ExportMenuItemMatching`, order `50` |
| MQTT event | `MQTTAlgorithmEventEnum.Event_MatchTemplate` |
| Flow node | `AlgorithmTMNode` |
| Result type | `ViewResultAlgType.AOI` |
| Result table | `t_scgd_algorithm_result_detail_aoi` |
| Result handler | `ViewHandleMatching` |

## Parameters

| Parameter | Default | Meaning |
| --- | --- | --- |
| `MinReducedArea` | `256` | Sampling detail, described as `64 ~ 2048`. |
| `ToleranceAngle` | `0` | Angle tolerance, described as `0-180`. |
| `Similarity` | `0.7` | Similarity threshold, described as `0-1`. |
| `MaxOverlapRatio` | `0` | Max overlap ratio, described as `0-0.8`. |
| `TargetNumber` | `70` | Target count. |

`TemplateFile` is not part of `MatchParam`. It is a runtime parameter on `AlgorithmMatching` and `AlgorithmTMNode`, so record the parameter template and template file separately.

## Execution Flow

Manual execution uses `DisplayMatching`: choose a `TemplateMatch`, choose `TemplateFile`, select a local image, service Raw/CIE file, or batch number, then call `AlgorithmMatching.SendCommand(...)`.

The MQTT request contains `ImgFileName`, `FileType`, `DeviceCode`, `DeviceType`, `TemplateFile`, and `TemplateParam`, and uses event `Event_MatchTemplate`.

The Flow path uses `AlgorithmTMNode`, whose `operatorCode` is `MatchTemplate`. It sends `TempName`, `TemplateFile`, and image parameters through `TMParam`.

Current XAML binds the template ComboBox `SelectedIndex` to `TemplatePoiSelectedIndex`, while `SendCommand` reads `TemplateSelectedIndex`. If the UI-selected template is not sent, check this binding first.

## Result Display

`ViewHandleMatching` handles `ViewResultAlgType.AOI`:

1. Load rows from `AlgResultAoiDao.GetAllByPid(result.Id)`.
2. Open `result.FilePath` when it exists.
3. Use four corner points for each AOI row.
4. Build a convex hull through `GrahamScan.ComputeConvexHull(...)`.
5. Draw a blue `DVPolygon` overlay.
6. Show score, angle, center point, and corner coordinates in the table.

The result table is `t_scgd_algorithm_result_detail_aoi`, with fields such as `score`, `angle`, `center_x/y`, and the four corner coordinate pairs.

The current `Load(...)` reloads DAO data only when `result.ViewResults != null`. If historical results show no AOI details, verify the caller's initialization and this condition.

## Troubleshooting

| Symptom | Check first |
| --- | --- |
| Service does not execute | `DeviceCode`, `DeviceType`, `Event_MatchTemplate`, and service status. |
| Template file invalid | `TemplateFile` existence and service-side path access. |
| Template parameters ignored | ComboBox binding, `TemplateSelectedIndex`, and `TemplateMatch.Params`. |
| Result table empty | Master result type `AOI` and detail table `pid`. |
| Overlay position wrong | Corner coordinates, source image, scaling, and coordinate system. |
| Table header duplicated | The last two Chinese headers currently both say left-bottom X; verify whether Y should be shown. |

## Handoff Checklist

- Record parameter template, template file, input image source, and algorithm service device.
- Keep sample image, template file, and expected AOI result when changing parameters.
- Update DAO, table columns, overlay, and project exports when result fields change.
- Validate both manual UI and Flow execution.

## Further Reading

- [Engine Result Handoff Chain](../../engine-components/result-handoff-chain.md)
- [Engine Template And Flow Chain](../../engine-components/template-flow-chain.md)
- [ROI Primitive](../primitives/roi.md)
- [Current Algorithm Template Coverage](../current-algorithm-template-coverage.md)

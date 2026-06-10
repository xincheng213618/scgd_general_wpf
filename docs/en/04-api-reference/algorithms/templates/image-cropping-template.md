# ImageCropping Template

`ImageCropping/` covers the legacy strong-typed image-cropping template, manual algorithm page, Flow nodes, and result display. It is different from `Jsons/ImageROI`, which belongs to the JSON-template branch.

## Quick Facts

| Item | Value |
| --- | --- |
| Template class | `TemplateImageCropping` |
| Parameter class | `ImageCroppingParam` |
| `TemplateDicId` | `32` |
| Code | `ImageCropping` |
| Manual algorithm | `AlgorithmImageCropping` |
| MQTT event | `Event_Image_Cropping` |
| Flow operator | `OLED.GetRIAand` |
| Result type | `ViewResultAlgType.Image_Cropping` |
| Result handler | `ViewHandleImageCropping` |

## Source Entry Points

| File | Role |
| --- | --- |
| `Templates/ImageCropping/TemplateImageCropping.cs` | Template registration and MySQL recovery command |
| `Templates/ImageCropping/ImageCroppingParam.cs` | Template fields |
| `Templates/ImageCropping/AlgorithmImageCropping.cs` | Manual algorithm, runtime ROI points, MQTT request |
| `Templates/ImageCropping/DisplayImageCropping.xaml(.cs)` | Manual run page |
| `Templates/ImageCropping/ViewHandleImageCropping.cs` | Result table and export |
| `FlowEngineLib/Node/OLED/OLEDImageCroppingNode.cs` | Two-input Flow cropping node |

## Parameters And ROI

`ImageCroppingParam` currently has only two persisted fields:

| Field | Meaning |
| --- | --- |
| `UnEgde` | Edge-related cropping parameter, spelling follows the source |
| `O_Index` | Output order/index parameter, default recovery SQL uses `[0,1,2,3]` |

`Point1` through `Point4` are runtime `PointFloat` values owned by `AlgorithmImageCropping`. They are sent as the `ROI` array during manual execution and are not persisted as template fields.

## Runtime Chain

Manual execution sends `ImgFileName`, `FileType`, `DeviceCode`, `DeviceType`, `TemplateParam`, and `ROI` to `MQTTAlgorithmEventEnum.Event_Image_Cropping`.

Flow has two relevant paths:

- Generic `AlgorithmNode`: `AlgorithmType.图像裁剪` maps to `operatorCode = "OLED.GetRIAand"` and selects `TemplateImageCropping`.
- `OLEDImageCroppingNode`: node `图像裁剪2` has `IN_IMG` and `IN_ROI`; it reads the upstream ROI master id into `OLEDImageCroppingParam.ROI_MasterId`.

## Result Display

`ViewHandleImageCropping` handles `ViewResultAlgType.Image_Cropping`. It loads detail rows through `AlgResultImageDao`, opens `result.FilePath` when it exists, and shows `file_name`, `order_index`, and `FileInfo`.

One current handoff risk: `SideSave(...)` writes CSV to `selectedPath` and also combines `selectedPath` with a PNG file name. Verify whether callers pass a directory or a file path before promising image export behavior.

## Handoff Notes

- This page describes strong-typed `ImageCropping`, not JSON `ImageROI`.
- Manual four-point ROI is runtime input, not saved inside `ImageCroppingParam`.
- Flow `OLEDImageCroppingNode` depends on upstream ROI results.
- Empty result tables should be traced through `ViewResultAlgType.Image_Cropping`, master id, and `AlgResultImageDao`.
- Keep the source spelling `UnEgde` when documenting or migrating data.

## Related Pages

- [Engine Result Display And Project Handoff](../../engine-components/result-handoff-chain.md)
- [Engine Template And Flow Chain](../../engine-components/template-flow-chain.md)
- [ROI Primitive](../primitives/roi.md)
- [JSON Templates](./json-templates.md)
- [Current Algorithm Template Coverage](../current-algorithm-template-coverage.md)

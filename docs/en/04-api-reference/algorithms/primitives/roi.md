# ROI

This page only describes the ROI-related primitives that actually exist in the current repository, no longer maintaining the old "unified ROI module design diagram" draft.

## How ROI Is Actually Split in the Current Repository

Based on current source code status, ROI is not a unified library under a single directory, but has at least three related branches:

1. Classic light area localization template, located in `Templates/FindLightArea`
2. Image cropping JSON template, located in `Templates/Jsons/ImageROI`
3. ARVR's `SFR_FindROI` JSON template, located in `Templates/Jsons/SFRFindROI`

So this page is more like a "ROI entry map," not "global ROI abstract class documentation."

## Most Critical Files

- `Engine/ColorVision.Engine/Templates/FindLightArea/TemplateRoi.cs`
- `Engine/ColorVision.Engine/Templates/FindLightArea/ROIParam.cs`
- `Engine/ColorVision.Engine/Templates/FindLightArea/AlgorithmRoi.cs`
- `Engine/ColorVision.Engine/Templates/FindLightArea/DisplayRoi.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/ImageROI/TemplateImageROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/ImageROI/AlgorithmImageROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/AlgorithmSFRFindROI.cs`

## What the Classic ROI Chain Currently Looks Like

### Template Entry Point

The current classic ROI actually resides in the `FindLightArea` group of code, not `Templates/ROI` as old documentation wrote.

`TemplateRoi`'s implementation characteristics are very clear:

- `Name = FindLightArea`
- `Code = FindLightArea`
- `TemplateDicId = 31`
- Returns `MysqlRoi` via `GetMysqlCommand()`

Therefore, this chain is currently essentially a "light area localization template," not a system-wide unified ROI definition.

### Parameter Model

`RoiParam` is currently very direct, exposing only three parameters:

- `Threshold`
- `Times`
- `SmoothSize`

This is not the same thing as the generic rectangle ROI or polygon ROI API from old drafts. It is more like a concrete algorithm's threshold template, rather than an abstract geometry object.

### Runtime and UI

`AlgorithmRoi` handles:

- Opening the `TemplateRoi` editing window
- Getting `DisplayRoi`
- Assembling `Event_LightArea2_GetData` request

`DisplayRoi` handles the current real user input flow:

- Selecting templates
- Selecting image source service
- Supporting three input types: batch number, raw files, and local images
- Pulling Raw file lists and supporting direct opening

This shows that the current classic ROI is closer to a "light area detection algorithm frontend host," rather than a standalone drawing component.

## Two JSON ROI Branches

### ImageROI

`TemplateImageROI` is a JSON template branch, currently:

- `Code = Image.ROI`
- `TemplateDicId = 52`
- `IsUserControl = true`

It hosts structured cropping parameters through `EditTemplateJson`, while `AlgorithmImageROI` publishes `Image.ROI` events.

This chain is about image cropping configuration, not a replica of the classic light area template.

### SFR_FindROI

`TemplateSFRFindROI` is also a JSON template branch, currently:

- `Code = ARVR.SFR.FindROI`
- `TemplateDicId = 36`
- `IsUserControl = true`

Its description text explicitly gives a `SfrRoiParam` structure hint; `AlgorithmSFRFindROI`, beyond the JSON template itself, additionally carries `POITemplateParam`, then publishes `ARVR.SFR.FindROI`.

This shows that "finding ROI" in ARVR is no longer a pure ROI template, but an algorithm chain where ROI and POI are linked together.

## Most Common Mistakes to Avoid

### ROI Is Not a Unified Base Library

ROI-related implementations in the current repository are scattered across classic parameter template and JSON template paths, with no single `ROI` root module handling all scenarios.

### Classic ROI Currently Primarily Refers to Light Area Localization

If `FindLightArea` is not used as the primary anchor, this page is easily written as a non-existent "generic ROI SDK."

### JSON ROI and Classic ROI Are Not the Same Configuration Model

`TemplateImageROI` and `TemplateSFRFindROI` are both JSON template hosts, while `TemplateRoi` is a traditional parameter template. The three cannot be merged into a single parameter table.

### Some ROI Chains Are Already Bound to POI

`AlgorithmSFRFindROI` explicitly requires `TemplatePoi`. In the current ARVR chain, ROI and POI are no longer two completely separate concept layers.

## Recommended Reading Order

1. `Engine/ColorVision.Engine/Templates/FindLightArea/TemplateRoi.cs`
2. `Engine/ColorVision.Engine/Templates/FindLightArea/AlgorithmRoi.cs`
3. `Engine/ColorVision.Engine/Templates/FindLightArea/DisplayRoi.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/Jsons/ImageROI/TemplateImageROI.cs`
5. `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

## Continue Reading

- [POI Primitive](./poi.md)
- [POI Templates](../templates/poi-template.md)
- [ARVR Templates](../templates/arvr-template.md)
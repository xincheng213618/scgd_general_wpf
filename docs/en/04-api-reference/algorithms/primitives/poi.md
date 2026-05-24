# POI

This page only describes POI as it exists as a shared primitive in the current repository, no longer maintaining the old "POI detection algorithm encyclopedia" draft.

## What Role POI Plays in the Current System

Based on current source code status, POI is more like a reusable data and template system rather than a single algorithm result:

- The main template stores point sets and configuration.
- Layout, filtering, revision, calibration, and output work around this point set.
- JSON algorithms and ARVR algorithms continue to reference POI templates.
- Flow nodes also treat POI as a shared input/output object.

Therefore, the focus of this page is not "how to find feature points," but how the POI primitive is currently stored, passed, and consumed.

## Most Critical Files

- `Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
- `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIFilters/TemplatePoiFilterParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIRevise/TemplatePoiReviseParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIOutput/TemplatePoiOutputParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIGenCali/TemplatePoiGenCalParam.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/AlgorithmPoiAnalysis.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/AlgorithmSFRFindROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/OLEDAOI/AlgorithmOLEDAOI.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## What the Current Data Looks Like

### Point Object

`PoiPoint` now stores a very direct set of display and positioning fields:

- `Id`
- `Name`
- `PointType`
- `PixX`, `PixY`
- `PixWidth`, `PixHeight`

It is not an abstract "point of interest interface," but a concrete object already aligned with the fields needed for current image editing and result display.

### Template Object

`PoiParam` packages point sets, dimensions, corner points, and configuration into a template. It currently contains at minimum:

- Template dimensions `Width`, `Height`
- Template type `Type`
- Four corner coordinates
- `PoiPoints`
- `CfgJson` and `PoiConfig`

Moreover, `CfgJson` is not a simple string cache; it currently serializes and deserializes with `PoiConfig` bidirectionally.

## How It Is Currently Stored

A core reality of POI is that it currently has its own dedicated master-detail data structure.

- Master records are saved via `PoiMasterDao`
- Point details are saved via `PoiDetailDao`

`PoiParam.LoadPoiDetailFromDB(...)` backfills point sets by `Pid`; the extension method `Save2DB(...)` clears old details and writes new points in batch.

This makes POI significantly different from templates that only depend on `ModMasterModel`/`ModDetailModel`.

## How the Current Runtime Chain Consumes POI

### Main POI Algorithm

`AlgorithmPoi` is the most direct POI consumer and producer. It currently supports:

- Main template `TemplatePoi`
- Filter template `TemplatePoiFilterParam`
- Revision template `TemplatePoiReviseParam`
- Output template `TemplatePoiOutputParam`
- File mode `POIStorageModel.File`

Ultimately publishes MQTT requests with multiple template parameters via `Event_POI_GetData`.

### Layout Algorithm

`AlgorithmBuildPoi` handles converting other information into POI point sets. It currently supports:

- Regular layout
- CADMapping layout
- Four-point polygon `LayoutPolygon`
- `CADMappingParam`
- `Event_Build_POI`

So in the current system, "obtaining a POI" does not only rely on detection, but can also rely on construction.

### Downstream Algorithm References

POI is now consumed by multiple other algorithm chains:

- `AlgorithmPoiAnalysis` includes `POITemplateParam`
- `AlgorithmSFRFindROI` includes `POITemplateParam`
- `AlgorithmOLEDAOI` also includes `POITemplateParam`

Therefore, POI is currently one of the input formats for other algorithms, not an accessory object that only appears at the end of result pages.

### Flow Node References

`POINodeConfigurators` shows that POI has become a shared node resource in Flow:

- `POINode` requires main template, filter, revision, and output templates
- `BuildPOINode` connects layout template, write-back POI template, and layout ROI template simultaneously
- `POIReviseNode` connects revision calibration template
- `POIAnalysisNode` connects JSON analysis template

This also shows that POI is a core primitive that must be selected during flow design time.

## Most Common Mistakes to Avoid

### POI Is Not the Result Structure of a Single Detection Algorithm

It is currently used simultaneously in detection, layout, analysis, AOI, and Flow nodes, and is a shared data template.

### Storage Is Not Only Database, Nor Only File

The main template goes through the database, but `AlgorithmPoi` also explicitly supports file mode and external point files.

### Companion Templates Are First-Class Members of the Current System

Filter, revision, calibration, and output templates all have real implementations and editing entry points, not "future extensions" in comments.

### Some Algorithms Consume POI, Not Produce POI

Chains like `PoiAnalysis`, `SFR_FindROI`, and `OLEDAOI` essentially read and use existing POI templates.

## Recommended Reading Order

1. `Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs`
2. `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
3. `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
4. `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## Continue Reading

- [POI Templates](../templates/poi-template.md)
- [JSON Templates](../templates/json-templates.md)
- [Flow Engine](../templates/flow-engine.md)
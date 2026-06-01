# POI Templates

This page only describes the POI template family that actually exists in the current repository, no longer maintaining the old "detector interface encyclopedia + pluggable algorithm samples" draft.

## What This Template Family Currently Does

Based on current source code status, POI is not an isolated template, but a set of templates and algorithm hosts centered around "point set data":

- The main POI template handles saving point sets, dimensions, and configuration.
- Filter, revision, calibration, and output each have their own companion templates.
- Runtime algorithms handle assembling these templates into MQTT requests.
- Flow nodes and several JSON algorithms continue to consume POI templates.

Therefore, what this page really covers is not "a particular POI detection algorithm," but how POI templates are created, edited, saved, and reused in the current system.

## Most Critical Files

- `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs`
- `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
- `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIFilters/TemplatePoiFilterParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIRevise/TemplatePoiReviseParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIOutput/TemplatePoiOutputParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIGenCali/TemplatePoiGenCalParam.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## How the Current Main Chain Runs

### Main Template and Data Model

`TemplatePoi` is the main entry point. It has several very important implementation characteristics:

- Inherits `ITemplate<PoiParam>`
- `IsSideHide = true`
- Template code is fixed as `POI`
- Directly opens `EditPoiParam` when a list item is double-clicked

Unlike many regular templates, the POI main template does not simply depend on the right-side `PropertyGrid`, but has its own editing window.

`PoiParam` is not a simple parameter class storing just a few values. It currently carries:

- Template dimensions `Width`, `Height`
- Four corner coordinates `LeftTopX/Y`, `RightTopX/Y`, `RightBottomX/Y`, `LeftBottomX/Y`
- Bidirectional conversion between `CfgJson` and `PoiConfig`
- `ObservableCollection<PoiPoint> PoiPoints`

`PoiPoint` itself stores the point information actually used by the current system:

- `Id`
- `Name`
- `PointType`
- `PixX`, `PixY`
- `PixWidth`, `PixHeight`

So the POI template is currently closer to a combination of "point set template + configuration template."

### Current Persistence Approach

The POI main template does not use the regular `ModMasterModel`/`ModDetailModel` default path. It currently uses dedicated tables:

- `PoiMasterDao`
- `PoiDetailDao`

`PoiParam.LoadPoiDetailFromDB(...)` loads point details back into `PoiPoints`; the extension method `Save2DB(...)` will:

- Save the master record
- Delete old point details
- Rewrite the entire set of `PoiDetailModel` using BulkCopy

This is also one of the places where POI pages are most easily written incorrectly: it is not "a set of regular detail items in the generic template table," but has its own point table.

### Import, Copy, and Create

`TemplatePoi` currently supports:

- Copying from the current template as a JSON temporary copy
- Importing point set templates from `.cfg`
- Actively loading point details before export
- Writing imported copies or empty templates back to the database upon creation

Moreover, after copying or importing, the template `Id` and each point's `Id` are reset to `-1` to prevent directly reusing old primary keys.

### Runtime Algorithm Chain

`AlgorithmPoi` is the current primary POI runtime entry point. It handles:

- Opening the POI main template editing window
- Opening filter, revision, and output template editing windows
- Selecting external point files in file mode
- Assembling MQTT parameters for `Event_POI_GetData`

The currently sent parameters are not limited to the main template, but may also include:

- `FilterTemplate`
- `ReviseTemplate`
- `OutputTemplate`
- `POIStorageType`
- `POIPointFileName`
- `IsSubPixel`
- `IsCCTWave`

This shows that the POI runtime chain is already a "multi-template combined request," not a single template running alone.

### Layout and Companion Templates

`AlgorithmBuildPoi` is another key chain. It currently handles:

- Opening the layout template `TemplateBuildPoi`
- Optionally loading CAD files
- Attaching four-point polygon and `CADMappingParam` when `POIBuildType == CADMapping`
- Publishing `Event_Build_POI`

Beyond this, the POI family currently includes multiple companion templates:

- `TemplatePoiFilterParam`: Filter template, `Code = POIFilter`, uses custom editing control
- `TemplatePoiReviseParam`: Revision template, `Code = PoiRevise`
- `TemplatePoiGenCalParam`: Calibration template, `Code = POIGenCali`, uses custom editing control
- `TemplatePoiOutputParam`: Output template, `Code = PoiOutput`, uses custom editing control

These templates are not "optional extensions" in comments, but objects actually referenced in current Flow and algorithm chains.

### How Flow and Other Algorithms Consume POI

POI is already a shared primitive, not a private template of a single algorithm. Currently there are at least three clear consumption paths:

1. `POINodeConfigurators` connects `TemplatePoi`, filter, revision, output, calibration, and other templates to POI node property panels.
2. `AlgorithmPoiAnalysis` additionally carries `POITemplateParam` alongside the JSON analysis template.
3. Algorithms like `AlgorithmSFRFindROI` and `AlgorithmOLEDAOI` also additionally reference `TemplatePoi`.

## Most Common Mistakes to Avoid

### POI Is Not a Single Algorithm

POI in the current repository is more like a shared point set template system, capable of generating points, filtering points, and also being consumed by other algorithms.

### Primary Storage Is Not a Regular Detail Table

The main template depends on `PoiMasterDao` and `PoiDetailDao`; continuing to explain it using the generic template table model would miss the point detail layer.

### The Main Editor Is Not Pure `PropertyGrid`

`TemplatePoi` enters `EditPoiParam` after double-click; filter and output templates also have their own `UserControl` editors. Continuing to write them as a unified right-side property panel would not match the real interface.

### File Mode and Database Mode Coexist

`AlgorithmPoi` explicitly supports both `POIStorageModel.Db` and `POIStorageModel.File` paths. Documentation can no longer write POI as "only existing in the database."

## Recommended Reading Order

1. `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
2. `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
3. `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
4. `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## Continue Reading

- [POI Primitive](../primitives/poi.md)
- [JSON Templates](./json-templates.md)
- [Flow Engine](./flow-engine.md)
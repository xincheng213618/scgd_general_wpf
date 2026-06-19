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
- `Engine/ColorVision.Engine/Templates/POI/POIGenCali/TemplatePOICalParam.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## Current Template Matrix

Only the main POI template uses the dedicated point tables. Companion templates are still dictionary templates, so do not merge them into one persistence model.

| Template | Dictionary/code | Editor entry | Main purpose |
| --- | --- | --- | --- |
| `TemplatePoi` | `TemplateDicId = -1`, `Code = POI` | Standalone `EditPoiParam` window | Saves point sets, dimensions, corners, config JSON, and point details. |
| `TemplateBuildPoi` | `TemplateDicId = 16`, `Code = BuildPOI` | Template/layout UI | Builds POI by rules or CAD mapping. |
| `TemplatePoiFilterParam` | `TemplateDicId = 23`, `Code = POIFilter` | Custom filter editor | Optional filter template during POI execution. |
| `TemplatePoiReviseParam` | `TemplateDicId = 24`, `Code = PoiRevise` | Template editor | Optional revision template during POI execution. |
| `TemplatePoiGenCalParam` | `TemplateDicId = 25`, `Code = POIGenCali` | Custom calibration editor | Used by POI calibration/revision Flow nodes. |
| `TemplatePoiOutputParam` | `TemplateDicId = 27`, `Code = PoiOutput` | Custom output editor | Optional file output template during POI execution. |
| `TemplateBuildPOIAA` | `TemplateDicId = 41`, `Code = BuildPOI` | JSON template editor | JSON V2 branch that builds POI from AA point-finding results. |

The main template stores real point positions. Companion templates describe how points are built, filtered, revised, and output.

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

- `PoiMasterDao` -> `t_scgd_algorithm_poi_template_master`
- `PoiDetailDao` -> `t_scgd_algorithm_poi_template_detail`

`PoiParam.LoadPoiDetailFromDB(...)` loads point details back into `PoiPoints`; the extension method `Save2DB(...)` will:

- Save the master record
- Delete old point details
- Rewrite the entire set of `PoiDetailModel` using BulkCopy

This is also one of the places where POI pages are most easily written incorrectly: it is not "a set of regular detail items in the generic template table," but has its own point table.

| Table | Key fields | Handoff meaning |
| --- | --- | --- |
| `t_scgd_algorithm_poi_template_master` | `name`, `type`, `width`, `height`, corner coordinates, `cfg_json`, `tenant_id`, `is_delete` | POI template body, canvas size, and config JSON. |
| `t_scgd_algorithm_poi_template_detail` | `pid`, `pt_type`, `pix_x`, `pix_y`, `pix_width`, `pix_height`, `remark` | Pixel position and size for each POI point or region. |

Deleting a template currently deletes the master record and removes it from the list. Copy/import resets template and point `Id` values to `-1`; if copied templates overwrite old points in the field, check this reset path first.

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

| Parameter | Source | Meaning |
| --- | --- | --- |
| `TemplateParam` | `TemplatePoi` | Required main POI template. |
| `FilterTemplate` | `TemplatePoiFilterParam` | Sent when `Id != -1`. |
| `ReviseTemplate` | `TemplatePoiReviseParam` | Sent when `Id != -1`. |
| `OutputTemplate` | `TemplatePoiOutputParam` | Sent when `Id != -1`. |
| `POIStorageType` | `POIStorageModel` | Sent in file mode to distinguish DB point sets from external point files. |
| `POIPointFileName` | File picker | External point file path in file mode. |
| `IsSubPixel`, `IsCCTWave` | Algorithm UI config | Runtime options for sub-pixel/CCT wave behavior. |

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

| Flow configurator branch | Device/input | Template selectors | Handoff focus |
| --- | --- | --- | --- |
| POI calibration revision | `DeviceAlgorithm` | `TemplatePoiGenCalParam` | Handles calibration/revision templates only; it does not directly select the main POI. |
| POI filter/revise/output | `DeviceAlgorithm` | `TemplatePoiFilterParam`, `TemplatePoiReviseParam`, `TemplatePoiOutputParam` | Post-processing combination for existing POI results. |
| POI execution | `DeviceAlgorithm` + image path | `TemplatePoi`, filter, revise, output | Full `Event_POI_GetData` runtime chain. |
| BuildPOI | `DeviceAlgorithm` + image path | `TemplateBuildPoi` or `TemplateBuildPOIAA`, plus `RePOI`, `LayoutROI`, `SavePOI` | Supports both traditional layout and JSON AA layout branches. |
| PoiAnalysis | `DeviceAlgorithm` | `TemplatePoiAnalysis` | JSON analysis templates still consume POI-related results. |

## Result Persistence And Display

POI results are not a single result type; handlers dispatch by `ViewResultAlgType`:

| Result type | Display/export entry | Table/file clues |
| --- | --- | --- |
| `POI`, `POI_Y` | `ViewHanlePOIY` | CSV export; values come from POI detail results. |
| `POI_XYZ` | `ViewHanlePOIXZY` | CSV export and XYZ result display. |
| `POI_XYZ_File`, `POI_Y_File`, `POI_CIE_File` | `ViewHanlePOIXZY` | File-style results, often stored through `t_scgd_algorithm_result_detail_poi_cie_file`. |
| `RealPOI`, `POI_XYZ_V2`, `POI_Y_V2`, `KB_Output_Lv`, `KB_Output_CIE` | `ViewHandleRealPOI` | V2/project output chain; always check actual `ResultType`. |
| `BuildPOI`, `BuildPOI_File` | `ViewHandleBuildPoi`, `ViewHandleBuildPoiFile` | Layout result or file result that may generate new POI data. |

Point-value detail currently includes `t_scgd_algorithm_result_detail_poi_mtf`, with fields such as `poi_id`, `poi_name`, `poi_type`, `poi_x/y`, `poi_width/height`, and `value`. If UI display and export disagree, first identify which handler handled the result type, then inspect the matching detail or file table.

## Most Common Mistakes to Avoid

### POI Is Not a Single Algorithm

POI in the current repository is more like a shared point set template system, capable of generating points, filtering points, and also being consumed by other algorithms.

### Primary Storage Is Not a Regular Detail Table

The main template depends on `PoiMasterDao` and `PoiDetailDao`; continuing to explain it using the generic template table model would miss the point detail layer.

### The Main Editor Is Not Pure `PropertyGrid`

`TemplatePoi` enters `EditPoiParam` after double-click; filter and output templates also have their own `UserControl` editors. Continuing to write them as a unified right-side property panel would not match the real interface.

### File Mode and Database Mode Coexist

`AlgorithmPoi` explicitly supports both `POIStorageModel.Db` and `POIStorageModel.File` paths. Documentation can no longer write POI as "only existing in the database."

### BuildPOI and POI Execution Are Different Events

`AlgorithmBuildPoi` publishes `Event_Build_POI`; `AlgorithmPoi` publishes `Event_POI_GetData`. The former focuses on generating point sets, while the latter reads/outputs values based on point sets. Do not mix their template parameters during field debugging.

## Acceptance Checks

| Scenario | Required check |
| --- | --- |
| Create/save POI | `t_scgd_algorithm_poi_template_master` has the master record and `t_scgd_algorithm_poi_template_detail` has matching point details. |
| Copy/import POI | Template and point detail `Id` values are reset, and creating the new template does not overwrite the old one. |
| File-mode execution | MQTT params contain `POIStorageType` and `POIPointFileName`; DB mode does not depend on external point files. |
| Filter/revise/output | Selecting companion templates adds `FilterTemplate`, `ReviseTemplate`, and `OutputTemplate`. |
| BuildPOI CADMapping | Request contains `LayoutPolygon` and `CADMappingParam`, with four-point ROI and CAD file path correct. |
| Result display | The correct handler is selected by `ViewResultAlgType`, and CSV fields match table/file results. |

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

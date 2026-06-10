# ARVR Templates

This page only describes the ARVR template family actually visible in the current repository, no longer maintaining the old "optics algorithm textbook + unified parameter manual" draft.

## What This Template Family Currently Does

Based on current source code status, ARVR is not a single template but a set of parallel templates and display algorithms:

- `MTF`
- `SFR`
- `FOV`
- `Distortion`
- `Ghost`

These implementations share the same host framework, but their parameter models, result presentations, and POI dependencies are not uniform. Moving further into Flow nodes, JSON variants such as `SFR_FindROI` are also mixed in.

So this page is better treated as an "ARVR family map," rather than attempting to maintain a universal parameter table.

## Most Critical Files

- `Engine/ColorVision.Engine/Templates/ARVR/MTF/TemplateMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/MTFParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/ViewHandleMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/SFRParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/AlgorithmSFR.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/WindowSFR.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/FOVParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/AlgorithmFOV.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/DisplayFOV.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/DistortionParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/AlgorithmDistortion.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/ViewResultDistortion.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs`
- `Engine/FlowEngineLib/Algorithm/AlgorithmARVRNode.cs`

## Current Template Matrix

The ARVR directory contains traditional strongly typed templates, while Flow also exposes JSON V2 branches. During handoff, use this table first instead of inferring behavior from directory names alone.

| Family | Traditional template | Dictionary/code | Runtime event | Key request params | Result entry |
| --- | --- | --- | --- | --- | --- |
| `FOV` | `TemplateFOV` | `TemplateDicId = 6`, `Code = FOV` | `Event_FOV_GetData` | `TemplateParam` | `ViewHandleFOV`, `ViewResultAlgType.FOV` |
| `Ghost` | `TemplateGhost` | `TemplateDicId = 7`, `Code = ghost` | `Ghost` | `TemplateParam`, `Color` | `ViewHandleGhost`, `ViewResultAlgType.Ghost` |
| `MTF` | `TemplateMTF` | `TemplateDicId = 8`, `Code = MTF` | `Event_MTF_GetData` | `TemplateParam`, `POITemplateParam` | `ViewHandleMTF`, `ViewResultAlgType.MTF` |
| `SFR` | `TemplateSFR` | `TemplateDicId = 9`, `Code = SFR` | `Event_SFR_GetData` | `TemplateParam`, `POITemplateParam` | `ViewHandleSFR`, `ViewResultAlgType.SFR` |
| `Distortion` | `TemplateDistortionParam` | `TemplateDicId = 10`, `Code = distortion` | `Distortion` | `TemplateParam` | `ViewHandleDistortion`, `ViewResultAlgType.Distortion` |
| `AOI` | `TemplateAOIParam` | `TemplateDicId = 12`, `Code = AOI` | Not a standalone primary runtime entry today | Template parameter config | Mainly ARVR/AOI parameter config; do not document it as a full result chain unless that code path is confirmed |

The runtime events above come from the current manual algorithm classes. Flow `operatorCode` also covers JSON branches such as `ARVR.BinocularFusion`, `ARVR.SFR.FindROI`, and `FindCross`.

## How the Current Main Chain Branches

### MTF

`TemplateMTF` is a classic parameter template, currently:

- `Code = MTF`
- `TemplateDicId = 8`

The most directly visible parameters in `MTFParam` include:

- `MTF_dRatio`
- `eEvaFunc`
- `dx`
- `dy`
- `ksize`

`AlgorithmMTF`'s actual behavior is not local computation, but:

- Opening `TemplateMTF`
- Opening `TemplatePoi`
- Assembling `POITemplateParam`
- Publishing `Event_MTF_GetData`

This shows that the current MTF runtime chain explicitly depends on POI templates rather than existing independently of POI.

On the result side, what is most worth examining is not the parameter class but `ViewHandleMTF`. It will:

- Export results as CSV
- Calculate max, min, mean, variance, and uniformity
- Connect as a `ViewResultAlgType.MTF` handler into the UI

### SFR

`SFRParam` is currently much simpler than in old documentation, with the only directly visible core parameter being `Gamma`. The real display and result interaction falls more on:

- `AlgorithmSFR`
- `WindowSFR`

Like MTF, `AlgorithmSFR` additionally requires `TemplatePoi` and then publishes `Event_SFR_GetData`. `WindowSFR` is responsible for deserializing `Pdfrequency` and `PdomainSamplingData` from the result into curves, and providing threshold and frequency conversion.

Therefore, current SFR documentation can no longer only discuss template parameters; it must also include the result window.

### FOV

`FOVParam` is currently a relatively complete parameter model, directly containing:

- `Radio`
- `CameraDegrees`
- `ThresholdValus`
- `DFovDist`
- `FovPattern`
- `FovType`
- `Xc`, `Yc`, `Xp`, `Yp`

`AlgorithmFOV` handles packaging `Event_FOV_GetData`, while `DisplayFOV` takes on a very practical layer of work:

- Getting image source device from service manager
- Supporting three input types: batch, raw files, and local images
- Pulling Raw file lists and allowing direct opening

This shows that FOV is currently not a minimal "configure parameters then run algorithm" template.

### Distortion

`DistortionParam` is currently a truly large parameter object, containing multiple groups of blob thresholds, area filters, shape filters, and global strategy items, such as:

- `filterByColor`
- `minThreshold` / `maxThreshold`
- `minArea` / `maxArea`
- `filterByCircularity`
- `filterByConvexity`
- `filterByInertia`
- `CornerType`
- `SlopeType`
- `LayoutType`
- `DistortionType`

`AlgorithmDistortion` handles publishing `Distortion` events, while `ViewResultDistortion` remaps enumeration values and final grid results into displayable description objects.

### Ghost

The core parameters directly visible in `GhostParam` are not complex, mainly centered on detection lattice:

- `Ghost_radius`
- `Ghost_cols`
- `Ghost_rows`
- `Ghost_ratioH`
- `Ghost_ratioL`

`AlgorithmGhost` additionally carries the `Color` parameter, then publishes the `Ghost` event. In other words, the color channel is currently a first-class input on the Ghost chain, not an addendum in page commentary.

## How They Connect in Flow

`AlgorithmARVRNode` and `AlgorithmNodeConfigurators` together reveal the real usage of the current ARVR family in Flow:

- `MTF` and `SFR` nodes require both parameter templates and `POI` templates.
- `FOV` and `Distortion` nodes can connect to both classic parameter templates and JSON variants.
- Branches like `SFR_FindROI` simultaneously connect `TemplateSFRFindROI` and `TemplatePoi`.

Therefore, the current ARVR family is not a flat directory, but a runtime surface pieced together from traditional templates, JSON templates, POI templates, and Flow nodes.

| Flow operator | `operatorCode` | Configurator mounts | Handoff focus |
| --- | --- | --- | --- |
| `MTF` | `MTF` | `TemplateMTF` + `TemplatePoi` | Missing POI means the request lacks `POITemplateParam`, and point interpretation is incomplete. |
| `SFR` | `SFR` | `TemplateSFR` + `TemplatePoi` | SFR curves depend on spatial ROI/POI definition. |
| `FOV` | `FOV` | `TemplateDFOV` + `TemplateFOV` | The same template name slot may bind JSON V2 or traditional templates; check the actual source. |
| `Distortion` | `Distortion` | `TemplateDistortion2` + `TemplateDistortionParam` | JSON V2 and strongly typed parameters coexist; result display may also depend on version. |
| `SFR_FindROI` | `ARVR.SFR.FindROI` | `TemplateSFRFindROI` + `TemplatePoi` | This is a JSON ROI detection chain, not traditional `TemplateSFR`. |
| `BinocularFusion` | `ARVR.BinocularFusion` | `TemplateBinocularFusion` | Uses JSON templates; do not look for a traditional ARVR parameter class first. |
| `FindCross` | `FindCross` | `TemplateFindCross` + `TemplatePoi` used as ROI | The UI label may say ROI, but the selector still uses `TemplatePoi`. |

`AlgorithmARVRNode.getBaseEventData(...)` also adds `BufferLen`, color channel, previous-step image params, and SMU result data. If manual execution works but Flow execution fails, compare the manual algorithm request with the Flow-generated request.

## Result Persistence And Display

Handoff should not stop at `Algorithm*.cs`. The traditional ARVR result chains currently include:

| Result | Table/field clues | Display entry | Debug focus |
| --- | --- | --- | --- |
| `FOV` | `t_scgd_algorithm_result_detail_fov`, with `pattern`, `radio`, `camera_degrees`, `dist`, `threshold`, `degrees` | `ViewHandleFOV` | Check image input, template params, and angle/distance result fields together. |
| `Ghost` | `t_scgd_algorithm_result_detail_ghost`, with `rows`, `cols`, `radius`, `led_centers`, `ghost_pixels` | `ViewHandleGhost` | Color channel and lattice count affect the final overlay. |
| `SFR` | `t_scgd_algorithm_result_detail_sfr`, with ROI fields, `gamma`, `pdfrequency`, `pdomain_sampling_data` | `ViewHandleSFR`, `WindowSFR` | CSV/curve display comes from deserialized sampling data, not a single scalar. |
| `Distortion` | `t_scgd_algorithm_result_detail_distortion`, with `layout_type`, `slope_type`, `corner_type`, `max_ratio`, `final_points` | `ViewHandleDistortion`, `ViewResultDistortion` | Validate enum mapping and final point grids together. |

## Most Common Mistakes to Avoid

### ARVR Does Not Have a Unified Schema

Subdirectories share template hosts and display algorithm style, not the same set of parameter fields.

### Most Algorithm Classes Are Hosts and Command Adapters

`AlgorithmMTF`, `AlgorithmSFR`, `AlgorithmFOV`, `AlgorithmDistortion`, `AlgorithmGhost` primarily handle opening windows, taking inputs, and sending MQTT requests, rather than directly performing numerical computation locally.

### POI Is Not Marginal in ARVR

At minimum, MTF, SFR, and SFR_FindROI all explicitly depend on `TemplatePoi`. If POI is ignored, this page cannot explain the current runtime chain.

### Result Processing Code Is Equally Important

Implementations like `ViewHandleMTF`, `WindowSFR`, `ViewResultDistortion` in the result layer are important entry points for understanding what users ultimately see and should not be omitted as in old documentation.

### Traditional Templates and JSON V2 Are Not Simple Replacements

FOV, Ghost, Distortion, SFR_FindROI, and related chains expose traditional and JSON templates in Flow. Do not document them as simply "upgraded to V2," and do not keep only the legacy description; confirm the current path by `operatorCode`, template type, and result version.

## Acceptance Checks

| Scenario | Required check |
| --- | --- |
| Manual MTF/SFR | Request contains both `TemplateParam` and `POITemplateParam`, and the result is handled by the matching `ViewHandle*`. |
| Flow ARVR node | Changing algorithm type switches template selectors, and `operatorCode` matches the selected algorithm. |
| FOV/Distortion V2 | One node can distinguish traditional and JSON templates without routing results to the wrong handler. |
| SFR curve | `WindowSFR` opens the curve, and CSV fields match `pdomain_sampling_data`. |
| Ghost | Request contains `Color`, and result-table lattice counts match overlay display. |

## Recommended Reading Order

1. `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`
2. `Engine/ColorVision.Engine/Templates/ARVR/SFR/AlgorithmSFR.cs`
3. `Engine/ColorVision.Engine/Templates/ARVR/FOV/DisplayFOV.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/ARVR/Distortion/ViewResultDistortion.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs`

## Continue Reading

- [POI Templates](./poi-template.md)
- [JSON Templates](./json-templates.md)
- [Flow Engine](./flow-engine.md)

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

## Most Common Mistakes to Avoid

### ARVR Does Not Have a Unified Schema

Subdirectories share template hosts and display algorithm style, not the same set of parameter fields.

### Most Algorithm Classes Are Hosts and Command Adapters

`AlgorithmMTF`, `AlgorithmSFR`, `AlgorithmFOV`, `AlgorithmDistortion`, `AlgorithmGhost` primarily handle opening windows, taking inputs, and sending MQTT requests, rather than directly performing numerical computation locally.

### POI Is Not Marginal in ARVR

At minimum, MTF, SFR, and SFR_FindROI all explicitly depend on `TemplatePoi`. If POI is ignored, this page cannot explain the current runtime chain.

### Result Processing Code Is Equally Important

Implementations like `ViewHandleMTF`, `WindowSFR`, `ViewResultDistortion` in the result layer are important entry points for understanding what users ultimately see and should not be omitted as in old documentation.

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
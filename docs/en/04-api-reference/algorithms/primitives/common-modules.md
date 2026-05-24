# Common Algorithm Modules

This page no longer describes the current repository as an independent "general algorithm platform." Based on source code status, it is better suited as a navigation page for shared algorithm components.

## What This Page Should Actually Cover

The common components in the current repository that are repeatedly reused by multiple algorithms, templates, and Flow nodes are mainly concentrated in these groups:

- ROI / Light area localization
- POI point sets and companion templates
- Matching template matching
- JSON-formatted cropping or edge-finding templates

The common thread among these components is not "pure algorithm kernel," but that they all simultaneously have:

- Template editing entry points
- Display algorithm hosts
- MQTT command packaging
- Sometimes also result processing or Flow integration

So the role of this page is more like "where to find which shared component group," rather than continuing to abstract a unified framework that does not exist.

## Most Important Branches to Examine First

### ROI

Classic ROI now mainly resides in `Templates/FindLightArea`:

- `TemplateRoi`
- `RoiParam`
- `AlgorithmRoi`
- `DisplayRoi`

It currently represents the light area localization template chain, not a global generic ROI SDK.

Additionally, there are JSON-version ROI entry points:

- `TemplateImageROI`
- `TemplateSFRFindROI`

If you are looking at cropping or ARVR ROI finding, these two are closer to the current state than classic `TemplateRoi`.

### POI

POI is the most typical shared primitive currently. It covers at least:

- `TemplatePoi`
- `PoiParam`
- `PoiPoint`
- `AlgorithmPOI`
- `AlgorithmBuildPoi`
- Filter, revision, calibration, output companion templates

And it continues to be referenced by JSON algorithms and Flow nodes, so POI is not a single algorithm page but a cross-module data structure.

### Matching

Matching is also a complete but relatively lean shared chain:

- `TemplateMatch`
- `MatchParam`
- `AlgorithmMatching`
- `DisplayMatching`
- `ViewHandleMatching`

`AlgorithmMatching` currently will:

- Open `TemplateMatch`
- Optionally open `TemplatePoi`
- Allow specifying `TemplateFile`
- Publish `Event_MatchTemplate`

So Matching is not just a single computation function remaining, but a complete template and command host.

## How These Shared Modules Connect to the System

Based on current implementation, they generally all follow the same type of runtime pattern:

1. Maintain templates via `TemplateEditorWindow` or custom editing controls.
2. Expose UI and commands through `DisplayAlgorithmBase` derived classes.
3. Assemble `CVTemplateParam` and other input parameters in algorithm classes.
4. Send requests to the service side via MQTT events.
5. Optionally consumed further by result handlers or Flow nodes.

This is also why writing these modules simply as "algorithm libraries" would be inaccurate — because in the current implementation, UI, templates, and command hosts are integrated.

## If You Need to Read Source Code by Requirement

### Want to See Region Selection or Cropping

Read [ROI](./roi.md) first.

### Want to See Point Set Templates, Point Set Construction, or POI Reuse

Read [POI](./poi.md) and [POI Templates](../templates/poi-template.md) first.

### Want to See Image Template Matching

Read `Engine/ColorVision.Engine/Templates/Matching/AlgorithmMatching.cs` and `TemplateMatch.cs` first.

### Want to See How These Components Are Orchestrated into Flows

Read [Flow Engine](../templates/flow-engine.md) and `Templates/Flow/NodeConfigurator` first.

## Most Common Mistakes to Avoid

### General Does Not Mean Independent Framework

These shared modules do not form a separately published public SDK, but are scattered under `ColorVision.Engine/Templates` and uniformly hosted by the main program.

### Shared Modules Are Not Pure

They typically mix templates, UI, MQTT messages, and result display. Continuing to write them following a strict three-layer architecture will easily misalign with the current state.

### POI, ROI, and Matching Have Cross-References

For example, Matching can further open `TemplatePoi`, while ARVR's `SFR_FindROI` requires POI templates. These modules are not completely independent islands.

## Continue Reading

- [ROI](./roi.md)
- [POI](./poi.md)
- [ARVR Templates](../templates/arvr-template.md)
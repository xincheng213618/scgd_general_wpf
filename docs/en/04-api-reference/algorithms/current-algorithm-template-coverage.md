# Current Algorithm Template Coverage

This page aligns the actual `Engine/ColorVision.Engine/Templates/` source directories with the current documentation entry points. It is not a promise that every algorithm is fully specified; it is a handoff map for deciding which page to read first and which directories still need deeper documentation.

## Coverage Status

| Status | Meaning |
| --- | --- |
| Dedicated page | A handoff-oriented topic page already exists for the main entry points, runtime chain, and boundaries. |
| Cross-covered | The directory is currently covered by template management, ROI/POI, common algorithm, or Engine chain pages. |
| Needs page | Ownership is clear, but the business meaning or acceptance rules should become a dedicated page later. |

## Templates Directory Coverage

| Template directory | Business role | Read first | Status | Handoff focus |
| --- | --- | --- | --- | --- |
| `ARVR/` | AR/VR detection template family connecting parameters, algorithm requests, and result views. | [ARVR Template](./templates/arvr-template.md), [Result Handoff Chain](../engine-components/result-handoff-chain.md) | Dedicated page | Covers template matrix, manual events, Flow `operatorCode`, POI dependency, result tables, handlers, and acceptance checks. |
| `BuzProduct/` | Product/business template that binds product masters, details, POI, and Validate rules. | [BuzProduct Business Template](./templates/buz-product-template.md), [Validate Rule Templates](./templates/validate-rules.md) | Dedicated page | Track the `BuzProduc` source spelling, master/detail tables, `poi_id`, and `val_rule_temp_id`. |
| `Compliance/` | Compliance result display and judgement interpretation layer for Y/XYZ/JND results. | [Compliance Result Handoff](./templates/compliance-results.md), [Result Handoff Chain](../engine-components/result-handoff-chain.md) | Dedicated page | Track result detail tables, handler type mapping, and `ValidateRuleResultType.M` pass logic. |
| `DataLoad/` | Data loading template for Flow DataLoad nodes, carrying device, serial number, result type, and ZIndex parameters. | [DataLoad Template](./templates/data-load-template.md), [Template And Flow Chain](../engine-components/template-flow-chain.md) | Dedicated page | Distinguish the `AlgDataLoadNode` template path from the explicit-parameter `AlgDataLoadNode2` path. |
| `FindLightArea/` | Light-area/ROI location templates tied to OpenCV helpers and ROI output. | [FindLightArea Template](./templates/find-light-area.md), [ROI Primitive](./primitives/roi.md) | Dedicated page | `Event_LightArea2_GetData`, `RoiParam`, point table, and convex-hull overlay. |
| `Flow/` | Flow templates that connect the template system with visual flows in `FlowEngineLib`. | [Flow Engine](./templates/flow-engine.md), [Engine Template And Flow Chain](../engine-components/template-flow-chain.md) | Dedicated page | Covers `TemplateFlow` save paths, `.cvflow` packages, import/export, runtime scheduling, and node configurator boundaries. |
| `FocusPoints/` | Legacy light-area/focus-point parameter template for binarization, filters, morphology, and ROI bounds. | [FocusPoints Template](./templates/focus-points-template.md), [FindLightArea Template](./templates/find-light-area.md) | Dedicated page | Distinguish manual `Event_LightArea_GetData` from Flow `operatorCode = "FocusPoints"`. |
| `ImageCropping/` | Strong-typed cropping template connecting four-point ROI, Flow two-input cropping, and cropped-image result display. | [ImageCropping Template](./templates/image-cropping-template.md), [Result Display And Project Handoff](../engine-components/result-handoff-chain.md) | Dedicated page | Track `Event_Image_Cropping`, `OLED.GetRIAand`, `ROI_MasterId`, and `ViewHandleImageCropping`. |
| `JND/` | JND-related detection templates, usually tied to AR/VR or display-quality business. | [JND Template](./templates/jnd-template.md), [POI Template](./templates/poi-template.md) | Dedicated page | `CutOff`, `POITemplateParam`, `h_jnd/v_jnd`, and project OK/NG boundary. |
| `Jsons/` | JSON template system with text/property editing and import/export paths. | [JSON Templates](./templates/json-templates.md), [Templates API Reference](./templates/api-reference.md) | Dedicated page | Covers the current JSON template catalog, schema index, V2/legacy boundaries, handlers, and acceptance checks. |
| `LedCheck/` | LED check template family for LED bead, brightness, or defect checks. | [LED Detection Templates](./templates/led-detection.md), [POI Template](./templates/poi-template.md) | Dedicated page | `FindLED` legacy/JSON entries, POI dependency, result handler registration, and export boundary. |
| `LEDStripDetection/` | LED strip detection templates tied to JSON templates, strip location, and defect results. | [LED Detection Templates](./templates/led-detection.md), [JSON Templates](./templates/json-templates.md) | Dedicated page | Legacy `Event_LED_StripDetection` versus JSON V2 `Version = 2.0`. |
| `Matching/` | Template matching and positioning chain covering manual UI, Flow node, MQTT request, and AOI result display. | [Matching Template](./templates/matching-template.md), [Result Display And Project Handoff](../engine-components/result-handoff-chain.md) | Dedicated page | Track `MatchTemplate`, `TemplateFile`, `t_scgd_algorithm_result_detail_aoi`, and four-point overlays. |
| `Menus/` | Template menu wrappers that define menu grouping, parent-child ownership, and default editor windows. | [Template Menu Entries](./templates/template-menu-entries.md), [Template Management](./templates/template-management.md) | Dedicated page | Track `OwnerGuid`, `Order`, `Header`, `Template`, and `ShowTemplateWindow()`. |
| `POI/` | POI template family providing points, regions, and upstream algorithm parameters. | [POI Template](./templates/poi-template.md), [POI Primitive](./primitives/poi.md) | Dedicated page | Covers main/companion template matrix, dedicated point tables, runtime params, BuildPOI, Flow consumption, and result handlers. |
| `SysDictionary/` | System dictionary template that maintains algorithm-default dictionary masters and details with `mod_type = 7`. | [SysDictionary Template](./templates/sys-dictionary-template.md), [Templates API Reference](./templates/api-reference.md) | Dedicated page | Track `TemplateModParam`, `symbol`, `default_val`, `val_type`, and migration boundaries. |
| `Validate/` | Rule-template system with a default compliance dictionary layer and actual rule-template layer. | [Validate Rule Templates](./templates/validate-rules.md), [Template Management](./templates/template-management.md) | Dedicated page | Track `mod_type = 110/111/120`, `CIEParams/JNDParams`, and rule master/detail tables. |

## Core Entry Files

| File | Handoff use |
| --- | --- |
| `TemplateContorl.cs` | Template discovery, `IITemplateLoad` loading, and registration. |
| `TemplateManagerWindow.xaml(.cs)` | Template management window; useful for tracing UI actions into template data. |
| `TemplateEditorWindow.xaml(.cs)` | Generic template editing window; useful for property editing, saving, and validation. |
| `TemplateSearchProvider.cs` | Template search entry; useful for "why can I not find this template" issues. |
| `TemplateSampleLibrary.cs` | Template sample and reuse entry; useful for tracing default template sources. |

## Maintenance Rules

- When a new `Templates/<Name>/` directory is added, add a row here first, then decide whether it needs a dedicated topic page.
- If a directory contains `Algorithm*`, result views, or MQTT execution requests, document parameter sources, execution service, result fields, and failure handling.
- If a directory is only a menu, dictionary, or wrapper layer, still state which template family it serves.
- When a "Needs page" directory enters project delivery, DLL release, or field acceptance scope, promote it to a dedicated page first.

## Next Priority Batch

1. Flow conversion/calibration nodes moved to [Flow Conversion And Calibration Nodes](../engine-components/flow-conversion-calibration-nodes.md): the current source tree does not contain `Templates/FileConvert/`, `Templates/ImageTransform/`, or `Templates/Calibration/`, so future maintenance should follow the node chain.
2. `Menus/` and `SysDictionaryMod/`: keep turning menu entries, dictionary defaults, and template-window registration into handoff checklists.
3. Customer project bundles under `Projects/`: align business entry points, dependent templates, plugin capabilities, and field acceptance criteria.

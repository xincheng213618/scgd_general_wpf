# Algorithms & Templates Overview

This chapter is now consolidated into a guide for the "template system and algorithm integration chain," no longer maintaining the old approach of flattening all image processing methods into an encyclopedia directory.

## What This Chapter Covers

The "algorithms" here primarily correspond to `Engine/ColorVision.Engine/Templates/` and its surrounding integration chain, not a comprehensive table of all low-level image processing code in the repository. The current focus includes:

- How templates are discovered, loaded, managed, and edited.
- How Flow templates integrate with `FlowEngineLib`.
- How JSON templates enter the system through specialized editors.
- How ARVR, POI, and other business template families interface with algorithm services.

If you are looking for OpenCV-level low-level processing functions, the entry point is typically not in this chapter but closer to `Engine/cvColorVision/`, `UI/ColorVision.Core/`, or the native DLL side.

## Current Chapter Structure

### Entry Page

- [Algorithm System Overview](./overview.md): Overall description of the current implementation chain — read this page first for the most efficient overview.

### Topic Directories

- `templates/`: Template management, flow templates, JSON templates, POI/ARVR, and other topic pages.
- `detectors/`: A small number of defect/detection topics.
- `primitives/`: A small number of basic building block descriptions.

These directories still retain some historical pages, but this chapter homepage no longer flattens all of them as stable entry points.

## Key Code Anchors Worth Knowing First

From the current state, these are the most important file types in the template and algorithm chain:

- `Templates/TemplateContorl.cs`: Template discovery and registration entry point.
- `Templates/TemplateManagerWindow.xaml.cs`: Template management window.
- `Templates/TemplateEditorWindow.xaml.cs`: Template editing window.
- `Templates/Flow/TemplateFlow.cs`: Flow template and flow editor integration point.
- `Templates/Jsons/ITemplateJson.cs`: Common loading/import/export logic for JSON templates.
- `Templates/Jsons/EditTemplateJson.xaml(.cs)`: JSON template editing control, handling both text and property editing modes.
- `Templates/POI/AlgorithmImp/AlgorithmPOI.cs`, `Templates/ARVR/*/Algorithm*.cs`: Typical business algorithm UI and message assembly entry points.

## Current Key Boundaries

- Many `Algorithm*` classes are not themselves the final computation core; currently their primary responsibility is collecting template parameters, file paths, and device information, then sending execution requests via MQTT/service chain.
- `POI` is not an isolated topic; in the current codebase it remains a shared upstream template and parameter source for multiple algorithm families.
- `Flow` templates, although different in presentation, are still part of the same Templates system and should not be completely separated from regular template chains.
- JSON templates and traditional strongly-typed templates currently coexist; do not assume by default that the system only retains one template definition approach.

## Recommended Reading Order

1. Start with [Algorithm System Overview](./overview.md) to build understanding of the runtime main chain.
2. Then cross-reference [Templates Module Analysis](../../03-architecture/components/templates/analysis.md) to understand directory structure and registration entry points.
3. If interested in flow templates, read [FlowEngineLib Architecture](../../03-architecture/components/engine/flow-engine.md).
4. Finally, enter individual pages under `templates/` by specific business domain, always cross-referencing with source code.
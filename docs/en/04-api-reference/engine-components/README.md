# Engine Components Overview

This chapter now only retains Engine-side module entry points that can be directly mapped to the current repository structure, no longer maintaining the old "version table + sample code + unified layer blueprint" draft.

## What This Chapter Actually Covers

The code under the `Engine/` directory is not a single algorithm library, but a set of runtime modules that work together:

- `ColorVision.Engine/`: Main engine layer, connecting services, templates, MQTT, database, and flow integration.
- `FlowEngineLib/`: Flow nodes and execution control core.
- `cvColorVision/`: Native capability wrappers and interop bridge.
- `ColorVision.FileIO/`: Image and custom format file I/O.
- `ST.Library.UI/`: Node editor and related UI foundation controls.

Therefore, when reading the Engine chapter, do not understand it as "only algorithm implementations" — it also includes runtime orchestration, flow execution, low-level wrappers, and editor support layers.

## How to Read This Chapter

If this is your first time entering the Engine code, it is recommended to build awareness in this order:

1. Start with `ColorVision.Engine` to understand how services, templates, and flows are connected into the main program.
2. Then look at `FlowEngineLib` to understand where node execution, start/end chains, and flow completion events come from.
3. Then supplement with `ColorVision.FileIO` and `cvColorVision` to distinguish the file I/O layer from the native algorithm/device wrapper layer.
4. Finally, look at `ST.Library.UI` to understand the node UI infrastructure that the flow editor depends on.

## Module Map

### Main Engine Layer

- [ColorVision.Engine](./ColorVision.Engine.md): The most important Engine entry point in the current system, primarily focusing on directories like `Services/`, `Templates/`, `MQTT/`, `Messages/`.

### Flow Execution Layer

- [FlowEngineLib](./FlowEngineLib.md): Node execution and flow control core, but it needs to be read together with `ColorVision.Engine/Templates/Flow/` to form the complete actual runtime chain.

### Underlying Support Layer

- [ColorVision.FileIO](./ColorVision.FileIO.md): File formats, import/export, and related I/O processing.
- [cvColorVision](./cvColorVision.md): Native vision capability wrappers and device/algorithm interop bridge.

### Editor Foundation Layer

- [ST.Library.UI](./ST.Library.UI.md): UI foundation capabilities such as flow node editor and property panel.

## Current Boundaries Most Easily Written Incorrectly

- `ColorVision.Engine` is not a monolithic module where "all algorithms are computed here" — it is more about organizing templates, devices, flows, and message chains.
- `FlowEngineLib` is not the complete implementation of the entire flow system; when truly entering the main program, it must go through the template and window layers in `Templates/Flow/`.
- `cvColorVision` and `ColorVision.FileIO` both belong to the support layer and should not be conflated with template/UI-side capabilities into the same layer.
- `Engine/ColorVision.ShellExtension/`, while currently existing in the source tree, has not yet been expanded in this chapter as a stable API reference entry point.

## Suggested Source Code Anchors to Read First

If the goal is to understand the real control surface on the Engine side, prioritizing these code files over old documentation is more effective:

- `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
- `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
- `Engine/FlowEngineLib/FlowEngineControl.cs`
- `Engine/FlowEngineLib/Start/BaseStartNode.cs`
- `Engine/FlowEngineLib/End/CVEndNode.cs`

## Continue Reading

- [Templates Module Analysis](../../03-architecture/components/templates/analysis.md)
- [FlowEngineLib Architecture](../../03-architecture/components/engine/flow-engine.md)
- [System Runtime](../../03-architecture/overview/runtime.md)
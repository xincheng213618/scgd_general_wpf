# Algorithm System Overview

This page only describes the template and algorithm integration chain that actually runs in the current repository, no longer maintaining the old "algorithm classification encyclopedia + sample code + GPU capability overview" draft.

## Where This System Actually Lives

The code most directly related to "algorithms" is not concentrated in a single directory:

- `Engine/ColorVision.Engine/Templates/`: Template definitions, template management, template editing, and most business algorithm UI integration points.
- `Engine/FlowEngineLib/`: Flow nodes, start/end chains, and execution control.
- `Engine/ColorVision.Engine/Services/Devices/Algorithm/`: Algorithm device service integration surface.
- `Engine/cvColorVision/` and lower-level native libraries: Handling some of the actual low-level computation and interop.

Therefore, if this chapter is only understood as a "managed algorithm function directory," it will immediately deviate from the current implementation.

## How the Current Main Chain Is Connected

From the current state, the most common runtime chain for algorithms/templates is roughly:

1. `TemplateContorl` scans loaded assemblies for `IITemplateLoad` implementations and registers templates into the system.
2. `TemplateManagerWindow` and `TemplateEditorWindow` allow users to browse, create, and edit templates.
3. Concrete business algorithm UI classes typically inherit `DisplayAlgorithmBase` and expose entry points like `OpenTemplateCommand`.
4. These algorithm UIs assemble `CVTemplateParam`, file paths, device information, and other parameters in `SendCommand(...)`.
5. Parameters are then sent to the actual execution side via `MQTTAlgorithm` or adjacent service chains.
6. For flow templates, execution enters the `TemplateFlow` + `FlowEngineToolWindow` + `FlowEngineLib` chain.

This means: many of the classes you see in `Templates/*/Algorithm*.cs` currently serve more as "algorithm frontend adapters" rather than the final operators themselves.

## Most Important Parts of the Current Template System

### Template Registration and Management

Core focus areas:

- `ITemplate.cs`
- `TemplateContorl.cs`
- `TemplateManagerWindow.xaml(.cs)`
- `TemplateEditorWindow.xaml(.cs)`

They determine how templates appear, how they are opened, and how they enter the editing flow.

### Flow Templates

`Templates/Flow/` is not a simple branch of regular parameter templates, but a special template family that connects flowcharts, flow editing windows, import/export, and batch execution together.

Current key entry points include:

- `TemplateFlow.cs`
- `FlowEngineToolWindow.xaml(.cs)`
- `DisplayFlow.xaml(.cs)`

### JSON Templates

`Templates/Jsons/` currently hosts a set of template implementations centered on JSON configuration. Its common chain mainly consists of:

- `ITemplateJson<T>`: Common logic for loading, saving, import, and export.
- `TemplateJsonParam`: JSON template parameter base type.
- `EditTemplateJson.xaml(.cs)`: Dual-mode editing control, supporting switching between text editing and property editing.

This is why you see both traditional parameter objects and JSON text editors coexisting in the template system.

### Business Template Families

The main template families still directly identifiable include:

- `POI/`
- `ARVR/`
- `JND/`
- `LedCheck/`
- `Compliance/`
- Multiple business template implementations under `Jsons/`

These directories were not all designed in the same period with the same rules; do not assume they necessarily share a completely uniform abstraction level when reading.

## Most Common Misinterpretations

### Misconception 1: Treating `Algorithm*.cs` as the Final Algorithm Implementation

Many such classes currently primarily do:

- Open template editing windows
- Maintain UI-side selection state
- Assemble message parameters
- Call `PublishAsyncClient(...)`

The actual low-level processing is often completed on the device service side, MQTT peer, native libraries, or other chains.

### Misconception 2: Thinking `POI` Is Just a Small Independent Topic

From the current code, POI remains an upstream template dependency shared by multiple ARVR/localization/analysis algorithms. Its templates and point data are repeatedly referenced by multiple algorithm UIs.

### Misconception 3: Excluding Flow Templates from the Template System

Flow templates are just more complex in presentation, but they still enter the main program through the Templates system and are then handled by adjacent windows and the flow library.

### Misconception 4: Assuming JSON Templates Are Just a "Temporary Compatibility Layer"

The current `Jsons/` directory and `ITemplateJson<T>` remain one of the main paths actually in use and should not be written as having been fully replaced by strongly-typed templates.

## Recommended Reading Order

1. `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
2. `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
3. `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
5. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
6. `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
7. Specific business algorithm directories such as `POI/`, `ARVR/`, and various `Algorithm*.cs` under `Jsons/`

## Continue Reading

- [Algorithms & Templates Overview](./README.md)
- [Templates Module Analysis](../../03-architecture/components/templates/analysis.md)
- [FlowEngineLib Architecture](../../03-architecture/components/engine/flow-engine.md)
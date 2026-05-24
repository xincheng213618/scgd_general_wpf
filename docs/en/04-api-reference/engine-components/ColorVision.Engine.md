# ColorVision.Engine

This page only describes the `ColorVision.Engine` module actually available in the current repository, no longer maintaining the old "complete API table + unified layer blueprint + pseudo-examples" draft.

## What This Module Is Now

Based on current source code status, `ColorVision.Engine` is not a simple algorithm library, but the most core engine assembly layer of the ColorVision main program. It currently handles at least:

- Host-side abstraction of device and service objects.
- Loading, editing, and persistence of the template system.
- MQTT requests, heartbeats, and message logging.
- UI and template bridging of FlowEngineLib in the main program.
- The connection between algorithm display layer and template editor.

Therefore, it is closer to a "runtime engine host layer" rather than a monolithic module that performs all business operations locally.

## Most Critical Files

- `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Flow/FlowEngineManager.cs`
- `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`
- `Engine/ColorVision.Engine/Services/DeviceService.cs`
- `Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs`
- `Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs`
- `Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs`
- `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`

If you just want to understand how the main engine organizes templates, devices, message chains, and flows, these files already cover the main path.

## How the Current Control Surface Is Partitioned

### Template Loading and Template Registration

`TemplateControl` is the overall entry point of the current template system. After MySQL becomes available, it scans all `IITemplateLoad` implementations in assemblies and executes `Load()`, then registers template instances into `ITemplateNames`.

This means the template system is currently not a hand-written static list, but connected through three steps:

- Initializer trigger
- Assembly scanning
- Template instance registry

### JSON Template Editing

`ITemplateJson<T>` shows the real landing point of current JSON templates:

- Template data is read from MySQL
- Template objects are wrapped into parameter objects via `Activator.CreateInstance`
- Save and delete also directly write back to the database

The corresponding editor `EditTemplateJson` provides:

- Text mode
- Property editing mode
- Comment/description view toggle
- External JSON validation website shortcut entry

This shows that the engine layer currently does not just store templates, but also directly hosts part of the template editing UI.

### Flow Bridge Layer

`FlowEngineManager` and `DisplayFlow` are the bridge surface between `ColorVision.Engine` and `FlowEngineLib`. They currently handle:

- Initializing Flow's MQTT default configuration
- Maintaining flow template lists and current selection
- Loading templates into `FlowEngineControl` using Base64 data
- Refreshing available service nodes combined with `MqttRCService`'s service token
- Providing UI operations for flow editing, template editing, batch record viewing, etc.

So the flow functionality in the main program is not completed by `FlowEngineLib` alone, but must go through this bridge code to truly enter the window and template system.

### Device and Service Abstraction

`DeviceService` is the foundational abstraction for host-side device objects, handling:

- Tree node behavior
- Icons and context menus
- Import/export configuration
- Reset, restart, and property commands
- Attachment to MQTT service objects or display controls

And `DeviceServiceFactoryRegistry` centrally registers service types like Camera, PG, Spectrum, SMU, Sensor as factories.

This shows that current device instantiation is no longer scattered switch-case, but centralized factory registration.

### MQTT Runtime

`MQTTServiceBase` is the most important host base class in the current message chain. It handles:

- Subscribing/publishing MQTT messages
- Maintaining `MsgRecord`
- Judging `IsAlive` based on heartbeat
- Handling timeout and response status

`MqttRCService` further takes on the registration center client role, handling:

- RC topic construction
- Re-registration
- Service token caching
- RC connection status

Many questions in the engine layer such as "is the service online, can the flow be refreshed, where does the device token come from" ultimately trace back to this layer.

## What Role Algorithms Play in This Layer

From implementations like `AlgorithmPOI` and `AlgorithmMTF`, algorithm classes in `ColorVision.Engine` currently are more about:

- Opening template editors
- Organizing template selection state
- Assembling MQTT parameters
- Calling device services to publish commands

In other words, algorithm objects at this layer are typically "display and command adapters," rather than pure algorithm kernels that directly perform image computation locally.

## Most Common Mistakes to Avoid

### It Is Not a "All Algorithms Execute Locally" Module

Many current algorithm classes actually assemble templates, file names, and device information into MQTT requests, then hand them over to the device or server for processing. Continuing to write this layer as pure local algorithm implementation would not match the real control chain.

### The Template System Cannot Be Separated from Initialization and Database

`TemplateControl` depends on assembly scanning after MySQL initialization; `ITemplateJson<T>` also directly interacts with the database. Writing it as a "completely local static template set" would miss critical prerequisites.

### Flow Functionality Is Not Entirely in FlowEngineLib

To actually edit, select, and run Flow templates in the main program, the `Templates/Flow/` bridge code layer is still needed. Only describing FlowEngineLib would miss the actual control surface on the host side.

### Device Service Instantiation Is Now Registration-Centralized

`DeviceServiceFactoryRegistry` is already the current real instantiation entry point. Continuing to use the scattered construction descriptions from old documentation would misrepresent the extension points.

## Recommended Reading Order

1. `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
2. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
3. `Engine/ColorVision.Engine/Services/DeviceService.cs`
4. `Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs`
5. `Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs`
6. `Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs`
7. `Engine/ColorVision.Engine/Templates/Flow/FlowEngineManager.cs`
8. `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`

This allows seeing the template and service host layer first, then connecting to the message chain and flow bridge layer.

## Continue Reading

- [docs/04-api-reference/engine-components/FlowEngineLib.md](./FlowEngineLib.md)
- [docs/03-architecture/components/templates/analysis.md](../../03-architecture/components/templates/analysis.md)
- [docs/04-api-reference/algorithms/overview.md](../algorithms/overview.md)
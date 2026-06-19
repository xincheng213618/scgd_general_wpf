# Engine Business Handoff

This page is for developers taking over Engine work. The goal is not to describe every algorithm, but to make the risky business chains clear enough to modify safely. If you already have a concrete requirement or defect, start with the [Engine Business Scenario Playbook](./business-scenario-playbook.md), then return here for the full chain. If the change is complete or ready for handoff, use [Engine Change Impact And Acceptance Checklist](./engine-change-impact-checklist.md) to confirm impact and evidence.

## Engine in One Sentence

Engine organizes device resources, template parameters, flow nodes, MQTT commands, algorithm results, image display, and database records into executable inspection business.

The main application and project packages usually provide entry points, screens, customer workflow, and result packaging. The core connection between devices, templates, and flows lives in Engine.

## Startup and Initialization

| Initialization target | Location | Purpose |
| --- | --- | --- |
| Plugin loading | `Engine/ColorVision.Engine/App.xaml.cs`, `UI/ColorVision.UI/Plugins/PluginLoader.cs` | Scan `Plugins/`, load plugins and project packages |
| Template initialization | `Templates/TemplateContorl.cs` | Discover and load template types and parameters |
| Device service initialization | `Services/ServiceManager.cs` | Create device service instances from resource models |

When taking over the system, first confirm that the application can complete all three steps. Failure may show up as missing menus, empty templates, or flow nodes that cannot find devices.

## Device Service Chain

Key files:

- `Services/ServiceManager.cs`
- `Services/DeviceService.cs`
- `Services/Devices/DeviceServiceFactory.cs`
- `Services/Devices/DeviceServiceConfig.cs`
- `Services/Type/TypeService.cs`

Current creation flow:

1. The system obtains `SysResourceModel` from resource configuration or database.
2. `ServiceManager` iterates resources.
3. `DeviceServiceFactoryRegistry.CreateService(sysResourceModel)` creates concrete devices based on `ServiceTypes`.
4. Devices are added to `ServiceManager.DeviceServices`.
5. UI, flow node configurators, and template pages filter the required device types from that collection.

Minimum steps for a new device:

1. Add the service type in `ServiceTypes`.
2. Add `ConfigXxx : DeviceServiceConfig`.
3. Add `DeviceXxx : DeviceService<ConfigXxx>`.
4. Register the factory in `DeviceServiceFactoryRegistry`.
5. Add a configurator under `Templates/Flow/NodeConfigurator/` if flow nodes need it.
6. Update device documentation and this module handbook.

Do not add only a window or menu. Without the factory registration, the resource can exist but no stable runtime service will be created.

## MQTT and Device Commands

Many Engine devices execute through MQTT or server-side commands rather than local direct calls.

| Location | Purpose |
| --- | --- |
| `MQTT/MQTTConfig.cs` | MQTT connection configuration |
| `MQTT/MQTTConnect.xaml.cs` | Connection window and default configuration switching |
| `Services/Devices/*/MQTT*.cs` | Device-specific MQTT command wrappers |
| `Messages/` | Business message models |

Typical path:

1. UI or a flow node calls a device service method.
2. The device service builds command parameters.
3. MQTT publishes the command to the backend service.
4. The backend returns a file, result ID, or status.
5. Engine queries results or downloads files and passes them to the display/project layer.

When debugging, continue past the UI button. Check MQTT connection, topic, backend result, and file server download.

## Template System Chain

Key files:

- `Templates/TemplateModel.cs`
- `Templates/TemplateContorl.cs`
- `Templates/TemplateManagerWindow.xaml.cs`
- `Templates/TemplateEditorWindow.xaml.cs`
- `Templates/Jsons/*/Template*.cs`
- `Templates/POI/TemplatePoi.cs`
- `Templates/Flow/TemplateFlow.cs`

Templates are not just UI forms. They manage named parameter groups, provide `CVTemplateParam` or JSON parameters to algorithm commands, and make business configuration saveable, copyable, and editable for flow templates.

Minimum steps for a new algorithm template:

1. Add a parameter class based on the existing `ParamBase` or `TemplateJsonParam` pattern.
2. Add a template class implementing `ITemplate<T>`, `ITemplateJson<T>`, or a nearby base class.
3. Set `Title`, `Code`, `TemplateDicId`, and `TemplateParams`.
4. Provide an editor, commonly `EditTemplateJson` or a dedicated UserControl.
5. Add a menu entry through `MenuITemplateAlgorithmBase` if needed.
6. Add `AlgorithmXxx`, `ViewHandleXxx`, and MySQL/DAO result reading if display is needed.
7. Add flow node and node configurator support if the template participates in flows.

The usual failure points are mismatched `TemplateDicId`, parameter collections, and result parsing. Verify save, flow execution, and display together.

## FlowEngine Chain

Key files:

- `Engine/FlowEngineLib/FlowEngineControl.cs`
- `Engine/FlowEngineLib/Start/BaseStartNode.cs`
- `Engine/FlowEngineLib/End/CVEndNode.cs`
- `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
- `Engine/ColorVision.Engine/Templates/Flow/Nodes/`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/`

`FlowEngineLib` runs nodes, but the ColorVision business flow is completed through additional layers:

- `TemplateFlow` saves and loads flow templates.
- Flow nodes bind device `DeviceCode`, template names, or algorithm parameters.
- Project packages select a flow template and trigger execution.
- After `FlowCompleted`, project packages or batch pages parse the results.

Debug flow issues in this order: template loading, device/template binding in nodes, and post-completion result processing.

## Results and Business Output

| Result type | Location | Purpose |
| --- | --- | --- |
| Raw algorithm result | `Dao/`, `Mysql*.cs` under template folders | Read algorithm results from database or backend |
| Display result | `Abstractions/IResultHandlers.cs`, `Templates/*/ViewHandle*.cs` | Draw overlays in ImageEditor or AlgorithmView |
| Project business result | `Projects/*/ObjectiveTestResult.cs`, `Process/*` | Feed customer CSV/PDF/MES/Socket outputs |

The same algorithm result may serve both common display and customer-specific judgment. Keep customer rules in project-level Process/Recipe/Fix code, not generic Engine result handlers.

## Where Common Changes Belong

| Requirement | Start here |
| --- | --- |
| New device type | `Services/Devices/`, `DeviceServiceFactoryRegistry` |
| New flow node | `Templates/Flow/Nodes/`, `Templates/Flow/NodeConfigurator/`, `FlowEngineLib` |
| New algorithm template | `Templates/Jsons/` or the matching algorithm folder |
| Template parameter editor change | Matching `Template*.cs` and editor control |
| Result overlay change | `ViewHandle*.cs`, ImageEditor draw/overlay code |
| Customer judgment rule change | `Projects/<Project>/Recipe/`, `Fix/`, `Process/` |
| Socket integration change | `Projects/<Project>/Services/SocketControl.cs` or `UI/ColorVision.SocketProtocol` |
| Batch/history result change | `Dao/`, project `ViewResultManager` |

## Troubleshooting Order

Device missing:

1. Resource exists.
2. `ServiceTypes` matches.
3. `DeviceServiceFactoryRegistry` has a factory.
4. `ServiceManager.DeviceServices` contains the instance.
5. UI or node configurator filters the correct device type.

Flow starts but produces no result:

1. Flow template loaded.
2. Node device code is valid.
3. MQTT is connected.
4. Algorithm service returns a result.
5. File server download works.
6. Project package parses the correct template name after `FlowCompleted`.

Result exists but no image overlay:

1. Matching `ViewHandle` or result handler exists.
2. Result type matches `ViewResultAlgType`.
3. ImageEditor receives the correct file path.
4. Overlay coordinates are converted for the current image size.

Project CSV/PDF field is empty:

1. Engine raw result contains a value.
2. Project `Process.Execute()` reads the correct key.
3. Recipe/Fix does not change it to empty or failed.
4. `ObjectiveTestResult` fields match exporter fields.

## Documentation Rule

- Engine handoff changes should update [Engine Change Impact And Acceptance Checklist](./engine-change-impact-checklist.md) when acceptance evidence or required checks change.
- Device chain changes must update this page.
- Template or Flow changes must update [Algorithms & Templates](../algorithms/README.md).
- Project business field changes must update [Project Packages](../projects/README.md) and the corresponding project page.
- Plugin loading rule changes must update [Plugins](../plugins/README.md).

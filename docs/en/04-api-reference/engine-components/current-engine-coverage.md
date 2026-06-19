# Current Engine Documentation Coverage

This page answers one handoff question: does the current `Engine/` business logic have a documented entry point? It is not a file-by-file API list. It maps real Engine projects and key `ColorVision.Engine` directories to the handoff pages a maintainer should read first.

## Current Coverage

| Engine project | Project file | README | Current docs page | Handoff entry | Result |
| --- | --- | --- | --- | --- | --- |
| `Engine/ColorVision.Engine/` | `ColorVision.Engine.csproj` | present | [ColorVision.Engine](./ColorVision.Engine.md) | [business matrix](./business-flow-matrix.md), [scenario playbook](./business-scenario-playbook.md), [runtime object map](./runtime-object-map.md) | main runtime covered |
| `Engine/FlowEngineLib/` | `FlowEngineLib.csproj` | present | [FlowEngineLib](./FlowEngineLib.md) | [templates and Flow chain](./template-flow-chain.md) | Flow execution covered |
| `Engine/cvColorVision/` | `cvColorVision.csproj` | present | [cvColorVision](./cvColorVision.md) | [result handoff chain](./result-handoff-chain.md) | native/vision boundary documented |
| `Engine/ColorVision.FileIO/` | `ColorVision.FileIO.csproj` | present | [ColorVision.FileIO](./ColorVision.FileIO.md) | [data export/import](../../01-user-guide/data-management/export-import.md), result chain | file I/O covered |
| `Engine/ST.Library.UI/` | `ST.Library.UI.csproj` | present | [ST.Library.UI](./ST.Library.UI.md) | [templates and Flow chain](./template-flow-chain.md) | node-editor UI base covered |
| `Engine/ColorVision.ShellExtension/` | `ColorVision.ShellExtension.csproj` | missing | [ColorVision.ShellExtension](./ColorVision.ShellExtension.md) | shell thumbnail extension page, [ColorVision.FileIO](./ColorVision.FileIO.md) | external Explorer integration covered |

## `ColorVision.Engine` Business Directory Coverage

| Source directory | Business meaning | Current handoff page | First handoff question |
| --- | --- | --- | --- |
| `Services/` | service management, device base types, terminal, cache, RC service | [device service chain](./device-service-chain.md), [business matrix](./business-flow-matrix.md) | Can the resource create the correct `DeviceService`? |
| `Services/Devices/` | Camera, Motor, SMU, FileServer, FlowDevice, and other devices | [device service chain](./device-service-chain.md) | Do manual actions and Flow nodes reference the same device? |
| `Templates/` | template parameters, Flow templates, algorithm templates, POI/ROI, ARVR templates | [templates and Flow chain](./template-flow-chain.md), [result handoff chain](./result-handoff-chain.md) | Are template version, node binding, and result mapping aligned? |
| `FlowEngineLib/Node/Algorithm/`, `FlowEngineLib/Algorithm/` | Flow algorithm, conversion, and calibration nodes | [Flow Conversion And Calibration Nodes](./flow-conversion-calibration-nodes.md), [templates and Flow chain](./template-flow-chain.md) | Do `operatorCode`, parameter object, and configurator agree? |
| `MQTT/` | MQTT configuration, connection, control objects | [device service chain](./device-service-chain.md), [scenario playbook](./business-scenario-playbook.md) | Do topic, connection state, and device Code match? |
| `Batch/`, `Dao/`, `Mysql/` | batches, result records, MySQL/SQLite data access | [result handoff chain](./result-handoff-chain.md) | Was data written, and do batch/SN match? |
| `Messages/` | MQTT and business message models | [business matrix](./business-flow-matrix.md) | Which message model does the project or external system use? |
| `Archive/`, `Reports/` | archived result lookup and report generation | [result handoff chain](./result-handoff-chain.md) | Do result source, fields, path, and report version match? |
| `ToolPlugins/` | built-in tools such as ImageJ and CVRaw-to-CSV | [scenario playbook](./business-scenario-playbook.md), [ColorVision.Engine](./ColorVision.Engine.md) | Is the tool a debug helper or a production deliverable? |
| `Abstractions/`, `PropertyEditor/`, `Utilities/` | shared interfaces, property editing, utilities | [runtime object map](./runtime-object-map.md) | Is it called directly by a business chain? |
| `Assets/`, `Properties/`, `CalFile/`, `Media/` | resources, properties, calibration/media helper files | referenced by scenario pages | Must the file be copied with the package? |
| `bin/`, `obj/` | build output and intermediates | not documentation objects | Do not hand-maintain or use as business evidence |

## Handoff Reading Order

1. If ownership is unclear, start with [Engine Business Flow Matrix](./business-flow-matrix.md).
2. For a known scenario such as device addition, template change, Flow failure, or missing result, use [Engine Business Scenario Playbook](./business-scenario-playbook.md).
3. If the change is complete or ready for handoff, use [Engine Change Impact And Acceptance Checklist](./engine-change-impact-checklist.md) to collect evidence.
4. If you know the class or runtime object, use [Engine Runtime Object Map](./runtime-object-map.md).
5. If you know the chain type, use [Device Service Chain](./device-service-chain.md), [Templates And Flow Chain](./template-flow-chain.md), or [Result Display And Project Handoff](./result-handoff-chain.md).
6. For customer project output, continue to [Project Capability & Handoff Matrix](../projects/project-capability-matrix.md) and the specific project page.

## Coverage Check

```powershell
Get-ChildItem Engine -Directory | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem Engine/ColorVision.Engine -Directory | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem docs/04-api-reference/engine-components -File | Sort-Object Name | Select-Object -ExpandProperty Name
```

When adding an Engine project, device type, template directory, or result chain, update this page, [Engine Business Flow Matrix](./business-flow-matrix.md), [Engine Business Scenario Playbook](./business-scenario-playbook.md), [Engine Change Impact And Acceptance Checklist](./engine-change-impact-checklist.md), and the relevant chain page.

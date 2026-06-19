# Engine Development Guide

Engine development starts by identifying the business chain being changed. Do not mix device services, templates, Flow nodes, algorithm results, and customer project judgement in one place.

## Read First

If you are taking over Engine work for the first time, start with:

- [Engine Business Handoff](../../04-api-reference/engine-components/business-handoff.md)
- [Engine Components & Handoff](../../04-api-reference/engine-components/README.md)
- [Engine Runtime Object Map](../../04-api-reference/engine-components/runtime-object-map.md)

The pages in this directory provide code-entry guidance for specific development topics.

## Common Change Points

| Goal | Main Folder | First Page |
| --- | --- | --- |
| Add or maintain device service | `Engine/ColorVision.Engine/Services/Devices/` | [Service Development Handoff](./services.md) |
| Add or maintain template | `Engine/ColorVision.Engine/Templates/` | [Template System Development Handoff](./templates.md) |
| Add or maintain Flow node | `Engine/ColorVision.Engine/Templates/Flow/`, `Engine/FlowEngineLib/` | [Engine Templates And Flow Chain](../../04-api-reference/engine-components/template-flow-chain.md) |
| Modify MQTT behavior | `Engine/ColorVision.Engine/MQTT/`, device service folders | [MQTT Message Processing Handoff](./mqtt.md) |
| Modify OpenCV/native integration | `Engine/cvColorVision/`, `UI/ColorVision.Core/`, `Engine/ColorVision.Engine/Media/` | [OpenCV And Native Integration Handoff](./opencv-integration.md) |
| Modify result display | `Templates/*/ViewHandle*.cs`, `UI/ColorVision.ImageEditor/` | [Engine Result Display And Project Handoff](../../04-api-reference/engine-components/result-handoff-chain.md) |
| Choose validation commands | `Test/`, backend, scripts, docs | [Testing and Validation Handoff](../testing.md) |

## Development Validation

At minimum, validate the module you touched, the host, and one end-to-end scenario:

```powershell
dotnet build Engine/ColorVision.Engine/ColorVision.Engine.csproj -c Release -p:Platform=x64
dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64
```

For native/OpenCV changes, also use the commands in [OpenCV And Native Integration Handoff](./opencv-integration.md). For documentation changes, run `npm run docs:build`.

## Maintenance Rules

- Device services handle device state, commands, configuration, and UI. They do not own customer judgement.
- Templates handle parameters, editing, persistence, and algorithm command inputs. They do not own final CSV/PDF/MES formats.
- Flow nodes are visual execution units. Result interpretation belongs to template/result/project layers.
- Project packages own customer Process, Recipe, Fix, protocol, and exporter rules.
- Every Engine change should update the matching handoff page when behavior, validation, or runtime ownership changes.

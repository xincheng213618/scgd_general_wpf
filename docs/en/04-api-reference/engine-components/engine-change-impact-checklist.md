# Engine Change Impact And Acceptance Checklist

Use this page after every Engine business change. It does not replace the [Engine Business Scenario Playbook](./business-scenario-playbook.md); it turns "what changed, what is affected, and what evidence proves it" into a repeatable handoff checklist.

## When To Use It

| Scenario | How to use |
| --- | --- |
| Field defect | Identify the change type, then collect the minimum evidence listed here |
| New device, template, node, or result handler | Define acceptance before finishing code |
| Customer project field change | Verify both Engine source results and project export fields |
| Release or field replacement | Preserve build, configuration, result, and rollback evidence |
| Handoff to a new maintainer | Explain whether the change belongs to Engine, a project package, a plugin, or UI |

## Ask Four Questions First

| Question | If yes | Ownership |
| --- | --- | --- |
| Does this change device resources, connection, status, or commands | Yes | `Engine/ColorVision.Engine/Services/` |
| Does this change template parameters, Flow nodes, or `.cvflow` import/export | Yes | `Templates/`, `FlowEngineLib/`, `Templates/Flow/NodeConfigurator/` |
| Does this change generic result reading, overlay, or historical result display | Yes | Engine result handlers and ImageEditor |
| Does this only change customer CSV/PDF/MES/Socket fields | Yes | `Projects/<Project>/Process`, `Recipe`, `Fix`, exporters |

Customer-specific rules should not go into generic Engine handlers. Generic devices, templates, Flow nodes, or result handlers should not live only inside a project window.

## Change Impact Quick Map

| Change type | Direct code | Upstream to check | Downstream to check | Minimum acceptance |
| --- | --- | --- | --- | --- |
| New device type | `ServiceTypes`, `DeviceServiceFactoryRegistry`, `DeviceXxx` | `SysResourceModel`, device config, MQTT/service side | Device page, Flow node configurator, project calls | Create resource, service instance, device page, minimal command, Flow binding |
| Device command change | `Services/Devices/*/MQTT*.cs` or service method | MQTT connection, device Code, service token | Flow status, result ID, project flow | Run manual command and Flow node command |
| New template parameter | `Template*.cs`, parameter model, editor control | Old template data, defaults, `TemplateDicId` | Flow node persistence, algorithm command, result handler | Create, edit, save, copy, import, open old data |
| New Flow node | `FlowEngineLib`, `Templates/Flow/Nodes`, `NodeConfigurator` | Node inputs/outputs, device/template lists | `.cvflow` save/import, `FlowCompleted`, project result reading | Open, edit, save, reopen, import, execute |
| Result display change | `IResultHandleBase`, `IViewResult`, DAO, `ViewHandleXxx` | `ViewResultAlgType`, result master row, file path | ImageEditor overlay, historical result page, project export | Open historical result, check overlay coordinates, table/sidebar, project CSV |
| Project result field change | Project `Process`, `Recipe`, `Fix`, exporter | Engine source result, batch/SN, template name | CSV/PDF/MES/Socket, customer sample | Run full project flow for one SN and compare with customer sample |
| Remote service chain change | `MQTTControl`, `MqttRCService`, device `MQTT*.cs` | broker, topic, service token, file server | Result query, Flow state, retry/timeout | Cover disconnected, timeout, success, and failure paths |
| File format or image I/O change | `ColorVision.FileIO`, `cvColorVision`, FileServer, Media | Source file, native DLL, cache directory | ImageEditor, Shell thumbnail, result handler | Open sample, export, reopen, verify thumbnail or overlay |

## Evidence Package Template

| Evidence | Content | Suggested location |
| --- | --- | --- |
| Change note | Business goal, trigger, affected modules, rollback path | PR or handoff document |
| Input sample | device Code, template name, Flow name, SN, batch id, result id, input file | Test record or field package |
| Output sample | UI state, Flow status, result table, overlay, CSV/PDF/MES/Socket response | Release evidence folder |
| Config snapshot | device config, MQTT, template JSON, `.cvflow`, project Recipe/Fix | Release package or config backup |
| Build info | `.csproj` version, DLL FileVersion, package name, build command | Release record |
| Documentation sync | Whether this page, chain pages, project pages, plugin pages, and user manual were updated | Documentation diff |

An acceptance record without batch id, SN, template name, or result id usually cannot prove that the Engine chain actually ran.

## Layered Acceptance

### Device Services

| Check | Pass criteria |
| --- | --- |
| Resource creation | Database resource exists and `type` maps to `ServiceTypes` |
| Service instance | `ServiceManager.DeviceServices` contains the target service |
| Display page | Device page opens and status refreshes |
| Command | Manual command succeeds; failure logs include device Code and reason |
| Flow binding | Node property can select the device and persists after reopen |
| Project package | If used by a project, the smallest project flow completes once |

### Templates And Flow

| Check | Pass criteria |
| --- | --- |
| Template scan | `TemplateControl` loads the target template |
| Parameter editing | PropertyGrid or custom editor explains the field |
| Save/reopen | Template and Flow parameters survive save and reopen |
| Import/export | `.cvflow` exported and imported into a clean environment restores linked templates |
| Execution | `FlowControl.FlowCompleted` returns status, SN, and parameters |
| Old data | Old templates, old flows, and old project config still open |

### Result Display

| Check | Pass criteria |
| --- | --- |
| Result master | `ViewResultAlg` can be found by batch or result id |
| Handler match | `CanHandle` / `CanHandle1` matches the target `ViewResultAlgType` |
| Detail read | DAO or model fills an `IViewResult` collection |
| Image path | ImageEditor opens the image or result file |
| Overlay | Coordinates, scaling, ROI/POI/curves are correct for the current image size |
| Project mapping | Project package exports non-empty fields from the same result |

### Project Outputs

| Check | Pass criteria |
| --- | --- |
| SN/batch | Project flow SN matches the Engine batch |
| Process | Project `Process` reads the correct template name, key, and result type |
| Recipe/Fix | Judgment and correction rules have versioned evidence |
| Export | CSV/PDF/XLSX order matches customer sample |
| External response | Socket/MES response is emitted after final result completion |
| Regression | Covers PASS, NG, algorithm failure, and external timeout |

## Handoff Focus By Role

| Role | Focus | Should not own |
| --- | --- | --- |
| Engine developer | Devices, templates, Flow, result handlers, DAO | Customer protocol field order |
| Project package developer | Process, Recipe, Fix, exports, Socket/MES | Generic device factories and generic overlay rules |
| UI developer | Device pages, template editors, ImageEditor overlays, status bar | Algorithm service business judgment |
| Plugin developer | manifest, menu entry, dependencies, packaging | Engine main business initialization |
| Field delivery | config, packages, samples, rollback, acceptance records | Ad-hoc template/database structure changes |

## Documentation Sync Rules

| Change | Must update |
| --- | --- |
| New device or service type | [Device Service Chain](./device-service-chain.md), [Business Flow Matrix](./business-flow-matrix.md), user manual device page |
| New template or Flow node | [Templates And Flow Chain](./template-flow-chain.md), [Algorithms And Templates](../algorithms/README.md), extension docs |
| New result handler | [Result Display And Project Handoff](./result-handoff-chain.md), UI ImageEditor docs |
| Project field change | [Project Guide](../../00-projects/README.md), the project page, project capability matrix |
| Plugin or release script change | [Plugin Development Manual](../../02-developer-guide/plugin-development/README.md), [Existing Plugin Capabilities](../plugins/README.md) |
| UI DLL publishing change | [UI DLL Publishing](../ui-components/publishing.md), [UI Component Release Matrix](../ui-components/release-matrix.md) |

## Final-Mile Check

Before handoff, confirm:

1. The change is assigned to Engine, project package, plugin, or UI.
2. The smallest business flow was executed, not only compiled.
3. Evidence includes SN, batch, template name, result id, or file path.
4. UI display and project export were both checked.
5. Release package contains required config, native DLLs, templates, `.cvflow`, or README/CHANGELOG.
6. This page and the relevant chain pages were updated.
7. Documentation build passes with `npm run docs:build`.

## Continue Reading

- [Engine Business Flow Matrix](./business-flow-matrix.md)
- [Engine Business Scenario Playbook](./business-scenario-playbook.md)
- [Engine Business Handoff](./business-handoff.md)
- [Engine Device Service Chain](./device-service-chain.md)
- [Engine Templates And Flow Chain](./template-flow-chain.md)
- [Engine Result Display And Project Handoff](./result-handoff-chain.md)
- [Project Capability And Handoff Matrix](../projects/project-capability-matrix.md)

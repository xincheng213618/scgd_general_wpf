# Project Guide

This chapter starts from customer projects rather than source directories. The `Projects/` folder contains customer packages, business solution bundles, and integration demos. They combine Engine, Flow templates, devices, Socket/MES protocols, and result export into deliverable production workflows.

For a first handoff, read this page to build the project map, then read the [Project Capability & Handoff Matrix](../04-api-reference/projects/project-capability-matrix.md), [Project Package Runtime And Handoff Playbook](../04-api-reference/projects/project-package-playbook.md), and [Project Package Release Evidence Checklist](../04-api-reference/projects/project-release-evidence.md). For the shared execution chain, continue with [Project Package Handoff Manual](../04-api-reference/projects/project-handoff.md), then open the specific project page or source folder.

## Current Project Map

| Project | Business positioning | Start here |
| --- | --- | --- |
| ProjectARVR | Early AR/VR optical test package with fixed PG switching, Socket events, and `ObjectiveTestResult` aggregation | [ProjectARVR](../04-api-reference/projects/project-arvr.md) |
| ProjectARVRLite | Lightweight AR/VR quick test package with configurable test types, preprocessing, Socket PG switching, and result CSV | [ProjectARVRLite](../04-api-reference/projects/project-arvr-lite.md) |
| ProjectARVRPro | Main professional AR/VR package with flow groups, Recipe, picture switching, Socket integration, and multiple output formats | [ProjectARVRPro](../04-api-reference/projects/project-arvr-pro.md) |
| ProjectARVRPro.IntegrationDemo | TCP/JSON integration sample for customers, MES, PLC, or automation controllers | [Integration Demo](../04-api-reference/projects/project-arvr-pro-integration-demo.md) |
| ProjectBlackMura | Display panel Black Mura inspection package with PG serial switching, five-color flow, and Excel reports | [ProjectBlackMura](../04-api-reference/projects/project-black-mura.md) |
| ProjectHeyuan | Heyuan Jingdian custom four-point WBRO color/luminance test with STX/ETX serial upload and CSV trace | [ProjectHeyuan](../04-api-reference/projects/project-heyuan.md) |
| ProjectKB | Keyboard backlight luminance and uniformity package with Modbus trigger, MES DLL, backlight autotune, CSV, and summary output | [ProjectKB](../04-api-reference/projects/project-kb.md) |
| ProjectLUX | LUX optical automation package for luminance, color, MTF, distortion, optical center, VID, and luminous flux | [ProjectLUX](../04-api-reference/projects/project-lux.md) |
| ProjectShiyuan | Shiyuan custom JND/POI export and fixed-image post-processing package | [ProjectShiyuan](../04-api-reference/projects/project-shiyuan.md) |

## What a Project Page Must Answer

| Question | Documentation should explain |
| --- | --- |
| What field problem does this project solve? | Customer scenario, test object, entry window, and main workflow |
| How does the outside system trigger it? | Socket, MES, serial, Modbus, or local button entry |
| How are runtime steps organized? | `Process/`, flow groups, Recipe/Fix, template binding |
| How are results judged and exported? | PASS/FAIL, CSV/XLSX/PDF, SQLite, Socket response fields |
| What must be delivered? | manifest, README, CHANGELOG, config, image assets, dependent DLLs |
| What breaks most often? | Protocol fields, legacy format compatibility, flow order, result fields, project config |

## Recommended Reading Order

1. [Project Capability & Handoff Matrix](../04-api-reference/projects/project-capability-matrix.md): compare trigger mode, output path, and delivery risk across all projects.
2. [Project Package Runtime And Handoff Playbook](../04-api-reference/projects/project-package-playbook.md): handle concrete issues around external triggers, flow groups, templates, Recipe/Fix, export, and packaging.
3. [Project Package Release Evidence Checklist](../04-api-reference/projects/project-release-evidence.md): record manifest, DLL, `.cvxp`, config, protocol, result samples, and rollback evidence.
4. [Project Package Handoff Manual](../04-api-reference/projects/project-handoff.md): understand the common loading, flow, configuration, and packaging chain.
5. [Project Package Overview](../04-api-reference/projects/README.md): confirm the projects that exist in this repository.
6. [Current Project Documentation Coverage](../04-api-reference/projects/current-project-coverage.md): verify that each `Projects/<Name>/` directory has a matching page.
7. Specific project page or source folder: read business protocol, flow groups, result export, and maintenance risks.
8. [Engine Business Flow Matrix](../04-api-reference/engine-components/business-flow-matrix.md): use this when the project reaches devices, templates, Flow, or result display.
9. [Existing Plugin Capabilities](../04-api-reference/plugins/README.md): compare general plugins with customer project packages.

## Project Packages vs General Plugins

| Type | Location | Goal |
| --- | --- | --- |
| Project package | `Projects/<Name>/` | Deliver a customer workflow, usually including flow groups, Recipe, protocol, and result export |
| General plugin | `Plugins/<Name>/` | Provide reusable tools such as spectrometer, system monitoring, or event viewing |
| Engine module | `Engine/` | Provide device, template, Flow, MQTT, data, and result-display capabilities |
| UI module | `UI/` | Provide reusable UI components, themes, windows, property editors, and image editor |

Customer-specific logic should normally stay in the project package. Shared logic should move to Engine, UI, or a general plugin only when multiple projects genuinely reuse it.

## Maintenance Rules

- Adding `Projects/<Name>/` requires updating this page, the project overview, [Current Project Documentation Coverage](../04-api-reference/projects/current-project-coverage.md), the capability matrix, and a specific project page.
- Protocol, result-output, acceptance, or packaging changes must update the [Project Package Runtime And Handoff Playbook](../04-api-reference/projects/project-package-playbook.md), [Project Capability & Handoff Matrix](../04-api-reference/projects/project-capability-matrix.md), and [Project Package Release Evidence Checklist](../04-api-reference/projects/project-release-evidence.md).
- Socket/MES fields, flow groups, Recipe/Fix, export fields, or manifest changes must update the corresponding project page.
- Project pages must describe the business chain and delivery risks, not only file names.
- Historical customer conversations are not current product commitments unless they can be matched to source, config, or manifest.
